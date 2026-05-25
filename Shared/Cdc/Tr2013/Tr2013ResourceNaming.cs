using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TrRebootTools.Shared.Serialization;
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

        private ResourceCollection? _lastAnimCollection;
        private readonly Dictionary<int, string> _animNames = new();

        protected override Dictionary<(ResourceType, ResourceSubType), string[]> Mappings => _mappings;

        protected internal override string? ReadOriginalFilePathInstance(ArchiveSet archiveSet, ResourceCollection collection, ResourceDescriptor resource)
        {
            switch (resource.Type)
            {
                case ResourceType.Animation:
                    UpdateAnimationNameCache(archiveSet, collection);
                    return _animNames.GetValueOrDefault(resource.Id);

                case ResourceType.SoundBank:
                {
                    if (archiveSet.GetArchive(resource.ArchiveId, resource.ArchiveSubId) == null)
                        return null;

                    using Stream stream = archiveSet.OpenResource(resource);
                    return ReadOriginalFilePathInstance(stream, resource.Type);
                }

                default:
                    return null;
            }
        }

        private void UpdateAnimationNameCache(ArchiveSet archiveSet, ResourceCollection collection)
        {
            if (collection == _lastAnimCollection)
                return;

            _animNames.Clear();
            _lastAnimCollection = collection;

            ResourceDescriptor? objectRefResource = collection.Resources.FirstOrDefault(r => r.Type == ResourceType.GlobalContentReference);
            if (objectRefResource == null)
                return;

            Stream objectRefStream = archiveSet.OpenResource(objectRefResource);
            ResourceReader objectRefReader = ResourceReader.Create(objectRefResource, objectRefStream, CdcGame.Tr2013);
            ResourceRef? objectRef = objectRefReader.ReadRef();
            if (objectRef?.ExternalResource == null)
                return;

            ResourceDescriptor? objectResource = collection.Get(objectRef.ExternalResource.Value);
            if (objectResource == null)
                return;

            Stream objectStream = archiveSet.OpenResource(objectResource);
            Tr2013Object obj = new(objectStream);
            foreach ((int animId, string animName) in obj.Animations)
            {
                _animNames[animId] = animName;
            }
        }

        protected internal override string? ReadOriginalFilePathInstance(Stream stream, ResourceType type)
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
