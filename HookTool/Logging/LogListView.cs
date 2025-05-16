using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using TrRebootTools.Shared.Controls.VirtualTreeView;

namespace TrRebootTools.HookTool.Logging
{
    internal class LogListView : VirtualTreeView
    {
        private readonly Timer _timer = new Timer(2000) { AutoReset = false };
        private readonly List<object> _items = new();
        private string _filter;
        private bool _filterChanged;

        public LogListView()
        {
            _timer.Elapsed += OnTimerElapsed;
        }

        public string Filter
        {
            get => _filter;
            set
            {
                if (value == _filter)
                    return;

                _filter = !string.IsNullOrWhiteSpace(value) ? value : null;
                _filterChanged = true;
                _timer.Stop();
                BeginUpdate();
                _timer.Start();
            }
        }

        public void AddItem(object item)
        {
            for (int i = Math.Max(_items.Count - 10, 0); i < _items.Count; i++)
            {
                if (item.Equals(_items[i]))
                    return;
            }

            _items.Add(item);

            _timer.Stop();
            BeginUpdate();
            AddNodeIfMatchingFilter(item);
            _timer.Start();
        }

        public new void Clear()
        {
            base.Clear();
            _items.Clear();
        }

        private async void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(() => OnTimerElapsed(sender, e));
                return;
            }

            if (_filterChanged)
            {
                base.Clear();
                foreach (object item in _items)
                {
                    AddNodeIfMatchingFilter(item);
                }
                _filterChanged = false;
            }

            EndUpdate();
            await Task.Delay(100);
            ScrollToBottom();
        }

        private void AddNodeIfMatchingFilter(object item)
        {
            if (_filter == null || item.ToString().IndexOf(_filter, StringComparison.InvariantCultureIgnoreCase) >= 0)
                InsertNode(null, NodeAttachMode.amAddChildLast, item);
        }
    }
}
