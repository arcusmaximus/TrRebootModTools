using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Cdc
{
    public class WwiseSound
    {
        private static class FourCC
        {
            public const int RIFF = 0x46464952;
            public const int WAVE = 0x45564157;
            public const int fmt = 0x20746D66;
            public const int cue = 0x20657563;
            public const int LIST = 0x5453494C;
            public const int adtl = 0x6C746461;
            public const int labl = 0x6C62616C;
        }

        private static readonly Dictionary<int, Func<Chunk>> ChunkFactories =
            new Dictionary<int, Func<Chunk>>
            {
                { FourCC.RIFF, () => new RootChunk() },
                { FourCC.fmt, () => new FormatChunk() },
                { FourCC.cue, () => new CueChunk() },
                { FourCC.LIST, () => new ListChunk() },
                { FourCC.labl, () => new LabelChunk() }
            };

        public WwiseSound(Stream stream, string fileNameForErrors)
        {
            BinaryReader reader = new BinaryReader(stream);
            Chunk chunk = ReadChunk(reader);
            if (chunk is not RootChunk rootChunk ||
                rootChunk.Id != FourCC.RIFF ||
                rootChunk.Type != FourCC.WAVE)
            {
                throw new InvalidDataException(fileNameForErrors + " is not a valid .wem file");
            }

            Chunks = rootChunk.Chunks;
        }

        public List<Chunk> Chunks
        {
            get;
        } = [];

        public FormatChunk Format => Chunks.OfType<FormatChunk>().First();

        public void Write(Stream stream)
        {
            RootChunk rootChunk = new RootChunk();
            rootChunk.Type = FourCC.WAVE;
            rootChunk.Chunks.AddRange(Chunks);

            BinaryWriter writer = new BinaryWriter(stream);
            WriteChunk(writer, rootChunk);
        }

        public abstract class Chunk
        {
            public abstract int Id
            {
                get;
            }

            public abstract void Read(BinaryReader reader, int size);
            public abstract void Write(BinaryWriter writer);
        }

        public class GenericChunk : Chunk
        {
            public GenericChunk(int id)
            {
                Id = id;
            }

            public override int Id
            {
                get;
            }

            public byte[] Data
            {
                get;
                private set;
            } = [];

            public override void Read(BinaryReader reader, int size)
            {
                Data = reader.ReadBytes(size);
            }

            public override void Write(BinaryWriter writer)
            {
                writer.Write(Data);
            }
        }

        public class FormatChunk : GenericChunk
        {
            public FormatChunk()
                : base(FourCC.fmt)
            {
            }

            public int SampleFrequency
            {
                get => BitConverter.ToInt32(Data, 4);
            }
        }

        public class CueChunk : Chunk
        {
            public override int Id => FourCC.cue;

            public List<CuePoint> Points
            {
                get;
            } = [];

            public override void Read(BinaryReader reader, int size)
            {
                int numPoints = reader.ReadInt32();
                if (size != 4 + Marshal.SizeOf<CuePoint>() * numPoints)
                    throw new InvalidDataException();

                for (int i = 0; i < numPoints; i++)
                {
                    Points.Add(reader.ReadStruct<CuePoint>());
                }
            }

            public override void Write(BinaryWriter writer)
            {
                writer.Write(Points.Count);
                foreach (CuePoint point in Points)
                {
                    writer.WriteStruct(point);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CuePoint
        {
            public int Identifier;
            public int Position;
            public int ChunkId;
            public int ChunkStart;
            public int BlockStart;
            public int SampleOffset;
        }

        public abstract class CollectionChunk : Chunk
        {
            public int Type
            {
                get;
                set;
            }

            public List<Chunk> Chunks
            {
                get;
            } = [];

            public override void Read(BinaryReader reader, int size)
            {
                Stream stream = reader.BaseStream;
                long listEndPos = stream.Position + size;

                Type = reader.ReadInt32();

                while (stream.Position < listEndPos)
                {
                    Chunks.Add(ReadChunk(reader));
                }
            }

            public override void Write(BinaryWriter writer)
            {
                writer.Write(Type);
                foreach (Chunk chunk in Chunks)
                {
                    WriteChunk(writer, chunk);
                }
            }
        }

        public class RootChunk : CollectionChunk
        {
            public override int Id => FourCC.RIFF;
        }

        public class ListChunk : CollectionChunk
        {
            public override int Id => FourCC.LIST;
        }

        public class LabelChunk : Chunk
        {
            public override int Id => FourCC.labl;

            public int CuePointIndex
            {
                get;
                set;
            }

            public string Label
            {
                get;
                set;
            } = "";

            public override void Read(BinaryReader reader, int size)
            {
                Stream stream = reader.BaseStream;
                long chunkEndPos = stream.Position + size;
                CuePointIndex = reader.ReadInt32();
                Label = reader.ReadZeroTerminatedString();
                if (stream.Position > chunkEndPos)
                    throw new InvalidDataException();

                while (stream.Position < chunkEndPos)
                {
                    if (reader.ReadByte() != 0)
                        throw new InvalidDataException();
                }
            }

            public override void Write(BinaryWriter writer)
            {
                Stream stream = writer.BaseStream;
                long chunkStartPos = stream.Position;
                writer.Write(CuePointIndex);
                writer.WriteZeroTerminatedString(Label);
                while ((stream.Position - chunkStartPos) % 4 != 0)
                {
                    writer.Write((byte)0);
                }
            }
        }

        private static Chunk ReadChunk(BinaryReader reader)
        {
            Stream stream = reader.BaseStream;

            int chunkType = reader.ReadInt32();
            int chunkLength = reader.ReadInt32();
            long chunkStartPos = stream.Position;
            Chunk chunk = ChunkFactories.GetValueOrDefault(chunkType)?.Invoke() ?? new GenericChunk(chunkType);
            chunk.Read(reader, chunkLength);
            if (stream.Position != chunkStartPos + chunkLength)
                throw new InvalidDataException();

            if ((chunkLength & 1) != 0 && stream.Position < stream.Length)
                stream.Position += 1;

            return chunk;
        }

        private static void WriteChunk(BinaryWriter writer, Chunk chunk)
        {
            Stream stream = writer.BaseStream;

            writer.Write(chunk.Id);

            long chunkLengthPos = stream.Position;
            writer.Write(0);

            long chunkStartPos = stream.Position;
            chunk.Write(writer);

            int chunkLength = (int)(stream.Position - chunkStartPos);
            stream.Position = chunkLengthPos;
            writer.Write(chunkLength);
            stream.Position = stream.Length;

            if ((chunkLength & 1) != 0)
                writer.Write((byte)0);
        }
    }
}
