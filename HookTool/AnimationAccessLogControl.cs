using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TrRebootTools.Shared;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Controls.VirtualTreeView;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.HookTool
{
    internal class AnimationAccessLogControl : AccessLogControl
    {
        private class AnimationLogEntry
        {
            public AnimationLogEntry(DateTime timestamp, string drm, int id, string name)
            {
                Timestamp = timestamp;
                Drm = drm;
                Id = id;
                Name = name;
            }

            public DateTime Timestamp { get; }
            public string Drm { get; }
            public int Id { get; }
            public string Name { get; }

            public override bool Equals(object other)
            {
                return Id == ((AnimationLogEntry)other).Id;
            }

            public override int GetHashCode()
            {
                return Id;
            }
        }

        public AnimationAccessLogControl()
        {
            _tvLog.Header.Columns.Add(new VirtualTreeColumn { Name = "Time", Width = 100 });
            _tvLog.Header.Columns.Add(new VirtualTreeColumn { Name = "DRM", Width = 300 });
            _tvLog.Header.Columns.Add(new VirtualTreeColumn { Name = "ID", Width = 100 });
            _tvLog.Header.Columns.Add(new VirtualTreeColumn { Name = "Name", Width = 300 });
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

            if (InvokeRequired)
            {
                BeginInvoke(() => HandlePlayingAnimation(id, name));
                return;
            }

            string drmName = null;
            ResourceCollectionItemReference usage = ResourceUsages.GetResourceUsages(ArchiveSet, new ResourceKey(ResourceType.Animation, id)).FirstOrDefault();
            if (usage != null)
            {
                string drmPath = CdcHash.Lookup(usage.CollectionReference.NameHash, ArchiveSet.Game);
                if (drmPath != null)
                    drmName = Path.GetFileName(drmPath);
            }
            _tvLog.AppendNode(new AnimationLogEntry(DateTime.Now, drmName, id, name));
        }

        protected override void GetNodeCellText(VirtualTreeView tree, VirtualTreeNode node, int column, out string cellText)
        {
            AnimationLogEntry entry = tree.GetNodeData<AnimationLogEntry>(node);
            if (entry == null)
            {
                cellText = "";
                return;
            }

            cellText = column switch
            {
                0 => entry.Timestamp.ToShortTimeString(),
                1 => entry.Drm,
                2 => entry.Id.ToString(),
                3 => entry.Name,
                _ => ""
            };
        }

        protected override async Task ExtractAsync(string folderPath, ITaskProgress progress, CancellationToken cancellationToken)
        {
            Dictionary<string, ResourceReference> animRefs = new();
            foreach (AnimationLogEntry entry in GetSelectedItems< AnimationLogEntry>())
            {
                ResourceReference resourceRef = ResourceUsages.GetResourceReference(ArchiveSet, new ResourceKey(ResourceType.Animation, entry.Id));
                if (resourceRef != null)
                    animRefs[entry.Name] = resourceRef;
            }

            folderPath = Path.Combine(folderPath, "Animations");
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

                    string folderPath = Path.Combine(baseFolderPath, name);
                    Directory.CreateDirectory(folderPath);

                    string filePath = Path.Combine(folderPath, ResourceNaming.GetFileName(ArchiveSet, resourceRef));
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
