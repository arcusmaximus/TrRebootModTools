using System;
using System.Windows.Forms;
using System.ComponentModel;

namespace TrRebootTools.Shared.Controls
{
    public partial class ArrayControl : UserControl
    {
        private IBindingList _list;

        public ArrayControl()
        {
            InitializeComponent();
        }

        public IBindingList DataSource
        {
            get => _list;
            set
            {
                if (value == _list)
                    return;

                if (_list != null)
                    _list.ListChanged -= HandleListChanged;

                _list = value;

                if (_list != null)
                    _list.ListChanged += HandleListChanged;

                ResetItemControls();
            }
        }

        private void HandleListChanged(object sender, ListChangedEventArgs e)
        {
            switch (e.ListChangedType)
            {
                case ListChangedType.ItemAdded:
                    InsertItemControl(e.NewIndex);
                    break;

                case ListChangedType.ItemDeleted:
                    RemoveItemControl(e.NewIndex);
                    break;

                case ListChangedType.Reset:
                    ResetItemControls();
                    break;
            }
        }

        private IArrayItemControl InsertItemControl(int index)
        {
            IArrayItemControl item = CreateItemControl();
            item.DataSource = _list[index];

            Control control = (Control)item;
            int height = control.Height;
            control.Dock = DockStyle.Top;
            Controls.Add(control);
            Controls.SetChildIndex(control, Controls.Count - 1 - index);
            control.Height = height;

            UpdateItemControlIndices(index);

            return item;
        }

        private void RemoveItemControl(int index)
        {
            int controlIdx = Controls.Count - 1 - index;
            Control control = Controls[controlIdx];
            Controls.RemoveAt(controlIdx);
            control.Dispose();

            UpdateItemControlIndices(index);
        }

        private void ResetItemControls()
        {
            SuspendLayout();

            int count = _list?.Count ?? 0;

            while (Controls.Count > count)
            {
                RemoveItemControl(Controls.Count - 1);
            }

            for (int i = 0; i < Math.Min(count, Controls.Count); i++)
            {
                ((IArrayItemControl)Controls[Controls.Count - 1 - i]).DataSource = _list[i];
            }

            while (Controls.Count < count)
            {
                InsertItemControl(Controls.Count);
            }

            ResumeLayout();
        }

        private void UpdateItemControlIndices(int startIndex)
        {
            for (int i = startIndex; i < Controls.Count; i++)
            {
                ((IArrayItemControl)Controls[Controls.Count - 1 - i]).Index = i;
            }
        }

        protected virtual IArrayItemControl CreateItemControl()
        {
            throw new NotImplementedException();
        }
    }
}
