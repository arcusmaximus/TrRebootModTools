import bpy
from typing import Annotated
from io_scene_tr_reboot.properties.BlenderPropertyGroup import BlenderAttachedPropertyGroup, Prop

class MaterialProperties(BlenderAttachedPropertyGroup[bpy.types.Material]):
    property_name = "tr11_properties"

    double_sided: Annotated[bool, Prop("Double-sided")]
