from ctypes import sizeof
from typing import Literal
from io_scene_tr_reboot.tr.Material import IMaterialPass, Material
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.ResourceReference import ResourceReference
from io_scene_tr_reboot.util.CStruct import CArray, CByte, CInt, CStruct32

class Tr2013Material(Material):
    @property
    def magic_offset(self) -> int:
        return 0x44

    @property
    def num_passes(self) -> int:
        return 10

    @property
    def pass_texture_extra_info_size(self) -> int:
        return 12

    def read_pass(self, reader: ResourceReader) -> IMaterialPass:
        return reader.read_struct(_MaterialPass)

class _MaterialPass(CStruct32):
    pixel_shader_ref: ResourceReference | None
    vertex_shader_ref: ResourceReference | None
    flags: CInt

    num_ps_textures: CByte
    num_ps_instance_textures: CByte
    num_ps_material_textures: CByte
    first_ps_instance_texture: CByte
    ps_textures_ref: ResourceReference | None
    num_ps_constants: CInt
    ps_constants_ref: ResourceReference | None

    num_vs_textures: CByte
    num_vs_instance_textures: CByte
    num_vs_material_textures: CByte
    first_vs_instance_texture: CByte
    vs_textures_ref: ResourceReference | None
    num_vs_constants: CInt
    vs_constants_ref: ResourceReference | None

    field_50: CInt
    field_54: CInt
    tesselation_shader_set_ref: ResourceReference | None
    shader_input_elements_ref: CArray[ResourceReference | None, Literal[3]]

assert(sizeof(_MaterialPass) == 0x44)
