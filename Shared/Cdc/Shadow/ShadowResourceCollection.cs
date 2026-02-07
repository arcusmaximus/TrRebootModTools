using System.IO;
using System.Runtime.InteropServices;

namespace TrRebootTools.Shared.Cdc.Shadow
{
    internal class ShadowResourceCollection : ResourceCollection<ShadowResourceCollection.ResourceLocation, ulong>
    {
        public ShadowResourceCollection(ulong nameHash, ulong locale, Stream stream)
            : base(nameHash, locale, stream)
        {
        }

        public override CdcGame Game => CdcGame.Shadow;

        protected override int HeaderVersion => 23;

        protected override int HeaderLocaleSize => 8;

        protected override int DependencyLocaleSize => 8;

        protected override ResourceReference MakeResourceReference(ResourceIdentification identification, ResourceLocation location)
        {
            return new ResourceReference(
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
            );
        }

        protected override ResourceIdentification MakeResourceIdentification(ResourceReference resourceRef)
        {
            return new ResourceIdentification
            {
                Type = (byte)resourceRef.Type,
                SubType = (int)resourceRef.SubType,
                Id = resourceRef.Id,
                Locale = resourceRef.Locale
            };
        }

        protected override ResourceLocation MakeResourceLocation(ResourceReference resourceRef)
        {
            return new ResourceLocation
            {
                Type = (int)resourceRef.Type,
                Id = resourceRef.Id
            };
        }

        protected override void UpdateResourceLocation(ref ResourceLocation location, ResourceReference resourceRef)
        {
            location.ArchiveId = (byte)resourceRef.ArchiveId;
            location.ArchiveSubId = (byte)resourceRef.ArchiveSubId;
            location.ArchivePart = (short)resourceRef.ArchivePart;
            location.OffsetInArchive = resourceRef.Offset;
            location.SizeInArchive = resourceRef.Length;
            location.DecompressionOffset = resourceRef.DecompressionOffset;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ResourceLocation
        {
            public int UniqueKey;
            public int Padding;
            public short ArchivePart;
            public byte ArchiveId;
            public byte ArchiveSubId;
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
