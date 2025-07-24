using System.IO;
using System.Threading.Tasks;
using TrRebootTools.Shared.Cdc;

namespace TrRebootTools.SoundConverter
{
    internal class TrSoundEncoder : FmodEncoder
    {
        public TrSoundEncoder(CdcGame game)
            : base(game)
        {
        }

        public override string InputExtension => ".wav";

        protected override async Task<string> ConvertInternalAsync(string wavFilePath)
        {
            string fsbFilePath = await ConvertWavAsync(wavFilePath);
            if (fsbFilePath == null || !File.Exists(fsbFilePath))
                return null;

            string trSoundFilePath = Path.ChangeExtension(fsbFilePath, $".tr{(int)Game}sound");

            using (Stream fsbStream = File.OpenRead(fsbFilePath))
            using (Stream trSoundStream = File.Create(trSoundFilePath))
            {
                BinaryWriter writer = new(trSoundStream);

                writer.Write(44100);        // Sample rate
                writer.Write(0);            // Loop start
                writer.Write(0);            // Loop end
                writer.Write(100);          // Priority
                if (Game == CdcGame.Rise)
                    writer.Write(1.0f);     // Highest peak

                fsbStream.CopyTo(trSoundStream);
            }

            File.Delete(fsbFilePath);
            return trSoundFilePath;
        }
    }
}
