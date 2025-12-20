using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Controls;
using TrRebootTools.Shared.Forms;

namespace TrRebootTools.Extractor
{
    public partial class MainForm : FormWithProgress
    {
        private readonly ArchiveSet _archiveSet;
        private readonly ResourceUsageCache _resourceUsages;

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
            _btnSwitchGame.BackgroundImage = gameInfo.Icon;

            _archiveSet = ArchiveSet.Open(gameFolderPath, true, false, game);
            _resourceUsages = new ResourceUsageCache();
            _tvFiles.FileDoubleClicked += _tvFiles_FileDoubleClicked;
        }

        public bool GameSelectionRequested
        {
            get;
            private set;
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            await Task.Delay(100);
            _tvFiles.Populate(_archiveSet);
            _lblLoading.Visible = false;

            if (!_resourceUsages.Load(_archiveSet.FolderPath))
            {
                await Task.Run(() => _resourceUsages.AddArchiveSet(_archiveSet, this, CancellationTokenSource.Token));
                _resourceUsages.Save(_archiveSet.FolderPath);
            }
        }

        private void _tvFiles_SelectionChanged(object sender, EventArgs e)
        {
            _btnExtract.Enabled = _tvFiles.SelectedFiles.Count > 0;
        }

        private void _tvFiles_FileDoubleClicked(object sender, FileTreeView<ArchiveFileReference>.FileEventArgs e)
        {
            if (_btnExtract.Enabled)
                _btnExtract_Click(_btnExtract, EventArgs.Empty);
        }

        private async void _btnExtract_Click(object sender, EventArgs e)
        {
            try
            {
                string folderPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), CdcGameInfo.Get(_archiveSet.Game).ShortName);
                Extractor extractor = new Extractor(_archiveSet);
                await Task.Run(() => extractor.Extract(folderPath, _tvFiles.SelectedFiles, this, CancellationTokenSource.Token));
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

        private void _btnSwitchGame_Click(object sender, EventArgs e)
        {
            GameSelectionRequested = true;
            Close();
        }

        protected override void EnableUi(bool enable)
        {
            _tvFiles.Enabled = enable;
            _btnExtract.Enabled = enable && _tvFiles.SelectedFiles.Count > 0;
            _btnSwitchGame.Enabled = enable;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                components?.Dispose();

            _archiveSet.Dispose();

            base.Dispose(disposing);
        }
    }
}
