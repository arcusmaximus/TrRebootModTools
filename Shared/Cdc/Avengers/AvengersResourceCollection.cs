using System.IO;
using System.Runtime.InteropServices;

namespace TrRebootTools.Shared.Cdc.Avengers
{
    internal class AvengersResourceCollection : ResourceCollection<
        AvengersResourceCollection.ResourceCollectionHeader,
        AvengersResourceCollection.ResourceIdentification,
        AvengersResourceCollection.ResourceLocation,
        uint
    >
    {
        public AvengersResourceCollection(ulong nameHash, ulong locale, Stream stream)
            : base(nameHash, locale, stream)
        {
        }

        public override CdcGame Game => CdcGame.Avengers;

        protected override int HeaderVersion => 22;

        protected override int HeaderLocaleSize => 0;

        protected override ResourceKey ToResourceKey(ResourceIdentification identification)
        {
            return new ResourceKey(
                (ResourceType)identification.Type,
                (ResourceSubType)identification.SubType,
                identification.Id,
                0xFFFFFFFF00000000 | identification.Locale
            );
        }

        protected override ResourceDescriptor MakeResourceDescriptor(ResourceIdentification identification, ResourceLocation location)
        {
            return new ResourceDescriptor(
                (ResourceType)location.Type,
                (ResourceSubType)identification.SubType,
                location.Id,
                identification.Locale,
                location.ArchiveId,
                location.ArchiveSubId,
                location.ArchivePart,
                location.OffsetInArchive,
                location.SizeInArchive,
                location.DecompressionOffset,
                identification.RefDefinitionsSize,
                identification.BodySize
            ) { Enabled = identification.Type != (byte)ResourceType.Empty };
        }

        protected override ResourceIdentification MakeResourceIdentification(ResourceDescriptor resource)
        {
            return new ResourceIdentification
            {
                Type = (byte)resource.Type,
                SubType = (int)resource.SubType,
                Id = resource.Id,
                Locale = (uint)resource.Locale
            };
        }

        protected override void UpdateResourceIdentification(ref ResourceIdentification identification, ResourceDescriptor resource)
        {
            if (resource.RefDefinitionsSize != null)
            {
                identification.RefDefinitionsSize = resource.RefDefinitionsSize.Value;
                identification.BodySize = resource.BodySize;
            }
            else
            {
                identification.BodySize = resource.BodySize - identification.RefDefinitionsSize;
            }
            if (resource.Enabled)
                identification.Type = (byte)(resource.Type < ResourceType.Max ? resource.Type : ResourceType.CollisionModel);
            else
                identification.Type = (byte)ResourceType.Empty;
        }

        protected override ResourceLocation MakeResourceLocation(ResourceDescriptor resource)
        {
            return new ResourceLocation
            {
                Type = (int)resource.Type,
                Id = resource.Id
            };
        }

        protected override void UpdateResourceLocation(ref ResourceLocation location, ResourceDescriptor resource)
        {
            location.ArchiveId = (ushort)resource.ArchiveId;
            location.ArchiveSubId = resource.ArchiveSubId;
            location.ArchivePart = resource.ArchivePart;
            location.OffsetInArchive = resource.Offset;
            location.SizeInArchive = resource.Length;
            location.DecompressionOffset = resource.DecompressionOffset;
        }

        protected override int DependencyLocaleSize => 0;

        [StructLayout(LayoutKind.Sequential)]
        public struct ResourceCollectionHeader : IHeader
        {
            public int Version;
            public int IncludeLength;
            public int DependenciesLength;
            public int PaddingLength;
            public int Flags;
            public int NumResources;
            public int MainResourceIndex;

            int IHeader.Version
            {
                get => Version;
                set => Version = value;
            }

            int IHeader.IncludeLength
            {
                get => IncludeLength;
                set => IncludeLength = value;
            }

            int IHeader.DependenciesLength
            {
                get => DependenciesLength;
                set => DependenciesLength = value;
            }

            int IHeader.NumResources
            {
                get => NumResources;
                set => NumResources = value;
            }

            int IHeader.MainResourceIndex
            {
                get => MainResourceIndex;
                set => MainResourceIndex = value;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ResourceIdentification
        {
            public uint BodySize;
            public byte Type;
            public byte Flags;
            public short Padding0;
            public ulong Hash;
            public uint SubTypeAndRefDefinitionsSize;
            public int Id;
            public uint Locale;
            public int Padding1;

            public int SubType
            {
                get => (int)((SubTypeAndRefDefinitionsSize >> 1) & 0x7F);
                set => SubTypeAndRefDefinitionsSize = (SubTypeAndRefDefinitionsSize & 0x7FFFFF01) | (uint)(value << 1);
            }

            public uint RefDefinitionsSize
            {
                get => (SubTypeAndRefDefinitionsSize >> 12) * 4;
                set => SubTypeAndRefDefinitionsSize = (SubTypeAndRefDefinitionsSize & 0xFFF) | ((value / 4) << 12);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ResourceLocation
        {
            public ulong UniqueKey;
            public uint OffsetInArchive;
            public ushort ArchiveId;
            public ushort ArchiveSubIdAndPart;
            public uint SizeInArchive;
            public uint DecompressionOffset;

            public int Type
            {
                get { return (int)((UniqueKey >> 24) & 0xFF); }
                set { UniqueKey = (UniqueKey & ~0xFF000000) | ((uint)value << 24); }
            }

            public int Id
            {
                get { return (int)(UniqueKey & 0x00FFFFFF); }
                set { UniqueKey = (UniqueKey & ~0xFFFFFFu) | (uint)value; }
            }

            public int ArchiveSubId
            {
                get
                {
                    int subId = ArchiveSubIdAndPart & 0x3F;
                    return subId == 0x3F ? 0 : subId;
                }
                set
                {
                    if (value == 0)
                        value = 0x3F;

                    ArchiveSubIdAndPart = (ushort)((ArchiveSubIdAndPart & ~0x3F) | value);
                }
            }

            public int ArchivePart
            {
                get => ArchiveSubIdAndPart >> 6;
                set => ArchiveSubIdAndPart = (ushort)((ArchiveSubIdAndPart & 0x3F) | (value << 6));
            }
        }
    }
}
