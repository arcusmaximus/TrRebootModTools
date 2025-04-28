using System.Collections.Generic;

namespace TrRebootTools.Shared.Cdc.Tr2013
{
    internal class Tr2013ResourceNaming : ResourceNaming
    {
        private static readonly Dictionary<(ResourceType, ResourceSubType), string[]> _mappings =
            new()
            {
                { (ResourceType.Animation, 0), [".tr9anim"] },
                { (ResourceType.AnimationLib, 0), [".tr9animlib"] },
                { (ResourceType.CollisionMesh, 0), [".tr9cmesh"] },
                { (ResourceType.Dtp, 0), [".tr9dtp"] },
                { (ResourceType.GlobalContentReference, 0), [".tr9objectref"] },
                { (ResourceType.Material, 0), [".tr9material"] },
                { (ResourceType.Model, 0), [".tr9modeldata", ".mesh"] },
                { (ResourceType.ObjectReference, 0), [".tr9objectref"] },
                { (ResourceType.PsdRes, 0), [".tr9psdres"] },
                { (ResourceType.Script, 0), [".tr9script"] },
                { (ResourceType.ShaderLib, 0), [".tr9shaderlib"] },
                { (ResourceType.SoundBank, 0), [".tr9sound"] },
                { (ResourceType.Texture, 0), [".dds", ".pcd9"] }
            };

        protected override Dictionary<(ResourceType, ResourceSubType), string[]> Mappings => _mappings;
    }
}
