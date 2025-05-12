using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using TrRebootTools.Shared.Cdc.Rise;
using TrRebootTools.Shared.Cdc.Shadow;
using TrRebootTools.Shared.Cdc.Tr2013;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Cdc
{
    public abstract class ResourceRefDefinitions
    {
        public record struct InternalRef(int RefPosition, int TargetPosition);
        public record struct ExternalRef(int RefPosition, ResourceKey ResourceKey);

        public static int ReadHeaderAndGetSize(Stream stream, CdcGame game)
        {
            return Create(null, stream, true, game).Size;
        }

        protected readonly ResourceReference _resourceRef;
        protected readonly Stream _stream;
        protected readonly BinaryReader _reader;
        protected readonly BinaryWriter _writer;

        protected readonly Dictionary<int, int> _internalRefDefinitionPosByRefPos = new();
        protected readonly Dictionary<int, int> _packedExternalRefDefinitionPosByRefPos = new();

        public static ResourceRefDefinitions Create(ResourceReference resourceRef, Stream stream, CdcGame game)
        {
            return Create(resourceRef, stream, false, game);
        }

        private static ResourceRefDefinitions Create(ResourceReference resourceRef, Stream stream, bool readSizeOnly, CdcGame game)
        {
            return game switch
            {
                CdcGame.Tr2013 => new Tr2013ResourceRefDefinitions(resourceRef, stream, readSizeOnly),
                CdcGame.Rise => new RiseResourceRefDefinitions(resourceRef, stream, readSizeOnly),
                CdcGame.Shadow => new ShadowResourceRefDefinitions(resourceRef, stream, readSizeOnly)
            };
        }

        protected ResourceRefDefinitions(ResourceReference resourceRef, Stream stream, bool readSizeOnly)
        {
            _stream = stream;
            _resourceRef = resourceRef;
            _reader = new BinaryReader(stream);
            if (stream.CanWrite)
                _writer = new BinaryWriter(stream);

            RefCounts counts = _reader.ReadStruct<RefCounts>();

            Size = GetSize(counts);
            if (readSizeOnly)
                return;

            if (!stream.CanSeek)
                throw new ArgumentException("Stream must be seekable", nameof(stream));

            for (int i = 0; i < counts.NumInternalRefs; i++)
            {
                int refDefinitionPos = (int)stream.Position;
                int refOffset = _reader.ReadInt32();
                int targetOffset = _reader.ReadInt32();
                _internalRefDefinitionPosByRefPos[Size + refOffset] = refDefinitionPos;
            }

            stream.Position += counts.NumWideExternalRefs * WideExternalRefSize;
            stream.Position += counts.NumIntPatches * 4;
            stream.Position += counts.NumShortPatches * 8;

            for (int i = 0; i < counts.NumPackedExternalRefs; i++)
            {
                int refDefinitionPos = (int)stream.Position;
                int packedRef = _reader.ReadInt32();
                int refOffset = (packedRef & 0x1FFFFFF) * 4;
                _packedExternalRefDefinitionPosByRefPos[Size + refOffset] = refDefinitionPos;
            }
        }

        public int Size
        {
            get;
        }

        private int GetSize(in RefCounts refCounts)
        {
            return Marshal.SizeOf<RefCounts>() + refCounts.NumInternalRefs * 8 +
                                                 refCounts.NumWideExternalRefs * WideExternalRefSize +
                                                 refCounts.NumIntPatches * 4 +
                                                 refCounts.NumShortPatches * 8 +
                                                 refCounts.NumPackedExternalRefs * 4;
        }

        protected abstract int WideExternalRefSize { get; }

        public IEnumerable<InternalRef> InternalRefs
        {
            get
            {
                foreach (int refPos in _internalRefDefinitionPosByRefPos.Keys)
                {
                    int targetPos = GetInternalRefTarget(refPos).Value;
                    yield return new InternalRef(refPos, targetPos);
                }
            }
        }

        public int? GetInternalRefTarget(int refPos)
        {
            if (!_internalRefDefinitionPosByRefPos.TryGetValue(refPos, out int refDefinitionPos))
                return null;

            long prevPos = _stream.Position;
            _stream.Position = refDefinitionPos + 4;
            int targetPos = Size + _reader.ReadInt32();
            _stream.Position = prevPos;
            return targetPos;
        }

        public void SetInternalRefTarget(int refPos, int targetPos)
        {
            int refDefinitionPos = _internalRefDefinitionPosByRefPos[refPos];
            _stream.Position = refDefinitionPos + 4;
            _writer.Write(targetPos - Size);
        }

        public IEnumerable<ExternalRef> ExternalRefs
        {
            get
            {
                foreach (int refPos in _packedExternalRefDefinitionPosByRefPos.Keys)
                {
                    ResourceKey resourceKey = GetExternalRefTarget(refPos).Value;
                    yield return new ExternalRef(refPos, resourceKey);
                }
            }
        }

        public virtual ResourceKey? GetExternalRefTarget(int refPos)
        {
            if (!_packedExternalRefDefinitionPosByRefPos.TryGetValue(refPos, out int refDefinitionPos))
                return null;

            long prevPos = _stream.Position;

            _stream.Position = refDefinitionPos;
            int packedRef = _reader.ReadInt32();
            ResourceType resourceType = (ResourceType)(packedRef >> 25);

            _stream.Position = refPos;
            int resourceId = (int)(_reader.ReadUInt32() & 0x7FFFFFFF);

            _stream.Position = prevPos;

            return new ResourceKey(resourceType, resourceId);
        }

        public virtual void SetExternalRefTarget(int refPos, ResourceKey resourceKey)
        {
            if (!_packedExternalRefDefinitionPosByRefPos.ContainsKey(refPos))
                throw new ArgumentException();

            _stream.Position = refPos;
            _writer.Write(resourceKey.Id);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RefCounts
        {
            public int NumInternalRefs;
            public int NumWideExternalRefs;
            public int NumIntPatches;
            public int NumShortPatches;
            public int NumPackedExternalRefs;
        }
    }
}
