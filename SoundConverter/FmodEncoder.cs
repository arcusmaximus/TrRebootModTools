using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using TrRebootTools.Shared.Cdc;

namespace TrRebootTools.SoundConverter
{
    internal abstract class FmodEncoder : SoundEncoder
    {
        protected override string ConsoleToolAppSettingsKey => "FmodConsole";
        protected override string ConsoleToolExeName => "fmod_designercl.exe";
        protected override string ConsoleToolMessage => "Please install FMOD Designer version 4.36.04 and select the location of fmod_designercl.exe.";
        protected override string ProjectFileExtension => ".fdp";

        protected FmodEncoder(CdcGame game)
        {
            Game = game;
        }

        public override CdcGame Game { get; }

        protected async Task<string> ConvertWavAsync(string wavFilePath)
        {
            if (!File.Exists(wavFilePath))
                return null;

            XmlDocument doc = new();
            doc.Load(ProjectFilePath);
            foreach (XmlElement elem in doc.SelectNodes("//waveform/filename"))
            {
                elem.InnerText = wavFilePath;
            }
            doc.Save(ProjectFilePath);

            await RunConsoleToolAsync($"-pc \"{ProjectFilePath}\"");

            string cacheFolderPath = Path.Combine(ProjectFolderPath, ".fsbcache");
            if (Directory.Exists(cacheFolderPath))
                Directory.Delete(cacheFolderPath, true);

            string fromFsbPath = Path.Combine(ProjectFolderPath, "proj_bank00.fsb");
            if (!File.Exists(fromFsbPath))
                return null;

            string toFsbPath = Path.ChangeExtension(wavFilePath, ".fsb");
            if (File.Exists(toFsbPath))
                File.Delete(toFsbPath);

            File.Move(fromFsbPath, toFsbPath);
            return toFsbPath;
        }

        protected override async Task CreateProjectAsync(string projectFilePath)
        {
            string projectFolderPath = Path.GetDirectoryName(projectFilePath);
            if (!Directory.Exists(projectFolderPath))
                Directory.CreateDirectory(projectFolderPath);

            string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            File.Copy(Path.Combine(assemblyFolder, "fmod-project.fdp"), projectFilePath);
        }
    }
}
