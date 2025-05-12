using System;
using System.Windows.Forms;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared;
using TrRebootTools.Shared.Controls.VirtualTreeView;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace TrRebootTools.HookTool
{
    internal partial class AccessLogControl : UserControl, ITaskProgress
    {
        private NotificationChannel _events;
        private ITaskProgress _progress;
        private CancellationToken _cancellationToken;

        public AccessLogControl()
        {
            InitializeComponent();
        }

        public void Init(ArchiveSet archiveSet, ResourceUsageCache resourceUsages, NotificationChannel events, ITaskProgress progress, CancellationToken cancellationToken)
        {
            ArchiveSet = archiveSet;
            ResourceUsages = resourceUsages;

            _events = events;
            _progress = progress;
            _cancellationToken = cancellationToken;
            SubscribeToEvents(_events);
        }

        protected virtual void SubscribeToEvents(NotificationChannel events)
        {
        }

        protected virtual void UnsubscribeFromEvents(NotificationChannel events)
        {
        }

        protected bool EnableLogging
        {
            get;
            set;
        } = true;

        protected ArchiveSet ArchiveSet
        {
            get;
            private set;
        }

        protected ResourceUsageCache ResourceUsages
        {
            get;
            private set;
        }

        private void _btnEnableLogging_Click(object sender, EventArgs e)
        {
            EnableLogging = _btnEnableLogging.Checked;
        }

        private void _btnClearLists_Click(object sender, EventArgs e)
        {
            _tvLog.Clear();
        }

        private void _btnCopy_Click(object sender, EventArgs e)
        {
            CopyToClipboard();
        }

        private void _tvLog_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A)
                _tvLog.SelectAll();
            else if (e.Control && e.KeyCode == Keys.C)
                CopyToClipboard();
        }

        private void _btnSave_Click(object sender, EventArgs e)
        {
            if (_saveFileDialog.ShowDialog() != DialogResult.OK)
                return;

            string csv = RowsToCsv(_tvLog.Nodes);
            File.WriteAllText(_saveFileDialog.FileName, csv);
        }

        private void CopyToClipboard()
        {
            string csv = RowsToCsv(_tvLog.SelectedNodes);
            Clipboard.SetText(csv);
        }

        private string RowsToCsv(IEnumerable<VirtualTreeNode> nodes)
        {
            StringBuilder result = new();
            foreach (VirtualTreeNode node in nodes)
            {
                for (int colIdx = 0; colIdx < _tvLog.Header.Columns.Count; colIdx++)
                {
                    if (colIdx > 0)
                        result.Append(",");

                    GetNodeCellText(_tvLog, node, colIdx, out string cellText);
                    result.Append(cellText);
                }
                result.AppendLine();
            }
            return result.ToString();
        }

        protected virtual void GetNodeCellText(VirtualTreeView tree, VirtualTreeNode node, int column, out string cellText)
        {
            cellText = null;
        }

        private void _tvLog_OnSelectionChanged(object sender, EventArgs e)
        {
            UpdateExtractButton();
        }

        private void UpdateExtractButton()
        {
            _btnExtract.Enabled = Enabled && _tvLog.SelectedNodes.Any();
        }

        private void _tvLog_DoubleClick(object sender, EventArgs e)
        {
            if (_btnExtract.Enabled)
                _btnExtract_Click(_btnExtract, EventArgs.Empty);
        }

        private async void _btnExtract_Click(object sender, EventArgs e)
        {
            try
            {
                string folderPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), CdcGameInfo.Get(ArchiveSet.Game).ShortName);
                await ExtractAsync(folderPath, this, _cancellationToken);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            finally
            {
                ArchiveSet.CloseStreams();
            }
        }

        protected virtual async Task ExtractAsync(string folderPath, ITaskProgress progress, CancellationToken cancellationToken)
        {
        }

        protected IEnumerable<T> GetSelectedItems<T>()
        {
            return _tvLog.SelectedNodes.Select(_tvLog.GetNodeData<T>);
        }

        void ITaskProgress.Begin(string statusText)
        {
            if (InvokeRequired)
            {
                Invoke(() => ((ITaskProgress)this).Begin(statusText));
                return;
            }

            _progress.Begin(statusText);
            _tvLog.Enabled = false;
            _btnExtract.Enabled = false;
        }

        void ITaskProgress.Report(float progress)
        {
            _progress.Report(progress);
        }

        void ITaskProgress.End()
        {
            if (InvokeRequired)
            {
                Invoke(() => ((ITaskProgress)this).End());
                return;
            }

            _progress.End();
            _tvLog.Enabled = true;
            _btnExtract.Enabled = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (_events != null)
                UnsubscribeFromEvents(_events);

            if (disposing)
                components?.Dispose();
            
            base.Dispose(disposing);
        }
    }
}
