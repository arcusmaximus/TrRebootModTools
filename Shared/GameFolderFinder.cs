using System;
using System.Configuration;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;
using TrRebootTools.Shared.Cdc;

namespace TrRebootTools.Shared
{
    public static class GameFolderFinder
    {
        public static string Find(CdcGame game)
        {
            CdcGameInfo gameInfo = CdcGameInfo.Get(game);
            Func<CdcGameInfo, string>[] getters =
            {
                GetGameFolderFromConfiguration,
                GetGameFolderFromRegistry,
                GetGameFolderFromFileBrowser
            };
            foreach (Func<CdcGameInfo, string> getter in getters)
            {
                string gameFolderPath = getter(gameInfo);
                if (string.IsNullOrEmpty(gameFolderPath) || !File.Exists(Path.Combine(gameFolderPath, gameInfo.ExeName)))
                    continue;

                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                config.AppSettings.Settings[gameInfo.Game + "Folder"].Value = gameFolderPath;
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(config.AppSettings.SectionInformation.Name);
                return gameFolderPath;
            }

            return null;
        }

        private static string GetGameFolderFromConfiguration(CdcGameInfo gameInfo)
        {
            return ConfigurationManager.AppSettings[gameInfo.Game + "Folder"];
        }

        private static string GetGameFolderFromRegistry(CdcGameInfo gameInfo)
        {
            using RegistryKey uninstallKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstallKey == null)
                return null;

            foreach (string appName in uninstallKey.GetSubKeyNames())
            {
                using RegistryKey appKey = uninstallKey.OpenSubKey(appName);
                if (appKey?.GetValue("DisplayName") as string == gameInfo.RegistryDisplayName)
                    return appKey.GetValue("InstallLocation") as string;
            }

            return null;
        }

        private static string GetGameFolderFromFileBrowser(CdcGameInfo gameInfo)
        {
            MessageBox.Show(
                $"Could not automatically determine the {gameInfo.ShortName} installation folder. Please select it manually.",
                $"{gameInfo.ShortName} Modding Tools",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );

            while (true)
            {
                using OpenFileDialog dialog = new OpenFileDialog
                {
                    Filter = $"{gameInfo.ExeName}|{gameInfo.ExeName};gamelaunchhelper.exe",
                    FileName = gameInfo.ExeName
                };
                if (dialog.ShowDialog() != DialogResult.OK)
                    return null;

                string folderPath = Path.GetDirectoryName(dialog.FileName);
                if (File.Exists(Path.Combine(folderPath, gameInfo.ExeName)))
                    return folderPath;

                MessageBox.Show($"Could not find {gameInfo.ExeName} in the selected folder.", "Game not found", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }
    }
}
