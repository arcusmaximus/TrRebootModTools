using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using TrRebootTools.Shared;

namespace TrRebootTools.ModManager.Util
{
    internal class ZipTempExtractor : IDisposable
    {
        private readonly string _archiveFilePath;

        public ZipTempExtractor(string archiveFilePath)
        {
            _archiveFilePath = archiveFilePath;

            FolderPath = Path.GetTempFileName();
            File.Delete(FolderPath);
            Directory.CreateDirectory(FolderPath);
        }

        public string FolderPath
        {
            get;
        }

        public async Task ExtractAsync(ITaskProgress progress, CancellationToken cancellationToken)
        {
            try
            {
                progress.Begin("Extracting archive...");

                using IArchive archive = ArchiveFactory.Open(_archiveFilePath);
                await archive.WriteToDirectoryAsync(
                    FolderPath,
                    new() { ExtractFullPath = true },
                    new Progress<ProgressReport>(p => progress.Report((float)(p.PercentComplete ?? 0))),
                    cancellationToken
                );
            }
            finally
            {
                progress.End();
            }
        }

        public void Dispose()
        {
            Directory.Delete(FolderPath, true);
        }
    }
}
