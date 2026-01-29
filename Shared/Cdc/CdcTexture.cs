using System;
using System.IO;
using System.Runtime.InteropServices;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Cdc
{
    public class CdcTexture
    {
        internal const uint CdcTextureMagic = 0x39444350;

        [StructLayout(LayoutKind.Sequential)]
        public struct CdcTextureHeader
        {
            public uint Magic;
            public uint Format;
            public uint Size;
            public uint HighResMipMapLevels;
            public ushort Width;
            public ushort Height;
            public ushort VolumeDepth;
            public byte Depth;
            public byte MipMapLevels;
            public ushort Flags;
            public byte Class;
            public byte TileMode;
        }

        private const uint DDS_MAGIC = 0x20534444;

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct DDS_HEADER
        {
            public uint size;
            public uint flags;
            public uint height;
            public uint width;
            public uint pitchOrLinearSize;
            public uint depth;
            public uint mipMapCount;
            public fixed uint reserved1[11];
            public DDS_PIXELFORMAT ddspf;
            public uint caps;
            public uint caps2;
            public uint caps3;
            public uint caps4;
            public uint reserved2;
        };

        [StructLayout(LayoutKind.Sequential)]
        private struct DDS_PIXELFORMAT
        {
            public uint size;
            public uint flags;
            public uint fourCC;
            public uint RGBBitCount;
            public uint RBitMask;
            public uint GBitMask;
            public uint BBitMask;
            public uint ABitMask;
        }

        private struct DDS_HEADER_DXT10
        {
            public uint dxgiFormat;
            public uint resourceDimension;
            public uint miscFlag;
            public uint arraySize;
            public uint miscFlags2;
        }

        private const uint DDS_HEADER_FLAGS_TEXTURE = 0x00001007;
        private const uint DDS_HEADER_FLAGS_MIPMAP = 0x00020000;
        private const uint DDS_HEADER_FLAGS_VOLUME = 0x00800000;
        private const uint DDS_HEADER_FLAGS_PITCH = 0x00000008;
        private const uint DDS_HEADER_FLAGS_LINEARSIZE = 0x00080000;

        private const uint DDS_SURFACE_FLAGS_TEXTURE = 0x00001000;
        private const uint DDS_SURFACE_FLAGS_MIPMAP = 0x00400008;
        private const uint DDS_SURFACE_FLAGS_CUBEMAP = 0x00000008;

        private const uint DDS_DIMENSION_TEXTURE1D = 2;
        private const uint DDS_DIMENSION_TEXTURE2D = 3;
        private const uint DDS_DIMENSION_TEXTURE3D = 4;

        private const uint DDS_FOURCC = 0x00000004;
        private const uint DDS_RGB = 0x00000040;
        private const uint DDS_RGBA = 0x00000041;

        private const uint DDS_RESOURCE_MISC_TEXTURECUBE = 0x4;

        private const uint DDS_CUBEMAP_POSITIVEX = 0x00000600;
        private const uint DDS_CUBEMAP_NEGATIVEX = 0x00000a00;
        private const uint DDS_CUBEMAP_POSITIVEY = 0x00001200;
        private const uint DDS_CUBEMAP_NEGATIVEY = 0x00002200;
        private const uint DDS_CUBEMAP_POSITIVEZ = 0x00004200;
        private const uint DDS_CUBEMAP_NEGATIVEZ = 0x00008200;
        private const uint DDS_CUBEMAP_ALLFACES = DDS_CUBEMAP_POSITIVEX | DDS_CUBEMAP_NEGATIVEX |
                                                  DDS_CUBEMAP_POSITIVEY | DDS_CUBEMAP_NEGATIVEY |
                                                  DDS_CUBEMAP_POSITIVEZ | DDS_CUBEMAP_NEGATIVEZ;

        public const uint DXGI_FORMAT_UNKNOWN = 0;
        public const uint DXGI_FORMAT_R11G11B10_FLOAT = 26;
        public const uint DXGI_FORMAT_R8G8B8A8_UNORM = 28;
        public const uint DXGI_FORMAT_R8G8B8A8_UNORM_SRGB = 29;
        public const uint DXGI_FORMAT_R8G8_UNORM = 49;
        public const uint DXGI_FORMAT_R8_UNORM = 61;
        public const uint DXGI_FORMAT_BC1_UNORM = 71;
        public const uint DXGI_FORMAT_BC1_UNORM_SRGB = 72;
        public const uint DXGI_FORMAT_BC2_UNORM = 74;
        public const uint DXGI_FORMAT_BC2_UNORM_SRGB = 75;
        public const uint DXGI_FORMAT_BC3_UNORM = 77;
        public const uint DXGI_FORMAT_BC3_UNORM_SRGB = 78;
        public const uint DXGI_FORMAT_BC4_UNORM = 80;
        public const uint DXGI_FORMAT_BC5_UNORM = 83;
        public const uint DXGI_FORMAT_B8G8R8A8_UNORM = 87;
        public const uint DXGI_FORMAT_B8G8R8A8_UNORM_SRGB = 91;
        public const uint DXGI_FORMAT_BC6H_UF16 = 95;
        public const uint DXGI_FORMAT_BC7_UNORM = 98;
        public const uint DXGI_FORMAT_BC7_UNORM_SRGB = 99;

        private const uint FOURCC_FORMAT_DXT1 = 0x31545844;
        private const uint FOURCC_FORMAT_DXT3 = 0x33545844;
        private const uint FOURCC_FORMAT_DXT5 = 0x35545844;

        private CdcTexture()
        {
        }

        public CdcTextureHeader Header;
        public byte[]? Data;

        public static CdcTexture Read(Stream stream)
        {
            using BinaryReader reader = new BinaryReader(stream);

            CdcTexture texture = new();
            texture.Header = reader.ReadStruct<CdcTextureHeader>();
            if (texture.Header.Magic != CdcTextureMagic)
                throw new InvalidDataException();

            if ((texture.Header.Flags & 0x2000) != 0)
                reader.ReadBytes(0x100);

            texture.Data = reader.ReadBytes((int)texture.Header.Size);
            return texture;
        }

        public static CdcTexture ReadFromDds(Stream stream, CdcGame game)
        {
            BinaryReader reader = new BinaryReader(stream);

            uint magic = reader.ReadUInt32();
            if (magic != DDS_MAGIC)
                throw new InvalidDataException($"Not a valid DDS file");

            DDS_HEADER ddsHeader = reader.ReadStruct<DDS_HEADER>();
            uint format;
            if ((ddsHeader.ddspf.flags & DDS_FOURCC) != 0)
            {
                if (ddsHeader.ddspf.fourCC == MakeFourCC("DX10"))
                {
                    DDS_HEADER_DXT10 dx10 = reader.ReadStruct<DDS_HEADER_DXT10>();
                    format = dx10.dxgiFormat;
                }
                else if (CdcGameInfo.Get(game).UsesFourccTextureFormat)
                {
                    format = ddsHeader.ddspf.fourCC;
                }
                else
                {
                    format = GetDxgiFormat(ddsHeader.ddspf);
                }
            }
            else
            {
                format = GetDxgiFormat(ddsHeader.ddspf);
            }
            if (format == DXGI_FORMAT_UNKNOWN)
                throw new NotSupportedException($"Unsupported DDS format");

            byte[] data = reader.ReadBytes((int)(stream.Length - stream.Position));
            return new CdcTexture
            {
                Header = new CdcTextureHeader
                {
                    Magic = CdcTextureMagic,
                    Format = format,
                    Size = (uint)data.Length,
                    HighResMipMapLevels = 0,
                    Width = (ushort)ddsHeader.width,
                    Height = (ushort)ddsHeader.height,
                    VolumeDepth = 1,
                    MipMapLevels = (byte)ddsHeader.mipMapCount,
                    Flags = 0x27,
                    Class = 1
                },
                Data = data
            };
        }

        public void Write(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.WriteStruct(Header);
            writer.Write(Data ?? []);
        }

        public void WriteAsDds(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);

            writer.Write(DDS_MAGIC);
            bool isCubeMap = (Header.Flags & 0x8000) != 0;

            DDS_HEADER header =
                new DDS_HEADER
                {
                    size = (uint)Marshal.SizeOf<DDS_HEADER>(),
                    flags = DDS_HEADER_FLAGS_TEXTURE | (Header.MipMapLevels > 1 ? DDS_HEADER_FLAGS_MIPMAP : 0),
                    height = Header.Height,
                    width = Header.Width,
                    depth = Header.VolumeDepth,
                    mipMapCount = isCubeMap ? 1u : Header.MipMapLevels,
                    caps = DDS_SURFACE_FLAGS_TEXTURE
                                | (Header.MipMapLevels > 1 ? DDS_SURFACE_FLAGS_MIPMAP : 0)
                                | (isCubeMap ? DDS_SURFACE_FLAGS_CUBEMAP : 0),
                    caps2 = isCubeMap ? DDS_CUBEMAP_ALLFACES : 0,
                    ddspf = GetDdsPixelFormat(Header.Format)
                };
            writer.WriteStruct(header);

            if ((header.ddspf.flags & DDS_FOURCC) != 0 && header.ddspf.fourCC == MakeFourCC("DX10"))
            {
                DDS_HEADER_DXT10 dx10 =
                    new DDS_HEADER_DXT10
                    {
                        dxgiFormat = MapSrgbFormatToRegular(Header.Format),
                        resourceDimension = DDS_DIMENSION_TEXTURE2D,
                        miscFlag = isCubeMap ? DDS_RESOURCE_MISC_TEXTURECUBE : 0,
                        arraySize = 1
                    };
                writer.WriteStruct(dx10);
            }

            writer.Write(Data ?? []);
        }

        private static uint GetDxgiFormat(DDS_PIXELFORMAT pixelFormat)
        {
            if ((pixelFormat.flags & DDS_RGB) != 0)
            {
                if (pixelFormat.RGBBitCount == 8 &&
                    pixelFormat.RBitMask == 0xFF)
                {
                    return DXGI_FORMAT_R8_UNORM;
                }

                if (pixelFormat.RGBBitCount == 16 &&
                    pixelFormat.RBitMask == 0x00FF &&
                    pixelFormat.GBitMask == 0xFF00)
                {
                    return DXGI_FORMAT_R8G8_UNORM;
                }

                if (pixelFormat.RGBBitCount == 32 &&
                    pixelFormat.BBitMask == 0x000000FF &&
                    pixelFormat.GBitMask == 0x0000FF00 &&
                    pixelFormat.RBitMask == 0x00FF0000 &&
                    pixelFormat.ABitMask == 0xFF000000)
                {
                    return DXGI_FORMAT_B8G8R8A8_UNORM;
                }

                throw new NotSupportedException(
                    $"DDS format not recognized\r\n" +
                    $"RGBBitCount = {pixelFormat.RGBBitCount}\r\n" +
                    $"RBitMask = {pixelFormat.RBitMask:X}\r\n" +
                    $"GBitmask = {pixelFormat.GBitMask:X}\r\n" +
                    $"BBitmask = {pixelFormat.BBitMask:X}\r\n" +
                    $"ABitmask = {pixelFormat.ABitMask:X}"
                );
            }
            else if ((pixelFormat.flags & DDS_FOURCC) != 0)
            {
                string fourCC = ParseFourCC(pixelFormat.fourCC);
                switch (fourCC)
                {
                    case "DXT1":
                        return DXGI_FORMAT_BC1_UNORM;

                    case "DXT3":
                        return DXGI_FORMAT_BC2_UNORM;

                    case "DXT5":
                        return DXGI_FORMAT_BC3_UNORM;

                    case "BC4U":
                    case "ATI1":
                        return DXGI_FORMAT_BC4_UNORM;

                    case "BC5U":
                        return DXGI_FORMAT_BC5_UNORM;

                    default:
                        throw new NotSupportedException($"DDS format \"{fourCC}\" not recognized.");
                }
            }
            return DXGI_FORMAT_UNKNOWN;
        }

        private static DDS_PIXELFORMAT GetDdsPixelFormat(uint dxgiFormat)
        {
            switch (dxgiFormat)
            {
                case DXGI_FORMAT_R8_UNORM:
                    return new DDS_PIXELFORMAT
                    {
                        size = (uint)Marshal.SizeOf<DDS_PIXELFORMAT>(),
                        flags = DDS_RGB,
                        RGBBitCount = 8,
                        RBitMask = 0xFF
                    };

                /*
            case DXGI_FORMAT_R8G8_UNORM:
                return new DDS_PIXELFORMAT
                {
                    size = (uint)Marshal.SizeOf<DDS_PIXELFORMAT>(),
                    flags = DDS_RGB,
                    RGBBitCount = 16,
                    RBitMask = 0x00FF,
                    GBitMask = 0xFF00
                };
                */

                case DXGI_FORMAT_B8G8R8A8_UNORM:
                    return new DDS_PIXELFORMAT
                    {
                        size = (uint)Marshal.SizeOf<DDS_PIXELFORMAT>(),
                        flags = DDS_RGBA,
                        RGBBitCount = 32,
                        BBitMask = 0x000000FF,
                        GBitMask = 0x0000FF00,
                        RBitMask = 0x00FF0000,
                        ABitMask = 0xFF000000
                    };

                case DXGI_FORMAT_BC1_UNORM:
                case DXGI_FORMAT_BC1_UNORM_SRGB:
                case FOURCC_FORMAT_DXT1:
                    return new DDS_PIXELFORMAT
                    {
                        size = (uint)Marshal.SizeOf<DDS_PIXELFORMAT>(),
                        flags = DDS_FOURCC,
                        fourCC = MakeFourCC("DXT1")
                    };

                case DXGI_FORMAT_BC2_UNORM:
                case DXGI_FORMAT_BC2_UNORM_SRGB:
                case FOURCC_FORMAT_DXT3:
                    return new DDS_PIXELFORMAT
                    {
                        size = (uint)Marshal.SizeOf<DDS_PIXELFORMAT>(),
                        flags = DDS_FOURCC,
                        fourCC = MakeFourCC("DXT3")
                    };

                case DXGI_FORMAT_BC3_UNORM:
                case DXGI_FORMAT_BC3_UNORM_SRGB:
                case FOURCC_FORMAT_DXT5:
                    return new DDS_PIXELFORMAT
                    {
                        size = (uint)Marshal.SizeOf<DDS_PIXELFORMAT>(),
                        flags = DDS_FOURCC,
                        fourCC = MakeFourCC("DXT5")
                    };

                default:
                    return new DDS_PIXELFORMAT
                    {
                        size = (uint)Marshal.SizeOf<DDS_PIXELFORMAT>(),
                        flags = DDS_FOURCC,
                        fourCC = MakeFourCC("DX10")
                    };
            }
        }

        public static uint MapSrgbFormatToRegular(uint dxgiFormat)
        {
            // As Blender doesn't support the SRGB variants
            return dxgiFormat switch
            {
                DXGI_FORMAT_R8G8B8A8_UNORM_SRGB => DXGI_FORMAT_R8G8B8A8_UNORM,
                DXGI_FORMAT_BC1_UNORM_SRGB => DXGI_FORMAT_BC1_UNORM,
                DXGI_FORMAT_BC2_UNORM_SRGB => DXGI_FORMAT_BC2_UNORM,
                DXGI_FORMAT_BC3_UNORM_SRGB => DXGI_FORMAT_BC3_UNORM,
                DXGI_FORMAT_BC7_UNORM_SRGB => DXGI_FORMAT_BC7_UNORM,
                _ => dxgiFormat
            };
        }

        public static uint MapRegularFormatToSrgb(uint dxgiFormat)
        {
            return dxgiFormat switch
            {
                DXGI_FORMAT_R8G8B8A8_UNORM => DXGI_FORMAT_R8G8B8A8_UNORM_SRGB,
                DXGI_FORMAT_BC1_UNORM => DXGI_FORMAT_BC1_UNORM_SRGB,
                DXGI_FORMAT_BC2_UNORM => DXGI_FORMAT_BC2_UNORM_SRGB,
                DXGI_FORMAT_BC3_UNORM => DXGI_FORMAT_BC3_UNORM_SRGB,
                DXGI_FORMAT_BC7_UNORM => DXGI_FORMAT_BC7_UNORM_SRGB,
                _ => dxgiFormat
            };
        }

        public static bool IsSrgbFormat(uint dxgiFormat)
        {
            return MapSrgbFormatToRegular(dxgiFormat) != dxgiFormat;
        }

        private static uint MakeFourCC(string chars)
        {
            return chars[0] | (uint)chars[1] << 8 | (uint)chars[2] << 16 | (uint)chars[3] << 24;
        }

        private static unsafe string ParseFourCC(uint fourCC)
        {
            char* pChars = stackalloc char[4];
            pChars[0] = (char)(fourCC & 0xFF);
            pChars[1] = (char)((fourCC >> 8) & 0xFF);
            pChars[2] = (char)((fourCC >> 16) & 0xFF);
            pChars[3] = (char)(fourCC >> 24);
            return new string(pChars, 0, 4);
        }
    }
}
