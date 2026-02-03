using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using TrRebootTools.Extractor.Controls;
using TrRebootTools.Shared;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Forms;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Extractor
{
    public partial class MainWindow : WindowWithProgress
    {
        private readonly ArchiveSet _archiveSet;
        private readonly ResourceUsageCache _resourceUsages;

        public MainWindow()
        {
            InitializeComponent();
        }

        public MainWindow(string gameFolderPath, CdcGame game)
            : this()
        {
            CdcGameInfo gameInfo = CdcGameInfo.Get(game);
            Version version = Assembly.GetExecutingAssembly().GetName().Version!;
            Title = $"{gameInfo.ShortName} Extractor {version.Major}.{version.Minor}.{version.Build}";
            _imgSelectGame.Source = gameInfo.Icon;

            _archiveSet = ArchiveSet.Open(gameFolderPath, true, false, game);
            _resourceUsages = new ResourceUsageCache();

            if (OperatingSystem.IsLinux())
            {
                Configuration config = Configuration.Load();
                _txtOutputFolder.Text = config.ExtractionOutputFolder;
            }
            else
            {
                _pnlTargetFolder.IsVisible = false;
            }
        }

        public bool GameSelectionRequested
        {
            get;
            private set;
        }

        protected override async void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            if (Design.IsDesignMode)
                return;

            await Task.Delay(100);

            _tvFiles.Populate(_archiveSet);
            _lblLoading.IsVisible = false;

            if (!_resourceUsages.Load(_archiveSet.FolderPath))
            {
                await Task.Run(() => _resourceUsages.AddArchiveSet(_archiveSet, this, CancellationToken));
                _resourceUsages.Save(_archiveSet.FolderPath);
            }
        }

        private void OnFileSelectionChanged(object? sender, EventArgs e)
        {
            _btnExtract.IsEnabled = _tvFiles.SelectedFiles.Count > 0;
        }

        private async void OnFileDoubleClicked(object? sender, ArchiveFileTreeView.FileEventArgs e)
        {
            if (_btnExtract.IsEnabled)
                await ExtractSelectedFilesAsync();
        }

        private async void OnBrowseOutputFolderClicked(object? sender, RoutedEventArgs e)
        {
            string? folderPath = await App.OpenFolderPickerAsync("Select output folder");
            if (folderPath != null)
                _txtOutputFolder.Text = folderPath;
        }

        private async void OnExtractClicked(object? sender, RoutedEventArgs e)
        {
            await ExtractSelectedFilesAsync();
        }

        private async Task ExtractSelectedFilesAsync()
        {
            try
            {
                string? folderPath = _txtOutputFolder.Text;
                if (string.IsNullOrEmpty(folderPath))
                    folderPath = AppContext.BaseDirectory;

                folderPath = Path.Combine(folderPath, CdcGameInfo.Get(_archiveSet.Game).ShortName);

                Extractor extractor = new(_archiveSet);
                await Task.Run(() => extractor.Extract(folderPath, _tvFiles.SelectedFiles, this, CancellationToken));
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

        private void OnSelectGameClicked(object? sender, RoutedEventArgs e)
        {
            GameSelectionRequested = true;
            Close();
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);
            Configuration config = Configuration.Load();
            config.ExtractionOutputFolder = _txtOutputFolder.Text;
            config.Save();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _archiveSet.Dispose();
        }
    }
}