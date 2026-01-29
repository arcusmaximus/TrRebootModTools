using System.IO;

namespace TrRebootTools.Shared.Cdc.Rise
{
    internal class RiseResourceRefDefinitions : ResourceRefDefinitions
    {
        public RiseResourceRefDefinitions(ResourceReference? resourceRef, Stream stream, bool readSizeOnly)
            : base(resourceRef, stream, readSizeOnly)
        {
        }

        protected override int WideExternalRefSize => 0x10;
    }
}
