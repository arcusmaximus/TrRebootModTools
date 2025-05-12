using System.IO;

namespace TrRebootTools.Shared.Cdc.Tr2013
{
    internal class Tr2013Material : Material
    {
        public Tr2013Material(int id, Stream stream)
            : base(id, stream, CdcGame.Tr2013)
        {
        }

        protected override int NumPasses => 10;
        protected override int PassRefsOffset => 0x48;

        protected override int PsConstantsCountOffset => 0x14;
        protected override int PsConstantsRefOffset => 0x18;

        protected override int VsConstantsCountOffset => 0x24;
        protected override int VsConstantsRefOffset => 0x28;
    }
}
