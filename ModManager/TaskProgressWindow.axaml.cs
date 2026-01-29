using Avalonia.Controls;
using Avalonia.Threading;
using System.Threading;
using TrRebootTools.Shared;

namespace TrRebootTools.ModManager
{
    internal partial class TaskProgressWindow : Window, ITaskProgress
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private bool _active;

        public TaskProgressWindow()
        {
            InitializeComponent();
        }

        public void Begin(string statusText)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Invoke(() => Begin(statusText));
                return;
            }
            Title = statusText;
            _progressBar.Value = 0;
            _active = true;
        }

        public void Report(float progress)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Invoke(() => Report(progress));
                return;
            }
            _progressBar.Value = progress;
        }

        public void End()
        {
            _active = false;
        }

        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        private void OnClosing(object? sender, WindowClosingEventArgs e)
        {
            if (!_active)
                return;

            e.Cancel = true;
            Title = "Canceling...";
            _cancellationTokenSource.Cancel();
        }
    }
}
