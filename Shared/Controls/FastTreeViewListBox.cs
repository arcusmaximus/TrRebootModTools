using Avalonia.Controls;
using System;

namespace TrRebootTools.Shared.Controls
{
    internal class FastTreeViewListBox : ListBox
    {
        private readonly EventHandler _itemExpandedChangedHandler;
        private string? _highlightText;

        public FastTreeViewListBox()
        {
            _itemExpandedChangedHandler = (s, e) => ItemExpandedChanged?.Invoke((FastTreeViewItem)s!);
        }

        protected override Type StyleKeyOverride => typeof(ListBox);

        protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        {
            recycleKey = null;
            return true;
        }

        protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        {
            FastTreeViewItem viewItem = new() { HighlightText = _highlightText };
            viewItem.ExpandedChanged += _itemExpandedChangedHandler;
            return viewItem;
        }

        public delegate void ItemEventDelegate(FastTreeViewItem item);

        public event ItemEventDelegate? ItemExpandedChanged;

        public string? HighlightText
        {
            get => _highlightText;
            set
            {
                if (value == _highlightText)
                    return;

                _highlightText = value;
                foreach (FastTreeViewItem item in GetRealizedContainers())
                {
                    item.HighlightText = value;
                }
            }
        }
    }
}
