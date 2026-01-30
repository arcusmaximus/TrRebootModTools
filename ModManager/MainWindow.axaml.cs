using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace TrRebootTools.ModManager
{
    internal partial class MainWindow : WindowWithProgress
    {
        public static readonly IValueConverter InstalledModOpacityConverter =
            new FuncValueConverter<bool, double>(enabled => enabled ? 1.0 : 0.5);

        private readonly ArchiveSet _archiveSet;
        private readonly ResourceUsageCache _gameResourceUsages;
        private readonly TrulyObservableCollection<InstalledMod> _installedMods = [];

        public MainWindow()
        {
            InitializeComponent();
        }

        public MainWindow(string gameFolderPath, CdcGame game)
            : this()
        {
            _archiveSet = ArchiveSet.Open(gameFolderPath, true, true, game);
            _gameResourceUsages = new ResourceUsageCache();

            CdcGameInfo gameInfo = CdcGameInfo.Get(game);
            Version version = Assembly.GetExecutingAssembly().GetName().Version!;
            Title = $"{gameInfo.ShortName} Mod Manager {version.Major}.{version.Minor}.{version.Build}";
            _imgSelectGame.Source = gameInfo.Icon;
        }

        public bool GameSelectionRequested
        {
            get;
            private set;
        }

        protected override async void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            _lbMods.ItemsSource = _installedMods;
            if (Design.IsDesignMode)
            {
                _installedMods.Add(new(1, "Enabled mod", true));
                _installedMods.Add(new(2, "Disabled mod", false));
                return;
            }

            RefreshModList();
            _installedMods.ItemChanged += OnInstalledModChanged;

            await Task.Delay(1);

            if (!_gameResourceUsages.Load(_archiveSet.FolderPath))
            {
                using ArchiveSet gameArchiveSet = ArchiveSet.Open(_archiveSet.FolderPath, true, false, _archiveSet.Game);
                await Task.Run(() => _gameResourceUsages.AddArchiveSet(gameArchiveSet, this, CancellationToken));
                _gameResourceUsages.Save(_archiveSet.FolderPath);
            }

            if (_archiveSet.DuplicateArchives.Any(a => a.ModName != null))
                await ReinstallModsAsync(false);
        }

        private async void OnAddFromZipClicked(object? sender, RoutedEventArgs e)
        {
            string? filePath = await App.OpenFilePickerAsync(
                "Select mod archive to install",
                new()
                {
                    { "Archive", ["*.7z", "*.zip", "*.rar"] }
                }
            );
            if (filePath == null)
                return;

            await InstallModFromZipAsync(filePath);
        }

        private async Task InstallModFromZipAsync(string filePath)
        {
            try
            {
                ModInstaller installer = new(_archiveSet, _gameResourceUsages, this);
                InstalledMod? mod = await Task.Run(
                    () => installer.InstallFromZipAsync(filePath, this, CancellationToken)
                );
                if (mod == null)
                    return;

                await UpdateFlatModArchiveAsync();
                _installedMods.Add(mod);
            }
            catch (Exception ex)
            {
                await MessageBox.ShowErrorAsync(ex);
            }
        }

        private async void OnAddFromFolderClicked(object? sender, RoutedEventArgs e)
        {
            string? folderPath = await App.OpenFolderPickerAsync("Select mod folder to install");
            if (folderPath == null)
                return;

            await InstallModFromFolderAsync(folderPath);
        }

        private async Task InstallModFromFolderAsync(string folderPath)
        {
            try
            {
                ModInstaller installer = new(_archiveSet, _gameResourceUsages, this);
                InstalledMod? mod = await Task.Run(
                    () => installer.InstallFromFolderAsync(folderPath, this, CancellationToken)
                );
                if (mod == null)
                    return;

                await UpdateFlatModArchiveAsync();
                RefreshModList();
            }
            catch (Exception ex)
            {
                await MessageBox.ShowErrorAsync(ex);
            }
        }

        private async void OnInstalledModChanged(InstalledMod mod, PropertyChangedEventArgs e)
        {
            try
            {
                if (mod.Enabled)
                    await Task.Run(() => _archiveSet.Enable(mod.ArchiveId, _gameResourceUsages, this, CancellationToken));
                else
                    await Task.Run(() => _archiveSet.Disable(mod.ArchiveId, _gameResourceUsages, this, CancellationToken));

                await UpdateFlatModArchiveAsync();
            }
            catch (Exception ex)
            {
                await MessageBox.ShowErrorAsync(ex);
            }
            finally
            {
                _archiveSet.CloseStreams();
            }
        }

        private async void OnRemoveClicked(object? sender, RoutedEventArgs e)
        {
            if (_lbMods.Selection.Count == 0)
            {
                await MessageBox.ShowAsync("", "No mods selected to remove.", icon: MsBox.Avalonia.Enums.Icon.Info);
                return;
            }

            if (await MessageBox.ShowAsync(
                    "Confirm",
                    "Are you sure you want to remove the selected mod(s)?",
                    ButtonEnum.YesNo,
                    MsBox.Avalonia.Enums.Icon.Question) == ButtonResult.No)
            {
                return;
            }

            try
            {
                foreach (int index in _lbMods.Selection.SelectedIndexes.OrderByDescending(i => i).ToList())
                {
                    InstalledMod mod = _installedMods[index];
                    await Task.Run(() => _archiveSet.Delete(mod.ArchiveId, _gameResourceUsages, this, CancellationToken));
                    _installedMods.RemoveAt(index);
                }
                await UpdateFlatModArchiveAsync();
            }
            catch (Exception ex)
            {
                await MessageBox.ShowErrorAsync(ex);
            }
            finally
            {
                _archiveSet.CloseStreams();
            }
        }

        private async void OnReinstallClicked(object? sender, RoutedEventArgs e)
        {
            await ReinstallModsAsync(true);
        }

        private async Task ReinstallModsAsync(bool showCompletionMessage)
        {
            ModInstaller installer = new ModInstaller(_archiveSet, _gameResourceUsages, this);
            try
            {
                await Task.Run(() => installer.ReinstallAll(this, CancellationToken));
                await UpdateFlatModArchiveAsync();
                if (showCompletionMessage)
                    await MessageBox.ShowAsync("", "Mod reinstallation complete.", icon: MsBox.Avalonia.Enums.Icon.Info);
            }
            catch (Exception ex)
            {
                await MessageBox.ShowErrorAsync(ex);
            }
            RefreshModList();
        }

        private void OnSelectGameClicked(object? sender, RoutedEventArgs e)
        {
            GameSelectionRequested = true;
            Close();
        }

        private async Task UpdateFlatModArchiveAsync()
        {
            ModInstaller installer = new(_archiveSet, _gameResourceUsages, this);
            await Task.Run(() => installer.UpdateFlatModArchive(this, CancellationToken));
        }

        private void OnDragModOver(object? sender, DragEventArgs e)
        {
            e.DragEffects = GetDroppedPathsIfAllowed(e) != null ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private async void OnDropMod(object? sender, DragEventArgs e)
        {
            List<string>? paths = GetDroppedPathsIfAllowed(e);
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

        private List<string>? GetDroppedPathsIfAllowed(DragEventArgs e)
        {
            if (!_lbMods.IsEnabled)
                return null;

            List<string> paths = [];
            foreach (string path in e.DataTransfer.GetFileSystemPaths())
            {
                if (File.Exists(path))
                {
                    string extension = Path.GetExtension(path);
                    if (extension is ".7z" or ".zip" or ".rar")
                    {
                        paths.Add(path);
                        continue;
                    }
                }

                if (Directory.Exists(path))
                {
                    paths.Add(path);
                    continue;
                }

                return null;
            }
            return paths.Count > 0 ? paths : null;
        }

        private void OnModsListKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
                OnRemoveClicked(sender, new());
        }

        private void RefreshModList()
        {
            _installedMods.Clear();
            _installedMods.AddRange(
                _archiveSet.Archives
                           .Where(a => a.ModName != null && a.SubId == 0)
                           .OrderBy(a => a.MetaData!.Version)
                           .Select(a => new InstalledMod(a.Id, a.ModName!, a.MetaData!.Enabled))
            );
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _archiveSet?.Dispose();
        }
    }
}