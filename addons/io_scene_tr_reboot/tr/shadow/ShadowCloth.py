from ctypes import sizeof
from typing import TYPE_CHECKING, Sequence
from mathutils import Vector
from io_scene_tr_reboot.tr.Cloth import ClothFeatureSupport, IClothDef, IClothDefAnchorBone, IClothDefMass, IClothDefSpring, IClothDefStrip, IClothTune, IClothTuneCollisionGroup, IClothTuneCollisionKey, IClothTuneConfig, IClothTuneStripGroup
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.ResourceReference import ResourceReference
from io_scene_tr_reboot.tr.rise.RiseCloth import RiseCloth
from io_scene_tr_reboot.util.CStruct import CByte, CFloat, CInt, CLong, CShort, CStruct64, CULong

class _ClothDef(CStruct64, IClothDef if TYPE_CHECKING else object):
    strips_ref: ResourceReference | None
    num_strips: CShort

assert(sizeof(_ClothDef) == 0xA)

class _ClothDefStrip(CStruct64, IClothDefStrip if TYPE_CHECKING else object):
    masses_ref: ResourceReference | None
    springs_ref: ResourceReference | None
    parent_bone_local_id: CInt
    id: CShort
    num_masses: CShort
    num_springs: CShort
    max_rank: CShort
    field_1C: CInt

assert(sizeof(_ClothDefStrip) == 0x20)

class _ClothDefMass(CStruct64, IClothDefMass if TYPE_CHECKING else object):
    position: Vector
    anchor_bones_ref: ResourceReference | None
    spring_vector_array_ref: ResourceReference | None
    local_bone_id: CShort
    num_anchor_bones: CByte
    rank: CByte
    mass: CByte
    bounce_back_factor: CByte
    field_26: CByte
    field_27: CByte
    field_28: CLong

assert(sizeof(_ClothDefMass) == 0x30)

class _ClothDefAnchorBone(CStruct64, IClothDefAnchorBone if TYPE_CHECKING else object):
    offset: Vector
    weight: CFloat
    local_bone_id: CShort
    field_16: CShort
    field_18: CLong

assert(sizeof(_ClothDefAnchorBone) == 0x20)

class _ClothDefSpring(CStruct64, IClothDefSpring if TYPE_CHECKING else object):
    rest_length: CFloat
    stretchiness_interpolation_value: CFloat
    mass_1_idx: CShort
    mass_2_idx: CShort

assert(sizeof(_ClothDefSpring) == 0xC)

class _ClothTune(CStruct64, IClothTune if TYPE_CHECKING else object):
    enabled: CByte
    field_1: CByte
    field_2: CShort
    field_4: CInt
    default_config_idx: CInt
    wet_config_idx: CInt
    low_cover_config_idx: CInt
    flammable_config_idx: CInt
    inner_distance: CFloat
    outer_distance: CFloat
    configs_ref: ResourceReference | None
    num_configs: CInt
    field_2C: CInt
    strip_groups_ref: ResourceReference | None
    num_strip_groups: CInt
    field_3C: CInt
    collision_groups_ref: ResourceReference | None
    num_collision_groups: CInt
    field_4C: CInt
    flammable: CInt
    num_flammable_configs: CInt
    flammable_configs_ref: ResourceReference | None

assert(sizeof(_ClothTune) == 0x60)

class _ClothTuneConfig(CStruct64, IClothTuneConfig if TYPE_CHECKING else object):
    num_strip_group_indices: CInt
    field_4: CInt
    strip_group_indices_ref: ResourceReference | None

assert(sizeof(_ClothTuneConfig) == 0x10)

class _ClothTuneStripGroup(CStruct64, IClothTuneStripGroup if TYPE_CHECKING else object):
    gravity_factor: CFloat
    buoyancy_factor: CFloat
    drag: CFloat
    max_velocity_update_iterations: CInt
    max_position_update_iterations: CInt
    relaxation_iterations: CInt
    sub_step_count: CInt
    wind_factor: CFloat
    wind_on_constraints: CInt
    max_mass_bounceback_factor: CFloat
    pose_follow_factor: CFloat
    transform_type: CInt
    fixed_to_free_slop: CInt
    free_to_free_slop: CInt
    free_to_free_slop_z: CInt
    rigidity_percentage: CInt
    mass_scale: CFloat
    time_delta_scale: CFloat
    blend_to_bind_time: CFloat
    collide_with_dynamic_hair: CInt
    collision_friction: CFloat
    collide_with_camera: CInt
    num_strip_ids: CInt
    zone_context: CInt
    strip_ids_ref: ResourceReference | None
    num_collision_group_indices: CInt
    field_6C: CInt
    collision_group_indices_ref: ResourceReference | None
    field_78: CLong
    field_80: CLong
    field_88: CLong

assert(sizeof(_ClothTuneStripGroup) == 0x90)

class _ClothTuneCollisionGroup(CStruct64, IClothTuneCollisionGroup if TYPE_CHECKING else object):
    count: CInt
    field_4: CInt
    items_ref: ResourceReference | None

assert(sizeof(_ClothTuneCollisionGroup) == 0x10)

class _ClothTuneCollisionKey(CStruct64, IClothTuneCollisionKey if TYPE_CHECKING else object):
    type: CByte
    field_1: CByte
    field_2: CShort
    field_4: CInt
    field_8: CLong
    field_10: CLong
    hash_data_1: CULong
    hash_data_2: CULong
    remaining_hash_data_ref: ResourceReference | None

assert(sizeof(_ClothTuneCollisionKey) == 0x30)

class ShadowCloth(RiseCloth):
    supports = ClothFeatureSupport(
        strip_buoyancy_factor = True,
        strip_pose_follow_factor = True,
        strip_free_to_free_slop_z = True,
        strip_blend_to_bind_time = True,
        mass_specific_bounceback_factor = True,
        spring_specific_stretchiness = True
    )

    def read_definition_header(self, reader: ResourceReader) -> IClothDef:
        return reader.read_struct(_ClothDef)

    def read_definition_strips(self, reader: ResourceReader, count: int) -> Sequence[IClothDefStrip]:
        return reader.read_struct_list(_ClothDefStrip, count)

    def read_definition_masses(self, reader: ResourceReader, count: int) -> Sequence[IClothDefMass]:
        return reader.read_struct_list(_ClothDefMass, count)

    def read_definition_anchor_bones(self, reader: ResourceReader, count: int) -> Sequence[IClothDefAnchorBone]:
        return reader.read_struct_list(_ClothDefAnchorBone, count)

    def read_definition_springs(self, reader: ResourceReader, count: int) -> Sequence[IClothDefSpring]:
        return reader.read_struct_list(_ClothDefSpring, count)

    def read_tune_header(self, reader: ResourceReader) -> IClothTune:
        return reader.read_struct(_ClothTune)

    def read_tune_collision_groups(self, reader: ResourceReader, count: int) -> Sequence[IClothTuneCollisionGroup]:
        return reader.read_struct_list(_ClothTuneCollisionGroup, count)

    def read_tune_strip_groups(self, reader: ResourceReader, count: int) -> Sequence[IClothTuneStripGroup]:
        return reader.read_struct_list(_ClothTuneStripGroup, count)

    def read_tune_strip_ids(self, reader: ResourceReader, count: int) -> Sequence[int]:
        return reader.read_uint16_list(count)

    def create_definition_header(self) -> IClothDef:
        return _ClothDef()

    def create_definition_strip(self) -> IClothDefStrip:
        return _ClothDefStrip()

    def create_definition_mass(self) -> IClothDefMass:
        return _ClothDefMass()

    def create_definition_anchor_bone(self) -> IClothDefAnchorBone:
        return _ClothDefAnchorBone()

    def create_definition_spring(self) -> IClothDefSpring:
        return _ClothDefSpring()

    def create_tune_header(self) -> IClothTune:
        return _ClothTune()

    def create_tune_config(self) -> IClothTuneConfig:
        return _ClothTuneConfig()

    def create_tune_strip_group(self) -> IClothTuneStripGroup:
        return _ClothTuneStripGroup()

    def write_tune_strip_id(self, writer: ResourceBuilder, id: int) -> None:
        writer.write_uint16(id)

    def create_tune_collision_group(self) -> _ClothTuneCollisionGroup:
        return _ClothTuneCollisionGroup()

    def create_component_collision(self) -> IClothTuneCollisionKey:
        return _ClothTuneCollisionKey()
