using TrRebootTools.Shared.Cdc.Rise;

namespace TrRebootTools.Shared.Cdc.Shadow
{
    internal class ShadowResourceBuilder : RiseResourceBuilder
    {
        public ShadowResourceBuilder(ResourceKey resourceKey)
            : base(resourceKey, CdcGame.Shadow)
        {
        }

        public ShadowResourceBuilder(ResourceDescriptor resourceDesc)
            : base(resourceDesc, CdcGame.Shadow)
        {
        }
    }
}
