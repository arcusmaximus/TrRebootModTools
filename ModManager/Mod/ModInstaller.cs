using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using TrRebootTools.Shared;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Util;
using TrRebootTools.ModManager.Util;

namespace TrRebootTools.ModManager.Mod
{
    internal class ModInstaller
    {
        private record struct ResourceCollectionItem(ResourceCollection Collection, int ResourceIndex);

        private readonly ArchiveSet _archiveSet;
        private readonly ResourceUsageCache _gameResourceUsageCache;

        public ModInstaller(ArchiveSet archiveSet, ResourceUsageCache gameResourceUsageCache)
        {
            _archiveSet = archiveSet;
            _gameResourceUsageCache = gameResourceUsageCache;
        }

        public InstalledMod InstallFromZip(string filePath, ITaskProgress progress, CancellationToken cancellationToken)
        {
            string modName = Regex.Replace(Path.GetFileNameWithoutExtension(filePath), @"-\d+$", "");
            if (IsModAlreadyInstalled(modName))
                return null;

            using ZipTempExtractor extractor = new ZipTempExtractor(filePath);
            extractor.Extract(progress, cancellationToken);

            using ModPackage modPackage = new FolderModPackage(modName, extractor.FolderPath, _archiveSet, _gameResourceUsageCache);
            return Install(modPackage, progress, cancellationToken);
        }

        public InstalledMod InstallFromFolder(string folderPath, ITaskProgress progress, CancellationToken cancellationToken)
        {
            string modName = Path.GetFileName(folderPath);
            Archive existingArchive = _archiveSet.Archives.FirstOrDefault(a => a.ModName == modName);
            if (existingArchive != null)
                _archiveSet.Delete(existingArchive.Id, _gameResourceUsageCache, progress, cancellationToken);

            using ModPackage modPackage = new FolderModPackage(folderPath, _archiveSet, _gameResourceUsageCache);
            return Install(modPackage, progress, cancellationToken);
        }

        public void ReinstallAll(ITaskProgress progress, CancellationToken cancellationToken)
        {
            List<OriginalModInfo> originalMods = new();
            foreach (IGrouping<int, Archive> archivesOfId in _archiveSet.Archives
                                                                        .Concat(_archiveSet.DuplicateArchives)
                                                                        .Where(a => a.ModName != null)
                                                                        .GroupBy(a => a.Id)
                                                                        .OrderByDescending(g => g.First().MetaData.Version))
            {
                _archiveSet.CloseStreams();

                ArchiveMetaData metaData = archivesOfId.First().MetaData;
                OriginalModInfo originalModInfo = new OriginalModInfo
                {
                    ModName = archivesOfId.First().ModName,
                    Enabled = metaData.Enabled,
                    NfoFilePath = metaData.FilePath + ".orig",
                    ArchiveFilePaths = new List<string>()
                };

                _archiveSet.Disable(archivesOfId.Key, _gameResourceUsageCache, progress, cancellationToken);

                File.Delete(originalModInfo.NfoFilePath);
                File.Move(metaData.FilePath, originalModInfo.NfoFilePath);

                foreach (string archiveFilePath in archivesOfId.Select(a => a.BaseFilePath))
                {
                    string origFilePath = archiveFilePath + ".orig";
                    File.Delete(origFilePath);
                    File.Move(archiveFilePath, origFilePath);
                    originalModInfo.ArchiveFilePaths.Add(origFilePath);
                }

                _archiveSet.Delete(archivesOfId.Key, _gameResourceUsageCache, progress, cancellationToken);
                originalMods.Insert(0, originalModInfo);
            }

            foreach (OriginalModInfo originalMod in originalMods)
            {
                using (ModPackage modPackage = new TigerModPackage(originalMod.NfoFilePath, originalMod.ArchiveFilePaths, _archiveSet.Game))
                {
                    InstalledMod installedMod = Install(modPackage, null, false, progress, cancellationToken);
                    if (installedMod == null)
                        continue;

                    if (!originalMod.Enabled)
                        _archiveSet.Disable(installedMod.ArchiveId, _gameResourceUsageCache, progress, cancellationToken);
                }

                File.Delete(originalMod.NfoFilePath);
                foreach (string archiveFilePath in originalMod.ArchiveFilePaths)
                {
                    File.Delete(archiveFilePath);
                }
            }
        }

        public void UpdateFlatModArchive(ITaskProgress progress, CancellationToken cancellationToken)
        {
            _archiveSet.GetFlattenedModArchiveDetails(out int flattenedArchiveId, out string flattenedArchiveFileName);
            if (flattenedArchiveFileName == null)
                return;

            List<TigerModPackage> modPackages = new();

            Archive origGameArchive = _archiveSet.GetArchive(flattenedArchiveId, 0);
            modPackages.Add(new TigerModPackage(new[] { origGameArchive }, _archiveSet.Game));

            foreach (IGrouping<int, Archive> archivesOfId in _archiveSet.Archives
                                                                        .Where(a => a.ModName != null && a.MetaData.Enabled)
                                                                        .GroupBy(a => a.Id)
                                                                        .OrderBy(g => g.First().MetaData.Version))
            {
                modPackages.Add(new TigerModPackage(archivesOfId, _archiveSet.Game));
            }

            using var modPackage = new MultiTigerModPackage(flattenedArchiveFileName.Replace(".000.tiger", ""), modPackages);
            Install(modPackage, null, true, progress, cancellationToken);
        }

        private class OriginalModInfo
        {
            public string ModName;
            public bool Enabled;
            public string NfoFilePath;
            public List<string> ArchiveFilePaths;
        }

        private bool IsModAlreadyInstalled(string modName)
        {
            if (_archiveSet.Archives.Any(a => a.ModName == modName))
            {
                MessageBox.Show($"The mod {modName} is already installed.", "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return true;
            }
            return false;
        }

        private InstalledMod Install(ModPackage modPackage, ITaskProgress progress, CancellationToken cancellationToken)
        {
            ModVariation modVariation = null;
            if (modPackage.Variations != null && modPackage.Variations.Count > 0)
            {
                using VariationSelectionForm form = new VariationSelectionForm(modPackage);
                if (form.ShowDialog() != DialogResult.OK)
                    return null;

                modVariation = form.SelectedVariation;
            }
            return Install(modPackage, modVariation, false, progress, cancellationToken);
        }

        private InstalledMod Install(ModPackage modPackage, ModVariation modVariation, bool flattened, ITaskProgress progress, CancellationToken cancellationToken)
        {
            ArchiveSet archiveSet = null;
            Dictionary<ulong, Archive> archives = null;
            try
            {
                progress.Begin($"Installing mod {modPackage.Name}...");

                _archiveSet.CloseStreams();
                ResourceUsageCache resourceUsageCache = !flattened ? GetFullResourceUsageCache() : _gameResourceUsageCache;
                archiveSet = !flattened ? _archiveSet : ArchiveSet.Open(_archiveSet.FolderPath, true, false, _archiveSet.Game);

                List<ResourceKey> modResourceKeys = modPackage.Resources.ToList();
                if (modVariation != null)
                    modResourceKeys.AddRange(modVariation.Resources);

                Dictionary<ResourceKey, List<ResourceCollectionItemReference>> modResourceUsages =
                    modResourceKeys.ToDictionary(r => r, r => resourceUsageCache.GetResourceUsages(archiveSet, r).ToList());
                modResourceUsages.RemoveAll(p => p.Value.Count == 0);

                Dictionary<ArchiveFileKey, ResourceCollection> modResourceCollections = modResourceUsages.Values
                                                                                                         .SelectMany(c => c)
                                                                                                         .Select(c => c.CollectionReference)
                                                                                                         .Distinct()
                                                                                                         .ToDictionary(r => (ArchiveFileKey)r, archiveSet.GetResourceCollection);

                Dictionary<ResourceKey, List<ResourceCollectionItem>> modResourceCollectionItems =
                    modResourceUsages.ToDictionary(
                        p => p.Key,
                        p => p.Value
                              .Select(r => new ResourceCollectionItem(modResourceCollections[r.CollectionReference], r.ResourceIndex))
                              .ToList()
                );

                Dictionary<ArchiveFileKey, HashSet<ResourceKey>> resourceRefsToAdd = GetResourceRefsToAdd(modPackage, modVariation, modResourceKeys, resourceUsageCache);
                AddResourceReferencesToCollections(modResourceCollections, modResourceCollectionItems, resourceRefsToAdd);

                IEnumerable<ArchiveFileKey> fileKeys = modResourceCollections.Keys.Concat(modPackage.Files).Concat(modVariation?.Files ?? Enumerable.Empty<ArchiveFileKey>());
                Dictionary<ulong, int> fileCountsByLocale = fileKeys.GroupBy(f => f.Locale).ToDictionary(g => g.Key, g => g.Count());
                fileCountsByLocale.TryAdd(0xFFFFFFFFFFFFFFFF, 0);

                archives = !flattened ? archiveSet.CreateModArchives(modPackage.Name, fileCountsByLocale)
                                      : new() { { 0xFFFFFFFFFFFFFFFF, archiveSet.CreateFlattenedModArchive(fileCountsByLocale.Values.Sum()) } };

                AddResourcesToArchive(archives[0xFFFFFFFFFFFFFFFF], modPackage, modVariation, modResourceCollectionItems, progress, cancellationToken);
                AddFilesToArchives(archives, modPackage, modVariation, modResourceCollections.Values);

                foreach (Archive archive in archives.Values.OrderBy(a => a.SubId))
                {
                    archive.CloseStreams();
                    if (!flattened)
                        archiveSet.Add(archive, _gameResourceUsageCache, progress, cancellationToken);
                }
                return new InstalledMod(archives.Values.First().Id, modPackage.Name, true);
            }
            catch
            {
                if (archives != null)
                {
                    foreach (Archive archive in archives.Values)
                    {
                        archive.CloseStreams();
                        if (archive.MetaData != null)
                            File.Delete(archive.MetaData.FilePath);

                        File.Delete(archive.BaseFilePath);
                    }
                }
                throw;
            }
            finally
            {
                archiveSet?.CloseStreams();
                progress.End();
            }
        }

        private ResourceUsageCache GetFullResourceUsageCache()
        {
            ResourceUsageCache fullResourceUsageCache = new ResourceUsageCache(_gameResourceUsageCache);
            using (ArchiveSet modArchiveSet = ArchiveSet.Open(_archiveSet.FolderPath, false, true, _archiveSet.Game))
            {
                fullResourceUsageCache.AddArchiveSet(modArchiveSet, null, CancellationToken.None);
            }
            return fullResourceUsageCache;
        }

        private Dictionary<ArchiveFileKey, HashSet<ResourceKey>> GetResourceRefsToAdd(
            ModPackage modPackage,
            ModVariation modVariation,
            List<ResourceKey> modResourceKeys,
            ResourceUsageCache fullResourceUsageCache)
        {
            Dictionary<ArchiveFileKey, HashSet<ResourceKey>> resourceRefsToAdd = new();
            foreach (ResourceKey modResourceKey in modResourceKeys)
            {
                HashSet<ResourceKey> externalResourceKeys = new();
                CollectExternalResourceKeys(modPackage, modVariation, modResourceKeys, modResourceKey, externalResourceKeys, fullResourceUsageCache);
                if (externalResourceKeys.Count == 0)
                    continue;

                foreach (ResourceCollectionItemReference modResourceUsage in fullResourceUsageCache.GetResourceUsages(_archiveSet, modResourceKey))
                {
                    resourceRefsToAdd.GetOrAdd(modResourceUsage.CollectionReference, () => new()).UnionWith(externalResourceKeys);
                }
            }
            return resourceRefsToAdd;
        }

        private void CollectExternalResourceKeys(
            ModPackage modPackage,
            ModVariation modVariation,
            List<ResourceKey> modResourceKeys,
            ResourceKey resourceKey,
            HashSet<ResourceKey> allExternalResourceKeys,
            ResourceUsageCache fullResourceUsageCache)
        {
            if (!MightHaveRefDefinitions(resourceKey))
                return;

            ResourceReference resourceRef = fullResourceUsageCache.GetResourceReference(_archiveSet, resourceKey);
            if (resourceRef == null || resourceRef.RefDefinitionsSize == 0)
                return;

            List<ResourceKey> externalResourceKeys;
            Stream resourceStream = null;
            try
            {
                if (modResourceKeys.Contains(resourceKey))
                    resourceStream = modVariation?.OpenResource(resourceKey) ?? modPackage.OpenResource(resourceKey);
                else
                    resourceStream = _archiveSet.OpenResource(resourceRef);

                if (!resourceStream.CanSeek)
                {
                    MemoryStream memResourceStream = new MemoryStream();
                    resourceStream.CopyTo(memResourceStream);
                    resourceStream.Close();
                    memResourceStream.Position = 0;
                    resourceStream = memResourceStream;
                }

                externalResourceKeys = ResourceRefDefinitions.Create(resourceRef, resourceStream, _archiveSet.Game)
                                                             .ExternalRefs
                                                             .Select(r => GetResourceKeyWithAddedSubType(r.ResourceKey, modResourceKeys)).ToList();
            }
            finally
            {
                resourceStream.Close();
            }

            foreach (ResourceKey externalResourceKey in externalResourceKeys)
            {
                if (resourceKey.SubType == ResourceSubType.Model && externalResourceKey.Type == ResourceType.Model)
                    continue;

                if (allExternalResourceKeys.Add(externalResourceKey))
                    CollectExternalResourceKeys(modPackage, modVariation, modResourceKeys, externalResourceKey, allExternalResourceKeys, fullResourceUsageCache);
            }
        }

        private static ResourceKey GetResourceKeyWithAddedSubType(ResourceKey resourceKey, List<ResourceKey> modResourceKeys)
        {
            switch (resourceKey.Type)
            {
                case ResourceType.Texture:
                    return new ResourceKey(ResourceType.Texture, ResourceSubType.Texture, resourceKey.Id);

                case ResourceType.Model:
                {
                    int index = modResourceKeys.IndexOf(resourceKey);
                    return index >= 0 ? modResourceKeys[index] : resourceKey;
                }

                default:
                    return resourceKey;
            }
        }

        private void AddResourceReferencesToCollections(
            Dictionary<ArchiveFileKey, ResourceCollection> modResourceCollections,
            Dictionary<ResourceKey, List<ResourceCollectionItem>> modResourceCollectionItems,
            Dictionary<ArchiveFileKey, HashSet<ResourceKey>> resourceRefsToAdd)
        {
            foreach ((ArchiveFileKey collectionKey, ICollection<ResourceKey> resourcesForCollection) in resourceRefsToAdd)
            {
                ResourceCollection modCollection = modResourceCollections.GetOrDefault(collectionKey);
                if (modCollection == null)
                    continue;

                foreach (ResourceKey resourceKey in resourcesForCollection)
                {
                    if (modCollection.ResourceReferences.Any(r => r.Type == resourceKey.Type && r.Id == resourceKey.Id))
                        continue;

                    int modCollectionResourceIdx = -1;
                    ResourceCollectionItemReference existingUsage = _gameResourceUsageCache.GetResourceUsages(_archiveSet, resourceKey).FirstOrDefault();
                    if (existingUsage != null)
                    {
                        ResourceCollection sourceCollection = _archiveSet.GetResourceCollection(existingUsage.CollectionReference);
                        if (sourceCollection != null)
                            modCollectionResourceIdx = modCollection.AddResourceReference(sourceCollection, existingUsage.ResourceIndex);
                    }
                    modResourceCollectionItems.GetOrAdd(resourceKey, () => new()).Add(new ResourceCollectionItem(modCollection, modCollectionResourceIdx));
                }
            }
        }

        private void AddResourcesToArchive(
            Archive archive,
            ModPackage modPackage,
            ModVariation modVariation,
            Dictionary<ResourceKey, List<ResourceCollectionItem>> modResourceCollectionItems,
            ITaskProgress progress,
            CancellationToken cancellationToken)
        {
            int resourceIdx = 0;
            int offsetInBatch = 0;
            foreach ((ResourceKey modResourceKey, List<ResourceCollectionItem> collectionItems) in modResourceCollectionItems)
            {
                cancellationToken.ThrowIfCancellationRequested();

                resourceIdx++;
                progress.Report((float)resourceIdx / modResourceCollectionItems.Count);

                Stream modResourceStream = modVariation?.OpenResource(modResourceKey) ?? modPackage.OpenResource(modResourceKey);
                if (modResourceStream == null)
                    continue;

                try
                {
                    int? refDefinitionsSize = null;
                    if (MightHaveRefDefinitions(modResourceKey))
                    {
                        ResourceReference existingResourceRef = collectionItems[0].ResourceIndex >= 0 ? collectionItems[0].Collection.ResourceReferences[collectionItems[0].ResourceIndex] : null;
                        if (existingResourceRef == null || existingResourceRef.RefDefinitionsSize > 0)
                            refDefinitionsSize = GetResourceRefDefinitionsSize(modPackage, modVariation, modResourceKey);
                    }

                    ArchiveBlobReference newResource = archive.AddResource(modResourceStream);
                    ResourceReference newResourceRef =
                        new ResourceReference(
                            modResourceKey.Type,
                            modResourceKey.SubType,
                            modResourceKey.Id,
                            0xFFFFFFFFFFFFFFFF,
                            newResource.ArchiveId,
                            newResource.ArchiveSubId,
                            newResource.ArchivePart,
                            newResource.Offset,
                            newResource.Length,
                            offsetInBatch,
                            refDefinitionsSize,
                            (int)modResourceStream.Length - (refDefinitionsSize ?? 0)
                        );
                    foreach (ResourceCollectionItem collectionItem in collectionItems)
                    {
                        if (collectionItem.ResourceIndex < 0)
                            collectionItem.Collection.AddResourceReference(newResourceRef);
                        else
                            collectionItem.Collection.UpdateResourceReference(collectionItem.ResourceIndex, newResourceRef);
                    }

                    offsetInBatch += (int)modResourceStream.Length;
                    offsetInBatch = (offsetInBatch + 0xF) & ~0xF;
                }
                finally
                {
                    modResourceStream.Close();
                }
            }
        }
        
        private int GetResourceRefDefinitionsSize(ModPackage modPackage, ModVariation modVariation, ResourceKey resourceKey)
        {
            using Stream stream = modVariation?.OpenResource(resourceKey) ?? modPackage.OpenResource(resourceKey);
            return ResourceRefDefinitions.ReadHeaderAndGetSize(stream, _archiveSet.Game);
        }

        private static void AddFilesToArchives(Dictionary<ulong, Archive> archives, ModPackage modPackage, ModVariation modVariation, IEnumerable<ResourceCollection> resourceCollections)
        {
            SortedDictionary<ArchiveFileKey, object> files = new();

            foreach (ArchiveFileKey fileKey in modPackage.Files)
            {
                files.Add(fileKey, modPackage);
            }

            if (modVariation != null)
            {
                foreach (ArchiveFileKey fileKey in modVariation.Files)
                {
                    files.Add(fileKey, modVariation);
                }
            }

            foreach (ResourceCollection resourceCollection in resourceCollections)
            {
                files.Add(new ArchiveFileKey(resourceCollection.NameHash, resourceCollection.Locale), resourceCollection);
            }

            foreach ((ArchiveFileKey fileKey, object value) in files)
            {
                switch (value)
                {
                    case ModPackage _:
                        AddFileToArchive(archives, fileKey, modPackage.OpenFile(fileKey));
                        break;

                    case ModVariation _:
                        AddFileToArchive(archives, fileKey, modVariation.OpenFile(fileKey));
                        break;

                    case ResourceCollection resourceCollection:
                        MemoryStream collectionStream = new MemoryStream();
                        resourceCollection.Write(collectionStream);
                        collectionStream.TryGetBuffer(out ArraySegment<byte> collectionData);
                        archives[resourceCollection.Locale].AddFile(resourceCollection.NameHash, resourceCollection.Locale, collectionData);
                        break;
                }
            }
        }

        private static void AddFileToArchive(Dictionary<ulong, Archive> archives, ArchiveFileKey fileKey, Stream stream)
        {
            if (stream == null)
                return;

            byte[] data = new byte[stream.Length];
            stream.Read(data, 0, data.Length);
            stream.Close();

            (archives.GetOrDefault(fileKey.Locale) ?? archives[0xFFFFFFFFFFFFFFFF]).AddFile(fileKey, data);
        }

        private static bool MightHaveRefDefinitions(ResourceKey resourceKey)
        {
            if (resourceKey.Type == ResourceType.Texture ||
                resourceKey.Type == ResourceType.ShaderLib ||
                resourceKey.SubType == ResourceSubType.ModelData ||
                resourceKey.Type == ResourceType.SoundBank)
                return false;

            return true;
        }
    }
}
