using System;
using System.Windows.Forms;
using TrRebootTools.ModManager.Mod;

namespace TrRebootTools.ModManager
{
    internal partial class VariationSelectionForm : Form
    {
        public VariationSelectionForm()
        {
            InitializeComponent();
            DialogResult = DialogResult.Cancel;
        }

        public VariationSelectionForm(ModPackage package)
            : this()
        {
            _lblIntro.Text = $"The mod \"{package.Name}\" contains multiple variations. Please select the one you'd like to install.";
            _lstVariation.DataSource = package.Variations;
            _lstVariation.DisplayMember = nameof(ModVariation.Name);

            if (_lstVariation.Items.Count > 0)
                _lstVariation.SelectedIndex = 0;
        }

        private void _lstVariation_SelectedIndexChanged(object sender, EventArgs e)
        {
            SelectedVariation = (ModVariation)_lstVariation.SelectedItem;
            if (SelectedVariation == null)
            {
                _btnOK.Enabled = false;
                return;
            }

            _pbPreview.Image = SelectedVariation.Image;
            _txtDescription.Text = SelectedVariation.Description;

            _spltScreenshot.Panel1Collapsed = false;
            _spltScreenshot.Panel2Collapsed = false;
            if (SelectedVariation.Image == null)
            {
                _spltScreenshot.Panel1Collapsed = true;
                if (string.IsNullOrWhiteSpace(SelectedVariation.Description))
                    _txtDescription.Text = "(No description provided)";
            }
            else if (string.IsNullOrEmpty(SelectedVariation.Description))
            {
                _spltScreenshot.Panel2Collapsed = true;
            }

            _btnOK.Enabled = true;
        }

        private void _lstVariation_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (_btnOK.Enabled)
                _btnOK_Click(_btnOK, EventArgs.Empty);
        }

        public ModVariation SelectedVariation
        {
            get;
            private set;
        }

        private void _btnOK_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void _btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
