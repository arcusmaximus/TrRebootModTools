using System.Drawing;
using System.Windows.Forms;

namespace TrRebootTools.Shared.Controls
{
    public partial class FileTreeViewBase : UserControl
    {
        protected static readonly Image FolderImage = Properties.Resources.Folder;
        protected static readonly Image FileImage = Properties.Resources.File;

        public FileTreeViewBase()
        {
            InitializeComponent();
            
        }

        public bool MultiSelect
        {
            get => _tvFiles.Options.Misc.MultiSelect;
            set => _tvFiles.Options.Misc.MultiSelect = value;
        }
    }
}
