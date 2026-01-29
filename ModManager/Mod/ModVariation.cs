using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using TrRebootTools.Shared.Cdc;

namespace TrRebootTools.ModManager.Mod
{
    internal abstract class ModVariation : IDisposable
    {
        public ModVariation(string name, string? description, Bitmap? image)
        {
            Name = name;
            Description = description;
            Image = image;
        }

        public string Name
        {
            get;
        }

        public string? Description
        {
            get;
        }

        public Bitmap? Image
        {
            get;
        }

        public abstract IEnumerable<ArchiveFileKey> Files
        {
            get;
        }

        public abstract Stream? OpenFile(ArchiveFileKey key);

        public abstract IEnumerable<ResourceKey> Resources
        {
            get;
        }

        public abstract Stream? OpenResource(ResourceKey resourceKey);

        public override string ToString()
        {
            return Name;
        }

        public virtual void Dispose()
        {
            Image?.Dispose();
        }
    }
}
