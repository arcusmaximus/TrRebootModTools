from ctypes import sizeof
from typing import TYPE_CHECKING, Sequence
from mathutils import Vector
from io_scene_tr_reboot.tr.Cloth import ClothFeatureSupport, IClothDef, IClothDefAnchorBone, IClothDefMass, IClothDefSpring, IClothDefStrip, IClothTune, IClothTuneCollisionGroup, IClothTuneCollisionKey, IClothTuneConfig, IClothTuneStripGroup
from io_scene_tr_reboot.tr.CollisionShape import CollisionShape, CollisionShapeKey, CollisionShapeType
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.ResourceReference import ResourceReference
from io_scene_tr_reboot.tr.tr2013.Tr2013Cloth import Tr2013Cloth
from io_scene_tr_reboot.util.CStruct import CByte, CFloat, CInt, CLong, CShort, CStruct, CStruct64, CULong

class _ClothDef(CStruct64, IClothDef if TYPE_CHECKING else object):
    strips_ref: ResourceReference | None
    num_strips: CShort

assert(sizeof(_ClothDef) == 0xA)

class _ClothDefStrip(CStruct64, IClothDefStrip if TYPE_CHECKING else object):
    masses_ref: ResourceReference | None
    springs_ref: ResourceReference | None
    parent_bone_local_id: CInt
    num_masses: CShort
    num_springs: CShort
    max_rank: CShort
    field_1A: CShort
    field_1C: CInt

    id: int
    _ignored_fields_ = ("id",)

assert(sizeof(_ClothDefStrip) == 0x20)

class _ClothDefMass(CStruct64, IClothDefMass if TYPE_CHECKING else object):
    position: Vector
    anchor_bones_ref: ResourceReference | None
    num_anchor_bones: CShort
    field_1A: CShort
    field_1C: CInt
    spring_vector_array_ref: ResourceReference | None
    is_pinned: CShort
    local_bone_id: CShort
    rank: CShort
    field_2E: CShort

    @property
    def mass(self) -> int:
        return 255 if self.is_pinned == 0 else 0

    @mass.setter
    def mass(self, value: int) -> None:             # type: ignore
        self.is_pinned = 0 if value == 255 else 1

    bounce_back_factor: int
    _ignored_fields_ = ("bounce_back_factor",)

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
    mass_1_idx: CShort
    mass_2_idx: CShort

    stretchiness_interpolation_value: float
    _ignored_fields_ = ("stretchiness_interpolation_value",)

assert(sizeof(_ClothDefSpring) == 8)

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
    gravity: CFloat
    drag: CFloat
    max_velocity_update_iterations: CInt
    max_position_update_iterations: CInt
    relaxation_iterations: CInt
    sub_step_count: CInt
    wind_factor: CFloat
    wind_on_constraints: CInt
    max_mass_bounceback_factor: CFloat
    transform_type: CInt
    fixed_to_free_slop: CInt
    free_to_free_slop: CInt
    rigidity_percentage: CInt
    mass_scale: CFloat
    time_delta_scale: CFloat
    collide_with_dynamic_hair: CInt
    num_strip_ids: CInt
    zone_context: CInt
    strip_ids_ref: ResourceReference | None
    num_collision_group_indices: CInt
    field_54: CInt
    collision_group_indices_ref: ResourceReference | None
    field_60: CLong
    field_68: CLong

    @property
    def gravity_factor(self) -> float:
        return self.gravity / 9.8

    @gravity_factor.setter
    def gravity_factor(self, value: float) -> None:     # type: ignore
        self.gravity = value * 9.8

    buoyancy_factor: float
    pose_follow_factor: float
    free_to_free_slop_z: int
    blend_to_bind_time: float
    _ignored_fields_ = ("buoyancy_factor", "pose_follow_factor", "free_to_free_slop_z", "blend_to_bind_time")

assert(sizeof(_ClothTuneStripGroup) == 0x70)

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

class RiseCloth(Tr2013Cloth):
    supports = ClothFeatureSupport(
        strip_buoyancy_factor = False,
        strip_pose_follow_factor = False,
        strip_free_to_free_slop_z = False,
        strip_blend_to_bind_time = False,
        mass_specific_bounceback_factor = False,
        spring_specific_stretchiness = False,
        hair_collision = True
    )

    def read_definition_header(self, reader: ResourceReader) -> IClothDef:
        return reader.read_struct(_ClothDef)

    def read_definition_strips(self, reader: ResourceReader, count: int) -> Sequence[IClothDefStrip]:
        dtp_strips = reader.read_struct_list(_ClothDefStrip, count)
        for i, dtp_strip in enumerate(dtp_strips):
            dtp_strip.id = i

        return dtp_strips

    def read_definition_masses(self, reader: ResourceReader, count: int) -> Sequence[IClothDefMass]:
        dtp_masses = reader.read_struct_list(_ClothDefMass, count)
        for dtp_mass in dtp_masses:
            dtp_mass.bounce_back_factor = 0

        return dtp_masses

    def read_definition_anchor_bones(self, reader: ResourceReader, count: int) -> Sequence[IClothDefAnchorBone]:
        return reader.read_struct_list(_ClothDefAnchorBone, count)

    def read_definition_springs(self, reader: ResourceReader, count: int) -> Sequence[IClothDefSpring]:
        dtp_springs = reader.read_struct_list(_ClothDefSpring, count)
        for dtp_spring in dtp_springs:
            dtp_spring.stretchiness_interpolation_value = 0

        return dtp_springs

    def read_tune_header(self, reader: ResourceReader) -> IClothTune:
        return reader.read_struct(_ClothTune)

    def read_tune_collision_groups(self, reader: ResourceReader, count: int) -> Sequence[IClothTuneCollisionGroup]:
        return reader.read_struct_list(_ClothTuneCollisionGroup, count)

    def read_tune_collisions(
        self,
        reader: ResourceReader,
        base_idx: int,
        count: int,
        skeleton_id: int,
        global_bone_ids: list[int | None],
        external_collisions: dict[CollisionShapeKey, CollisionShape]
    ) -> Sequence[CollisionShape]:
        dtp_collision_hashes = reader.read_struct_list(_ClothTuneCollisionKey, count)
        collisions: list[CollisionShape] = []
        for dtp_collision_hash in dtp_collision_hashes:
            collision_key = self.read_tune_collision_key(reader, skeleton_id, dtp_collision_hash)
            collision = external_collisions.get(collision_key)
            if collision is not None:
                collisions.append(collision)

        return collisions

    def read_tune_collision_key(self, reader: ResourceReader, skeleton_id: int, dtp_collision: IClothTuneCollisionKey) -> CollisionShapeKey:
        if dtp_collision.hash_data_2 == 0:
            return CollisionShapeKey(CollisionShapeType(dtp_collision.type), skeleton_id, dtp_collision.hash_data_1)

        inputs: list[int] = [dtp_collision.hash_data_1]
        if dtp_collision.hash_data_2 != 0:
            inputs.append(dtp_collision.hash_data_2)
            if dtp_collision.remaining_hash_data_ref is not None:
                reader.seek(dtp_collision.remaining_hash_data_ref)
                while True:
                    value = reader.read_uint64()
                    if value == 0:
                        break

                    inputs.append(value)

        return CollisionShapeKey(CollisionShapeType(dtp_collision.type), skeleton_id, self.get_collision_hash(inputs))

    def get_collision_hash(self, input: list[int]) -> int:
        hash = 0
        for value in input:
            for i, byte in enumerate(value.to_bytes(8, "little")):
                if (i % 4) == 0 and hash == 0:
                    hash = 0xCBF29CE484222325

                hash = ((hash * 0x100000001B3) ^ byte) & 0xFFFFFFFFFFFFFFFF

        return hash

    def read_tune_strip_groups(self, reader: ResourceReader, count: int) -> Sequence[IClothTuneStripGroup]:
        dtp_strip_groups = reader.read_struct_list(_ClothTuneStripGroup, count)
        for dtp_strip_group in dtp_strip_groups:
            dtp_strip_group.buoyancy_factor = 0.5
            dtp_strip_group.pose_follow_factor = 0.0
            dtp_strip_group.free_to_free_slop_z = 0
            dtp_strip_group.blend_to_bind_time = 10

        return dtp_strip_groups

    def read_tune_strip_ids(self, reader: ResourceReader, count: int) -> Sequence[int]:
        return reader.read_uint32_list(count)

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
        writer.write_uint32(id)

    def create_tune_collision_group(self) -> IClothTuneCollisionGroup:
        return _ClothTuneCollisionGroup()

    def create_tune_collision(self, collision: CollisionShape, global_bone_ids: list[int | None]) -> CStruct:
        dtp_collision_hash = _ClothTuneCollisionKey()
        dtp_collision_hash.type = collision.type
        dtp_collision_hash.hash_data_1 = collision.hash
        dtp_collision_hash.remaining_hash_data_ref = None
        return dtp_collision_hash
