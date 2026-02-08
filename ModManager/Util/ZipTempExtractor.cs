using System;
using System.IO;
using System.Linq;
using System.Threading;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
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

        public void Extract(ITaskProgress progress, CancellationToken cancellationToken)
        {
            try
            {
                progress.Begin("Extracting archive...");

                using IArchive archive = ArchiveFactory.Open(_archiveFilePath);
                int numFiles = archive.Entries.Count(e => !e.IsDirectory);

                using IReader reader = archive.ExtractAllEntries();
                ExtractionOptions options = new ExtractionOptions { ExtractFullPath = true };
                int i = 0;
                while (reader.MoveToNextEntry())
                {
                    if (reader.Entry.IsDirectory)
                        continue;

                    cancellationToken.ThrowIfCancellationRequested();

                    reader.WriteEntryToDirectory(FolderPath, options);
                    i++;
                    progress.Report((float)i / numFiles);
                }
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
