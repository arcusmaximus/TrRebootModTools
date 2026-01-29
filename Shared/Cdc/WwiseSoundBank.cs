using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Cdc
{
    public class WwiseSoundBank
    {
        private delegate Section SectionFactory(WwiseSoundBank bank, string tag, BinaryReader reader, int length);
        private delegate HircEntry HircEntryFactory(byte type, byte[] data);

        private static readonly Dictionary<string, SectionFactory> SectionFactories =
            new Dictionary<string, SectionFactory>
            {
                { "BKHD", (bank, tag, reader, length) => new HeaderSection(bank, tag, reader, length) },
                { "DIDX", (bank, tag, reader, length) => new DataIndexSection(tag, reader, length) },
                { "DATA", (bank, tag, reader, length) => new DataSection(bank, tag, reader, length) },
                { "HIRC", (bank, tag, reader, length) => new HircSection(tag, reader) }
            };

        private static readonly SectionFactory DefaultSectionFactory = (bank, tag, reader, length) => new UnknownSection(tag, reader, length);

        private static readonly Dictionary<byte, HircEntryFactory> HircEntryFactories =
            new Dictionary<byte, HircEntryFactory>
            {
                { 2, (type, data) => new HircSoundEntry(type, data) }
            };

        private static readonly HircEntryFactory DefaultHircEntryFactory = (type, data) => new HircEntry(type, data);

        private readonly List<Section> _sections = new();

        public WwiseSoundBank(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            Id = reader.ReadInt32();
            int bankLength = reader.ReadInt32();

            int position = 0;
            while (position < bankLength)
            {
                string sectionTag = Encoding.ASCII.GetString(reader.ReadBytes(4));
                int sectionLength = reader.ReadInt32();

                SectionFactory factory = SectionFactories.GetValueOrDefault(sectionTag) ?? DefaultSectionFactory;
                Section section = factory(this, sectionTag, reader, sectionLength);
                _sections.Add(section);

                position += 8 + sectionLength;
            }

            GetEmbeddedSoundsFromSections();
        }

        public int Id
        {
            get;
        }

        public Dictionary<int, ArraySegment<byte>> EmbeddedSounds
        {
            get;
        } = new();

        public IEnumerable<int> ReferencedSoundIds
        {
            get
            {
                HircSection? section = GetSection<HircSection>();
                if (section == null)
                    return [];

                return section.Entries.OfType<HircSoundEntry>().Select(e => e.SoundId);
            }
        }

        public void Write(Stream stream)
        {
            ApplyEmbeddedSoundsToSections();

            BinaryWriter writer = new(stream);
            writer.Write(Id);
            writer.Write(0);
            foreach (Section section in _sections)
            {
                int sectionOffset = (int)stream.Position;
                writer.Write(Encoding.ASCII.GetBytes(section.Tag));
                writer.Write(0);
                section.Write(writer);
                int sectionLength = (int)stream.Position - sectionOffset - 8;

                stream.Position = sectionOffset + 4;
                writer.Write(sectionLength);
                stream.Position = stream.Length;
            }

            stream.Position = 4;
            writer.Write((int)stream.Length - 8);

            stream.Position = stream.Length;
        }

        private void GetEmbeddedSoundsFromSections()
        {
            DataIndexSection? indexSection = GetSection<DataIndexSection>();
            DataSection? dataSection = GetSection<DataSection>();
            if (indexSection == null || dataSection == null)
                return;

            for (int i = 0; i < indexSection.Entries.Count; i++)
            {
                EmbeddedSounds.Add(indexSection.Entries[i].SoundId, dataSection.SoundFiles[i]);
            }
        }

        private void ApplyEmbeddedSoundsToSections()
        {
            DataIndexSection? indexSection = GetSection<DataIndexSection>();
            DataSection? dataSection = GetSection<DataSection>();
            if (indexSection == null || dataSection == null)
                return;

            indexSection.Entries.Clear();
            dataSection.SoundFiles.Clear();
            int offset = 0;
            foreach ((int soundId, ArraySegment<byte> content) in EmbeddedSounds)
            {
                indexSection.Entries.Add(new DataIndexEntry { SoundId = soundId, Offset = offset, Length = content.Count });
                dataSection.SoundFiles.Add(content);
                offset += (content.Count + 0xF) & ~0xF;
            }
        }

        private T? GetSection<T>()
            where T : Section
        {
            return _sections.OfType<T>().FirstOrDefault();
        }

        private abstract class Section
        {
            public Section(string tag)
            {
                Tag = tag;
            }

            public string Tag
            {
                get;
            }

            public abstract void Write(BinaryWriter writer);

            public override string ToString() => Tag;
        }

        private class HeaderSection : Section
        {
            private readonly WwiseSoundBank _bank;

            public HeaderSection(WwiseSoundBank bank, string tag, BinaryReader reader, int length)
                : base(tag)
            {
                _bank = bank;

                if (length < 0x14 || (length % 4) != 0)
                    throw new InvalidDataException($"SoundBank: Invalid BHKD length {length}");

                Version = reader.ReadInt32();
                Id = reader.ReadInt32();
                Int1 = reader.ReadInt32();
                Int2 = reader.ReadInt32();
                Int3 = reader.ReadInt32();
                for (int offset = 0x14; offset < length; offset += 4)
                {
                    if (reader.ReadInt32() != 0)
                        throw new InvalidDataException("SoundBank: Unexpected nonzero padding in BKHD");
                }
            }

            public int Id
            {
                get;
                set;
            }

            public int Version
            {
                get;
                set;
            }

            public int Int1
            {
                get;
                set;
            }

            public int Int2
            {
                get;
                set;
            }

            public int Int3
            {
                get;
                set;
            }

            public override void Write(BinaryWriter writer)
            {
                writer.Write(Version);
                writer.Write(Id);
                writer.Write(Int1);
                writer.Write(Int2);
                writer.Write(Int3);

                DataIndexSection? indexSection = _bank.GetSection<DataIndexSection>();
                if (indexSection == null)
                    return;

                int indexSectionSize = 8 + 0xC * indexSection.Entries.Count;
                while (((writer.BaseStream.Position + indexSectionSize) & 0xF) != 0)
                {
                    writer.Write(0);
                }
            }
        }

        private class DataIndexSection : Section
        {
            public DataIndexSection(string tag, BinaryReader reader, int length)
                : base(tag)
            {
                int numEntries = length / 0xC;
                Entries = new List<DataIndexEntry>(numEntries);
                for (int i = 0; i < numEntries; i++)
                {
                    Entries.Add(reader.ReadStruct<DataIndexEntry>());
                }
            }

            public List<DataIndexEntry> Entries
            {
                get;
            }

            public override void Write(BinaryWriter writer)
            {
                foreach (DataIndexEntry entry in Entries)
                {
                    writer.WriteStruct(entry);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DataIndexEntry
        {
            public int SoundId;
            public int Offset;
            public int Length;
        }

        private class DataSection : Section
        {
            public DataSection(WwiseSoundBank bank, string tag, BinaryReader reader, int length)
                : base(tag)
            {
                byte[] data = reader.ReadBytes(length);
                DataIndexSection? index = bank.GetSection<DataIndexSection>();
                if (index == null)
                    return;

                foreach (DataIndexEntry entry in index.Entries)
                {
                    SoundFiles.Add(new ArraySegment<byte>(data, entry.Offset, entry.Length));
                }
            }

            public List<ArraySegment<byte>> SoundFiles
            {
                get;
            } = [];

            public override void Write(BinaryWriter writer)
            {
                long startPos = writer.BaseStream.Position;
                foreach (ArraySegment<byte> blob in SoundFiles)
                {
                    if (blob.Array == null)
                        throw new ArgumentNullException();

                    writer.Write(blob.Array, blob.Offset, blob.Count);
                    while (((writer.BaseStream.Position - startPos) & 0xF) != 0)
                    {
                        writer.Write((byte)0);
                    }
                }
            }
        }

        private class HircSection : Section
        {
            public HircSection(string tag, BinaryReader reader)
                : base(tag)
            {
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    byte entryType = reader.ReadByte();
                    int entryLength = reader.ReadInt32();
                    byte[] entryData = reader.ReadBytes(entryLength);
                    HircEntryFactory factory = HircEntryFactories.GetValueOrDefault(entryType) ?? DefaultHircEntryFactory;
                    Entries.Add(factory(entryType, entryData));
                }
            }

            public List<HircEntry> Entries
            {
                get;
            } = new();

            public override void Write(BinaryWriter writer)
            {
                writer.Write(Entries.Count);
                foreach (HircEntry entry in Entries)
                {
                    writer.Write(entry.Type);
                    writer.Write(entry.Data.Length);
                    writer.Write(entry.Data);
                }
            }
        }

        private class HircEntry
        {
            public HircEntry(byte type, byte[] data)
            {
                Type = type;
                Data = data;
            }

            public byte Type
            {
                get;
            }

            public byte[] Data
            {
                get;
            }
        }

        private class HircSoundEntry : HircEntry
        {
            public HircSoundEntry(byte type, byte[] data)
                : base(type, data)
            {
            }

            public int SoundId
            {
                get { return BitConverter.ToInt32(Data, 9); }
                set { Array.Copy(BitConverter.GetBytes(value), 0, Data, 9, 4); }
            }
        }

        private class UnknownSection : Section
        {
            public UnknownSection(string tag, BinaryReader reader, int length)
                : base(tag)
            {
                Data = reader.ReadBytes(length);
            }

            public byte[] Data
            {
                get;
            }

            public override void Write(BinaryWriter writer)
            {
                writer.Write(Data);
            }
        }
    }
}
