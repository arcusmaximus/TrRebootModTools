using System;
using System.IO;
using TrRebootTools.Shared.Serialization;

namespace TrRebootTools.Shared.Cdc.Tr2013
{
    internal class Tr2013ResourceReader : ResourceReader
    {
        public Tr2013ResourceReader(Stream stream)
            : base(stream, CdcGame.Tr2013)
        {
        }

        public Tr2013ResourceReader(ResourceDescriptor? resourceDesc, Stream stream)
            : base(resourceDesc, stream, CdcGame.Tr2013)
        {
        }

        protected override int WideExternalRefSize => 8;

        protected override (int RefOffset, PackedExternalRef Ref) ReadPackedExternalRefDefinition()
        {
            (int refOffset, PackedExternalRef resourceRef) = base.ReadPackedExternalRefDefinition();
            if (_resourceDesc != null)
                resourceRef = Tr2013ResourceCollection.AdjustResourceKeyAfterRead(_resourceDesc.ArchiveId, resourceRef);
            
            return (refOffset, resourceRef);
        }

        protected override (int RefOffset, ResourceRef Ref) ReadWideExternalRefDefinition()
        {
            ulong refDefsValue = ReadUInt64();
            int refOffset = (int)((refDefsValue >> 16) & 0x7FFFFF) * 4;
            int targetOffset = (int)(refDefsValue >> 39);

            uint bodyValue = BitConverter.ToUInt32(_data.AsSpan(RefDefinitionsSize + refOffset, 4));
            ResourceType type = (ResourceType)(bodyValue >> 24);
            int id = (int)(bodyValue & 0xFFFFFF);
            ResourceKey resourceKey = new(type, id);
            if (_resourceDesc != null)
                resourceKey = Tr2013ResourceCollection.AdjustResourceKeyAfterRead(_resourceDesc.ArchiveId, resourceKey);

            return (refOffset, new ResourceRef(resourceKey, targetOffset));
        }
    }
}
