using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TrRebootTools.Shared.Cdc;

namespace TrRebootTools.Shared
{
    public class MultiplexStreamInfo
    {
        public MultiplexStream.AudioChannel[]? AudioChannels
        {
            get;
            set;
        }

        public bool Looping
        {
            get;
            set;
        }

        public SubtitleFrame[]? SubtitleFrames
        {
            get;
            set;
        }

        public class SubtitleFrame
        {
            public int FrameNumber
            {
                get;
                set;
            }

            public Dictionary<string, string> Subtitles
            {
                get;
                set;
            } = [];
        }

        public static MultiplexStreamInfo Load(string filePath)
        {
            using Stream stream = File.OpenRead(filePath);
            return JsonSerializer.Deserialize<MultiplexStreamInfo>(stream)!;
        }

        public void Save(string filePath)
        {
            using Stream stream = File.Create(filePath);
            JsonSerializer.Serialize(stream, this);
        }
    }
}
