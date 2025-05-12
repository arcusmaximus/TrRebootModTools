using TrRebootTools.Shared.Controls;

namespace TrRebootTools.HookTool.Materials
{
    public partial class MaterialConstantArrayControl : ArrayControl
    {
        public MaterialConstantArrayControl()
        {
            InitializeComponent();
        }

        protected override IArrayItemControl CreateItemControl()
        {
            return new MaterialConstantControl();
        }
    }
}
