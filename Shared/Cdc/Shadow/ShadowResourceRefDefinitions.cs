using System.IO;

namespace TrRebootTools.Shared.Cdc.Shadow
{
    internal class ShadowResourceRefDefinitions : ResourceRefDefinitions
    {
        public ShadowResourceRefDefinitions(ResourceReference? resourceRef, Stream stream, bool readSizeOnly)
            : base(resourceRef, stream, readSizeOnly)
        {
        }

        protected override int WideExternalRefSize => 0x10;
    }
}
