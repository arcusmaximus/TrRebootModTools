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
    internal class AnimationAccessLogControl : AccessLogControl
    {
        private class AnimationLogEntry : IListViewEntry
        {
            public AnimationLogEntry(DateTime timestamp, string? drm, int id, string name)
            {
                Timestamp = timestamp;
                Drm = drm;
                Id = id;
                Name = name;
            }

            public DateTime Timestamp { get; }
            public string? Drm { get; }
            public int Id { get; }
            public string Name { get; }

            public string? this[int column]
            {
                get
                {
                    return column switch
                    {
                        0 => Timestamp.ToShortTimeString(),
                        1 => Drm,
                        2 => Id.ToString(),
                        3 => Name,
                        _ => null
                    };
                }
            }

            public override bool Equals(object? other)
            {
                return other is AnimationLogEntry otherEntry && Id == otherEntry.Id;
            }

            public override int GetHashCode()
            {
                return Id;
            }

            public override string ToString()
            {
                return $"{Drm} - {Name}";
            }
        }

        public AnimationAccessLogControl()
        {
            _lstLog.Columns = [
                new ListViewColumn("Time", new(40, GridUnitType.Pixel)),
                new ListViewColumn("DRM", new(1, GridUnitType.Star)),
                new ListViewColumn("ID", new(100, GridUnitType.Pixel)),
                new ListViewColumn("Name", new(1, GridUnitType.Star))
            ];
        }

        protected override void SubscribeToEvents(NotificationChannel events)
        {
            events.PlayingAnimation += HandlePlayingAnimation;
        }

        protected override void UnsubscribeFromEvents(NotificationChannel events)
        {
            events.PlayingAnimation -= HandlePlayingAnimation;
        }

        private void HandlePlayingAnimation(int id, string name)
        {
            if (!EnableLogging)
                return;

            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.InvokeAsync(() => HandlePlayingAnimation(id, name));
                return;
            }

            string? drmName = null;
            ResourceCollectionItemReference? usage = ResourceUsages.GetResourceUsages(ArchiveSet, new ResourceKey(ResourceType.Animation, id)).FirstOrDefault();
            if (usage != null)
            {
                string? drmPath = CdcHash.Lookup(usage.CollectionReference.NameHash, ArchiveSet.Game, true);
                if (drmPath != null)
                    drmName = Path.GetFileName(drmPath);
            }
            _lstLog.AddEntry(new AnimationLogEntry(DateTime.Now, drmName, id, name));
        }

        protected override async Task ExtractAsync(string folderPath, ITaskProgress progress, CancellationToken cancellationToken)
        {
            Dictionary<string, ResourceReference> animRefs = new();
            foreach (AnimationLogEntry entry in _lstLog.Selection.SelectedItems!)
            {
                ResourceReference? resourceRef = ResourceUsages.GetResourceReference(ArchiveSet, new ResourceKey(ResourceType.Animation, entry.Id));
                if (resourceRef != null)
                    animRefs[entry.Name] = resourceRef;
            }

            folderPath = Path.Combine(folderPath, "Animations");
            Directory.CreateDirectory(folderPath);
            await Task.Run(() => ExtractAnimations(folderPath, animRefs, progress, cancellationToken));
        }

        private void ExtractAnimations(string baseFolderPath, Dictionary<string, ResourceReference> animRefs, ITaskProgress progress, CancellationToken cancellationToken)
        {
            try
            {
                progress.Begin("Extracting...");

                int numExtracted = 0;
                foreach ((string name, ResourceReference resourceRef) in animRefs)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    string filePath = Path.Combine(baseFolderPath, name + "." + ResourceNaming.GetFileName(ArchiveSet, resourceRef));
                    using Stream resourceStream = ArchiveSet.OpenResource(resourceRef);
                    using Stream fileStream = File.Create(filePath);
                    resourceStream.CopyTo(fileStream);

                    numExtracted++;
                    progress.Report((float)numExtracted / animRefs.Count);
                }
            }
            finally
            {
                progress.End();
            }
        }
    }
}
