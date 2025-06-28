using Ookii.Dialogs.WinForms;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TrRebootTools.ModManager.Mod;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Forms;

namespace TrRebootTools.HookTool
{
    internal partial class MainForm : FormWithProgress
    {
        private const string ModName = "hooktool";

        private readonly ArchiveSet _archiveSet;
        private readonly ResourceUsageCache _gameResourceUsages;

        private GameProcess _process;
        private bool _ingame;
        private string _modFolder;
        private readonly FileSystemWatcher _watcher = new() { NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite, IncludeSubdirectories = true };
        private readonly System.Timers.Timer _modReinstallTimer = new(1000) { AutoReset = false };

        private readonly SemaphoreSlim _modInstallLock = new(1, 1);

        public MainForm()
        {
            InitializeComponent();
        }

        public MainForm(string gameFolderPath, CdcGame game)
            : this()
        {
            CdcGameInfo gameInfo = CdcGameInfo.Get(game);
            Version version = Assembly.GetEntryAssembly().GetName().Version;
            Text = string.Format(Text, gameInfo.ShortName, $"{version.Major}.{version.Minor}.{version.Build}");

            _archiveSet = ArchiveSet.Open(gameFolderPath, true, true, game);
            _gameResourceUsages = new ResourceUsageCache();
            _watcher.Created += HandleModChanged;
            _watcher.Changed += HandleModChanged;
            _watcher.Renamed += HandleModChanged;
            _watcher.Deleted += HandleModChanged;
            _modReinstallTimer.Elapsed += async (s, e) => await HandleModChangeTimerElapsed();

            if (game != CdcGame.Shadow)
                _tcMain.TabPages.Remove(_tpAnimations);
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            if (!_gameResourceUsages.Load(_archiveSet.FolderPath))
            {
                using ArchiveSet gameArchiveSet = ArchiveSet.Open(_archiveSet.FolderPath, true, false, _archiveSet.Game);
                await Task.Run(() => _gameResourceUsages.AddArchiveSet(gameArchiveSet, this, CancellationTokenSource.Token));
                _gameResourceUsages.Save(_archiveSet.FolderPath);
            }

            string modFolderPath = ConfigurationManager.AppSettings["ModFolder"];
            if (!string.IsNullOrEmpty(modFolderPath))
            {
                if (Directory.Exists(modFolderPath))
                    await SetModFolderAsync(modFolderPath);
                else
                    modFolderPath = null;
            }

            string gameExePath = Path.Combine(_archiveSet.FolderPath, CdcGameInfo.Get(_archiveSet.Game).ExeName);
            try
            {
                _process = new GameProcess(gameExePath, _archiveSet.Game);
                _process.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                Close();
                return;
            }
            _process.Events.GameEntered += HandleGameEntered;
            _process.Exited += HandleGameExited;

            _fileLog.Init(_archiveSet, _gameResourceUsages, _process.Events, this, CancellationTokenSource.Token);
            _animationLog.Init(_archiveSet, _gameResourceUsages, _process.Events, this, CancellationTokenSource.Token);
            _materialControl.Init(_process, _archiveSet.Game);
            _materialControl.SetModFolder(modFolderPath);
        }

        private async void HandleGameEntered()
        {
            if (InvokeRequired)
            {
                Invoke(HandleGameEntered);
                return;
            }
            _ingame = true;
            _btnBrowseModFolder.Enabled = true;
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (_ingame && e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length == 1 && Directory.Exists(paths[0]))
                e.Effect = DragDropEffects.Copy;
        }

        private async void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            if (_ingame && e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length == 1 && Directory.Exists(paths[0]))
                await SetModFolderAsync(paths[0]);
        }

        private async void _btnBrowseModFolder_Click(object sender, EventArgs e)
        {
            using var dialog = new VistaFolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
                await SetModFolderAsync(dialog.SelectedPath);
        }

        private async Task SetModFolderAsync(string folderPath)
        {
            _modFolder = folderPath;
            _btnBrowseModFolder.Text = folderPath;
            _materialControl.SetModFolder(folderPath);

            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings["ModFolder"].Value = folderPath;
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection(config.AppSettings.SectionInformation.Name);

            try
            {
                UninstallMod();
                await InstallModAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }

            _watcher.Path = folderPath;
            _watcher.EnableRaisingEvents = true;
        }

        private void _materialControl_SavingMaterial(object sender, EventArgs e)
        {
            _watcher.EnableRaisingEvents = false;
        }

        private void _materialControl_SavedMaterial(object sender, EventArgs e)
        {
            _watcher.EnableRaisingEvents = true;
        }

        private void HandleModChanged(object sender, FileSystemEventArgs e)
        {
            _modReinstallTimer.Stop();
            _modReinstallTimer.Start();
        }

        private async Task HandleModChangeTimerElapsed()
        {
            if (_ingame)
            {
                await _modInstallLock.WaitAsync();
                try
                {
                    UninstallMod();
                    await InstallModAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
                finally
                {
                    _modInstallLock.Release();
                }
            }

            Invoke(() => _materialControl.SetModFolder(_modFolder));
        }

        private void UninstallMod()
        {
            int? archiveId = _archiveSet.Archives.FirstOrDefault(a => a.ModName == ModName)?.Id;
            if (archiveId == null)
                return;

            if (_ingame)
            {
                _archiveSet.Disable(archiveId.Value, _gameResourceUsages, null, CancellationToken.None);
                _process.Commands.UnloadMissingArchives();
                Thread.Sleep(1000);
            }
            _archiveSet.Delete(archiveId.Value, _gameResourceUsages, null, CancellationToken.None);
        }

        private async Task InstallModAsync()
        {
            if (_modFolder == null)
                return;

            ModInstaller installer = new ModInstaller(_archiveSet, _gameResourceUsages);
            await Task.Run(() => installer.InstallFromFolder(ModName, _modFolder, this, CancellationTokenSource.Token));
            _archiveSet.CloseStreams();
            if (_ingame)
                _process.Commands.LoadNewArchives();
        }

        private void HandleGameExited()
        {
            if (InvokeRequired)
            {
                Invoke(HandleGameExited);
                return;
            }
            _ingame = false;
            _btnBrowseModFolder.Enabled = false;
            _process.Dispose();
            _process = null;
            UninstallMod();
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            UninstallMod();
        }

        protected override void Dispose(bool disposing)
        {
            _process?.Dispose();

            if (disposing)
                components?.Dispose();

            base.Dispose(disposing);
        }
    }
}
