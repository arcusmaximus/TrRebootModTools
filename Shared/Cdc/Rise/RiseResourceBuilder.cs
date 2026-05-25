using System;
using System.IO;
using TrRebootTools.Shared.Serialization;

namespace TrRebootTools.Shared.Cdc.Rise
{
    internal class RiseResourceBuilder : ResourceBuilder
    {
        public RiseResourceBuilder(ResourceKey resourceKey, CdcGame game = CdcGame.Rise)
            : base(resourceKey, game)
        {
        }

        public RiseResourceBuilder(ResourceDescriptor resourceDesc, CdcGame game = CdcGame.Rise)
            : base(resourceDesc, game)
        {
        }

        protected override void WriteWideExternalRefDefinition(BinaryWriter writer, int refOffset, ResourceRef resourceRef, ArraySegment<byte> body)
        {
            writer.Write(refOffset);
            writer.Write((int)resourceRef.ExternalResource!.Value.Type);
            writer.Write(resourceRef.ExternalResource!.Value.Id);
            writer.Write(resourceRef.Offset);
        }
    }
}
