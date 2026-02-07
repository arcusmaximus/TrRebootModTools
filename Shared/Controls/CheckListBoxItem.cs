using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using System;

namespace TrRebootTools.Shared.Controls
{
    internal class CheckListBoxItem : ListBoxItem
    {
        private readonly CheckBox _checkbox;
        private readonly TextBlock _label;

        private ICheckListBoxEntry? _entry;

        public CheckListBoxItem()
        {
            StackPanel panel = new() { Orientation = Orientation.Horizontal, Spacing = 2 };
            
            _checkbox = new() { VerticalAlignment = VerticalAlignment.Center };
            _checkbox.IsCheckedChanged += OnCheckBoxChanged;
            panel.Children.Add(_checkbox);

            _label = new() { VerticalAlignment = VerticalAlignment.Center };
            panel.Children.Add(_label);

            Content = panel;
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (_entry != null)
                _entry.CheckedChanged -= OnEntryCheckedChanged;

            _entry = DataContext as ICheckListBoxEntry;
            _checkbox.IsChecked = _entry?.Checked ?? false;
            _label.Text = _entry?.Name;
            Opacity = (_entry?.Checked ?? false) ? 1.0 : 0.5;

            if (_entry != null)
                _entry.CheckedChanged += OnEntryCheckedChanged;
        }

        private void OnEntryCheckedChanged(object? sender, EventArgs e)
        {
            if (_entry != null)
            {
                _checkbox.IsChecked = _entry.Checked;
                Opacity = _entry.Checked ? 1.0 : 0.5;
                IsCheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnCheckBoxChanged(object? sender, RoutedEventArgs e)
        {
            if (_entry != null)
                _entry.Checked = _checkbox.IsChecked ?? false;
        }

        public event EventHandler? IsCheckedChanged;

        protected override Type StyleKeyOverride => typeof(ListBoxItem);
    }
}
