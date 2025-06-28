using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using TrRebootTools.Shared;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Util;
using TrRebootTools.ModManager.Mod;

namespace TrRebootTools.ModManager
{
    internal partial class MainForm : Form, ITaskProgress
    {
        private readonly ArchiveSet _archiveSet;
        private readonly ResourceUsageCache _gameResourceUsages;
        private readonly BindingList<InstalledMod> _installedMods = new BindingList<InstalledMod>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private bool _closeRequested;

        public MainForm()
        {
            InitializeComponent();
            Font = SystemFonts.MessageBoxFont;
        }

        public MainForm(string gameFolderPath, CdcGame game)
            : this()
        {
            _archiveSet = ArchiveSet.Open(gameFolderPath, true, true, game);
            _gameResourceUsages = new ResourceUsageCache();

            CdcGameInfo gameInfo = CdcGameInfo.Get(game);
            Version version = Assembly.GetEntryAssembly().GetName().Version;
            Text = string.Format(Text, gameInfo.ShortName, $"{version.Major}.{version.Minor}.{version.Build}");
            _btnSelectGame.BackgroundImage = gameInfo.Icon;
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            MainForm_Resize(sender, e);
            foreach (ColumnStyle column in _pnlToolbar.ColumnStyles)
            {
                if (column.SizeType == SizeType.Absolute)
                    column.Width = _pnlToolbar.Height;
            }

            _lvMods.CheckedMember = nameof(InstalledMod.Enabled);
            _lvMods.DisplayMember = nameof(InstalledMod.Name);
            _lvMods.ForeColorMember = nameof(InstalledMod.NameColor);
            _lvMods.DataSource = _installedMods;
            RefreshModList();
            _installedMods.ListChanged += HandleInstalledModChanged;

            if (!_gameResourceUsages.Load(_archiveSet.FolderPath))
            {
                using ArchiveSet gameArchiveSet = ArchiveSet.Open(_archiveSet.FolderPath, true, false, _archiveSet.Game);
                await Task.Run(() => _gameResourceUsages.AddArchiveSet(gameArchiveSet, this, _cancellationTokenSource.Token));
                _gameResourceUsages.Save(_archiveSet.FolderPath);
            }

            if (_archiveSet.DuplicateArchives.Any(a => a.ModName != null))
                await ReinstallMods(false);
        }

        public bool GameSelectionRequested
        {
            get;
            private set;
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (_lvMods != null)
                _colModName.Width = _lvMods.Width - 30;
        }

        private async void _btnAddFromZip_Click(object sender, EventArgs e)
        {
            if (_fileBrowser.ShowDialog() != DialogResult.OK)
                return;

            await InstallModFromZipAsync(_fileBrowser.FileName);
        }

        private async Task InstallModFromZipAsync(string filePath)
        {
            try
            {
                ModInstaller installer = new ModInstaller(_archiveSet, _gameResourceUsages);
                InstalledMod mod = await Task.Run(() => installer.InstallFromZip(filePath, this, _cancellationTokenSource.Token));
                if (mod == null)
                    return;

                await UpdateFlatModArchiveAsync();
                _installedMods.Add(mod);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private async void _btnAddFromFolder_Click(object sender, EventArgs e)
        {
            if (_folderBrowser.ShowDialog() != DialogResult.OK)
                return;

            await InstallModFromFolderAsync(_folderBrowser.SelectedPath);
        }

        private async Task InstallModFromFolderAsync(string folderPath)
        {
            try
            {
                ModInstaller installer = new ModInstaller(_archiveSet, _gameResourceUsages);
                InstalledMod mod = await Task.Run(() => installer.InstallFromFolder(folderPath, this, _cancellationTokenSource.Token));
                if (mod == null)
                    return;

                await UpdateFlatModArchiveAsync();
                RefreshModList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private async void HandleInstalledModChanged(object sender, ListChangedEventArgs e)
        {
            if (e.ListChangedType != ListChangedType.ItemChanged)
                return;

            InstalledMod mod = _installedMods[e.NewIndex];

            try
            {
                if (mod.Enabled)
                    await Task.Run(() => _archiveSet.Enable(mod.ArchiveId, _gameResourceUsages, this, _cancellationTokenSource.Token));
                else
                    await Task.Run(() => _archiveSet.Disable(mod.ArchiveId, _gameResourceUsages, this, _cancellationTokenSource.Token));

                await UpdateFlatModArchiveAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            finally
            {
                _archiveSet.CloseStreams();
            }
        }

        private async void _btnRemove_Click(object sender, EventArgs e)
        {
            if (_lvMods.SelectedIndices.Count == 0)
            {
                MessageBox.Show("No mods selected to remove.", "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(
                    "Are you sure you want to remove the selected mod(s)?",
                    "Confirm",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                return;
            }

            try
            {
                foreach (int index in _lvMods.SelectedIndices.Cast<int>().OrderByDescending(i => i).ToList())
                {
                    InstalledMod mod = _installedMods[index];
                    await Task.Run(() => _archiveSet.Delete(mod.ArchiveId, _gameResourceUsages, this, _cancellationTokenSource.Token));
                    _installedMods.RemoveAt(index);
                }
                await UpdateFlatModArchiveAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            finally
            {
                _archiveSet.CloseStreams();
            }
        }

        private async void _btnReinstall_Click(object sender, EventArgs e)
        {
            await ReinstallMods(true);
        }

        private async Task ReinstallMods(bool showCompletionMessage)
        {
            ModInstaller installer = new ModInstaller(_archiveSet, _gameResourceUsages);
            try
            {
                await Task.Run(() => installer.ReinstallAll(this, _cancellationTokenSource.Token));
                await UpdateFlatModArchiveAsync();
                if (showCompletionMessage)
                    MessageBox.Show("Mod reinstallation complete.", "", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            RefreshModList();
        }

        private void _btnSelectGame_Click(object sender, EventArgs e)
        {
            GameSelectionRequested = true;
            Close();
        }

        private async Task UpdateFlatModArchiveAsync()
        {
            ModInstaller installer = new ModInstaller(_archiveSet, _gameResourceUsages);
            await Task.Run(() => installer.UpdateFlatModArchive(this, _cancellationTokenSource.Token));
        }

        private void _lvMods_DragEnter(object sender, DragEventArgs e)
        {
            if (GetDroppedFilesIfAllowed(e) != null)
                e.Effect = DragDropEffects.Copy;
        }

        private async void _lvMods_DragDrop(object sender, DragEventArgs e)
        {
            string[] paths = GetDroppedFilesIfAllowed(e);
            if (paths == null)
                return;

            foreach (string path in paths)
            {
                if (File.Exists(path))
                    await InstallModFromZipAsync(path);
                else
                    await InstallModFromFolderAsync(path);
            }
        }

        private string[] GetDroppedFilesIfAllowed(DragEventArgs e)
        {
            if (!_lvMods.Enabled)
                return null;

            if (!(e.Data.GetData(DataFormats.FileDrop) is string[] paths))
                return null;

            foreach (string path in paths)
            {
                if (File.Exists(path))
                {
                    string extension = Path.GetExtension(path);
                    if (extension != ".7z" && extension != ".zip" && extension != ".rar")
                        return null;
                }
                else if (!Directory.Exists(path))
                {
                    return null;
                }
            }
            return paths;
        }

        private void _lvMods_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
                _btnRemove_Click(sender, e);
        }

        private void RefreshModList()
        {
            _installedMods.Clear();
            _installedMods.AddRange(
                _archiveSet.Archives
                           .Where(a => a.ModName != null && a.SubId == 0)
                           .OrderBy(a => a.MetaData.Version)
                           .Select(a => new InstalledMod(a.Id, a.ModName, a.MetaData.Enabled))
            );
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_progressBar.Visible)
                return;

            _statusBar.Text = "Canceling...";
            _cancellationTokenSource.Cancel();
            _closeRequested = true;
            e.Cancel = true;
        }

        void ITaskProgress.Begin(string statusText)
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => ((ITaskProgress)this).Begin(statusText)));
                return;
            }

            EnableUi(false);
            _lblStatus.Text = statusText;
            _progressBar.Value = 0;
            _progressBar.Visible = true;
        }

        void ITaskProgress.Report(float progress)
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => ((ITaskProgress)this).Report(progress)));
                return;
            }

            _progressBar.Value = (int)(progress * _progressBar.Maximum);
        }

        void ITaskProgress.End()
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => ((ITaskProgress)this).End()));
                return;
            }

            EnableUi(true);
            _lblStatus.Text = string.Empty;
            _progressBar.Visible = false;
            if (_closeRequested)
                Close();
        }

        private void EnableUi(bool enable)
        {
            _pnlToolbar.Enabled = enable;
            _lvMods.Enabled = enable;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                components?.Dispose();

            _archiveSet?.Dispose();
            base.Dispose(disposing);
        }
    }
}
