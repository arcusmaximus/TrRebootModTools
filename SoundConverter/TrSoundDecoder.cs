using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using TrRebootTools.Shared.Cdc;

namespace TrRebootTools.SoundConverter
{
    internal class TrSoundDecoder : ISoundConverter
    {
        public TrSoundDecoder(CdcGame game)
        {
            Game = game;
        }

        public virtual CdcGame Game { get; }
        public virtual string InputExtension => $".tr{(int)Game}sound";

        public async Task<bool> ConvertAsync(string inputFilePath, string outputFolderPath)
        {
            string fsbFilePath = Path.Combine(outputFolderPath, Path.GetFileNameWithoutExtension(inputFilePath) + ".fsb");
            string wavFilePath = Path.ChangeExtension(fsbFilePath, ".wav");

            using (Stream trsoundStream = File.OpenRead(inputFilePath))
            using (Stream fsbStream = File.Create(fsbFilePath))
            {
                trsoundStream.Position = Game == CdcGame.Tr2013 ? 0x10 : 0x14;
                trsoundStream.CopyTo(fsbStream);
            }

            string vgmstreamPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), @"vgmstream\vgmstream-cli.exe");
            if (!File.Exists(vgmstreamPath))
                return false;

            File.Delete(wavFilePath);
            await ProcessHelper.RunAsync(vgmstreamPath, $"-o \"{wavFilePath}\" \"{fsbFilePath}\"");
            File.Delete(fsbFilePath);
            return File.Exists(wavFilePath);
        }

        public void Dispose()
        {
        }
    }
}
