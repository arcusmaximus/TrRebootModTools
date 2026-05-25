using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrRebootTools.Shared.Cdc;

namespace TrRebootTools.ModManager.Mod
{
    internal class TigerModPackage : ModPackage
    {
        private readonly Dictionary<int, Archive> _archivesBySubId;
        private readonly Dictionary<ArchiveFileKey, ArchiveFileDescriptor> _files = new();
        private readonly Dictionary<ResourceKey, ResourceDescriptor> _resources = new();

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
                foreach (ArchiveFileDescriptor file in archive.Files)
                {
                    if (file.ArchiveId != archive.Id)
                        continue;

                    string? filePath = CdcHash.Lookup(file.NameHash, game);
                    if (filePath == null || !filePath.EndsWith(".drm"))
                    {
                        _files[file] = file;
                        continue;
                    }

                    ResourceCollection? collection = archive.GetResourceCollection(file);
                    if (collection == null)
                        continue;

                    foreach (ResourceDescriptor resource in collection.Resources)
                    {
                        if (resource.ArchiveId == archive.Id && (resource.Locale & localePlatformMask) == localePlatformMask)
                            _resources[resource] = resource;
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
            ArchiveFileDescriptor? file = _files.GetValueOrDefault(fileKey);
            return file != null ? _archivesBySubId[file.ArchiveSubId].OpenFile(file) : null;
        }

        public override IEnumerable<ResourceKey> Resources => _resources.Keys;

        public override Stream? OpenResource(ResourceKey resourceKey)
        {
            ResourceDescriptor? resource = _resources.GetValueOrDefault(resourceKey);
            return resource != null ? _archivesBySubId[resource.ArchiveSubId].OpenResource(_resources[resourceKey]) : null;
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
