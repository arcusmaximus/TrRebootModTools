using Avalonia.Controls;
using Avalonia.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TrRebootTools.Shared.Controls
{
    public class CheckListBox : ListBox
    {
        private bool _suppressEventRaising;

        protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        {
            recycleKey = null;
            return true;
        }

        protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        {
            CheckListBoxItem control = new();
            control.IsCheckedChanged += OnItemCheckedChanged;
            return control;
        }

        private void OnItemCheckedChanged(object? sender, EventArgs e)
        {
            if (_suppressEventRaising)
                return;

            ICheckListBoxEntry? entry = (sender as CheckListBoxItem)?.DataContext as ICheckListBoxEntry;
            if (entry == null)
                return;

            if (Selection.SelectedItems.Contains(entry))
                CheckSelectedItems(entry.Checked);
            else
                ItemsCheckedChanged?.Invoke([entry]);
        }

        public delegate void ItemsCheckedChangedHandler(List<ICheckListBoxEntry> entries);

        public event ItemsCheckedChangedHandler? ItemsCheckedChanged;

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Space && Selection.Count > 0)
            {
                bool newChecked = !((ICheckListBoxEntry)Selection.SelectedItems[0]!).Checked;
                CheckSelectedItems(newChecked);
                return;
            }

            base.OnKeyDown(e);
        }

        private void CheckSelectedItems(bool newChecked)
        {
            try
            {
                _suppressEventRaising = true;
                
                List<ICheckListBoxEntry> entries = Selection.SelectedItems.Cast<ICheckListBoxEntry>().ToList();
                foreach (ICheckListBoxEntry entry in entries)
                {
                    entry.Checked = newChecked;
                }
                ItemsCheckedChanged?.Invoke(entries);
            }
            finally
            {
                _suppressEventRaising = false;
            }
        }

        protected override Type StyleKeyOverride => typeof(ListBox);
    }
}
