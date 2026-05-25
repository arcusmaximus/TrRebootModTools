using System.Collections.Generic;
using System.IO;
using TrRebootTools.Shared.Serialization;

namespace TrRebootTools.Shared.Cdc.Tr2013
{
    internal class Tr2013Object
    {
        public Tr2013Object(Stream stream)
        {
            ResourceReader reader = ResourceReader.Create(null, stream, CdcGame.Tr2013);
            ReadAnimations(reader);
        }

        private void ReadAnimations(ResourceReader reader)
        {
            reader.Seek(0x40);
            int numAnims = reader.ReadInt32();
            ResourceRef? animsRef = reader.ReadRef();
            if (animsRef == null)
                return;

            reader.Seek(animsRef);
            for (int i = 0; i < numAnims; i++)
            {
                int id = reader.ReadUInt16();
                reader.Skip(6);
                ResourceRef? nameRef = reader.ReadRef();
                if (nameRef == null)
                    continue;

                using (reader.Seek(nameRef))
                {
                    Animations[id] = reader.ReadString();
                }
            }
        }

        public Dictionary<int, string> Animations
        {
            get;
        } = new();
    }
}
