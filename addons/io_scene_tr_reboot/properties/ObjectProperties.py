from typing import Annotated
import bpy
from io_scene_tr_reboot.properties.BlenderPropertyGroup import BlenderAttachedPropertyGroup, BlenderPropertyGroup, Prop
from io_scene_tr_reboot.util.Enumerable import Enumerable
from io_scene_tr_reboot.util.Serializer import Serializer

def search_bones(self: BlenderPropertyGroup, context: bpy.types.Context, edit_text: str) -> list[str]:
    bl_armature_obj = context.object
    while True:
        if not bl_armature_obj:
            return [""]

        if isinstance(bl_armature_obj.data, bpy.types.Armature):
            break

        bl_armature_obj = bl_armature_obj.parent

    return Enumerable(bl_armature_obj.data.bones).select(lambda b: b.name) \
                                                 .order_by(lambda b: b) \
                                                 .where(lambda b: b.startswith(edit_text)) \
                                                 .to_list()

class ObjectClothProperties(BlenderPropertyGroup):
    parent_bone_name: Annotated[str, Prop("Parent", search = search_bones)]
    gravity_factor: Annotated[float, Prop("Gravity Factor", min = -2, max = 2, default = 1)]
    buoyancy_factor: Annotated[float, Prop("Buoyancy", min = 0, max = 1, default = 0.5, subtype = "FACTOR")]
    dampening: Annotated[float, Prop("Drag", min = 0, max = 1, default = 0.2, subtype = "FACTOR")]
    wind_factor: Annotated[float, Prop("Wind Factor", min = 0, subtype = "FACTOR")]
    stiffness: Annotated[float, Prop("Pose Follow Factor", min = 0, max = 1, subtype = "FACTOR")]
    rigidity: Annotated[float, Prop("Rigidity", min = 0, max = 1, default = 0.6, subtype = "FACTOR")]
    bounceback_factor: Annotated[float, Prop("Bounceback Strength", min = 0, max = 1, subtype = "FACTOR")]

    transform_type: Annotated[int, Prop("Transform Type", min = 0, max = 2, default = 1)]
    max_velocity_iterations: Annotated[int, Prop("Max Velocity Iterations", min = 0, default = 3)]
    max_position_iterations: Annotated[int, Prop("Max Position Iteration", min = 0, default = 2)]
    relaxation_iterations: Annotated[int, Prop("Relaxation Iterations", min = 0, default = 5)]
    sub_step_count: Annotated[int, Prop("Sub Step Count", min = 0, default = 2)]
    fixed_to_free_slop: Annotated[float, Prop("Fixed to Free Slop", min = 0, max = 1, default = 0, subtype = "FACTOR")]
    free_to_free_slop: Annotated[float, Prop("Free to Free Slop", min = 0, max = 1, default = 0, subtype = "FACTOR")]
    free_to_free_slop_z: Annotated[float, Prop("Free to Free Slop Z", min = 0, max = 1, default = 0, subtype = "FACTOR")]
    mass_scale: Annotated[float, Prop("Mass Scale", min = 0, default = 20)]
    time_delta_scale: Annotated[float, Prop("Time Delta Scale", min = 0, default = 1)]
    blend_to_bind_time: Annotated[float, Prop("Blend to Bind Time", min = 0, default = 8)]
    is_hair_collider: Annotated[bool, Prop("Hair Collider")]

class ObjectCollisionProperties(BlenderPropertyGroup):
    data: Annotated[str, Prop("Data")]

class ObjectMeshProperties(BlenderPropertyGroup):
    draw_group_id: Annotated[int, Prop("Draw Group ID")]
    flags: Annotated[int, Prop("Flags")]

class ObjectSkeletonProperties(BlenderPropertyGroup):
    global_blend_shape_ids: Annotated[str, Prop("Local -> global blend shape ID mappings")]

    @staticmethod
    def get_global_blend_shape_ids(bl_armature_obj: bpy.types.Object) -> dict[int, int]:
        serialized_mappings = ObjectProperties.get_instance(bl_armature_obj).skeleton.global_blend_shape_ids
        if not serialized_mappings:
            return {}

        mappings = Serializer.deserialize_dict(serialized_mappings)
        return Enumerable(mappings.items()).to_dict(lambda p: int(p[0]), lambda p: int(p[1]))

    @staticmethod
    def set_global_blend_shape_ids(bl_armature_obj: bpy.types.Object, mappings: dict[int, int]) -> None:
        ObjectProperties.get_instance(bl_armature_obj).skeleton.global_blend_shape_ids = Serializer.serialize_dict(mappings)

class ObjectProperties(BlenderAttachedPropertyGroup[bpy.types.Object]):
    property_name = "tr11_properties"

    blend_shape_normals_source_file_path: Annotated[str, Prop("Shape Key Normals Source", description = ".trXmodeldata file to copy shape key normals from")]
    cloth: Annotated[ObjectClothProperties, Prop("Cloth properties")]
    collision: Annotated[ObjectCollisionProperties, Prop("Collision properties")]
    mesh: Annotated[ObjectMeshProperties, Prop("Mesh properties")]
    skeleton: Annotated[ObjectSkeletonProperties, Prop("Skeleton properties")]
