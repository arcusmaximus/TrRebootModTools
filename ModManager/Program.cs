using Avalonia;
using Avalonia.Interactivity;
using System;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using TrRebootTools.ModManager.Mod;
using TrRebootTools.Shared;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Forms;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.ModManager
{
    public static class Program
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
                    CdcGame? game = ShiftGameFromCommandLine(ref args) ?? await GameSelectionWindow.GetGameAsync(forceGamePrompt);
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

                    if (args.Length > 0)
                    {
                        bool success = await HandleCommandLineAsync(args, gameFolderPath, game.Value);
                        return success ? 0 : 1;
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

        private static CdcGame? ShiftGameFromCommandLine(ref string[] args)
        {
            if (args.Length == 0 || !Enum.TryParse(args[0], out CdcGame game))
                return null;

            string[] newArgs = new string[args.Length - 1];
            Array.Copy(args, 1, newArgs, 0, args.Length - 1);
            args = newArgs;
            return game;
        }

        private static async Task<bool> HandleCommandLineAsync(string[] args, string gameFolderPath, CdcGame game)
        {
            using ArchiveSet archiveSet = ArchiveSet.Open(gameFolderPath, true, true, game);
            ResourceUsageCache resourceUsageCache = new();

            try
            {
                bool reinstallMods = false;

                if (!resourceUsageCache.Load(gameFolderPath))
                {
                    using ArchiveSet gameArchiveSet = ArchiveSet.Open(gameFolderPath, true, false, game);
                    await RunTaskWithProgressAsync((progress, cancellationToken) => resourceUsageCache.AddArchiveSet(archiveSet, progress, cancellationToken));
                    resourceUsageCache.Save(gameFolderPath);
                    reinstallMods = true;
                }

                if (reinstallMods)
                {
                    ModInstaller installer = new(archiveSet, resourceUsageCache);
                    await RunTaskWithProgressAsync(installer.ReinstallAll);
                }

                await HandleCommandLineAsync(args, archiveSet, resourceUsageCache, game);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                await MessageBox.ShowAsync("Error", ex.Message, icon: MsBox.Avalonia.Enums.Icon.Error);
                return false;
            }
        }

        private static async Task HandleCommandLineAsync(string[] args, ArchiveSet archiveSet, ResourceUsageCache resourceUsageCache, CdcGame game)
        {
            if (args.Length == 1 && (File.Exists(args[0]) || Directory.Exists(args[0])))
            {
                string modPath = args[0];
                await InstallModAsync(archiveSet, resourceUsageCache, modPath);
            }
            else if (args.Length == 2)
            {
                string action = args[0];
                string modName = args[1];
                int? archiveId = archiveSet.Archives.FirstOrDefault(a => a.ModName == modName)?.Id;
                if (archiveId == null)
                    throw new Exception($"No mod found with name {modName}");

                switch (action)
                {
                    case "enable":
                        await RunTaskWithProgressAsync((progress, cancellationToken) => archiveSet.Enable(archiveId.Value, resourceUsageCache, progress, cancellationToken));
                        break;
                    case "disable":
                        await RunTaskWithProgressAsync((progress, cancellationToken) => archiveSet.Disable(archiveId.Value, resourceUsageCache, progress, cancellationToken));
                        break;
                    case "uninstall":
                        await RunTaskWithProgressAsync(
                            (progress, cancellationToken) =>
                            {
                                archiveSet.Disable(archiveId.Value, resourceUsageCache, progress, cancellationToken);
                                archiveSet.Delete(archiveId.Value, resourceUsageCache, progress, cancellationToken);
                            }
                        );
                        break;
                }
            }
            else
            {
                throw new Exception("Invalid command line");
            }

            if (archiveSet.GetActiveFlattenedModArchiveIdentity() != null)
            {
                ModInstaller installer = new(archiveSet, resourceUsageCache);
                await RunTaskWithProgressAsync(installer.UpdateFlatModArchive);
            }
        }

        private static async Task InstallModAsync(ArchiveSet archiveSet, ResourceUsageCache resourceUsageCache, string modPath)
        {
            ModInstaller installer = new(archiveSet, resourceUsageCache);
            if (File.Exists(modPath))
            {
                string extension = Path.GetExtension(modPath);
                if (extension != ".7z" && extension != ".zip" && extension != ".rar")
                {
                    await MessageBox.ShowAsync(
                        "File not supported",
                        "Only .zip and .7z files are supported for direct mod installation. Please extract the archive and install the folder instead.",
                        icon: MsBox.Avalonia.Enums.Icon.Error
                    );
                    return;
                }
                await RunTaskWithProgressAsync((progress, cancellationToken) => installer.InstallFromZipAsync(modPath, progress, cancellationToken));
            }
            else if (Directory.Exists(modPath))
            {
                await RunTaskWithProgressAsync((progress, cancellationToken) => installer.InstallFromFolderAsync(modPath, progress, cancellationToken));
            }
            else
            {
                await MessageBox.ShowAsync("Error", "The specified mod path does not exist.", icon: MsBox.Avalonia.Enums.Icon.Error);
            }
        }

        private static async Task RunTaskWithProgressAsync(Action<ITaskProgress, CancellationToken> action)
        {
            await RunTaskWithProgressAsync(
                (progress, cancellationToken) =>
                {
                    action(progress, cancellationToken);
                    return Task.CompletedTask;
                }
            );
        }

        private static async Task RunTaskWithProgressAsync(Func<ITaskProgress, CancellationToken, Task> action)
        {
            ExceptionDispatchInfo? exception = null;

            TaskProgressWindow progressWindow = new();
            progressWindow.Loaded += OnProgressWindowLoaded;
            await App.ShowDialogAsync(progressWindow);
            
            exception?.Throw();
            return;

            async void OnProgressWindowLoaded(object? sender, RoutedEventArgs e)
            {
                try
                {
                    await Task.Run(
                        () => action(progressWindow, progressWindow.CancellationToken),
                        progressWindow.CancellationToken
                    );
                }
                catch (Exception ex)
                {
                    exception = ExceptionDispatchInfo.Capture(ex);
                }
                progressWindow.Close();
            }
        }
    }
}
