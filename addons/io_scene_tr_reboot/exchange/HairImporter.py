from array import array
from typing import cast
import bpy
from io_scene_tr_reboot.BlenderHelper import BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.exchange.MaterialImporter import MaterialImporter
from io_scene_tr_reboot.properties.SceneProperties import SceneProperties
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.tr.Enumerations import ResourceType
from io_scene_tr_reboot.tr.Hair import Hair
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey

class HairImporter:
    scale_factor: float
    material_importer: MaterialImporter

    def __init__(self, scale_factor: float) -> None:
        self.scale_factor = scale_factor
        self.material_importer = MaterialImporter()

    def import_from_collection(self, tr_collection: Collection, bl_armature_obj: bpy.types.Object | None) -> list[bpy.types.Object]:
        tr_hair = tr_collection.get_hair()
        if tr_hair is None:
            return []

        bl_hair = bpy.data.hair_curves.new(BlenderNaming.make_hair_name(tr_collection.name, tr_hair.model_id, tr_hair.hair_data_id))

        bl_hair.add_curves([16] * (len(tr_hair.points) // 16))
        self.apply_point_positions(bl_hair, tr_hair)
        self.apply_point_thicknesses(bl_hair, tr_hair)
        self.apply_point_weights(bl_hair, tr_hair)

        if tr_hair.material_id is not None:
            bl_material = self.material_importer.import_material(tr_collection, ResourceKey(ResourceType.MATERIAL, tr_hair.material_id))
            if bl_material is not None:
                bl_hair.materials.append(bl_material)

        bl_obj = BlenderHelper.create_object(bl_hair)
        bl_obj.parent = bl_armature_obj

        self.store_collection_files(tr_collection)

        return [bl_obj]

    def apply_point_positions(self, bl_hair: bpy.types.Curves, tr_hair: Hair) -> None:
        coords = array("f", [0]) * (len(tr_hair.points) * 3)
        coord_idx = 0
        for tr_point in tr_hair.points:
            coords[coord_idx + 0] = tr_point.position[0] * self.scale_factor
            coords[coord_idx + 1] = tr_point.position[1] * self.scale_factor
            coords[coord_idx + 2] = tr_point.position[2] * self.scale_factor
            coord_idx += 3

        bl_hair.points.foreach_set("position", coords)

    def apply_point_thicknesses(self, bl_hair: bpy.types.Curves, tr_hair: Hair) -> None:
        thicknesses = array("f", [0]) * len(tr_hair.points)
        for i, tr_point in enumerate(tr_hair.points):
            thicknesses[i] = tr_point.thickness * self.scale_factor

        bl_hair.points.foreach_set("radius", thicknesses)

    def apply_point_weights(self, bl_hair: bpy.types.Curves, tr_hair: Hair) -> None:
        global_bone_weights: dict[int, array[float]] = {}
        for i, tr_point in enumerate(tr_hair.points):
            for tr_weight in tr_point.weights:
                weight_array = global_bone_weights.get(tr_weight.global_bone_id)
                if weight_array is None:
                    weight_array = array("f", [0]) * len(tr_hair.points)
                    global_bone_weights[tr_weight.global_bone_id] = weight_array

                weight_array[i] = tr_weight.weight

        for global_bone_id, weight_array in global_bone_weights.items():
            bl_bone_attr = cast(bpy.types.FloatAttribute, bl_hair.attributes.new(BlenderNaming.make_bone_name(None, global_bone_id, None), "FLOAT", "POINT"))
            bl_bone_attr.data.foreach_set("value", weight_array)

    def store_collection_files(self, tr_collection: Collection) -> None:
        for file_id, file_data in self.get_collection_files_to_store(tr_collection).items():
            SceneProperties.set_file(file_id, file_data)

    def get_collection_files_to_store(self, tr_collection: Collection) -> dict[int, bytes]:
        return {}
