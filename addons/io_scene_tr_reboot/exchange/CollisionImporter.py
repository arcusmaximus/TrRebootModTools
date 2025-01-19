from typing import Any, Iterable, cast
import bpy
import bmesh
from mathutils import Matrix, Vector
from io_scene_tr_reboot.BlenderHelper import BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.properties.ObjectProperties import ObjectProperties
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.tr.Collision import Collision, CollisionBox, CollisionCapsule, CollisionDoubleRadiiCapsule, CollisionSphere
from io_scene_tr_reboot.util.Enumerable import Enumerable
from io_scene_tr_reboot.util.SlotsBase import SlotsBase

class CollisionImporter(SlotsBase):
    scale_factor: float
    bl_context: bpy.types.Context

    def __init__(self, scale_factor: float) -> None:
        self.scale_factor = scale_factor
        self.bl_context = bpy.context

    def import_from_collection(self, tr_collection: Collection, bl_armature_obj: bpy.types.Object) -> list[bpy.types.Object]:
        bl_collision_empty = BlenderHelper.create_object(None, BlenderNaming.make_collision_empty_name(tr_collection.name))
        bl_collision_empty.parent = bl_armature_obj

        bl_collision_objs: list[bpy.types.Object] = []
        for tr_collision in tr_collection.get_collisions():
            bl_collision_obj = self.import_collision(tr_collection, tr_collision, bl_armature_obj)
            if bl_collision_obj is None:
                continue

            bl_collision_obj.parent = bl_collision_empty
            bl_collision_objs.append(bl_collision_obj)

        return bl_collision_objs

    def import_collision(self, tr_collection: Collection, tr_collision: Collision, bl_armature_obj: bpy.types.Object | None) -> bpy.types.Object | None:
        bl_bone: bpy.types.Bone | None = None
        bl_target_bone: bpy.types.Bone | None = None
        if bl_armature_obj is not None and tr_collision.global_bone_id >= 0:
            bl_bone = self.find_bone(bl_armature_obj, tr_collision.global_bone_id)
            if isinstance(tr_collision, CollisionCapsule) and tr_collision.target_global_bone_id is not None:
                bl_target_bone = self.find_bone(bl_armature_obj, tr_collision.target_global_bone_id)

        bl_collision_obj: bpy.types.Object | None
        if isinstance(tr_collision, CollisionBox):
            bl_collision_obj = self.create_collision_box(tr_collision)
        elif isinstance(tr_collision, CollisionSphere):
            bl_collision_obj = self.create_collision_sphere(tr_collision)
        elif isinstance(tr_collision, CollisionCapsule) or isinstance(tr_collision, CollisionDoubleRadiiCapsule):
            bl_collision_obj = self.create_collision_capsule(tr_collision, bl_bone, bl_target_bone)
        else:
            return None

        if bl_collision_obj is None:
            return None

        bl_collision_obj.name = BlenderNaming.make_collision_name(tr_collection.name, tr_collision.type, tr_collision.hash)
        cast(bpy.types.Mesh, bl_collision_obj.data).name = bl_collision_obj.name
        bl_collision_obj.hide_set(True)
        BlenderHelper.move_object_to_collection(bl_collision_obj, self.bl_context.scene.collection)
        ObjectProperties.get_instance(bl_collision_obj).collision.data = tr_collision.serialize()

        if tr_collision.transform is not None:
            transform = tr_collision.transform.copy()
            transform.translation = transform.translation * self.scale_factor
            bl_collision_obj.matrix_local = transform

        if bl_armature_obj is not None and bl_bone is not None:
            if tr_collision.transform is None:
                transform = Matrix.Translation(bl_bone.matrix_local.translation)
                bl_collision_obj.matrix_local = transform

            self.add_armature_modifier(bl_collision_obj, bl_armature_obj, bl_bone)

        return bl_collision_obj

    def create_collision_box(self, tr_collision: CollisionBox) -> bpy.types.Object:
        bpy.ops.mesh.primitive_cube_add(size = 1, calc_uvs = False)
        bl_cube_obj = cast(bpy.types.Object, self.bl_context.object)
        bl_cube_obj.scale = Vector((tr_collision.width, tr_collision.depth, tr_collision.height)) * self.scale_factor
        bpy.ops.object.transform_apply()
        return cast(bpy.types.Object, self.bl_context.object)

    def create_collision_sphere(self, tr_collision: CollisionSphere) -> bpy.types.Object:
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions = 3, radius = tr_collision.radius * self.scale_factor)
        bpy.ops.object.shade_smooth()
        return cast(bpy.types.Object, self.bl_context.object)

    def create_collision_capsule(
        self,
        tr_collision: CollisionCapsule | CollisionDoubleRadiiCapsule,
        bl_bone: bpy.types.Bone | None,
        bl_target_bone: bpy.types.Bone | None
    ) -> bpy.types.Object | None:
        radius1 = tr_collision.radius_1 if isinstance(tr_collision, CollisionDoubleRadiiCapsule) else tr_collision.radius
        radius2 = tr_collision.radius_2 if isinstance(tr_collision, CollisionDoubleRadiiCapsule) else tr_collision.radius
        if radius1 > 100 or radius2 > 100:
            return None

        radius1 *= self.scale_factor
        radius2 *= self.scale_factor

        if tr_collision.length is not None:
            length = tr_collision.length * self.scale_factor
        elif bl_bone is not None and bl_target_bone is not None:
            length = (bl_target_bone.matrix_local.translation - bl_bone.matrix_local.translation).length
        else:
            return None

        bm_mesh = bmesh.new()

        result: dict[str, Any] = bmesh.ops.create_uvsphere(
            bm_mesh,
            u_segments = 16,
            v_segments = 8,
            radius = radius1,
            matrix = Matrix.Translation((0, 0, -length / 2))
        )
        bmesh.ops.delete(
            bm_mesh,
            geom = Enumerable[bmesh.types.BMVert](result["verts"]).where(lambda v: v.co.z > -length / 2 + 0.0001)
                                                                  .to_list()
        )

        result = bmesh.ops.create_uvsphere(
            bm_mesh,
            u_segments = 16,
            v_segments = 8,
            radius = radius2,
            matrix = Matrix.Translation((0, 0, length / 2))
        )
        bmesh.ops.delete(
            bm_mesh,
            geom = Enumerable[bmesh.types.BMVert](result["verts"]).where(lambda v: v.co.z < length / 2 - 0.0001)
                                                                  .to_list()
        )

        bmesh.ops.create_cone(
            bm_mesh,
            segments = 16,
            radius1 = radius1,
            radius2 = radius2,
            depth = length
        )

        bmesh.ops.remove_doubles(bm_mesh, dist = 0.0001, verts = list(cast(Iterable[bmesh.types.BMVert], bm_mesh.verts)))

        bl_mesh = bpy.data.meshes.new("_")
        bm_mesh.to_mesh(bl_mesh)
        bm_mesh.free()

        bl_collision_obj = BlenderHelper.create_object(bl_mesh)
        bpy.ops.object.shade_smooth()
        return bl_collision_obj

    def find_bone(self, bl_armature_obj: bpy.types.Object, global_bone_id: int) -> bpy.types.Bone | None:
        bl_bones = Enumerable(cast(bpy.types.Armature, bl_armature_obj.data).bones)
        return bl_bones.first_or_none(lambda b: BlenderNaming.parse_bone_name(b.name).global_id == global_bone_id)

    def add_armature_modifier(self, bl_mesh_obj: bpy.types.Object, bl_armature_obj: bpy.types.Object, bl_bone: bpy.types.Bone) -> None:
        bl_vertex_group = bl_mesh_obj.vertex_groups.new(name = bl_bone.name)
        bl_vertex_group.add(range(len(cast(bpy.types.Mesh, bl_mesh_obj.data).vertices)), 1.0, "REPLACE")

        bl_mesh_obj.parent = bl_armature_obj
        bl_modifier = cast(bpy.types.ArmatureModifier, bl_mesh_obj.modifiers.new("Armature", "ARMATURE"))
        bl_modifier.object = bl_armature_obj
