using System.IO;
using System.Runtime.InteropServices;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Cdc.Shadow
{
    internal class ShadowArchive : Archive
    {
        public ShadowArchive(string baseFilePath, ArchiveMetaData metaData)
            : base(baseFilePath, metaData)
        {
        }

        protected override CdcGame Game => CdcGame.Shadow;

        protected override int HeaderVersion => 5;

        protected override bool SupportsSubId => true;

        protected override ArchiveFileReference ReadFileReference(BinaryReader reader)
        {
            var entry = reader.ReadStruct<ArchiveFileEntry>();
            return new ArchiveFileReference(
                entry.NameHash,
                entry.Locale,
                entry.ArchiveId,
                entry.ArchiveSubId,
                entry.ArchivePart,
                entry.Offset,
                entry.UncompressedSize
            );
        }

        protected override void WriteFileReference(BinaryWriter writer, ArchiveFileReference fileRef)
        {
            ArchiveFileEntry entry =
                new ArchiveFileEntry
                {
                    NameHash = fileRef.NameHash,
                    Locale = fileRef.Locale,
                    ArchiveId = (byte)fileRef.ArchiveId,
                    ArchiveSubId = (byte)fileRef.ArchiveSubId,
                    ArchivePart = (short)fileRef.ArchivePart,
                    Offset = fileRef.Offset,
                    UncompressedSize = fileRef.Length
                };
            writer.WriteStruct(entry);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ArchiveFileEntry
        {
            public ulong NameHash;
            public ulong Locale;
            public int UncompressedSize;
            public int CompressedSize;
            public short ArchivePart;
            public byte ArchiveId;
            public byte ArchiveSubId;
            public int Offset;
        }
    }
}
