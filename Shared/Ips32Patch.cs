using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TrRebootTools.Shared
{
    public class Ips32Patch
    {
        private static readonly byte[] StartMagic = Encoding.ASCII.GetBytes("IPS32");
        private static readonly uint EndMagic = 0x45454F46;

        public record Patch(uint Position, byte[] Data);

        public Ips32Patch()
        {
        }

        public Ips32Patch(Stream stream)
        {
            BinaryReader reader = new(stream);
            byte[] startMagic = reader.ReadBytes(StartMagic.Length);
            if (!startMagic.SequenceEqual(StartMagic))
                throw new InvalidDataException("Invalid start magic in IPS32 patch");

            while (stream.Position < stream.Length)
            {
                uint position = FlipInt32(reader.ReadUInt32());
                if (position == EndMagic)
                    break;

                ushort length = FlipInt16(reader.ReadUInt16());
                byte[] data = reader.ReadBytes(length);
                Add(position, data);
            }
        }

        public List<Patch> Patches { get; } = new();

        public void Add(uint position, byte[] data)
        {
            Patches.Add(new(position, data));
        }

        public void Apply(byte[] data)
        {
            foreach (Patch patch in Patches)
            {
                Array.Copy(patch.Data, 0, data, patch.Position, patch.Data.Length);
            }
        }

        public void Write(Stream stream)
        {
            BinaryWriter writer = new(stream);
            writer.Write(StartMagic);
            foreach (Patch patch in Patches)
            {
                writer.Write(FlipInt32(patch.Position));
                writer.Write(FlipInt16((ushort)patch.Data.Length));
                writer.Write(patch.Data);
            }
            writer.Write(FlipInt32(EndMagic));
        }

        private static uint FlipInt32(uint value)
        {
            uint b0 = value & 0xFF;
            uint b1 = (value >> 8) & 0xFF;
            uint b2 = (value >> 16) & 0xFF;
            uint b3 = (value >> 24) & 0xFF;
            return (b0 << 24) | (b1 << 16) | (b2 << 8) | b3;
        }

        private static ushort FlipInt16(ushort value)
        {
            ushort b0 = (ushort)(value & 0xFF);
            ushort b1 = (ushort)(value >> 8);
            return (ushort)((b0 << 8) | b1);
        }
    }
}
