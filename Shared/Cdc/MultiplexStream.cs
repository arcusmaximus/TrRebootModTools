using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Cdc
{
    public class MultiplexStream
    {
        public enum AudioChannelType
        {
            Mono,
            Left,
            Right
        }

        public record AudioChannel(AudioChannelType Type, float LeftVolume, float RightVolume);

        public MultiplexStream()
        {
        }

        public MultiplexStream(Stream stream)
        {
            Read(stream);
        }

        public AudioChannel[]? AudioChannels
        {
            get;
            set;
        }

        public bool HasAnimation
        {
            get;
            set;
        }

        public bool HasSubtitles
        {
            get;
            set;
        }

        public bool Looping
        {
            get;
            set;
        }

        public List<Packet> Packets
        {
            get;
        } = [];

        private unsafe void Read(Stream stream)
        {
            BinaryReader reader = new(stream);
            StreamHeader header = reader.ReadStruct<StreamHeader>();

            HasAnimation = header.HasAnimation != 0;
            HasSubtitles = header.HasSubtitles != 0;
            if (HasAnimation && HasSubtitles)
                throw new NotSupportedException(".mul files containing both animation and subtitles are not supported");

            Looping = header.LoopStartFileOffset != 0;

            if (header.AudioChannelCount < 0 || header.AudioChannelCount > 12)
                throw new InvalidDataException();

            AudioChannels = new AudioChannel[header.AudioChannelCount];
            for (int i = 0; i < header.AudioChannelCount; i++)
            {
                AudioChannels[i] = new AudioChannel(
                    (AudioChannelType)header.AudioChannelTypes[i],
                    header.AudioChannelLeftVolumes[i],
                    header.AudioChannelRightVolumes[i]
                );
            }

            reader.Seek(0x2000);
            while (stream.Position < stream.Length)
            {
                Packet? packet = ReadPacket(reader);
                if (packet != null)
                    Packets.Add(packet);

                reader.Align(0x10);
            }
        }

        private Packet? ReadPacket(BinaryReader reader)
        {
            PacketType type = (PacketType)reader.ReadInt32();
            int size = reader.ReadInt32();
            reader.Skip(8);

            Packet? packet = type switch
            {
                PacketType.Cinematic => new CinePacket(),
                PacketType.Sound => new SoundPacket(),
                _ => null
            };
            if (packet != null)
                packet.Read(reader, this);
            else
                reader.Skip(size);

            return packet;
        }

        public unsafe void Write(Stream stream)
        {
            StreamHeader header = new()
            {
                Hertz = 44100,
                StartLoop = Looping ? 0 : -1,
                EndLoop = GetNumAudioSamples(),
                LoopStartFileOffset = Looping ? 0x2000 : 0,
                HasAnimation = Convert.ToInt32(HasAnimation),
                HasSubtitles = Convert.ToInt32(HasSubtitles),
                MediaLength = Packets.OfType<SoundPacket>().Count()
            };

            if (AudioChannels != null)
            {
                if (AudioChannels.Length > 12)
                    throw new InvalidDataException();

                header.AudioChannelCount = AudioChannels.Length;
                for (int i = 0; i < AudioChannels.Length; i++)
                {
                    header.AudioChannelTypes[i] = (byte)AudioChannels[i].Type;
                    header.AudioChannelLeftVolumes[i] = AudioChannels[i].LeftVolume;
                    header.AudioChannelRightVolumes[i] = AudioChannels[i].RightVolume;
                    header.SpliceMarkersSampleOffset[1023 - i] = Packets.OfType<SoundPacket>().Sum(p => p.ChannelData[i].Length);
                }
            }

            BinaryWriter writer = new(stream);
            writer.WriteStruct(header);
            writer.Align(0x1000);

            foreach (Packet packet in Packets)
            {
                WritePacket(writer, packet);
                writer.Align(0x10);
            }
        }

        private int GetNumAudioSamples()
        {
            SoundPacket? soundPacket = Packets.OfType<SoundPacket>().FirstOrDefault();
            if (soundPacket == null || soundPacket.ChannelData.Length == 0)
                return int.MaxValue;

            return soundPacket.ChannelData.Max(c => BitConverter.ToInt32(c, 0x50));
        }

        private void WritePacket(BinaryWriter writer, Packet packet)
        {
            PacketType type = packet switch
            {
                CinePacket _ => PacketType.Cinematic,
                SoundPacket _ => PacketType.Sound
            };
            writer.Write((int)type);

            int packetSizePos = writer.Tell();
            writer.Write(0);

            writer.Write(0L);

            long packetContentPos = writer.Tell();
            packet.Write(writer, this);

            writer.Seek(packetSizePos);
            writer.Write(writer.GetLength() - packetContentPos);

            writer.SeekToEnd();
        }

        private enum PacketType
        {
            Sound,
            Cinematic,
            FaceFx,
            Padding
        }

        public abstract class Packet
        {
            public abstract void Read(BinaryReader reader, MultiplexStream parent);
            public abstract void Write(BinaryWriter writer, MultiplexStream parent);
        }

        public class CinePacket : Packet
        {
            public const int HeaderMagic = 0x43494E45;

            public byte[]? Header
            {
                get;
                set;
            }

            public int FrameNumber
            {
                get;
                set;
            }

            public byte[]? ChannelValues
            {
                get;
                set;
            }

            public Dictionary<string, string>? Subtitles
            {
                get;
                set;
            }

            public static unsafe byte[] MakeEmptyHeader(int version)
            {
                int size = Marshal.SizeOf<EmptyCineHeader>();
                EmptyCineHeader header =
                    new()
                    {
                        Magic = HeaderMagic,
                        Version = version,
                        HeaderSize = size - 8
                    };

                byte[] data = new byte[size];
                fixed (byte* pData = data)
                {
                    Unsafe.Write(pData, header);
                }
                return data;
            }

            public override void Read(BinaryReader reader, MultiplexStream parent)
            {
                int firstInt = reader.ReadInt32();
                int frameSize;
                if (firstInt == HeaderMagic)
                {
                    int version = reader.ReadInt32();
                    int headerSize = reader.ReadInt32();
                    reader.BaseStream.Position -= 0xC;
                    Header = reader.ReadBytes(headerSize + 8);
                    frameSize = reader.ReadInt32();
                }
                else
                {
                    frameSize = firstInt;
                }

                FrameNumber = reader.ReadInt32();
                if (FrameNumber < 0)
                    return;

                if (parent.HasAnimation)
                    ChannelValues = reader.ReadBytes(frameSize - 4);
                else if (parent.HasSubtitles)
                    ReadSubtitles(reader);
            }

            public override void Write(BinaryWriter writer, MultiplexStream parent)
            {
                if (Header != null)
                    writer.Write(Header);

                int frameSizePos = writer.Tell();
                writer.Write(0);
                writer.Write(FrameNumber);
                if (parent.HasAnimation)
                {
                    if (ChannelValues != null)
                        writer.Write(ChannelValues);
                }
                else
                {
                    WriteSubtitles(writer);
                }

                writer.Seek(frameSizePos);
                writer.Write(writer.GetLength() - frameSizePos - 4);
                writer.SeekToEnd();
                writer.Align(0x10);
            }

            private void ReadSubtitles(BinaryReader reader)
            {
                int subtitleSize = reader.ReadInt32();
                if (subtitleSize == 0)
                    return;

                int subtitleEndPos = reader.Tell() + subtitleSize;
                Subtitles = new();
                byte[] buffer = new byte[0x1000];
                while (reader.Tell() < subtitleEndPos)
                {
                    string language = ReadLine();
                    string text = ReadLine();
                    Subtitles[language] = text;
                }
                return;

                string ReadLine()
                {
                    int length = 0;
                    while (true)
                    {
                        byte b = reader.ReadByte();
                        if (b == 0xD)
                            break;

                        buffer[length++] = b;
                    }
                    return Encoding.UTF8.GetString(buffer, 0, length);
                }
            }

            private void WriteSubtitles(BinaryWriter writer)
            {
                int subtitleSizePos = writer.Tell();
                writer.Write(0);
                if (Subtitles == null)
                    return;

                byte[] buffer = new byte[0x1000];
                Encoding encoding = new UTF8Encoding(false);
                foreach ((string language, string text) in Subtitles)
                {
                    WriteLine(language);
                    WriteLine(text);
                }

                writer.Seek(subtitleSizePos);
                writer.Write(writer.GetLength() - subtitleSizePos - 4);
                writer.SeekToEnd();
                return;

                void WriteLine(string str)
                {
                    int length = encoding.GetBytes(str, 0, str.Length, buffer, 0);
                    buffer[length++] = 0xD;
                    writer.Write(buffer, 0, length);
                }
            }
        }

        public class SoundPacket : Packet
        {
            public byte[][] ChannelData
            {
                get;
                set;
            } = [];

            public override void Read(BinaryReader reader, MultiplexStream parent)
            {
                ChannelData = new byte[parent.AudioChannels?.Length ?? 0][];

                for (int i = 0; i < ChannelData.Length; i++)
                {
                    int channelDataSize = reader.ReadInt32();
                    int channel = reader.ReadInt32();
                    if (channel != i)
                        throw new InvalidDataException("Unexpected channel in .mul audio data");

                    reader.Skip(8);
                    ChannelData[channel] = reader.ReadBytes(channelDataSize);
                }
            }

            public override void Write(BinaryWriter writer, MultiplexStream parent)
            {
                for (int i = 0; i < ChannelData.Length; i++)
                {
                    writer.Write(ChannelData[i].Length);
                    writer.Write(i);
                    writer.Write(0L);
                    writer.Write(ChannelData[i]);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct StreamHeader
        {
            public int Hertz;
            public int StartLoop;
            public int EndLoop;
            public int AudioChannelCount;
            public int ReverbVol;
            public int StartSizeToLoad;
            public int PartialLoop;
            public int LoopAreaSize;
            public int HasAnimation;
            public int HasSubtitles;
            public int FaceFxSize;
            public int LoopStartFileOffset;
            public int LoopStartBundleOffset;
            public int MaxEEBytesPerRead;
            public float MediaLength;
            public fixed float AudioChannelLeftVolumes[12];
            public fixed float AudioChannelRightVolumes[12];
            public fixed int LoopStartSamplesToSkip[12];
            public int SpliceMarkersCount;
            public fixed int SpliceMarkersSampleOffset[1024];
            public fixed short SpliceMarkersIdentifier[1024];
            public fixed byte SpliceMarkersChars[1024];
            public fixed byte AudioChannelTypes[12];
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct EmptyCineHeader
        {
            public int Magic;
            public int Version;
            public int HeaderSize;
            public int NumFrames;
            public fixed byte Name[0x40];
            public int MainUnitId;
            public int NumAnchors;
            public int NumSkeletons;
            public int NumCameras;
            public int TriggerUnitId;
            public int NumTriggers;
            public int NumSubtitles;
        }
    }
}
