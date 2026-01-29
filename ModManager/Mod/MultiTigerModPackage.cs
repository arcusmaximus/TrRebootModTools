using System.Collections.Generic;
using System.IO;
using TrRebootTools.Shared.Cdc;

namespace TrRebootTools.ModManager.Mod
{
    internal class MultiTigerModPackage : ModPackage
    {
        private readonly List<TigerModPackage> _packages = [];
        private readonly Dictionary<ArchiveFileKey, TigerModPackage> _files = new();
        private readonly Dictionary<ResourceKey, TigerModPackage> _resources = new();

        public MultiTigerModPackage(string name, IEnumerable<TigerModPackage> packages)
        {
            Name = name;

            foreach (TigerModPackage package in packages)
            {
                _packages.Add(package);

                foreach (ArchiveFileKey fileKey in package.Files)
                {
                    _files[fileKey] = package;
                }

                foreach (ResourceKey resourceKey in package.Resources)
                {
                    _resources[resourceKey] = package;
                }
            }
        }

        public override string Name
        {
            get;
        }

        public override IEnumerable<ArchiveFileKey> Files => _files.Keys;

        public override IEnumerable<ResourceKey> Resources => _resources.Keys;

        public override Stream? OpenFile(ArchiveFileKey fileKey)
        {
            return _files.GetValueOrDefault(fileKey)?.OpenFile(fileKey);
        }

        public override Stream? OpenResource(ResourceKey resourceKey)
        {
            return _resources.GetValueOrDefault(resourceKey)?.OpenResource(resourceKey);
        }

        public override void Dispose()
        {
            base.Dispose();
            foreach (TigerModPackage package in _packages)
            {
                package.Dispose();
            }
        }
    }
}
