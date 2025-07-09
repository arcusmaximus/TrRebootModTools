from array import array
import bpy
from typing import Callable, ClassVar, Iterable, TypeVar, cast
from mathutils import Vector
from io_scene_tr_reboot.BlenderHelper import BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.properties.ObjectProperties import ObjectProperties
from io_scene_tr_reboot.properties.SceneProperties import SceneProperties
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.tr.Hashes import Hashes
from io_scene_tr_reboot.tr.Mesh import IMesh
from io_scene_tr_reboot.tr.MeshPart import IMeshPart
from io_scene_tr_reboot.tr.Model import IModel
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey
from io_scene_tr_reboot.tr.Skeleton import ISkeleton
from io_scene_tr_reboot.tr.Vertex import Vertex
from io_scene_tr_reboot.util.Enumerable import Enumerable
from io_scene_tr_reboot.util.SlotsBase import SlotsBase

T = TypeVar("T")

class ModelImporter(SlotsBase):
    scale_factor: float
    import_lods: bool
    split_into_parts: bool
    bl_target_collection: bpy.types.Collection | None

    __tr_vertex_idx_attr_name: ClassVar[str] = "tr_vertex_idx"

    def __init__(self, scale_factor: float, import_lods: bool, split_into_parts: bool, bl_target_collection: bpy.types.Collection | None = None) -> None:
        self.scale_factor = scale_factor
        self.import_lods = import_lods
        self.split_into_parts = split_into_parts
        self.bl_target_collection = bl_target_collection

    def import_from_collection(
        self,
        tr_collection: Collection,
        bl_collection_obj: bpy.types.Object,
        bl_armature_objs: dict[ResourceKey, bpy.types.Object]
    ) -> list[bpy.types.Object]:
        bl_mesh_objs = self.import_model_instances(tr_collection, bl_collection_obj, bl_armature_objs)
        self.store_collection_files(tr_collection)
        return bl_mesh_objs

    def import_model_instances(
        self,
        tr_collection: Collection,
        bl_collection_obj: bpy.types.Object,
        bl_armature_objs: dict[ResourceKey, bpy.types.Object]
    ) -> list[bpy.types.Object]:
        bl_mesh_objs: list[bpy.types.Object] = []

        for tr_instance in tr_collection.get_model_instances():
            bl_mesh_objs.extend(self.import_model_instance(tr_collection, tr_instance, bl_collection_obj, bl_armature_objs))

        return bl_mesh_objs

    def import_model_instance(
        self,
        tr_collection: Collection,
        tr_instance: Collection.ModelInstance,
        bl_collection_obj: bpy.types.Object,
        bl_armature_objs: dict[ResourceKey, bpy.types.Object]
    ) -> list[bpy.types.Object]:
        tr_model = tr_collection.get_model(tr_instance.model_resource)
        if tr_model is None:
            return []

        tr_skeleton = tr_collection.get_skeleton(tr_instance.skeleton_resource) if tr_instance.skeleton_resource is not None else None
        bl_mesh_objs = self.import_model(tr_collection, tr_model, tr_skeleton)

        for bl_mesh_obj in bl_mesh_objs:
            transform = tr_instance.transform.copy()
            transform.translation = transform.translation * self.scale_factor
            bl_mesh_obj.matrix_local = transform

        if tr_instance.skeleton_resource is not None:
            self.parent_objects_to_armature(bl_mesh_objs, bl_armature_objs[tr_instance.skeleton_resource])
        else:
            for bl_mesh_obj in bl_mesh_objs:
                bl_mesh_obj.parent = bl_collection_obj

        return bl_mesh_objs

    def import_model(self, tr_collection: Collection, tr_model: IModel, tr_skeleton: ISkeleton | None) -> list[bpy.types.Object]:
        bpy.ops.object.select_all(action = "DESELECT")

        bl_mesh_objs: list[bpy.types.Object] = []

        self.remove_shadow_meshes(tr_model)
        if self.import_lods:
            self.separate_lods(tr_model)
        else:
            self.remove_lods(tr_model)

        if self.split_into_parts:
            self.split_meshes_into_parts(tr_model)
        else:
            self.split_meshes_by_draw_group_and_flags(tr_model)

        for i, tr_mesh in enumerate(tr_model.meshes):
            bl_mesh_name = BlenderNaming.make_mesh_name(tr_collection.name, tr_collection.id, tr_model.id, tr_model.refs.model_data_resource and tr_model.refs.model_data_resource.id or 0, i)
            bl_obj = self.import_mesh(tr_collection, tr_model, tr_mesh, tr_skeleton, bl_mesh_name)
            if bl_obj is not None:
                bl_mesh_objs.append(bl_obj)

        return bl_mesh_objs

    def import_mesh(self, tr_collection: Collection, tr_model: IModel, tr_mesh: IMesh, tr_skeleton: ISkeleton | None, name: str) -> bpy.types.Object | None:
        bl_obj: bpy.types.Object
        bl_mesh = bpy.data.meshes.get(name)
        if bl_mesh is None:
            bl_mesh = self.create_mesh_data(tr_mesh, name)
            if bl_mesh is None:
                return None

            bl_obj = self.create_mesh_object(bl_mesh, tr_model, tr_mesh)
            self.create_color_maps(bl_mesh, tr_mesh)
            self.create_uv_maps(bl_mesh, tr_mesh)
            self.apply_materials(bl_mesh, tr_collection, tr_model, tr_mesh)
            self.create_vertex_groups(bl_obj, bl_mesh, tr_mesh, tr_skeleton)
            self.create_shape_keys(bl_obj, tr_mesh, tr_skeleton)
            self.clean_mesh(bl_mesh)

            has_blend_shapes = Enumerable(tr_mesh.blend_shapes).any(lambda b: b is not None)
            if not has_blend_shapes:
                self.apply_vertex_normals(bl_mesh, tr_mesh)

            bl_mesh.attributes.remove(bl_mesh.attributes[ModelImporter.__tr_vertex_idx_attr_name])
            bpy.ops.object.shade_smooth()
        else:
            bl_obj = self.create_mesh_object(bl_mesh, tr_model, tr_mesh)

        BlenderHelper.move_object_to_collection(bl_obj, self.bl_target_collection)
        return bl_obj

    def create_mesh_data(self, tr_mesh: IMesh, name: str) -> bpy.types.Mesh | None:
        vertices: list[Vector] = [self.get_vertex_position(vertex) for vertex in tr_mesh.vertices]
        faces: list[tuple[int, ...]] = []
        for tr_mesh_part in tr_mesh.parts:
            for i in range(0, len(tr_mesh_part.indices), 3):
                faces.append((tr_mesh_part.indices[i], tr_mesh_part.indices[i + 1], tr_mesh_part.indices[i + 2]))

        if len(faces) == 0:
            return None

        bl_mesh: bpy.types.Mesh = bpy.data.meshes.new(name)
        bl_mesh.from_pydata(vertices, [], faces, False)
        bl_mesh.update()

        bl_tr_vertex_idx_attr = cast(bpy.types.IntAttribute, bl_mesh.attributes.new(ModelImporter.__tr_vertex_idx_attr_name, "INT", "POINT"))
        bl_tr_vertex_idx_attr.data.foreach_set("value", range(len(tr_mesh.vertices)))
        return bl_mesh

    def create_mesh_object(self, bl_mesh: bpy.types.Mesh, tr_model: IModel, tr_mesh: IMesh) -> bpy.types.Object:
        bl_obj = BlenderHelper.create_object(bl_mesh)
        props = ObjectProperties.get_instance(bl_obj).mesh
        props.draw_group_id = tr_mesh.parts[0].draw_group_id
        props.flags = tr_mesh.parts[0].flags

        if self.import_lods:
            object_width = (tr_model.header.bound_box_max.x - tr_model.header.bound_box_min.x) * self.scale_factor
            bl_obj.location = Vector((max(tr_mesh.parts[0].lod_level - 1, 0) * object_width * 1.5, 0, 0))

        return bl_obj

    def create_color_maps(self, bl_mesh: bpy.types.Mesh, tr_mesh: IMesh) -> None:
        for color_map_idx, attr_name_hash in enumerate([Hashes.color1, Hashes.color2]):
            if not tr_mesh.vertex_format.has_attribute(attr_name_hash):
                continue

            bl_color_map = cast(bpy.types.ByteColorAttribute, bl_mesh.color_attributes.new(BlenderNaming.make_color_map_name(color_map_idx), "BYTE_COLOR", "POINT"))
            bl_color_map.data.foreach_set(
                "color",
                [component for vertex in tr_mesh.vertices for component in vertex.attributes[attr_name_hash]]
            )

    def create_uv_maps(self, bl_mesh: bpy.types.Mesh, tr_mesh: IMesh) -> None:
        for uv_map_idx, attr_name_hash in enumerate([Hashes.texcoord1, Hashes.texcoord2, Hashes.texcoord3, Hashes.texcoord4]):
            if not tr_mesh.vertex_format.has_attribute(attr_name_hash):
                continue

            uv_layer = bl_mesh.uv_layers.new(name = BlenderNaming.make_uv_map_name(uv_map_idx))
            uv_layer.data.foreach_set(
                "uv",
                [coord for tr_mesh_part in tr_mesh.parts for index in tr_mesh_part.indices for coord in self.get_vertex_uv(tr_mesh.vertices[index], attr_name_hash)]
            )

    def get_vertex_uv(self, vertex: Vertex, attr_name_hash: int) -> tuple[float, float]:
        uv: tuple[float, ...] = vertex.attributes[attr_name_hash]
        return (16 * uv[0], 1 - 16 * uv[1])

    def apply_materials(self, bl_mesh: bpy.types.Mesh, tr_collection: Collection, tr_model: IModel, tr_mesh: IMesh) -> None:
        polygon_idx_base: int = 0
        material_slot_by_name: dict[str, int] = {}
        polygon_material_slots = [-1] * len(bl_mesh.polygons)

        for tr_mesh_part in tr_mesh.parts:
            material_resource = tr_mesh_part.material_idx >= 0 and tr_model.refs.material_resources[tr_mesh_part.material_idx] or None
            if material_resource is None:
                continue

            material_name = BlenderNaming.make_material_name(tr_collection.get_resource_name(material_resource), material_resource.id)
            bl_material = bpy.data.materials.get(material_name)
            if bl_material is None:
                continue

            material_slot = material_slot_by_name.get(bl_material.name)
            if material_slot is None:
                material_slot = len(bl_mesh.materials)
                bl_mesh.materials.append(bl_material)
                material_slot_by_name[bl_material.name] = material_slot

            num_polygons = len(tr_mesh_part.indices) // 3
            for i in range(polygon_idx_base, polygon_idx_base + num_polygons):
                polygon_material_slots[i] = material_slot

            polygon_idx_base += num_polygons

        bl_mesh.polygons.foreach_set("material_index", polygon_material_slots)

    def create_vertex_groups(self, bl_obj: bpy.types.Object, bl_mesh: bpy.types.Mesh, tr_mesh: IMesh, tr_skeleton: ISkeleton | None) -> None:
        if not tr_mesh.vertex_format.has_attribute(Hashes.skin_indices):
            return

        has_8_weights_per_vertex = self.get_consistent_flag(tr_mesh.parts, lambda p: p.has_8_weights_per_vertex, "has_8_weights_per_vertex")
        has_16bit_skin_indices   = self.get_consistent_flag(tr_mesh.parts, lambda p: p.has_16bit_skin_indices, "has_16bit_skin_indices")

        bone_index_mask: int
        bone_index_shift: int
        if has_8_weights_per_vertex and not has_16bit_skin_indices:
            bone_index_mask = 0xFF
            bone_index_shift = 8
        else:
            bone_index_mask = 0x3FF
            bone_index_shift = 16

        vertex_groups: list[array[float]] = []
        for local_bone_id in tr_mesh.bone_indices:
            vertex_groups.append(array("f", [0]) * len(tr_mesh.vertices))

        for vertex_idx, vertex in enumerate(tr_mesh.vertices):
            bone_indices = vertex.attributes[Hashes.skin_indices]
            weights      = vertex.attributes.get(Hashes.skin_weights)
            for i in range(4):
                for j in range(has_8_weights_per_vertex and 2 or 1):
                    mesh_bone_index = (int(bone_indices[i]) >> (j * bone_index_shift)) & bone_index_mask
                    weight = ((int(weights[i]) >> (j * 8)) & 0xFF) / 255.0 if weights is not None else 1.0
                    vertex_groups[mesh_bone_index][vertex_idx] += weight

        for mesh_bone_index, weights in enumerate(vertex_groups):
            local_bone_id = tr_mesh.bone_indices[mesh_bone_index]
            global_bone_id = tr_skeleton.bones[local_bone_id].global_id if tr_skeleton is not None else None
            vertex_group_name = BlenderNaming.make_bone_name(None, global_bone_id, local_bone_id)
            if bl_obj.vertex_groups.get(vertex_group_name) is not None:
                continue

            bl_attr = cast(bpy.types.FloatAttribute, bl_mesh.attributes.new(vertex_group_name, "FLOAT", "POINT"))
            bl_attr.data.foreach_set("value", weights)
            bl_mesh.attributes.active = bl_attr
            bpy.ops.geometry.attribute_convert(mode = "VERTEX_GROUP", domain = "POINT", data_type = "FLOAT")

    def create_shape_keys(self, bl_obj: bpy.types.Object, tr_mesh: IMesh, tr_skeleton: ISkeleton | None) -> None:
        if not Enumerable(tr_mesh.blend_shapes).any(lambda b: b is not None):
            return

        bl_obj.shape_key_add(name = "Basis")

        for local_blend_shape_id, tr_blendshape in enumerate(tr_mesh.blend_shapes):
            if tr_blendshape is None:
                continue

            global_blend_shape_id = tr_skeleton.global_blend_shape_ids.get(local_blend_shape_id) if tr_skeleton is not None else None
            bl_shape_key = bl_obj.shape_key_add(name = BlenderNaming.make_shape_key_name(tr_blendshape.name, global_blend_shape_id, local_blend_shape_id), from_mix = False)
            for vertex_idx, offsets in tr_blendshape.vertices.items():
                shape_key_point = cast(bpy.types.ShapeKeyPoint, bl_shape_key.data[vertex_idx])
                base_pos = Vector(tr_mesh.vertices[vertex_idx].attributes[Hashes.position])
                shape_key_point.co = (base_pos + offsets.position_offset) * self.scale_factor

    def clean_mesh(self, bl_mesh: bpy.types.Mesh) -> None:
        had_double_sided_faces = bl_mesh.validate()
        with BlenderHelper.enter_edit_mode():
            bpy.ops.mesh.select_all(action = "SELECT")
            bpy.ops.mesh.delete_loose()
            if had_double_sided_faces:
                bpy.ops.mesh.select_all(action = "SELECT")
                bpy.ops.mesh.remove_doubles()
                bpy.ops.mesh.normals_make_consistent()

            bpy.ops.mesh.select_all(action = "DESELECT")

    def apply_vertex_normals(self, bl_mesh: bpy.types.Mesh, tr_mesh: IMesh) -> None:
        if not tr_mesh.vertex_format.has_attribute(Hashes.normal):
            return

        tr_vertex_indices = [0] * len(bl_mesh.vertices)
        bl_tr_vertex_idx_attr = cast(bpy.types.IntAttribute, bl_mesh.attributes[ModelImporter.__tr_vertex_idx_attr_name])
        bl_tr_vertex_idx_attr.data.foreach_get("value", tr_vertex_indices)
        normals: list[Vector] = [self.get_vertex_normal(tr_mesh.vertices[tr_vertex_idx]) for tr_vertex_idx in tr_vertex_indices]
        bl_mesh.normals_split_custom_set_from_vertices(normals)     # type: ignore

        if hasattr(bl_mesh, "use_auto_smooth"):
            setattr(bl_mesh, "use_auto_smooth", True)

    def parent_objects_to_armature(self, bl_objs: Iterable[bpy.types.Object], bl_armature_obj: bpy.types.Object) -> None:
        used_bone_names: set[str] = set()

        for bl_obj in bl_objs:
            bl_obj.parent = bl_armature_obj
            bl_armature_modifier = cast(bpy.types.ArmatureModifier, bl_obj.modifiers.new("Armature", "ARMATURE"))
            bl_armature_modifier.object = bl_armature_obj
            used_bone_names.update(Enumerable(bl_obj.vertex_groups).select(lambda g: g.name).where(lambda n: BlenderNaming.try_parse_bone_name(n) is not None))

        bl_armature = cast(bpy.types.Armature, bl_armature_obj.data)
        for bl_bone in bl_armature.bones:
            if BlenderNaming.try_parse_bone_name(bl_bone.name) is not None:
                BlenderHelper.add_bone_to_group(bl_armature_obj, bl_bone, BlenderNaming.non_deforming_bone_group_name, None, False)

        for bl_bone in bl_armature.bones:
            if bl_bone.name not in used_bone_names:
                continue

            while True:
                BlenderHelper.remove_bone_from_group(bl_bone, BlenderNaming.non_deforming_bone_group_name)
                bl_bone = bl_bone.parent
                if bl_bone is None or bl_bone.name in used_bone_names:
                    break

                used_bone_names.add(bl_bone.name)

    def remove_shadow_meshes(self, tr_model: IModel) -> None:
        for i in range(len(tr_model.meshes) - 1, -1, -1):
            tr_mesh = tr_model.meshes[i]
            for j in range(len(tr_mesh.parts) - 1, -1, -1):
                if tr_mesh.parts[j].is_shadow:
                    del tr_mesh.parts[j]

            if len(tr_mesh.parts) == 0:
                del tr_model.meshes[i]

    def separate_lods(self, tr_model: IModel) -> None:
        new_tr_meshes: list[IMesh] = []
        for tr_mesh in tr_model.meshes:
            for tr_mesh_parts in Enumerable(tr_mesh.parts).group_by(lambda p: p.lod_level).values():
                new_tr_meshes.append(self.make_tr_mesh(tr_mesh, tr_mesh_parts))

        tr_model.meshes = new_tr_meshes

    def remove_lods(self, tr_model: IModel) -> None:
        for i in range(len(tr_model.meshes) - 1, -1, -1):
            tr_mesh = tr_model.meshes[i]
            for j in range(len(tr_mesh.parts) - 1, -1, -1):
                lod_level = tr_mesh.parts[j].lod_level - 1
                if lod_level >= 0 and lod_level < len(tr_model.lod_levels) and tr_model.lod_levels[lod_level].min > 0:
                    del tr_mesh.parts[j]

            if len(tr_mesh.parts) == 0:
                del tr_model.meshes[i]

    def split_meshes_into_parts(self, tr_model: IModel) -> None:
        new_tr_meshes: list[IMesh] = []
        for tr_mesh in tr_model.meshes:
            for tr_mesh_part in tr_mesh.parts:
                new_tr_meshes.append(self.make_tr_mesh(tr_mesh, [tr_mesh_part]))

        tr_model.meshes = new_tr_meshes

    def split_meshes_by_draw_group_and_flags(self, tr_model: IModel) -> None:
        new_tr_meshes: list[IMesh] = []
        for tr_mesh in tr_model.meshes:
            for tr_mesh_parts in Enumerable(tr_mesh.parts).group_by(lambda p: (p.draw_group_id, p.flags)).values():
                new_tr_meshes.append(self.make_tr_mesh(tr_mesh, tr_mesh_parts))

        tr_model.meshes = new_tr_meshes

    def make_tr_mesh(self, tr_mesh: IMesh, tr_mesh_parts: list[IMeshPart]) -> IMesh:
        new_tr_mesh = tr_mesh.clone()
        new_tr_mesh.parts = tr_mesh_parts
        return new_tr_mesh

    def get_vertex_position(self, vertex: Vertex) -> Vector:
        attr = vertex.attributes[Hashes.position]
        return Vector((attr[0], attr[1], attr[2])) * self.scale_factor

    def get_vertex_normal(self, vertex: Vertex) -> Vector:
        attr: tuple[float, ...] = vertex.attributes[Hashes.normal]
        x, y, z = attr[0]*2 - 1, attr[1]*2 - 1, attr[2]*2 - 1
        normal = Vector((x, y, z))
        normal.normalize()
        return normal

    def get_consistent_flag(self, items: Iterable[T], get_flag: Callable[[T], bool], flag_name: str) -> bool:
        flags = Enumerable(items).select(get_flag).distinct().to_list()
        if len(flags) == 2:
            raise Exception(f"Inconsistent flag {flag_name} in mesh parts")

        return flags[0]

    def store_collection_files(self, tr_collection: Collection) -> None:
        for file_id, file_data in self.get_collection_files_to_store(tr_collection).items():
            SceneProperties.set_file(file_id, file_data)

    def get_collection_files_to_store(self, tr_collection: Collection) -> dict[int, bytes]:
        return {}
