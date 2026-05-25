using System.IO;
using TrRebootTools.Shared.Serialization;

namespace TrRebootTools.Shared.Cdc.Rise
{
    internal class RiseResourceReader : ResourceReader
    {
        public RiseResourceReader(Stream stream, CdcGame game = CdcGame.Rise)
            : base(stream, game)
        {
        }

        public RiseResourceReader(ResourceDescriptor? resourceDesc, Stream stream, CdcGame game = CdcGame.Rise)
            : base(resourceDesc, stream, game)
        {
        }

        protected override int WideExternalRefSize => 0x10;

        protected override (int RefOffset, ResourceRef Ref) ReadWideExternalRefDefinition()
        {
            int refOffset = ReadInt32();
            ResourceType type = (ResourceType)ReadInt32();
            int id = ReadInt32();
            int targetOffset = ReadInt32();
            return (refOffset, new ResourceRef(type, id, targetOffset));
        }
    }
}
