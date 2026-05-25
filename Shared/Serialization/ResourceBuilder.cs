using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Cdc.Rise;
using TrRebootTools.Shared.Cdc.Shadow;
using TrRebootTools.Shared.Cdc.Tr2013;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Serialization
{
    public abstract class ResourceBuilder
    {
        public static ResourceBuilder Create(ResourceKey resourceKey, CdcGame game)
        {
            return game switch
            {
                CdcGame.Tr2013 => new Tr2013ResourceBuilder(resourceKey),
                CdcGame.Rise   => new RiseResourceBuilder(resourceKey),
                CdcGame.Shadow => new ShadowResourceBuilder(resourceKey)
            };
        }

        public static ResourceBuilder Create(ResourceDescriptor resourceDesc, CdcGame game)
        {
            return game switch
            {
                CdcGame.Tr2013 => new Tr2013ResourceBuilder(resourceDesc),
                CdcGame.Rise   => new RiseResourceBuilder(resourceDesc),
                CdcGame.Shadow => new ShadowResourceBuilder(resourceDesc)
            };
        }

        private static readonly Encoding StringEncoding = new UTF8Encoding(false);

        private protected readonly ResourceKey _resourceKey;
        private protected readonly ResourceDescriptor? _resourceDesc;
        private protected readonly int _refSize;

        private protected readonly SortedList<int, ResourceRef> _refs = new();
        private protected readonly MemoryStream _body = new();

        private List<(ResourceRef Ref, Action<ResourceBuilder> WriteValue)>? _pendingWrites;

        protected ResourceBuilder(ResourceKey resourceKey, CdcGame game)
        {
            _resourceKey = resourceKey;
            _refSize = CdcGameInfo.Get(game).PointerSize;
        }

        protected ResourceBuilder(ResourceDescriptor resourceDesc, CdcGame game)
            : this((ResourceKey)resourceDesc, game)
        {
            _resourceDesc = resourceDesc;
        }

        public int Position
        {
            get => (int)_body.Position;
            set => _body.Position = value;
        }

        public void Write(bool value)
        {
            Write((byte)(value ? 1 : 0));
        }

        public void Write(byte value)
        {
            Span<byte> data = [value];
            _body.Write(data);
        }

        public void Write(short value)
        {
            Span<byte> data = stackalloc byte[2];
            BitConverter.TryWriteBytes(data, value);
            _body.Write(data);
        }

        public void Write(ushort value)
        {
            Span<byte> data = stackalloc byte[2];
            BitConverter.TryWriteBytes(data, value);
            _body.Write(data);
        }

        public void Write(int value)
        {
            Span<byte> data = stackalloc byte[4];
            BitConverter.TryWriteBytes(data, value);
            _body.Write(data);
        }

        public void Write(uint value)
        {
            Span<byte> data = stackalloc byte[4];
            BitConverter.TryWriteBytes(data, value);
            _body.Write(data);
        }

        public void Write(long value)
        {
            Span<byte> data = stackalloc byte[8];
            BitConverter.TryWriteBytes(data, value);
            _body.Write(data);
        }

        public void Write(ulong value)
        {
            Span<byte> data = stackalloc byte[8];
            BitConverter.TryWriteBytes(data, value);
            _body.Write(data);
        }

        public void Write(float value)
        {
            Span<byte> data = stackalloc byte[4];
            BitConverter.TryWriteBytes(data, value);
            _body.Write(data);
        }

        public void Write(string? value)
        {
            if (value != null)
                _body.Write(StringEncoding.GetBytes(value));

            Write((byte)0);
        }

        public void Write(ResourceRef? resourceRef)
        {
            if (resourceRef != null)
                _refs[(int)_body.Position] = resourceRef;
            else
                _refs.Remove((int)_body.Position);

            if (_refSize == 4)
                Write(0);
            else
                Write(0L);
        }

        public ResourceRef WriteNewRef(int targetOffset = -1)
        {
            ResourceRef resourceRef = new(_resourceKey, targetOffset);
            Write(resourceRef);
            return resourceRef;
        }

        public void Write<T>(T value)
            where T : IResourceStruct
        {
            value.Write(this);
        }

        public void Write<T>(IEnumerable<T> values)
            where T : IResourceStruct
        {
            foreach (T value in values)
            {
                value.Write(this);
            }
        }

        public void Write<T>(T value, int fieldSelector)
            where T : IResourceUnion
        {
            value.Write(this, fieldSelector);
        }

        public void Write<T>(IEnumerable<T> values, int fieldSelector)
            where T : IResourceUnion
        {
            foreach (T value in values)
            {
                value.Write(this, fieldSelector);
            }
        }

        public void WritePadding(int length)
        {
            Span<byte> padding = stackalloc byte[length];
            _body.Write(padding);
        }

        public void AddPendingWrite(ResourceRef resourceRef, Action<ResourceBuilder> writeValue)
        {
            (_pendingWrites ??= []).Add((resourceRef, writeValue));
        }

        public void HandlePendingWrites()
        {
            while (_pendingWrites != null)
            {
                var writes = _pendingWrites;
                _pendingWrites = null;
                foreach ((ResourceRef resourceRef, Action<ResourceBuilder> writeValue) in writes)
                {
                    resourceRef.Offset = Position;
                    writeValue(this);
                }
            }
        }

        public void Build(Stream output)
        {
            ResourceRefCounts counts = new();
            foreach (ResourceRef reference in _refs.Values)
            {
                if (reference.Equals(_resourceKey))
                    counts.NumInternalRefs++;
                else if (reference.Offset == 0)
                    counts.NumPackedExternalRefs++;
                else
                    counts.NumWideExternalRefs++;
            }

            BinaryWriter writer = new(output);
            writer.WriteStruct(counts);

            _body.TryGetBuffer(out ArraySegment<byte> body);

            foreach ((int refOffset, ResourceRef internalRef) in _refs.Where(p => p.Value.IsInternal))
            {
                writer.Write(refOffset);
                writer.Write(internalRef.Offset);
            }

            foreach ((int refOffset, ResourceRef wideExternalRef) in _refs.Where(p => p.Value.IsExternal && p.Value.Offset > 0))
            {
                WriteWideExternalRefDefinition(writer, refOffset, wideExternalRef, body);
            }

            foreach ((int refOffset, ResourceRef packedExternalRef) in _refs.Where(p => p.Value.IsExternal && p.Value.Offset == 0))
            {
                WritePackedExternalRefDefinition(writer, refOffset, packedExternalRef, body);
            }

            writer.Write(body);
        }

        protected virtual void WritePackedExternalRefDefinition(BinaryWriter writer, int refOffset, ResourceRef resourceRef, ArraySegment<byte> body)
        {
            writer.Write((int)resourceRef.ExternalResource!.Value.Type << 25 | refOffset / 4);
            BitConverter.TryWriteBytes(body.AsSpan(refOffset, 4), resourceRef.ExternalResource.Value.Id);
        }

        protected abstract void WriteWideExternalRefDefinition(BinaryWriter writer, int refOffset, ResourceRef resourceRef, ArraySegment<byte> body);
    }
}
