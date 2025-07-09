using System.Collections.Generic;
using System.IO;
using System.Text;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Cdc.Rise
{
    internal class RiseResourceNaming : ResourceNaming
    {
        private static readonly Dictionary<(ResourceType, ResourceSubType), string[]> _mappings =
            new()
            {
                { (ResourceType.Animation, 0), [".tr10anim", ".anim"] },
                { (ResourceType.AnimationLib, 0), [".tr10animlib", ".trigger"] },
                { (ResourceType.CollisionMesh, 0), [".tr10cmesh", ".tr2cmesh"] },
                { (ResourceType.Dtp, 0), [".tr10dtp", ".dtp"] },
                { (ResourceType.Dtp, ResourceSubType.StreamLayer), [".tr10layer"] },
                { (ResourceType.GlobalContentReference, 0), [".tr10contentref", ".object"] },
                { (ResourceType.Material, 0), [".tr10material", ".material"] },
                { (ResourceType.Model, ResourceSubType.CubeLut), [".tr10cubelut"] },
                { (ResourceType.Model, ResourceSubType.Model), [".tr10modeldata", ".tr2mesh"] },
                { (ResourceType.Model, ResourceSubType.ModelData), [".tr10modeldata", ".tr2mesh"] },
                { (ResourceType.Model, ResourceSubType.ShResource), [".tr10shresource"] },
                { (ResourceType.ObjectReference, 0), [".tr10objectref", ".grplist"] },
                { (ResourceType.PsdRes, 0), [".tr10psdres", ".psdres"] },
                { (ResourceType.Script, 0), [".tr10script", ".script"] },
                { (ResourceType.ShaderLib, 0), [".tr10shaderlib"] },
                { (ResourceType.SoundBank, 0), [".tr10sound", ".sound"] },
                { (ResourceType.Texture, 0), [".dds", ".tr2pcd"] }
            };

        protected override Dictionary<(ResourceType, ResourceSubType), string[]> Mappings => _mappings;

        protected override string ReadOriginalFilePathInstance(ArchiveSet archiveSet, ResourceReference resourceRef)
        {
            switch (resourceRef.Type)
            {
                case ResourceType.Material:
                case ResourceType.Model:
                case ResourceType.SoundBank:
                case ResourceType.Texture:
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

        protected override string ReadOriginalFilePathInstance(Stream stream, ResourceType type)
        {
            return type switch
            {
                ResourceType.Material => ReadMaterialOriginalFilePath(stream),
                ResourceType.Model => ReadModelOriginalFilePath(stream),
                ResourceType.SoundBank => ReadSoundOriginalFilePath(stream),
                ResourceType.Texture => ReadTextureOriginalFilePath(stream),
                _ => null,
            };
        }

        private static string ReadMaterialOriginalFilePath(Stream stream)
        {
            MemoryStream memStream = new();
            stream.CopyTo(memStream);
            memStream.Position = 0;

            ResourceRefDefinitions refDefs = ResourceRefDefinitions.Create(null, memStream, CdcGame.Rise);
            int? namePos = refDefs.GetInternalRefTarget(refDefs.Size + 0x48);
            if (namePos == null)
                return null;

            memStream.Position = namePos.Value;
            return new BinaryReader(memStream).ReadZeroTerminatedString();
        }

        private static string ReadModelOriginalFilePath(Stream stream)
        {
            MemoryStream memStream = new();
            stream.CopyTo(memStream);
            memStream.Position = 0;

            ResourceRefDefinitions refDefs = ResourceRefDefinitions.Create(null, memStream, CdcGame.Rise);
            int? modelDataPos = refDefs.GetInternalRefTarget(refDefs.Size);
            if (modelDataPos == null)
                return null;

            BinaryReader reader = new BinaryReader(memStream);
            memStream.Position = modelDataPos.Value + 0xC0;
            int namePos = reader.ReadInt32();
            if (namePos <= 0)
                return null;

            memStream.Position = modelDataPos.Value + namePos;
            return reader.ReadZeroTerminatedString();
        }

        private static string ReadSoundOriginalFilePath(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
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

        private static string ReadTextureOriginalFilePath(Stream stream)
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
