using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TrRebootTools.Shared;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Controls;

namespace TrRebootTools.HookTool.Logging
{
    internal class FileAccessLogControl : AccessLogControl
    {
        private class FileLogEntry : IListViewEntry
        {
            public FileLogEntry(DateTime timestamp, ArchiveFileKey key, string path, string? soundBank)
            {
                Timestamp = timestamp;
                Key = key;
                Path = path;
                SoundBank = soundBank;
            }

            public DateTime Timestamp { get; }
            public ArchiveFileKey Key { get; }
            public string Path { get; }
            public string? SoundBank { get; }

            public string? this[int column]
            {
                get
                {
                    return column switch
                    {
                        0 => Timestamp.ToShortTimeString(),
                        1 => Path,
                        2 => SoundBank,
                        _ => string.Empty
                    };
                }
            }

            public override bool Equals(object? other)
            {
                return other is FileLogEntry otherEntry && Key == otherEntry.Key;
            }

            public override int GetHashCode()
            {
                return Key.GetHashCode();
            }

            public override string ToString()
            {
                return Path;
            }
        }

        public FileAccessLogControl()
        {
            _lstLog.Columns = [
                new ListViewColumn("Time", new(40, GridUnitType.Pixel)),
                new ListViewColumn("Path", new(1, GridUnitType.Star)),
                new ListViewColumn("Sound bank", new(1, GridUnitType.Star))
            ];
        }

        protected override void SubscribeToEvents(NotificationChannel events)
        {
            events.OpeningFile += HandleOpeningFile;
        }

        protected override void UnsubscribeFromEvents(NotificationChannel events)
        {
            events.OpeningFile -= HandleOpeningFile;
        }

        private void HandleOpeningFile(ArchiveFileKey key, string path)
        {
            if (!EnableLogging)
                return;

            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.InvokeAsync(() => HandleOpeningFile(key, path));
                return;
            }

            string? soundBank = Path.GetExtension(path) == ".wem" ? GetWwiseSoundBank(path) : null;
            _lstLog.AddEntry(new FileLogEntry(DateTime.Now, key, path, soundBank));
        }

        private string? GetWwiseSoundBank(string soundFilePath)
        {
            if (!int.TryParse(Path.GetFileNameWithoutExtension(soundFilePath), out int soundId))
                return null;

            var soundUsages = ResourceUsages.GetWwiseSoundUsages(soundId);
            WwiseSoundBankItemReference? soundUsage = soundUsages.FirstOrDefault(u => u.Type == WwiseSoundBankItemReferenceType.DataIndex) ?? soundUsages.FirstOrDefault();
            if (soundUsage == null)
                return null;

            ResourceCollectionItemReference? bankUsage = ResourceUsages.GetResourceUsages(ArchiveSet, new ResourceKey(ResourceType.SoundBank, soundUsage.BankResourceId)).FirstOrDefault();
            if (bankUsage == null)
                return null;

            string? drmPath = CdcHash.Lookup(bankUsage.CollectionReference.NameHash, ArchiveSet.Game, true);
            if (drmPath == null)
                return null;

            return Path.GetFileName(drmPath);
        }

        protected override async Task ExtractAsync(string folderPath, ITaskProgress progress, CancellationToken cancellationToken)
        {
            List<ArchiveFileReference> fileRefs = _lstLog.Selection.SelectedItems.Cast<FileLogEntry>()
                                                                                 .Select(e => ArchiveSet.GetFileReference(e.Key))
                                                                                 .Where(f => f != null)
                                                                                 .ToList()!;

            var extractor = new Extractor.Extractor(ArchiveSet);
            await Task.Run(() => extractor.Extract(folderPath, fileRefs, progress, cancellationToken));
        }
    }
}
