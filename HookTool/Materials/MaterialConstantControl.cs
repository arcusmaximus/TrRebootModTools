using System.Drawing;
using System.Windows.Forms;
using TrRebootTools.Shared.Controls;

namespace TrRebootTools.HookTool.Materials
{
    internal partial class MaterialConstantControl : UserControl, IArrayItemControl
    {
        private int _index;
        private MaterialConstant _constant;

        public MaterialConstantControl()
        {
            InitializeComponent();
        }

        public int Index
        {
            get => _index;
            set
            {
                _index = value;
                _lblIndex.Text = value.ToString();
            }
        }

        object IArrayItemControl.DataSource
        {
            get => Constant;
            set => Constant = (MaterialConstant)value;
        }

        public MaterialConstant Constant
        {
            get => _constant;
            set
            {
                if (value == _constant)
                    return;

                if (_constant != null)
                {
                    DataBindings.Clear();
                    _xControl.DataBindings.Clear();
                    _yControl.DataBindings.Clear();
                    _zControl.DataBindings.Clear();
                    _wControl.DataBindings.Clear();
                }

                _constant = value;

                if (_constant != null)
                {
                    Binding backgroundBinding = DataBindings.Add(nameof(BackColor), _constant, nameof(_constant.Dirty), true, DataSourceUpdateMode.Never);
                    backgroundBinding.Format += GetBackgroundColor;
                    backgroundBinding.ReadValue();

                    _xControl.DataBindings.Add(nameof(_xControl.Value), _constant, nameof(_constant.X), false, DataSourceUpdateMode.OnPropertyChanged);
                    _yControl.DataBindings.Add(nameof(_yControl.Value), _constant, nameof(_constant.Y), false, DataSourceUpdateMode.OnPropertyChanged);
                    _zControl.DataBindings.Add(nameof(_zControl.Value), _constant, nameof(_constant.Z), false, DataSourceUpdateMode.OnPropertyChanged);
                    _wControl.DataBindings.Add(nameof(_wControl.Value), _constant, nameof(_constant.W), false, DataSourceUpdateMode.OnPropertyChanged);
                }
                else
                {
                    BackColor = SystemColors.Control;
                    _xControl.Value = 0;
                    _yControl.Value = 0;
                    _zControl.Value = 0;
                    _wControl.Value = 0;
                }
            }
        }

        private void GetBackgroundColor(object sender, ConvertEventArgs e)
        {
            e.Value = (bool)e.Value ? SystemColors.Info : SystemColors.Control;
        }
    }
}
