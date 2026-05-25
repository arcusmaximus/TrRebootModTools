using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Cdc.Rise;
using TrRebootTools.Shared.Cdc.Shadow;
using TrRebootTools.Shared.Cdc.Tr2013;

namespace TrRebootTools.Shared.Serialization
{
    public abstract partial class ResourceReader
    {
        public static ResourceReader Create(Stream stream, CdcGame game)
        {
            return game switch
            {
                CdcGame.Tr2013 => new Tr2013ResourceReader(stream),
                CdcGame.Rise   => new RiseResourceReader(stream),
                CdcGame.Shadow => new ShadowResourceReader(stream)
            };
        }

        public static ResourceReader Create(ResourceDescriptor? resourceDesc, Stream stream, CdcGame game)
        {
            return game switch
            {
                CdcGame.Tr2013 => new Tr2013ResourceReader(resourceDesc, stream),
                CdcGame.Rise   => new RiseResourceReader(resourceDesc, stream),
                CdcGame.Shadow => new ShadowResourceReader(resourceDesc, stream)
            };
        }

        public struct ReturnPoint : IDisposable
        {
            private readonly ResourceReader _reader;
            private readonly int _returnPos;

            public ReturnPoint(ResourceReader reader)
            {
                _reader = reader;
                _returnPos = reader.Position;
            }

            public void Dispose()
            {
                _reader.Position = _returnPos;
            }
        }

        private enum RefType
        {
            None,
            Internal,
            WideExternal,
            PackedExternal
        }

        protected record struct PackedExternalRef(ResourceType Type, int Id)
        {
            public static implicit operator ResourceKey(PackedExternalRef resourceRef)
            {
                return new(resourceRef.Type, resourceRef.Id);
            }

            public static implicit operator PackedExternalRef(ResourceKey resourceKey)
            {
                return new(resourceKey.Type, resourceKey.Id);
            }
        }

        protected readonly ResourceDescriptor? _resourceDesc;
        protected readonly ArraySegment<byte> _data;
        protected readonly int _refSize;

        private uint[]? _refIndicesByOffset;
        private int[]? _internalTargetOffsets;
        private ResourceRef[]? _wideExternalRefs;
        private PackedExternalRef[]? _packedExternalRefs;

        protected ResourceReader(Stream stream, CdcGame game)
        {
            if (stream is not MemoryStream memStream || !memStream.TryGetBuffer(out _data))
            {
                _data = new byte[stream.Length];
                stream.Read(_data);
            }

            CdcGameInfo gameInfo = CdcGameInfo.Get(game);
            _refSize = gameInfo.PointerSize;

            ResourceRefCounts refCounts = ReadStruct<ResourceRefCounts>();
            Position += refCounts.NumInternalRefs * 8 +
                        refCounts.NumWideExternalRefs * WideExternalRefSize +
                        refCounts.NumIntPatches * 4 +
                        refCounts.NumShortPatches * 8 +
                        refCounts.NumPackedExternalRefs * 4;
            RefDefinitionsSize = Position;
        }

        protected ResourceReader(ResourceDescriptor? resourceDesc, Stream stream, CdcGame game)
            : this(stream, game)
        {
            _resourceDesc = resourceDesc;
        }

        public int RefDefinitionsSize
        {
            get;
        }

        public IEnumerable<ResourceRef> ExternalRefs
        {
            get
            {
                InitRefs();
                foreach (ResourceRef resourceRef in _wideExternalRefs)
                {
                    yield return resourceRef;
                }
                foreach (PackedExternalRef packedRef in _packedExternalRefs)
                {
                    yield return new ResourceRef(packedRef);
                }
            }
        }

        public int Position
        {
            get;
            set;
        }

        public int Offset
        {
            get => Position - RefDefinitionsSize;
            set => Position = RefDefinitionsSize + value;
        }

        public void Skip(int length)
        {
            Position += length;
        }

        public ReturnPoint Seek(int offset)
        {
            ReturnPoint returnPoint = new(this);
            Position = RefDefinitionsSize + offset;
            return returnPoint;
        }

        public ReturnPoint Seek(ResourceRef resourceRef)
        {
            if (resourceRef.ExternalResource != null)
                throw new ArgumentException();

            ReturnPoint returnPoint = new(this);
            Position = RefDefinitionsSize + resourceRef.Offset;
            return returnPoint;
        }

        public bool ReadBoolean()
        {
            return ReadByte() != 0;
        }

        public sbyte ReadSByte()
        {
            return (sbyte)_data[Position++];
        }

        public byte ReadByte()
        {
            return _data[Position++];
        }

        public short ReadInt16()
        {
            short value = BitConverter.ToInt16(_data.AsSpan(Position, 2));
            Position += 2;
            return value;
        }

        public ushort ReadUInt16()
        {
            ushort value = BitConverter.ToUInt16(_data.AsSpan(Position, 2));
            Position += 2;
            return value;
        }

        public int ReadInt32()
        {
            int value = BitConverter.ToInt32(_data.AsSpan(Position, 4));
            Position += 4;
            return value;
        }

        public uint ReadUInt32()
        {
            uint value = BitConverter.ToUInt32(_data.AsSpan(Position, 4));
            Position += 4;
            return value;
        }

        public long ReadInt64()
        {
            long value = BitConverter.ToInt64(_data.AsSpan(Position, 8));
            Position += 8;
            return value;
        }

        public ulong ReadUInt64()
        {
            ulong value = BitConverter.ToUInt64(_data.AsSpan(Position, 8));
            Position += 8;
            return value;
        }

        public float ReadSingle()
        {
            float value = BitConverter.ToSingle(_data.AsSpan(Position, 4));
            Position += 4;
            return value;
        }

        public string ReadString()
        {
            int startPos = Position;
            while (_data[Position] != 0)
            {
                Position++;
            }
            string value = Encoding.UTF8.GetString(_data.AsSpan(startPos..Position));
            Position++;
            return value;
        }

        public ResourceRef? ReadRef()
        {
            InitRefs();
            (RefType refType, int refIndex) = GetRefIndex(Position - RefDefinitionsSize);
            Position += _refSize;
            return refType switch
            {
                RefType.None => null,
                RefType.Internal => new ResourceRef(_internalTargetOffsets[refIndex]),
                RefType.WideExternal => _wideExternalRefs[refIndex],
                RefType.PackedExternal => new ResourceRef(_packedExternalRefs[refIndex])
            };
        }

        public ResourceRef<TValue>? ReadRef<TValue>()
        {
            InitRefs();
            (RefType refType, int refIndex) = GetRefIndex(Position - RefDefinitionsSize);
            Position += _refSize;
            return refType switch
            {
                RefType.None => null,
                RefType.Internal => new ResourceRef<TValue>(_internalTargetOffsets[refIndex])
            };
        }

        public ResourceRef?[] ReadRefArray(int count)
        {
            var refs = new ResourceRef?[count];
            for (int i = 0; i < count; i++)
            {
                refs[i] = ReadRef();
            }
            return refs;
        }

        public T ReadStruct<T>()
            where T : IResourceStruct, new()
        {
            T result = new();
            result.Read(this);
            return result;
        }

        public T ReadUnion<T>(int fieldSelector)
            where T : IResourceUnion, new()
        {
            T result = new();
            result.Read(this, fieldSelector);
            return result;
        }

        [MemberNotNull(
            nameof(_refIndicesByOffset),
            nameof(_internalTargetOffsets),
            nameof(_wideExternalRefs),
            nameof(_packedExternalRefs)
        )]
        private void InitRefs()
        {
            if (_refIndicesByOffset != null)
                return;

            int prevPos = Position;
            Position = 0;
            ResourceRefCounts refCounts = ReadStruct<ResourceRefCounts>();

            _refIndicesByOffset = new uint[(_data.Count - RefDefinitionsSize + 3) / 4];

            _internalTargetOffsets = new int[refCounts.NumInternalRefs];
            for (int i = 0; i < refCounts.NumInternalRefs; i++)
            {
                int refOffset = ReadInt32();
                int targetOffset = ReadInt32();
                SetRefIndex(refOffset, RefType.Internal, i);
                _internalTargetOffsets[i] = targetOffset;
            }

            _wideExternalRefs = new ResourceRef[refCounts.NumWideExternalRefs];
            for (int i = 0; i < refCounts.NumWideExternalRefs; i++)
            {
                (int refOffset, ResourceRef resourceRef) = ReadWideExternalRefDefinition();
                SetRefIndex(refOffset, RefType.WideExternal, i);
                _wideExternalRefs[i] = resourceRef;
            }

            Skip(4 * refCounts.NumIntPatches);
            Skip(8 * refCounts.NumShortPatches);

            _packedExternalRefs = new PackedExternalRef[refCounts.NumPackedExternalRefs];
            for (int i = 0; i < refCounts.NumPackedExternalRefs; i++)
            {
                (int refOffset, PackedExternalRef resourceRef) = ReadPackedExternalRefDefinition();
                SetRefIndex(refOffset, RefType.PackedExternal, i);
                _packedExternalRefs[i] = resourceRef;
            }

            Position = prevPos;
        }

        private (RefType, int) GetRefIndex(int refOffset)
        {
            uint value = _refIndicesByOffset![refOffset / 4];
            return ((RefType)(value >> 30), (int)(value & 0x3FFFFFFF));
        }

        private void SetRefIndex(int refOffset, RefType type, int index)
        {
            _refIndicesByOffset![refOffset / 4] = (uint)type << 30 | (uint)index;
        }

        protected abstract int WideExternalRefSize { get; }

        protected virtual (int RefOffset, PackedExternalRef Ref) ReadPackedExternalRefDefinition()
        {
            uint value = ReadUInt32();
            ResourceType resourceType = (ResourceType)(value >> 25);
            int refOffset = (int)(value & 0x1FFFFFF) * 4;
            int resourceId = BitConverter.ToInt32(_data.AsSpan(RefDefinitionsSize + refOffset, 4)) & 0x7FFFFFFF;
            return (refOffset, new PackedExternalRef(resourceType, resourceId));
        }

        protected abstract (int RefOffset, ResourceRef Ref) ReadWideExternalRefDefinition();
    }
}
