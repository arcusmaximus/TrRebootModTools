using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared
{
    public static class GameFolderFinder
    {
        public static async Task<string?> FindAsync(CdcGame game)
        {
            CdcGameInfo gameInfo = CdcGameInfo.Get(game);
            Func<CdcGameInfo, Task<string?>>[] getters =
            [
                GetGameFolderFromConfigurationAsync,
                GetGameFolderFromWindowsRegistryAsync,
                GetGameFolderFromLinuxSteamAsync,
                GetGameFolderFromFileBrowserAsync
            ];
            foreach (Func<CdcGameInfo, Task<string?>> getter in getters)
            {
                string? gameFolderPath = await getter(gameInfo);
                if (!IsValidGameFolder(gameInfo, gameFolderPath))
                    continue;

                Configuration config = Configuration.Load();
                config.GameFolderPaths[game] = gameFolderPath;
                config.Save();
                return gameFolderPath;
            }

            return null;
        }

        private static async Task<string?> GetGameFolderFromConfigurationAsync(CdcGameInfo gameInfo)
        {
            return Configuration.Load().GameFolderPaths.GetValueOrDefault(gameInfo.Game);
        }

        private static async Task<string?> GetGameFolderFromWindowsRegistryAsync(CdcGameInfo gameInfo)
        {
            if (!OperatingSystem.IsWindows())
                return null;

            using RegistryKey? uninstallKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstallKey == null)
                return null;

            foreach (string appName in uninstallKey.GetSubKeyNames())
            {
                using RegistryKey? appKey = uninstallKey.OpenSubKey(appName);
                if (appKey?.GetValue("DisplayName") as string == gameInfo.RegistryDisplayName)
                    return appKey.GetValue("InstallLocation") as string;
            }

            return null;
        }

        private static async Task<string?> GetGameFolderFromLinuxSteamAsync(CdcGameInfo gameInfo)
        {
            if (!OperatingSystem.IsLinux())
                return null;

            return gameInfo.LinuxSteamFolderPath;
        }

        private static async Task<string?> GetGameFolderFromFileBrowserAsync(CdcGameInfo gameInfo)
        {
            await MessageBox.ShowAsync(
                $"{gameInfo.ShortName} Modding Tools",
                $"Could not automatically determine the {gameInfo.ShortName} installation folder. Please select it manually.",
                icon: MsBox.Avalonia.Enums.Icon.Info
            );

            while (true)
            {
                string? filePath = await App.OpenFilePickerAsync(
                    $"Select {gameInfo.ShortName} game binary",
                    new()
                    {
                        { gameInfo.ShortName, gameInfo.ExeNames }
                    }
                );
                if (filePath == null)
                    return null;

                string? folderPath = Path.GetDirectoryName(filePath);
                if (IsValidGameFolder(gameInfo, folderPath))
                    return folderPath;

                await MessageBox.ShowAsync(
                    "Game not found",
                    $"Could not find {string.Join('/', gameInfo.ExeNames)} in the selected folder.",
                    icon: MsBox.Avalonia.Enums.Icon.Error
                );
            }
        }

        private static bool IsValidGameFolder(CdcGameInfo gameInfo, [NotNullWhen(true)] string? folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                return false;
            
            if (!Directory.Exists(folderPath))
                return false;

            return gameInfo.ExeNames.Any(e => File.Exists(Path.Combine(folderPath, e)));
        }
    }
}
