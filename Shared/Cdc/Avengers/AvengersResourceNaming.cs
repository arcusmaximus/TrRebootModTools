using System.Collections.Generic;

namespace TrRebootTools.Shared.Cdc.Avengers
{
    internal class AvengersResourceNaming : ResourceNaming
    {
        private static readonly Dictionary<(ResourceType, ResourceSubType), string[]> _mappings =
            new()
            {
                { (ResourceType.Animation, 0),                      [".maanim"] },
                { (ResourceType.AnimationLib, 0),                   [".maanimlib"] },
                { (ResourceType.CollisionModel, 0),                 [".macmodel"] },
                { (ResourceType.Dtp, 0),                            [".madtp"] },
                { (ResourceType.Dtp, ResourceSubType.StreamLayer),  [".malayer"] },
                { (ResourceType.GlobalContentReference, 0),         [".macontentref"] },
                { (ResourceType.Material, 0),                       [".mamaterial"] },
                { (ResourceType.Model, ResourceSubType.CubeLut),    [".macubelut"] },
                { (ResourceType.Model, ResourceSubType.Model),      [".mamodel"] },
                { (ResourceType.Model, ResourceSubType.ModelData),  [".mamodeldata"] },
                { (ResourceType.Model, ResourceSubType.ShResource), [".mashresource"] },
                { (ResourceType.ObjectReference, 0),                [".maobjectref"] },
                { (ResourceType.BlendShapeDriver, 0),               [".madrivers"] },
                { (ResourceType.Script, 0),                         [".mascript"] },
                { (ResourceType.ShaderLib, 0),                      [".mashaderlib"] },
                { (ResourceType.SoundBank, 0),                      [".bnk"] },
                { (ResourceType.Texture, 0),                        [".dds"] }
            };

        protected override Dictionary<(ResourceType, ResourceSubType), string[]> Mappings => _mappings;
    }
}
