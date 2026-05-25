namespace TrRebootTools.Shared.Cdc
{
    public class ArchiveFileDescriptor : ArchiveBlobDescriptor
    {
        public ArchiveFileDescriptor(ulong nameHash, ulong locale, int archiveId, int archiveSubId, int archivePart, uint offset, uint length)
            : base(archiveId, archiveSubId, archivePart, offset, length)
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

        public static implicit operator ArchiveFileKey(ArchiveFileDescriptor file)
        {
            return new ArchiveFileKey(file.NameHash, file.Locale);
        }
    }
}
