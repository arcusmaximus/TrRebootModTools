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
            CdcGame? game = config.SelectedGame;
            string? folderPath;
            if (game != null && !forcePrompt)
            {
                folderPath = await GameFolderFinder.FindAsync(game.Value, false);
                if (folderPath != null)
                    return (game, folderPath);
            }

            while (true)
            {
                GameSelectionWindow window = new();
                await App.ShowDialogAsync(window);
                game = window._selectedGame;
                if (game == null)
                    return (null, null);

                folderPath = await GameFolderFinder.FindAsync(game.Value, true);
                if (folderPath == null)
                    continue;

                config = Configuration.Load();
                config.SelectedGame = game;
                config.Save();
                return (game, folderPath);
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
