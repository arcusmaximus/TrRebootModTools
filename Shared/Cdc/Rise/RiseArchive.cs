using System.IO;
using System.Runtime.InteropServices;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Cdc.Rise
{
    internal class RiseArchive : Archive
    {
        public RiseArchive(string baseFilePath, ArchiveMetaData? metaData)
            : base(baseFilePath, metaData)
        {
        }

        protected override CdcGame Game => CdcGame.Rise;
        protected override int HeaderVersion => 4;
        protected override bool SupportsSubId => false;
        protected override bool SupportsLanguageList => false;

        protected override ArchiveFileDescriptor ReadFileDescriptor(BinaryReader reader)
        {
            var entry = reader.ReadStruct<ArchiveFileEntry>();
            return new ArchiveFileDescriptor(
                entry.NameHash,
                0xFFFFFFFF00000000 | entry.Locale,
                entry.ArchiveId,
                entry.ArchiveId == Id ? SubId : 0,
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
                    NameHash = (uint)file.NameHash,
                    Locale = (uint)file.Locale,
                    ArchiveId = (short)file.ArchiveId,
                    ArchivePart = (short)file.ArchivePart,
                    Offset = file.Offset,
                    UncompressedSize = file.Length
                };
            writer.WriteStruct(entry);
        }

        [StructLayout(LayoutKind.Sequential)]
        protected struct ArchiveFileEntry
        {
            public uint NameHash;
            public uint Locale;
            public uint UncompressedSize;
            public uint CompressedSize;
            public short ArchivePart;
            public short ArchiveId;
            public uint Offset;
        }
    }
}
