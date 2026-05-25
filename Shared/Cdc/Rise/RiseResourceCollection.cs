using System.IO;
using System.Runtime.InteropServices;

namespace TrRebootTools.Shared.Cdc.Rise
{
    internal class RiseResourceCollection : TrResourceCollection<RiseResourceCollection.ResourceLocation, uint>
    {
        public RiseResourceCollection(ulong nameHash, ulong locale, Stream stream)
            : base(nameHash, locale, stream)
        {
        }

        public override CdcGame Game => CdcGame.Rise;

        protected override int HeaderVersion => 22;

        protected override int HeaderLocaleSize => 0;

        protected override int DependencyLocaleSize => 4;

        protected override ResourceDescriptor MakeResourceDescriptor(ResourceIdentification identification, ResourceLocation location)
        {
            return new ResourceDescriptor(
                (ResourceType)location.Type,
                (ResourceSubType)identification.SubType,
                location.Id,
                0xFFFFFFFF00000000 | identification.Locale,
                location.ArchiveId,
                0,
                location.ArchivePart,
                location.OffsetInArchive,
                location.SizeInArchive,
                location.DecompressionOffset,
                identification.RefDefinitionsSize,
                identification.BodySize
            ) { Enabled = identification.Type != (byte)ResourceType.Empty };
        }

        protected override ResourceIdentification MakeResourceIdentification(ResourceDescriptor resourceDesc)
        {
            return new ResourceIdentification
            {
                Type = (byte)resourceDesc.Type,
                SubType = (int)resourceDesc.SubType,
                Id = resourceDesc.Id,
                Locale = (uint)resourceDesc.Locale
            };
        }

        protected override ResourceLocation MakeResourceLocation(ResourceDescriptor resourceDesc)
        {
            return new ResourceLocation
            {
                Type = (int)resourceDesc.Type,
                Id = resourceDesc.Id
            };
        }

        protected override void UpdateResourceLocation(ref ResourceLocation location, ResourceDescriptor resourceDesc)
        {
            location.ArchiveId = (byte)resourceDesc.ArchiveId;
            location.ArchivePart = (short)resourceDesc.ArchivePart;
            location.OffsetInArchive = resourceDesc.Offset;
            location.SizeInArchive = resourceDesc.Length;
            location.DecompressionOffset = resourceDesc.DecompressionOffset;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ResourceLocation
        {
            public int UniqueKey;
            public int Padding;
            public short ArchivePart;
            public short ArchiveId;
            public uint OffsetInArchive;
            public uint SizeInArchive;
            public uint DecompressionOffset;

            public int Type
            {
                get { return UniqueKey >> 24; }
                set { UniqueKey = (value << 24) | (UniqueKey & 0x00FFFFFF); }
            }

            public int Id
            {
                get { return UniqueKey & 0x00FFFFFF; }
                set { UniqueKey = (int)(UniqueKey & 0xFF000000) | value; }
            }
        }
    }
}
