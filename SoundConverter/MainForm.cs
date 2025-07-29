using System;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using TrRebootTools.Shared.Cdc;

namespace TrRebootTools.SoundConverter
{
    public partial class MainForm : Form
    {
        private static class AppSettingKeys
        {
            public const string Game = "Game";
            public const string OutputFolder = "OutputFolder";
        }

        private CancellationTokenSource _cancellationTokenSource;

        private readonly ISoundConverter[] _converters = [
            new TrSoundDecoder(CdcGame.Tr2013),
            new TrSoundDecoder(CdcGame.Rise),
            new TrSoundEncoder(CdcGame.Tr2013),
            new TrSoundEncoder(CdcGame.Rise),
            new MulEncoder(CdcGame.Tr2013),
            new MulEncoder(CdcGame.Rise),
            new WwiseEncoder()
        ];

        public MainForm()
        {
            InitializeComponent();
            Font = SystemFonts.MessageBoxFont;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            string gameStr = ConfigurationManager.AppSettings[AppSettingKeys.Game];
            if (Enum.TryParse(gameStr, out CdcGame game))
                SetSelectedGame(game);

            _dlgSelectInputFiles.Filter = string.Join("|", _converters.Select(c => $"{c.InputExtension}|*{c.InputExtension}").Distinct());

            _txtOutputFolder.Text = ConfigurationManager.AppSettings[AppSettingKeys.OutputFolder];
        }

        private void _lstInputFiles_DragEnter(object sender, DragEventArgs e)
        {
            if (GetDroppedItemsIfAllowed(e) != null)
                e.Effect = DragDropEffects.Copy;
        }

        private void _lstInputFiles_DragDrop(object sender, DragEventArgs e)
        {
            string[] inputPaths = GetDroppedItemsIfAllowed(e);
            if (inputPaths == null)
                return;

            _lstInputFiles.BeginUpdate();
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
            _lstInputFiles.EndUpdate();
        }

        private string[] GetDroppedItemsIfAllowed(DragEventArgs e)
        {
            if (!_pnlOptions.Enabled)
                return null;

            if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
                return null;

            foreach (string path in paths)
            {
                if (Directory.Exists(path))
                    continue;

                if (File.Exists(path) && IsInputFileAllowed(path))
                    continue;

                return null;
            }
            return paths;
        }

        private void _btnAddInputFiles_Click(object sender, EventArgs e)
        {
            if (_dlgSelectInputFiles.ShowDialog() != DialogResult.OK)
                return;

            _lstInputFiles.Items.AddRange(_dlgSelectInputFiles.FileNames.Where(IsInputFileAllowed).ToArray());
        }

        private bool IsInputFileAllowed(string path)
        {
            if (Path.GetFileNameWithoutExtension(path).Contains(".channel"))
                return false;

            string extension = Path.GetExtension(path);
            return _converters.Any(c => c.InputExtension.Equals(extension, StringComparison.InvariantCultureIgnoreCase));
        }

        private void _btnRemoveSelectedInputFiles_Click(object sender, EventArgs e)
        {
            foreach (int index in _lstInputFiles.SelectedIndices.Cast<int>().OrderByDescending(i => i).ToList())
            {
                _lstInputFiles.Items.RemoveAt(index);
            }
        }

        private void _btnClearInputFiles_Click(object sender, EventArgs e)
        {
            _lstInputFiles.Items.Clear();
        }

        private void _btnBrowseOutputFolder_Click(object sender, EventArgs e)
        {
            if (_dlgSelectOutputFolder.ShowDialog() != DialogResult.OK)
                return;

            _txtOutputFolder.Text = _dlgSelectOutputFolder.SelectedPath;
        }

        private async void _btnConvert_Click(object sender, EventArgs e)
        {
            if (_lstInputFiles.Items.Count == 0)
            {
                MessageBox.Show("Please select files to convert.", "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(_txtOutputFolder.Text))
            {
                MessageBox.Show("Please specify an output folder.", "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!Directory.Exists(_txtOutputFolder.Text))
            {
                MessageBox.Show("The specified output folder does not exist.", "", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            _cancellationTokenSource = new();

            CdcGame game = GetSelectedGame();
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

                    string inputFileExtension = Path.GetExtension(inputFilePath);
                    ISoundConverter converter = _converters.FirstOrDefault(c => c.InputExtension.Equals(inputFileExtension, StringComparison.InvariantCultureIgnoreCase) && c.Game == game);
                    if (converter == null)
                    {
                        MessageBox.Show($"Don't know what to do for {inputFilePath}\r\nPlease make sure to select the correct game.", "", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        success = false;
                        break;
                    }

                    success &= await converter.ConvertAsync(inputFilePath, outputFolderPath);
                    if (!success)
                        break;

                    _progressBar.PerformStep();
                }

                if (!_cancellationTokenSource.Token.IsCancellationRequested && success)
                    MessageBox.Show("Conversion complete.", "", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            finally
            {
                SetWorking(false);
            }
        }

        private CdcGame GetSelectedGame()
        {
            if (_radTr2013.Checked)
                return CdcGame.Tr2013;

            if (_radRise.Checked)
                return CdcGame.Rise;

            return CdcGame.Shadow;
        }

        private void SetSelectedGame(CdcGame game)
        {
            RadioButton button = game switch
            {
                CdcGame.Tr2013 => _radTr2013,
                CdcGame.Rise => _radRise,
                CdcGame.Shadow => _radShadow
            };
            button.Checked = true;
        }

        private void SetWorking(bool working)
        {
            _pnlOptions.Enabled = !working;
            _btnConvert.Visible = !working;
            _progressBar.Visible = working;
            _btnCancel.Visible = working;
        }

        private void _btnCancel_Click(object sender, EventArgs e)
        {
            _btnCancel.Enabled = false;
            _cancellationTokenSource.Cancel();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings[AppSettingKeys.Game].Value = GetSelectedGame().ToString();
            config.AppSettings.Settings[AppSettingKeys.OutputFolder].Value = _txtOutputFolder.Text;
            config.Save(ConfigurationSaveMode.Modified);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();

            foreach (ISoundConverter converter in _converters)
            {
                converter.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
