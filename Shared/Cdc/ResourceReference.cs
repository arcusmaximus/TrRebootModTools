namespace TrRebootTools.Shared.Cdc
{
    public class ResourceReference : ArchiveBlobReference
    {
        public ResourceReference(
            ResourceType type,
            ResourceSubType subType,
            int resourceId,
            ulong locale,
            int archiveId,
            int archiveSubId,
            int archivePart,
            int offsetInArchive,
            int sizeInArchive,
            int decompressionOffset,
            int? refDefinitionsSize,
            int bodySize)
            : base(archiveId, archiveSubId, archivePart, offsetInArchive, sizeInArchive)
        {
            Type = type;
            SubType = subType;
            Id = resourceId;
            Locale = locale;
            DecompressionOffset = decompressionOffset;
            RefDefinitionsSize = refDefinitionsSize;
            BodySize = bodySize;
        }

        public ResourceType Type
        {
            get;
        }

        public ResourceSubType SubType
        {
            get;
        }

        public int Id
        {
            get;
        }

        public ulong Locale
        {
            get;
        }

        public int DecompressionOffset
        {
            get;
        }

        public int? RefDefinitionsSize
        {
            get;
        }

        public int BodySize
        {
            get;
        }

        public bool Enabled
        {
            get;
            set;
        } = true;

        public override string ToString()
        {
            return $"{(ResourceKey)this} -> Archive {ArchiveId}:{ArchiveSubId}:{ArchivePart}, Offset {Offset:X}, OffsetInBatch {DecompressionOffset:X}";
        }

        public static implicit operator ResourceKey(ResourceReference resourceRef)
        {
            return new ResourceKey(resourceRef.Type, resourceRef.SubType, resourceRef.Id, resourceRef.Locale);
        }
    }
}
