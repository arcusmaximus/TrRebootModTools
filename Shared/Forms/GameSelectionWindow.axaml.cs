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

        public static async Task<(CdcGame? Game, string? FolderPath)> GetGameAsync(bool forcePrompt)
        {
            Configuration config = Configuration.Load();
            string? folderPath;
            if (config.SelectedGame != null && !forcePrompt)
            {
                folderPath = await GameFolderFinder.FindAsync(config.SelectedGame.Value, false);
                if (folderPath != null)
                    return (config.SelectedGame, folderPath);
            }

            while (true)
            {
                GameSelectionWindow window = new();
                await App.ShowDialogAsync(window);
                if (window._selectedGame == null)
                    return (null, null);

                folderPath = await GameFolderFinder.FindAsync(window._selectedGame.Value, true);
                if (folderPath == null)
                    continue;

                config = Configuration.Load();
                config.SelectedGame = window._selectedGame;
                config.Save();
                return (config.SelectedGame, folderPath);
            }
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
