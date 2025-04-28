using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace TrRebootTools.Shared.Forms
{
    public partial class FormWithProgress : Form, ITaskProgress
    {
        private bool _closeRequested;

        public FormWithProgress()
        {
            InitializeComponent();
            Font = SystemFonts.MessageBoxFont;
            CancellationTokenSource = new();
        }

        protected CancellationTokenSource CancellationTokenSource
        {
            get;
        }

        protected virtual void EnableUi(bool enable)
        {
        }

        private void FormWithProgress_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_progressBar.Visible)
                return;

            _lblStatus.Text = "Canceling...";
            CancellationTokenSource.Cancel();
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
    }
}
