using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Extractor
{
    public class Extractor
    {
        private readonly ArchiveSet _archiveSet;

        public Extractor(ArchiveSet archiveSet)
        {
            _archiveSet = archiveSet;
        }

        public void Extract(string folderPath, ICollection<ArchiveFileReference> fileRefs, ITaskProgress progress, CancellationToken cancellationToken)
        {
            folderPath = @"\\?\" + Path.GetFullPath(folderPath);

            progress = new MultiStepTaskProgress(progress, fileRefs.Count);
            foreach (ArchiveFileReference fileRef in fileRefs)
            {
                string filePath = GetFilePath(folderPath, fileRef);
                ResourceCollection collection = Path.GetExtension(filePath) == ".drm" ? _archiveSet.GetResourceCollection(fileRef) : null;
                if (collection != null)
                    ExtractResourceCollection(filePath, collection, progress, cancellationToken);
                else
                    ExtractFile(filePath, fileRef, progress);
            }
        }

        private string GetFilePath(string baseFolderPath, ArchiveFileReference fileRef)
        {
            string fileName = CdcHash.Lookup(fileRef.NameHash, _archiveSet.Game) ?? fileRef.NameHash.ToString("X016");
            if ((fileRef.Locale & 0xFFFFFFF) != 0xFFFFFFF)
                fileName += "\\" + CdcGameInfo.Get(_archiveSet.Game).LocaleToLanguageCode(fileRef.Locale) + Path.GetExtension(fileName);
            
            return Path.Combine(baseFolderPath, fileName);
        }

        private void ExtractResourceCollection(string folderPath, ResourceCollection collection, ITaskProgress progress, CancellationToken cancellationToken)
        {
            try
            {
                progress.Begin($"Extracting {Path.GetFileName(folderPath)}...");

                Directory.CreateDirectory(folderPath);

                GetResourceReferencesRecursive(collection, out Dictionary<string, ResourceReference> refResources, out HashSet<ResourceReference> otherResources);
                int numExtractedResources = 0;
                int numTotalResources = refResources.Count + otherResources.Count;

                foreach ((string collectionName, ResourceReference resourceRef) in refResources)
                {
                    string fileName = collectionName + ResourceNaming.GetExtension(resourceRef.Type, resourceRef.SubType, _archiveSet.Game);
                    string filePath = Path.Combine(folderPath, fileName);
                    ExtractResource(filePath, resourceRef, ref numExtractedResources, numTotalResources, progress, cancellationToken);
                }

                ulong localeMask = CdcGameInfo.Get(collection.Game).LocalePlatformMask;
                foreach (ResourceReference resourceRef in otherResources)
                {
                    if ((resourceRef.Locale & localeMask) != localeMask)
                        continue;

                    string filePath = Path.Combine(folderPath, ResourceNaming.GetFilePath(_archiveSet, resourceRef));
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                    ExtractResource(filePath, resourceRef, ref numExtractedResources, numTotalResources, progress, cancellationToken);
                }
            }
            finally
            {
                progress.End();
            }
        }

        private void ExtractResource(string filePath, ResourceReference resourceRef, ref int numExtractedResources, int numTotalResources, ITaskProgress progress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (Stream resourceStream = _archiveSet.OpenResource(resourceRef))
            using (Stream fileStream = File.Create(filePath))
            {
                switch (resourceRef.Type)
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
            }

            numExtractedResources++;
            progress.Report((float)numExtractedResources / numTotalResources);
        }

        private void GetResourceReferencesRecursive(ResourceCollection collection, out Dictionary<string, ResourceReference> refResources, out HashSet<ResourceReference> otherResources)
        {
            refResources = new();
            otherResources = new();

            Queue<ResourceCollection> collections = new Queue<ResourceCollection>();
            collections.Enqueue(collection);
            while (collections.Count > 0)
            {
                collection = collections.Dequeue();
                string collectionPath = CdcHash.Lookup(collection.NameHash, _archiveSet.Game);
                if (collectionPath == null)
                    continue;

                string collectionName = Path.GetFileNameWithoutExtension(collectionPath);

                bool isStreamLayer = collectionPath.Contains("\\streamlayers\\");
                ResourceReference mainResourceRef = collection.MainResourceReference;
                foreach (ResourceReference resource in collection.ResourceReferences)
                {
                    if (resource == mainResourceRef)
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
                            refResources[collectionName] = new ResourceReference(
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
                    else if (resource.Type == ResourceType.ObjectReference || resource.Type == ResourceType.GlobalContentReference)
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
                    ResourceCollection dependencyCollection = _archiveSet.GetResourceCollection(dependency.FilePath, dependency.Locale);
                    if (dependencyCollection != null)
                        collections.Enqueue(dependencyCollection);
                }
            }
        }

        private void ExtractFile(string filePath, ArchiveFileReference fileRef, ITaskProgress progress)
        {
            try
            {
                progress.Begin($"Extracting {Path.GetFileName(filePath)}...");

                using Stream archiveFileStream = _archiveSet.OpenFile(fileRef);

                string folderPath = Path.GetDirectoryName(filePath);
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
            if (locals == null)
                return;

            using StreamWriter streamWriter = new StreamWriter(extractedFileStream);
            using JsonWriter jsonWriter = new JsonTextWriter(streamWriter) { Formatting = Formatting.Indented };
            jsonWriter.WriteStartObject();
            foreach ((string key, string value) in locals.Strings.OrderBy(p => p.Key, Comparer<string>.Create(CompareLocalsBinKeys)))
            {
                jsonWriter.WritePropertyName(key);
                jsonWriter.WriteValue(value);
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
            
            string folderPath = Path.GetDirectoryName(mulFilePath);
            foreach (string fmodFilePath in Directory.GetFiles(folderPath, "*.fsb"))
            {
                ConvertGameSound(fmodFilePath);
                File.Delete(fmodFilePath);
            }
        }

        private static void ConvertGameSound(string soundFilePath)
        {
            string vgmstreamPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), @"vgmstream\vgmstream-cli.exe");
            if (!File.Exists(vgmstreamPath))
                return;

            if (soundFilePath.StartsWith(@"\\?\"))
                soundFilePath = soundFilePath.Substring(4);

            using Process process = Process.Start(
                new ProcessStartInfo
                {
                    FileName = vgmstreamPath,
                    Arguments = $"-o \"{Path.ChangeExtension(soundFilePath, ".wav")}\" \"{soundFilePath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            );
            process.WaitForExit();
        }
    }
}
