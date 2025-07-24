using System.IO;
using System.Runtime.InteropServices;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Cdc.Rise
{
    internal class RiseArchive : Archive
    {
        public RiseArchive(string baseFilePath, ArchiveMetaData metaData)
            : base(baseFilePath, metaData)
        {
        }

        protected override CdcGame Game => CdcGame.Rise;

        protected override int HeaderVersion => 4;

        protected override bool SupportsSubId => false;

        protected override ArchiveFileReference ReadFileReference(BinaryReader reader)
        {
            var entry = reader.ReadStruct<ArchiveFileEntry>();
            return new ArchiveFileReference(
                entry.NameHash,
                0xFFFFFFFF00000000 | entry.Locale,
                entry.ArchiveId,
                entry.ArchiveId == Id ? SubId : 0,
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
                    NameHash = (uint)fileRef.NameHash,
                    Locale = (uint)fileRef.Locale,
                    ArchiveId = (short)fileRef.ArchiveId,
                    ArchivePart = (short)fileRef.ArchivePart,
                    Offset = fileRef.Offset,
                    UncompressedSize = fileRef.Length
                };
            writer.WriteStruct(entry);
        }

        [StructLayout(LayoutKind.Sequential)]
        protected struct ArchiveFileEntry
        {
            public uint NameHash;
            public uint Locale;
            public int UncompressedSize;
            public int CompressedSize;
            public short ArchivePart;
            public short ArchiveId;
            public int Offset;
        }
    }
}
