from typing import Annotated, Any, Iterable, cast
import bpy
import bmesh
from io_scene_tr_reboot.BlenderHelper import BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.properties.BlenderPropertyGroup import BlenderAttachedPropertyGroup, Prop
from io_scene_tr_reboot.properties.BoneProperties import BoneProperties
from io_scene_tr_reboot.util.Enumerable import Enumerable

def get_cloth_bone_bounceback(self: bpy.types.Property) -> float:
    bl_context = bpy.context
    if bl_context.object is None:
        return 0

    bl_armature = bl_context.object.data
    if not isinstance(bl_armature, bpy.types.Armature) or bl_context.mode != "POSE":
        return 0

    bl_pose_bones = Enumerable(bl_context.selected_pose_bones)
    bl_pose_bone = bl_pose_bones.first_or_none(lambda b: b == bl_context.active_pose_bone) or bl_pose_bones.first_or_none()
    if not bl_pose_bone:
        return 0

    return BoneProperties.get_instance(bl_pose_bone.bone).cloth.bounceback_factor

def set_cloth_bone_bounceback(self: bpy.types.Property, value: float) -> None:
    bl_context = bpy.context
    if bl_context.object is None:
        return

    bl_armature = bl_context.object.data
    if not isinstance(bl_armature, bpy.types.Armature) or bl_context.mode != "POSE":
        return

    for bl_pose_bone in bl_context.selected_pose_bones:
        bone_id_set = BlenderNaming.try_parse_bone_name(bl_pose_bone.name)
        if bone_id_set is not None and bone_id_set.global_id is None:
            BoneProperties.get_instance(bl_pose_bone.bone).cloth.bounceback_factor = value

def get_cloth_spring_stretchiness(self: bpy.types.Property) -> float:
    bl_context = bpy.context
    if bl_context.object is None or bl_context.object.mode != "EDIT":
        return 0

    bl_mesh = bl_context.object.data
    if not isinstance(bl_mesh, bpy.types.Mesh):
        return 0

    bl_bmesh = bmesh.from_edit_mesh(bl_mesh)

    bl_active_item = bl_bmesh.select_history.active
    if isinstance(bl_active_item, bmesh.types.BMEdge):
        return BlenderHelper.get_edge_bevel_weight(bl_bmesh, bl_active_item.index)

    bl_selected_edge = Enumerable(cast(Iterable[Any], bl_bmesh.select_history)).of_type(bmesh.types.BMEdge).last_or_none() or \
                       Enumerable(cast(Iterable[bmesh.types.BMEdge], bl_bmesh.edges)).first_or_none(lambda e: e.select)
    if bl_selected_edge is not None:
        return BlenderHelper.get_edge_bevel_weight(bl_bmesh, bl_selected_edge.index)

    return 0

def set_cloth_spring_stretchiness(self: bpy.types.Property, value: float) -> None:
    bl_context = bpy.context
    if bl_context.object is None or bl_context.object.mode != "EDIT":
        return

    bl_mesh = bl_context.object.data
    if not isinstance(bl_mesh, bpy.types.Mesh):
        return

    bl_bmesh = bmesh.from_edit_mesh(bl_mesh)
    for bl_edge in Enumerable(cast(Iterable[bmesh.types.BMEdge], bl_bmesh.edges)).where(lambda e: e.select):
        BlenderHelper.set_edge_bevel_weight(bl_bmesh, bl_edge.index, value)

class ToolSettingProperties(BlenderAttachedPropertyGroup[bpy.types.Scene]):
    property_name = "tr11_tool_properties"
    cloth_bone_bounceback_factor: Annotated[float, Prop("Bounceback Strength", min = 0, max = 1, subtype = "FACTOR", get = get_cloth_bone_bounceback, set = set_cloth_bone_bounceback)]
    cloth_spring_stretchiness: Annotated[float, Prop("Stretchiness", min = 0, max = 1, subtype = "FACTOR", get = get_cloth_spring_stretchiness, set = set_cloth_spring_stretchiness)]
