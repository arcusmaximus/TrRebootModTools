using Avalonia.Controls;
using Avalonia.Metadata;
using Avalonia.Threading;
using System.Collections.Specialized;
using System.Threading;

namespace TrRebootTools.Shared.Forms
{
    public partial class WindowWithProgress : Window, ITaskProgress
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private bool _closeRequested;

        public WindowWithProgress()
        {
            InitializeComponent();
            Content.CollectionChanged += HandleContentChanged;
        }

        [Content]
        public new Avalonia.Controls.Controls Content { get; } = [];

        protected CancellationToken CancellationToken => _cancellationTokenSource.Token;

        private void HandleContentChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (Content.Count != 2)
                return;

            base.Content = Content[0];
            _pnlMain.Children.Add(Content[1]);
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);
            if (!_progressBar.IsVisible)
                return;

            _lblStatus.Text = "Canceling...";
            _cancellationTokenSource.Cancel();
            _closeRequested = true;
            e.Cancel = true;
        }

        void ITaskProgress.Begin(string statusText)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Invoke(() => ((ITaskProgress)this).Begin(statusText));
                return;
            }

            EnableUi(false);
            _lblStatus.Text = statusText;
            _progressBar.Value = 0;
            _progressBar.IsVisible = true;
        }

        void ITaskProgress.Report(float progress)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Invoke(() => ((ITaskProgress)this).Report(progress));
                return;
            }

            _progressBar.Value = progress;
        }

        void ITaskProgress.End()
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Invoke(() => ((ITaskProgress)this).End());
                return;
            }

            EnableUi(true);
            _lblStatus.Text = string.Empty;
            _progressBar.IsVisible = false;
            if (_closeRequested)
                Close();
        }

        private void EnableUi(bool enable)
        {
            Content[1].IsEnabled = enable;
        }
    }
}
