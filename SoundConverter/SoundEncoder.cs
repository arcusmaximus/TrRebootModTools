using System.Configuration;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using TrRebootTools.Shared.Cdc;

namespace TrRebootTools.SoundConverter
{
    internal abstract class SoundEncoder : ISoundConverter
    {
        private string _consoleToolPath;

        public abstract CdcGame Game { get; }
        public abstract string InputExtension { get; }

        public async Task<bool> ConvertAsync(string inputFilePath, string outputFolderPath)
        {
            if (!await InitAsync())
                return false;

            string subFolderPath = "";
            int gamePathIndex = inputFilePath.IndexOf("\\pc-w\\");
            if (gamePathIndex < 0)
                gamePathIndex = inputFilePath.IndexOf("\\pcx64-w\\");

            if (gamePathIndex >= 0)
                subFolderPath = Path.GetDirectoryName(inputFilePath.Substring(gamePathIndex + 1));
            else if (CdcGameInfo.Get(Game).LanguageCodeToLocale(Path.GetFileNameWithoutExtension(inputFilePath)) != null)
                subFolderPath = Path.GetFileName(Path.GetDirectoryName(inputFilePath));

            string tempOutputFilePath = await ConvertInternalAsync(inputFilePath);
            if (tempOutputFilePath == null || !File.Exists(tempOutputFilePath))
                return false;

            string outputExtension = Path.GetExtension(tempOutputFilePath);
            string outputFileName = Path.GetFileNameWithoutExtension(inputFilePath) + outputExtension;
            string outputFilePath = Path.Combine(outputFolderPath, subFolderPath, outputFileName);
            if (outputFilePath != tempOutputFilePath)
            {
                if (File.Exists(outputFilePath))
                    File.Delete(outputFilePath);

                Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath));
                File.Move(tempOutputFilePath, outputFilePath);
            }
            return true;
        }

        protected abstract Task<string> ConvertInternalAsync(string inputFilePath);

        protected async Task<bool> InitAsync()
        {
            _consoleToolPath ??= GetConsoleToolPath();
            if (_consoleToolPath == null)
                return false;

            if (ProjectFilePath == null)
            {
                string projectName = GetType().Name + "Project";
                string projectFolderPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), projectName);
                if (Directory.Exists(projectFolderPath))
                    Directory.Delete(projectFolderPath, true);

                string projectFilePath = Path.Combine(projectFolderPath, projectName + ProjectFileExtension);
                await CreateProjectAsync(projectFilePath);
                if (!File.Exists(projectFilePath))
                {
                    MessageBox.Show("Failed to create project file.", "", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
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

        protected string ProjectFolderPath
        {
            get;
            private set;
        }

        protected string ProjectFilePath
        {
            get;
            private set;
        }

        private string GetConsoleToolPath()
        {
            string consolePath = ConfigurationManager.AppSettings[ConsoleToolAppSettingsKey];
            if (!string.IsNullOrEmpty(consolePath) && File.Exists(consolePath))
                return consolePath;

            MessageBox.Show(ConsoleToolMessage, "", MessageBoxButtons.OK, MessageBoxIcon.Information);

            using OpenFileDialog dialog = new();
            dialog.Filter = $"{ConsoleToolExeName}|{ConsoleToolExeName}";
            if (dialog.ShowDialog() != DialogResult.OK)
                return null;

            consolePath = dialog.FileName;
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings[ConsoleToolAppSettingsKey].Value = consolePath;
            config.Save(ConfigurationSaveMode.Modified);
            return consolePath;
        }

        protected abstract Task CreateProjectAsync(string projectFilePath);

        protected async Task<string> RunConsoleToolAsync(string arguments)
        {
            return await ProcessHelper.RunAsync(_consoleToolPath, arguments);
        }

        public virtual void Dispose()
        {
            if (ProjectFolderPath != null && Directory.Exists(ProjectFolderPath))
                Directory.Delete(ProjectFolderPath, true);
        }
    }
}
