using System.Collections.Generic;

namespace TrRebootTools.Shared.Cdc.Tr2013
{
    internal class Tr2013ResourceNaming : ResourceNaming
    {
        private static readonly Dictionary<(ResourceType, ResourceSubType), string[]> _mappings =
            new()
            {
                { (ResourceType.Animation, 0), new[] { ".tr9anim" } },
                { (ResourceType.AnimationLib, 0), new[] { ".tr9animlib" } },
                { (ResourceType.CollisionMesh, 0), new[] { ".tr9cmesh" } },
                { (ResourceType.Dtp, 0), new[] { ".tr9dtp" } },
                { (ResourceType.GlobalContentReference, 0), new[] { ".tr9objectref" } },
                { (ResourceType.Material, 0), new[] { ".tr9material" } },
                { (ResourceType.Model, 0), new[] { ".tr9modeldata", ".mesh" } },
                { (ResourceType.ObjectReference, 0), new[] { ".tr9objectref" } },
                { (ResourceType.PsdRes, 0), new[] { ".tr9psdres" } },
                { (ResourceType.Script, 0), new[] { ".tr9script" } },
                { (ResourceType.ShaderLib, 0), new[] { ".tr9shaderlib" } },
                { (ResourceType.SoundBank, 0), new[] { ".tr9sound" } },
                { (ResourceType.Texture, 0), new[] { ".dds", ".pcd9" } }
            };

        protected override Dictionary<(ResourceType, ResourceSubType), string[]> Mappings => _mappings;
    }
}
