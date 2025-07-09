from enum import IntEnum
from io_scene_tr_reboot.util.BinaryReader import BinaryReader
from io_scene_tr_reboot.util.IoHelper import IoHelper

class DdsConstants:
    DDS_FOURCC = 0x00000004
    DDS_RGB = 0x00000040

class DdsType(IntEnum):
    OTHER  = 0
    RGB    = 1
    NORMAL = 2

class DdsFile:
    @staticmethod
    def get_type(file_path: str) -> DdsType:
        header: bytes
        with IoHelper.open_read(file_path) as file:
            header = file.read(0x94)

        reader = BinaryReader(header)
        if bytes(reader.read_bytes(4)) != b"DDS ":
            raise Exception("Invalid DDS magic")

        # Skip to DDS_PIXELFORMAT structure
        reader.skip(0x48)

        pixel_format_flags = reader.read_uint32_at(4)
        if pixel_format_flags & DdsConstants.DDS_RGB:
            r_bitmask = reader.read_uint32_at(0x10)
            g_bitmask = reader.read_uint32_at(0x14)
            b_bitmask = reader.read_uint32_at(0x18)
            if r_bitmask > 0 and g_bitmask > 0:
                if b_bitmask > 0:
                    return DdsType.RGB
                else:
                    return DdsType.NORMAL
            else:
                return DdsType.OTHER
        elif pixel_format_flags & DdsConstants.DDS_FOURCC:
            fourcc = bytes(reader.read_bytes_at(8, 4))
            match fourcc:
                case b"DXT1" | b"DXT2" | b"DXT3" | b"DXT4" | b"DXT5" | b"BC4U" | b"BC4S" | b"ATI2" | b"BC5S":
                    return DdsType.RGB

                case b"DX10":
                    # Skip to DDS_HEADER_DXT10 structure
                    reader.skip(0x20 + 0x14)
                    dxgi_format = reader.read_int32_at(0)
                    if 1  <= dxgi_format <= 14 or \
                       23 <= dxgi_format <= 32 or \
                       70 <= dxgi_format <= 78 or \
                       87 <= dxgi_format <= 93 or \
                       dxgi_format == 115:
                        return DdsType.RGB
                    elif 15 <= dxgi_format <= 18 or \
                         33 <= dxgi_format <= 38 or \
                         48 <= dxgi_format <= 52 or \
                         82 <= dxgi_format <= 84:
                        return DdsType.NORMAL
                    else:
                        return DdsType.OTHER

                case _:
                    return DdsType.OTHER
        else:
            return DdsType.OTHER
