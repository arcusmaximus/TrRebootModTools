using System.Collections.Generic;
using System.IO;
using TrRebootTools.Shared.Serialization;

namespace TrRebootTools.Shared.Cdc.Rise
{
    public class RiseAnimationLibrary
    {
        public RiseAnimationLibrary(Stream stream)
        {
            ResourceReader reader = ResourceReader.Create(stream, CdcGame.Rise);
            reader.Seek(0x10);
            int numAnims = reader.ReadInt32();
            reader.Skip(4);
            ResourceRef? animsRef = reader.ReadRef();
            if (animsRef == null)
                return;

            reader.Seek(animsRef);
            for (int i = 0; i < numAnims; i++)
            {
                int id = reader.ReadUInt16();
                reader.Skip(0xE);
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
