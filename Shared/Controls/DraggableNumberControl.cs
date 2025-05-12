using System;
using System.Drawing;
using System.Windows.Forms;

namespace TrRebootTools.Shared.Controls
{
    public partial class DraggableNumberControl : UserControl
    {
        private float _value;

        private bool _dragging;
        private int _dragStartX;
        private float _dragStartValue;

        public DraggableNumberControl()
        {
            InitializeComponent();
        }

        public float Value
        {
            get => _value;
            set
            {
                if (value == _value)
                    return;

                _value = value;
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler ValueChanged;

        private void DraggableNumberControl_Paint(object sender, PaintEventArgs e)
        {
            using Brush fillBrush = new SolidBrush(SystemColors.Highlight);
            using Brush backgroundBrush = new SolidBrush(SystemColors.ControlDark);
            using Brush textBrush = new SolidBrush(SystemColors.HighlightText);

            int filledUpTo = (int)(Width * Math.Min(Math.Max(_value, 0), 1));
            if (filledUpTo > 0)
                e.Graphics.FillRectangle(fillBrush, 0, 0, filledUpTo, Height);

            if (filledUpTo < Width)
                e.Graphics.FillRectangle(backgroundBrush, filledUpTo, 0, Width - filledUpTo, Height);

            e.Graphics.DrawString(
                _value.ToString("0.000"),
                Font,
                textBrush,
                new RectangleF(0, 0, Width, Height),
                new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }
            );
        }

        private void DraggableNumberControl_MouseDown(object sender, MouseEventArgs e)
        {
            _dragging = true;
            _dragStartX = e.Location.X;
            _dragStartValue = _value;
        }

        private void DraggableNumberControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragging)
                Value = _dragStartValue + (float)(e.Location.X - _dragStartX) / Width;
        }

        private void DraggableNumberControl_MouseUp(object sender, MouseEventArgs e)
        {
            _dragging = false;

            if (e.Location.X == _dragStartX && !_txtValue.Visible)
            {
                _txtValue.Visible = true;
                _txtValue.Text = _value.ToString();
                _txtValue.Focus();
                _txtValue.SelectAll();
            }
        }

        private void _txtValue_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Return:
                    Confirm();
                    break;

                case Keys.Escape:
                    Cancel();
                    break;
            }
        }

        private void _txtValue_Leave(object sender, EventArgs e)
        {
            Confirm();
        }

        private void Confirm()
        {
            if (float.TryParse(_txtValue.Text, out float value))
                Value = value;

            _txtValue.Visible = false;
        }

        private void Cancel()
        {
            _txtValue.Visible = false;
        }
    }
}
