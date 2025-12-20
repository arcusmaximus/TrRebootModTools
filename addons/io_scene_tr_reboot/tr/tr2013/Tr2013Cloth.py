from ctypes import sizeof
from typing import TYPE_CHECKING, Sequence, cast
from mathutils import Vector
from io_scene_tr_reboot.tr.Cloth import Cloth, ClothFeatureSupport, ClothMass, ClothMassAnchorBone, ClothMassSpringVector, ClothSpring, ClothStrip, IClothDef, IClothDefAnchorBone, IClothDefMass, IClothDefSpring, IClothDefStrip, IClothTune, IClothTuneCollisionGroup, IClothTuneConfig, IClothTuneStripGroup
from io_scene_tr_reboot.tr.CollisionShape import CollisionShape, CollisionShapeKey
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.ResourceReference import ResourceReference
from io_scene_tr_reboot.tr.tr2013.Tr2013CollisionShape import Tr2013CollisionShape
from io_scene_tr_reboot.util.CStruct import CFloat, CInt, CLong, CShort, CStruct, CStruct32
from io_scene_tr_reboot.util.Enumerable import Enumerable

class _ClothDef(CStruct32, IClothDef if TYPE_CHECKING else object):
    strips_ref: ResourceReference | None
    num_strips: CShort

assert(sizeof(_ClothDef) == 6)

class _ClothDefStrip(CStruct32, IClothDefStrip if TYPE_CHECKING else object):
    masses_ref: ResourceReference | None
    springs_ref: ResourceReference | None
    parent_bone_local_id: CInt
    num_masses: CShort
    num_springs: CShort
    max_rank: CShort
    field_12: CShort

    id: int
    _ignored_fields_ = ("id",)

assert(sizeof(_ClothDefStrip) == 0x14)

class _ClothDefMass(CStruct32, IClothDefMass if TYPE_CHECKING else object):
    position: Vector
    anchor_bones_ref: ResourceReference | None
    num_anchor_bones: CShort
    field_16: CShort
    spring_vector_array_ref: ResourceReference | None
    is_pinned: CShort
    local_bone_id: CShort
    rank: CShort
    field_22: CShort
    field_24: CInt
    field_28: CInt
    field_2C: CInt

    @property
    def mass(self) -> int:
        return 255 if self.is_pinned == 0 else 0

    @mass.setter
    def mass(self, value: int) -> None:             # type: ignore
        self.is_pinned = 0 if value == 255 else 1

    bounce_back_factor: int
    _ignored_fields_ = ("bounce_back_factor",)

assert(sizeof(_ClothDefMass) == 0x30)

class _ClothDefAnchorBone(CStruct32, IClothDefAnchorBone if TYPE_CHECKING else object):
    offset: Vector
    weight: CFloat
    local_bone_id: CShort
    field_16: CShort
    field_18: CLong

assert(sizeof(_ClothDefAnchorBone) == 0x20)

class _ClothDefSpring(CStruct32, IClothDefSpring if TYPE_CHECKING else object):
    rest_length: CFloat
    mass_1_idx: CShort
    mass_2_idx: CShort

    stretchiness_interpolation_value: float
    _ignored_fields_ = ("stretchiness_interpolation_value",)

assert(sizeof(_ClothDefSpring) == 8)

class _ClothTune(CStruct32, IClothTune if TYPE_CHECKING else object):
    default_config_idx: CInt
    wet_config_idx: CInt
    low_cover_config_idx: CInt
    flammable_config_idx: CInt
    inner_distance: CFloat
    outer_distance: CFloat
    configs_ref: ResourceReference | None
    num_configs: CInt
    strip_groups_ref: ResourceReference | None
    num_strip_groups: CInt
    collision_groups_ref: ResourceReference | None
    num_collision_groups: CInt
    flammable: CInt
    num_flammable_configs: CInt
    flammable_configs_ref: ResourceReference | None

    enabled: int
    _ignored_fields_ = ("enabled",)

assert(sizeof(_ClothTune) == 0x3C)

class _ClothTuneConfig(CStruct32, IClothTuneConfig if TYPE_CHECKING else object):
    num_strip_group_indices: CInt
    strip_group_indices_ref: ResourceReference | None

assert(sizeof(_ClothTuneConfig) == 8)

class _ClothTuneStripGroup(CStruct32, IClothTuneStripGroup if TYPE_CHECKING else object):
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
    num_strip_ids: CInt
    strip_ids_ref: ResourceReference | None
    num_collision_group_indices: CInt
    collision_group_indices_ref: ResourceReference | None
    field_4C: CInt
    field_50: CInt
    field_54: CInt
    field_58: CInt

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
    collide_with_dynamic_hair: int
    _ignored_fields_ = ("buoyancy_factor", "pose_follow_factor", "free_to_free_slop_z", "blend_to_bind_time", "collide_with_dynamic_hair")

assert(sizeof(_ClothTuneStripGroup) == 0x5C)

class _ClothTuneCollisionGroup(CStruct32, IClothTuneCollisionGroup if TYPE_CHECKING else object):
    count: CInt
    items_ref: ResourceReference | None

assert(sizeof(_ClothTuneCollisionGroup) == 8)

class Tr2013Cloth(Cloth):
    supports = ClothFeatureSupport(
        strip_buoyancy_factor = False,
        strip_pose_follow_factor = False,
        strip_free_to_free_slop_z = False,
        strip_blend_to_bind_time = False,
        mass_specific_bounceback_factor = False,
        spring_specific_stretchiness = False,
        hair_collision = False
    )

    definition_id: int
    tune_id: int
    strips: list[ClothStrip]

    def __init__(self, definition_id: int, tune_id: int) -> None:
        self.definition_id = definition_id
        self.tune_id = tune_id
        self.strips = []

    def read(self, definition_reader: ResourceReader, tune_reader: ResourceReader | None, skeleton_id: int, global_bone_ids: list[int | None], external_collisions: list[CollisionShape]) -> None:
        self.read_definition(definition_reader)
        if tune_reader is not None:
            self.read_tune(tune_reader, skeleton_id, global_bone_ids, { CollisionShapeKey(collision.type, skeleton_id, collision.hash): collision for collision in external_collisions })

    def read_definition(self, reader: ResourceReader) -> None:
        dtp_def = self.read_definition_header(reader)
        if dtp_def.strips_ref is None:
            return

        reader.seek(dtp_def.strips_ref)
        for dtp_strip in self.read_definition_strips(reader, dtp_def.num_strips):
            if dtp_strip.masses_ref is None or dtp_strip.springs_ref is None:
                raise Exception()

            strip = ClothStrip(dtp_strip.id, dtp_strip.parent_bone_local_id)

            reader.seek(dtp_strip.masses_ref)
            for dtp_mass in self.read_definition_masses(reader, dtp_strip.num_masses):
                mass = ClothMass(dtp_mass.local_bone_id, dtp_mass.position)
                mass.mass = dtp_mass.mass / 255.0
                mass.bounceback_factor = dtp_mass.bounce_back_factor / 255.0

                if dtp_mass.anchor_bones_ref is not None:
                    reader.seek(dtp_mass.anchor_bones_ref)
                    for dtp_anchor_bone in self.read_definition_anchor_bones(reader, dtp_mass.num_anchor_bones):
                        mass.anchor_local_bones.append(ClothMassAnchorBone(dtp_anchor_bone.local_bone_id, dtp_anchor_bone.offset))

                if dtp_mass.spring_vector_array_ref is not None:
                    reader.seek(dtp_mass.spring_vector_array_ref)
                    num_spring_vectors = reader.read_int32()
                    reader.skip(0xC)
                    for _ in range(num_spring_vectors):
                        spring_vector = reader.read_vec3d()
                        spring_idx = int(reader.read_float())
                        mass.spring_vectors.append(ClothMassSpringVector(spring_idx, spring_vector))

                strip.masses.append(mass)

            reader.seek(dtp_strip.springs_ref)
            for dtp_spring in self.read_definition_springs(reader, dtp_strip.num_springs):
                strip.springs.append(ClothSpring(dtp_spring.mass_1_idx, dtp_spring.mass_2_idx, dtp_spring.stretchiness_interpolation_value))

            self.strips.append(strip)

    def read_tune(self, reader: ResourceReader, skeleton_id: int, global_bone_ids: list[int | None], external_collisions: dict[CollisionShapeKey, CollisionShape]) -> None:
        dtp_tune = self.read_tune_header(reader)
        if dtp_tune.strip_groups_ref is None or \
           dtp_tune.collision_groups_ref is None:
            raise Exception()

        reader.seek(dtp_tune.collision_groups_ref)
        collision_groups: list[Sequence[CollisionShape]] = []
        collision_base_idx = 0
        for dtp_collision_group in self.read_tune_collision_groups(reader, dtp_tune.num_collision_groups):
            if dtp_collision_group.items_ref is None:
                raise Exception()

            reader.seek(dtp_collision_group.items_ref)
            collision_groups.append(self.read_tune_collisions(reader, collision_base_idx, dtp_collision_group.count, skeleton_id, global_bone_ids, external_collisions))
            collision_base_idx += dtp_collision_group.count

        reader.seek(dtp_tune.strip_groups_ref)
        for dtp_strip_group in self.read_tune_strip_groups(reader, dtp_tune.num_strip_groups):
            if dtp_strip_group.collision_group_indices_ref is None or dtp_strip_group.strip_ids_ref is None:
                raise Exception()

            strip_group_collisions: list[CollisionShape] = []
            reader.seek(dtp_strip_group.collision_group_indices_ref)
            for collision_set_idx in reader.read_uint32_list(dtp_strip_group.num_collision_group_indices):
                if collision_set_idx < len(collision_groups):
                    strip_group_collisions.extend(collision_groups[collision_set_idx])

            reader.seek(dtp_strip_group.strip_ids_ref)
            for strip_id in self.read_tune_strip_ids(reader, dtp_strip_group.num_strip_ids):
                strip = Enumerable(self.strips).first_or_none(lambda s: s.id == strip_id)
                if strip is None:
                    continue

                strip.gravity_factor            = dtp_strip_group.gravity_factor
                strip.buoyancy_factor           = dtp_strip_group.buoyancy_factor
                strip.wind_factor               = dtp_strip_group.wind_factor
                strip.pose_follow_factor        = dtp_strip_group.pose_follow_factor
                strip.rigidity                  = dtp_strip_group.rigidity_percentage / 100
                strip.mass_bounceback_factor    = dtp_strip_group.max_mass_bounceback_factor
                strip.drag                      = dtp_strip_group.drag

                strip.transform_type            = dtp_strip_group.transform_type
                strip.max_velocity_iterations   = dtp_strip_group.max_velocity_update_iterations
                strip.max_position_iterations   = dtp_strip_group.max_position_update_iterations
                strip.relaxation_iterations     = dtp_strip_group.relaxation_iterations
                strip.sub_step_count            = dtp_strip_group.sub_step_count
                strip.fixed_to_free_slop        = dtp_strip_group.fixed_to_free_slop / 100
                strip.free_to_free_slop         = dtp_strip_group.free_to_free_slop / 100
                strip.free_to_free_slop_z       = dtp_strip_group.free_to_free_slop_z / 100
                strip.mass_scale                = dtp_strip_group.mass_scale
                strip.time_delta_scale          = dtp_strip_group.time_delta_scale
                strip.blend_to_bind_time        = dtp_strip_group.blend_to_bind_time
                strip.is_hair_collider          = dtp_strip_group.collide_with_dynamic_hair != 0 or strip.is_hair_collider

                for mass in strip.masses:
                    mass.bounceback_factor *= dtp_strip_group.max_mass_bounceback_factor

                for spring in strip.springs:
                    spring.stretchiness = dtp_strip_group.free_to_free_slop / 100 + spring.stretchiness * \
                                          (dtp_strip_group.free_to_free_slop_z - dtp_strip_group.free_to_free_slop) / 100

                strip.collisions = strip_group_collisions

    def write(self, definition_writer: ResourceBuilder, tune_writer: ResourceBuilder, global_bone_ids: list[int | None]) -> None:
        self.recalculate_strip_ids()
        self.write_definition(definition_writer)
        self.write_tune(tune_writer, global_bone_ids)

    def recalculate_strip_ids(self) -> None:
        for i, strip in enumerate(self.strips):
            strip.id = i

    def write_definition(self, writer: ResourceBuilder) -> None:
        dtp = self.create_definition_header()
        dtp.strips_ref = writer.make_internal_ref()
        dtp.num_strips = len(self.strips)
        writer.write_struct(cast(CStruct, dtp))
        writer.align(0x10)

        dtp.strips_ref.offset = writer.position
        dtp_strips: list[IClothDefStrip] = []
        for strip in self.strips:
            dtp_strip = self.create_definition_strip()
            dtp_strip.id = strip.id
            dtp_strip.masses_ref = writer.make_internal_ref()
            dtp_strip.num_masses = len(strip.masses)
            dtp_strip.max_rank = len(strip.masses)
            dtp_strip.springs_ref = writer.make_internal_ref()
            dtp_strip.num_springs = len(strip.springs)
            dtp_strip.parent_bone_local_id = strip.parent_bone_local_id
            writer.write_struct(cast(CStruct, dtp_strip))

            dtp_strips.append(dtp_strip)

        writer.align(0x10)

        dtp_masses: list[IClothDefMass] = []
        for strip, dtp_strip in Enumerable(self.strips).zip(dtp_strips):
            cast(ResourceReference, dtp_strip.masses_ref).offset = writer.position
            for mass_idx, mass in enumerate(strip.masses):
                dtp_mass = self.create_definition_mass()
                dtp_mass.local_bone_id = mass.local_bone_id
                dtp_mass.position = mass.position
                dtp_mass.num_anchor_bones = len(mass.anchor_local_bones)
                dtp_mass.anchor_bones_ref = writer.make_internal_ref() if dtp_mass.num_anchor_bones > 0 else None
                dtp_mass.spring_vector_array_ref = writer.make_internal_ref()
                dtp_mass.rank = mass_idx
                dtp_mass.mass = int(mass.mass * 255)
                dtp_mass.bounce_back_factor = int(mass.bounceback_factor * 255)
                writer.write_struct(cast(CStruct, dtp_mass))

                dtp_masses.append(dtp_mass)

        for mass, dtp_mass in Enumerable(self.strips).select_many(lambda s: s.masses).zip(dtp_masses):
            if dtp_mass.anchor_bones_ref is not None:
                dtp_mass.anchor_bones_ref.offset = writer.position
                for anchor_bone in mass.anchor_local_bones:
                    dtp_anchor_bone = self.create_definition_anchor_bone()
                    dtp_anchor_bone.offset = anchor_bone.offset
                    dtp_anchor_bone.weight = 1.0 / len(mass.anchor_local_bones)
                    dtp_anchor_bone.local_bone_id = anchor_bone.local_bone_id
                    writer.write_struct(cast(CStruct, dtp_anchor_bone))

            writer.align(0x10)
            cast(ResourceReference, dtp_mass.spring_vector_array_ref).offset = writer.position
            writer.write_uint32(len(mass.spring_vectors))
            writer.write_int32(0)
            writer.write_int32(0)
            writer.write_int32(0)
            for spring_idx, spring_vector in mass.spring_vectors:
                writer.write_vec3d(spring_vector)
                writer.write_float(spring_idx)

        for strip, dtp_strip in Enumerable(self.strips).zip(dtp_strips):
            cast(ResourceReference, dtp_strip.springs_ref).offset = writer.position
            for spring in strip.springs:
                dtp_spring = self.create_definition_spring()
                dtp_spring.rest_length = (strip.masses[spring.mass_1_idx].position - strip.masses[spring.mass_2_idx].position).length
                dtp_spring.stretchiness_interpolation_value = spring.stretchiness
                dtp_spring.mass_1_idx = spring.mass_1_idx
                dtp_spring.mass_2_idx = spring.mass_2_idx
                writer.write_struct(cast(CStruct, dtp_spring))

    def write_tune(self, writer: ResourceBuilder, global_bone_ids: list[int | None]) -> None:
        dtp = self.create_tune_header()
        dtp.enabled = 1
        dtp.inner_distance = 5000
        dtp.outer_distance = 20000
        dtp.configs_ref = writer.make_internal_ref()

        # For the vast majority of cases we only need one config, but there *might* be an action graph that selects
        # a different one (AGInstanceSwitchClothConfig) - and of course, the game doesn't check whether the requested
        # config exists and just crashes. As such, we export the same config multiple times as a safety measure.
        dtp.num_configs = 8

        dtp.strip_groups_ref = writer.make_internal_ref()
        dtp.num_strip_groups = len(self.strips)
        dtp.collision_groups_ref = writer.make_internal_ref()
        dtp.num_collision_groups = len(self.strips)
        dtp.flammable_configs_ref = None
        writer.write_struct(cast(CStruct, dtp))
        writer.align(0x10)

        dtp.configs_ref.offset = writer.position
        dtp_configs: list[IClothTuneConfig] = []
        for _ in range(dtp.num_configs):
            dtp_config = self.create_tune_config()
            dtp_config.num_strip_group_indices = len(self.strips)
            dtp_config.strip_group_indices_ref = writer.make_internal_ref()
            writer.write_struct(cast(CStruct, dtp_config))
            dtp_configs.append(dtp_config)

        writer.align(0x10)

        for dtp_config in dtp_configs:
            cast(ResourceReference, dtp_config.strip_group_indices_ref).offset = writer.position

        for i in range(len(self.strips)):
            writer.write_uint32(i)

        writer.align(0x10)

        dtp.strip_groups_ref.offset = writer.position
        dtp_strip_groups: list[IClothTuneStripGroup] = []
        for strip in self.strips:
            dtp_strip_group = self.create_tune_strip_group()
            dtp_strip_group.gravity_factor = strip.gravity_factor
            dtp_strip_group.buoyancy_factor = strip.buoyancy_factor
            dtp_strip_group.drag = strip.drag
            dtp_strip_group.max_velocity_update_iterations = strip.max_velocity_iterations
            dtp_strip_group.max_position_update_iterations = strip.max_position_iterations
            dtp_strip_group.relaxation_iterations = strip.relaxation_iterations
            dtp_strip_group.sub_step_count = strip.sub_step_count
            dtp_strip_group.wind_factor = strip.wind_factor
            dtp_strip_group.max_mass_bounceback_factor = 1 if self.supports.mass_specific_bounceback_factor else strip.mass_bounceback_factor
            dtp_strip_group.pose_follow_factor = strip.pose_follow_factor
            dtp_strip_group.transform_type = strip.transform_type
            dtp_strip_group.fixed_to_free_slop = int(strip.fixed_to_free_slop * 100)
            dtp_strip_group.free_to_free_slop = int(strip.free_to_free_slop * 100)
            dtp_strip_group.free_to_free_slop_z = int(strip.free_to_free_slop_z * 100)
            dtp_strip_group.rigidity_percentage = int(strip.rigidity * 100)
            dtp_strip_group.mass_scale = strip.mass_scale
            dtp_strip_group.time_delta_scale = strip.time_delta_scale
            dtp_strip_group.blend_to_bind_time = strip.blend_to_bind_time
            dtp_strip_group.strip_ids_ref = writer.make_internal_ref()
            dtp_strip_group.num_strip_ids = 1
            dtp_strip_group.collide_with_dynamic_hair = self.supports.hair_collision and strip.is_hair_collider
            if len(strip.collisions) > 0:
                dtp_strip_group.collision_group_indices_ref = writer.make_internal_ref()
                dtp_strip_group.num_collision_group_indices = 1
            else:
                dtp_strip_group.collision_group_indices_ref = None

            writer.write_struct(cast(CStruct, dtp_strip_group))

            dtp_strip_groups.append(dtp_strip_group)

        for i, (strip, dtp_strip_group) in enumerate(Enumerable(self.strips).zip(dtp_strip_groups)):
            cast(ResourceReference, dtp_strip_group.strip_ids_ref).offset = writer.position
            self.write_tune_strip_id(writer, strip.id)

            if dtp_strip_group.collision_group_indices_ref is not None:
                dtp_strip_group.collision_group_indices_ref.offset = writer.position
                writer.write_uint32(i)

        writer.align(0x10)

        dtp.collision_groups_ref.offset = writer.position
        dtp_collision_groups: list[IClothTuneCollisionGroup] = []
        for strip in self.strips:
            dtp_collision_group = self.create_tune_collision_group()
            dtp_collision_group.count = len(strip.collisions)
            dtp_collision_group.items_ref = writer.make_internal_ref()
            writer.write_struct(cast(CStruct, dtp_collision_group))

            dtp_collision_groups.append(dtp_collision_group)

        for strip, dtp_collision_group in Enumerable(self.strips).zip(dtp_collision_groups):
            cast(ResourceReference, dtp_collision_group.items_ref).offset = writer.position
            for collision in strip.collisions:
                dtp_collision = self.create_tune_collision(collision, global_bone_ids)
                writer.write_struct(dtp_collision)

    def read_definition_header(self, reader: ResourceReader) -> IClothDef:
        return reader.read_struct(_ClothDef)

    def read_definition_strips(self, reader: ResourceReader, count: int) -> Sequence[IClothDefStrip]:
        def_strips = reader.read_struct_list(_ClothDefStrip, count)
        for i, def_strip in enumerate(def_strips):
            def_strip.id = i

        return def_strips

    def read_definition_masses(self, reader: ResourceReader, count: int) -> Sequence[IClothDefMass]:
        def_masses = reader.read_struct_list(_ClothDefMass, count)
        for def_mass in def_masses:
            def_mass.bounce_back_factor = 0

        return def_masses

    def read_definition_anchor_bones(self, reader: ResourceReader, count: int) -> Sequence[IClothDefAnchorBone]:
        return reader.read_struct_list(_ClothDefAnchorBone, count)

    def read_definition_springs(self, reader: ResourceReader, count: int) -> Sequence[IClothDefSpring]:
        def_springs = reader.read_struct_list(_ClothDefSpring, count)
        for def_spring in def_springs:
            def_spring.stretchiness_interpolation_value = 0

        return def_springs

    def read_tune_header(self, reader: ResourceReader) -> IClothTune:
        dtp_tune = reader.read_struct(_ClothTune)
        dtp_tune.enabled = 1
        return dtp_tune

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
        collisions: list[CollisionShape] = []
        for i in range(count):
            collisions.append(Tr2013CollisionShape.read(reader, base_idx + i, skeleton_id, global_bone_ids))

        return collisions

    def read_tune_strip_groups(self, reader: ResourceReader, count: int) -> Sequence[IClothTuneStripGroup]:
        dtp_strip_groups = reader.read_struct_list(_ClothTuneStripGroup, count)
        for dtp_strip_group in dtp_strip_groups:
            dtp_strip_group.buoyancy_factor = 0.5
            dtp_strip_group.pose_follow_factor = 0.0
            dtp_strip_group.free_to_free_slop_z = 0
            dtp_strip_group.blend_to_bind_time = 10
            dtp_strip_group.collide_with_dynamic_hair = 0

        return dtp_strip_groups

    def read_tune_strip_ids(self, reader: ResourceReader, count: int) -> Sequence[int]:
        return reader.read_bytes(count)

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
        return Tr2013CollisionShape.to_struct(collision, global_bone_ids)
