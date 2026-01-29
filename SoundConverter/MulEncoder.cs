using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
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

        protected override async Task<string?> ConvertInternalAsync(string jsonFilePath)
        {
            MultiplexStreamInfo mulInfo = MultiplexStreamInfo.Load(jsonFilePath);

            List<string> fsbFilePaths = new();
            try
            {
                Dictionary<ulong, string> fsbsByWavHash = new();
                for (int i = 0; i < (mulInfo.AudioChannels?.Length ?? 0); i++)
                {
                    string wavFilePath = Path.ChangeExtension(jsonFilePath, $".channel{i}.wav");
                    ulong wavHash = CalculateFileHash(wavFilePath);
                    string? existingFsbFilePath = fsbsByWavHash.GetValueOrDefault(wavHash);
                    string? fsbFilePath;
                    if (existingFsbFilePath != null)
                    {
                        fsbFilePath = Path.ChangeExtension(wavFilePath, ".fsb");
                        File.Copy(existingFsbFilePath, fsbFilePath, true);
                    }
                    else
                    {
                        fsbFilePath = await ConvertWavAsync(wavFilePath);
                        if (fsbFilePath == null)
                            return null;

                        fsbsByWavHash.Add(wavHash, fsbFilePath);
                    }

                    fsbFilePaths.Add(fsbFilePath);
                }

                string mulFilePath = Path.Combine(ProjectFolderPath!, "result.mul");
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

        private static ulong CalculateFileHash(string filePath)
        {
            using Stream stream = File.OpenRead(filePath);
            MD5 md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(stream);
            return BitConverter.ToUInt64(hash, 0);
        }
    }
}
