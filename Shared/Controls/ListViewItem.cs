using Avalonia.Controls;
using System;

namespace TrRebootTools.Shared.Controls
{
    internal class ListViewItem : ListBoxItem
    {
        private readonly ListView _listView;
        private readonly ListViewRow _row;

        public ListViewItem(ListView listView)
        {
            _listView = listView;
            _row = new(listView, false);
            Content = _row;
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (DataContext is not IListViewEntry entry)
                return;

            for (int i = 0; i < _listView.Columns.Count; i++)
            {
                _row.SetCellText(i, entry[i]);
            }
        }

        protected override Type StyleKeyOverride => typeof(ListBoxItem);
    }
}
