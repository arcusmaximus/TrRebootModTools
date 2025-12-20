using System.IO;
using System.Runtime.InteropServices;

namespace TrRebootTools.Shared.Cdc.Tr2013
{
    internal class Tr2013ResourceCollection : ResourceCollection<Tr2013ResourceCollection.ResourceLocation, uint>
    {
        private const int Patch3ArchiveId = 69;

        public Tr2013ResourceCollection(ulong nameHash, ulong locale, Stream stream)
            : base(nameHash, locale, stream)
        {
        }

        public override CdcGame Game => CdcGame.Tr2013;

        protected override int HeaderVersion => 22;

        protected override int HeaderLocaleSize => 0;

        protected override int DependencyLocaleSize => 0;

        protected override ResourceReference MakeResourceReference(ResourceIdentification<uint> identification, ResourceLocation location)
        {
            ResourceKey resourceKey = AdjustResourceKeyAfterRead(location.ArchiveId, new ResourceKey((ResourceType)location.Type, location.Id));
            return new ResourceReference(
                resourceKey.Type,
                (ResourceSubType)identification.SubType,
                resourceKey.Id,
                0xFFFFFFFF00000000 | identification.Locale,
                location.ArchiveId,
                0,
                location.ArchivePart,
                location.OffsetInArchive,
                location.SizeInArchive,
                location.DecompressionOffset,
                identification.RefDefinitionsSize,
                identification.BodySize
            );
        }

        protected override ResourceIdentification<uint> MakeResourceIdentification(ResourceReference resourceRef)
        {
            ResourceKey resourceKey = AdjustResourceKeyBeforeWrite(resourceRef.ArchiveId, resourceRef);
            return new ResourceIdentification<uint>
            {
                Type = (byte)resourceRef.Type,
                SubType = (int)resourceRef.SubType,
                Id = resourceKey.Id,
                Locale = (uint)resourceRef.Locale
            };
        }

        protected override ResourceLocation MakeResourceLocation(ResourceReference resourceRef)
        {
            ResourceKey resourceKey = AdjustResourceKeyBeforeWrite(resourceRef.ArchiveId, resourceRef);
            return new ResourceLocation
            {
                Type = (int)resourceRef.Type,
                Id = resourceKey.Id
            };
        }

        protected override void UpdateResourceLocation(ref ResourceLocation location, ResourceReference resourceRef)
        {
            location.PackedOffset = (resourceRef.Offset | (resourceRef.ArchiveId << 4) | resourceRef.ArchivePart);
            location.SizeInArchive = resourceRef.Length;
            location.DecompressionOffset = resourceRef.DecompressionOffset;
        }

        public static ResourceKey AdjustResourceKeyAfterRead(int archiveId, ResourceKey resourceKey)
        {
            return AdjustResourceKey(archiveId, resourceKey, true);
        }

        public static ResourceKey AdjustResourceKeyBeforeWrite(int archiveId, ResourceKey resourceKey)
        {
            return AdjustResourceKey(archiveId, resourceKey, false);
        }

        private static ResourceKey AdjustResourceKey(int archiveId, ResourceKey resourceKey, bool add)
        {
            if (archiveId != Patch3ArchiveId)
                return resourceKey;

            switch (resourceKey.Type)
            {
                case ResourceType.Material:
                case ResourceType.Model:
                case ResourceType.Texture:
                case ResourceType.ShaderLib:
                case ResourceType.Dtp:
                case ResourceType.Script:
                    return new ResourceKey(resourceKey.Type, resourceKey.SubType, resourceKey.Id + (add ? 1 : -1) * 400000, resourceKey.Locale);

                case ResourceType.GlobalContentReference:
                    return new ResourceKey(resourceKey.Type, resourceKey.SubType, resourceKey.Id + (add ? 1 : -1) * 40000, resourceKey.Locale);

                default:
                    return resourceKey;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ResourceLocation
        {
            public int UniqueKey;
            public int PackedOffset;
            public int SizeInArchive;
            public int DecompressionOffset;

            public int Type
            {
                get => UniqueKey >> 24;
                set => UniqueKey = (value << 24) | (UniqueKey & 0x00FFFFFF);
            }

            public int Id
            {
                get => UniqueKey & 0x00FFFFFF;
                set => UniqueKey = (int)(UniqueKey & 0xFF000000) | value;
            }

            public int ArchiveId
            {
                get => (PackedOffset >> 4) & 0x7F;
                set => PackedOffset = (int)(PackedOffset & 0xFFFFF80F) | (value << 4);
            }

            public int ArchivePart
            {
                get => PackedOffset & 0xF;
                set => PackedOffset = (int)(PackedOffset & 0xFFFFFFF0) | value;
            }

            public int OffsetInArchive
            {
                get => (int)(PackedOffset & 0xFFFFF800);
                set => PackedOffset = (PackedOffset & 0x7FF) | value;
            }
        }
    }
}
