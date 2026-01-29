using System;
using System.IO;
using System.Text;

namespace TrRebootTools.Shared.Cdc.Shadow
{
    public class ShadowLocalsBin : LocalsBin
    {
        public ShadowLocalsBin(Stream stream)
        {
            byte[] data = new byte[stream.Length];
            stream.Read(data, 0, data.Length);

            LanguageId = BitConverter.ToInt32(data, 0);
            int numStrings = BitConverter.ToInt32(data, 4);
            for (int i = 0; i < numStrings; i++)
            {
                int keyPos = BitConverter.ToInt32(data, 8 + 8 * i);
                if (keyPos == 0)
                    continue;

                int separatorPos = keyPos;
                while (data[separatorPos] != ' ')
                {
                    separatorPos++;
                }

                string key = Encoding.UTF8.GetString(data, keyPos, separatorPos - keyPos);
                string value = ReadString(data, separatorPos + 1);
                Strings.Add(key, value);
            }
        }

        public override void Write(Stream stream)
        {
            byte[] encodeBuffer = new byte[4096];

            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(LanguageId);
            writer.Write(Strings.Count);
            for (int i = 0; i < Strings.Count; i++)
            {
                writer.Write(0L);
            }

            int stringIdx = 0;
            foreach ((string key, string value) in Strings)
            {
                stream.Position = 8 + 8 * stringIdx;
                writer.Write(stream.Length);
                stream.Position = stream.Length;

                int keyLength = Encoding.UTF8.GetBytes(key, 0, key.Length, encodeBuffer, 0);
                writer.Write(encodeBuffer, 0, keyLength);
                writer.Write((byte)' ');

                WriteString(writer, value);

                stringIdx++;
            }
        }
    }
}
