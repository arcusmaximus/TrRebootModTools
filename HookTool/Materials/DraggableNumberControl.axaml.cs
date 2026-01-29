using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using System;

namespace TrRebootTools.HookTool.Materials
{
    public partial class DraggableNumberControl : UserControl
    {
        private float _value;

        private bool _dragging;
        private double _dragStartX;
        private float _dragStartValue;

        public DraggableNumberControl()
        {
            InitializeComponent();
        }

        public static readonly DirectProperty<DraggableNumberControl, float> ValueProperty =
            AvaloniaProperty.RegisterDirect<DraggableNumberControl, float>(nameof(Value), c => c.Value, (c, v) => c.Value = v);

        public float Value
        {
            get => _value;
            set
            {
                if (value == _value)
                    return;

                float oldValue = _value;
                _value = value;
                _prgValue.Value = Math.Min(Math.Max(_value, 0), 1);
                _lblValue.Content = _value.ToString("0.000");
                RaisePropertyChanged(ValueProperty, oldValue, value);
            }
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _dragging = true;
            _dragStartX = e.GetPosition(null).X;
            _dragStartValue = _value;
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_dragging)
                Value = _dragStartValue + (float)((e.GetPosition(null).X - _dragStartX) / DesiredSize.Width);
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            _dragging = false;

            if ((int)e.GetPosition(null).X == (int)_dragStartX && !_txtValue.IsVisible)
            {
                _txtValue.IsVisible = true;
                _txtValue.Text = _value.ToString();
                _txtValue.Focus();
                _txtValue.SelectAll();
            }
        }

        private void OnTextBoxKeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Return:
                    Confirm();
                    break;

                case Key.Escape:
                    Cancel();
                    break;
            }
        }

        private void OnTextBoxLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Confirm();
        }

        private void Confirm()
        {
            if (float.TryParse(_txtValue.Text, out float value))
                Value = value;

            _txtValue.IsVisible = false;
        }

        private void Cancel()
        {
            _txtValue.IsVisible = false;
        }
    }
}
