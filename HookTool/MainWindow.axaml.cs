using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TrRebootTools.ModManager.Mod;
using TrRebootTools.Shared;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Forms;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.HookTool
{
    public partial class MainWindow : WindowWithProgress
    {
        private const string ModName = "hooktool";

        private readonly ArchiveSet _archiveSet;
        private readonly ResourceUsageCache _gameResourceUsages;

        private GameProcess? _process;
        private bool _ingame;
        private string? _modFolder;
        private readonly FileSystemWatcher _watcher = new() { NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite, IncludeSubdirectories = true };
        private readonly System.Timers.Timer _modReinstallTimer = new(1000) { AutoReset = false };

        private readonly SemaphoreSlim _modInstallLock = new(1, 1);

        public MainWindow()
        {
            InitializeComponent();
        }

        public MainWindow(string gameFolderPath, CdcGame game)
            : this()
        {
            CdcGameInfo gameInfo = CdcGameInfo.Get(game);
            Version version = Assembly.GetExecutingAssembly().GetName().Version!;
            Title = $"{gameInfo.ShortName} Hook Tool {version.Major}.{version.Minor}.{version.Build}";

            _archiveSet = ArchiveSet.Open(gameFolderPath, true, true, game);
            _gameResourceUsages = new ResourceUsageCache();
            _watcher.Created += HandleModChanged;
            _watcher.Changed += HandleModChanged;
            _watcher.Renamed += HandleModChanged;
            _watcher.Deleted += HandleModChanged;
            _modReinstallTimer.Elapsed += async (s, e) => await HandleModChangeTimerElapsed();

            if (game != CdcGame.Shadow)
                _tpAnimationLog.IsVisible = false;
        }

        protected override async void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            if (Design.IsDesignMode)
                return;

            if (!_gameResourceUsages.Load(_archiveSet.FolderPath))
            {
                using ArchiveSet gameArchiveSet = ArchiveSet.Open(_archiveSet.FolderPath, true, false, _archiveSet.Game);
                await Task.Run(() => _gameResourceUsages.AddArchiveSet(gameArchiveSet, this, CancellationToken));
                _gameResourceUsages.Save(_archiveSet.FolderPath);
            }

            string? modFolderPath = Configuration.Load().ExtraSettings.GetValueOrDefault("ModFolder");
            if (!string.IsNullOrEmpty(modFolderPath))
            {
                if (Directory.Exists(modFolderPath))
                    await SetModFolderAsync(modFolderPath);
                else
                    modFolderPath = null;
            }

            string gameExePath = Path.Combine(_archiveSet.FolderPath, CdcGameInfo.Get(_archiveSet.Game).ExeNames[0]);
            try
            {
                _process = new GameProcess(gameExePath, _archiveSet.Game);
                _process.Start();
            }
            catch (Exception ex)
            {
                await MessageBox.ShowErrorAsync(ex);
                Close();
                return;
            }
            _process.Events.GameEntered += HandleGameEntered;
            _process.Exited += HandleGameExited;

            _fileLog.Init(_archiveSet, _gameResourceUsages, _process.Events, this, CancellationToken);
            _animationLog.Init(_archiveSet, _gameResourceUsages, _process.Events, this, CancellationToken);
            _materialControl.Init(_process, _archiveSet.Game);
            _materialControl.SetModFolder(modFolderPath);
        }

        private void HandleGameEntered()
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Invoke(HandleGameEntered);
                return;
            }
            _ingame = true;
            _btnBrowseModFolder.IsEnabled = true;
        }

        private void OnDragEnter(object? sender, DragEventArgs e)
        {
            List<string> paths = e.DataTransfer.GetFileSystemPaths().ToList();
            e.DragEffects = _ingame && paths.Count == 1 && Directory.Exists(paths[0]) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private async void OnDragDrop(object? sender, DragEventArgs e)
        {
            List<string> paths = e.DataTransfer.GetFileSystemPaths().ToList();
            if (_ingame && paths.Count == 1 && Directory.Exists(paths[0]))
                await SetModFolderAsync(paths[0]);
        }

        private async void OnBrowseModFolderClick(object? sender, RoutedEventArgs e)
        {
            string? folderPath = await App.OpenFolderPickerAsync("Select mod folder");
            if (folderPath != null)
                await SetModFolderAsync(folderPath);
        }

        private async Task SetModFolderAsync(string folderPath)
        {
            _modFolder = folderPath;
            _btnBrowseModFolder.Content = folderPath;
            _materialControl.SetModFolder(folderPath);

            Configuration config = Configuration.Load();
            config.ExtraSettings["ModFolder"] = folderPath;
            config.Save();

            try
            {
                UninstallMod();
                await InstallModAsync();
            }
            catch (Exception ex)
            {
                await MessageBox.ShowErrorAsync(ex);
            }

            _watcher.Path = folderPath;
            _watcher.EnableRaisingEvents = true;
        }

        private void OnSavingMaterial(object? sender, EventArgs e)
        {
            _watcher.EnableRaisingEvents = false;
        }

        private void OnSavedMaterial(object? sender, EventArgs e)
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
                    await MessageBox.ShowErrorAsync(ex);
                }
                finally
                {
                    _modInstallLock.Release();
                }
            }

            Dispatcher.UIThread.Invoke(() => _materialControl.SetModFolder(_modFolder));
        }

        private void UninstallMod()
        {
            int? archiveId = _archiveSet.Archives.FirstOrDefault(a => a.ModName == ModName)?.Id;
            if (archiveId == null)
                return;

            if (_ingame)
            {
                _archiveSet.Disable(archiveId.Value, _gameResourceUsages, null, CancellationToken.None);
                _process?.Commands.UnloadMissingArchives();
                Thread.Sleep(1000);
            }
            _archiveSet.Delete(archiveId.Value, _gameResourceUsages, null, CancellationToken.None);
        }

        private async Task InstallModAsync()
        {
            if (_modFolder == null)
                return;

            ModInstaller installer = new ModInstaller(_archiveSet, _gameResourceUsages);
            await Task.Run(() => installer.InstallFromFolderAsync(ModName, _modFolder, this, CancellationToken));
            _archiveSet.CloseStreams();
            if (_ingame)
                _process?.Commands.LoadNewArchives();
        }

        private void HandleGameExited()
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Invoke(HandleGameExited);
                return;
            }
            _ingame = false;
            _btnBrowseModFolder.IsEnabled = false;
            _process?.Dispose();
            _process = null;
            UninstallMod();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            UninstallMod();
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            _process?.Dispose();
        }
    }
}