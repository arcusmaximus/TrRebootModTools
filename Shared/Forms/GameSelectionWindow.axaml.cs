using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;
using TrRebootTools.Shared.Cdc;

namespace TrRebootTools.Shared.Forms
{
    public partial class GameSelectionWindow : Window
    {
        private CdcGame? _selectedGame;

        public GameSelectionWindow()
        {
            InitializeComponent();
        }

        public static async Task<CdcGame?> GetGameAsync(bool forcePrompt)
        {
            Configuration config = Configuration.Load();
            if (forcePrompt || config.SelectedGame == null)
            {
                GameSelectionWindow window = new();
                await App.ShowDialogAsync(window);
                config.SelectedGame = window._selectedGame;
            }

            if (config.SelectedGame != null)
                config.Save();

            return config.SelectedGame;
        }

        private void OnTr2013Selected(object? sender, RoutedEventArgs e)
        {
            _selectedGame = CdcGame.Tr2013;
            Close();
        }

        private void OnRiseSelected(object? sender, RoutedEventArgs e)
        {
            _selectedGame = CdcGame.Rise;
            Close();
        }

        private void OnShadowSelected(object? sender, RoutedEventArgs e)
        {
            _selectedGame = CdcGame.Shadow;
            Close();
        }

        private void OnAvengersSelected(object? sender, RoutedEventArgs e)
        {
            _selectedGame = CdcGame.Avengers;
            Close();
        }
    }
}
