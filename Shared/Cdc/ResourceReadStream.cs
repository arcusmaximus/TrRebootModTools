using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace TrRebootTools.Shared.Cdc
{
    public class ResourceReadStream : Stream
    {
        private readonly bool _leaveOpen;

        private readonly List<Chunk> _chunks = [];
        private long _position;
        private readonly long _length;

        private int _currentChunkIndex;
        private Stream? _currentChunkStream;
        private int _remainingChunkSize;

        public ResourceReadStream(Stream archivePartStream, ResourceReference resourceRef, bool leaveOpen)
        {
            ArchivePartStream = archivePartStream;
            ResourceReference = resourceRef;
            _leaveOpen = leaveOpen;

            ArchivePartStream.Position = resourceRef.Offset;
            BinaryReader reader = new BinaryReader(archivePartStream);
            int magic = reader.ReadInt32();
            if (magic != 0x4D524443)
                throw new InvalidDataException();

            int type = reader.ReadInt32();
            int numChunks = reader.ReadInt32();
            reader.ReadInt32();

            int chunkOffset = (int)archivePartStream.Position + 8 * numChunks;
            if (chunkOffset % 16 != 0)
                chunkOffset += 16 - chunkOffset % 16;

            for (int i = 0; i < numChunks; i++)
            {
                int uncompressedSize = reader.ReadInt32() >> 8;
                int compressedSize = reader.ReadInt32();
                _chunks.Add(new Chunk(chunkOffset, uncompressedSize, compressedSize));

                chunkOffset += compressedSize;
                if (chunkOffset % 16 != 0)
                    chunkOffset += 16 - chunkOffset % 16;

                _length += uncompressedSize;
            }

            _currentChunkIndex = -1;
        }

        public Stream ArchivePartStream
        {
            get;
        }

        public ResourceReference ResourceReference
        {
            get;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_remainingChunkSize == 0)
                NextChunk();

            int totalReadSize = 0;
            while (count > 0 && _currentChunkStream != null)
            {
                int chunkReadSize = Math.Min(count, _remainingChunkSize);
                _currentChunkStream.Read(buffer, offset, chunkReadSize);
                offset += chunkReadSize;
                count -= chunkReadSize;
                totalReadSize += chunkReadSize;

                _remainingChunkSize -= chunkReadSize;
                if (_remainingChunkSize == 0)
                    NextChunk();
            }
            _position += totalReadSize;
            return totalReadSize;
        }

        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        private void NextChunk()
        {
            if (_currentChunkIndex == _chunks.Count)
                return;

            if (_currentChunkStream != null && _currentChunkStream != ArchivePartStream)
                _currentChunkStream.Dispose();

            _currentChunkStream = null;

            _currentChunkIndex++;
            if (_currentChunkIndex == _chunks.Count)
            {
                _remainingChunkSize = 0;
                return;
            }

            Chunk chunk = _chunks[_currentChunkIndex];
            ArchivePartStream.Position = chunk.Offset;
            _remainingChunkSize = chunk.UncompressedSize;
            if (chunk.CompressedSize == chunk.UncompressedSize)
            {
                _currentChunkStream = ArchivePartStream;
            }
            else
            {
                ArchivePartStream.Position += 2;
                using Stream decompressStream = new DeflateStream(ArchivePartStream, CompressionMode.Decompress, true);
                byte[] data = new BinaryReader(decompressStream).ReadBytes(chunk.UncompressedSize);
                _currentChunkStream = new MemoryStream(data);
            }
        }

        private readonly struct Chunk
        {
            public Chunk(int offset, int uncompressedSize, int compressedSize)
            {
                Offset = offset;
                UncompressedSize = uncompressedSize;
                CompressedSize = compressedSize;
            }

            public int Offset
            {
                get;
            }

            public int UncompressedSize
            {
                get;
            }

            public int CompressedSize
            {
                get;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (_currentChunkStream != null && _currentChunkStream != ArchivePartStream)
                _currentChunkStream?.Dispose();

            if (!_leaveOpen)
                ArchivePartStream.Dispose();
        }
    }
}
