using Avalonia.Controls;

namespace TrRebootTools.HookTool.Materials
{
    public partial class MaterialConstantControl : UserControl
    {
        private int _index;

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
    }
}
