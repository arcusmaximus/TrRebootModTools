using System;
using System.IO;
using TrRebootTools.Shared.Serialization;

namespace TrRebootTools.Shared.Cdc.Tr2013
{
    internal class Tr2013ResourceBuilder : ResourceBuilder
    {
        public Tr2013ResourceBuilder(ResourceKey resourceKey)
            : base(resourceKey, CdcGame.Tr2013)
        {
        }

        public Tr2013ResourceBuilder(ResourceDescriptor resourceDesc)
            : base(resourceDesc, CdcGame.Tr2013)
        {
        }

        protected override void WritePackedExternalRefDefinition(BinaryWriter writer, int refOffset, ResourceRef resourceRef, ArraySegment<byte> body)
        {
            if (_resourceDesc != null)
                resourceRef = Tr2013ResourceCollection.AdjustResourceKeyBeforeWrite(_resourceDesc.ArchiveId, resourceRef.ExternalResource!.Value);
            
            base.WritePackedExternalRefDefinition(writer, refOffset, resourceRef, body);
        }

        protected override void WriteWideExternalRefDefinition(BinaryWriter writer, int refOffset, ResourceRef resourceRef, ArraySegment<byte> body)
        {
            ulong refDefsValue = ((ulong)resourceRef.Offset << 39) | (((ulong)refOffset / 4) << 16);
            writer.Write(refDefsValue);

            if (_resourceDesc != null)
                resourceRef = Tr2013ResourceCollection.AdjustResourceKeyBeforeWrite(_resourceDesc.ArchiveId, resourceRef.ExternalResource!.Value);

            uint bodyValue = ((uint)resourceRef.ExternalResource!.Value.Type << 24) | (uint)resourceRef.ExternalResource.Value.Id;
            BitConverter.TryWriteBytes(body.AsSpan(refOffset, 4), bodyValue);
        }
    }
}
