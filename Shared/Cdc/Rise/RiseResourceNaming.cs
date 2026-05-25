using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TrRebootTools.Shared.Serialization;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Cdc.Rise
{
    internal class RiseResourceNaming : ResourceNaming
    {
        private static readonly Dictionary<(ResourceType, ResourceSubType), string[]> _mappings =
            new()
            {
                { (ResourceType.Animation, 0),                      [".tr10anim", ".anim"] },
                { (ResourceType.AnimationLib, 0),                   [".tr10animlib", ".trigger"] },
                { (ResourceType.CollisionModel, 0),                 [".tr10cmodel", ".tr2cmesh"] },
                { (ResourceType.Dtp, 0),                            [".tr10dtp", ".dtp"] },
                { (ResourceType.Dtp, ResourceSubType.StreamLayer),  [".tr10layer"] },
                { (ResourceType.GlobalContentReference, 0),         [".tr10contentref", ".object"] },
                { (ResourceType.Material, 0),                       [".tr10material", ".material"] },
                { (ResourceType.Model, ResourceSubType.CubeLut),    [".tr10cubelut"] },
                { (ResourceType.Model, ResourceSubType.Model),      [".tr10modeldata", ".tr2mesh"] },
                { (ResourceType.Model, ResourceSubType.ModelData),  [".tr10modeldata", ".tr2mesh"] },
                { (ResourceType.Model, ResourceSubType.ShResource), [".tr10shresource"] },
                { (ResourceType.ObjectReference, 0),                [".tr10objectref", ".grplist"] },
                { (ResourceType.BlendShapeDriver, 0),               [".tr10drivers", ".psdres"] },
                { (ResourceType.Script, 0),                         [".tr10script", ".script"] },
                { (ResourceType.ShaderLib, 0),                      [".tr10shaderlib"] },
                { (ResourceType.SoundBank, 0),                      [".tr10sound", ".sound"] },
                { (ResourceType.Texture, 0),                        [".dds", ".tr2pcd"] }
            };

        private ResourceCollection? _lastAnimCollection = null;
        private readonly Dictionary<int, string> _animNames = new();

        protected override Dictionary<(ResourceType, ResourceSubType), string[]> Mappings => _mappings;

        protected internal override string? ReadOriginalFilePathInstance(ArchiveSet archiveSet, ResourceCollection collection, ResourceDescriptor resource)
        {
            if (archiveSet.GetArchive(resource.ArchiveId, resource.ArchiveSubId) == null)
                return null;

            switch (resource.Type)
            {
                case ResourceType.Animation:
                    UpdateAnimationNameCache(archiveSet, collection);
                    return _animNames.GetValueOrDefault(resource.Id);

                case ResourceType.Material:
                case ResourceType.Model:
                case ResourceType.SoundBank:
                case ResourceType.Texture:
                {
                    using Stream stream = archiveSet.OpenResource(resource);
                    return ReadOriginalFilePathInstance(stream, resource.Type);
                }

                default:
                    return null;
            }
        }

        protected internal override string? ReadOriginalFilePathInstance(Stream stream, ResourceType type)
        {
            return type switch
            {
                ResourceType.Material  => ReadMaterialOriginalFilePath(stream),
                ResourceType.Model     => ReadModelOriginalFilePath(stream),
                ResourceType.SoundBank => ReadSoundOriginalFilePath(stream),
                ResourceType.Texture   => ReadTextureOriginalFilePath(stream),
                _ => null,
            };
        }

        private void UpdateAnimationNameCache(ArchiveSet archiveSet, ResourceCollection collection)
        {
            if (collection == _lastAnimCollection)
                return;

            _animNames.Clear();
            _lastAnimCollection = collection;

            foreach (ResourceDescriptor libRef in collection.Resources.Where(r => r.Type == ResourceType.AnimationLib))
            {
                MemoryStream stream = archiveSet.LoadResource(libRef);
                RiseAnimationLibrary lib = new(stream);
                foreach ((int animId, string animName) in lib.Animations)
                {
                    _animNames[animId] = animName;
                }
            }
        }

        private static string? ReadMaterialOriginalFilePath(Stream stream)
        {
            ResourceReader reader = ResourceReader.Create(stream, CdcGame.Rise);
            reader.Seek(0x48);
            ResourceRef? nameRef = reader.ReadRef();
            if (nameRef == null)
                return null;

            reader.Seek(nameRef);
            return reader.ReadString();
        }

        private static string? ReadModelOriginalFilePath(Stream stream)
        {
            ResourceReader reader = ResourceReader.Create(stream, CdcGame.Rise);
            ResourceRef? modelDataRef = reader.ReadRef();
            if (modelDataRef == null)
                return null;

            reader.Seek(modelDataRef + 0xC0);
            int nameOffset = reader.ReadInt32();
            if (nameOffset <= 0)
                return null;

            reader.Seek(modelDataRef + nameOffset);
            return reader.ReadString();
        }

        private static string? ReadSoundOriginalFilePath(Stream stream)
        {
            BinaryReader reader = new(stream);
            reader.ReadBytes(0x14);

            string magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
            if (magic != "FSB5")
                return null;

            int version = reader.ReadInt32();
            int numSamples = reader.ReadInt32();
            int sampleHeaderSize = reader.ReadInt32();
            int nameTableSize = reader.ReadInt32();
            int dataSize = reader.ReadInt32();
            int mode = reader.ReadInt32();
            reader.ReadBytes(0x20);

            if (numSamples != 1 || nameTableSize == 0)
                return null;

            reader.ReadBytes(sampleHeaderSize);
            reader.ReadBytes(4);
            return reader.ReadZeroTerminatedString();
        }

        private static string? ReadTextureOriginalFilePath(Stream stream)
        {
            using BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
            var header = reader.ReadStruct<CdcTexture.CdcTextureHeader>();
            if (header.Magic != CdcTexture.CdcTextureMagic)
                return null;

            if ((header.Flags & 0x2000) == 0)
                return null;

            return reader.ReadZeroTerminatedString();
        }
    }
}
