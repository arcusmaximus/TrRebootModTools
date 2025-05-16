using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TrRebootTools.Shared;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Controls.VirtualTreeView;
using TrRebootTools.HookTool.Logging;
using TrRebootTools.HookTool;

namespace TrRebootTools.HookTool.Logging
{
    internal class FileAccessLogControl : AccessLogControl
    {
        private class FileLogEntry
        {
            public FileLogEntry(DateTime timestamp, ArchiveFileKey key, string path, string soundBank)
            {
                Timestamp = timestamp;
                Key = key;
                Path = path;
                SoundBank = soundBank;
            }

            public DateTime Timestamp { get; }
            public ArchiveFileKey Key { get; }
            public string Path { get; }
            public string SoundBank { get; }

            public override bool Equals(object other)
            {
                return Key == ((FileLogEntry)other).Key;
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
            _tvLog.Header.Columns.Add(new VirtualTreeColumn { Name = "Time", Width = 100 });
            _tvLog.Header.Columns.Add(new VirtualTreeColumn { Name = "File", Width = 400 });
            _tvLog.Header.Columns.Add(new VirtualTreeColumn { Name = "SoundBank", Width = 300 });
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

            if (InvokeRequired)
            {
                BeginInvoke(() => HandleOpeningFile(key, path));
                return;
            }

            string soundBank = Path.GetExtension(path) == ".wem" ? GetWwiseSoundBank(path) : null;
            _tvLog.AddItem(new FileLogEntry(DateTime.Now, key, path, soundBank));
        }

        private string GetWwiseSoundBank(string soundFilePath)
        {
            if (!int.TryParse(Path.GetFileNameWithoutExtension(soundFilePath), out int soundId))
                return null;

            var soundUsages = ResourceUsages.GetWwiseSoundUsages(soundId);
            WwiseSoundBankItemReference soundUsage = soundUsages.FirstOrDefault(u => u.Type == WwiseSoundBankItemReferenceType.DataIndex) ?? soundUsages.FirstOrDefault();
            if (soundUsage == null)
                return null;

            ResourceCollectionItemReference bankUsage = ResourceUsages.GetResourceUsages(ArchiveSet, new ResourceKey(ResourceType.SoundBank, soundUsage.BankResourceId)).FirstOrDefault();
            if (bankUsage == null)
                return null;

            string drmPath = CdcHash.Lookup(bankUsage.CollectionReference.NameHash, ArchiveSet.Game);
            if (drmPath == null)
                return null;

            return Path.GetFileName(drmPath);
        }

        protected override void GetNodeCellText(VirtualTreeView tree, VirtualTreeNode node, int column, out string cellText)
        {
            FileLogEntry entry = tree.GetNodeData<FileLogEntry>(node);
            if (entry == null)
            {
                cellText = "";
                return;
            }

            cellText = column switch
            {
                0 => entry.Timestamp.ToShortTimeString(),
                1 => entry.Path,
                2 => entry.SoundBank,
                _ => ""
            };
        }

        protected override async Task ExtractAsync(string folderPath, ITaskProgress progress, CancellationToken cancellationToken)
        {
            List<ArchiveFileReference> fileRefs = GetSelectedItems<FileLogEntry>().Select(e => ArchiveSet.GetFileReference(e.Key))
                                                                                  .Where(f => f != null)
                                                                                  .ToList();

            var extractor = new Extractor.Extractor(ArchiveSet);
            await Task.Run(() => extractor.Extract(folderPath, fileRefs, progress, cancellationToken));
        }
    }
}
