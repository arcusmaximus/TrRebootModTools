using Avalonia;
using System;
using System.Threading.Tasks;
using TrRebootTools.Shared;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Forms;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Extractor
{
    internal static class Program
    {
        public static AppBuilder BuildAvaloniaApp()
        {
            return App.Build(() => new MainWindow());
        }

        [STAThread]
        public static int Main(string[] args)
        {
            return App.Run(() => MainInternalAsync(args));
        }

        private static async Task<int> MainInternalAsync(string[] args)
        {
            try
            {
                bool forceGamePrompt = false;
                while (true)
                {
                    CdcGame? game = await GameSelectionWindow.GetGameAsync(forceGamePrompt);
                    if (game == null)
                        break;

                    string? gameFolderPath = await GameFolderFinder.FindAsync(game.Value);
                    while (gameFolderPath == null)
                    {
                        game = await GameSelectionWindow.GetGameAsync(true);
                        if (game == null)
                            return 0;

                        gameFolderPath = await GameFolderFinder.FindAsync(game.Value);
                    }

                    MainWindow window = new(gameFolderPath, game.Value);
                    await App.ShowDialogAsync(window);
                    if (!window.GameSelectionRequested)
                        break;

                    forceGamePrompt = true;
                }
                return 0;
            }
            catch (Exception ex)
            {
                await MessageBox.ShowErrorAsync(ex);
                return 1;
            }
        }
    }
}
