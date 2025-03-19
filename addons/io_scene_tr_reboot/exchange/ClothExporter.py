from typing import NamedTuple, cast
import bpy
import os
from mathutils import Vector
from io_scene_tr_reboot.BlenderHelper import BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.properties.BoneProperties import BoneProperties
from io_scene_tr_reboot.properties.ObjectProperties import ObjectProperties
from io_scene_tr_reboot.tr.Cloth import Cloth, ClothMass, ClothMassAnchorBone, ClothMassSpringVector, ClothSpring, ClothStrip
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.tr.Collision import Collision
from io_scene_tr_reboot.tr.Enumerations import CdcGame, ResourceType
from io_scene_tr_reboot.tr.Factories import Factories
from io_scene_tr_reboot.tr.IFactory import IFactory
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey
from io_scene_tr_reboot.util.Enumerable import Enumerable

class _ClothIdSet(NamedTuple):
    skeleton_id: int
    definition_id: int
    tune_id: int

class _BoundingBox(NamedTuple):
    min: Vector
    max: Vector

    def intersects(self, other: "_BoundingBox") -> bool:
        return other.max.x >= self.min.x and \
               other.max.y >= self.min.y and \
               other.max.z >= self.min.z and \
               other.min.x <= self.max.x and \
               other.min.y <= self.max.y and \
               other.min.z <= self.max.z

class _BoneInfo(NamedTuple):
    global_id: int | None
    position: Vector

class ClothExporter:
    scale_factor: float
    game: CdcGame
    factory: IFactory

    def __init__(self, scale_factor: float, game: CdcGame) -> None:
        self.scale_factor = scale_factor
        self.game = game
        self.factory = Factories.get(game)

    def export_cloths(self, folder_path: str, bl_armature_obj: bpy.types.Object, bl_local_armature_objs: dict[int, bpy.types.Object]) -> None:
        collision_bounding_boxes = self.get_collision_bounding_boxes(bl_armature_obj)

        for bl_empty in Enumerable(bl_armature_obj.children).where(lambda o: not o.data and BlenderNaming.is_cloth_empty_name(o.name)):
            for cloth_id_set, bl_cloth_strip_objs in Enumerable(bl_empty.children).where(lambda o: isinstance(o.data, bpy.types.Mesh)) \
                                                                                  .group_by(self.get_cloth_id_set) \
                                                                                  .items():
                bl_local_armature_obj = bl_local_armature_objs.get(cloth_id_set.skeleton_id) or bl_armature_obj
                bone_infos_by_local_id = self.get_bone_infos_by_local_id(bl_local_armature_obj)
                self.export_cloth(folder_path, cloth_id_set, bl_cloth_strip_objs, bl_armature_obj, bone_infos_by_local_id, collision_bounding_boxes)

    def get_collision_bounding_boxes(self, bl_armature_obj: bpy.types.Object) -> dict[Collision, _BoundingBox]:
        result: dict[Collision, _BoundingBox] = {}

        for bl_empty in Enumerable(bl_armature_obj.children).where(lambda o: not o.data and BlenderNaming.is_collision_empty_name(o.name)):
            for bl_obj in Enumerable(bl_empty.children).where(lambda o: isinstance(o.data, bpy.types.Mesh)):
                collision_key = BlenderNaming.parse_collision_name(bl_obj.name)

                collision_data = ObjectProperties.get_instance(bl_obj).collision.data
                collision: Collision
                if collision_data:
                    collision = self.factory.deserialize_collision(collision_data)
                else:
                    collision = self.factory.create_collision(collision_key.type, collision_key.hash)

                bounding_box = self.get_world_bounding_box(bl_obj)
                result[collision] = bounding_box

        return result

    def get_cloth_id_set(self, bl_cloth_strip_obj: bpy.types.Object) -> _ClothIdSet:
        cloth_strip_id_set = BlenderNaming.parse_cloth_strip_name(bl_cloth_strip_obj.name)
        return _ClothIdSet(cloth_strip_id_set.skeleton_id, cloth_strip_id_set.definition_id, cloth_strip_id_set.component_id)

    def get_bone_infos_by_local_id(self, bl_armature_obj: bpy.types.Object) -> dict[int, _BoneInfo]:
        result: dict[int, _BoneInfo] = {}
        with BlenderHelper.enter_edit_mode(bl_armature_obj):
            for bl_bone in cast(bpy.types.Armature, bl_armature_obj.data).edit_bones:
                bone_id_set = BlenderNaming.parse_bone_name(bl_bone.name)
                if bone_id_set.local_id is not None:
                    result[bone_id_set.local_id] = _BoneInfo(bone_id_set.global_id, bl_bone.head / self.scale_factor)

        return result

    def export_cloth(
        self,
        folder_path: str,
        cloth_id_set: _ClothIdSet,
        bl_cloth_strip_objs: list[bpy.types.Object],
        bl_armature_obj: bpy.types.Object,
        bone_infos_by_local_id: dict[int, _BoneInfo],
        collision_bounding_boxes: dict[Collision, _BoundingBox]
    ) -> None:
        tr_cloth = self.factory.create_cloth(cloth_id_set.definition_id, cloth_id_set.tune_id)
        for bl_cloth_strip_obj in bl_cloth_strip_objs:
            if len(cast(bpy.types.Mesh, bl_cloth_strip_obj.data).vertices) == 0:
                continue

            self.add_cloth_strip(tr_cloth, bl_cloth_strip_obj, bl_armature_obj, bone_infos_by_local_id, collision_bounding_boxes)

        definition_builder = ResourceBuilder(ResourceKey(ResourceType.DTP, cloth_id_set.definition_id), self.game)
        tune_builder = ResourceBuilder(ResourceKey(ResourceType.DTP, cloth_id_set.tune_id), self.game)
        global_bone_ids = Enumerable(cast(bpy.types.Armature, bl_armature_obj.data).bones).select(lambda b: BlenderNaming.parse_bone_name(b.name)) \
                                                                                          .order_by(lambda ids: ids.local_id) \
                                                                                          .select(lambda ids: ids.global_id) \
                                                                                          .to_list()
        tr_cloth.write(definition_builder, tune_builder, global_bone_ids)

        self.write_cloth_definition_file(folder_path, bl_armature_obj, definition_builder)
        self.write_cloth_tune_file(folder_path, bl_armature_obj, tune_builder)

    def write_cloth_definition_file(self, folder_path: str, bl_armature_obj: bpy.types.Object, definition_builder: ResourceBuilder) -> None:
        definition_file_path = os.path.join(folder_path, Collection.make_resource_file_name(definition_builder.resource, self.game))
        with open(definition_file_path, "wb") as definition_file:
            definition_file.write(definition_builder.build())

    def write_cloth_tune_file(self, folder_path: str, bl_armature_obj: bpy.types.Object, tune_builder: ResourceBuilder) -> None:
        tune_file_path = os.path.join(folder_path, Collection.make_resource_file_name(tune_builder.resource, self.game))
        with open(tune_file_path, "wb") as component_file:
            component_file.write(tune_builder.build())

    def add_cloth_strip(
            self,
            tr_cloth: Cloth,
            bl_cloth_strip_obj: bpy.types.Object,
            bl_armature_obj: bpy.types.Object,
            bone_infos_by_local_id: dict[int, _BoneInfo],
            collision_bounding_boxes: dict[Collision, _BoundingBox]
        ) -> None:
        cloth_strip_id_set = BlenderNaming.parse_cloth_strip_name(bl_cloth_strip_obj.name)

        tr_cloth_strip = ClothStrip(cloth_strip_id_set.strip_id, self.get_cloth_strip_parent_bone_local_id(bl_cloth_strip_obj, bone_infos_by_local_id))
        self.apply_cloth_strip_properties(tr_cloth_strip, bl_cloth_strip_obj)
        self.add_cloth_strip_masses(tr_cloth_strip, bl_cloth_strip_obj, bl_armature_obj, bone_infos_by_local_id)
        self.add_cloth_strip_springs(tr_cloth_strip, bl_cloth_strip_obj)
        self.add_cloth_strip_collisions(tr_cloth_strip, bl_cloth_strip_obj, collision_bounding_boxes)
        tr_cloth.strips.append(tr_cloth_strip)

    def get_cloth_strip_parent_bone_local_id(self, bl_cloth_strip_obj: bpy.types.Object, bone_infos_by_local_id: dict[int, _BoneInfo]) -> int:
        cloth_strip_properties = ObjectProperties.get_instance(bl_cloth_strip_obj).cloth
        if not cloth_strip_properties:
            raise Exception(f"Cloth strip {bl_cloth_strip_obj.name} has no parent bone. Please select a valid bone in the sidebar or delete the cloth strip.")

        parent_bone_id_set = BlenderNaming.try_parse_bone_name(cloth_strip_properties.parent_bone_name)
        if parent_bone_id_set is None:
            raise Exception(f"Invalid parent bone name [{cloth_strip_properties.parent_bone_name}] in cloth strip object {bl_cloth_strip_obj.name}.")

        if parent_bone_id_set.local_id is not None:
            return parent_bone_id_set.local_id

        if parent_bone_id_set.global_id is None:
            raise Exception(f"Cloth strip object {bl_cloth_strip_obj.name} has parent bone {cloth_strip_properties.parent_bone_name} which has no global ID.")

        for local_bone_id, bone_info in bone_infos_by_local_id.items():
            if bone_info.global_id == parent_bone_id_set.global_id:
                return local_bone_id

        raise Exception(f"Cloth strip object {bl_cloth_strip_obj.name} has parent bone {cloth_strip_properties.parent_bone_name} which wasn't found in the local skeleton.")

    def apply_cloth_strip_properties(self, tr_cloth_strip: ClothStrip, bl_cloth_strip_obj: bpy.types.Object) -> None:
        cloth_strip_properties = ObjectProperties.get_instance(bl_cloth_strip_obj).cloth
        tr_cloth_strip.gravity_factor = cloth_strip_properties.gravity_factor
        tr_cloth_strip.buoyancy_factor = cloth_strip_properties.buoyancy_factor
        tr_cloth_strip.wind_factor = cloth_strip_properties.wind_factor
        tr_cloth_strip.pose_follow_factor = cloth_strip_properties.stiffness
        tr_cloth_strip.rigidity = cloth_strip_properties.rigidity
        tr_cloth_strip.mass_bounceback_factor = cloth_strip_properties.bounceback_factor
        tr_cloth_strip.drag = cloth_strip_properties.dampening

        tr_cloth_strip.transform_type = cloth_strip_properties.transform_type
        tr_cloth_strip.max_velocity_iterations = cloth_strip_properties.max_velocity_iterations
        tr_cloth_strip.max_position_iterations = cloth_strip_properties.max_position_iterations
        tr_cloth_strip.relaxation_iterations = cloth_strip_properties.relaxation_iterations
        tr_cloth_strip.sub_step_count = cloth_strip_properties.sub_step_count
        tr_cloth_strip.fixed_to_free_slop = cloth_strip_properties.fixed_to_free_slop
        tr_cloth_strip.free_to_free_slop = cloth_strip_properties.free_to_free_slop
        tr_cloth_strip.free_to_free_slop_z = cloth_strip_properties.free_to_free_slop_z
        tr_cloth_strip.mass_scale = cloth_strip_properties.mass_scale
        tr_cloth_strip.time_delta_scale = cloth_strip_properties.time_delta_scale
        tr_cloth_strip.blend_to_bind_time = cloth_strip_properties.blend_to_bind_time

    def add_cloth_strip_masses(
        self,
        tr_cloth_strip: ClothStrip,
        bl_cloth_strip_obj: bpy.types.Object,
        bl_armature_obj: bpy.types.Object,
        bone_infos_by_local_id: dict[int, _BoneInfo]
    ) -> None:
        bl_armature = cast(bpy.types.Armature, bl_armature_obj.data)

        bl_cloth_strip_mesh = cast(bpy.types.Mesh, bl_cloth_strip_obj.data)
        for bl_vertex in bl_cloth_strip_mesh.vertices:
            if len(bl_vertex.groups) != 1:
                raise Exception(f"A vertex in cloth strip object {bl_cloth_strip_obj.name} has incorrect vertex groups. Please regenerate the armature.")

            bone_name = bl_cloth_strip_obj.vertex_groups[bl_vertex.groups[0].group].name
            bl_bone = bl_armature.bones.get(bone_name)
            if bl_bone is None:
                raise Exception(f"Cloth strip object {bl_cloth_strip_obj.name} references bone {bone_name} which doesn't exist.")

            bone_id_set = BlenderNaming.parse_bone_name(bone_name)
            if bone_id_set.local_id is None:
                raise Exception(f"Cloth strip object {bl_cloth_strip_obj.name} contains vertex group {bone_name} which has no local ID.")

            bone_cloth_properties = BoneProperties.get_instance(bl_bone).cloth

            tr_cloth_mass = ClothMass(bone_id_set.local_id, bone_infos_by_local_id[bone_id_set.local_id].position)
            tr_cloth_mass.bounceback_factor = bone_cloth_properties.bounceback_factor
            if BlenderHelper.is_bone_in_group(bl_armature_obj, bl_bone, BlenderNaming.pinned_cloth_bone_group_name):
                anchor_offset = bone_infos_by_local_id[tr_cloth_strip.parent_bone_local_id].position - bone_infos_by_local_id[bone_id_set.local_id].position
                tr_cloth_mass.anchor_local_bones = [ClothMassAnchorBone(tr_cloth_strip.parent_bone_local_id, anchor_offset)]
                tr_cloth_mass.mass = 0.0
            else:
                tr_cloth_mass.mass = 1.0

            tr_cloth_strip.masses.append(tr_cloth_mass)

    def add_cloth_strip_springs(self, tr_cloth_strip: ClothStrip, bl_cloth_strip_obj: bpy.types.Object) -> None:
        bl_cloth_strip_mesh = cast(bpy.types.Mesh, bl_cloth_strip_obj.data)

        for bl_edge in bl_cloth_strip_mesh.edges:
            tr_cloth_spring = ClothSpring(bl_edge.vertices[0], bl_edge.vertices[1], BlenderHelper.get_edge_bevel_weight(bl_cloth_strip_mesh, bl_edge.index))
            spring_idx = len(tr_cloth_strip.springs)
            tr_cloth_strip.springs.append(tr_cloth_spring)

            spring_vector = bl_cloth_strip_mesh.vertices[tr_cloth_spring.mass_2_idx].co - bl_cloth_strip_mesh.vertices[tr_cloth_spring.mass_1_idx].co
            tr_cloth_mass1 = tr_cloth_strip.masses[tr_cloth_spring.mass_1_idx]
            if len(tr_cloth_mass1.spring_vectors) < 4:
                tr_cloth_mass1.spring_vectors.append(ClothMassSpringVector(spring_idx, spring_vector))

            tr_cloth_mass2 = tr_cloth_strip.masses[tr_cloth_spring.mass_2_idx]
            if len(tr_cloth_mass2.spring_vectors) < 4:
                tr_cloth_mass2.spring_vectors.append(ClothMassSpringVector(spring_idx, -spring_vector))

        for tr_cloth_mass in tr_cloth_strip.masses:
            num_springs = len(tr_cloth_mass.spring_vectors)
            for i in range(num_springs):
                cross_vector = tr_cloth_mass.spring_vectors[i].vector.cross(tr_cloth_mass.spring_vectors[(i + 1) % num_springs].vector)
                tr_cloth_mass.spring_vectors.append(ClothMassSpringVector(-1, cast(Vector, cross_vector)))

    def add_cloth_strip_collisions(self, tr_cloth_strip: ClothStrip, bl_cloth_strip_obj: bpy.types.Object, collision_bounding_boxes: dict[Collision, _BoundingBox]) -> None:
        cloth_strip_bounding_box = self.get_world_bounding_box(bl_cloth_strip_obj)
        cloth_strip_bounding_box = _BoundingBox(
            cloth_strip_bounding_box.min - Vector((0.5, 0.5, 0.5)),
            cloth_strip_bounding_box.max + Vector((0.5, 0.5, 0.5))
        )
        for collision_key, collision_bounding_box in collision_bounding_boxes.items():
            if cloth_strip_bounding_box.intersects(collision_bounding_box):
                tr_cloth_strip.collisions.append(collision_key)

    def get_world_bounding_box(self, bl_obj: bpy.types.Object) -> _BoundingBox:
        corners = Enumerable(bl_obj.bound_box).select(lambda c: bl_obj.matrix_world @ Vector(c)).to_list()
        min_corner = Vector((
            Enumerable(corners).min(lambda c: c.x),
            Enumerable(corners).min(lambda c: c.y),
            Enumerable(corners).min(lambda c: c.z)
        ))
        max_corner = Vector((
            Enumerable(corners).max(lambda c: c.x),
            Enumerable(corners).max(lambda c: c.y),
            Enumerable(corners).max(lambda c: c.z)
        ))
        return _BoundingBox(min_corner, max_corner)
