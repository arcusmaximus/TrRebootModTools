using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace TrRebootTools.Extractor
{
    public class Extractor
    {
        private readonly ArchiveSet _archiveSet;

        public Extractor(ArchiveSet archiveSet)
        {
            _archiveSet = archiveSet;
        }

        public void Extract(string folderPath, ICollection<ArchiveFileDescriptor> files, ITaskProgress progress, CancellationToken cancellationToken)
        {
            if (OperatingSystem.IsWindows())
                folderPath = @"\\?\" + Path.GetFullPath(folderPath);

            progress = new MultiStepTaskProgress(progress, files.Count);
            foreach (var filesOfName in files.GroupBy(f => f.NameHash))
            {
                foreach (ArchiveFileDescriptor file in filesOfName)
                {
                    string filePath = GetFilePath(folderPath, file, filesOfName.Count() > 1);
                    ResourceCollection? collection = Path.GetExtension(filePath) == ".drm" ? _archiveSet.GetResourceCollection(file) : null;
                    if (collection != null)
                        ExtractResourceCollection(filePath, collection, progress, cancellationToken);
                    else
                        ExtractFile(filePath, file, progress);
                }
            }
        }

        private string GetFilePath(string baseFolderPath, ArchiveFileDescriptor file, bool forceLocaleFolder)
        {
            string fileName = CdcHash.Lookup(file.NameHash, _archiveSet.Game, true) ?? file.NameHash.ToString("X016");
            if (forceLocaleFolder || (file.Locale & 0xFFFFFFF) != 0xFFFFFFF)
                fileName += Path.DirectorySeparatorChar + CdcGameInfo.Get(_archiveSet.Game).LocaleToLanguageCode(file.Locale) + Path.GetExtension(fileName);
            
            return Path.Combine(baseFolderPath, fileName);
        }

        private void ExtractResourceCollection(string folderPath, ResourceCollection collection, ITaskProgress progress, CancellationToken cancellationToken)
        {
            try
            {
                progress.Begin($"Extracting {Path.GetFileName(folderPath)}...");

                Directory.CreateDirectory(folderPath);

                GetResourceDescriptorsRecursive(collection, out Dictionary<string, ResourceDescriptor> refResources, out HashSet<ResourceDescriptor> otherResources);
                int numExtractedResources = 0;
                int numTotalResources = refResources.Count + otherResources.Count;

                foreach ((string collectionName, ResourceDescriptor resource) in refResources)
                {
                    string fileName = collectionName + ResourceNaming.GetExtension(resource.Type, resource.SubType, _archiveSet.Game);
                    string filePath = Path.Combine(folderPath, fileName);
                    ExtractResource(filePath, resource, ref numExtractedResources, numTotalResources, progress, cancellationToken);
                }

                ulong localeMask = CdcGameInfo.Get(collection.Game).LocalePlatformMask;
                foreach (ResourceDescriptor resource in otherResources)
                {
                    if ((resource.Locale & localeMask) != localeMask)
                        continue;

                    string filePath = Path.Combine(folderPath, ResourceNaming.GetFilePath(_archiveSet, collection, resource));
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                    ExtractResource(filePath, resource, ref numExtractedResources, numTotalResources, progress, cancellationToken);
                }
            }
            finally
            {
                progress.End();
            }
        }

        private void ExtractResource(string filePath, ResourceDescriptor resource, ref int numExtractedResources, int numTotalResources, ITaskProgress progress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using Stream? resourceStream = _archiveSet.TryOpenResource(resource);
            if (resourceStream == null)
                return;

            using Stream fileStream = File.Create(filePath);
            switch (resource.Type)
            {
                case ResourceType.Texture:
                    CdcTexture texture = CdcTexture.Read(resourceStream);
                    texture.WriteAsDds(fileStream);
                    break;
                    
                case ResourceType.SoundBank when CdcGameInfo.Get(_archiveSet.Game).UsesWwise:
                    byte[] header = new byte[8];
                    resourceStream.Read(header, 0, 8);
                    int length = BitConverter.ToInt32(header, 4);
                    byte[] data = new byte[length];
                    resourceStream.Read(data, 0, length);
                    fileStream.Write(data, 0, length);
                    break;
                
                default:
                    resourceStream.CopyTo(fileStream);
                    break;
            }

            numExtractedResources++;
            progress.Report((float)numExtractedResources / numTotalResources);
        }

        private void GetResourceDescriptorsRecursive(ResourceCollection collection, out Dictionary<string, ResourceDescriptor> refResources, out HashSet<ResourceDescriptor> otherResources)
        {
            refResources = new();
            otherResources = new();

            Queue<ResourceCollection> collections = new Queue<ResourceCollection>();
            collections.Enqueue(collection);
            while (collections.Count > 0)
            {
                collection = collections.Dequeue();
                string? collectionPath = CdcHash.Lookup(collection.NameHash, _archiveSet.Game, true);
                if (collectionPath == null)
                    continue;

                string collectionName = Path.GetFileNameWithoutExtension(collectionPath);

                bool isStreamLayer = collectionPath.Contains(Path.DirectorySeparatorChar + "streamlayers" + Path.DirectorySeparatorChar);
                ResourceDescriptor? mainResource = collection.MainResource;
                foreach (ResourceDescriptor resource in collection.Resources)
                {
                    if (resource == mainResource)
                    {
                        ResourceSubType? subType = null;
                        if (resource.Type == ResourceType.Dtp)
                        {
                            if (isStreamLayer)
                                subType = ResourceSubType.StreamLayer;
                            else if (_archiveSet.Game == CdcGame.Tr2013)
                                subType = ResourceSubType.Level;
                        }

                        if (subType != null)
                        {
                            refResources[collectionName] = new ResourceDescriptor(
                                ResourceType.Dtp,
                                subType.Value,
                                resource.Id,
                                resource.Locale,
                                resource.ArchiveId,
                                resource.ArchiveSubId,
                                resource.ArchivePart,
                                resource.Offset,
                                resource.Length,
                                resource.DecompressionOffset,
                                resource.RefDefinitionsSize,
                                resource.BodySize
                            );
                        }
                        else
                        {
                            refResources[collectionName] = resource;
                        }
                    }
                    else if (resource.Type is ResourceType.ObjectReference or ResourceType.GlobalContentReference)
                    {
                        refResources[collectionName] = resource;
                    }
                    else
                    {
                        otherResources.Add(resource);
                    }
                }

                foreach (ResourceCollectionDependency dependency in collection.Dependencies)
                {
                    ResourceCollection? dependencyCollection = _archiveSet.GetResourceCollection(dependency.FilePath, dependency.Locale);
                    if (dependencyCollection != null)
                        collections.Enqueue(dependencyCollection);
                }
            }
        }

        private void ExtractFile(string filePath, ArchiveFileDescriptor file, ITaskProgress progress)
        {
            try
            {
                progress.Begin($"Extracting {Path.GetFileName(filePath)}...");

                using Stream archiveFileStream = _archiveSet.OpenFile(file);

                string folderPath = Path.GetDirectoryName(filePath)!;
                Directory.CreateDirectory(folderPath);

                if (Path.GetFileName(folderPath) == "locals.bin")
                {
                    filePath = Path.ChangeExtension(filePath, ".json");
                    using Stream extractedFileStream = File.Create(filePath);
                    ExtractLocalsBin(archiveFileStream, extractedFileStream);
                }
                else
                {
                    using (Stream extractedFileStream = File.Create(filePath))
                    {
                        archiveFileStream.CopyTo(extractedFileStream);
                    }

                    string extension = Path.GetExtension(filePath);
                    switch (extension)
                    {
                        case ".mul":
                            SplitMultiplexStream(filePath);
                            break;
                        case ".wem":
                            ConvertGameSound(filePath);
                            break;
                    }
                }
            }
            finally
            {
                progress.End();
            }
        }

        private void ExtractLocalsBin(Stream archiveFileStream, Stream extractedFileStream)
        {
            LocalsBin locals = LocalsBin.Open(archiveFileStream, _archiveSet.Game);
            using Utf8JsonWriter jsonWriter = new(
                extractedFileStream,
                new()
                {
                    Indented = true,
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
                }
            );
            jsonWriter.WriteStartObject();
            foreach ((string key, string value) in locals.Strings.OrderBy(p => p.Key, Comparer<string>.Create(CompareLocalsBinKeys)))
            {
                jsonWriter.WriteString(key, value);
            }
            jsonWriter.WriteEndObject();
        }

        private static int CompareLocalsBinKeys(string a, string b)
        {
            if (int.TryParse(a, out int aInt) && int.TryParse(b, out int bInt))
                return aInt - bInt;
            else
                return a.CompareTo(b);
        }

        private void SplitMultiplexStream(string mulFilePath)
        {
            new MultiplexStreamSplitter(_archiveSet.Game).Split(mulFilePath);
            
            string folderPath = Path.GetDirectoryName(mulFilePath)!;
            foreach (string fmodFilePath in Directory.GetFiles(folderPath, "*.fsb"))
            {
                ConvertGameSound(fmodFilePath);
                File.Delete(fmodFilePath);
            }
        }

        private static void ConvertGameSound(string soundFilePath)
        {
            ProcessHelper.RunVgmStreamAsync(soundFilePath, Path.ChangeExtension(soundFilePath, ".wav"), false).Wait();
        }
    }
}
