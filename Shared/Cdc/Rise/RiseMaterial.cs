using System.IO;

namespace TrRebootTools.Shared.Cdc.Rise
{
    internal class RiseMaterial : Material
    {
        public RiseMaterial(int id, Stream stream)
            : base(id, stream, CdcGame.Rise)
        {
        }

        protected override int NumPasses => 10;
        protected override int PassRefsOffset => 0x58;

        protected override int PsConstantsCountOffset => 0x20;
        protected override int PsConstantsRefOffset => 0x28;

        protected override int VsConstantsCountOffset => 0x40;
        protected override int VsConstantsRefOffset => 0x48;
    }
}
