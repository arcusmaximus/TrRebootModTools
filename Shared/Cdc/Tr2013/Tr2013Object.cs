using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Cdc.Tr2013
{
    internal class Tr2013Object
    {
        public Tr2013Object(Stream stream)
        {
            ResourceRefDefinitions refs = ResourceRefDefinitions.Create(null, stream, CdcGame.Tr2013);
            BinaryReader reader = new(stream);
            ReadAnimations(reader, refs);
        }

        private void ReadAnimations(BinaryReader reader, ResourceRefDefinitions refs)
        {
            reader.Seek(refs.Size + 0x40);
            int numAnims = reader.ReadInt32();
            int? animsPos = refs.GetInternalRefTarget(reader.Tell());
            if (animsPos == null)
                return;

            for (int i = 0; i < numAnims; i++)
            {
                reader.Seek(animsPos.Value + i * 0xC);
                int id = reader.ReadUInt16();
                reader.Skip(6);
                int? namePos = refs.GetInternalRefTarget(reader.Tell());
                if (namePos == null)
                    continue;

                reader.Seek(namePos.Value);
                string name = reader.ReadZeroTerminatedString();
                Animations[id] = name;
            }
        }

        public Dictionary<int, string> Animations
        {
            get;
        } = new();
    }
}
