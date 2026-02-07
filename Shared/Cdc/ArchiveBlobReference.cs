namespace TrRebootTools.Shared.Cdc
{
    public class ArchiveBlobReference
    {
        public ArchiveBlobReference(int archiveId, int archiveSubId, int archivePart, uint offset, uint length)
        {
            ArchiveId = archiveId;
            ArchiveSubId = archiveSubId;
            ArchivePart = archivePart;
            Offset = offset;
            Length = length;
        }

        public int ArchiveId
        {
            get;
        }

        public int ArchiveSubId
        {
            get;
        }

        public int ArchivePart
        {
            get;
        }

        public uint Offset
        {
            get;
        }

        public uint Length
        {
            get;
        }

        protected bool Equals(ArchiveBlobReference other)
        {
            return ArchiveId == other.ArchiveId &&
                   ArchiveSubId == other.ArchiveSubId &&
                   ArchivePart == other.ArchivePart &&
                   Offset == other.Offset &&
                   Length == other.Length;
        }

        public override bool Equals(object? obj)
        {
            return obj is ArchiveBlobReference other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = ArchiveId;
                hashCode = hashCode * 397 ^ ArchiveSubId;
                hashCode = hashCode * 397 ^ ArchivePart;
                hashCode = hashCode * 397 ^ (int)Offset;
                hashCode = hashCode * 397 ^ (int)Length;
                return hashCode;
            }
        }
    }
}
