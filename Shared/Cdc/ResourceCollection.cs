using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrRebootTools.Shared.Cdc.Avengers;
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
                CdcGame.Tr2013      => new Tr2013ResourceCollection(nameHash, locale, stream),
                CdcGame.Rise        => new RiseResourceCollection(nameHash, locale, stream),
                CdcGame.Shadow      => new ShadowResourceCollection(nameHash, locale, stream),
                CdcGame.Avengers    => new AvengersResourceCollection(nameHash, locale, stream),
                _ => throw new NotSupportedException()
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

        public abstract ResourceDescriptor? MainResource
        {
            get;
        }

        public abstract IReadOnlyList<ResourceDescriptor> Resources { get; }

        public abstract bool Contains(ResourceKey resourceKey);

        public ResourceDescriptor? Get(ResourceKey resourceKey)
        {
            return Resources.FirstOrDefault(r => resourceKey.Equals(r));
        }

        public abstract int Add(ResourceCollection otherCollection, int otherResourceId);
        public abstract int Add(ResourceDescriptor resource);
        public abstract void Set(int index, ResourceDescriptor resource);

        public abstract IEnumerable<ResourceCollectionDependency> Dependencies { get; }

        public abstract void Write(Stream stream);
    }

    internal abstract class ResourceCollection<THeader, TResourceIdentification, TResourceLocation, TLocale> : ResourceCollection
        where THeader : unmanaged, ResourceCollection<THeader, TResourceIdentification, TResourceLocation, TLocale>.IHeader
        where TResourceIdentification : unmanaged
        where TResourceLocation : unmanaged
        where TLocale : unmanaged
    {
        private THeader _header;
        private readonly ulong _headerLocale;
        private readonly List<TResourceIdentification> _resourceIdentifications = [];
        private readonly List<ResourceCollectionDependency> _dependencies;
        private readonly List<ResourceCollectionDependency> _includes;
        private readonly List<TResourceLocation> _resourceLocations = [];

        private List<ResourceDescriptor>? _resourceDescriptors;
        private HashSet<ResourceKey>? _resourceKeyLookup;

        protected ResourceCollection(ulong nameHash, ulong locale, Stream stream)
            : base(nameHash, locale)
        {
            BinaryReader reader = new(stream);
            _header = reader.ReadStruct<THeader>();
            if (_header.Version != HeaderVersion)
                throw new NotSupportedException($"Only version {HeaderVersion} .drm files are supported");

            _headerLocale = ReadLocale(reader, HeaderLocaleSize);

            for (int i = 0; i < _header.NumResources; i++)
            {
                _resourceIdentifications.Add(reader.ReadStruct<TResourceIdentification>());
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

        public override ResourceDescriptor? MainResource
        {
            get => _header.MainResourceIndex >= 0 ? Resources[_header.MainResourceIndex] : null;
        }

        public override IReadOnlyList<ResourceDescriptor> Resources
        {
            get
            {
                if (_resourceDescriptors == null)
                {
                    _resourceDescriptors = [];
                    for (int i = 0; i < _resourceIdentifications.Count; i++)
                    {
                        var identification = _resourceIdentifications[i];
                        var location = _resourceLocations[i];
                        ResourceDescriptor resourceDesc = MakeResourceDescriptor(identification, location);
                        _resourceDescriptors.Add(resourceDesc);
                    }
                }
                return _resourceDescriptors;
            }
        }

        public override bool Contains(ResourceKey resourceKey)
        {
            _resourceKeyLookup ??= _resourceIdentifications.Select(ToResourceKey).ToHashSet();
            return _resourceKeyLookup.Contains(resourceKey);
        }

        public override int Add(ResourceCollection otherCollection, int otherResourceId)
        {
            var otherSpecificCollection = (ResourceCollection<THeader, TResourceIdentification, TResourceLocation, TLocale>)otherCollection;
            int resourceIdx = _resourceIdentifications.Count;
            var identification = otherSpecificCollection._resourceIdentifications[otherResourceId];
            var location = otherSpecificCollection._resourceLocations[otherResourceId];
            _resourceIdentifications.Add(identification);
            _resourceLocations.Add(location);
            _resourceDescriptors = null;
            _resourceKeyLookup?.Add(ToResourceKey(identification));
            return resourceIdx;
        }

        public override int Add(ResourceDescriptor resourceDesc)
        {
            int resourceIdx = _resourceIdentifications.Count;
            _resourceIdentifications.Add(MakeResourceIdentification(resourceDesc));
            _resourceLocations.Add(MakeResourceLocation(resourceDesc));
            _resourceDescriptors?.Add(resourceDesc);
            _resourceKeyLookup?.Add(resourceDesc);

            Set(resourceIdx, resourceDesc);
            return resourceIdx;
        }

        public override void Set(int index, ResourceDescriptor resourceDesc)
        {
            TResourceIdentification identification = _resourceIdentifications[index];
            UpdateResourceIdentification(ref identification, resourceDesc);
            _resourceIdentifications[index] = identification;

            TResourceLocation location = _resourceLocations[index];
            UpdateResourceLocation(ref location, resourceDesc);
            _resourceLocations[index] = location;

            if (_resourceDescriptors != null)
                _resourceDescriptors[index] = resourceDesc;
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
            BinaryWriter writer = new(stream);

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

        protected abstract ResourceKey ToResourceKey(TResourceIdentification identification);
        protected abstract ResourceDescriptor MakeResourceDescriptor(TResourceIdentification identification, TResourceLocation location);

        protected abstract TResourceIdentification MakeResourceIdentification(ResourceDescriptor resourceDesc);
        protected abstract void UpdateResourceIdentification(ref TResourceIdentification identification, ResourceDescriptor resourceDesc);

        protected abstract TResourceLocation MakeResourceLocation(ResourceDescriptor resourceDesc);
        protected abstract void UpdateResourceLocation(ref TResourceLocation location, ResourceDescriptor resourceDesc);

        protected abstract int DependencyLocaleSize { get; }

        private List<ResourceCollectionDependency> ReadDependencies(BinaryReader reader, int length)
        {
            int startPos = reader.Tell();
            List<ResourceCollectionDependency> dependencies = [];
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
                8 => reader.ReadUInt64(),
                _ => throw new ArgumentException()
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

        internal interface IHeader
        {
            int Version { get; set; }
            int IncludeLength { get; set; }
            int DependenciesLength { get; set; }
            int NumResources { get; set; }
            int MainResourceIndex { get; set; }
        }
    }
}
