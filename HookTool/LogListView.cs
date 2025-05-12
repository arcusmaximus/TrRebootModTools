using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using TrRebootTools.Shared.Controls.VirtualTreeView;

namespace TrRebootTools.HookTool
{
    internal class LogListView : VirtualTreeView
    {
        private readonly Timer _timer = new Timer(2000);
        private readonly object[] _recentItems = new object[10];
        private int _recentItemIdx;

        public LogListView()
        {
            _timer.Elapsed += OnTimerElapsed;
        }

        public void AppendNode(object data)
        {
            if (_recentItems.Contains(data))
                return;

            _recentItems[_recentItemIdx] = data;
            _recentItemIdx = (_recentItemIdx + 1) % _recentItems.Length;

            _timer.Stop();
            BeginUpdate();
            InsertNode(null, NodeAttachMode.amAddChildLast, data);
            _timer.Start();
        }

        private async void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(() => OnTimerElapsed(sender, e));
                return;
            }

            EndUpdate();
            _timer.Stop();
            await Task.Delay(100);
            ScrollToBottom();
        }
    }
}
