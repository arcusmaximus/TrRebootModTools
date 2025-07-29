using System;
using System.Windows.Forms;
using TrRebootTools.Shared;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Forms;

namespace TrRebootTools.Extractor
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                bool forceGamePrompt = false;
                while (true)
                {
                    CdcGame? game = GameSelectionForm.GetGame(forceGamePrompt);
                    if (game == null)
                        break;

                    string gameFolderPath = GameFolderFinder.Find(game.Value);
                    if (gameFolderPath == null)
                        break;

                    using MainForm form = new MainForm(gameFolderPath, game.Value);
                    Application.Run(form);
                    if (!form.GameSelectionRequested)
                        break;

                    forceGamePrompt = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }
    }
}
