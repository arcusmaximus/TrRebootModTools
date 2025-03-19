from ctypes import sizeof
from typing import TYPE_CHECKING
from io_scene_tr_reboot.tr.Material import IMaterialPass, Material
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.ResourceReference import ResourceReference
from io_scene_tr_reboot.util.CStruct import CByte, CInt, CStruct64

class RiseMaterial(Material):
    @property
    def magic_offset(self) -> int:
        return 0x54

    @property
    def num_passes(self) -> int:
        return 10

    @property
    def pass_texture_extra_info_size(self) -> int:
        return 8

    def read_pass(self, reader: ResourceReader) -> IMaterialPass:
        return reader.read_struct(_MaterialPass)

class _MaterialPass(CStruct64, IMaterialPass if TYPE_CHECKING else object):
    pixel_shader_ref: ResourceReference | None
    vertex_shader_ref: ResourceReference | None
    flags: CInt

    num_ps_textures: CByte
    num_ps_texture_ranges: CByte
    num_ps_textures_: CByte
    ps_texture_range_start: CByte
    ps_textures_ref: ResourceReference | None
    num_ps_constants: CInt
    field_24: CInt
    ps_constants_ref: ResourceReference | None

    num_vs_textures: CByte
    num_vs_texture_ranges: CByte
    num_vs_textures_: CByte
    vs_texture_range_start: CByte
    field_34: CInt
    vs_textures_ref: ResourceReference | None
    num_vs_constants: CInt
    field_44: CInt
    vs_constants_ref: ResourceReference | None

    field_50: CInt
    field_54: CInt
    tesselation_shader_set_ref: ResourceReference | None
    shader_input_elements_ref: ResourceReference | None

assert(sizeof(_MaterialPass) == 0x68)
