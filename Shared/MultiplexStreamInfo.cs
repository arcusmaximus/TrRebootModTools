using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using TrRebootTools.Shared.Cdc;

namespace TrRebootTools.Shared
{
    public class MultiplexStreamInfo
    {
        public MultiplexStream.AudioChannel[] AudioChannels
        {
            get;
            set;
        }

        public bool Looping
        {
            get;
            set;
        }

        public SubtitleFrame[] SubtitleFrames
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
            }
        }

        public static MultiplexStreamInfo Load(string filePath)
        {
            using StreamReader streamReader = new(filePath);
            JsonTextReader jsonReader = new(streamReader);
            return new JsonSerializer().Deserialize<MultiplexStreamInfo>(jsonReader);
        }

        public void Save(string filePath)
        {
            using Stream stream = File.Create(filePath);
            using StreamWriter streamWriter = new(stream);
            using JsonTextWriter jsonWriter = new(streamWriter) { Formatting = Formatting.Indented };
            new JsonSerializer().Serialize(jsonWriter, this);
        }
    }
}
