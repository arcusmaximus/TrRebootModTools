import bpy
from io_scene_tr_reboot.BlenderHelper import BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.exchange.CollisionImporter import CollisionImporter
from io_scene_tr_reboot.properties.ObjectProperties import ObjectProperties
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.tr.CollisionModel import CollisionMesh, CollisionModel
from io_scene_tr_reboot.util.Enumerable import Enumerable
import colorsys

class CollisionModelImporter(CollisionImporter):
    scale_factor: float
    bl_target_collection: bpy.types.Collection | None

    def __init__(self, scale_factor: float, bl_target_collection: bpy.types.Collection | None = None) -> None:
        self.scale_factor = scale_factor
        self.bl_target_collection = bl_target_collection

    def import_from_collection(self, tr_collection: Collection, bl_collection_obj: bpy.types.Object) -> list[bpy.types.Object]:
        bl_objs: list[bpy.types.Object] = []
        for tr_instance in tr_collection.get_model_instances():
            bl_objs.extend(self.import_model_instance(tr_collection, tr_instance, bl_collection_obj))

        return bl_objs

    def import_model_instance(self, tr_collection: Collection, tr_instance: Collection.ModelInstance, bl_collection_obj: bpy.types.Object) -> list[bpy.types.Object]:
        if tr_instance.collision_model_resource is None:
            return []

        tr_model = tr_collection.get_collision_model(tr_instance.collision_model_resource)
        if tr_model is None or len(tr_model.meshes) == 0:
            return []

        bl_collisions_empty = self.get_or_create_collision_empty(tr_collection, bl_collection_obj)
        bl_materials = self.import_materials(tr_model)

        bl_mesh_objs: list[bpy.types.Object] = []
        transform = tr_instance.transform.copy()
        transform.translation = transform.translation * self.scale_factor
        for mesh_idx, tr_mesh in enumerate(tr_model.meshes):
            mesh_name = BlenderNaming.make_collision_mesh_name(tr_collection.name, tr_collection.id, tr_instance.collision_model_resource.id, mesh_idx)
            bl_mesh_obj = self.import_mesh(tr_mesh, mesh_name, bl_materials)
            if bl_mesh_obj is None:
                continue

            bl_mesh_obj.parent = bl_collisions_empty
            bl_mesh_obj.matrix_local = transform
            bl_mesh_obj.hide_set(True)
            bl_mesh_obj.hide_render = True
            BlenderHelper.move_object_to_collection(bl_mesh_obj, self.bl_target_collection)
            bl_mesh_objs.append(bl_mesh_obj)

        return bl_mesh_objs

    def import_materials(self, tr_model: CollisionModel) -> dict[int, bpy.types.Material]:
        bl_materials: dict[int, bpy.types.Material] = {}
        for material_id in Enumerable(tr_model.meshes).select_many(lambda m: m.material_ids).distinct():
            material_name = BlenderNaming.make_collision_material_name(material_id)
            bl_material = bpy.data.materials.get(material_name)
            if bl_material is None:
                bl_material = bpy.data.materials.new(material_name)

            bl_materials[material_id] = bl_material

        return bl_materials

    def import_mesh(self, tr_mesh: CollisionMesh, name: str, bl_materials: dict[int, bpy.types.Material]) -> bpy.types.Object | None:
        bl_mesh = bpy.data.meshes.get(name)
        if bl_mesh is None:
            vertices = Enumerable(tr_mesh.vertices).select(lambda v: v * self.scale_factor).to_list()
            faces = Enumerable(tr_mesh.faces).select(lambda f: f.indices).to_list()

            bl_mesh = bpy.data.meshes.new(name)
            bl_mesh.from_pydata(vertices, [], faces, False)
            bl_mesh.update()

            for material_id in tr_mesh.material_ids:
                bl_mesh.materials.append(bl_materials.get(material_id))

            polygon_materials = [-1] * len(bl_mesh.polygons)
            for i, face in enumerate(tr_mesh.faces):
                polygon_materials[i] = face.material_idx

            bl_mesh.polygons.foreach_set("material_index", polygon_materials)

        bl_obj = BlenderHelper.create_object(bl_mesh, name)
        bpy.ops.object.shade_flat()
        ObjectProperties.get_instance(bl_obj).collision_model.collision_type_id = tr_mesh.collision_type_id or 0
        return bl_obj

    @staticmethod
    def assign_material_colors() -> None:
        bl_materials = Enumerable(bpy.data.materials).where(lambda m: BlenderNaming.try_parse_collision_material_name(m.name) is not None).to_list()
        for i, bl_material in enumerate(bl_materials):
            bl_material.diffuse_color = (*colorsys.hsv_to_rgb(i / len(bl_materials), 0.7, 1), 1)
