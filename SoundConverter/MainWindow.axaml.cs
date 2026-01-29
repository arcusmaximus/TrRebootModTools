using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using TrRebootTools.Shared;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.SoundConverter
{
    public partial class MainWindow : Window
    {
        private static class ExtraSettingKeys
        {
            public const string OutputFolder = "OutputFolder";
        }

        private CancellationTokenSource? _cancellationTokenSource;
        private bool _closeRequested;

        private readonly ISoundConverter[] _converters = [
            new TrSoundDecoder(CdcGame.Tr2013),
            new TrSoundDecoder(CdcGame.Rise),
            new TrSoundEncoder(CdcGame.Tr2013),
            new TrSoundEncoder(CdcGame.Rise),
            new MulEncoder(CdcGame.Tr2013),
            new MulEncoder(CdcGame.Rise),
            new WwiseDecoder(),
            new WwiseEncoder()
        ];

        public MainWindow()
        {
            InitializeComponent();
            Version version = Assembly.GetExecutingAssembly().GetName().Version!;
            Title = $"TR Reboot Sound Converter {version.Major}.{version.Minor}.{version.Build}";
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            Configuration config = Configuration.Load();
            SelectedGame = config.SelectedGame ?? CdcGame.Tr2013;
            _txtOutputFolder.Text = config.ExtraSettings.GetValueOrDefault(ExtraSettingKeys.OutputFolder);
        }

        private async void OnAddInputFilesClick(object? sender, RoutedEventArgs e)
        {
            CdcGame game = SelectedGame;
            List<string> filePaths = await App.OpenFilesPickerAsync(
                "Select files to add",
                _converters.Where(c => c.Game == game)
                           .ToDictionary(
                                c => c.InputExtension,
                                c => new string[] { "*" + c.InputExtension }
                            ),
                this
            );
            foreach (string filePath in filePaths.Where(IsInputFileAllowed))
            {
                _lstInputFiles.Items.Add(filePath);
            }
        }

        private void OnDragFilesOver(object? sender, DragEventArgs e)
        {
            e.DragEffects = GetDroppedFilesIfAllowed(e) != null ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void OnDropFiles(object? sender, DragEventArgs e)
        {
            List<string>? inputPaths = GetDroppedFilesIfAllowed(e);
            if (inputPaths == null)
                return;

            foreach (string inputPath in inputPaths)
            {
                if (Directory.Exists(inputPath))
                {
                    foreach (string filePath in Directory.EnumerateFiles(inputPath, "*", SearchOption.AllDirectories))
                    {
                        if (IsInputFileAllowed(filePath))
                            _lstInputFiles.Items.Add(filePath);
                    }
                }
                else
                {
                    _lstInputFiles.Items.Add(inputPath);
                }
            }
        }

        private List<string>? GetDroppedFilesIfAllowed(DragEventArgs e)
        {
            if (!_grdOptions.IsEnabled)
                return null;

            List<string> paths = [];
            foreach (string path in e.DataTransfer.GetFileSystemPaths())
            {
                if (!Directory.Exists(path) && (!File.Exists(path) || !IsInputFileAllowed(path)))
                    return null;

                paths.Add(path);
            }
            return paths;
        }

        private bool IsInputFileAllowed(string path)
        {
            if (Path.GetFileNameWithoutExtension(path).Contains(".channel"))
                return false;

            CdcGame game = SelectedGame;
            string extension = Path.GetExtension(path);
            return _converters.Any(c => c.Game == game && c.InputExtension.Equals(extension, StringComparison.InvariantCultureIgnoreCase));
        }

        private void OnRemoveSelectedInputFilesClick(object? sender, RoutedEventArgs e)
        {
            foreach (int index in _lstInputFiles.Selection.SelectedIndexes.OrderByDescending(i => i).ToList())
            {
                _lstInputFiles.Items.RemoveAt(index);
            }
        }

        private void OnClearInputFilesClick(object? sender, RoutedEventArgs e)
        {
            _lstInputFiles.Items.Clear();
        }

        private async void OnBrowseOutputFolderClick(object? sender, RoutedEventArgs e)
        {
            string? folderPath = await App.OpenFolderPickerAsync("Select output folder", this);
            if (folderPath != null)
                _txtOutputFolder.Text = folderPath;
        }

        private async void OnConvertClick(object? sender, RoutedEventArgs e)
        {
            if (_lstInputFiles.Items.Count == 0)
            {
                await MessageBox.ShowAsync("", "Please select files to convert.", icon: MsBox.Avalonia.Enums.Icon.Info);
                return;
            }

            if (string.IsNullOrWhiteSpace(_txtOutputFolder.Text))
            {
                await MessageBox.ShowAsync("", "Please specify an output folder.", icon: MsBox.Avalonia.Enums.Icon.Info);
                return;
            }

            if (!Directory.Exists(_txtOutputFolder.Text))
            {
                await MessageBox.ShowAsync("", "The specified output folder does not exist.", icon: MsBox.Avalonia.Enums.Icon.Error);
                return;
            }

            _cancellationTokenSource = new();

            CdcGame game = SelectedGame;
            string outputFolderPath = _txtOutputFolder.Text;

            try
            {
                _progressBar.Value = 0;
                _progressBar.Maximum = _lstInputFiles.Items.Count;

                SetWorking(true);

                bool success = true;
                foreach (string inputFilePath in _lstInputFiles.Items)
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                        break;

                    string? inputFileExtension = Path.GetExtension(inputFilePath);
                    ISoundConverter? converter = _converters.FirstOrDefault(c => c.InputExtension.Equals(inputFileExtension, StringComparison.InvariantCultureIgnoreCase) && c.Game == game);
                    if (converter == null)
                    {
                        await MessageBox.ShowAsync(
                            "",
                            $"Don't know what to do for {inputFilePath}\r\nPlease make sure to select the correct game.",
                            icon: MsBox.Avalonia.Enums.Icon.Warning
                        );
                        success = false;
                        break;
                    }

                    success &= await converter.ConvertAsync(inputFilePath, outputFolderPath);
                    if (!success)
                        break;

                    _progressBar.Value += 1;
                }

                if (!_cancellationTokenSource.Token.IsCancellationRequested && success)
                    await MessageBox.ShowAsync("", "Conversion complete.", icon: MsBox.Avalonia.Enums.Icon.Info);
            }
            catch (Exception ex)
            {
                await MessageBox.ShowErrorAsync(ex);
            }
            finally
            {
                SetWorking(false);
            }

            _cancellationTokenSource = null;
            if (_closeRequested)
                Close();
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            _btnCancel.IsEnabled = false;
            _cancellationTokenSource?.Cancel();
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);
            if (_cancellationTokenSource != null)
            {
                _btnCancel.IsEnabled = false;
                _cancellationTokenSource.Cancel();
                e.Cancel = true;
                _closeRequested = true;
                return;
            }

            foreach (ISoundConverter converter in _converters)
            {
                converter.Dispose();
            }

            Configuration config = Configuration.Load();
            config.SelectedGame = SelectedGame;
            config.ExtraSettings[ExtraSettingKeys.OutputFolder] = _txtOutputFolder.Text ?? string.Empty;
            config.Save();
        }

        private CdcGame SelectedGame
        {
            get
            {
                if (_radTr2013.IsChecked == true)
                    return CdcGame.Tr2013;

                if (_radRise.IsChecked == true)
                    return CdcGame.Rise;

                return CdcGame.Shadow;
            }
            set
            {
                RadioButton button = value switch
                {
                    CdcGame.Tr2013 => _radTr2013,
                    CdcGame.Rise => _radRise,
                    CdcGame.Shadow => _radShadow
                };
                button.IsChecked = true;
            }
        }

        private void SetWorking(bool working)
        {
            _grdOptions.IsEnabled = !working;
            _btnConvert.IsVisible = !working;
            _grdProgress.IsVisible = working;
        }
    }
}