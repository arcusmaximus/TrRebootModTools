using System;
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

        internal override Stream OpenPart(string filePath, int partIdx, FileMode mode, FileAccess access)
        {
            Stream stream = base.OpenPart(filePath, partIdx, mode, access);
            if (stream.Length < 4)
                return stream;

            Span<byte> magic = stackalloc byte[4];
            stream.Read(magic);
            stream.Position = 0;
            if (BitConverter.ToInt32(magic) == Magic)
                return stream;
            
            return new AvengersEncryptedArchiveStream(partIdx, filePath, stream);
        }

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
                new()
                {
                    NameHash = fileRef.NameHash,
                    Locale = (uint)fileRef.Locale,
                    ArchiveId = (ushort)fileRef.ArchiveId,
                    ArchiveSubId = fileRef.ArchiveSubId,
                    ArchivePart = fileRef.ArchivePart,
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
            public ushort ArchiveId;
            public ushort ArchiveSubIdAndPart;

            public int ArchiveSubId
            {
                get
                {
                    int subId = ArchiveSubIdAndPart & 0x3F;
                    return subId == 0x3F ? 0 : subId;
                }
                set
                {
                    if (value == 0)
                        value = 0x3F;

                    ArchiveSubIdAndPart = (ushort)((ArchiveSubIdAndPart & ~0x3F) | value);
                }
            }

            public int ArchivePart
            {
                get => ArchiveSubIdAndPart >> 6;
                set => ArchiveSubIdAndPart = (ushort)((ArchiveSubIdAndPart & 0x3F) | (value << 6));
            }
        }
    }
}
