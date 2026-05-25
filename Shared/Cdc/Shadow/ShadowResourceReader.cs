using System.IO;
using TrRebootTools.Shared.Cdc.Rise;

namespace TrRebootTools.Shared.Cdc.Shadow
{
    internal class ShadowResourceReader : RiseResourceReader
    {
        public ShadowResourceReader(Stream stream)
            : base(stream, CdcGame.Shadow)
        {
        }

        public ShadowResourceReader(ResourceDescriptor? resourceDesc, Stream stream)
            : base(resourceDesc, stream, CdcGame.Shadow)
        {
        }
    }
}
