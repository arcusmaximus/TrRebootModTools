using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrRebootTools.Shared.Cdc;

namespace TrRebootTools.ModManager.Mod
{
    internal class TigerModPackage : ModPackage
    {
        private readonly Dictionary<int, Archive> _archivesBySubId;
        private readonly Dictionary<ArchiveFileKey, ArchiveFileReference> _files = new();
        private readonly Dictionary<ResourceKey, ResourceReference> _resources = new();

        public TigerModPackage(string nfoFilePath, List<string> archiveFilePaths, CdcGame game)
            : this(LoadArchives(nfoFilePath, archiveFilePaths, game), game)
        {
        }

        private static IEnumerable<Archive> LoadArchives(string nfoFilePath, IEnumerable<string> archiveFilePaths, CdcGame game)
        {
            ArchiveMetaData? metaData = nfoFilePath != null ? ArchiveMetaData.Load(nfoFilePath) : null;
            return archiveFilePaths.Select(p => Archive.Open(p, metaData, game));
        }

        public TigerModPackage(IEnumerable<Archive> archives, CdcGame game)
        {
            _archivesBySubId = archives.ToDictionary(a => a.SubId);
            Name = _archivesBySubId.Values.First().ModName!;

            ulong localePlatformMask = CdcGameInfo.Get(game).LocalePlatformMask;
            foreach (Archive archive in _archivesBySubId.Values)
            {
                foreach (ArchiveFileReference fileRef in archive.Files)
                {
                    if (fileRef.ArchiveId != archive.Id)
                        continue;

                    string? filePath = CdcHash.Lookup(fileRef.NameHash, game);
                    if (filePath == null || !filePath.EndsWith(".drm"))
                    {
                        _files[fileRef] = fileRef;
                        continue;
                    }

                    ResourceCollection? collection = archive.GetResourceCollection(fileRef);
                    if (collection == null)
                        continue;

                    foreach (ResourceReference resourceRef in collection.ResourceReferences)
                    {
                        if (resourceRef.ArchiveId == archive.Id && (resourceRef.Locale & localePlatformMask) == localePlatformMask)
                            _resources[resourceRef] = resourceRef;
                    }
                }
            }
        }

        public override string Name
        {
            get;
        }

        public override IEnumerable<ArchiveFileKey> Files => _files.Keys;

        public override Stream? OpenFile(ArchiveFileKey fileKey)
        {
            ArchiveFileReference? fileRef = _files.GetValueOrDefault(fileKey);
            return fileRef != null ? _archivesBySubId[fileRef.ArchiveSubId].OpenFile(fileRef) : null;
        }

        public override IEnumerable<ResourceKey> Resources => _resources.Keys;

        public override Stream? OpenResource(ResourceKey resourceKey)
        {
            ResourceReference? resourceRef = _resources.GetValueOrDefault(resourceKey);
            return resourceRef != null ? _archivesBySubId[resourceRef.ArchiveSubId].OpenResource(_resources[resourceKey]) : null;
        }

        public override string ToString()
        {
            return Name;
        }

        public override void Dispose()
        {
            base.Dispose();
            foreach (Archive archive in _archivesBySubId.Values)
            {
                archive.Dispose();
            }
        }
    }
}
