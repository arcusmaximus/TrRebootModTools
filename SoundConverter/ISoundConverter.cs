using System;
using System.Threading.Tasks;
using TrRebootTools.Shared.Cdc;

namespace TrRebootTools.SoundConverter
{
    internal interface ISoundConverter : IDisposable
    {
        CdcGame Game { get; }
        string InputExtension { get; }

        Task<bool> ConvertAsync(string inputFilePath, string outputFolderPath);
    }
}
