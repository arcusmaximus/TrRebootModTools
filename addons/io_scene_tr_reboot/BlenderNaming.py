import re
from typing import ClassVar, Iterable, NamedTuple, overload
from io_scene_tr_reboot.tr.CollisionShape import CollisionShapeKey, CollisionShapeType
from io_scene_tr_reboot.util.Conditional import coalesce
from io_scene_tr_reboot.util.Enumerable import Enumerable

class BlenderModelIdSet(NamedTuple):
    object_id: int
    model_id: int
    model_data_id: int

class BlenderMeshIdSet(NamedTuple):
    object_id: int
    model_id: int
    model_data_id: int
    mesh_idx: int

class BlenderCollisionModelIdSet(NamedTuple):
    object_id: int
    model_id: int

class BlenderCollisionMeshIdSet(NamedTuple):
    object_id: int
    model_id: int
    mesh_idx: int

class BlenderBoneIdSet(NamedTuple):
    skeleton_id: int | None
    global_id: int | None
    local_id: int | None

class BlenderBlendShapeIdSet(NamedTuple):
    global_id: int | None
    local_id: int

class BlenderClothStripIdSet(NamedTuple):
    skeleton_id: int
    definition_id: int
    component_id: int
    strip_id: int

class BlenderHairStrandGroupIdSet(NamedTuple):
    model_id: int | None
    hair_data_id: int
    part_name: str
    is_master: bool

class BlenderNaming:
    local_collection_name: ClassVar[str] = "Split meshes for export"

    non_deforming_bone_group_name: ClassVar[str] = "Non-deforming bones"

    pinned_cloth_bone_group_name: ClassVar[str] = "Pinned cloth bones"
    pinned_cloth_bone_palette_name: ClassVar[str] = "THEME01"

    unpinned_cloth_bone_group_name: ClassVar[str] = "Unpinned cloth bones"
    unpinned_cloth_bone_palette_name: ClassVar[str] = "THEME08"

    constrained_bone_group_name: ClassVar[str] = "Twist bones"
    constrained_bone_palette_name: ClassVar[str] = "THEME03"

    helper_bone_group_name: ClassVar[str] = "Helper bones"
    helper_bone_palette_name: ClassVar[str] = "THEME10"

    @staticmethod
    def make_collection_empty_name(collection_name: str, object_id: int) -> str:
        return BlenderNaming.make_collection_item_name(collection_name, f"object_{object_id}")

    @staticmethod
    def try_parse_collection_empty_name(name: str) -> int | None:
        match = re.search(r"_object_(\d+)(?:\.\d+)?$", name)
        if match is None:
            return None

        return int(match.group(1))

    @staticmethod
    def make_mesh_name(collection_name: str, object_id: int, model_id: int, model_data_id: int, mesh_idx: int) -> str:
        return BlenderNaming.make_collection_item_name(collection_name, f"model_{object_id}_{model_id}_{model_data_id}_{mesh_idx}")

    @staticmethod
    def make_local_mesh_name(object_id: int, model_id: int, model_data_id: int, mesh_idx: int) -> str:
        return BlenderNaming.make_mesh_name("split", object_id, model_id, model_data_id, mesh_idx)

    @staticmethod
    def try_parse_mesh_name(name: str) -> BlenderMeshIdSet | None:
        match = re.search(r"_model_(?:(\d+)_)?(\d+)_(\d+)_(\d+)(?:\.\d+)?$", name)
        if match is None:
            return None

        object_id = match.group(1)
        return BlenderMeshIdSet(int(object_id) if object_id is not None else 0, int(match.group(2)), int(match.group(3)), int(match.group(4)))

    @staticmethod
    def parse_mesh_name(name: str) -> BlenderMeshIdSet:
        mesh_id_set = BlenderNaming.try_parse_mesh_name(name)
        if mesh_id_set is None:
            raise Exception(f"{name} is not a valid mesh name.")

        return mesh_id_set

    @staticmethod
    def try_parse_model_name(name: str) -> BlenderModelIdSet | None:
        mesh_id_set = BlenderNaming.try_parse_mesh_name(name)
        if mesh_id_set is None:
            return None

        return BlenderModelIdSet(mesh_id_set.object_id, mesh_id_set.model_id, mesh_id_set.model_data_id)

    @staticmethod
    def parse_model_name(name: str) -> BlenderModelIdSet:
        model_id_set = BlenderNaming.try_parse_model_name(name)
        if model_id_set is None:
            raise Exception(f"{name} is not a valid mesh name.")

        return model_id_set

    @staticmethod
    def make_local_empty_name(object_id: int, model_id: int, model_data_id: int) -> str:
        return f"split_{object_id}_{model_id}_{model_data_id}"

    @staticmethod
    def is_local_empty_name(name: str) -> bool:
        return name.startswith("split_")

    @staticmethod
    def parse_local_empty_name(name: str) -> BlenderModelIdSet:
        match = re.fullmatch(r"split_(?:(\d+)_)?(\d+)_(\d+)(?:\.\d+)?", name)
        if match is None:
            raise Exception(f"{name} is not a valid local empty name")

        object_id = match.group(1)
        return BlenderModelIdSet(int(object_id) if len(object_id) > 0 else 0, int(match.group(2)), int(match.group(3)))

    @staticmethod
    def make_color_map_name(idx: int) -> str:
        return f"color{1 + idx}"

    @staticmethod
    def parse_color_map_name(name: str) -> int:
        match = re.fullmatch(r"color(\d+)", name)
        if match is None:
            raise Exception(f"{name} is not a valid color attribute name.")

        return int(match.group(1)) - 1

    @staticmethod
    def make_uv_map_name(idx: int) -> str:
        return f"texcoord{1 + idx}"

    @staticmethod
    def parse_uv_map_name(name: str) -> int:
        match = re.fullmatch(r"texcoord(\d+)", name)
        if match is None:
            raise Exception(f"{name} is not a valid UV map name.")

        return int(match.group(1)) - 1

    @staticmethod
    def make_global_armature_name(local_skeleton_ids: Iterable[int]) -> str:
        return "merged_skeleton_" + "_".join(
            Enumerable(local_skeleton_ids).order_by(lambda id: id)      \
                                          .select(lambda id: str(id))
        )

    @staticmethod
    def try_parse_global_armature_name(name: str) -> list[int] | None:
        match = re.fullmatch(r"merged_skeleton_([_\d]+)(?:\.\d+)?", name)
        if match is None:
            return None

        return Enumerable(match.group(1).split("_")).select(lambda id: int(id)).to_list()

    @staticmethod
    def parse_global_armature_name(name: str) -> list[int]:
        skeleton_ids = BlenderNaming.try_parse_global_armature_name(name)
        if skeleton_ids is None:
            raise Exception(f"{name} is not a valid merged armature name.")

        return skeleton_ids

    @staticmethod
    def make_local_armature_name(collection_name: str, id: int) -> str:
        return BlenderNaming.make_collection_item_name(collection_name, f"skeleton_{id}")

    @staticmethod
    def try_parse_local_armature_name(name: str) -> int | None:
        match = re.search(r"_skeleton_(\d+)(?:\.\d+)?$", name)
        if match is None:
            return None

        return int(match.group(1))

    @staticmethod
    def parse_local_armature_name(name: str) -> int:
        local_skeleton_id = BlenderNaming.try_parse_local_armature_name(name)
        if local_skeleton_id is None:
            raise Exception(f"{name} is not a valid armature name.")

        return local_skeleton_id

    @overload
    @staticmethod
    def make_bone_name(skeleton_id: int | None, global_id: int | None, local_id: int | None, /) -> str: ...

    @overload
    @staticmethod
    def make_bone_name(id_set: BlenderBoneIdSet, /) -> str: ...

    @staticmethod
    def make_bone_name(skeleton_id_or_id_set: int | BlenderBoneIdSet | None, global_id: int | None = None, local_id: int | None = None, /) -> str:
        skeleton_id: int | None
        if isinstance(skeleton_id_or_id_set, BlenderBoneIdSet):
            id_set = skeleton_id_or_id_set
            skeleton_id = id_set.skeleton_id
            global_id = id_set.global_id
            local_id = id_set.local_id
        else:
            skeleton_id = skeleton_id_or_id_set

        if skeleton_id is not None:
            if local_id is None:
                raise Exception("Must provide local bone ID when providing skeleton ID")

            return f"bone_{skeleton_id}_{coalesce(global_id, 'x')}_{local_id}"
        elif local_id is not None:
            return f"bone_{coalesce(global_id, 'x')}_{local_id}"
        elif global_id is not None:
            return f"bone_{global_id}"
        else:
            raise Exception("Must provide at least one ID for bone name")

    @staticmethod
    def try_parse_bone_name(name: str) -> BlenderBoneIdSet | None:
        match = re.fullmatch(r"bone_(x|\d+)(?:_(x|\d+))?(?:_(\d+))?", name)
        if match is None:
            return None

        if match.group(3) is not None:
            return BlenderBoneIdSet(
                int(match.group(1)),
                int(match.group(2)) if match.group(2) != "x" else None,
                int(match.group(3))
            )
        elif match.group(2) is not None:
            return BlenderBoneIdSet(
                None,
                int(match.group(1)) if match.group(1) != "x" else None,
                int(match.group(2))
            )
        else:
            return BlenderBoneIdSet(
                None,
                int(match.group(1)),
                None
            )

    @staticmethod
    def parse_bone_name(name: str) -> BlenderBoneIdSet:
        bone_id_set = BlenderNaming.try_parse_bone_name(name)
        if bone_id_set is None:
            raise Exception(f"{name} is not a valid bone name.")

        return bone_id_set

    @staticmethod
    def try_get_bone_global_id(name: str) -> int | None:
        id_set = BlenderNaming.try_parse_bone_name(name)
        if id_set is None:
            return None

        return id_set.global_id

    @staticmethod
    def get_bone_global_id(name: str) -> int:
        global_id = BlenderNaming.try_get_bone_global_id(name)
        if global_id is None:
            raise Exception(f"{name} is not a valid bone name.")

        return global_id

    @staticmethod
    def try_get_bone_local_id(name: str) -> int | None:
        id_set = BlenderNaming.try_parse_bone_name(name)
        if id_set is None:
            return None

        return id_set.local_id

    @staticmethod
    def get_bone_local_id(name: str) -> int:
        local_id = BlenderNaming.try_get_bone_local_id(name)
        if local_id is None:
            raise Exception(f"{name} is not a valid bone name.")

        return local_id

    @staticmethod
    def make_helper_bone_name(global_id: int) -> str:
        return f"helper_{global_id}"

    @staticmethod
    def try_parse_helper_bone_name(name: str) -> int | None:
        match = re.fullmatch(r"helper_(\d+)", name)
        if match is None:
            return None

        return int(match.group(1))

    @staticmethod
    def make_shape_key_name(name: str | None, global_id: int | None, local_id: int) -> str:
        name = f"{name or 'shapekey'}_{coalesce(global_id, 'x')}_{local_id}"
        if len(name) > 63:
            name = name[-63:]

        return name

    @staticmethod
    def parse_shape_key_name(name: str) -> BlenderBlendShapeIdSet:
        match = re.search(r"_(x|\d+)_(\d+)$", name)
        if match is not None:
            return BlenderBlendShapeIdSet(
                int(match.group(1)) if match.group(1) != "x" else None,
                int(match.group(2))
            )

        match = re.fullmatch(r"shapekey_(\d+)", name)
        if match is not None:
            return BlenderBlendShapeIdSet(None, int(match.group(1)))

        raise Exception(f"{name} is not a valid shape key name.")

    @staticmethod
    def make_material_name(name: str | None, id: int) -> str:
        return f"{name or 'material'}_{id}"

    @staticmethod
    def parse_material_name(name: str) -> int:
        material_id = BlenderNaming.try_parse_material_name(name)
        if material_id is None:
            raise Exception(f"{name} is not a valid material name.")

        return material_id

    @staticmethod
    def try_parse_material_name(name: str) -> int | None:
        match = re.search(r"_(\d+)(?:\.\d+)?$", name)
        if match is None:
            return None

        return int(match.group(1))

    @staticmethod
    def make_grease_pencil_material_name(base_name: str) -> str:
        return f"gp_{base_name}"

    @staticmethod
    def try_parse_grease_pencil_material_name(name: str) -> str | None:
        match = re.fullmatch(r"gp_(.+)", name)
        if match is None:
            return None

        return match.group(1)

    @staticmethod
    def make_action_name(id: int, model_data_id: int | None, mesh_idx: int | None) -> str:
        name = f"animation_{id}"
        if model_data_id is not None and mesh_idx is not None:
            name += f"_{model_data_id}_{mesh_idx}"

        return name

    @staticmethod
    def try_parse_action_name(name: str) -> int | None:
        match = re.fullmatch(r"animation_(\d+)(?:_\d+)?(?:\.\d+)?", name)
        if match is None:
            return None

        return int(match.group(1))

    @staticmethod
    def make_cloth_empty_name(collection_name: str) -> str:
        return BlenderNaming.make_collection_item_name(collection_name, f"cloth")

    @staticmethod
    def is_cloth_empty_name(name: str) -> bool:
        return re.search(r"_cloth(?:\.\d+)?$", name) is not None

    @staticmethod
    def make_cloth_strip_name(collection_name: str, skeleton_id: int, definition_id: int, component_id: int, strip_id: int) -> str:
        return BlenderNaming.make_collection_item_name(collection_name, f"clothstrip_{skeleton_id}_{definition_id}_{component_id}_{strip_id}")

    @staticmethod
    def try_parse_cloth_strip_name(name: str) -> BlenderClothStripIdSet | None:
        match = re.search(r"_clothstrip_(\d+)_(\d+)_(\d+)_(\d+)(?:\.\d+)?$", name)
        if match is None:
            return None

        return BlenderClothStripIdSet(int(match.group(1)), int(match.group(2)), int(match.group(3)), int(match.group(4)))

    @staticmethod
    def parse_cloth_strip_name(name: str) -> BlenderClothStripIdSet:
        id_set = BlenderNaming.try_parse_cloth_strip_name(name)
        if id_set is None:
            raise Exception(f"{name} is not a valid cloth strip name")

        return id_set

    @staticmethod
    def make_collision_empty_name(collection_name: str) -> str:
        return BlenderNaming.make_collection_item_name(collection_name, "collisions")

    @staticmethod
    def is_collision_empty_name(name: str) -> bool:
        return name.endswith("_collisions")

    @staticmethod
    def make_collision_shape_name(collection_name: str, collision_type: CollisionShapeType, skeleton_id: int, collision_hash: int) -> str:
        return BlenderNaming.make_collection_item_name(collection_name, f"collision_{collision_type.name.lower()}_{skeleton_id}_{collision_hash:016X}")

    @staticmethod
    def try_parse_collision_shape_name(name: str) -> CollisionShapeKey | None:
        match = re.search(r"_collision_([a-z]+)(?:_(\d+))?_([0-9A-F]+)(?:\.\d+)?$", name)
        if match is None:
            return None

        type = CollisionShapeType[match.group(1).upper()]
        skeleton_id = int(match.group(2)) if match.group(2) else None
        hash = int(match.group(3), base = 16)
        return CollisionShapeKey(type, skeleton_id, hash)

    @staticmethod
    def parse_collision_shape_name(name: str) -> CollisionShapeKey:
        key = BlenderNaming.try_parse_collision_shape_name(name)
        if key is None:
            raise Exception(f"{name} is not a valid collision shape name")

        return key

    @staticmethod
    def make_collision_mesh_name(collection_name: str, object_id: int, model_id: int, mesh_idx: int) -> str:
        return BlenderNaming.make_collection_item_name(collection_name, f"cmodel_{object_id}_{model_id}_{mesh_idx}")

    @staticmethod
    def try_parse_collision_mesh_name(name: str) -> BlenderCollisionMeshIdSet | None:
        match = re.search(r"_cmodel_(\d+)_(\d+)_(\d+)(?:\.\d+)?$", name)
        if match is None:
            return None

        return BlenderCollisionMeshIdSet(int(match.group(1)), int(match.group(2)), int(match.group(3)))

    @staticmethod
    def try_parse_collision_model_name(name: str) -> BlenderCollisionModelIdSet | None:
        mesh_id_set = BlenderNaming.try_parse_collision_mesh_name(name)
        if mesh_id_set is None:
            return None

        return BlenderCollisionModelIdSet(mesh_id_set.object_id, mesh_id_set.model_id)

    @staticmethod
    def parse_collision_model_name(name: str) -> BlenderCollisionModelIdSet:
        model_id_set = BlenderNaming.try_parse_collision_model_name(name)
        if model_id_set is None:
            raise Exception(f"{name} is not a valid collision mesh name.")

        return model_id_set

    @staticmethod
    def make_collision_material_name(id: int) -> str:
        return f"collision_material_{id}"

    @staticmethod
    def try_parse_collision_material_name(name: str) -> int | None:
        match = re.fullmatch(r"collision_material_(\d+)(?:\.\d+)?", name)
        if match is None:
            return None

        return int(match.group(1))

    @staticmethod
    def parse_collision_material_name(name: str) -> int:
        id = BlenderNaming.try_parse_collision_material_name(name)
        if id is None:
            raise Exception(f"{name} is not a valid collision material name.")

        return id

    @staticmethod
    def make_hair_strand_group_name(collection_name: str, model_id: int | None, hair_data_id: int, part_name: str, is_master: bool) -> str:
        ids = str(hair_data_id)
        if model_id is not None:
            ids = f"{model_id}_{ids}"

        return BlenderNaming.make_collection_item_name(collection_name, f"hair_{ids}_{part_name}_{'guide' if is_master else 'render'}")

    @staticmethod
    def try_parse_hair_strand_group_name(name: str) -> BlenderHairStrandGroupIdSet | None:
        match = re.search(r"_hair(?:_(\d+))?_(\d+)_([a-zA-Z0-9]+)_(guide|render)(?:\.\d+)?$", name)
        if match is None:
            return None

        return BlenderHairStrandGroupIdSet(int(match.group(1)) if match.group(1) else None, int(match.group(2)), match.group(3), match.group(4) == "guide")

    @staticmethod
    def parse_hair_strand_group_name(name: str) -> BlenderHairStrandGroupIdSet:
        ids = BlenderNaming.try_parse_hair_strand_group_name(name)
        if ids is None:
            raise Exception(f"{name} is not a valid hair name")

        return ids

    @staticmethod
    def make_hair_strand_group_material_name(material_id: int | None, is_master: bool) -> str:
        if material_id is not None:
            return f"hair_material_{material_id}"

        return f"hair_material_{'guide' if is_master else 'follow'}"

    @staticmethod
    def try_parse_hair_strand_group_material_name(name: str) -> int | None:
        match = re.fullmatch(r"hair_material_(\d+)", name)
        if match is None:
            return None

        return int(match.group(1))

    @staticmethod
    def make_collection_item_name(collection_name: str, suffix: str) -> str:
        result = f"{collection_name}_{suffix}"
        if len(result) > 59:
            result = result[-59:]

        return result
