using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using TrRebootTools.Shared.Cdc.Rise;
using TrRebootTools.Shared.Cdc.Shadow;
using TrRebootTools.Shared.Cdc.Tr2013;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Cdc
{
    public abstract class ResourceCollection
    {
        public static ResourceCollection Open(ulong nameHash, ulong locale, Stream stream, CdcGame version)
        {
            return version switch
            {
                CdcGame.Tr2013 => new Tr2013ResourceCollection(nameHash, locale, stream),
                CdcGame.Rise => new RiseResourceCollection(nameHash, locale, stream),
                CdcGame.Shadow => new ShadowResourceCollection(nameHash, locale, stream)
            };
        }

        protected ResourceCollection(ulong nameHash, ulong locale)
        {
            NameHash = nameHash;
            Locale = locale;
        }

        public ulong NameHash
        {
            get;
        }

        public ulong Locale
        {
            get;
        }

        public abstract CdcGame Game
        {
            get;
        }

        public abstract ResourceReference MainResourceReference
        {
            get;
        }

        public abstract IReadOnlyList<ResourceReference> ResourceReferences { get; }

        public abstract int AddResourceReference(ResourceCollection otherCollection, int otherResourceId);
        public abstract int AddResourceReference(ResourceReference resourceRef);
        public abstract void UpdateResourceReference(int resourceIdx, ResourceReference resourceRef);

        public abstract IEnumerable<ResourceCollectionDependency> Dependencies { get; }

        public abstract void Write(Stream stream);
    }

    public abstract class ResourceCollection<TResourceLocation, TLocale> : ResourceCollection
        where TResourceLocation : unmanaged
        where TLocale : unmanaged
    {
        private ResourceCollectionHeader _header;
        private readonly ulong _headerLocale;
        private readonly List<ResourceIdentification<TLocale>> _resourceIdentifications = new();
        private readonly List<ResourceCollectionDependency> _dependencies;
        private readonly List<ResourceCollectionDependency> _includes;
        private readonly List<TResourceLocation> _resourceLocations = new();

        private List<ResourceReference> _resourceReferences;

        protected ResourceCollection(ulong nameHash, ulong locale, Stream stream)
            : base(nameHash, locale)
        {
            BinaryReader reader = new BinaryReader(stream);
            _header = reader.ReadStruct<ResourceCollectionHeader>();
            if (_header.Version != HeaderVersion)
                throw new NotSupportedException($"Only version {HeaderVersion} .drm files are supported");

            _headerLocale = ReadLocale(reader, HeaderLocaleSize);

            for (int i = 0; i < _header.NumResources; i++)
            {
                _resourceIdentifications.Add(reader.ReadStruct<ResourceIdentification<TLocale>>());
            }

            _dependencies = ReadDependencies(reader, _header.DependenciesLength);
            _includes = ReadDependencies(reader, _header.IncludeLength);

            for (int i = 0; i < _header.NumResources; i++)
            {
                _resourceLocations.Add(reader.ReadStruct<TResourceLocation>());
            }
        }

        protected abstract int HeaderVersion { get; }

        protected abstract int HeaderLocaleSize { get; }

        public override ResourceReference MainResourceReference
        {
            get => _header.MainResourceIndex >= 0 ? ResourceReferences[_header.MainResourceIndex] : null;
        }

        public override IReadOnlyList<ResourceReference> ResourceReferences
        {
            get
            {
                if (_resourceReferences == null)
                {
                    _resourceReferences = new List<ResourceReference>();
                    for (int i = 0; i < _resourceIdentifications.Count; i++)
                    {
                        var identification = _resourceIdentifications[i];
                        var location = _resourceLocations[i];
                        ResourceReference resourceRef = MakeResourceReference(identification, location);
                        resourceRef.Enabled = identification.Type != (byte)ResourceType.Empty;
                        _resourceReferences.Add(resourceRef);
                    }
                }
                return _resourceReferences;
            }
        }

        public override int AddResourceReference(ResourceCollection otherCollection, int otherResourceId)
        {
            var otherSpecificCollection = (ResourceCollection<TResourceLocation, TLocale>)otherCollection;
            int resourceIdx = _resourceIdentifications.Count;
            _resourceIdentifications.Add(otherSpecificCollection._resourceIdentifications[otherResourceId]);
            _resourceLocations.Add(otherSpecificCollection._resourceLocations[otherResourceId]);
            _resourceReferences = null;
            return resourceIdx;
        }

        public override int AddResourceReference(ResourceReference resourceRef)
        {
            int resourceIdx = _resourceIdentifications.Count;
            _resourceIdentifications.Add(MakeResourceIdentification(resourceRef));
            _resourceLocations.Add(MakeResourceLocation(resourceRef));
            if (_resourceReferences != null)
                _resourceReferences.Add(resourceRef);

            UpdateResourceReference(resourceIdx, resourceRef);
            return resourceIdx;
        }

        public override void UpdateResourceReference(int resourceIdx, ResourceReference resourceRef)
        {
            ResourceIdentification<TLocale> identification = _resourceIdentifications[resourceIdx];
            if (resourceRef.RefDefinitionsSize != null)
            {
                identification.RefDefinitionsSize = resourceRef.RefDefinitionsSize.Value;
                identification.BodySize = resourceRef.BodySize;
            }
            else
            {
                identification.BodySize = resourceRef.BodySize - identification.RefDefinitionsSize;
            }
            identification.Type = (byte)(resourceRef.Enabled ? resourceRef.Type : ResourceType.Empty);
            _resourceIdentifications[resourceIdx] = identification;

            TResourceLocation location = _resourceLocations[resourceIdx];
            UpdateResourceLocation(ref location, resourceRef);
            _resourceLocations[resourceIdx] = location;

            if (_resourceReferences != null)
                _resourceReferences[resourceIdx] = resourceRef;
        }

        public override IEnumerable<ResourceCollectionDependency> Dependencies
        {
            get
            {
                string platform = CdcGameInfo.Get(Game).ArchivePlatform; 
                foreach (ResourceCollectionDependency dependency in _dependencies.Concat(_includes))
                {
                    string filePath = $"{platform}\\{dependency.FilePath}";
                    if (!filePath.Contains('.'))
                        filePath += ".drm";

                    yield return new ResourceCollectionDependency(filePath, dependency.Locale);
                }
            }
        }

        public override void Write(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);

            _header.NumResources = _resourceIdentifications.Count;
            _header.DependenciesLength = _dependencies.Sum(d => DependencyLocaleSize + d.FilePath.Length + 1);
            _header.IncludeLength = _includes.Sum(d => DependencyLocaleSize + d.FilePath.Length + 1);
            writer.WriteStruct(_header);

            WriteLocale(writer, _headerLocale, HeaderLocaleSize);

            for (int i = 0; i < _header.NumResources; i++)
            {
                writer.WriteStruct(_resourceIdentifications[i]);
            }

            WriteDependencies(writer, _dependencies);
            WriteDependencies(writer, _includes);

            for (int i = 0; i < _header.NumResources; i++)
            {
                writer.WriteStruct(_resourceLocations[i]);
            }
        }

        protected abstract ResourceReference MakeResourceReference(ResourceIdentification<TLocale> identification, TResourceLocation location);
        protected abstract ResourceIdentification<TLocale> MakeResourceIdentification(ResourceReference resourceRef);
        protected abstract TResourceLocation MakeResourceLocation(ResourceReference resourceRef);
        protected abstract void UpdateResourceLocation(ref TResourceLocation location, ResourceReference resourceRef);

        protected abstract int DependencyLocaleSize { get; }

        private List<ResourceCollectionDependency> ReadDependencies(BinaryReader reader, int length)
        {
            int startPos = reader.Tell();
            List<ResourceCollectionDependency> dependencies = new();
            while (reader.Tell() < startPos + length)
            {
                ulong locale = ReadLocale(reader, DependencyLocaleSize);
                string filePath = reader.ReadZeroTerminatedString();
                dependencies.Add(new ResourceCollectionDependency(filePath, locale));
            }
            return dependencies;
        }

        private void WriteDependencies(BinaryWriter writer, List<ResourceCollectionDependency> dependencies)
        {
            foreach (ResourceCollectionDependency dependency in dependencies)
            {
                WriteLocale(writer, dependency.Locale, DependencyLocaleSize);
                writer.WriteZeroTerminatedString(dependency.FilePath);
            }
        }

        private static ulong ReadLocale(BinaryReader reader, int size)
        {
            return size switch
            {
                0 => 0xFFFFFFFFFFFFFFFF,
                4 => 0xFFFFFFFF00000000 | reader.ReadUInt32(),
                8 => reader.ReadUInt64()
            };
        }

        private static void WriteLocale(BinaryWriter writer, ulong locale, int size)
        {
            switch (size)
            {
                case 4:
                    writer.Write((uint)locale);
                    break;

                case 8:
                    writer.Write(locale);
                    break;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ResourceCollectionHeader
        {
            public int Version;
            public int IncludeLength;
            public int DependenciesLength;
            public int PaddingLength;
            public int Size;
            public int Flags;
            public int NumResources;
            public int MainResourceIndex;
        }

        [StructLayout(LayoutKind.Sequential)]
        protected struct ResourceIdentification<TLocale>
            where TLocale : unmanaged
        {
            public int BodySize;
            public byte Type;
            public byte Flags;
            public short Padding;
            public int SubTypeAndRefDefinitionsSize;
            public int Id;
            public TLocale Locale;

            public int SubType
            {
                get { return (SubTypeAndRefDefinitionsSize & 0xFF) >> 1; }
                set { SubTypeAndRefDefinitionsSize = (SubTypeAndRefDefinitionsSize & 0x7FFFFF01) | (value << 1); }
            }

            public int RefDefinitionsSize
            {
                get { return SubTypeAndRefDefinitionsSize >> 8; }
                set { SubTypeAndRefDefinitionsSize = (SubTypeAndRefDefinitionsSize & 0xFF) | (value << 8); }
            }
        }
    }
}
