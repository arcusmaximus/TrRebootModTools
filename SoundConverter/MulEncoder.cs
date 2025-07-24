using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TrRebootTools.Shared;
using TrRebootTools.Shared.Cdc;

namespace TrRebootTools.SoundConverter
{
    internal class MulEncoder : FmodEncoder
    {
        public MulEncoder(CdcGame game)
            : base(game)
        {
        }

        public override string InputExtension => ".json";

        protected override async Task<string> ConvertInternalAsync(string jsonFilePath)
        {
            MultiplexStreamInfo mulInfo = MultiplexStreamInfo.Load(jsonFilePath);

            List<string> fsbFilePaths = new();
            try
            {
                for (int i = 0; i < mulInfo.AudioChannels.Length; i++)
                {
                    string wavFilePath = Path.ChangeExtension(jsonFilePath, $".channel{i}.wav");
                    string fsbFilePath = await ConvertWavAsync(wavFilePath);
                    if (fsbFilePath == null)
                        return null;

                    fsbFilePaths.Add(fsbFilePath);
                }

                string mulFilePath = Path.Combine(ProjectFolderPath, "result.mul");
                new MultiplexStreamBuilder(Game).Build(jsonFilePath, mulFilePath);
                return mulFilePath;
            }
            finally
            {
                foreach (string fsbFilePath in fsbFilePaths)
                {
                    File.Delete(fsbFilePath);
                }
            }
        }
    }
}
