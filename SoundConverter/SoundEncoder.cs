using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using TrRebootTools.Shared;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.SoundConverter
{
    internal abstract class SoundEncoder : ISoundConverter
    {
        private string? _consoleToolPath;

        public abstract CdcGame Game { get; }
        public abstract string InputExtension { get; }

        public async Task<bool> ConvertAsync(string inputFilePath, string outputFolderPath)
        {
            if (!await InitAsync())
                return false;

            string subFolderPath = "";
            int gamePathIndex = inputFilePath.IndexOf(Path.DirectorySeparatorChar + "pc-w" + Path.DirectorySeparatorChar);
            if (gamePathIndex < 0)
                gamePathIndex = inputFilePath.IndexOf(Path.DirectorySeparatorChar + "pcx64-w" + Path.DirectorySeparatorChar);

            if (gamePathIndex >= 0)
                subFolderPath = Path.GetDirectoryName(inputFilePath.Substring(gamePathIndex + 1))!;
            else if (CdcGameInfo.Get(Game).LanguageCodeToLocale(Path.GetFileNameWithoutExtension(inputFilePath)) != null)
                subFolderPath = Path.GetFileName(Path.GetDirectoryName(inputFilePath))!;

            string? tempOutputFilePath = await ConvertInternalAsync(inputFilePath);
            if (tempOutputFilePath == null || !File.Exists(tempOutputFilePath))
                return false;

            string outputExtension = Path.GetExtension(tempOutputFilePath);
            string outputFileName = Path.GetFileNameWithoutExtension(inputFilePath) + outputExtension;
            string outputFilePath = Path.Combine(outputFolderPath, subFolderPath, outputFileName);
            if (outputFilePath != tempOutputFilePath)
            {
                if (File.Exists(outputFilePath))
                    File.Delete(outputFilePath);

                Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);
                File.Move(tempOutputFilePath, outputFilePath);
            }
            return true;
        }

        protected abstract Task<string?> ConvertInternalAsync(string inputFilePath);

        protected async Task<bool> InitAsync()
        {
            _consoleToolPath ??= await GetConsoleToolPathAsync();
            if (_consoleToolPath == null)
                return false;

            if (ProjectFilePath == null)
            {
                string projectName = GetType().Name + "Project";
                string projectFolderPath = Path.Combine(AppContext.BaseDirectory, projectName);
                if (Directory.Exists(projectFolderPath))
                    Directory.Delete(projectFolderPath, true);

                string projectFilePath = Path.Combine(projectFolderPath, projectName + ProjectFileExtension);
                await CreateProjectAsync(projectFilePath);
                if (!File.Exists(projectFilePath))
                {
                    await MessageBox.ShowAsync("", "Failed to create project file.", icon: MsBox.Avalonia.Enums.Icon.Error);
                    return false;
                }

                ProjectFolderPath = projectFolderPath;
                ProjectFilePath = projectFilePath;
            }

            return true;
        }

        protected abstract string ConsoleToolAppSettingsKey { get; }
        protected abstract string ConsoleToolExeName { get; }
        protected abstract string ConsoleToolMessage { get; }
        protected abstract string ProjectFileExtension { get; }

        protected string? ProjectFolderPath
        {
            get;
            private set;
        }

        protected string? ProjectFilePath
        {
            get;
            private set;
        }

        private async Task<string?> GetConsoleToolPathAsync()
        {
            Configuration config = Configuration.Load();
            string? consolePath = config.ExtraSettings.GetValueOrDefault(ConsoleToolAppSettingsKey);
            if (!string.IsNullOrEmpty(consolePath) && File.Exists(consolePath))
                return consolePath;

            await MessageBox.ShowAsync("", ConsoleToolMessage, icon: MsBox.Avalonia.Enums.Icon.Info);

            consolePath = await App.OpenFilePickerAsync(
                "",
                new()
                {
                    { ConsoleToolExeName, [ConsoleToolExeName] }
                }
            );
            if (consolePath == null)
                return null;

            config.ExtraSettings[ConsoleToolAppSettingsKey] = consolePath;
            config.Save();
            return consolePath;
        }

        protected abstract Task CreateProjectAsync(string projectFilePath);

        protected async Task<string> RunConsoleToolAsync(string arguments)
        {
            if (_consoleToolPath == null)
                throw new InvalidOperationException();

            return await ProcessHelper.RunAsync(_consoleToolPath, arguments);
        }

        public virtual void Dispose()
        {
            if (ProjectFolderPath != null && Directory.Exists(ProjectFolderPath))
                Directory.Delete(ProjectFolderPath, true);
        }
    }
}
