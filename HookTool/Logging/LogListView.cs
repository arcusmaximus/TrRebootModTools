using System;
using System.Collections.Generic;
using TrRebootTools.Shared.Controls;

namespace TrRebootTools.HookTool.Logging
{
    internal class LogListView : ListView
    {
        private readonly List<IListViewEntry> _entries = [];
        private string? _filter;

        public void AddEntry(IListViewEntry entry)
        {
            for (int i = Math.Max(_entries.Count - 30, 0); i < _entries.Count; i++)
            {
                if (entry.Equals(_entries[i]))
                    return;
            }

            _entries.Add(entry);
            AddItemIfMatchingFilter(entry);
            ScrollToBottom();
        }

        public void Clear()
        {
            _entries.Clear();
            Items.Clear();
        }

        public string? Filter
        {
            get => _filter;
            set
            {
                if (value == _filter)
                    return;

                _filter = !string.IsNullOrWhiteSpace(value) ? value : null;
                Items.Clear();
                foreach (IListViewEntry entry in _entries)
                {
                    AddItemIfMatchingFilter(entry);
                }
                ScrollToBottom();
            }
        }

        private void AddItemIfMatchingFilter(IListViewEntry entry)
        {
            if (_filter == null || (entry.ToString() ?? string.Empty).IndexOf(_filter, StringComparison.InvariantCultureIgnoreCase) >= 0)
                Items.Add(entry);
        }

        private void ScrollToBottom()
        {
            if (Items.Count > 0)
                ScrollIntoView(Items.Count - 1);
        }
    }
}
