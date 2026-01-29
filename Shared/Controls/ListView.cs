using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using System;
using System.Collections.Generic;

namespace TrRebootTools.Shared.Controls
{
    public class ListView : ListBox
    {
        public List<ListViewColumn> Columns { get; set; } = [];

        internal double[] ColumnWidths { get; private set; } = [];

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            ColumnWidths = new double[Columns.Count];

            ListViewRow headerRow = new(this, true);
            for (int i = 0; i < Columns.Count; i++)
            {
                headerRow.SetCellText(i, Columns[i].Name);
            }

            DockPanel dockPanel = new();
            dockPanel.Children.Add(headerRow);
            DockPanel.SetDock(headerRow, Dock.Top);

            Border border = (Border)VisualChildren[0];
            ScrollViewer scrollViewer = (ScrollViewer)border.Child!;
            border.Child = dockPanel;
            dockPanel.Children.Add(scrollViewer);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double flexibleWidth = double.IsFinite(availableSize.Width) ? availableSize.Width : ListViewRow.MinRowWidth; ;
            double starTotal = 0;
            foreach (ListViewColumn column in Columns)
            {
                if (column.Width.IsAbsolute)
                    flexibleWidth -= column.Width.Value;
                else
                    starTotal += column.Width.Value;
            }

            for (int i = 0; i < Columns.Count; i++)
            {
                if (Columns[i].Width.IsAbsolute)
                    ColumnWidths[i] = Columns[i].Width.Value;
                else
                    ColumnWidths[i] = flexibleWidth * Columns[i].Width.Value / starTotal;
            }

            return base.MeasureOverride(availableSize);
        }

        protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        {
            recycleKey = null;
            return true;
        }

        protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        {
            return new ListViewItem(this);
        }

        protected override Type StyleKeyOverride => typeof(ListBox);
    }
}
