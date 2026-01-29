using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;
using TrRebootTools.ModManager.Mod;
using TrRebootTools.Shared;

namespace TrRebootTools.ModManager
{
    internal partial class VariationSelectionWindow : Window
    {
        public static async Task<ModVariation?> ShowDialogAsync(ModPackage package)
        {
            VariationSelectionWindow window = new(package);
            await App.ShowDialogAsync(window);
            return window._selectedVariation;
        }

        private ModVariation? _selectedVariation;

        public VariationSelectionWindow()
        {
            InitializeComponent();
        }

        private VariationSelectionWindow(ModPackage package)
            : this()
        {
            _lblIntro.Content = string.Format((string)_lblIntro.Content!, package.Name);
            _lstVariations.ItemsSource = package.Variations;
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            if (_lstVariations.Items.Count > 0)
                _lstVariations.SelectedIndex = 0;
        }

        private void OnVariationSelected(object? sender, SelectionChangedEventArgs e)
        {
            _selectedVariation = (ModVariation?)_lstVariations.SelectedItem;
            if (_selectedVariation == null)
            {
                _imgVariationImage.Source = null;
                _txtVariationDescription.Text = null;
                _btnOK.IsEnabled = false;
                return;
            }

            _imgVariationImage.Source = _selectedVariation.Image;
            _txtVariationDescription.Text = string.IsNullOrWhiteSpace(_selectedVariation.Description) ? "(No description provided)" : _selectedVariation.Description;
            _splitVariationDetails.SetPanelCollapsed(0, _selectedVariation.Image == null);
            _splitVariationDetails.SetPanelCollapsed(1, string.IsNullOrWhiteSpace(_selectedVariation.Description) && _selectedVariation.Image != null);
            _btnOK.IsEnabled = true;
        }

        private void OnVariationDoubleClicked(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            if (_selectedVariation != null)
                Close();
        }

        private void OnOKClicked(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnCancelClicked(object? sender, RoutedEventArgs e)
        {
            _selectedVariation = null;
            Close();
        }
    }
}
