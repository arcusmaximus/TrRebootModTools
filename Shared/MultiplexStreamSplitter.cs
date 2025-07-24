using System.IO;
using System.Linq;
using TrRebootTools.Shared.Cdc;

namespace TrRebootTools.Shared
{
    public class MultiplexStreamSplitter
    {
        private readonly CdcGame _game;

        public MultiplexStreamSplitter(CdcGame game)
        {
            _game = game;
        }

        public void Split(string mulFilePath)
        {
            MultiplexStream mul;
            using (Stream mulStream = File.OpenRead(mulFilePath))
            {
                mul = new(mulStream);
            }

            if ((mul.AudioChannels?.Length ?? 0) == 0)
                return;

            ExtractInfo(mulFilePath, mul);
            ExtractAnimation(mulFilePath, mul);
            ExtractAudio(mulFilePath, mul);
        }

        private void ExtractInfo(string mulFilePath, MultiplexStream mul)
        {
            if ((mul.AudioChannels?.Length ?? 0) == 0 && !mul.HasSubtitles)
                return;

            MultiplexStreamInfo info = new()
                                       {
                                           AudioChannels = mul.AudioChannels
                                       };
            if (mul.HasSubtitles)
            {
                info.SubtitleFrames = mul.Packets
                                         .OfType<MultiplexStream.CinePacket>()
                                         .Where(p => (p.Subtitles?.Count ?? 0) > 0)
                                         .Select(p => new MultiplexStreamInfo.SubtitleFrame
                                                      {
                                                          FrameNumber = p.FrameNumber,
                                                          Subtitles = p.Subtitles
                                                      })
                                         .ToArray();
            }

            info.Save(Path.ChangeExtension(mulFilePath, ".json"));
        }

        private void ExtractAnimation(string mulFilePath, MultiplexStream mul)
        {
            if (!mul.HasAnimation)
                return;

            using Stream stream = File.Create(Path.ChangeExtension(mulFilePath, $".tr{(int)_game}cineanim"));
            BinaryWriter writer = new(stream);
            foreach (var packet in mul.Packets.OfType<MultiplexStream.CinePacket>())
            {
                if (packet.Header != null)
                    writer.Write(packet.Header);

                if ((packet.ChannelValues?.Length ?? 0) > 0)
                {
                    writer.Write(4 + packet.ChannelValues.Length);
                    writer.Write(packet.FrameNumber);
                    writer.Write(packet.ChannelValues);
                }
            }
        }

        private void ExtractAudio(string mulFilePath, MultiplexStream mul)
        {
            int numChannels = mul.AudioChannels?.Length ?? 0;
            if (numChannels == 0)
                return;

            string[] fmodFilePaths = new string[numChannels];
            Stream[] fmodStreams = new Stream[numChannels];
            for (int i = 0; i < numChannels; i++)
            {
                fmodFilePaths[i] = Path.ChangeExtension(mulFilePath, $".channel{i}.fsb");
                fmodStreams[i] = File.Create(fmodFilePaths[i]);
            }

            try
            {
                foreach (var soundPacket in mul.Packets.OfType<MultiplexStream.SoundPacket>())
                {
                    for (int i = 0; i < numChannels; i++)
                    {
                        byte[] data = soundPacket.ChannelData[i];
                        fmodStreams[i].Write(data, 0, data.Length);
                    }
                }
            }
            finally
            {
                for (int i = 0; i < numChannels; i++)
                {
                    fmodStreams[i].Close();
                }
            }
        }
    }
}
