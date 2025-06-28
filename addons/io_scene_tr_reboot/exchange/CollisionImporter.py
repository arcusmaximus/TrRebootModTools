import bpy
from io_scene_tr_reboot.BlenderHelper import BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.util.Enumerable import Enumerable

class CollisionImporter:
    scale_factor: float
    bl_target_collection: bpy.types.Collection | None

    def __init__(self, scale_factor: float, bl_target_collection: bpy.types.Collection | None = None) -> None:
        self.scale_factor = scale_factor
        self.bl_target_collection = bl_target_collection

    def get_or_create_collision_empty(self, tr_collection: Collection, bl_collection_obj: bpy.types.Object) -> bpy.types.Object:
        bl_collisions_empty_name = BlenderNaming.make_collision_empty_name(tr_collection.name)
        bl_collisions_empty = Enumerable(bl_collection_obj.children).first_or_none(lambda o: o.name.startswith(bl_collisions_empty_name))
        if bl_collisions_empty is None:
            bl_collisions_empty = BlenderHelper.create_object(None, bl_collisions_empty_name)
            bl_collisions_empty.hide_set(True)
            bl_collisions_empty.parent = bl_collection_obj
            BlenderHelper.move_object_to_collection(bl_collisions_empty, self.bl_target_collection)

        return bl_collisions_empty
