using System.IO;
using System.Runtime.InteropServices;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Cdc.Shadow
{
    internal class ShadowArchive : Archive
    {
        public ShadowArchive(string baseFilePath, ArchiveMetaData? metaData)
            : base(baseFilePath, metaData)
        {
        }

        protected override CdcGame Game => CdcGame.Shadow;
        protected override int HeaderVersion => 5;
        protected override bool SupportsSubId => true;
        protected override bool SupportsLanguageList => false;

        protected override ArchiveFileDescriptor ReadFileDescriptor(BinaryReader reader)
        {
            var entry = reader.ReadStruct<ArchiveFileEntry>();
            return new ArchiveFileDescriptor(
                entry.NameHash,
                entry.Locale,
                entry.ArchiveId,
                entry.ArchiveSubId,
                entry.ArchivePart,
                entry.Offset,
                entry.UncompressedSize
            );
        }

        protected override void WriteFileDescriptor(BinaryWriter writer, ArchiveFileDescriptor file)
        {
            ArchiveFileEntry entry =
                new()
                {
                    NameHash = file.NameHash,
                    Locale = file.Locale,
                    ArchiveId = (byte)file.ArchiveId,
                    ArchiveSubId = (byte)file.ArchiveSubId,
                    ArchivePart = (short)file.ArchivePart,
                    Offset = file.Offset,
                    UncompressedSize = file.Length
                };
            writer.WriteStruct(entry);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ArchiveFileEntry
        {
            public ulong NameHash;
            public ulong Locale;
            public uint UncompressedSize;
            public uint CompressedSize;
            public short ArchivePart;
            public byte ArchiveId;
            public byte ArchiveSubId;
            public uint Offset;
        }
    }
}
