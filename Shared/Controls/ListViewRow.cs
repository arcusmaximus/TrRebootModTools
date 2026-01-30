using Avalonia;
using Avalonia.Controls;
using System;

namespace TrRebootTools.Shared.Controls
{
    internal class ListViewRow : Panel
    {
        public const double MinRowWidth = 500;

        private readonly ListView _listView;
        private readonly TextBlock[] _cells;

        public ListViewRow(ListView listView, bool isHeader)
        {
            _listView = listView;
            IsHeader = isHeader;

            _cells = new TextBlock[listView.Columns.Count];
            for (int i = 0; i < _cells.Length; i++)
            {
                _cells[i] = new TextBlock();
                Children.Add(_cells[i]);
            }
        }

        public bool IsHeader
        {
            get;
        }

        public void SetCellText(int index, string? text)
        {
            _cells[index].Text = text;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double[] columnWidths = _listView.ColumnWidths;
            double height = 0;
            for (int i = 0; i < columnWidths.Length; i++)
            {
                _cells[i].Measure(new Size(columnWidths[i], availableSize.Height));
                height = Math.Max(height, _cells[i].DesiredSize.Height);
            }
            return new Size(
                double.IsFinite(availableSize.Width) ? availableSize.Width : MinRowWidth,
                height
            );
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double[] columnWidths = _listView.ColumnWidths;
            double x = IsHeader ? 6 : 0;
            for (int i = 0; i < _cells.Length; i++)
            {
                _cells[i].Arrange(new(x, 0, columnWidths[i], finalSize.Height));
                x += columnWidths[i];
            }
            return finalSize;
        }
    }
}
