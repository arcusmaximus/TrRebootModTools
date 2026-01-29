using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TrRebootTools.Shared.Cdc.Rise;
using TrRebootTools.Shared.Cdc.Shadow;
using TrRebootTools.Shared.Cdc.Tr2013;

namespace TrRebootTools.Shared.Cdc
{
    public abstract class LocalsBin
    {
        public static LocalsBin Open(Stream stream, CdcGame version)
        {
            return version switch
            {
                CdcGame.Tr2013 => new Tr2013LocalsBin(stream),
                CdcGame.Rise   => new RiseLocalsBin(stream),
                CdcGame.Shadow => new ShadowLocalsBin(stream),
                _ => throw new NotSupportedException()
            };
        }

        private byte[] _encodeBuffer = new byte[4096];

        public int LanguageId
        {
            get;
            set;
        }

        public string[]? InputSpecificKeys
        {
            get;
            set;
        }

        public Dictionary<string, string> Strings
        {
            get;
        } = new();

        public abstract void Write(Stream stream);

        protected string ReadString(byte[] data, int pos)
        {
            int terminatorPos = pos;
            while (data[terminatorPos] != 0)
            {
                terminatorPos++;
            }
            return Encoding.UTF8.GetString(data, pos, terminatorPos - pos);
        }

        protected void WriteString(BinaryWriter writer, string str)
        {
            int numBytes;
            try
            {
                numBytes = Encoding.UTF8.GetBytes(str, 0, str.Length, _encodeBuffer, 0);
            }
            catch (ArgumentException)
            {
                _encodeBuffer = new byte[Encoding.UTF8.GetByteCount(str)];
                numBytes = Encoding.UTF8.GetBytes(str, 0, str.Length, _encodeBuffer, 0);
            }
            writer.Write(_encodeBuffer, 0, numBytes);
            writer.Write((byte)0);
        }
    }
}
