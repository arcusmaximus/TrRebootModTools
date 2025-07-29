using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrRebootTools.Shared.Cdc;

namespace TrRebootTools.Shared
{
    public class MultiplexStreamBuilder
    {
        private readonly CdcGame _game;

        public MultiplexStreamBuilder(CdcGame game)
        {
            _game = game;
        }

        public void Build(string infoFilePath, string mulFilePath)
        {
            MultiplexStreamInfo info = MultiplexStreamInfo.Load(infoFilePath);
            MultiplexStream mul =
                new()
                {
                    AudioChannels = info.AudioChannels,
                    Looping = info.Looping
                };

            AddPackets(mul, infoFilePath, info);

            using Stream mulStream = File.Create(mulFilePath);
            mul.Write(mulStream);
        }

        private void AddPackets(MultiplexStream mul, string infoFilePath, MultiplexStreamInfo info)
        {
            List<MultiplexStream.SoundPacket> soundPackets = MakeSoundPackets(infoFilePath, info);

            List<MultiplexStream.CinePacket> cinePackets;
            string animationFilePath = Path.ChangeExtension(infoFilePath, $".tr{(int)_game}cineanim");
            if (File.Exists(animationFilePath))
            {
                cinePackets = MakeAnimationPackets(animationFilePath);
                mul.HasAnimation = true;
            }
            else if ((info.SubtitleFrames?.Length ?? 0) > 0)
            {
                cinePackets = MakeSubtitlePackets(info, soundPackets.Count);
                mul.HasSubtitles = true;
            }
            else
            {
                cinePackets = new();
            }

            for (int i = 0; i < Math.Max(soundPackets.Count, cinePackets.Count); i++)
            {
                if (i < soundPackets.Count)
                    mul.Packets.Add(soundPackets[i]);

                if (i < cinePackets.Count)
                    mul.Packets.Add(cinePackets[i]);
            }
        }

        private List<MultiplexStream.SoundPacket> MakeSoundPackets(string infoFilePath, MultiplexStreamInfo info)
        {
            int numChannels = info.AudioChannels?.Length ?? 0;
            if (numChannels == 0)
                return new();

            byte[][] fsbContents = ReadAudioChannelFiles(infoFilePath, info);
            int[] fsbPositions = new int[numChannels];

            List<MultiplexStream.SoundPacket> packets = new();
            while (true)
            {
                var packet = new MultiplexStream.SoundPacket { ChannelData = new byte[numChannels][] };

                bool packetHasData = false;
                for (int channel = 0; channel < numChannels; channel++)
                {
                    byte[] fsbContent = fsbContents[channel];
                    int startPos = fsbPositions[channel];
                    int endPos = FindNextMp3Frame(fsbContent, startPos == 0 ? 0x80 : startPos);

                    byte[] frame = new byte[endPos - startPos];
                    Array.Copy(fsbContent, startPos, frame, 0, frame.Length);
                    packet.ChannelData[channel] = frame;

                    fsbPositions[channel] = endPos;
                    if (frame.Length > 0)
                        packetHasData = true;
                }

                if (packetHasData)
                    packets.Add(packet);
                else
                    break;
            }

            return packets;
        }

        private static byte[][] ReadAudioChannelFiles(string infoFilePath, MultiplexStreamInfo info)
        {
            int numChannels = info.AudioChannels?.Length ?? 0;
            byte[][] fsbContents = new byte[numChannels][];

            for (int channel = 0; channel < numChannels; channel++)
            {
                string fsbFilePath = Path.ChangeExtension(infoFilePath, $".channel{channel}.fsb");
                if (!File.Exists(fsbFilePath))
                    throw new FileNotFoundException($"File not found: {fsbFilePath}");

                fsbContents[channel] = File.ReadAllBytes(fsbFilePath);
                if (fsbContents[channel].Length < 0x100)
                    throw new InvalidDataException($"File {fsbFilePath} is invalid (too small)");
            }

            return fsbContents;
        }

        private static int FindNextMp3Frame(byte[] data, int pos)
        {
            pos += 2;
            while (true)
            {
                if (pos > data.Length - 2)
                    return data.Length;

                if (data[pos] == 0xFF && data[pos + 1] == 0xFB)
                    break;

                pos++;
            }
            return pos;
        }

        private List<MultiplexStream.CinePacket> MakeAnimationPackets(string animFilePath)
        {
            using Stream stream = File.OpenRead(animFilePath);
            BinaryReader reader = new(stream);

            List<MultiplexStream.CinePacket> packets = new();
            while (stream.Position < stream.Length)
            {
                byte[] header = null;

                int firstInt = reader.ReadInt32();
                int frameSize;
                if (firstInt == MultiplexStream.CinePacket.HeaderMagic)
                {
                    int version = reader.ReadInt32();
                    int headerSize = reader.ReadInt32();
                    stream.Position -= 0xC;
                    header = reader.ReadBytes(headerSize + 8);
                    frameSize = reader.ReadInt32();
                }
                else
                {
                    frameSize = firstInt;
                }

                int frameNr = reader.ReadInt32();
                byte[] channelValues = reader.ReadBytes(frameSize - 4);
                packets.Add(
                    new()
                    {
                        Header = header,
                        FrameNumber = frameNr,
                        ChannelValues = channelValues
                    }
                );
            }

            return packets;
        }

        private List<MultiplexStream.CinePacket> MakeSubtitlePackets(MultiplexStreamInfo info, int numFrames)
        {
            List<MultiplexStream.CinePacket> packets = new();
            List<MultiplexStreamInfo.SubtitleFrame> subtitleFrames = info.SubtitleFrames.OrderBy(f => f.FrameNumber).ToList();
            numFrames = Math.Max(numFrames, subtitleFrames.Last().FrameNumber + 1);

            int subtitleFrameIdx = 0;
            for (int i = 0; i < numFrames; i++)
            {
                MultiplexStream.CinePacket packet = new() { FrameNumber = i };
                if (i == 0)
                    packet.Header = MultiplexStream.CinePacket.MakeEmptyHeader(_game == CdcGame.Tr2013 ? 13 : 14);
                
                if (subtitleFrameIdx < info.SubtitleFrames.Length && info.SubtitleFrames[subtitleFrameIdx].FrameNumber == i)
                    packet.Subtitles = info.SubtitleFrames[subtitleFrameIdx++].Subtitles;

                packets.Add(packet);
            }

            return packets;
        }
    }
}
