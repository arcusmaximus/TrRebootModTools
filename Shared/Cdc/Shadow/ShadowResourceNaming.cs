using System.Collections.Generic;

namespace TrRebootTools.Shared.Cdc.Shadow
{
    internal class ShadowResourceNaming : ResourceNaming
    {
        private static readonly Dictionary<(ResourceType, ResourceSubType), string[]> _mappings =
            new()
            {
                { (ResourceType.Animation, 0), [".tr11anim"] },
                { (ResourceType.AnimationLib, 0), [".tr11animlib"] },
                { (ResourceType.CollisionMesh, 0), [".tr11cmesh"] },
                { (ResourceType.Dtp, 0), [".tr11dtp"] },
                { (ResourceType.Dtp, ResourceSubType.StreamLayer), [".tr11layer"] },
                { (ResourceType.GlobalContentReference, 0), [".tr11contentref"] },
                { (ResourceType.Material, 0), [".tr11material"] },
                { (ResourceType.Model, ResourceSubType.CubeLut), [".tr11cubelut"] },
                { (ResourceType.Model, ResourceSubType.Model), [".tr11model"] },
                { (ResourceType.Model, ResourceSubType.ModelData), [".tr11modeldata"] },
                { (ResourceType.Model, ResourceSubType.ShResource), [".tr11shresource"] },
                { (ResourceType.ObjectReference, 0), [".tr11objectref"] },
                { (ResourceType.PsdRes, 0), [".tr11psdres"] },
                { (ResourceType.Script, 0), [".tr11script"] },
                { (ResourceType.ShaderLib, 0), [".tr11shaderlib"] },
                { (ResourceType.SoundBank, 0), [".bnk"] },
                { (ResourceType.Texture, 0), [".dds"] }
            };

        protected override Dictionary<(ResourceType, ResourceSubType), string[]> Mappings => _mappings;
    }
}
