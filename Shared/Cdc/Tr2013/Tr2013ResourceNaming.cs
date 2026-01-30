using System.Collections.Generic;
using System.IO;
using System.Text;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Cdc.Tr2013
{
    internal class Tr2013ResourceNaming : ResourceNaming
    {
        private static readonly Dictionary<(ResourceType, ResourceSubType), string[]> _mappings =
            new()
            {
                { (ResourceType.Animation, 0),                  [".tr9anim"] },
                { (ResourceType.AnimationLib, 0),               [".tr9animlib"] },
                { (ResourceType.CollisionModel, 0),             [".tr9cmodel"] },
                { (ResourceType.Dtp, 0),                        [".tr9dtp"] },
                { (ResourceType.Dtp, ResourceSubType.Level),    [".tr9level"] },
                { (ResourceType.GlobalContentReference, 0),     [".tr9objectref"] },
                { (ResourceType.Material, 0),                   [".tr9material"] },
                { (ResourceType.Model, 0),                      [".tr9modeldata", ".mesh"] },
                { (ResourceType.ObjectReference, 0),            [".tr9objectref"] },
                { (ResourceType.BlendShapeDriver, 0),           [".tr9drivers"] },
                { (ResourceType.Script, 0),                     [".tr9script"] },
                { (ResourceType.ShaderLib, 0),                  [".tr9shaderlib"] },
                { (ResourceType.SoundBank, 0),                  [".tr9sound"] },
                { (ResourceType.Texture, 0),                    [".dds", ".pcd9"] }
            };

        protected override Dictionary<(ResourceType, ResourceSubType), string[]> Mappings => _mappings;

        protected override string? ReadOriginalFilePathInstance(ArchiveSet archiveSet, ResourceReference resourceRef)
        {
            switch (resourceRef.Type)
            {
                case ResourceType.SoundBank:
                {
                    if (archiveSet.GetArchive(resourceRef.ArchiveId, resourceRef.ArchiveSubId) == null)
                        return null;

                    using Stream stream = archiveSet.OpenResource(resourceRef);
                    return ReadOriginalFilePathInstance(stream, resourceRef.Type);
                }

                default:
                    return null;
            }
        }

        protected override string? ReadOriginalFilePathInstance(Stream stream, ResourceType type)
        {
            return type switch
            {
                ResourceType.SoundBank => ReadSoundOriginalFilePath(stream),
                _ => null,
            };
        }

        private static string? ReadSoundOriginalFilePath(Stream stream)
        {
            BinaryReader reader = new(stream);
            reader.ReadBytes(0x10);

            string magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
            if (magic != "FSB4")
                return null;

            reader.ReadBytes(0x2E);
            return reader.ReadZeroTerminatedString();
        }
    }
}
