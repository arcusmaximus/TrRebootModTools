using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using TrRebootTools.Shared.Cdc;

namespace TrRebootTools.SoundConverter
{
    internal class WwiseEncoder : SoundEncoder
    {
        public override CdcGame Game => CdcGame.Shadow;
        public override string InputExtension => ".wav";

        protected override async Task<string?> ConvertInternalAsync(string wavFilePath)
        {
            string sourcesFilePath = Path.Combine(ProjectFolderPath!, "sources.wsources");
            XElement sourcesXml =
                new XElement(
                    "ExternalSourcesList",
                    new XAttribute("SchemaVersion", 1),
                    new XElement(
                        "Source",
                        new XAttribute("Path", wavFilePath),
                        new XAttribute("Conversion", "Vorbis Quality High")
                    )
                );
            sourcesXml.Save(sourcesFilePath);

            string wwiseLog = await RunConsoleToolAsync($"convert-external-source \"{ProjectFilePath}\" --source-file \"{sourcesFilePath}\"");

            string wemFilePath = Path.Combine(
                ProjectFolderPath!,
                "GeneratedSoundBanks",
                "Windows",
                Path.ChangeExtension(Path.GetFileName(wavFilePath), ".wem")
            );
            if (!File.Exists(wemFilePath))
                throw new Exception("Wwise conversion failed.\r\n\r\n" + wwiseLog);

            return wemFilePath;
        }

        protected override string ConsoleToolAppSettingsKey => "WwiseConsole";
        protected override string ConsoleToolExeName => "WwiseConsole.exe";
        protected override string ConsoleToolMessage => "Please install the Wwise authoring tools version 2023.1.1.8417 (through the Audiokinetic Launcher) and select the location of WwiseConsole.exe.";

        protected override string ProjectFileExtension => ".wproj";

        protected override async Task CreateProjectAsync(string projectFilePath)
        {
            await RunConsoleToolAsync($"create-new-project \"{projectFilePath}\"");
        }
    }
}
