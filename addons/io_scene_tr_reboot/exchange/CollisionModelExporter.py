from typing import NamedTuple, cast
import bpy
import os
from mathutils import Vector
from io_scene_tr_reboot.BlenderNaming import BlenderCollisionModelIdSet, BlenderNaming
from io_scene_tr_reboot.properties.ObjectProperties import ObjectProperties
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.tr.CollisionModel import CollisionBoundingBoxNode, CollisionFace, CollisionMesh
from io_scene_tr_reboot.tr.Enumerations import CdcGame, ResourceType
from io_scene_tr_reboot.tr.Factories import Factories
from io_scene_tr_reboot.tr.IFactory import IFactory
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey
from io_scene_tr_reboot.util.Enumerable import Enumerable

class _Face(NamedTuple):
    min: Vector
    max: Vector
    bl_face: bpy.types.MeshPolygon

class _BoundingBoxWithFaces(NamedTuple):
    min: Vector
    max: Vector
    faces: list[_Face]

class CollisionModelExporter:
    scale_factor: float
    game: CdcGame
    factory: IFactory

    def __init__(self, scale_factor: float, game: CdcGame) -> None:
        self.scale_factor = scale_factor
        self.game = game
        self.factory = Factories.get(game)

    def export_model(self, folder_path: str, ids: BlenderCollisionModelIdSet, bl_mesh_objs: list[bpy.types.Object]) -> None:
        tr_model = self.factory.create_collision_model()
        for bl_mesh_obj in bl_mesh_objs:
            tr_mesh = self.create_mesh(bl_mesh_obj)
            tr_model.meshes.append(tr_mesh)

        resource_key = ResourceKey(ResourceType.DTP, ids.model_id)
        writer = ResourceBuilder(resource_key, self.game)
        tr_model.write(writer)

        with open(os.path.join(folder_path, Collection.make_resource_file_name(resource_key, self.game)), "wb") as file:
            file.write(writer.build())

    def create_mesh(self, bl_mesh_obj: bpy.types.Object) -> CollisionMesh:
        bl_mesh = cast(bpy.types.Mesh, bl_mesh_obj.data)
        tr_mesh = CollisionMesh()

        for bl_vertex in bl_mesh.vertices:
            tr_mesh.vertices.append(bl_vertex.co / self.scale_factor)

        (tr_root_bb_node, tr_faces) = self.create_bb_nodes_and_faces(bl_mesh_obj)
        tr_mesh.root_bounding_box_node = tr_root_bb_node
        tr_mesh.faces = tr_faces

        if len(bl_mesh.materials) == 0:
            raise Exception(f"Collision mesh {bl_mesh_obj.name} doesn't have any materials assigned.")

        for bl_material in bl_mesh.materials:
            if bl_material is None:
                raise Exception(f"Collision mesh {bl_mesh_obj.name} has an empty material slot.")

            tr_mesh.material_ids.append(BlenderNaming.parse_collision_material_name(bl_material.name))

        collision_type_id = ObjectProperties.get_instance(bl_mesh_obj).collision_model.collision_type_id
        if collision_type_id > 0:
            tr_mesh.collision_type_id = collision_type_id

        return tr_mesh

    def create_bb_nodes_and_faces(self, bl_mesh_obj: bpy.types.Object) -> tuple[CollisionBoundingBoxNode, list[CollisionFace]]:
        bl_mesh = cast(bpy.types.Mesh, bl_mesh_obj.data)

        faces: list[_Face] = []
        for bl_face in bl_mesh.polygons:
            faces.append(self.get_bounding_box_from_face(bl_mesh, bl_face))

        root_bounding_box = self.get_bounding_box_from_faces(faces)
        ordered_faces: list[_Face] = []
        tr_root_node = self.create_bounding_box_node_recursive(root_bounding_box, ordered_faces)

        tr_faces: list[CollisionFace] = []
        for bl_face in Enumerable(ordered_faces).select(lambda f: f.bl_face):
            if len(bl_face.vertices) != 3:
                raise Exception(f"Collision mesh {bl_mesh_obj.name} contains non-triangular faces")

            tr_face = CollisionFace(list(bl_face.vertices), bl_face.material_index)
            tr_faces.append(tr_face)

        return (tr_root_node, tr_faces)

    def create_bounding_box_node_recursive(self, bounding_box: _BoundingBoxWithFaces, ordered_faces: list[_Face]) -> CollisionBoundingBoxNode:
        min_pos = bounding_box.min / self.scale_factor
        max_pos = bounding_box.max / self.scale_factor
        left_child:  _BoundingBoxWithFaces | None = None
        right_child: _BoundingBoxWithFaces | None = None
        if len(bounding_box.faces) > 1:
            (left_child, right_child) = self.try_split_bounding_box(bounding_box)

        if left_child is not None and right_child is not None:
            tr_node = CollisionBoundingBoxNode(min_pos, max_pos, [], 0, 0)
            tr_node.children.append(self.create_bounding_box_node_recursive(left_child,  ordered_faces))
            tr_node.children.append(self.create_bounding_box_node_recursive(right_child, ordered_faces))
        else:
            tr_node = CollisionBoundingBoxNode(min_pos, max_pos, [], len(ordered_faces), len(bounding_box.faces))
            ordered_faces.extend(bounding_box.faces)

        return tr_node

    def try_split_bounding_box(self, bounding_box: _BoundingBoxWithFaces) -> tuple[_BoundingBoxWithFaces | None, _BoundingBoxWithFaces | None]:
        split_axis, split_coord = self.try_get_bounding_box_split_coord(bounding_box)
        if split_axis is None or split_coord is None:
            return (None, None)

        left_faces:  list[_Face] = []
        right_faces: list[_Face] = []

        for face in bounding_box.faces:
            left_depth  = split_coord - face.min[split_axis]
            right_depth = face.max[split_axis] - split_coord
            if left_depth > right_depth:
                left_faces.append(face)
            else:
                right_faces.append(face)

        if len(left_faces) == 0 or len(right_faces) == 0:
            return (None, None)

        left_bb  = self.get_bounding_box_from_faces(left_faces)
        right_bb = self.get_bounding_box_from_faces(right_faces)
        return (left_bb, right_bb)

    def try_get_bounding_box_split_coord(self, bounding_box: _BoundingBoxWithFaces) -> tuple[int | None, float | None]:
        coord_sets: tuple[set[int], ...] = (set(), set(), set())
        for face in bounding_box.faces:
            for axis in range(3):
                coord_sets[axis].add(int(face.min[axis]))
                coord_sets[axis].add(int(face.max[axis]))

        split_axis = Enumerable(range(3)).order_by_descending(lambda axis: len(coord_sets[axis])).first()
        if len(coord_sets[split_axis]) == 0:
            return (None, None)

        sorted_coords = sorted(coord_sets[split_axis])
        split_coord = float(sorted_coords[len(sorted_coords) // 2])
        return (split_axis, split_coord)

    def get_bounding_box_from_face(self, bl_mesh: bpy.types.Mesh, bl_face: bpy.types.MeshPolygon) -> _Face:
        min_pos = Vector(( 9999,  9999,  9999))
        max_pos = Vector((-9999, -9999, -9999))
        for vertex_idx in bl_face.vertices:
            bl_vertex = bl_mesh.vertices[vertex_idx]

            min_pos.x = min(min_pos.x, bl_vertex.co.x)
            min_pos.y = min(min_pos.y, bl_vertex.co.y)
            min_pos.z = min(min_pos.z, bl_vertex.co.z)

            max_pos.x = max(max_pos.x, bl_vertex.co.x)
            max_pos.y = max(max_pos.y, bl_vertex.co.y)
            max_pos.z = max(max_pos.z, bl_vertex.co.z)

        return _Face(min_pos, max_pos, bl_face)

    def get_bounding_box_from_faces(self, faces: list[_Face]) -> _BoundingBoxWithFaces:
        min_pos = Vector(( 9999,  9999,  9999))
        max_pos = Vector((-9999, -9999, -9999))

        for face in faces:
            min_pos.x = min(min_pos.x, face.min.x)
            min_pos.y = min(min_pos.y, face.min.y)
            min_pos.z = min(min_pos.z, face.min.z)

            max_pos.x = max(max_pos.x, face.max.x)
            max_pos.y = max(max_pos.y, face.max.y)
            max_pos.z = max(max_pos.z, face.max.z)

        return _BoundingBoxWithFaces(min_pos, max_pos, faces)
