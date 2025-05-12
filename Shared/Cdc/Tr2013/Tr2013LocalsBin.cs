using System;
using System.IO;
using System.Linq;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Cdc.Tr2013
{
    public class Tr2013LocalsBin : LocalsBin
    {
        public Tr2013LocalsBin(Stream stream)
        {
            byte[] data = new byte[stream.Length];
            stream.Read(data, 0, data.Length);

            LanguageId = BitConverter.ToInt32(data, 0);
            int numInputSpecificStringIds = BitConverter.ToInt32(data, 4);
            int numStrings = BitConverter.ToInt32(data, 8);

            int readPos = 0xC;
            InputSpecificKeys = new string[numInputSpecificStringIds];
            for (int i = 0; i < numInputSpecificStringIds; i++)
            {
                int key = BitConverter.ToInt32(data, readPos);
                readPos += 4;
                InputSpecificKeys[i] = key.ToString();
            }

            for (int i = 0; i < numStrings; i++)
            {
                int stringPos = BitConverter.ToInt32(data, readPos);
                readPos += PointerSize;
                if (stringPos == 0)
                    continue;

                string str = ReadString(data, stringPos);
                Strings[i.ToString()] = str;
            }
        }

        protected virtual int PointerSize => 4;

        public override void Write(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(LanguageId);
            writer.Write(InputSpecificKeys?.Length ?? 0);
            int numStringPositions = Strings.Count > 0 ? Strings.Max(p => int.Parse(p.Key)) + 1 : 0;
            writer.Write(numStringPositions);

            foreach (string inputSpecificKey in InputSpecificKeys ?? [])
            {
                writer.Write(int.Parse(inputSpecificKey));
            }

            long stringPositionsPos = stream.Position;
            for (int i = 0; i < numStringPositions; i++)
            {
                if (PointerSize == 4)
                    writer.Write(0);
                else
                    writer.Write(0L);
            }

            for (int i = 0; i < numStringPositions; i++)
            {
                string str = Strings.GetOrDefault(i.ToString());
                if (str == null)
                    continue;

                stream.Position = stringPositionsPos + PointerSize * i;
                writer.Write(stream.Length);

                stream.Position = stream.Length;
                WriteString(writer, str);
            }
        }
    }
}
