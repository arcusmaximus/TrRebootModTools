using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TrRebootTools.Shared;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.ModManager.Mod;
using TrRebootTools.Shared.Forms;
using System.Collections.Generic;

namespace TrRebootTools.ModManager
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            //GenerateCineEyePatchFiles();
            //return;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool forceGamePrompt = false;
            while (true)
            {
                CdcGame? game = GameSelectionForm.GetGame(forceGamePrompt);
                if (game == null)
                    break;

                string gameFolderPath = GameFolderFinder.Find(game.Value);
                if (gameFolderPath == null)
                    break;

                if (args.Length == 1)
                {
                    HandleCommandLine(gameFolderPath, args[0], game.Value);
                    break;
                }

                MainForm form = new MainForm(gameFolderPath, game.Value);
                Application.Run(form);
                if (!form.GameSelectionRequested)
                    break;

                forceGamePrompt = true;
            }
        }

        private static void HandleCommandLine(string gameFolderPath, string modPath, CdcGame game)
        {
            using ArchiveSet archiveSet = ArchiveSet.Open(gameFolderPath, true, true, game);
            ResourceUsageCache resourceUsageCache = new ResourceUsageCache();

            try
            {
                bool reinstallMods = archiveSet.DuplicateArchives.Count > 0;

                if (!resourceUsageCache.Load(gameFolderPath))
                {
                    using ArchiveSet gameArchiveSet = ArchiveSet.Open(gameFolderPath, true, false, game);
                    RunTaskWithProgress((progress, cancellationToken) => resourceUsageCache.AddArchiveSet(archiveSet, progress, cancellationToken));
                    resourceUsageCache.Save(gameFolderPath);
                    reinstallMods = true;
                }

                if (reinstallMods)
                {
                    ModInstaller installer = new ModInstaller(archiveSet, resourceUsageCache);
                    RunTaskWithProgress((progress, cancellationToken) => installer.ReinstallAll(progress, cancellationToken));
                }

                InstallMod(archiveSet, resourceUsageCache, modPath);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private static void InstallMod(ArchiveSet archiveSet, ResourceUsageCache resourceUsageCache, string modPath)
        {
            ModInstaller installer = new ModInstaller(archiveSet, resourceUsageCache);
            if (File.Exists(modPath))
            {
                string extension = Path.GetExtension(modPath);
                if (extension != ".7z" && extension != ".zip" && extension != ".rar")
                {
                    MessageBox.Show(
                        "Only .zip and .7z files are supported for direct mod installation. Please extract the archive and install the folder instead.",
                        "File not supported",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Exclamation
                    );
                    return;
                }
                RunTaskWithProgress((progress, cancellationToken) => installer.InstallFromZip(modPath, progress, cancellationToken));
            }
            else if (Directory.Exists(modPath))
            {
                RunTaskWithProgress((progress, cancellationToken) => installer.InstallFromFolder(modPath, progress, cancellationToken));
            }
            else
            {
                MessageBox.Show("The specified mod path does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private static void RunTaskWithProgress(Action<ITaskProgress, CancellationToken> action)
        {
            using TaskProgressForm progressForm = new TaskProgressForm();
            ExceptionDispatchInfo exception = null;
            progressForm.Load +=
                async (s, e) =>
                {
                    try
                    {
                        await Task.Run(() => action(progressForm, progressForm.CancellationToken), progressForm.CancellationToken);
                    }
                    catch (Exception ex)
                    {
                        exception = ExceptionDispatchInfo.Capture(ex);
                    }
                    progressForm.Close();
                };
            Application.Run(progressForm);
            exception?.Throw();
        }

        /*
        private static void GenerateCineEyePatchFiles()
        {
            using ArchiveSet archiveSet = ArchiveSet.Open(@"D:\Steam\steamapps\common\Rise of the Tomb Raider", true, false, CdcGame.Rise);
            foreach (ArchiveFileReference fileRef in archiveSet.Files)
            {
                string filePath = CdcHash.Lookup(fileRef.NameHash, CdcGame.Rise);
                if (filePath == null || !filePath.StartsWith("pcx64-w\\cinstream\\") || !filePath.EndsWith(".mul"))
                    continue;

                using Stream stream = archiveSet.OpenFile(fileRef);
                BinaryReader reader = new(stream);
                stream.Position = 0x2000;

                int numChannels = 0;
                int[] channelRunTypes = null;
                int[] channelRunLengths = null;
                Dictionary<int, float> channelChanges = null;

                Ips32Patch ips = new();
                while (stream.Position < stream.Length)
                {
                    int packetType = reader.ReadInt32();
                    int packetSize = reader.ReadInt32();
                    stream.Position += 8;
                    if (packetType != 1)
                    {
                        stream.Position += packetSize;
                        stream.Position = (stream.Position + 0xF) & ~0xF;
                        continue;
                    }

                    if (channelRunTypes == null)
                    {
                        int cineMagic = reader.ReadInt32();
                        if (cineMagic != 0x43494E45)
                            throw new InvalidDataException();

                        stream.Position += 0x50;
                        int numAnchors = reader.ReadInt32();
                        stream.Position += numAnchors * 0xC;
                        numChannels += numAnchors * 9;

                        int numSkeletons = reader.ReadInt32();
                        int? laraFirstChannel = null;
                        for (int i = 0; i < numSkeletons; i++)
                        {
                            numChannels += 3;

                            int instanceId = reader.ReadInt32();
                            int numBones = reader.ReadInt32();
                            numChannels += numBones * 14;

                            int firstChannel = reader.ReadInt32();
                            if (instanceId == -1 && numBones == 185)
                                laraFirstChannel = firstChannel;

                            stream.Position += numBones * 0x40;

                            int numLights = reader.ReadInt32();
                            int firstLightChannel = reader.ReadInt32();
                            numChannels += numLights * 10;

                            int numCustomChannels = reader.ReadInt32();
                            int firstCustomChannel = reader.ReadInt32();
                            numChannels += numCustomChannels;

                            int numBlendShapes = reader.ReadInt32();
                            int firstBlendShapeChannel = reader.ReadInt32();
                            numChannels += numBlendShapes;
                        }

                        int numCameras = reader.ReadInt32();
                        stream.Position += numCameras * 4;
                        numChannels += numCameras * 16;

                        int triggerUnitId = reader.ReadInt32();
                        int numTriggers = reader.ReadInt32();
                        stream.Position += numTriggers * 8;
                        numChannels += numTriggers;

                        int numSubtitles = reader.ReadInt32();

                        stream.Position += numSkeletons * 0x1C;
                        stream.Position += numCameras * 0x1C;

                        int numCameraCuts = reader.ReadInt32();
                        stream.Position += numCameraCuts * 4;

                        if (laraFirstChannel == null)
                            break;

                        channelRunTypes = new int[numChannels];
                        channelRunLengths = new int[numChannels];
                        channelChanges = new()
                        {
                            { laraFirstChannel.Value + 3 + 14*87 + 6, -1.715306f },
                            { laraFirstChannel.Value + 3 + 14*87 + 7, -2f },
                            { laraFirstChannel.Value + 3 + 14*87 + 8, 2.1f },

                            { laraFirstChannel.Value + 3 + 14*88 + 6, 1.71841f },
                            { laraFirstChannel.Value + 3 + 14*88 + 7, -2f },
                            { laraFirstChannel.Value + 3 + 14*88 + 8, 2.1f },
                        };
                    }

                    int frameSize = reader.ReadInt32();
                    int frameNr = reader.ReadInt32();
                    if (frameNr < 0)
                        break;

                    for (int i = 0; i < numChannels; i++)
                    {
                        bool readValue;
                        if (channelRunLengths[i] == 0)
                        {
                            int channelInfo = reader.ReadInt32();
                            channelRunLengths[i] = channelInfo & 0x0FFFFFFF;
                            channelRunTypes[i] = channelInfo >> 28;
                            readValue = true;
                        }
                        else
                        {
                            readValue = channelRunTypes[i] != 0;
                        }
                        if (readValue)
                        {
                            if (channelChanges.TryGetValue(i, out float value))
                                ips.Add((uint)stream.Position, BitConverter.GetBytes(value));

                            float origValue = reader.ReadSingle();
                        }
                        channelRunLengths[i]--;
                    }

                    int subtitleLength = reader.ReadInt32();
                    stream.Position += subtitleLength;
                    stream.Position = (stream.Position + 0xF) & ~0xF;
                }

                if (ips.Patches.Count == 0)
                    continue;

                string ipsFilePath = Path.Combine(@"D:\Projects\TrRebootModTools\Build\Debug\_mod", filePath + ".ips32");
                Directory.CreateDirectory(Path.GetDirectoryName(ipsFilePath));
                using Stream ipsStream = File.Create(ipsFilePath);
                ips.Write(ipsStream);
            }
        }
        */

        /*
        private static void CreateSottrSoundSpreadsheet()
        {
            using ArchiveSet archiveSet = ArchiveSet.Open(@"D:\Steam\steamapps\common\Shadow of the Tomb Raider", true, false, CdcGame.Shadow);

            Dictionary<string, Dictionary<string, List<int>>> soundIds = new();
            HashSet<string> languages = new();
            foreach (ArchiveFileReference fileRef in archiveSet.Files)
            {
                string filePath = CdcHash.Lookup(fileRef.NameHash, CdcGame.Shadow);
                if (filePath == null || !filePath.EndsWith(".wem"))
                    continue;

                string folderPath = Path.GetDirectoryName(filePath);
                if (folderPath == null || Path.GetDirectoryName(folderPath) != "pcx64-w\\wwise")
                    continue;

                string subtitleKeys;
                using (Stream stream = archiveSet.OpenFile(fileRef))
                {
                    WwiseSound sound = new WwiseSound(stream);
                    subtitleKeys = string.Join(
                        "\r\n",
                        sound.Chunks
                             .OfType<WwiseSound.ListChunk>()
                             .SelectMany(c => c.Chunks)
                             .OfType<WwiseSound.LabelChunk>()
                             .Select(c => c.Label.ToLower().Replace("subtitle-start:", "").Replace("subtitle-end:", ""))
                             .Distinct()
                    );
                }
                if (subtitleKeys.Length == 0)
                    continue;

                string language = Path.GetFileName(folderPath);
                languages.Add(language);

                int soundId = int.Parse(Path.GetFileNameWithoutExtension(filePath));
                soundIds.GetOrAdd(subtitleKeys, () => new())
                        .GetOrAdd(language, () => new())
                        .Add(soundId);
            }

            Dictionary<string, LocalsBin> locals = new();
            foreach (CdcGameInfo.Language language in CdcGameInfo.Get(CdcGame.Shadow).Languages)
            {
                using Stream stream = archiveSet.OpenFile(archiveSet.GetFileReference("pcx64-w\\local\\locals.bin", language.Locale));
                locals[language.Name.ToLower()] = LocalsBin.Open(stream, CdcGame.Shadow);
            }

            Dictionary<string, Dictionary<Guid, string>> guidLocals = new();
            foreach ((string language, LocalsBin localsOfLanguage) in locals)
            {
                Dictionary<Guid, string> guidLocalsOfLanguage = new();
                foreach ((string key, string value) in localsOfLanguage.Strings)
                {
                    Guid? guid = GetSubtitleKeyGuid(key);
                    if (guid != null)
                        guidLocalsOfLanguage[guid.Value] = value;
                }
                guidLocals[language] = guidLocalsOfLanguage;
            }

            using StreamWriter writer = new StreamWriter(@"D:\Downloads\sounds.csv");
            writer.Write("\"Subtitle keys\"");
            foreach (string language in languages.OrderBy(l => l))
            {
                writer.Write(",\"" + language.Substring(0, 1).ToUpper() + language.Substring(1) + " text\"");
                writer.Write(",\"" + language.Substring(0, 1).ToUpper() + language.Substring(1) + " file\"");
            }
            writer.WriteLine();

            foreach ((string subtitleKeys, Dictionary<string, List<int>> idsByLanguage) in soundIds.OrderBy(p => p.Key))
            {
                writer.Write($"\"{subtitleKeys}\"");

                foreach (string language in languages.OrderBy(l => l))
                {
                    writer.Write(",");
                    string subtitleTexts = string.Join(
                        "\r\n",
                        subtitleKeys.Split(new[] { "\r\n" }, StringSplitOptions.None)
                                    .Select(k => GetSubtitleText(k, locals[language], guidLocals[language]))
                    );
                    writer.Write("\"" + subtitleTexts.Replace("\"", "\"\"") + "\"");

                    writer.Write(",");
                    if (idsByLanguage.TryGetValue(language, out List<int> idsOfLanguage))
                        writer.Write("\"" + string.Join("\r\n", idsOfLanguage.Select(id => id + ".wem")) + "\"");
                }
                writer.WriteLine();
            }
        }


        private static Guid? GetSubtitleKeyGuid(string key)
        {
            Match match = Regex.Match(key, @"[0-9a-f]{8}[-_][0-9a-f]{4}[-_][0-9a-f]{4}[-_][0-9a-f]{4}[-_][0-9a-f]{12}");
            if (!match.Success)
                return null;

            return Guid.Parse(match.Value.Replace('_', '-'));
        }

        private static string GetSubtitleText(string key, LocalsBin locals, Dictionary<Guid, string> guidLocals)
        {
            string value = locals.Strings.GetOrDefault(key);
            if (value != null)
                return value;

            Guid? guid = GetSubtitleKeyGuid(key);
            if (guid != null)
                return guidLocals.GetOrDefault(guid.Value);

            return null;
        }


        private static void ExtractSoundNames()
        {
            using ArchiveSet archiveSet = ArchiveSet.Open(@"D:\Steam\steamapps\common\Shadow of the Tomb Raider", true, false, CdcGame.Shadow);
            ResourceUsageCache cache = new ResourceUsageCache();
            cache.Load(archiveSet.FolderPath);

            var archiveSetNameHashes = archiveSet.Files.Select(f => f.NameHash).ToHashSet();
            using Stream listStream = File.Open(@"D:\Projects\TrRebootModTools\Extractor\SOTR_PC_Release.list", FileMode.Append, FileAccess.Write);
            using StreamWriter listWriter = new StreamWriter(listStream);
            foreach (int soundId in cache.WwiseSoundIds)
            {
                foreach (string soundPath in GetSoundFilePaths(soundId))
                {
                    AddSoundPath(soundPath, archiveSetNameHashes, listWriter);
                }
            }
        }

        private static readonly string[] Languages = {
            "arabic",
            "english",
            "french",
            "german",
            "iberspanish",
            "italian",
            "japanese",
            "ksa_english",
            "latamspanish",
            "polish",
            "portuguese",
            "russian",
            "simplechinese"
        };

        private static IEnumerable<string> GetSoundFilePaths(int soundId)
        {
            yield return $"pcx64-w\\wwise\\{soundId}.wem";
            foreach (string language in Languages)
            {
                yield return $"pcx64-w\\wwise\\{language}\\{soundId}.wem";
            }
        }

        private static void AddSoundPath(string path, HashSet<ulong> archiveSetNameHashes, StreamWriter listWriter)
        {
            ulong nameHash = CdcHash.Calculate(path, CdcGame.Shadow);
            if (!archiveSetNameHashes.Contains(nameHash) || CdcHash.Lookup(nameHash, CdcGame.Shadow) != null)
                return;

            listWriter.WriteLine(path);
        }
        */
    }
}
