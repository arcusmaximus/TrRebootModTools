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
using TrRebootTools.Shared.Util;

namespace TrRebootTools.HookTool.Logging
{
    internal class AnimationAccessLogControl : AccessLogControl
    {
        private class AnimationLogEntry : IListViewEntry
        {
            public AnimationLogEntry(DateTime timestamp, string? drm, ResourceCollectionItemReference resource, int id, string name)
            {
                Timestamp = timestamp;
                Drm = drm;
                Resource = resource;
                Id = id;
                Name = name;
            }

            public DateTime Timestamp { get; }
            public string? Drm { get; }
            public ResourceCollectionItemReference Resource { get; }
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

        private void HandlePlayingAnimation(int id, string name)
        {
            if (!EnableLogging)
                return;

            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.InvokeAsync(() => HandlePlayingAnimation(id, name));
                return;
            }

            ResourceCollectionItemReference? resource = ResourceUsages.GetResourceUsages(ArchiveSet, new ResourceKey(ResourceType.Animation, id)).FirstOrDefault();
            if (resource == null)
                return;

            string? drmName = null;
            string? drmPath = CdcHash.Lookup(resource.CollectionReference.NameHash, ArchiveSet.Game, true);
            if (drmPath != null)
                drmName = Path.GetFileName(drmPath);

            _lstLog.AddEntry(new AnimationLogEntry(DateTime.Now, drmName, resource, id, name));
        }

        protected override async Task ExtractAsync(string folderPath, ITaskProgress progress, CancellationToken cancellationToken)
        {
            folderPath = Path.Combine(folderPath, "Animations");
            Directory.CreateDirectory(folderPath);
            List<AnimationLogEntry> anims = _lstLog.Selection.SelectedItems!.Cast<AnimationLogEntry>().ToList();
            await Task.Run(() => ExtractAnimations(folderPath, anims, progress, cancellationToken));
        }

        private void ExtractAnimations(string baseFolderPath, List<AnimationLogEntry> anims, ITaskProgress progress, CancellationToken cancellationToken)
        {
            try
            {
                progress.Begin("Extracting...");

                Dictionary<ArchiveFileReference, ResourceCollection?> collections = new();
                int numExtracted = 0;
                foreach (AnimationLogEntry anim in anims)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    ResourceCollection? collection = collections.GetOrAdd(anim.Resource.CollectionReference, ArchiveSet.GetResourceCollection);
                    if (collection == null)
                        continue;

                    ResourceReference resourceRef = collection.ResourceReferences[anim.Resource.ResourceIndex];

                    string filePath = Path.Combine(baseFolderPath, anim.Name + "." + ResourceNaming.GetFileName(ArchiveSet, collection, resourceRef, false));
                    using Stream resourceStream = ArchiveSet.OpenResource(resourceRef);
                    using Stream fileStream = File.Create(filePath);
                    resourceStream.CopyTo(fileStream);

                    numExtracted++;
                    progress.Report((float)numExtracted / anims.Count);
                }
            }
            finally
            {
                progress.End();
            }
        }
    }
}
