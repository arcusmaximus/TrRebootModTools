using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Cdc
{
    public class ResourceUsageCache
    {
        private const string FileName = "resourceusage.bin";
        private const int Version = 8;

        private readonly ResourceUsageCache _baseCache;
        private readonly Dictionary<ResourceKey, Dictionary<ArchiveFileKey, int>> _resourceUsages = new();
        private readonly Dictionary<(ResourceType, int), List<ulong>> _resourceLocales = new();
        private readonly Dictionary<string, ResourceKey> _resourceKeysByOriginalFilePath = new();
        private readonly Dictionary<int, HashSet<WwiseSoundBankItemReference>> _wwiseSoundUsages = new();

        public ResourceUsageCache()
        {
        }

        public ResourceUsageCache(ResourceUsageCache baseCache)
        {
            _baseCache = baseCache;
        }

        public void AddArchiveSet(ArchiveSet archiveSet, ITaskProgress progress, CancellationToken cancellationToken)
        {
            try
            {
                progress?.Begin("Creating resource usage cache...");
                AddFiles(archiveSet.Files, archiveSet, progress, cancellationToken);
            }
            finally
            {
                progress?.End();
            }
        }

        public void AddArchives(IEnumerable<Archive> archives, ArchiveSet archiveSet)
        {
            foreach (Archive archive in archives)
            {
                AddArchive(archive, archiveSet);
            }
        }

        public void AddArchive(Archive archive, ArchiveSet archiveSet)
        {
            AddFiles(archive.Files, archiveSet, null, CancellationToken.None);
        }

        private void AddFiles(IEnumerable<ArchiveFileReference> files, ArchiveSet archiveSet, ITaskProgress progress, CancellationToken cancellationToken)
        {
            List<ArchiveFileReference> collectionRefs = files.Where(f => CdcHash.Lookup(f.NameHash, archiveSet.Game)?.EndsWith(".drm") ?? false).ToList();
            int collectionIdx = 0;
            foreach (ArchiveFileReference collectionRef in collectionRefs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ResourceCollection collection = archiveSet.GetResourceCollection(collectionRef);
                if (collection != null)
                    AddResourceCollection(archiveSet, collection);

                collectionIdx++;
                progress?.Report((float)collectionIdx / collectionRefs.Count);
            }
        }

        public void AddResourceCollection(ArchiveSet archiveSet, ResourceCollection collection)
        {
            ArchiveFileKey fileKey = new ArchiveFileKey(collection.NameHash, collection.Locale);
            foreach (ResourceReference resourceRef in collection.ResourceReferences)
            {
                _resourceUsages.GetOrDefault(resourceRef)?.Remove(fileKey);
            }

            ulong localePlatformMask = CdcGameInfo.Get(collection.Game).LocalePlatformMask;
            bool parseWwiseSoundBanks = CdcGameInfo.Get(collection.Game).UsesWwise;
            for (int i = 0; i < collection.ResourceReferences.Count; i++)
            {
                ResourceReference resourceRef = collection.ResourceReferences[i];
                if ((resourceRef.Locale & localePlatformMask) != localePlatformMask)
                    continue;

                AddResourceReference(archiveSet, collection, i);
                if (resourceRef.Type == ResourceType.SoundBank && parseWwiseSoundBanks)
                    AddWwiseSoundBank(archiveSet, resourceRef);
            }
        }

        public void AddResourceReference(ArchiveSet archiveSet, ResourceCollection collection, int resourceIdx)
        {
            ResourceReference resourceRef = collection.ResourceReferences[resourceIdx];
            if (!_resourceUsages.ContainsKey(resourceRef))
            {
                string originalFilePath = ResourceNaming.ReadOriginalFilePath(archiveSet, resourceRef);
                if (originalFilePath != null)
                    _resourceKeysByOriginalFilePath[originalFilePath] = resourceRef;
            }

            Dictionary<ArchiveFileKey, int> usages = _resourceUsages.GetOrAdd(resourceRef, () => []);

            ArchiveFileKey collectionKey = new ArchiveFileKey(collection.NameHash, collection.Locale);
            if (collection.Locale == 0xFFFFFFFFFFFFFFFF)
            {
                if (!usages.ContainsKey(collectionKey))
                {
                    foreach (ArchiveFileKey localeSpecificCollectionKey in usages.Keys.Where(c => c.NameHash == collection.NameHash).ToList())
                    {
                        usages.Remove(localeSpecificCollectionKey);
                    }
                }
            }
            else
            {
                if (usages.ContainsKey(new ArchiveFileKey(collection.NameHash, 0xFFFFFFFFFFFFFFFF)))
                    return;
            }

            usages[collectionKey] = resourceIdx;

            if (resourceRef.Locale != 0xFFFFFFFFFFFFFFFF)
            {
                List<ulong> locales = _resourceLocales.GetOrAdd((resourceRef.Type, resourceRef.Id), () => []);
                if (!locales.Contains(resourceRef.Locale))
                    locales.Add(resourceRef.Locale);
            }
        }

        private void AddWwiseSoundBank(ArchiveSet archiveSet, ResourceReference resourceRef)
        {
            WwiseSoundBank bank;
            using (Stream stream = archiveSet.OpenResource(resourceRef))
            {
                bank = new WwiseSoundBank(stream);
            }

            int index = 0;
            foreach (int soundId in bank.EmbeddedSounds.Keys)
            {
                _wwiseSoundUsages.GetOrAdd(soundId, () => [])
                                 .Add(new WwiseSoundBankItemReference(resourceRef.Id, WwiseSoundBankItemReferenceType.DataIndex, index++));
            }

            index = 0;
            foreach (int soundId in bank.ReferencedSoundIds)
            {
                _wwiseSoundUsages.GetOrAdd(soundId, () => [])
                                 .Add(new WwiseSoundBankItemReference(resourceRef.Id, WwiseSoundBankItemReferenceType.Event, index++));
            }
        }

        public bool TryGetResourceKeyByOriginalFilePath(string filePath, out ResourceKey resourceKey)
        {
            return _resourceKeysByOriginalFilePath.TryGetValue(filePath, out resourceKey);
        }

        public IReadOnlyCollection<ulong> GetResourceLocales(ResourceType type, int id)
        {
            return _resourceLocales.GetOrDefault((type, id));
        }

        public IEnumerable<ResourceCollectionItemReference> GetResourceUsages(ArchiveSet archiveSet, ResourceType resourceType, int resourceId)
        {
            List<ulong> locales = _resourceLocales.GetOrDefault((resourceType, resourceId));
            if (locales == null)
                return GetResourceUsages(archiveSet, new ResourceKey(resourceType, resourceId));

            return locales.SelectMany(l => GetResourceUsages(archiveSet, new ResourceKey(resourceType, 0, resourceId, l)));
        }

        public IEnumerable<ResourceCollectionItemReference> GetResourceUsages(ArchiveSet archiveSet, ResourceKey resourceKey)
        {
            IEnumerable<ResourceCollectionItemReference> baseUsages = _baseCache?.GetResourceUsages(archiveSet, resourceKey);
            Dictionary<ArchiveFileKey, int> usages = _resourceUsages.GetOrDefault(resourceKey);

            if (baseUsages != null)
            {
                foreach (ResourceCollectionItemReference baseUsage in baseUsages)
                {
                    if (usages != null)
                    {
                        if (usages.ContainsKey(new ArchiveFileKey(baseUsage.CollectionReference.NameHash, baseUsage.CollectionReference.Locale)))
                            continue;

                        if (baseUsage.CollectionReference.Locale != 0xFFFFFFFFFFFFFFFF &&
                            usages.ContainsKey(new ArchiveFileKey(baseUsage.CollectionReference.NameHash, 0xFFFFFFFFFFFFFFFF)))
                            continue;
                    }

                    yield return baseUsage;
                }
            }

            if (usages != null)
            {
                foreach ((ArchiveFileKey collectionKey, int resourceIdx) in usages)
                {
                    yield return new ResourceCollectionItemReference(archiveSet.GetFileReference(collectionKey.NameHash, collectionKey.Locale), resourceIdx);
                }
            }
        }

        public ResourceReference GetResourceReference(ArchiveSet archiveSet, ResourceKey resourceKey)
        {
            ResourceCollectionItemReference collectionItem = GetResourceUsages(archiveSet, resourceKey).FirstOrDefault();
            if (collectionItem == null)
                return null;

            ResourceCollection collection = archiveSet.GetResourceCollection(collectionItem.CollectionReference);
            return collection?.ResourceReferences[collectionItem.ResourceIndex];
        }

        public IEnumerable<int> WwiseSoundIds => _wwiseSoundUsages.Keys;

        public IEnumerable<WwiseSoundBankItemReference> GetWwiseSoundUsages(int soundId)
        {
            return _wwiseSoundUsages.GetOrDefault(soundId) ?? Enumerable.Empty<WwiseSoundBankItemReference>();
        }

        public bool Load(string archiveFolderPath)
        {
            string filePath = Path.Combine(archiveFolderPath, FileName);
            if (!File.Exists(filePath))
                return false;

            using Stream stream = File.OpenRead(filePath);
            BinaryReader reader = new BinaryReader(stream);
            int version = reader.ReadInt32();
            if (version != Version)
                return false;

            ReadResourceUsages(reader);
            ReadOriginalResourceFilePaths(reader);
            ReadSoundUsages(reader);
            return true;
        }

        private void ReadResourceUsages(BinaryReader reader)
        {
            int numResources = reader.ReadInt32();
            for (int i = 0; i < numResources; i++)
            {
                ResourceType type = (ResourceType)reader.ReadByte();
                int id = reader.ReadInt32();
                ulong locale = ReadLocale(reader);
                int numCollections = reader.ReadUInt16();
                Dictionary<ArchiveFileKey, int> usages = new(numCollections);
                for (int j = 0; j < numCollections; j++)
                {
                    ulong collectionNameHash = reader.ReadUInt64();
                    ulong collectionLocale = ReadLocale(reader);
                    int resourceIdx = reader.ReadUInt16();
                    usages.Add(new ArchiveFileKey(collectionNameHash, collectionLocale), resourceIdx);
                }
                _resourceUsages.Add(new ResourceKey(type, 0, id, locale), usages);

                if (locale != 0xFFFFFFFFFFFFFFFF)
                    _resourceLocales.GetOrAdd((type, id), () => []).Add(locale);
            }
        }

        private static ulong ReadLocale(BinaryReader reader)
        {
            return reader.ReadByte() == 0 ? 0xFFFFFFFFFFFFFFFF : reader.ReadUInt64();
        }

        private void ReadOriginalResourceFilePaths(BinaryReader reader)
        {
            int numPaths = reader.ReadInt32();
            for (int i = 0; i < numPaths; i++)
            {
                string filePath = reader.ReadString();
                ResourceType type = (ResourceType)reader.ReadByte();
                int id = reader.ReadInt32();
                _resourceKeysByOriginalFilePath[filePath] = new ResourceKey(type, id);
            }
        }

        private void ReadSoundUsages(BinaryReader reader)
        {
            int numSounds = reader.ReadInt32();
            for (int i = 0; i < numSounds; i++)
            {
                int id = reader.ReadInt32();
                int numUsages = reader.ReadInt32();
                HashSet<WwiseSoundBankItemReference> usages = new();
                for (int j = 0; j < numUsages; j++)
                {
                    int soundBankResourceId = reader.ReadInt32();
                    WwiseSoundBankItemReferenceType type = (WwiseSoundBankItemReferenceType)reader.ReadByte();
                    int index = reader.ReadInt32();
                    usages.Add(new WwiseSoundBankItemReference(soundBankResourceId, type, index));
                }
                _wwiseSoundUsages.Add(id, usages);
            }
        }

        public void Save(string archiveFolderPath)
        {
            using Stream stream = File.Create(Path.Combine(archiveFolderPath, FileName));
            BinaryWriter writer = new BinaryWriter(stream);

            writer.Write(Version);

            WriteResourceUsages(writer);
            WriteOriginalResourceFilePaths(writer);
            WriteSoundUsages(writer);
        }

        private void WriteResourceUsages(BinaryWriter writer)
        {
            writer.Write(_resourceUsages.Count);
            foreach ((ResourceKey resource, Dictionary<ArchiveFileKey, int> usages) in _resourceUsages)
            {
                writer.Write((byte)resource.Type);
                writer.Write(resource.Id);
                WriteLocale(writer, resource.Locale);
                writer.Write((ushort)usages.Count);
                foreach ((ArchiveFileKey collectionKey, int resourceIdx) in usages)
                {
                    writer.Write(collectionKey.NameHash);
                    WriteLocale(writer, collectionKey.Locale);
                    writer.Write((ushort)resourceIdx);
                }
            }
        }

        private static void WriteLocale(BinaryWriter writer, ulong locale)
        {
            if (locale == 0xFFFFFFFFFFFFFFFF)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(locale);
            }
        }

        private void WriteOriginalResourceFilePaths(BinaryWriter writer)
        {
            writer.Write(_resourceKeysByOriginalFilePath.Count);
            foreach ((string filePath, ResourceKey resourceKey) in _resourceKeysByOriginalFilePath)
            {
                writer.Write(filePath);
                writer.Write((byte)resourceKey.Type);
                writer.Write(resourceKey.Id);
            }
        }

        private void WriteSoundUsages(BinaryWriter writer)
        {
            writer.Write(_wwiseSoundUsages.Count);
            foreach ((int soundId, HashSet<WwiseSoundBankItemReference> usages) in _wwiseSoundUsages)
            {
                writer.Write(soundId);
                writer.Write(usages.Count);
                foreach (WwiseSoundBankItemReference usage in usages)
                {
                    writer.Write(usage.BankResourceId);
                    writer.Write((byte)usage.Type);
                    writer.Write(usage.Index);
                }
            }
        }
    }
}
