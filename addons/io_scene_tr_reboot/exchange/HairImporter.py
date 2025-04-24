from array import array
from typing import cast
import bpy
from io_scene_tr_reboot.BlenderHelper import BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.properties.SceneProperties import SceneProperties
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.tr.Hair import Hair, HairPart, HairStrandGroup
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey

class HairImporter:
    POINT_FACTOR_GEOMETRY_NODES_NAME = "Factor for strand coloring"
    POINT_FACTOR_ATTR_NAME = "hair_point_factor"
    MASTER_STRANDS_MATERIAL_COLOR = (0, 0, 1, 1)
    DEFAULT_HAIR_ROOT_COLOR = (0, 0, 0, 1)
    DEFAULT_HAIR_TIP_COLOR  = (1, 1, 1, 1)

    scale_factor: float

    def __init__(self, scale_factor: float) -> None:
        self.scale_factor = scale_factor

    def import_from_collection(self, tr_collection: Collection, bl_armature_objs: dict[ResourceKey, bpy.types.Object]) -> list[bpy.types.Object]:
        bl_hair_part_objs: list[bpy.types.Object] = []

        for hair_resource_set in tr_collection.get_hair_resource_sets():
            tr_hair = tr_collection.get_hair(hair_resource_set.hair_resource)
            if tr_hair is None:
                continue

            bl_parent_obj = bl_armature_objs.get(hair_resource_set.skeleton_resource) if hair_resource_set.skeleton_resource is not None else None
            if bl_parent_obj is None:
                empty_name = BlenderNaming.make_hair_asset_name(tr_collection.name, tr_hair.model_id, tr_hair.hair_data_id)
                bl_parent_obj = bpy.data.objects.get(empty_name) or BlenderHelper.create_object(None, empty_name)

            bl_hair_part_objs.extend(self.import_hair(tr_collection, tr_hair, bl_parent_obj))

        self.store_collection_files(tr_collection)

        if bpy.context.scene is not None:
            bpy.context.scene.render.hair_subdiv = 1

        return bl_hair_part_objs

    def import_hair(self, tr_collection: Collection, tr_hair: Hair, bl_parent_obj: bpy.types.Object | None) -> list[bpy.types.Object]:
        bl_strand_group_objs: list[bpy.types.Object] = []

        for tr_hair_part in tr_hair.parts:
            bl_master_strand_group_obj = self.import_strand_group(tr_collection, tr_hair, tr_hair_part, True)
            if bl_master_strand_group_obj is not None:
                bl_master_strand_group_obj.parent = bl_parent_obj
                bl_strand_group_objs.append(bl_master_strand_group_obj)

            bl_slave_strand_group_obj = self.import_strand_group(tr_collection, tr_hair, tr_hair_part, False)
            if bl_slave_strand_group_obj is not None:
                bl_slave_strand_group_obj.parent = bl_master_strand_group_obj or bl_parent_obj
                bl_strand_group_objs.append(bl_slave_strand_group_obj)

        return bl_strand_group_objs

    def import_strand_group(self, tr_collection: Collection, tr_hair: Hair, tr_hair_part: HairPart, is_master: bool) -> bpy.types.Object | None:
        tr_strand_group = tr_hair_part.master_strands if is_master else tr_hair_part.slave_strands
        if len(tr_strand_group.points) == 0 or len(tr_strand_group.points) != sum(tr_strand_group.strand_point_counts):
            return None

        strand_group_name = BlenderNaming.make_hair_strand_group_name(tr_collection.name, tr_hair.model_id, tr_hair.hair_data_id, tr_hair_part.name, is_master)
        bl_strand_group = bpy.data.hair_curves.new(strand_group_name)

        bl_strand_group.add_curves(tr_strand_group.strand_point_counts)
        self.apply_point_positions(bl_strand_group, tr_strand_group)
        self.apply_point_weights(bl_strand_group, tr_strand_group)

        if tr_hair.supports_strand_thickness:
            self.apply_point_thicknesses(bl_strand_group, tr_strand_group)

        material_name = BlenderNaming.make_hair_strand_group_material_name(tr_hair.material_id, is_master)
        bl_strand_group.materials.append(self.get_or_create_material(material_name, is_master))

        bl_strand_group_obj = BlenderHelper.create_object(bl_strand_group)
        if is_master:
            bl_strand_group_obj.show_in_front = True
        else:
            self.add_point_factor_modifier(bl_strand_group_obj)

        return bl_strand_group_obj

    def apply_point_positions(self, bl_strand_group: bpy.types.Curves, tr_strand_group: HairStrandGroup) -> None:
        coords = array("f", [0]) * (len(tr_strand_group.points) * 3)
        coord_idx = 0
        for tr_point in tr_strand_group.points:
            coords[coord_idx + 0] = tr_point.position[0] * self.scale_factor
            coords[coord_idx + 1] = tr_point.position[1] * self.scale_factor
            coords[coord_idx + 2] = tr_point.position[2] * self.scale_factor
            coord_idx += 3

        bl_strand_group.points.foreach_set("position", coords)

    def apply_point_weights(self, bl_strand_group: bpy.types.Curves, tr_strand_group: HairStrandGroup) -> None:
        global_bone_weights: dict[int, array[float]] = {}
        for i, tr_point in enumerate(tr_strand_group.points):
            for tr_weight in tr_point.weights:
                weight_array = global_bone_weights.get(tr_weight.global_bone_id)
                if weight_array is None:
                    weight_array = array("f", [0]) * len(tr_strand_group.points)
                    global_bone_weights[tr_weight.global_bone_id] = weight_array

                weight_array[i] = tr_weight.weight

        for global_bone_id, weight_array in global_bone_weights.items():
            bl_bone_attr = cast(bpy.types.FloatAttribute, bl_strand_group.attributes.new(BlenderNaming.make_bone_name(None, global_bone_id, None), "FLOAT", "POINT"))
            bl_bone_attr.data.foreach_set("value", weight_array)

    def apply_point_thicknesses(self, bl_strand_group: bpy.types.Curves, tr_strand_group: HairStrandGroup) -> None:
        thicknesses = array("f", [0]) * len(tr_strand_group.points)
        for i, tr_point in enumerate(tr_strand_group.points):
            thicknesses[i] = tr_point.thickness * self.scale_factor

        bl_strand_group.points.foreach_set("radius", thicknesses)

    def add_point_factor_modifier(self, bl_strand_group_obj: bpy.types.Object) -> None:
        bl_modifier = cast(bpy.types.NodesModifier, bl_strand_group_obj.modifiers.new(HairImporter.POINT_FACTOR_GEOMETRY_NODES_NAME, "NODES"))
        bl_modifier.node_group = self.get_or_create_point_factor_geometry_nodes()

    def get_or_create_point_factor_geometry_nodes(self) -> bpy.types.GeometryNodeTree:
        bl_node_group = bpy.data.node_groups.get(HairImporter.POINT_FACTOR_GEOMETRY_NODES_NAME)
        if isinstance(bl_node_group, bpy.types.GeometryNodeTree):
            return bl_node_group

        bl_node_group = cast(bpy.types.GeometryNodeTree, bpy.data.node_groups.new(HairImporter.POINT_FACTOR_GEOMETRY_NODES_NAME, "GeometryNodeTree"))
        bl_node_group.is_modifier = True
        if bl_node_group.interface is None:
            raise Exception()

        bl_node_group.interface.new_socket("Geometry", in_out = "INPUT",  socket_type = "NodeSocketGeometry")
        bl_node_group.interface.new_socket("Geometry", in_out = "OUTPUT", socket_type = "NodeSocketGeometry")

        bl_input_node = bl_node_group.nodes.new("NodeGroupInput")

        bl_spline_parameter_node = bl_node_group.nodes.new("GeometryNodeSplineParameter")
        bl_spline_parameter_node.location = (200, -100)

        bl_store_attr_node = bl_node_group.nodes.new("GeometryNodeStoreNamedAttribute")
        bl_store_attr_node.location = (400, 50)
        cast(bpy.types.NodeSocketString, bl_store_attr_node.inputs[2]).default_value = HairImporter.POINT_FACTOR_ATTR_NAME

        # Group Input.Geometry -> Store Named Attribute.Geometry
        bl_node_group.links.new(bl_input_node.outputs[0], bl_store_attr_node.inputs[0])

        # Spline Parameter.Factor -> Store Named Attribute.Value
        bl_node_group.links.new(bl_spline_parameter_node.outputs[0], bl_store_attr_node.inputs[3])

        bl_output_node = bl_node_group.nodes.new("NodeGroupOutput")
        bl_output_node.location = (600, 0)

        # Store Named Attribute.Geometry -> Group Output.Geometry
        bl_node_group.links.new(bl_store_attr_node.outputs[0], bl_output_node.inputs[0])

        return bl_node_group

    def get_or_create_material(self, name: str, is_master: bool) -> bpy.types.Material:
        bl_material = bpy.data.materials.get(name)
        if bl_material is not None:
            return bl_material

        bl_material = BlenderHelper.create_empty_material(name)
        bl_node_group = bl_material.node_tree
        if bl_node_group is None:
            raise Exception()

        bl_attr_node = cast(bpy.types.ShaderNodeAttribute, bl_node_group.nodes.new("ShaderNodeAttribute"))
        bl_attr_node.attribute_name = HairImporter.POINT_FACTOR_ATTR_NAME

        bl_color_ramp_node = cast(bpy.types.ShaderNodeValToRGB, bl_node_group.nodes.new("ShaderNodeValToRGB"))
        bl_color_ramp_node.location = (200, 0)
        if bl_color_ramp_node.color_ramp is None:
            raise Exception()

        bl_color_ramp_node.color_ramp.elements[0].color = HairImporter.DEFAULT_HAIR_ROOT_COLOR
        bl_color_ramp_node.color_ramp.elements[0].position = 0.2
        bl_color_ramp_node.color_ramp.elements[1].color = HairImporter.DEFAULT_HAIR_TIP_COLOR

        # Attribute.Fac -> Color Ramp.Fac
        bl_node_group.links.new(bl_attr_node.outputs[2], bl_color_ramp_node.inputs[0])

        bl_shader_node = bl_node_group.nodes.new("ShaderNodeBsdfPrincipled")
        bl_shader_node.location = (500, 0)

        # Color Ramp.Color -> Shader.Color
        bl_node_group.links.new(bl_color_ramp_node.outputs[0], bl_shader_node.inputs[0])

        bl_output_node = bl_node_group.nodes.new("ShaderNodeOutputMaterial")
        bl_output_node.location = (800, 0)

        # Shader.BSDF -> Output.Surface
        bl_node_group.links.new(bl_shader_node.outputs[0], bl_output_node.inputs[0])

        if is_master:
            bl_material.diffuse_color = HairImporter.MASTER_STRANDS_MATERIAL_COLOR

        return bl_material

    def store_collection_files(self, tr_collection: Collection) -> None:
        for file_id, file_data in self.get_collection_files_to_store(tr_collection).items():
            SceneProperties.set_file(file_id, file_data)

    def get_collection_files_to_store(self, tr_collection: Collection) -> dict[int, bytes]:
        return {}
