using System;
using System.IO;
using System.Windows.Forms;
using TrRebootTools.Shared;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Forms;

namespace TrRebootTools.HookTool
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            CdcGame? game = GameSelectionForm.GetGame(true);
            if (game == null)
                return;

            string gameFolderPath = GameFolderFinder.Find(game.Value);
            if (gameFolderPath == null)
                return;

            string exePath = Path.Combine(gameFolderPath, CdcGameInfo.Get(game.Value).ExeName);
            if (!GameProcess.SupportsHooking(exePath, game.Value, out string unsupportedReason))
            {
                MessageBox.Show(unsupportedReason, "Can't launch game", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            using MainForm form = new MainForm(gameFolderPath, game.Value);
            Application.Run(form);
        }
    }
}
