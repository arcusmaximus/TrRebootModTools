using System;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace TrRebootTools.Shared.Controls
{
    public class BindableListView : ListView
    {
        private IBindingList _dataSource;

        private string _checkedMember;
        private PropertyInfo _checkedProperty;

        private string _displayMember;
        private PropertyInfo _displayProperty;

        private string _foreColorMember;
        private PropertyInfo _foreColorProperty;

        private bool _suspendDataSourceChanges;

        public IBindingList DataSource
        {
            get => _dataSource;
            set
            {
                if (value == _dataSource)
                    return;

                if (_dataSource != null)
                    _dataSource.ListChanged -= HandleChange;

                _dataSource = value;

                if (_dataSource != null)
                    _dataSource.ListChanged += HandleChange;

                Reset();
            }
        }

        public string CheckedMember
        {
            get => _checkedMember;
            set
            {
                if (value == _checkedMember)
                    return;

                _checkedMember = value;
                _checkedProperty = null;
                Reset();
            }
        }

        public string DisplayMember
        {
            get => _displayMember;
            set
            {
                if (value == _displayMember)
                    return;

                _displayMember = value;
                _displayProperty = null;
                Reset();
            }
        }

        public string ForeColorMember
        {
            get => _foreColorMember;
            set
            {
                if (value == _foreColorMember)
                    return;

                _foreColorMember = value;
                _foreColorProperty = null;
                Reset();
            }
        }

        private void HandleChange(object sender, ListChangedEventArgs e)
        {
            switch (e.ListChangedType)
            {
                case ListChangedType.ItemAdded:
                    AddItem(_dataSource[e.NewIndex]);
                    break;

                case ListChangedType.ItemChanged:
                    UpdateItem(Items[e.NewIndex], _dataSource[e.NewIndex]);
                    break;

                case ListChangedType.ItemDeleted:
                    Items.RemoveAt(e.NewIndex);
                    break;

                case ListChangedType.Reset:
                    Reset();
                    break;
            }
        }

        protected override void OnItemChecked(ItemCheckedEventArgs e)
        {
            base.OnItemChecked(e);
            if (_checkedProperty != null && !_suspendDataSourceChanges)
                _checkedProperty.SetValue(_dataSource[e.Item.Index], e.Item.Checked);
        }

        private void Reset()
        {
            BeginUpdate();

            Items.Clear();
            if (_dataSource != null)
            {
                ResolveMembers();
                foreach (object item in _dataSource)
                {
                    AddItem(item);
                }
            }

            EndUpdate();
        }

        private void AddItem(object dataSourceItem)
        {
            try
            {
                _suspendDataSourceChanges = true;

                ListViewItem listViewItem = Items.Add(string.Empty);
                UpdateItem(listViewItem, dataSourceItem);
            }
            finally
            {
                _suspendDataSourceChanges = false;
            }
        }

        private void UpdateItem(ListViewItem listViewItem, object dataSourceItem)
        {
            if (_checkedProperty != null)
                listViewItem.Checked = Convert.ToBoolean(_checkedProperty.GetValue(dataSourceItem));

            if (_displayProperty != null)
                listViewItem.Text = Convert.ToString(_displayProperty.GetValue(dataSourceItem));
            else
                listViewItem.Text = dataSourceItem.ToString();

            if (_foreColorProperty != null && _foreColorProperty.GetValue(dataSourceItem) is Color color)
                listViewItem.ForeColor = color;
        }

        private void ResolveMembers()
        {
            Type itemType = ListBindingHelper.GetListItemType(_dataSource);

            if (_checkedProperty == null && _checkedMember != null)
                _checkedProperty = itemType.GetProperty(_checkedMember);

            if (_displayProperty == null && _displayMember != null)
                _displayProperty = itemType.GetProperty(_displayMember);

            if (_foreColorProperty == null && _foreColorMember != null)
                _foreColorProperty = itemType.GetProperty(_foreColorMember);
        }
    }
}
