using System.IO;
using System.Runtime.InteropServices;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Cdc.Avengers
{
    internal class AvengersArchive : Archive
    {
        public AvengersArchive(string baseFilePath, ArchiveMetaData? metaData)
            : base(baseFilePath, metaData)
        {
        }

        protected override CdcGame Game => CdcGame.Avengers;
        protected override int HeaderVersion => 8;
        protected override bool SupportsSubId => false;
        protected override bool SupportsLanguageList => true;

        internal override Stream OpenPart(int partIdx, string filePath, FileMode mode, FileAccess access)
        {
            Stream stream = base.OpenPart(partIdx, filePath, mode, access);
            if (Id <= 11)
                stream = new AvengersArchiveStream(Id, partIdx, stream);

            return stream;
        }

        protected override ArchiveFileReference ReadFileReference(BinaryReader reader)
        {
            var entry = reader.ReadStruct<ArchiveFileEntry>();
            int subId = entry.ArchivePart & 0x3F;
            if (subId == 0x3F)
                subId = 0;

            int part = entry.ArchivePart >> 6;
            return new ArchiveFileReference(
                entry.NameHash,
                entry.Locale,
                entry.ArchiveId,
                subId,
                part,
                entry.Offset,
                entry.UncompressedSize
            );
        }

        protected override void WriteFileReference(BinaryWriter writer, ArchiveFileReference fileRef)
        {
            int subId = fileRef.ArchiveSubId == 0 ? 0x3F : fileRef.ArchiveSubId;
            ArchiveFileEntry entry =
                new()
                {
                    NameHash = fileRef.NameHash,
                    Locale = (uint)fileRef.Locale,
                    ArchiveId = (short)fileRef.ArchiveId,
                    ArchivePart = (short)((fileRef.ArchivePart << 6) | fileRef.ArchiveSubId),
                    Offset = fileRef.Offset,
                    UncompressedSize = fileRef.Length
                };
            writer.WriteStruct(entry);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ArchiveFileEntry
        {
            public ulong NameHash;
            public uint Locale;
            public uint UncompressedSize;
            public uint Offset;
            public short ArchiveId;
            public short ArchivePart;
        }
    }
}
