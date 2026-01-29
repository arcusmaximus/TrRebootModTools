using System.IO;
using System.Threading.Tasks;
using TrRebootTools.Shared;
using TrRebootTools.Shared.Cdc;

namespace TrRebootTools.SoundConverter
{
    internal class WwiseDecoder : ISoundConverter
    {
        public CdcGame Game => CdcGame.Shadow;

        public string InputExtension => ".wem";

        public async Task<bool> ConvertAsync(string inputFilePath, string outputFolderPath)
        {
            string outputFilePath = Path.Combine(outputFolderPath, Path.GetFileNameWithoutExtension(inputFilePath) + ".wav");
            File.Delete(outputFilePath);
            await ProcessHelper.RunVgmStreamAsync(inputFilePath, outputFilePath, true);
            return true;
        }

        public void Dispose()
        {
        }
    }
}
