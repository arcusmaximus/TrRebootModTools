using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using TrRebootTools.Shared.Cdc.Avengers;
using TrRebootTools.Shared.Cdc.Rise;
using TrRebootTools.Shared.Cdc.Shadow;
using TrRebootTools.Shared.Cdc.Tr2013;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Cdc
{
    public abstract class Archive : IDisposable
    {
        private const int MaxResourceChunkSize = 0x40000;

        private int _numParts = 1;
        private List<Stream>? _partStreams;
        private readonly List<ArchiveFileReference> _fileRefs = [];
        private long _nextFileRefPos;
        private int _maxFiles;
        private bool _hasWrittenResources;

        protected Archive(string baseFilePath, ArchiveMetaData? metaData)
        {
            BaseFilePath = baseFilePath;
            MetaData = metaData;
        }

        protected abstract CdcGame Game { get; }
        protected abstract int HeaderVersion { get; }
        protected abstract bool SupportsSubId { get; }
        protected abstract bool SupportsLanguageList { get; }
        protected abstract ArchiveFileReference ReadFileReference(BinaryReader reader);
        protected abstract void WriteFileReference(BinaryWriter writer, ArchiveFileReference fileRef);
        protected virtual int ContentAlignment => 0x10;

        public static Archive Create(string baseFilePath, int id, int subId, ArchiveMetaData? metaData, int maxFiles, CdcGame game)
        {
            Archive archive = InstantiateArchive(baseFilePath, metaData, game);
            archive.Id = id;
            archive.SubId = subId;
            archive._maxFiles = maxFiles;

            Stream stream = archive.OpenPart(0, baseFilePath, FileMode.Create, FileAccess.ReadWrite);
            archive._partStreams = [stream];

            BinaryWriter writer = new(stream);
            ArchiveHeader header =
                new()
                {
                    Magic = 0x53464154,
                    Version = archive.HeaderVersion,
                    NumParts = 1,
                    Id = archive.Id
                };
            writer.WriteStruct(header);

            if (archive.SupportsSubId)
                writer.Write(archive.SubId);

            string platformStr = CdcGameInfo.Get(game).ArchivePlatform;
            byte[] platformBytes = new byte[0x20];
            Encoding.ASCII.GetBytes(platformStr, 0, platformStr.Length, platformBytes, 0);
            writer.Write(platformBytes);

            archive._nextFileRefPos = stream.Position;
            ArchiveFileReference fileRef = new ArchiveFileReference(0, 0, 0, 0, 0, 0, 0);
            for (int i = 0; i < maxFiles; i++)
            {
                archive.WriteFileReference(writer, fileRef);
            }

            return archive;
        }

        public static Archive Open(string baseFilePath, ArchiveMetaData? metaData, CdcGame game)
        {
            Archive archive = InstantiateArchive(baseFilePath, metaData, game);

            using Stream stream = archive.OpenPart(0, baseFilePath, FileMode.Open, FileAccess.ReadWrite);
            BinaryReader reader = new(stream);
            
            ArchiveHeader header = reader.ReadStruct<ArchiveHeader>();
            if (header.Magic != 0x53464154)
                throw new InvalidDataException("Invalid magic in tiger file");

            if (header.Version != archive.HeaderVersion)
                throw new NotSupportedException($"Only version {archive.HeaderVersion} archive files are supported");

            archive._numParts = header.NumParts;
            archive._maxFiles = header.NumFiles;
            archive.Id = header.Id;

            if (archive.SupportsSubId)
            {
                archive.SubId = reader.ReadInt32();
            }
            else
            {
                string archiveName = Path.GetFileName(baseFilePath).Replace(".000.tiger", "");
                archive.SubId = CdcGameInfo.Get(game).Languages.IndexOf(l => archiveName.EndsWith(l.Name)) + 1;
            }

            reader.Skip(0x20);

            if (archive.SupportsLanguageList)
            {
                int languageBits = reader.ReadInt32();
                int numLanguages = reader.ReadInt32();
                reader.Skip(numLanguages * 0x18);
            }

            for (int i = 0; i < header.NumFiles; i++)
            {
                archive._fileRefs.Add(archive.ReadFileReference(reader));
            }

            return archive;
        }

        internal static Archive InstantiateArchive(string baseFilePath, ArchiveMetaData? metaData, CdcGame game)
        {
            return game switch
            {
                CdcGame.Tr2013      => new Tr2013Archive(baseFilePath, metaData),
                CdcGame.Rise        => new RiseArchive(baseFilePath, metaData),
                CdcGame.Shadow      => new ShadowArchive(baseFilePath, metaData),
                CdcGame.Avengers    => new AvengersArchive(baseFilePath, metaData),
                _ => throw new NotSupportedException()
            };
        }

        private List<Stream> PartStreams
        {
            get
            {
                if (_partStreams == null)
                {
                    lock (this)
                    {
                        if (_partStreams == null)
                        {
                            _partStreams = [];
                            for (int i = 0; i < _numParts; i++)
                            {
                                string partFilePath = GetPartFilePath(i);
                                _partStreams.Add(OpenPart(i, partFilePath, FileMode.Open, ModName != null ? FileAccess.ReadWrite : FileAccess.Read));
                            }
                        }
                    }
                }
                return _partStreams;
            }
        }

        internal virtual Stream OpenPart(int partIdx, string filePath, FileMode mode, FileAccess access)
        {
            return File.Open(filePath, mode, access, FileShare.ReadWrite);
        }

        public string BaseFilePath
        {
            get;
        }

        public int Id
        {
            get;
            private set;
        }

        public int SubId
        {
            get;
            private set;
        }

        public ArchiveMetaData? MetaData
        {
            get;
        }

        public string? ModName
        {
            get
            {
                string? entry = MetaData?.CustomEntries.FirstOrDefault(c => c.StartsWith("mod:"));
                return entry?.Substring("mod:".Length);
            }
        }

        public IReadOnlyCollection<ArchiveFileReference> Files => _fileRefs;

        public ResourceCollection? GetResourceCollection(ArchiveFileReference fileRef)
        {
            if (fileRef.ArchiveId != Id || fileRef.ArchiveSubId != SubId)
                throw new ArgumentException("File reference does not match archive", nameof(fileRef));

            string? filePath = CdcHash.Lookup(fileRef.NameHash, Game, true);
            if (filePath == null || Path.GetExtension(filePath) != ".drm")
                return null;

            Stream stream = PartStreams[fileRef.ArchivePart];
            stream.Position = fileRef.Offset;
            try
            {
                return ResourceCollection.Open(fileRef.NameHash, fileRef.Locale, stream, Game);
            }
            catch
            {
                return null;
            }
        }

        public Stream OpenFile(ArchiveFileReference fileRef)
        {
            if (fileRef.ArchiveId != Id || fileRef.ArchiveSubId != SubId)
                throw new ArgumentException("File reference does not match archive", nameof(fileRef));

            Stream partStream = PartStreams[fileRef.ArchivePart];
            if (ArchiveDecompressionStream.IsCompressed(partStream, fileRef))
                return new ArchiveDecompressionStream(partStream, fileRef, true);
            else
                return new WindowedStream(partStream, fileRef.Offset, fileRef.Length);
        }

        public Stream OpenResource(ResourceReference resourceRef)
        {
            if (resourceRef.ArchiveId != Id || resourceRef.ArchiveSubId != SubId)
                throw new ArgumentException("Resource reference does not match archive", nameof(resourceRef));

            Stream stream = PartStreams[resourceRef.ArchivePart];
            if (resourceRef.RefDefinitionsSize + resourceRef.BodySize == resourceRef.Length)
                return new WindowedStream(stream, resourceRef.Offset, resourceRef.Length);

            return new ArchiveDecompressionStream(stream, resourceRef, true);
        }

        public ArchiveFileReference AddFile(ArchiveFileKey identifier, byte[] data)
        {
            return AddFile(identifier.NameHash, identifier.Locale, data);
        }

        public ArchiveFileReference AddFile(ArchiveFileKey identifier, ArraySegment<byte> data)
        {
            return AddFile(identifier.NameHash, identifier.Locale, data);
        }

        public ArchiveFileReference AddFile(ulong nameHash, ulong locale, byte[] data)
        {
            return AddFile(nameHash, locale, new ArraySegment<byte>(data));
        }

        public ArchiveFileReference AddFile(ulong nameHash, ulong locale, ArraySegment<byte> data)
        {
            if (_fileRefs.Count == _maxFiles)
                throw new InvalidOperationException("Can't add any further files");

            if (data.Array == null)
                throw new ArgumentNullException("No file data specified", nameof(data));

            Stream contentStream = PartStreams.Last();
            BinaryWriter contentWriter = new BinaryWriter(contentStream);
            contentStream.Position = contentStream.Length;
            contentWriter.Align(ContentAlignment);

            uint offset = (uint)contentStream.Length;
            contentWriter.Write(data.Array, data.Offset, data.Count);

            Stream indexStream = PartStreams[0];
            BinaryWriter indexWriter = new BinaryWriter(indexStream);

            ArchiveFileReference fileRef = new(nameHash, locale, Id, SubId, 0, offset, (uint)data.Count);
            indexStream.Position = _nextFileRefPos;
            WriteFileReference(indexWriter, fileRef);
            _nextFileRefPos = indexStream.Position;

            _fileRefs.Add(fileRef);
            indexStream.Position = 0xC;
            indexWriter.Write(_fileRefs.Count);

            return fileRef;
        }

        public ArchiveBlobReference AddResource(Stream contentStream)
        {
            int archivePart = PartStreams.Count - 1;
            Stream partStream = PartStreams[archivePart];
            partStream.Position = partStream.Length;

            BinaryWriter writer = new BinaryWriter(partStream);

            uint resourceOffset = ((uint)partStream.Position + (uint)ContentAlignment - 1) & ~((uint)ContentAlignment - 1);

            if (_hasWrittenResources)
            {
                uint nextMarkerOffset = (uint)partStream.Position - 0x10;
                partStream.Position -= 0xC;
                writer.Write(resourceOffset - nextMarkerOffset);
                partStream.Position += 8;
            }

            writer.Align(ContentAlignment);

            WriteResource(contentStream, writer);
            uint resourceLength = (uint)partStream.Position - resourceOffset;

            writer.Align(0x10);
            writer.Write(0x5458454E);       // "NEXT" marker
            writer.Write(0);                // Offset to next resource (0 for last)
            writer.Align(0x10);
            _hasWrittenResources = true;

            return new ArchiveBlobReference(Id, SubId, archivePart, resourceOffset, resourceLength);
        }

        private void WriteResource(Stream contentStream, BinaryWriter writer)
        {
            if (contentStream is ArchiveDecompressionStream resourceStream)
            {
                Stream archivePartStream = resourceStream.ArchivePartStream;
                ArchiveBlobReference resourceRef = resourceStream.BlobRef;
                archivePartStream.CopySegmentTo(resourceRef.Offset, resourceRef.Length, writer.BaseStream);
                return;
            }

            Stream partStream = writer.BaseStream;

            int numChunks = (int)(contentStream.Length / MaxResourceChunkSize);
            if (contentStream.Length % MaxResourceChunkSize != 0)
                numChunks++;

            writer.Write(0x4D524443);      // Magic
            writer.Write(0);               // Type
            writer.Write(numChunks);
            writer.Write(0);

            int chunkSizesOffset = (int)partStream.Position;
            for (int i = 0; i < numChunks; i++)
            {
                writer.Write(0);
                writer.Write(0);
            }
            writer.Align(0x10);

            long remainingSize = contentStream.Length;
            byte[] uncompressedChunkData = new byte[MaxResourceChunkSize];
            for (int i = 0; i < numChunks; i++)
            {
                int uncompressedChunkSize = (int)Math.Min(remainingSize, MaxResourceChunkSize);
                contentStream.Read(uncompressedChunkData, 0, uncompressedChunkSize);

                int chunkOffset = (int)partStream.Position;
                writer.Write((byte)0x78);
                writer.Write((byte)0x9C);
                using (DeflateStream compressor = new DeflateStream(partStream, CompressionMode.Compress, true))
                {
                    compressor.Write(uncompressedChunkData, 0, uncompressedChunkSize);
                }
                writer.Write(0);
                int compressedChunkSize = (int)partStream.Position - chunkOffset;

                partStream.Position = chunkSizesOffset;
                writer.Write(uncompressedChunkSize << 8 | 0x02);
                writer.Write(compressedChunkSize);
                chunkSizesOffset += 8;

                partStream.Position = partStream.Length;
                writer.Align(0x10);

                remainingSize -= uncompressedChunkSize;
            }
        }

        public string GetPartFilePath(int part)
        {
            return BaseFilePath.Replace(".000.tiger", $".{part:d03}.tiger");
        }

        public void CloseStreams()
        {
            if (_partStreams == null)
                return;

            foreach (Stream stream in _partStreams)
            {
                stream.Dispose();
            }
            _partStreams = null;
        }

        public void Delete()
        {
            if (SubId == 0)
            {
                if (MetaData != null)
                    File.Delete(MetaData.FilePath);

                File.Delete(SpecMasksToc.GetFilePathForArchive(BaseFilePath));
            }

            Dispose();
            for (int i = 0; i < _numParts; i++)
            {
                File.Delete(GetPartFilePath(i));
            }
        }

        public override string ToString()
        {
            return Path.GetFileName(BaseFilePath);
        }

        public void Dispose()
        {
            CloseStreams();
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ArchiveHeader
        {
            public int Magic;
            public int Version;
            public int NumParts;
            public int NumFiles;
            public int Id;
        }
    }
}
