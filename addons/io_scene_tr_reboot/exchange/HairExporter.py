from array import array
from typing import cast
import bpy
import os
from mathutils import Vector
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.tr.Enumerations import CdcGame, ResourceType
from io_scene_tr_reboot.tr.Factories import Factories
from io_scene_tr_reboot.tr.Hair import Hair, HairPart, HairPoint, HairPointWeight, HairStrandGroup
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey
from io_scene_tr_reboot.util.Enumerable import Enumerable
from io_scene_tr_reboot.util.IoHelper import IoHelper

class HairExporter:
    scale_factor: float
    game: CdcGame

    def __init__(self, scale_factor: float, game: CdcGame) -> None:
        self.scale_factor = scale_factor
        self.game = game

    def export_hair(self, folder_path: str, bl_strand_group_objs: list[bpy.types.Object]) -> None:
        tr_hair = self.create_hair(bl_strand_group_objs)
        if tr_hair is None:
            return

        resource_type: ResourceType
        extension: str
        if tr_hair.model_id is not None:
            resource_type = ResourceType.MODEL
            extension = f".tr{self.game}modeldata"
        else:
            resource_type = ResourceType.DTP
            extension = f".tr{self.game}dtp"

        file_path = os.path.join(folder_path, str(tr_hair.hair_data_id) + extension)
        with IoHelper.open_write(file_path) as file:
            builder = ResourceBuilder(ResourceKey(resource_type, tr_hair.hair_data_id), self.game)
            tr_hair.write(builder)
            file.write(builder.build())

    def create_hair(self, bl_strand_group_objs: list[bpy.types.Object]) -> Hair | None:
        if len(bl_strand_group_objs) == 0:
            return None

        id_set = BlenderNaming.parse_hair_strand_group_name(bl_strand_group_objs[0].name)
        tr_hair = Factories.get(self.game).create_hair(id_set.model_id, id_set.hair_data_id)

        bl_first_strand_group_data = cast(bpy.types.Curves, bl_strand_group_objs[0].data)
        if len(bl_first_strand_group_data.materials) > 0:
            bl_material = bl_first_strand_group_data.materials[0]
            if bl_material is not None:
                tr_hair.material_id = BlenderNaming.try_parse_hair_strand_group_material_name(bl_material.name)

        for bl_obj in Enumerable(bl_strand_group_objs).order_by(lambda o: o.name):
            bl_obj_eval = bl_obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
            bl_data_eval = bl_obj_eval.data
            if not isinstance(bl_data_eval, bpy.types.Curves):
                raise Exception(f"{bl_obj.name} is not a hair curve object.")

            bl_modifier: bpy.types.NodesModifier | None = None
            if Enumerable(bl_data_eval.curves).any(lambda c: c.points_length != 16):
                bl_modifier = self.add_curve_resample_modifier(bl_obj)
                bl_obj_eval = bl_obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
                bl_data_eval = cast(bpy.types.Curves, bl_obj_eval.data)

            id_set = BlenderNaming.parse_hair_strand_group_name(bl_obj.name)
            tr_hair_part = Enumerable(tr_hair.parts).first_or_none(lambda p: p.name == id_set.part_name)
            if tr_hair_part is None:
                tr_hair_part = HairPart(id_set.part_name)
                tr_hair.parts.append(tr_hair_part)

            tr_strand_group = self.create_strand_group(bl_data_eval)
            if id_set.is_master:
                tr_hair_part.master_strands = tr_strand_group
            else:
                tr_hair_part.slave_strands = tr_strand_group

            if bl_modifier is not None:
                bl_node_group = bl_modifier.node_group
                bl_obj.modifiers.remove(bl_modifier)
                if bl_node_group is not None:
                    bpy.data.node_groups.remove(bl_node_group)

        return tr_hair

    def create_strand_group(self, bl_strand_group_data: bpy.types.Curves) -> HairStrandGroup:
        tr_points = self.create_strand_group_points(bl_strand_group_data)
        tr_strand_point_counts = self.get_strand_point_counts(bl_strand_group_data)
        return HairStrandGroup(tr_points, tr_strand_point_counts)

    def create_strand_group_points(self, bl_strand_group_data: bpy.types.Curves) -> list[HairPoint]:
        coords = array("f", [0]) * (len(bl_strand_group_data.points) * 3)
        bl_strand_group_data.position_data.foreach_get("vector", coords)

        thicknesses = array("f", [0]) * len(bl_strand_group_data.points)
        bl_thickness_attr = bl_strand_group_data.attributes.get("radius")
        if isinstance(bl_thickness_attr, bpy.types.FloatAttribute):
            bl_thickness_attr.data.foreach_get("value", thicknesses)

        global_bone_weights: dict[int, array[float]] = {}
        for bl_bone_attr in bl_strand_group_data.attributes:
            if not isinstance(bl_bone_attr, bpy.types.FloatAttribute):
                continue

            bone_id_set = BlenderNaming.try_parse_bone_name(bl_bone_attr.name)
            if bone_id_set is None or bone_id_set.global_id is None:
                continue

            bone_weights = array("f", [0]) * len(bl_strand_group_data.points)
            bl_bone_attr.data.foreach_get("value", bone_weights)
            global_bone_weights[bone_id_set.global_id] = bone_weights

        tr_points = cast(list[HairPoint], [None] * len(bl_strand_group_data.points))
        coord_idx = 0
        for point_idx in range(len(bl_strand_group_data.points)):
            position = Vector((
                coords[coord_idx + 0] / self.scale_factor,
                coords[coord_idx + 1] / self.scale_factor,
                coords[coord_idx + 2] / self.scale_factor
            ))
            coord_idx += 3

            thickness = thicknesses[point_idx] / self.scale_factor

            tr_point_weights: list[HairPointWeight] = []
            for global_bone_id, bone_weights in global_bone_weights.items():
                if bone_weights[point_idx] > 0:
                    tr_point_weights.append(HairPointWeight(global_bone_id, bone_weights[point_idx]))

            tr_points[point_idx] = HairPoint(position, thickness, tr_point_weights)

        return tr_points

    def get_strand_point_counts(self, bl_strand_group_data: bpy.types.Curves) -> list[int]:
        point_counts = [0] * len(bl_strand_group_data.curve_offset_data)
        bl_strand_group_data.curve_offset_data.foreach_get("value", point_counts)
        for i in range(len(point_counts) - 1):
            point_counts[i] = point_counts[i + 1] - point_counts[i]

        point_counts.pop()
        return point_counts

    def add_curve_resample_modifier(self, bl_obj: bpy.types.Object) -> bpy.types.NodesModifier:
        bl_node_group = cast(bpy.types.GeometryNodeTree, bpy.data.node_groups.new("__resample", "GeometryNodeTree"))
        bl_node_group.is_modifier = True

        if bl_node_group.interface is None:
            raise Exception()

        bl_node_group.interface.new_socket("Geometry", in_out = "INPUT",  socket_type = "NodeSocketGeometry")
        bl_node_group.interface.new_socket("Geometry", in_out = "OUTPUT", socket_type = "NodeSocketGeometry")

        bl_input_node    = bl_node_group.nodes.new("NodeGroupInput")
        bl_resample_node = bl_node_group.nodes.new("GeometryNodeResampleCurve")
        bl_output_node   = bl_node_group.nodes.new("NodeGroupOutput")

        bl_node_group.links.new(bl_input_node.outputs[0], bl_resample_node.inputs[0])
        cast(bpy.types.NodeSocketInt, bl_resample_node.inputs[2]).default_value = 16
        bl_node_group.links.new(bl_resample_node.outputs[0], bl_output_node.inputs[0])

        bl_modifier = cast(bpy.types.NodesModifier, bl_obj.modifiers.new("Copy attribute", "NODES"))
        bl_modifier.node_group = bl_node_group
        return bl_modifier
