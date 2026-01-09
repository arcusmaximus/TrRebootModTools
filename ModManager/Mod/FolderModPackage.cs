using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Util;
using System.Text.RegularExpressions;
using TrRebootTools.Shared;
using System.Globalization;

namespace TrRebootTools.ModManager.Mod
{
    internal class FolderModPackage : ModPackage
    {
        private const string VariationDescriptionFileName = "variation.txt";
        private const string VariationImageFileName = "variation.png";

        private readonly string _folderPath;
        private readonly bool _checkForDuplicates;
        private readonly ArchiveSet _archiveSet;
        private readonly Dictionary<ulong, ulong?> _gameFileLocales = new();
        private readonly ResourceUsageCache _resourceUsageCache;
        private readonly Dictionary<ArchiveFileKey, string> _files = new();
        private readonly Dictionary<ResourceKey, string> _physicalResources = new();
        private readonly Dictionary<ResourceKey, MemoryStream> _virtualResources = new();

        public FolderModPackage(string folderPath, bool checkForDuplicates, ArchiveSet archiveSet, ResourceUsageCache resourceUsageCache)
            : this(Path.GetFileName(folderPath), folderPath, checkForDuplicates, archiveSet, resourceUsageCache)
        {
        }

        public FolderModPackage(string name, string folderPath, bool checkForDuplicates, ArchiveSet archiveSet, ResourceUsageCache resourceUsageCache)
        {
            Name = name;
            _folderPath = folderPath;
            _checkForDuplicates = checkForDuplicates;
            _archiveSet = archiveSet;
            _resourceUsageCache = resourceUsageCache;
            foreach (ArchiveFileKey fileKey in archiveSet.Files)
            {
                if (fileKey.Locale != 0xFFFFFFFFFFFFFFFF)
                    _gameFileLocales[fileKey.NameHash] = !_gameFileLocales.ContainsKey(fileKey.NameHash) ? fileKey.Locale : null;
            }

            ScanFolder(_folderPath, _folderPath);
            AddVirtualResources(_virtualResources, _physicalResources, _files, _archiveSet, _resourceUsageCache);
        }

        public override string Name
        {
            get;
        }

        public override IEnumerable<ArchiveFileKey> Files => _files.Keys;

        public override Stream OpenFile(ArchiveFileKey fileKey)
        {
            return OpenFile(_files, fileKey, _archiveSet);
        }

        public override IEnumerable<ResourceKey> Resources => _physicalResources.Keys.Concat(_virtualResources.Keys);

        public override Stream OpenResource(ResourceKey resourceKey)
        {
            return OpenResource(_physicalResources, _virtualResources, resourceKey, _archiveSet, _resourceUsageCache);
        }

        private void ScanFolder(string baseFolderPath, string folderPath)
        {
            AddFilesAndResourcesFromFolder(baseFolderPath, folderPath, _files, _physicalResources, false, _checkForDuplicates, _gameFileLocales, _resourceUsageCache, _archiveSet.Game);

            foreach (string subFolderPath in Directory.EnumerateDirectories(folderPath))
            {
                if (File.Exists(Path.Combine(subFolderPath, VariationDescriptionFileName)) ||
                    File.Exists(Path.Combine(subFolderPath, VariationImageFileName)))
                {
                    Variations.Add(new FolderModVariation(subFolderPath, _archiveSet, _checkForDuplicates, _resourceUsageCache, _gameFileLocales));
                }
                else
                {
                    ScanFolder(baseFolderPath, subFolderPath);
                }
            }
        }

        private static void AddFilesAndResourcesFromFolder(
            string baseFolderPath,
            string folderPath,
            Dictionary<ArchiveFileKey, string> files,
            Dictionary<ResourceKey, string> resources,
            bool recursive,
            bool checkForDuplicates,
            Dictionary<ulong, ulong?> gameFileLocales,
            ResourceUsageCache usageCache,
            CdcGame game)
        {
            if (!baseFolderPath.EndsWith("\\"))
                baseFolderPath += "\\";

            foreach (string filePath in Directory.EnumerateFiles(folderPath, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            {
                if (TryGetResourceKey(baseFolderPath, filePath, out ResourceKey resourceKey, usageCache, game))
                {
                    if (checkForDuplicates && resources.TryGetValue(resourceKey, out string existingFilePath))
                        throw new Exception($"The mod contains a duplicate resource:\r\n{existingFilePath.Substring(baseFolderPath.Length)}\r\n{filePath.Substring(baseFolderPath.Length)}");

                    resources[resourceKey] = filePath;
                    continue;
                }

                string extension = Path.GetExtension(filePath);
                if (extension is ".txt" or ".png" or ".drm" or ".tmp")
                    continue;

                string filePathToHash = filePath.Substring(baseFolderPath.Length);
                ulong? locale = CdcGameInfo.Get(game).LanguageCodeToLocale(Path.GetFileNameWithoutExtension(filePathToHash));
                if (locale != null)
                    filePathToHash = Path.GetDirectoryName(filePathToHash);

                if (Path.GetExtension(filePathToHash) == ".ips32")
                    filePathToHash = filePathToHash.Substring(0, filePathToHash.Length - 6);

                ulong nameHash = CdcHash.Calculate(filePathToHash, game);
                locale ??= gameFileLocales.GetOrDefault(nameHash);
                files[new ArchiveFileKey(nameHash, locale ?? 0xFFFFFFFFFFFFFFFF)] = filePath;
            }
        }

        private static bool TryGetResourceKey(string baseFolderPath, string filePath, out ResourceKey resourceKey, ResourceUsageCache usageCache, CdcGame game)
        {
            return ResourceNaming.TryGetResourceKey(filePath, out resourceKey, game) || 
                   TryGetResourceKeyByOriginalFilePath(filePath, out resourceKey, usageCache, game) ||
                   TryGetResourceKeyFromRefIndex(baseFolderPath, filePath, out resourceKey, game);
        }

        private static bool TryGetResourceKeyByOriginalFilePath(string filePath, out ResourceKey resourceKey, ResourceUsageCache usageCache, CdcGame game)
        {
            (ResourceType type, _) = ResourceNaming.GetType(filePath, game);
            if (type == ResourceType.Unknown)
            {
                resourceKey = new();
                return false;
            }

            using Stream stream = File.OpenRead(filePath);
            string origFilePath = ResourceNaming.ReadOriginalFilePath(stream, type, game);
            if (origFilePath == null)
            {
                resourceKey = new();
                return false;
            }

            if (!usageCache.TryGetResourceKeyByOriginalFilePath(origFilePath, out resourceKey))
                return false;

            ulong locale = resourceKey.Locale;
            if (Path.GetExtension(filePath) == Path.GetExtension(Path.GetDirectoryName(filePath)))
            {
                ulong? newLocale = CdcGameInfo.Get(game).LanguageCodeToLocale(Path.GetFileNameWithoutExtension(filePath));
                if (newLocale != null)
                    locale = newLocale.Value;
            }
            resourceKey = new(resourceKey.Type, resourceKey.SubType, resourceKey.Id, locale);
            return true;
        }

        private static bool TryGetResourceKeyFromRefIndex(string baseFolderPath, string resourceFilePath, out ResourceKey resourceKey, CdcGame game)
        {
            Match match = Regex.Match(Path.GetFileNameWithoutExtension(resourceFilePath), @"^(?:Section|Replace)\s+(\d+)$");
            if (!match.Success)
            {
                resourceKey = new();
                return false;
            }

            string collectionFilePath = null;
            string folderPath = resourceFilePath;
            do
            {
                folderPath = Path.GetDirectoryName(folderPath);
                collectionFilePath = Directory.EnumerateFiles(folderPath, "*.drm").FirstOrDefault();
                if (collectionFilePath != null)
                    break;
            } while (folderPath.TrimEnd('\\') != baseFolderPath.TrimEnd('\\'));

            if (collectionFilePath == null)
            {
                resourceKey = new();
                return false;
            }

            using Stream stream = File.OpenRead(collectionFilePath);
            ResourceCollection collection = ResourceCollection.Open(0, 0, stream, game);
            resourceKey = collection.ResourceReferences[int.Parse(match.Groups[1].Value) - 1];
            return true;
        }

        private static void AddVirtualResources(
            Dictionary<ResourceKey, MemoryStream> virtualResources,
            Dictionary<ResourceKey, string> physicalResources,
            Dictionary<ArchiveFileKey, string> files,
            ArchiveSet archiveSet,
            ResourceUsageCache resourceUsageCache)
        {
            Dictionary<ResourceKey, WwiseSoundBank> virtualSoundBanks = new();
            foreach ((ArchiveFileKey wemFileKey, string wemFilePath) in files.Where(p => Path.GetExtension(p.Value) == ".wem"))
            {
                if (!int.TryParse(Path.GetFileNameWithoutExtension(wemFilePath), out int soundId))
                    continue;

                ArchiveFileReference fileRef = archiveSet.GetFileReference(wemFileKey);
                if (fileRef == null)
                    throw new Exception($"{Path.GetFileName(wemFilePath)} in the mod was not found in the game's original files; incorrect subfolder?");

                foreach (WwiseSoundBankItemReference soundUsage in resourceUsageCache.GetWwiseSoundUsages(soundId))
                {
                    if (soundUsage.Type != WwiseSoundBankItemReferenceType.DataIndex)
                        continue;

                    ResourceKey bankResourceKey = new(ResourceType.SoundBank, 0, soundUsage.BankResourceId, soundUsage.BankResourceLocale);
                    if (physicalResources.ContainsKey(bankResourceKey))
                        continue;

                    WwiseSoundBank bank = virtualSoundBanks.GetOrDefault(bankResourceKey);
                    if (bank == null)
                    {
                        ResourceReference bankResourceRef = resourceUsageCache.GetResourceReference(archiveSet, bankResourceKey);
                        if (bankResourceRef == null)
                            continue;

                        using (Stream stream = archiveSet.OpenResource(bankResourceRef))
                        {
                            bank = new WwiseSoundBank(stream);
                        }
                        virtualSoundBanks.Add(bankResourceKey, bank);
                    }

                    if (bank.EmbeddedSounds.GetOrDefault(soundId).Count == fileRef.Length)
                    {
                        using (Stream stream = OpenFile(files, wemFileKey, archiveSet))
                        {
                            bank.EmbeddedSounds[soundId] = stream.GetContent();
                        }
                    }
                    else
                    {
                        bank.EmbeddedSounds.Remove(soundId);
                    }
                }
            }

            foreach ((ResourceKey bankResourceKey, WwiseSoundBank bank) in virtualSoundBanks)
            {
                MemoryStream stream = new MemoryStream();
                bank.Write(stream);
                stream.Position = 0;
                virtualResources.Add(bankResourceKey, stream);
            }
        }

        private static Stream OpenFile(Dictionary<ArchiveFileKey, string> files, ArchiveFileKey fileKey, ArchiveSet archiveSet)
        {
            string filePath = files.GetOrDefault(fileKey);
            if (filePath == null)
                return null;

            Stream fileStream = File.OpenRead(filePath);
            Stream resultStream = null;

            try
            {
                if (filePath.EndsWith(".json") && Path.GetFileName(Path.GetDirectoryName(filePath)) == "locals.bin")
                    resultStream = GetPatchedLocalsBin(fileStream, fileKey, archiveSet);
                else if (filePath.EndsWith(".wem"))
                    resultStream = GetPatchedSound(filePath, fileStream, fileKey, archiveSet);
                else if (filePath.EndsWith(".ips32"))
                    resultStream = GetIps32PatchedStream(fileStream, fileKey, archiveSet);
                else
                    resultStream = fileStream;
            }
            finally
            {
                if (resultStream != fileStream)
                    fileStream.Close();
            }

            return resultStream;
        }

        private static Stream GetPatchedLocalsBin(Stream jsonStream, ArchiveFileKey fileKey, ArchiveSet archiveSet)
        {
            ArchiveFileReference fileRef = archiveSet.GetFileReference(fileKey);
            if (fileRef == null)
                throw new Exception($"File {fileKey} not found for locals.bin");

            LocalsBin locals;
            using (Stream origStream = archiveSet.OpenFile(fileRef))
            {
                locals = LocalsBin.Open(origStream, archiveSet.Game);
            }

            JObject json;
            try
            {
                using StreamReader streamReader = new StreamReader(jsonStream);
                using JsonReader jsonReader = new JsonTextReader(streamReader);
                json = JObject.Load(jsonReader);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to parse JSON file for locals.bin:\r\n" + ex.Message);
            }

            foreach ((string key, JToken value) in json)
            {
                locals.Strings[key] = (string)value;
            }

            MemoryStream patchedStream = new MemoryStream();
            locals.Write(patchedStream);
            patchedStream.Position = 0;
            return patchedStream;
        }

        private static Stream GetPatchedSound(string modSoundFilePath, Stream modSoundFileStream, ArchiveFileKey fileKey, ArchiveSet archiveSet)
        {
            if (modSoundFileStream.Length == 0)
                return modSoundFileStream;

            WwiseSound modSound = new WwiseSound(modSoundFileStream, modSoundFilePath);
            modSoundFileStream.Position = 0;

            if (modSound.Chunks.OfType<WwiseSound.CueChunk>().Any())
                return modSoundFileStream;

            ArchiveFileReference fileRef = archiveSet.GetFileReference(fileKey);
            if (fileRef == null)
                return modSoundFileStream;

            WwiseSound origSound;
            using (Stream origSoundStream = archiveSet.OpenFile(fileRef))
            {
                origSound = new WwiseSound(origSoundStream, "Vanilla " + Path.GetFileName(modSoundFilePath));
            }

            int modSampleFreq = modSound.Format.SampleFrequency;
            int origSampleFreq = origSound.Format.SampleFrequency;

            int chunkInsertIdx = modSound.Chunks.IndexOf(c => c is WwiseSound.FormatChunk) + 1;
            foreach (WwiseSound.Chunk origChunk in origSound.Chunks)
            {
                if (origChunk is WwiseSound.CueChunk origCueChunk && modSampleFreq != origSampleFreq)
                {
                    for (int i = 0; i < origCueChunk.Points.Count; i++)
                    {
                        WwiseSound.CuePoint point = origCueChunk.Points[i];
                        point.Position = (int)((long)point.Position * modSampleFreq / origSampleFreq);
                        origCueChunk.Points[i] = point;
                    }
                }

                if (origChunk is WwiseSound.CueChunk or WwiseSound.ListChunk)
                    modSound.Chunks.Insert(chunkInsertIdx++, origChunk);
            }

            MemoryStream modSoundMemStream = new();
            modSound.Write(modSoundMemStream);
            modSoundMemStream.Position = 0;
            return modSoundMemStream;
        }

        private static Stream GetIps32PatchedStream(Stream ipsStream, ArchiveFileKey fileKey, ArchiveSet archiveSet)
        {
            ArchiveFileReference fileRef = archiveSet.GetFileReference(fileKey);
            if (fileRef == null)
                throw new Exception($"File {fileKey} not found for IPS32 patching");

            using Stream fileStream = archiveSet.OpenFile(fileRef);
            byte[] data = new byte[fileStream.Length];
            fileStream.Read(data, 0, data.Length);

            Ips32Patch patch = new(ipsStream);
            patch.Apply(data);

            return new MemoryStream(data);
        }

        private static Stream OpenResource(
            Dictionary<ResourceKey, string> physicalResources,
            Dictionary<ResourceKey, MemoryStream> virtualResources,
            ResourceKey resourceKey,
            ArchiveSet archiveSet,
            ResourceUsageCache resourceUsageCache)
        {
            Stream virtualStream = virtualResources.GetOrDefault(resourceKey);
            if (virtualStream != null)
                return virtualStream;

            string filePath = physicalResources.GetOrDefault(resourceKey);
            if (filePath == null)
                return null;

            Stream stream = File.OpenRead(filePath);
            string extension = Path.GetExtension(filePath);
            switch (resourceKey.Type)
            {
                case ResourceType.Texture when extension.Equals(".dds", StringComparison.InvariantCultureIgnoreCase):
                    try
                    {
                        return ConvertTexture(stream, resourceKey, archiveSet, resourceUsageCache);
                    }
                    finally
                    {
                        stream.Close();
                    }

                case ResourceType.SoundBank when extension.Equals(".bnk", StringComparison.InvariantCultureIgnoreCase):
                    try
                    {
                        return ConvertSoundBank(stream);
                    }
                    finally
                    {
                        stream.Close();
                    }

                default:
                    return stream;
            }
        }

        private static Stream ConvertTexture(Stream ddsStream, ResourceKey resourceKey, ArchiveSet archiveSet, ResourceUsageCache resourceUsageCache)
        {
            CdcTexture texture = CdcTexture.ReadFromDds(ddsStream, archiveSet.Game);

            uint originalFormat = GetOriginalTextureFormat(resourceKey, archiveSet, resourceUsageCache);
            if (CdcTexture.IsSrgbFormat(originalFormat))
                texture.Header.Format = CdcTexture.MapRegularFormatToSrgb(texture.Header.Format);

            Stream textureStream = new MemoryStream();
            texture.Write(textureStream);
            textureStream.Position = 0;
            return textureStream;
        }

        private static Stream ConvertSoundBank(Stream bnkStream)
        {
            MemoryStream dtpStream = new MemoryStream(8 + (int)bnkStream.Length);
            
            BinaryWriter writer = new BinaryWriter(dtpStream);
            writer.Write(0);
            writer.Write((int)bnkStream.Length);
            bnkStream.CopyTo(dtpStream);

            BinaryReader reader = new BinaryReader(dtpStream);
            dtpStream.Position = 0x14;
            uint soundBankId = reader.ReadUInt32();
            dtpStream.Position = 0;
            writer.Write(soundBankId);

            dtpStream.Position = 0;
            return dtpStream;
        }

        private static uint GetOriginalTextureFormat(ResourceKey resourceKey, ArchiveSet archiveSet, ResourceUsageCache resourceUsageCache)
        {
            ResourceReference resourceRef = resourceUsageCache.GetResourceReference(archiveSet, resourceKey);
            if (resourceRef == null)
                return 0;

            using Stream origTextureStream = archiveSet.OpenResource(resourceRef);
            BinaryReader reader = new BinaryReader(origTextureStream);
            CdcTexture.CdcTextureHeader header = reader.ReadStruct<CdcTexture.CdcTextureHeader>();
            return header.Format;
        }

        private class FolderModVariation : ModVariation
        {
            private readonly ArchiveSet _archiveSet;
            private readonly ResourceUsageCache _resourceUsageCache;
            private readonly Dictionary<ArchiveFileKey, string> _files = new();
            private readonly Dictionary<ResourceKey, string> _physicalResources = new();
            private readonly Dictionary<ResourceKey, MemoryStream> _virtualResources = new();

            public FolderModVariation(string folderPath, ArchiveSet archiveSet, bool checkForDuplicates, ResourceUsageCache resourceUsageCache, Dictionary<ulong, ulong?> gameFileLocales)
                : base(Path.GetFileName(folderPath), GetDescription(folderPath), GetImage(folderPath))
            {
                FolderPath = folderPath;
                _archiveSet = archiveSet;
                _resourceUsageCache = resourceUsageCache;
                AddFilesAndResourcesFromFolder(FolderPath, FolderPath, _files, _physicalResources, true, checkForDuplicates, gameFileLocales, _resourceUsageCache, _archiveSet.Game);
                AddVirtualResources(_virtualResources, _physicalResources, _files, _archiveSet, _resourceUsageCache);
            }

            public string FolderPath
            {
                get;
            }

            private static string GetDescription(string folderPath)
            {
                string filePath = Path.Combine(folderPath, VariationDescriptionFileName);
                return File.Exists(filePath) ? File.ReadAllText(filePath) : null;
            }

            private static Image GetImage(string folderPath)
            {
                string filePath = Path.Combine(folderPath, VariationImageFileName);
                if (!File.Exists(filePath))
                    return null;

                using Image image = Image.FromFile(filePath);
                return new Bitmap(image);
            }

            public override IEnumerable<ArchiveFileKey> Files => _files.Keys;

            public override Stream OpenFile(ArchiveFileKey fileKey)
            {
                return FolderModPackage.OpenFile(_files, fileKey, _archiveSet);
            }

            public override IEnumerable<ResourceKey> Resources => _physicalResources.Keys.Concat(_virtualResources.Keys);

            public override Stream OpenResource(ResourceKey resourceKey)
            {
                return FolderModPackage.OpenResource(_physicalResources, _virtualResources, resourceKey, _archiveSet, _resourceUsageCache);
            }
        }
    }
}
