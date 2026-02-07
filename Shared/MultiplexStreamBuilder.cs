using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrRebootTools.Shared.Cdc;

namespace TrRebootTools.Shared
{
    public class MultiplexStreamBuilder
    {
        private const float Mp3FrameDuration = 1152f / 44100f;      // (Samples per MP3 frame) divided by (samples per second)
        private const float AnimationFrameDuration = 1f / 30f;      // 30FPS animation

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
            List<(float Time, MultiplexStream.SoundPacket Packet)> soundPackets = MakeSoundPackets(infoFilePath, info);

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
                cinePackets = [];
            }

            float time = 0;
            int cinePacketIdx = 0;
            int soundPacketIdx = 0;
            while (cinePacketIdx < cinePackets.Count || soundPacketIdx < soundPackets.Count)
            {
                if (cinePacketIdx < cinePackets.Count)
                    mul.Packets.Add(cinePackets[cinePacketIdx++]);

                if (soundPacketIdx < soundPackets.Count && soundPackets[soundPacketIdx].Time <= time)
                    mul.Packets.Add(soundPackets[soundPacketIdx++].Packet);

                time += AnimationFrameDuration;
            }
        }

        private List<(float Time, MultiplexStream.SoundPacket Packet)> MakeSoundPackets(string infoFilePath, MultiplexStreamInfo info)
        {
            int numChannels = info.AudioChannels?.Length ?? 0;
            if (numChannels == 0)
                return [];

            byte[][] fsbContents = ReadAudioChannelFiles(infoFilePath, info);
            int longestChannel = Enumerable.Range(0, fsbContents.Length).OrderByDescending(c => fsbContents[c].Length).First();

            List<(float Time, MultiplexStream.SoundPacket Packet)> packets = [];
            int chunkStart = 0;
            int chunkEnd;
            int framePos = 0x80;
            float time = 0;
            while (chunkStart < fsbContents[longestChannel].Length)
            {
                float startTime = time;
                while (framePos < fsbContents[longestChannel].Length && framePos - chunkStart < 0x800)
                {
                    framePos = FindNextMp3Frame(fsbContents[longestChannel], framePos);
                    time += Mp3FrameDuration;
                }
                chunkEnd = Math.Max(framePos & ~0x7FF, chunkStart + 0x800);

                var packet = new MultiplexStream.SoundPacket { ChannelData = new byte[numChannels][] };
                for (int channel = 0; channel < numChannels; channel++)
                {
                    byte[] fsbContent = fsbContents[channel];
                    int channelChunkStart = Math.Min(chunkStart, fsbContent.Length);
                    int channelChunkEnd = Math.Min(chunkEnd, fsbContent.Length);
                    if (channelChunkEnd > channelChunkStart)
                    {
                        byte[] chunk = new byte[channelChunkEnd - channelChunkStart];
                        Array.Copy(fsbContent, channelChunkStart, chunk, 0, channelChunkEnd - channelChunkStart);
                        packet.ChannelData[channel] = chunk;
                    }
                    else
                    {
                        packet.ChannelData[channel] = [0xFF, 0xFB];
                    }
                }
                packets.Add((startTime, packet));

                chunkStart = chunkEnd;
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
                byte[]? header = null;

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
            List<MultiplexStream.CinePacket> packets = [];
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
