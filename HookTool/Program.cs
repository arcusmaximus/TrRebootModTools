using Avalonia;
using System;
using System.IO;
using System.Threading.Tasks;
using TrRebootTools.Shared;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Forms;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.HookTool
{
    internal class Program
    {
        public static AppBuilder BuildAvaloniaApp()
        {
            return App.Build(() => new MainWindow());
        }

        [STAThread]
        public static void Main(string[] args)
        {
            App.Run(() => MainInternalAsync(args));
        }

        private static async Task MainInternalAsync(string[] args)
        {
            try
            {
                (CdcGame? game, string? gameFolderPath) = await GameSelectionWindow.GetGameAsync(true);
                if (game == null || gameFolderPath == null)
                    return;

                string exePath = Path.Combine(gameFolderPath, CdcGameInfo.Get(game.Value).ExeNames[0]);
                if (!GameProcess.SupportsHooking(exePath, game.Value, out string? unsupportedReason))
                {
                    await MessageBox.ShowAsync("Can't launch game", unsupportedReason, icon: MsBox.Avalonia.Enums.Icon.Error);
                    return;
                }

                MainWindow window = new(gameFolderPath, game.Value);
                await App.ShowDialogAsync(window);
            }
            catch (Exception ex)
            {
                await MessageBox.ShowErrorAsync(ex);
            }
        }
    }
}
