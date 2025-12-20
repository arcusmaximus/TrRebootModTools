using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using TrRebootTools.Shared.Cdc.Rise;
using TrRebootTools.Shared.Cdc.Shadow;
using TrRebootTools.Shared.Cdc.Tr2013;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Cdc
{
    public abstract class ArchiveSet : IDisposable
    {
        public const string ModArchivePrefix = "bigfile._mod.";
        public const string OriginalGameArchivePrefix = "bigfile._orig.";

        private readonly object _lock = new();
        private readonly Dictionary<(int, int), Archive> _archives = new();
        private readonly List<Archive> _duplicateArchives = new List<Archive>();
        private readonly Dictionary<ArchiveFileKey, ArchiveFileReference> _files = new();

        public static ArchiveSet Open(string folderPath, bool includeGame, bool includeMods, CdcGame game)
        {
            return game switch
            {
                CdcGame.Tr2013 => new Tr2013ArchiveSet(folderPath, includeGame, includeMods),
                CdcGame.Rise => new RiseArchiveSet(folderPath, includeGame, includeMods),
                CdcGame.Shadow => new ShadowArchiveSet(folderPath, includeGame, includeMods)
            };
        }

        protected ArchiveSet(string folderPath, bool includeGame, bool includeMods)
        {
            FolderPath = folderPath;

            EnsureFlattenedModArchiveBackup();

            Dictionary<int, ArchiveMetaData> metaDatas = new();
            foreach (string nfoFilePath in Directory.EnumerateFiles(folderPath))
            {
                if (!nfoFilePath.EndsWith(".nfo") && !nfoFilePath.EndsWith(".nfo.disabled"))
                    continue;

                ArchiveMetaData metaData = ArchiveMetaData.Load(nfoFilePath);
                metaDatas[metaData.DlcIndex] = metaData;
            }

            foreach (string archiveFilePath in Directory.EnumerateFiles(folderPath, "*.000.tiger", SearchOption.AllDirectories))
            {
                if (!ShouldLoad(archiveFilePath, includeGame, includeMods))
                    continue;

                using Stream stream = File.OpenRead(archiveFilePath);
                using BinaryReader reader = new BinaryReader(stream);
                var header = reader.ReadStruct<Archive.ArchiveHeader>();
                ArchiveMetaData metaData = metaDatas?.GetOrDefault(header.Id);
                if (metaData != null || !SupportsMetaData)
                    Load(archiveFilePath, metaData);
            }

            foreach (Archive archive in GetSortedArchives())
            {
                foreach (ArchiveFileReference file in archive.Files)
                {
                    _files[file] = file;
                }
            }
        }

        public abstract CdcGame Game { get; }

        protected abstract bool SupportsMetaData { get; }

        public string FolderPath
        {
            get;
        }

        public IReadOnlyCollection<Archive> Archives => _archives.Values;

        public IReadOnlyCollection<Archive> DuplicateArchives => _duplicateArchives;

        public Archive GetArchive(int id, int subId)
        {
            return _archives.GetOrDefault((id, subId));
        }

        public IReadOnlyCollection<ArchiveFileReference> Files => _files.Values;

        private void EnsureFlattenedModArchiveBackup()
        {
            string archiveFileName = GetActiveFlattenedModArchiveIdentity()?.FileName;
            if (archiveFileName == null)
                return;

            string archiveFilePath = Path.Combine(FolderPath, archiveFileName);
            string backupFilePath = Path.Combine(FolderPath, OriginalGameArchivePrefix + archiveFileName);
            if (File.Exists(archiveFilePath) && !File.Exists(backupFilePath))
                File.Copy(archiveFilePath, backupFilePath);
        }

        private bool ShouldLoad(string archiveFilePath, bool includeGame, bool includeMods)
        {
            string archiveFileName = Path.GetFileName(archiveFilePath);
            if (archiveFileName.StartsWith(ModArchivePrefix) ? !includeMods : !includeGame)
                return false;

            if (archiveFileName.StartsWith(OriginalGameArchivePrefix) &&
                archiveFileName.Substring(OriginalGameArchivePrefix.Length) != GetActiveFlattenedModArchiveIdentity()?.FileName)
            {
                return false;
            }

            if (GetAllFlattenedModArchiveIdentities().Any(i => i.FileName == archiveFileName))
                return false;

            return true;
        }

        private void Load(string archiveFilePath, ArchiveMetaData metaData)
        {
            Archive archive = Archive.Open(archiveFilePath, metaData, Game);
            var archiveKey = (archive.Id, archive.SubId);
            if (!_archives.ContainsKey(archiveKey))
                _archives.Add(archiveKey, archive);
            else
                _duplicateArchives.Add(archive);
        }

        public Archive CreateFlattenedModArchive(int maxFiles)
        {
            ArchiveIdentity ident = GetActiveFlattenedModArchiveIdentity();
            return Archive.Create(Path.Combine(FolderPath, ident.FileName), ident.Id, 0, null, maxFiles, Game);
        }

        public Dictionary<ulong, Archive> CreateModArchives(string modName, Dictionary<ulong, int> maxFilesByLocale)
        {
            lock (_lock)
            {
                string simplifiedName = Regex.Replace(modName, @"[^-.\w]", "_").ToLower();
                simplifiedName = Regex.Replace(simplifiedName, @"__+", "_").Trim('_');

                int gameId = _archives.Values.Select(a => a.MetaData?.GameId ?? 0).FirstOrDefault(id => id > 0);
                int version = _archives.Values.Max(a => a.MetaData?.Version ?? 0) + 1;
                int id = _archives.Values.Max(a => a.Id) + 1;
                string steamId = _archives.Values
                                          .SelectMany(a => (IEnumerable<string>)a.MetaData?.CustomEntries ?? [])
                                          .FirstOrDefault(c => c.StartsWith("steamID:"));

                string nfoFilePath = Path.Combine(FolderPath, $"{ModArchivePrefix}{simplifiedName}.000.000.nfo");
                ArchiveMetaData metaData = ArchiveMetaData.Create(nfoFilePath, gameId, version, id, id);
                if (steamId != null)
                    metaData.CustomEntries.Add(steamId);

                metaData.CustomEntries.Add("mod:" + modName);
                metaData.Save();

                int subId = 0;

                Dictionary<ulong, Archive> archives = new();
                SpecMasksToc toc = new SpecMasksToc();
                foreach (var localesOfSuffix in maxFilesByLocale.Keys.GroupBy(MakeLocaleSuffix))
                {
                    string archiveFileName = $"{ModArchivePrefix}{simplifiedName}.000{localesOfSuffix.Key}.000.tiger";
                    int maxFiles = localesOfSuffix.Sum(l => maxFilesByLocale[l]);
                    Archive archive = Archive.Create(Path.Combine(FolderPath, archiveFileName), id, subId, metaData, maxFiles, Game);
                    subId++;

                    foreach (ulong locale in localesOfSuffix)
                    {
                        toc.Entries.Add(locale, archiveFileName);
                        archives.Add(locale, archive);
                    }
                }

                if (toc.Entries.Count > 1 && RequiresSpecMaskFiles)
                    toc.Write(SpecMasksToc.GetFilePathForArchive(Path.ChangeExtension(nfoFilePath, ".tiger")));

                return archives;
            }
        }

        public virtual ICollection<ArchiveIdentity> GetAllFlattenedModArchiveIdentities()
        {
            return [];
        }

        public virtual ArchiveIdentity GetActiveFlattenedModArchiveIdentity()
        {
            return null;
        }

        protected abstract string MakeLocaleSuffix(ulong locale);

        protected abstract bool RequiresSpecMaskFiles { get; }

        public void Add(Archive archive, ResourceUsageCache gameResourceUsageCache, ITaskProgress progress, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                _archives.Add((archive.Id, archive.SubId), archive);
                UpdateResourceReferences(gameResourceUsageCache, progress, cancellationToken);
            }
        }

        public void Enable(int archiveId, ResourceUsageCache gameResourceUsageCache, ITaskProgress progress, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                Archive archive = _archives[(archiveId, 0)];
                if (archive.MetaData.Enabled)
                    return;

                try
                {
                    progress?.Begin($"Enabling mod {archive.ModName}...");

                    archive.MetaData.Enabled = true;
                    UpdateResourceReferences(gameResourceUsageCache, progress, cancellationToken);
                }
                finally
                {
                    progress?.End();
                }
            }
        }

        public void Disable(int archiveId, ResourceUsageCache gameResourceUsageCache, ITaskProgress progress, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                Archive archive = _archives[(archiveId, 0)];
                Disable(archiveId, gameResourceUsageCache, progress, $"Disabling mod {archive.ModName}...", cancellationToken);
            }
        }

        private void Disable(int archiveId, ResourceUsageCache gameResourceUsageCache, ITaskProgress progress, string statusText, CancellationToken cancellationToken)
        {
            Archive archive = _archives[(archiveId, 0)];
            if (!archive.MetaData.Enabled)
                return;

            try
            {
                progress?.Begin(statusText);
                archive.MetaData.Enabled = false;
                UpdateResourceReferences(gameResourceUsageCache, progress, cancellationToken);
            }
            finally
            {
                progress?.End();
            }
        }

        public void Delete(int archiveId, ResourceUsageCache gameResourceUsageCache, ITaskProgress progress, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                Disable(archiveId, gameResourceUsageCache, progress, $"Removing mod {_archives[(archiveId, 0)].ModName}...", cancellationToken);

                foreach (Archive archive in _archives.Values.Where(a => a.Id == archiveId).Concat(_duplicateArchives.Where(a => a.Id == archiveId)).ToList())
                {
                    archive.Delete();
                    archive.Dispose();
                    if (!_duplicateArchives.Remove(archive))
                        _archives.Remove((archive.Id, archive.SubId));
                }
            }
        }

        private void UpdateResourceReferences(ResourceUsageCache gameResourceUsageCache, ITaskProgress progress, CancellationToken cancellationToken)
        {
            List<Archive> sortedArchives = GetSortedArchives();
            int firstModArchiveIdx = sortedArchives.IndexOf(a => a.ModName != null);
            if (firstModArchiveIdx < 0)
                firstModArchiveIdx = sortedArchives.Count;

            _files.Clear();
            foreach (Archive archive in sortedArchives.Take(firstModArchiveIdx))
            {
                foreach (ArchiveFileReference file in archive.Files)
                {
                    _files[file] = file;
                }
            }

            int numTotalFiles = 0;
            foreach (Archive archive in sortedArchives.Skip(firstModArchiveIdx))
            {
                numTotalFiles += archive.Files.Count;
            }

            ResourceUsageCache resourceUsageCache = new ResourceUsageCache(gameResourceUsageCache);

            int numUpdatedFiles = 0;
            HashSet<int> archiveIds = sortedArchives.Select(a => a.Id).ToHashSet();
            Dictionary<ResourceKey, ResourceReference> modResourceRefs = new();
            foreach (Archive archive in sortedArchives.Skip(firstModArchiveIdx))
            {
                foreach (ArchiveFileReference file in archive.Files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    numUpdatedFiles++;
                    progress?.Report((float)numUpdatedFiles / numTotalFiles);

                    ResourceCollection collection = archive.GetResourceCollection(file);
                    if (collection == null)
                    {
                        _files[file] = file;
                        continue;
                    }

                    bool changesMade = false;
                    for (int resourceIdx = 0; resourceIdx < collection.ResourceReferences.Count; resourceIdx++)
                    {
                        ResourceReference resourceRef = collection.ResourceReferences[resourceIdx];
                        if (resourceRef.ArchiveId == archive.Id && resourceRef.ArchiveSubId == archive.SubId)
                        {
                            resourceUsageCache.AddResourceReference(this, collection, resourceIdx);
                            modResourceRefs[resourceRef] = resourceRef;
                        }
                        else if (modResourceRefs.TryGetValue(resourceRef, out ResourceReference modResourceRef) &&
                                 (resourceRef.ArchiveId != modResourceRef.ArchiveId || resourceRef.ArchiveSubId != modResourceRef.ArchiveSubId))
                        {
                            collection.UpdateResourceReference(resourceIdx, modResourceRef);
                            changesMade = true;
                        }
                        else if (!archiveIds.Contains(resourceRef.ArchiveId))
                        {
                            ResourceReference gameResourceRef = resourceUsageCache.GetResourceReference(this, resourceRef);
                            if (gameResourceRef == null)
                            {
                                gameResourceRef = new ResourceReference(
                                    resourceRef.Type,
                                    resourceRef.SubType,
                                    resourceRef.Id,
                                    resourceRef.Locale,
                                    sortedArchives[0].Id,
                                    sortedArchives[0].SubId,
                                    0,
                                    0,
                                    0,
                                    0,
                                    0,
                                    0
                                ) { Enabled = false };
                            }
                            collection.UpdateResourceReference(resourceIdx, gameResourceRef);
                            changesMade = true;
                        }
                    }

                    if (changesMade)
                    {
                        using Stream collectionStream = archive.OpenFile(file);
                        collection.Write(collectionStream);
                    }

                    _files[file] = file;
                }
            }
        }

        public ArchiveFileReference GetFileReference(ArchiveFileKey fileId)
        {
            return GetFileReference(fileId.NameHash, fileId.Locale);
        }

        public ArchiveFileReference GetFileReference(ulong nameHash, ulong locale = 0xFFFFFFFFFFFFFFFF)
        {
            return _files.GetOrDefault(new ArchiveFileKey(nameHash, locale));
        }

        public ArchiveFileReference GetFileReference(string name, ulong locale = 0xFFFFFFFFFFFFFFFF)
        {
            return GetFileReference(CdcHash.Calculate(name, Game), locale);
        }

        public ResourceCollection GetResourceCollection(ArchiveFileReference file)
        {
            return _archives[(file.ArchiveId, file.ArchiveSubId)].GetResourceCollection(file);
        }

        public ResourceCollection GetResourceCollection(ulong nameHash, ulong locale = 0xFFFFFFFFFFFFFFFF)
        {
            ArchiveFileReference fileRef = GetFileReference(nameHash, locale);
            return fileRef != null ? GetResourceCollection(fileRef) : null;
        }

        public ResourceCollection GetResourceCollection(string name, ulong locale = 0xFFFFFFFFFFFFFFFF)
        {
            ArchiveFileReference fileRef = GetFileReference(name, locale);
            return fileRef != null ? GetResourceCollection(fileRef) : null;
        }

        public Stream OpenFile(ArchiveFileReference fileRef)
        {
            return _archives[(fileRef.ArchiveId, fileRef.ArchiveSubId)].OpenFile(fileRef);
        }

        public Stream OpenResource(ResourceReference resourceRef)
        {
            return _archives[(resourceRef.ArchiveId, resourceRef.ArchiveSubId)].OpenResource(resourceRef);
        }

        public virtual List<Archive> GetSortedArchives()
        {
            List<Archive> sortedArchives = _archives.Values.Where(a => a.MetaData?.Enabled ?? false).ToList();
            sortedArchives.Sort(CompareArchivePriority);
            return sortedArchives;
        }

        private static int CompareArchivePriority(Archive a, Archive b)
        {
            int comparison = a.MetaData.Version.CompareTo(b.MetaData.Version);
            if (comparison != 0)
                return comparison;

            comparison = b.MetaData.Required.CompareTo(a.MetaData.Required);
            if (comparison != 0)
                return comparison;

            comparison = a.MetaData.PackageId.CompareTo(b.MetaData.PackageId);
            if (comparison != 0)
                return comparison;

            comparison = a.MetaData.Chunk.CompareTo(b.MetaData.Chunk);
            if (comparison != 0)
                return comparison;

            return a.SubId.CompareTo(b.SubId);
        }

        public void CloseStreams()
        {
            foreach (Archive archive in _archives.Values.Concat(_duplicateArchives))
            {
                archive.CloseStreams();
            }
        }

        public void Dispose()
        {
            foreach (Archive archive in _archives.Values.Concat(_duplicateArchives))
            {
                archive.Dispose();
            }
            _archives.Clear();
            _duplicateArchives.Clear();
        }
    }
}
