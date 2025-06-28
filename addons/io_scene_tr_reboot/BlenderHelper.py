from types import TracebackType
from typing import Any, ClassVar, Iterable, NamedTuple, Sequence, cast
import bpy
import bmesh
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.util.Enumerable import Enumerable
from io_scene_tr_reboot.util.SlotsBase import SlotsBase

class BlenderBoneGroup(NamedTuple):
    name: str
    palette: str

class BlenderHelper:
    is_blender_40: ClassVar[bool] = cast(tuple[int, ...], bpy.app.version) >= (4, 0, 0)

    @staticmethod
    def get_or_create_collection(name: str) -> bpy.types.Collection:
        bl_collection = bpy.data.collections.get(name)
        if bl_collection is None:
            bl_collection = bpy.data.collections.new(name)
            if bpy.context.scene is not None:
                bpy.context.scene.collection.children.link(bl_collection)

        return bl_collection

    @staticmethod
    def is_collection_excluded(bl_collection: bpy.types.Collection) -> bool:
        return BlenderHelper.__get_view_layer_collection(bl_collection).exclude

    @staticmethod
    def set_collection_excluded(bl_collection: bpy.types.Collection, exclude: bool) -> None:
        BlenderHelper.__get_view_layer_collection(bl_collection).exclude = exclude

    @staticmethod
    def __get_view_layer_collection(bl_collection: bpy.types.Collection) -> bpy.types.LayerCollection:
        if bpy.context.view_layer is None:
            raise Exception()

        return Enumerable([bpy.context.view_layer.layer_collection]).with_descendants(lambda c: c.children).first(lambda c: c.collection == bl_collection)

    @staticmethod
    def move_object_to_collection(bl_obj: bpy.types.Object, bl_collection: bpy.types.Collection | None, include_children: bool = False) -> None:
        if bl_collection is None:
            if bpy.context.scene is None:
                return

            bl_collection = bpy.context.scene.collection

        bl_objs = [bl_obj]
        if include_children:
            bl_objs.extend(bl_obj.children_recursive)

        for bl_obj in bl_objs:
            hidden = bl_obj.hide_get() or bl_obj.hide_viewport

            for bl_existing_collection in list(bl_obj.users_collection):
                bl_existing_collection.objects.unlink(bl_obj)

            bl_collection.objects.link(bl_obj)

            if BlenderHelper.is_collection_excluded(bl_collection):
                bl_obj.hide_viewport = hidden
            else:
                bl_obj.hide_viewport = False
                bl_obj.hide_set(hidden)

    @staticmethod
    def create_object(bl_data: bpy.types.ID | None, name: str | None = None) -> bpy.types.Object:
        bl_obj = bpy.data.objects.new(name or (bl_data and bl_data.name) or "", bl_data)
        BlenderHelper.__add_object_to_scene(bl_obj)
        return bl_obj

    @staticmethod
    def duplicate_object(bl_obj: bpy.types.Object, duplicate_data: bool, recursive: bool = False) -> bpy.types.Object:
        bl_copied_obj = bl_obj.copy()
        if duplicate_data and bl_obj.data is not None:
            bl_copied_obj.data = bl_obj.data.copy()

        BlenderHelper.__add_object_to_scene(bl_copied_obj)

        if recursive:
            for bl_child_obj in bl_obj.children:
                bl_copied_child_obj = BlenderHelper.duplicate_object(bl_child_obj, duplicate_data, True)
                bl_copied_child_obj.parent = bl_copied_obj

        return bl_copied_obj

    @staticmethod
    def __add_object_to_scene(bl_obj: bpy.types.Object) -> None:
        if bpy.context.scene is None or bpy.context.view_layer is None:
            return

        bpy.context.scene.collection.objects.link(bl_obj)

        bpy.ops.object.select_all(action = "DESELECT")
        bl_obj.select_set(True)
        bpy.context.view_layer.objects.active = bl_obj

    @staticmethod
    def select_object(bl_obj: bpy.types.Object) -> None:
        if bpy.context.view_layer is None:
            return

        bpy.ops.object.select_all(action = "DESELECT")
        bl_obj.hide_set(False)
        bl_obj.select_set(True)
        bpy.context.view_layer.objects.active = bl_obj

    @staticmethod
    def enter_edit_mode(bl_obj: bpy.types.Object | None = None) -> "BlenderEditContext":
        if bpy.context and bpy.context.object and not bpy.context.object.hide_get():
            bpy.ops.object.mode_set(mode = "OBJECT")

        if bl_obj is not None:
            BlenderHelper.select_object(bl_obj)

        return BlenderEditContext()

    @staticmethod
    def join_objects(bl_target_obj: bpy.types.Object, bl_source_objs: Iterable[bpy.types.Object]) -> None:
        if bpy.context.view_layer is None:
            return

        bpy.ops.object.select_all(action = "DESELECT")
        for bl_source_obj in bl_source_objs:
            bl_source_obj.select_set(True)

        bl_target_obj.select_set(True)
        bpy.context.view_layer.objects.active = bl_target_obj
        bpy.ops.object.join()

    @staticmethod
    def prepare_for_model_export(bl_obj: bpy.types.Object) -> "BlenderModelExportContext":
        return BlenderModelExportContext(bl_obj)

    @staticmethod
    def is_bone_visible(bl_armature: bpy.types.Armature, bl_bone: bpy.types.Bone) -> bool:
        if BlenderHelper.is_blender_40:
            bl_hidden_bone_collection = bl_armature.collections.get(BlenderNaming.hidden_bone_group_name)
            return bl_hidden_bone_collection is None or bl_hidden_bone_collection.bones.get(bl_bone.name) is None
        else:
            return getattr(bl_bone, "layers")[0]

    @staticmethod
    def set_bone_visible(bl_armature: bpy.types.Armature, bl_bone: bpy.types.Bone, visible: bool) -> None:
        if BlenderHelper.is_blender_40:
            bl_hidden_bone_collection = bl_armature.collections.get(BlenderNaming.hidden_bone_group_name)

            if visible:
                if bl_hidden_bone_collection:
                    bl_hidden_bone_collection.unassign(bl_bone)
            else:
                if bl_hidden_bone_collection is None:
                    bl_hidden_bone_collection = bl_armature.collections.new(BlenderNaming.hidden_bone_group_name)
                    bl_hidden_bone_collection.is_visible = False

                bl_hidden_bone_collection.assign(bl_bone)
        else:
            if visible:
                getattr(bl_bone, "layers")[0] = True
                getattr(bl_bone, "layers")[1] = False
            else:
                getattr(bl_bone, "layers")[1] = True
                getattr(bl_bone, "layers")[0] = False

    @staticmethod
    def temporarily_show_all_bones(bl_armature_obj: bpy.types.Object) -> "BlenderShowAllBonesContext":
        return BlenderShowAllBonesContext(bl_armature_obj)

    @staticmethod
    def get_bone_group(bl_armature_obj: bpy.types.Object, bl_bone: bpy.types.Bone) -> BlenderBoneGroup | None:
        if BlenderHelper.is_blender_40:
            if bl_bone.color is None:
                return None

            bl_armature = cast(bpy.types.Armature, bl_armature_obj.data)
            for bl_bone_collection in bl_armature.collections:
                if bl_bone_collection.bones.get(bl_bone.name):
                    return BlenderBoneGroup(bl_bone_collection.name, str(bl_bone.color.palette))
        elif bl_armature_obj.pose is not None:
            bl_bone_group = getattr(bl_armature_obj.pose.bones[bl_bone.name], "bone_group")
            if bl_bone_group is not None:
                return BlenderBoneGroup(bl_bone_group.name, bl_bone_group.color_set)

        return None

    @staticmethod
    def is_bone_in_group(bl_armature_obj: bpy.types.Object, bl_bone: bpy.types.Bone, group_name: str) -> bool:
        if BlenderHelper.is_blender_40:
            bl_armature = cast(bpy.types.Armature, bl_armature_obj.data)
            bl_bone_collection = bl_armature.collections.get(group_name)
            return bl_bone_collection is not None and bl_bone_collection.bones.get(bl_bone.name) is not None
        elif bl_armature_obj.pose is not None:
            bl_bone_group = getattr(bl_armature_obj.pose.bones[bl_bone.name], "bone_group")
            return bl_bone_group is not None and getattr(bl_bone_group, "name") == group_name
        else:
            return False

    @staticmethod
    def move_bone_to_group(bl_armature_obj: bpy.types.Object, bl_bone: bpy.types.Bone, group_name: str | None, palette: str | None) -> None:
        if BlenderHelper.is_blender_40:
            bl_armature = cast(bpy.types.Armature, bl_armature_obj.data)

            for bl_bone_collection in list(bl_bone.collections):
                if bl_bone_collection.name != BlenderNaming.hidden_bone_group_name:
                    bl_bone_collection.unassign(bl_bone)

            if group_name is not None:
                bl_armature = cast(bpy.types.Armature, bl_armature_obj.data)
                bl_bone_collection = bl_armature.collections.get(group_name) or bl_armature.collections.new(group_name)
                bl_bone_collection.assign(bl_bone)

            if bl_bone.color is not None:
                bl_bone.color.palette = cast(Any, palette or "DEFAULT")
        elif bl_armature_obj.pose is not None:
            bl_bone_group: Any = None
            if group_name is not None:
                bl_bone_groups = getattr(bl_armature_obj.pose, "bone_groups")
                bl_bone_group = bl_bone_groups.get(group_name) or bl_bone_groups.new(name = group_name)
                bl_bone_group.color_set = palette

            setattr(bl_armature_obj.pose.bones[bl_bone.name], "bone_group", bl_bone_group)

    @staticmethod
    def remove_bone_from_group(bl_armature_obj: bpy.types.Object, bl_bone: bpy.types.Bone) -> None:
        if BlenderHelper.is_blender_40:
            bl_bone.collections.clear()
        elif bl_armature_obj.pose is not None:
            setattr(bl_armature_obj.pose.bones[bl_bone.name], "bone_group", None)

    @staticmethod
    def reset_pose(bl_armature_obj: bpy.types.Object) -> None:
        if bl_armature_obj.pose is None:
            return

        for bl_bone in bl_armature_obj.pose.bones:
            bl_bone.matrix_basis.identity()

    @staticmethod
    def get_edge_bevel_weight(bl_mesh: bpy.types.Mesh | bmesh.types.BMesh, edge_idx: int) -> float:
        if BlenderHelper.is_blender_40:
            if isinstance(bl_mesh, bpy.types.Mesh):
                bl_attr = cast(bpy.types.FloatAttribute | None, bl_mesh.attributes.get("bevel_weight_edge"))
                if bl_attr is None:
                    return 0

                return bl_attr.data[edge_idx].value
            else:
                bl_layer = bl_mesh.edges.layers.float.get("bevel_weight_edge")
                if bl_layer is None:
                    return 0

                bl_mesh.edges.ensure_lookup_table()
                return bl_mesh.edges[edge_idx][bl_layer] or 0
        else:
            if isinstance(bl_mesh, bpy.types.Mesh):
                return getattr(bl_mesh.edges[edge_idx], "bevel_weight")
            else:
                bl_layer_collection = cast(bmesh.types.BMLayerCollection[float], getattr(bl_mesh.edges.layers, "bevel_weight"))
                bl_layer = bl_layer_collection.active
                return bl_mesh.edges[edge_idx][bl_layer]

    @staticmethod
    def set_edge_bevel_weight(bl_mesh: bpy.types.Mesh | bmesh.types.BMesh, edge_idx: int, weight: float) -> None:
        if BlenderHelper.is_blender_40:
            if isinstance(bl_mesh, bpy.types.Mesh):
                bl_attr = cast(bpy.types.FloatAttribute, bl_mesh.attributes.get("bevel_weight_edge") or bl_mesh.attributes.new("bevel_weight_edge", "FLOAT", "EDGE"))
                bl_attr.data[edge_idx].value = weight
            else:
                bl_layer = bl_mesh.edges.layers.float.get("bevel_weight_edge")
                if bl_layer is None:
                    raise Exception("bevel_weight_edge attribute not found")

                bl_mesh.edges[edge_idx][bl_layer] = weight
        else:
            if isinstance(bl_mesh, bpy.types.Mesh):
                setattr(bl_mesh.edges[edge_idx], "bevel_weight", weight)
            else:
                bl_layer_collection = cast(bmesh.types.BMLayerCollection[float | None], getattr(bl_mesh.edges.layers, "bevel_weight"))
                bl_mesh.edges[edge_idx][bl_layer_collection.active] = weight

    @staticmethod
    def create_empty_material(name: str) -> bpy.types.Material:
        bl_material = bpy.data.materials.new(name)
        bl_material.use_nodes = True
        bl_material.show_transparent_back = False

        if bl_material.node_tree is not None:
            for node in bl_material.node_tree.nodes:
                bl_material.node_tree.nodes.remove(node)

        return bl_material

    @staticmethod
    def view_all() -> None:
        if bpy.context.screen is None:
            return

        for bl_area in Enumerable(bpy.context.screen.areas).where(lambda a: a.type == "VIEW_3D"):
            bl_region = Enumerable(bl_area.regions).first_or_none(lambda r: r.type == "WINDOW")
            if bl_region is None:
                continue

            with bpy.context.temp_override(area = bl_area, region = bl_region):
                bpy.ops.view3d.view_all()

class BlenderEditContext(SlotsBase):
    def __init__(self) -> None:
        bpy.ops.object.mode_set(mode = "EDIT")

    def __enter__(self) -> None:
        pass

    def __exit__(self, exc_type: type[BaseException] | None, exc_val: BaseException | None, exc_tb: TracebackType | None) -> None:
        bpy.ops.object.mode_set(mode = "OBJECT")

class BlenderModelExportContext(SlotsBase):
    bl_obj: bpy.types.Object
    modifiers_enabled: list[bool]
    shape_key_values: list[float]
    active_shape_key_idx: int | None
    only_active_shape_key: bool
    use_auto_smooth: bool

    def __init__(self, bl_obj: bpy.types.Object) -> None:
        self.bl_obj = bl_obj

        self.modifiers_enabled = Enumerable(bl_obj.modifiers).select(lambda m: m.show_viewport).to_list()
        for bl_modifier in bl_obj.modifiers:
            if not isinstance(bl_modifier, bpy.types.TriangulateModifier):
                bl_modifier.show_viewport = False

        bl_mesh = cast(bpy.types.Mesh, bl_obj.data)
        if bl_mesh.shape_keys is None:
            self.shape_key_values = []
        else:
            self.shape_key_values = Enumerable(bl_mesh.shape_keys.key_blocks).select(lambda s: s.value).to_list()
            for bl_shape_key in bl_mesh.shape_keys.key_blocks:
                bl_shape_key.value = 0

        self.active_shape_key_idx = bl_obj.active_shape_key_index
        self.only_active_shape_key = bl_obj.show_only_shape_key
        bl_obj.show_only_shape_key = False

        if hasattr(bl_mesh, "use_auto_smooth"):
            self.use_auto_smooth = getattr(bl_mesh, "use_auto_smooth")
            if bl_mesh.has_custom_normals:
                setattr(bl_mesh, "use_auto_smooth", True)

        bpy.ops.object.select_all(action = "DESELECT")
        bl_obj.select_set(True)
        if bpy.context.view_layer is not None:
            bpy.context.view_layer.objects.active = bl_obj

    def __enter__(self) -> None:
        pass

    def __exit__(self, exc_type: type[BaseException] | None, exc_val: BaseException | None, exc_tb: TracebackType | None) -> None:
        for i, enabled in enumerate(self.modifiers_enabled):
            self.bl_obj.modifiers[i].show_viewport = enabled

        bl_mesh = cast(bpy.types.Mesh, self.bl_obj.data)
        if bl_mesh.shape_keys is not None:
            for i, value in enumerate(self.shape_key_values):
                bl_mesh.shape_keys.key_blocks[i].value = value

        self.bl_obj.active_shape_key_index = self.active_shape_key_idx
        self.bl_obj.show_only_shape_key = self.only_active_shape_key
        if hasattr(bl_mesh, "use_auto_smooth"):
            setattr(bl_mesh, "use_auto_smooth", self.use_auto_smooth)

class BlenderShowAllBonesContext(SlotsBase):
    bl_armature_obj: bpy.types.Object
    hidden_bone_set_indices: list[int]

    def __init__(self, bl_armature_obj: bpy.types.Object) -> None:
        self.bl_armature_obj = bl_armature_obj
        self.hidden_bone_set_indices = []

        if cast(tuple[int, ...], bpy.app.version) >= (4, 0, 0):
            bl_bone_collections = cast(bpy.types.Armature, bl_armature_obj.data).collections
            for i, bl_bone_collection in enumerate(cast(Iterable[bpy.types.BoneCollection], bl_bone_collections)):
                if not bl_bone_collection.is_visible:
                    self.hidden_bone_set_indices.append(i)

                bl_bone_collection.is_visible = True
        else:
            bl_layers = cast(list[bool], getattr(bl_armature_obj.data, "layers"))
            for i, layer_visible in enumerate(bl_layers):
                if not layer_visible:
                    self.hidden_bone_set_indices.append(i)

                bl_layers[i] = True

    def __enter__(self) -> None:
        pass

    def __exit__(self, exc_type: type[BaseException] | None, exc_val: BaseException | None, exc_tb: TracebackType | None) -> None:
        if cast(tuple[int, ...], bpy.app.version) >= (4, 0, 0):
            bl_armature = cast(bpy.types.Armature, self.bl_armature_obj.data)
            bl_bone_collections = cast(Sequence[bpy.types.BoneCollection], bl_armature.collections)
            for i in self.hidden_bone_set_indices:
                bl_bone_collections[i].is_visible = False
        else:
            bl_layers = cast(list[bool], getattr(self.bl_armature_obj.data, "layers"))
            for i in self.hidden_bone_set_indices:
                bl_layers[i] = False
