using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TrRebootTools.ModManager.Util;
using TrRebootTools.Shared;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.ModManager.Mod
{
    public class ModInstaller
    {
        private record struct ResourceCollectionItem(ResourceCollection Collection, int ResourceIndex);

        private readonly ArchiveSet _archiveSet;
        private readonly ResourceUsageCache _gameResourceUsageCache;

        public ModInstaller(ArchiveSet archiveSet, ResourceUsageCache gameResourceUsageCache, Window? mainWindow = null)
        {
            _archiveSet = archiveSet;
            _gameResourceUsageCache = gameResourceUsageCache;
        }

        public async Task<InstalledMod?> InstallFromZipAsync(string filePath, ITaskProgress progress, CancellationToken cancellationToken)
        {
            string modName = Regex.Replace(Path.GetFileNameWithoutExtension(filePath), @"(-\d+)+$", "");
            if (await IsModAlreadyInstalledAsync(modName))
                return null;

            using ZipTempExtractor extractor = new(filePath);
            extractor.Extract(progress, cancellationToken);

            using ModPackage modPackage = new FolderModPackage(modName, extractor.FolderPath, false, _archiveSet, _gameResourceUsageCache);
            return await InstallAsync(modPackage, progress, cancellationToken);
        }

        public async Task<InstalledMod?> InstallFromFolderAsync(string folderPath, ITaskProgress progress, CancellationToken cancellationToken)
        {
            string modName = Path.GetFileName(folderPath);
            return await InstallFromFolderAsync(modName, folderPath, progress, cancellationToken);
        }

        public async Task<InstalledMod?> InstallFromFolderAsync(string modName, string folderPath, ITaskProgress progress, CancellationToken cancellationToken)
        {
            Archive? existingArchive = _archiveSet.Archives.FirstOrDefault(a => a.ModName != null && Regex.Replace(a.ModName, @" \(.+\)$", "") == modName);
            if (existingArchive != null)
                _archiveSet.Delete(existingArchive.Id, _gameResourceUsageCache, progress, cancellationToken);

            using ModPackage modPackage = new FolderModPackage(modName, folderPath, true, _archiveSet, _gameResourceUsageCache);
            return await InstallAsync(modPackage, progress, cancellationToken);
        }

        public void ReinstallAll(ITaskProgress progress, CancellationToken cancellationToken)
        {
            List<OriginalModInfo> originalMods = [];
            foreach (IGrouping<int, Archive> archivesOfId in _archiveSet.Archives
                                                                        .Concat(_archiveSet.DuplicateArchives)
                                                                        .Where(a => a.ModName != null)
                                                                        .GroupBy(a => a.Id)
                                                                        .OrderByDescending(g => g.First().MetaData!.Version))
            {
                _archiveSet.CloseStreams();

                ArchiveMetaData metaData = archivesOfId.First().MetaData!;
                OriginalModInfo originalModInfo = new(
                    ModName: archivesOfId.First().ModName!,
                    Enabled: metaData.Enabled,
                    NfoFilePath: metaData.FilePath + ".orig",
                    ArchiveFilePaths: []
                );

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
                        _archiveSet.Disable([installedMod.ArchiveId], _gameResourceUsageCache, progress, cancellationToken);
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
            ArchiveIdentity? flattenedArchiveIdent = _archiveSet.GetActiveFlattenedModArchiveIdentity();
            if (flattenedArchiveIdent == null)
                return;

            List<TigerModPackage> modPackages = [];

            Archive? origGameArchive = _archiveSet.GetArchive(flattenedArchiveIdent.Id, 0);
            if (origGameArchive == null)
                throw new Exception($"Archive with ID {flattenedArchiveIdent.Id} not found");

            modPackages.Add(new TigerModPackage([origGameArchive], _archiveSet.Game));

            foreach (IGrouping<int, Archive> archivesOfId in _archiveSet.Archives
                                                                        .Where(a => a.ModName != null && a.MetaData!.Enabled)
                                                                        .GroupBy(a => a.Id)
                                                                        .OrderBy(g => g.First().MetaData!.Version))
            {
                modPackages.Add(new TigerModPackage(archivesOfId, _archiveSet.Game));
            }

            using var modPackage = new MultiTigerModPackage(flattenedArchiveIdent.FileName.Replace(".000.tiger", ""), modPackages);
            Install(modPackage, null, true, progress, cancellationToken);
        }

        private record OriginalModInfo(
            string ModName,
            bool Enabled,
            string NfoFilePath,
            List<string> ArchiveFilePaths
        );

        private async Task<bool> IsModAlreadyInstalledAsync(string modName)
        {
            if (_archiveSet.Archives.Any(a => a.ModName == modName))
            {
                await MessageBox.ShowAsync("", $"The mod {modName} is already installed.", icon: MsBox.Avalonia.Enums.Icon.Info);
                return true;
            }
            return false;
        }

        private async Task<InstalledMod?> InstallAsync(ModPackage modPackage, ITaskProgress progress, CancellationToken cancellationToken)
        {
            ModVariation? modVariation = null;
            if (modPackage.Variations != null && modPackage.Variations.Count > 0)
            {
                modVariation = await Dispatcher.UIThread.InvokeAsync(() => VariationSelectionWindow.ShowDialogAsync(modPackage));
                if (modVariation == null)
                    return null;
            }
            return Install(modPackage, modVariation, false, progress, cancellationToken);
        }

        private InstalledMod Install(ModPackage modPackage, ModVariation? modVariation, bool flattened, ITaskProgress? progress, CancellationToken cancellationToken)
        {
            ArchiveSet? archiveSet = null;
            Dictionary<ulong, Archive>? archives = null;
            string modName = modPackage.Name;
            if (modVariation != null)
                modName += $" ({modVariation.Name})";

            try
            {
                progress?.Begin($"Installing mod {modName}...");

                _archiveSet.CloseStreams();
                ResourceUsageCache resourceUsageCache = !flattened ? GetFullResourceUsageCache() : _gameResourceUsageCache;
                archiveSet = !flattened ? _archiveSet : ArchiveSet.Open(_archiveSet.FolderPath, true, false, _archiveSet.Game);

                ResourceKeyLookup modResourceKeys = new(modPackage.Resources);
                if (modVariation != null)
                    AddModVariationResources(modResourceKeys, modVariation.Resources);

                Dictionary<ResourceKey, List<ResourceCollectionItemReference>> modResourceUsages =
                    modResourceKeys.ToDictionary(
                        r => r,
                        r => (r.Locale == 0xFFFFFFFFFFFFFFFF ? resourceUsageCache.GetResourceUsages(archiveSet, r.Type, r.Id)
                                                             : resourceUsageCache.GetResourceUsages(archiveSet, r)
                             ).ToList()
                );
                modResourceUsages.RemoveAll(p => p.Value.Count == 0);

                Dictionary<ArchiveFileKey, ResourceCollection> modResourceCollections = modResourceUsages.Values
                                                                                                         .SelectMany(c => c)
                                                                                                         .Select(c => c.CollectionReference)
                                                                                                         .Distinct()
                                                                                                         .ToDictionary(r => (ArchiveFileKey)r, r => archiveSet.GetResourceCollection(r)!);

                Dictionary<ResourceKey, List<ResourceCollectionItem>> modResourceCollectionItems =
                    modResourceUsages.ToDictionary(
                        p => p.Key,
                        p => p.Value
                              .Select(r => new ResourceCollectionItem(modResourceCollections[r.CollectionReference], r.ResourceIndex))
                              .ToList()
                    );

                Dictionary<ArchiveFileKey, HashSet<ResourceKey>> resourceRefsToAdd = GetResourceRefsToAdd(modPackage, modVariation, modResourceKeys, resourceUsageCache);
                AddResourceReferencesToCollections(modResourceCollections, modResourceCollectionItems, resourceRefsToAdd, resourceUsageCache);

                IEnumerable<ArchiveFileKey> fileKeys = modResourceCollections.Keys.Concat(modPackage.Files).Concat(modVariation?.Files ?? []);
                Dictionary<ulong, int> fileCountsByLocale = fileKeys.GroupBy(f => f.Locale).ToDictionary(g => g.Key, g => g.Count());
                fileCountsByLocale.TryAdd(0xFFFFFFFFFFFFFFFF, 0);

                archives = !flattened ? archiveSet.CreateModArchives(modName, fileCountsByLocale)
                                      : new() { { 0xFFFFFFFFFFFFFFFF, archiveSet.CreateFlattenedModArchive(fileCountsByLocale.Values.Sum()) } };

                AddResourcesToArchive(archives[0xFFFFFFFFFFFFFFFF], modPackage, modVariation, modResourceCollectionItems, progress, cancellationToken);
                AddFilesToArchives(archives, modPackage, modVariation, modResourceCollections.Values);

                foreach (Archive archive in archives.Values.Distinct())
                {
                    archive.CloseStreams();
                    if (!flattened)
                        archiveSet.Add(archive, _gameResourceUsageCache, progress, cancellationToken);
                }
                return new InstalledMod(archives.Values.First().Id, modName, true);
            }
            catch
            {
                if (archives != null)
                {
                    foreach (Archive archive in archives.Values.Distinct())
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
                progress?.End();
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

        private void AddModVariationResources(ResourceKeyLookup modResourceKeys, IEnumerable<ResourceKey> variationResourceKeys)
        {
            foreach (ResourceKey resourceKey in variationResourceKeys)
            {
                if (modResourceKeys.Contains(resourceKey))
                {
                    string extension = ResourceNaming.GetExtension(resourceKey.Type, resourceKey.SubType, _archiveSet.Game);
                    throw new Exception($"The resource {resourceKey.Id}{extension} exists in both the base mod and the selected variation.");
                }
                modResourceKeys.Add(resourceKey);
            }
        }

        private Dictionary<ArchiveFileKey, HashSet<ResourceKey>> GetResourceRefsToAdd(
            ModPackage modPackage,
            ModVariation? modVariation,
            ResourceKeyLookup modResourceKeys,
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
                    resourceRefsToAdd.GetOrAdd(modResourceUsage.CollectionReference, () => []).UnionWith(externalResourceKeys);
                }
            }
            return resourceRefsToAdd;
        }

        private void CollectExternalResourceKeys(
            ModPackage modPackage,
            ModVariation? modVariation,
            ResourceKeyLookup modResourceKeys,
            ResourceKey resourceKey,
            HashSet<ResourceKey> allExternalResourceKeys,
            ResourceUsageCache fullResourceUsageCache)
        {
            if (!MightHaveRefDefinitions(resourceKey))
                return;

            ResourceReference? resourceRef = fullResourceUsageCache.GetResourceReference(_archiveSet, resourceKey);
            if (resourceRef != null && resourceRef.RefDefinitionsSize == 0)
                return;

            List<ResourceKey> externalResourceKeys;
            Stream? resourceStream = null;
            try
            {
                if (modResourceKeys.Contains(resourceKey))
                    resourceStream = modVariation?.OpenResource(resourceKey) ?? modPackage.OpenResource(resourceKey)!;
                else if (resourceRef != null)
                    resourceStream = _archiveSet.OpenResource(resourceRef);
                else
                    return;

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
                                                             .SelectMany(r => GetLocaleSpecificResourceKeys(r.ResourceKey, modResourceKeys, fullResourceUsageCache))
                                                             .Select(r => GetResourceKeyWithAddedSubType(r, modResourceKeys))
                                                             .ToList();
            }
            finally
            {
                resourceStream?.Close();
            }

            foreach (ResourceKey externalResourceKey in externalResourceKeys)
            {
                if (resourceKey.SubType == ResourceSubType.Model && externalResourceKey.Type == ResourceType.Model)
                    continue;

                if (allExternalResourceKeys.Add(externalResourceKey))
                    CollectExternalResourceKeys(modPackage, modVariation, modResourceKeys, externalResourceKey, allExternalResourceKeys, fullResourceUsageCache);
            }
        }

        private static IEnumerable<ResourceKey> GetLocaleSpecificResourceKeys(ResourceKey resourceKey, ResourceKeyLookup modResourceKeys, ResourceUsageCache resourceUsageCache)
        {
            if (resourceKey.Locale != 0xFFFFFFFFFFFFFFFF)
            {
                yield return resourceKey;
                yield break;
            }

            IReadOnlyCollection<ulong> cacheLocales = resourceUsageCache.GetResourceLocales(resourceKey.Type, resourceKey.Id) ?? [];
            IReadOnlyCollection<ulong> modLocales = modResourceKeys.GetLocales(resourceKey.Type, resourceKey.Id) ?? [];
            if (cacheLocales.Count == 0 && modLocales.Count == 0)
            {
                yield return resourceKey;
                yield break;
            }

            foreach (ulong locale in cacheLocales.Union(modLocales))
            {
                yield return new ResourceKey(resourceKey.Type, resourceKey.SubType, resourceKey.Id, locale);
            }
        }

        private static ResourceKey GetResourceKeyWithAddedSubType(ResourceKey resourceKey, ResourceKeyLookup modResourceKeys)
        {
            switch (resourceKey.Type)
            {
                case ResourceType.Texture:
                    return new ResourceKey(ResourceType.Texture, ResourceSubType.Texture, resourceKey.Id, resourceKey.Locale);

                case ResourceType.Model:
                {
                    ResourceSubType? subType = modResourceKeys.GetSubType(resourceKey.Type, resourceKey.Id);
                    if (subType != null)
                        return new ResourceKey(resourceKey.Type, subType.Value, resourceKey.Id, resourceKey.Locale);

                    break;
                }
            }
            return resourceKey;
        }

        private void AddResourceReferencesToCollections(
            Dictionary<ArchiveFileKey, ResourceCollection> modResourceCollections,
            Dictionary<ResourceKey, List<ResourceCollectionItem>> modResourceCollectionItems,
            Dictionary<ArchiveFileKey, HashSet<ResourceKey>> resourceRefsToAdd,
            ResourceUsageCache resourceUsageCache)
        {
            foreach ((ArchiveFileKey collectionKey, ICollection<ResourceKey> resourcesForCollection) in resourceRefsToAdd)
            {
                ResourceCollection? modCollection = modResourceCollections.GetValueOrDefault(collectionKey);
                if (modCollection == null)
                    continue;

                foreach (ResourceKey resourceKey in resourcesForCollection)
                {
                    if (modCollection.Contains(resourceKey))
                        continue;

                    int modCollectionResourceIdx = -1;
                    ResourceCollectionItemReference? existingUsage = resourceUsageCache.GetResourceUsages(_archiveSet, resourceKey).FirstOrDefault();
                    if (existingUsage != null)
                    {
                        ResourceCollection? sourceCollection = _archiveSet.GetResourceCollection(existingUsage.CollectionReference);
                        if (sourceCollection != null)
                            modCollectionResourceIdx = modCollection.AddResourceReference(sourceCollection, existingUsage.ResourceIndex);
                    }
                    modResourceCollectionItems.GetOrAdd(resourceKey, () => []).Add(new ResourceCollectionItem(modCollection, modCollectionResourceIdx));
                }
            }
        }

        private void AddResourcesToArchive(
            Archive archive,
            ModPackage modPackage,
            ModVariation? modVariation,
            Dictionary<ResourceKey, List<ResourceCollectionItem>> modResourceCollectionItems,
            ITaskProgress? progress,
            CancellationToken cancellationToken)
        {
            int resourceIdx = 0;
            uint decompressionOffset = 0;
            foreach ((ResourceKey modResourceKey, List<ResourceCollectionItem> collectionItems) in modResourceCollectionItems)
            {
                cancellationToken.ThrowIfCancellationRequested();

                resourceIdx++;
                progress?.Report((float)resourceIdx / modResourceCollectionItems.Count);

                Stream? modResourceStream = modVariation?.OpenResource(modResourceKey) ?? modPackage.OpenResource(modResourceKey);
                if (modResourceStream == null)
                    continue;

                try
                {
                    uint? refDefinitionsSize = null;
                    if (MightHaveRefDefinitions(modResourceKey))
                    {
                        ResourceReference? existingResourceRef = collectionItems[0].ResourceIndex >= 0 ? collectionItems[0].Collection.ResourceReferences[collectionItems[0].ResourceIndex] : null;
                        if (existingResourceRef == null || existingResourceRef.RefDefinitionsSize > 0)
                            refDefinitionsSize = (uint)GetResourceRefDefinitionsSize(modPackage, modVariation, modResourceKey);
                    }

                    ArchiveBlobReference newResource = archive.AddResource(modResourceStream);
                    ResourceReference newResourceRef =
                        new ResourceReference(
                            modResourceKey.Type,
                            modResourceKey.SubType,
                            modResourceKey.Id,
                            modResourceKey.Locale,
                            newResource.ArchiveId,
                            newResource.ArchiveSubId,
                            newResource.ArchivePart,
                            newResource.Offset,
                            newResource.Length,
                            decompressionOffset,
                            refDefinitionsSize,
                            (uint)modResourceStream.Length - (refDefinitionsSize ?? 0)
                        );
                    foreach (ResourceCollectionItem collectionItem in collectionItems)
                    {
                        if (collectionItem.ResourceIndex < 0)
                            collectionItem.Collection.AddResourceReference(newResourceRef);
                        else
                            collectionItem.Collection.UpdateResourceReference(collectionItem.ResourceIndex, newResourceRef);
                    }

                    decompressionOffset += (uint)modResourceStream.Length;
                    decompressionOffset = (decompressionOffset + 0xF) & ~0xFu;
                }
                finally
                {
                    modResourceStream.Close();
                }
            }
        }
        
        private int GetResourceRefDefinitionsSize(ModPackage modPackage, ModVariation? modVariation, ResourceKey resourceKey)
        {
            using Stream stream = modVariation?.OpenResource(resourceKey) ?? modPackage.OpenResource(resourceKey)!;
            return ResourceRefDefinitions.ReadHeaderAndGetSize(stream, _archiveSet.Game);
        }

        private static void AddFilesToArchives(Dictionary<ulong, Archive> archives, ModPackage modPackage, ModVariation? modVariation, IEnumerable<ResourceCollection> resourceCollections)
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
                        AddFileToArchive(archives, fileKey, modVariation!.OpenFile(fileKey));
                        break;

                    case ResourceCollection resourceCollection:
                        MemoryStream collectionStream = new();
                        resourceCollection.Write(collectionStream);
                        collectionStream.TryGetBuffer(out ArraySegment<byte> collectionData);
                        archives[resourceCollection.Locale].AddFile(resourceCollection.NameHash, resourceCollection.Locale, collectionData);
                        break;
                }
            }
        }

        private static void AddFileToArchive(Dictionary<ulong, Archive> archives, ArchiveFileKey fileKey, Stream? stream)
        {
            if (stream == null)
                return;

            byte[] data = new byte[stream.Length];
            stream.Read(data, 0, data.Length);
            stream.Close();

            (archives.GetValueOrDefault(fileKey.Locale) ?? archives[0xFFFFFFFFFFFFFFFF]).AddFile(fileKey, data);
        }

        private static bool MightHaveRefDefinitions(ResourceKey resourceKey)
        {
            if (resourceKey.Type == ResourceType.Texture ||
                resourceKey.Type == ResourceType.ShaderLib ||
                resourceKey.Type == ResourceType.SoundBank)
                return false;

            return true;
        }
    }
}
