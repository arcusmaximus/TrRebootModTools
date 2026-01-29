using System;
using System.Collections.Generic;
using System.IO;
using TrRebootTools.Shared.Cdc;

namespace TrRebootTools.ModManager.Mod
{
    internal abstract class ModPackage : IDisposable
    {
        public abstract string Name
        {
            get;
        }

        public abstract IEnumerable<ArchiveFileKey> Files
        {
            get;
        }

        public abstract Stream? OpenFile(ArchiveFileKey fileKey);

        public abstract IEnumerable<ResourceKey> Resources
        {
            get;
        }

        public abstract Stream? OpenResource(ResourceKey resourceKey);

        public List<ModVariation> Variations
        {
            get;
        } = [];

        public override string ToString()
        {
            return Name;
        }

        public virtual void Dispose()
        {
            foreach (ModVariation variation in Variations)
            {
                variation.Dispose();
            }
        }
    }
}
