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
            byte[] data = new byte[(int)stream.Length];
            stream.Read(data, 0, data.Length);

            int nameOffset = data.Length - 1;
            if (data[nameOffset] != 0x00)
                return null;

            while (nameOffset > 0 && data[nameOffset - 1] >= 0x40)
            {
                nameOffset--;
            }
            if (nameOffset == 0 || nameOffset == data.Length - 1)
                return null;

            return Encoding.ASCII.GetString(data, nameOffset, data.Length - 1 - nameOffset);
        }

        private static string ReadModelOriginalFilePath(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            reader.ReadBytes(0x10);
            long numMaterials = reader.ReadInt64();
            if (numMaterials < 0 || numMaterials > 0x1000)
                return null;

            int dataSize = reader.ReadInt32();
            for (int i = 0; i < 5; i++)
            {
                int meshGroup = reader.ReadInt32();
            }
            reader.ReadInt32();
            for (long i = 0; i < numMaterials; i++)
            {
                int material = reader.ReadInt32();
            }
            reader.ReadBytes(8 * 4 + 8);
            long numHashes = reader.ReadInt64();
            if (numHashes < 0 || numHashes > 0x1000)
                return null;

            for (long i = 0; i < numHashes; i++)
            {
                long hash = reader.ReadInt64();
            }

            long offsetMeshStart = numHashes * 4 + 52 + dataSize;
            reader.ReadBytes((int)(offsetMeshStart - stream.Position));

            reader.ReadBytes(0xC0);
            long offsetModelName = reader.ReadInt64();
            if (offsetModelName <= 0 || offsetModelName > 0x1000)
                return null;

            reader.ReadBytes((int)(offsetMeshStart + offsetModelName - stream.Position));
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
            reader.ReadBytes(0x1C);
            return reader.ReadZeroTerminatedString();
        }
    }
}
