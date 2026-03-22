using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Cdc.Rise
{
    public class RiseAnimationLibrary
    {
        public RiseAnimationLibrary(Stream stream)
        {
            ResourceRefDefinitions refs = ResourceRefDefinitions.Create(null, stream, CdcGame.Rise);
            BinaryReader reader = new(stream);
            reader.Seek(refs.Size + 0x10);
            int numAnims = reader.ReadInt32();
            reader.Skip(4);
            int? animsPos = refs.GetInternalRefTarget(reader.Tell());
            if (animsPos == null)
                return;

            for (int i = 0; i < numAnims; i++)
            {
                reader.Seek(animsPos.Value + i * 0x18);
                int id = reader.ReadUInt16();
                reader.Skip(0xE);
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
