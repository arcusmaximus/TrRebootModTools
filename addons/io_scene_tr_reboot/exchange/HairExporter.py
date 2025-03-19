from array import array
from typing import cast
import bpy
import os
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.tr.Enumerations import CdcGame, ResourceType
from io_scene_tr_reboot.tr.Factories import Factories
from io_scene_tr_reboot.tr.Hair import Hair, HairPoint, HairPointWeight
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey
from io_scene_tr_reboot.util.Enumerable import Enumerable

class HairExporter:
    scale_factor: float
    game: CdcGame

    def __init__(self, scale_factor: float, game: CdcGame) -> None:
        self.scale_factor = scale_factor
        self.game = game

    def export_hair(self, folder_path: str, bl_obj: bpy.types.Object) -> None:
        ids = BlenderNaming.parse_hair_name(bl_obj.name)

        bl_obj_eval = bl_obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
        bl_data_eval = bl_obj_eval.data
        if not isinstance(bl_data_eval, bpy.types.Curves):
            raise Exception(f"{bl_obj.name} is not a hair curve object.")

        bl_modifier: bpy.types.NodesModifier | None = None
        if Enumerable(bl_data_eval.curves).any(lambda c: c.points_length != 16):
            bl_modifier = self.add_curve_resample_modifier(bl_obj)
            bl_obj_eval = bl_obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
            bl_data_eval = cast(bpy.types.Curves, bl_obj_eval.data)

        tr_hair = self.create_hair(bl_obj_eval, bl_data_eval)

        if bl_modifier is not None:
            bl_node_group = bl_modifier.node_group
            bl_obj.modifiers.remove(bl_modifier)
            if bl_node_group is not None:
                bpy.data.node_groups.remove(bl_node_group)

        resource_type: ResourceType
        extension: str
        if ids.model_id is not None:
            resource_type = ResourceType.MODEL
            extension = f".tr{self.game}modeldata"
        else:
            resource_type = ResourceType.DTP
            extension = f".tr{self.game}dtp"

        file_path = os.path.join(folder_path, str(ids.hair_data_id) + extension)
        with open(file_path, "wb") as file:
            builder = ResourceBuilder(ResourceKey(resource_type, ids.hair_data_id), self.game)
            tr_hair.write(builder)
            file.write(builder.build())

    def add_curve_resample_modifier(self, bl_obj: bpy.types.Object) -> bpy.types.NodesModifier:
        bl_node_group = cast(bpy.types.GeometryNodeTree, bpy.data.node_groups.new("__resample", "GeometryNodeTree"))   # type: ignore
        bl_node_group.is_modifier = True

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

    def create_hair(self, bl_hair_obj: bpy.types.Object, bl_hair_data: bpy.types.Curves) -> Hair:
        id_set = BlenderNaming.parse_hair_name(bl_hair_obj.name)
        tr_hair = Factories.get(self.game).create_hair(id_set.model_id, id_set.hair_data_id)

        if len(bl_hair_data.materials) > 0:
            bl_material = bl_hair_data.materials[0]
            if bl_material is not None:
                tr_hair.material_id = BlenderNaming.parse_material_name(bl_material.name)

        coords = array("f", [0]) * (len(bl_hair_data.points) * 3)
        bl_hair_data.position_data.foreach_get("vector", coords)

        thicknesses = array("f", [0]) * len(bl_hair_data.points)
        bl_thickness_attr = bl_hair_data.attributes.get("radius")
        if isinstance(bl_thickness_attr, bpy.types.FloatAttribute):
            bl_thickness_attr.data.foreach_get("value", thicknesses)

        global_bone_weights: dict[int, array[float]] = {}
        for bl_bone_attr in bl_hair_data.attributes:
            if not isinstance(bl_bone_attr, bpy.types.FloatAttribute):
                continue

            bone_id_set = BlenderNaming.try_parse_bone_name(bl_bone_attr.name)
            if bone_id_set is None or bone_id_set.global_id is None:
                continue

            bone_weights = array("f", [0]) * len(bl_hair_data.points)
            bl_bone_attr.data.foreach_get("value", bone_weights)
            global_bone_weights[bone_id_set.global_id] = bone_weights

        coord_idx = 0
        for point_idx in range(len(bl_hair_data.points)):
            position = (
                coords[coord_idx + 0] / self.scale_factor,
                coords[coord_idx + 1] / self.scale_factor,
                coords[coord_idx + 2] / self.scale_factor
            )
            coord_idx += 3

            thickness = thicknesses[point_idx] / self.scale_factor

            tr_point_weights: list[HairPointWeight] = []
            for global_bone_id, bone_weights in global_bone_weights.items():
                if bone_weights[point_idx] > 0:
                    tr_point_weights.append(HairPointWeight(global_bone_id, bone_weights[point_idx]))

            tr_hair.points.append(
                HairPoint(
                    position,
                    thickness,
                    tr_point_weights
                )
            )

        return tr_hair
