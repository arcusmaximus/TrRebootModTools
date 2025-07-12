from typing import Annotated
import bpy
from io_scene_tr_reboot.properties.BlenderPropertyGroup import BlenderAttachedPropertyGroup, BlenderPropertyGroup, BlenderPropertyGroupCollection, Prop
from io_scene_tr_reboot.util.Enumerable import Enumerable

def search_bones(self: BlenderPropertyGroup, context: bpy.types.Context, edit_text: str) -> list[str]:
    if not context or not context.armature:
        return [""]

    return Enumerable(context.armature.bones).select(lambda b: b.name) \
                                             .order_by(lambda b: b) \
                                             .where(lambda b: b.startswith(edit_text)) \
                                             .to_list()

class BoneConstraintProperties(BlenderPropertyGroup):
    data: Annotated[str, Prop("Data")]

class BoneClothProperties(BlenderPropertyGroup):
    bounceback_factor: Annotated[float, Prop("Bounceback Strength", min = 0, max = 1, subtype = "FACTOR")]

class BoneProperties(BlenderAttachedPropertyGroup[bpy.types.Bone]):
    property_name = "tr11_properties"

    counterpart_bone_name: Annotated[str, Prop("Counterpart", search = search_bones)]
    constraints: Annotated[BlenderPropertyGroupCollection[BoneConstraintProperties], Prop("Constraints")]
    cloth: Annotated[BoneClothProperties, Prop("Cloth properties")]
    is_flipped: Annotated[bool, Prop("Is flipped", description = "Has opposite orientation than in original skeletion")]
