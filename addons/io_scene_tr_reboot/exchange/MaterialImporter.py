from typing import NamedTuple, cast
import bpy
import os
import re
from io_scene_tr_reboot.BlenderHelper import BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.tr.Enumerations import ResourceType
from io_scene_tr_reboot.tr.Material import Material
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey
from io_scene_tr_reboot.util.DdsFile import DdsFile, DdsType
from io_scene_tr_reboot.util.Enumerable import Enumerable
from io_scene_tr_reboot.util.SlotsBase import SlotsBase

class _TextureNodeSet(NamedTuple):
    diffuse: bpy.types.ShaderNodeTexImage | None
    normal:  bpy.types.ShaderNodeTexImage | None

class MaterialImporter(SlotsBase):
    def import_from_collection(self, tr_collection: Collection) -> None:
        for material_resource in tr_collection.get_resources(ResourceType.MATERIAL):
            self.import_material(tr_collection, material_resource)

    def import_material(self, tr_collection: Collection, material_resource: ResourceKey) -> bpy.types.Material | None:
        material_name = BlenderNaming.make_material_name(tr_collection.get_resource_name(material_resource), material_resource.id)

        bl_material = bpy.data.materials.get(material_name)
        if bl_material is not None:
            return bl_material

        tr_material = tr_collection.get_material(material_resource)
        if tr_material is None:
            return None

        bl_material = BlenderHelper.create_empty_material(material_name)
        if bl_material.node_tree is None:
            raise Exception()

        bl_texture_nodes = self.__add_texture_nodes(bl_material, tr_collection, tr_material)

        bl_transparent_node = bl_material.node_tree.nodes.new("ShaderNodeBsdfTransparent")
        bl_transparent_node.location = (300, 0)

        bl_diffuse_shader_node = bl_material.node_tree.nodes.new("ShaderNodeBsdfDiffuse")
        bl_diffuse_shader_node.location = (300, -100)

        if bl_texture_nodes.diffuse is not None:
            bl_material.node_tree.links.new(bl_texture_nodes.diffuse.outputs[0], bl_diffuse_shader_node.inputs[0])

        bl_transparent_node.width = max(bl_transparent_node.width, bl_diffuse_shader_node.width)
        bl_diffuse_shader_node.width = bl_transparent_node.width

        bl_normal_node: bpy.types.Node | None = None
        if bl_texture_nodes.normal is not None:
            bl_separate_node = bl_material.node_tree.nodes.new("ShaderNodeSeparateColor")
            bl_separate_node.location = (300, -250)
            bl_separate_node.hide = True
            bl_material.node_tree.links.new(bl_texture_nodes.normal.outputs[0], bl_separate_node.inputs[0])

            bl_combine_node = bl_material.node_tree.nodes.new("ShaderNodeCombineColor")
            bl_combine_node.location = (300, -300)
            bl_combine_node.hide = True
            bl_material.node_tree.links.new(bl_separate_node.outputs[0], bl_combine_node.inputs[0])
            bl_material.node_tree.links.new(bl_separate_node.outputs[1], bl_combine_node.inputs[1])
            cast(bpy.types.NodeSocketFloat, bl_combine_node.inputs[2]).default_value = 1.0

            bl_normal_node = bl_material.node_tree.nodes.new("ShaderNodeNormalMap")
            bl_normal_node.location = (300, -350)
            cast(bpy.types.NodeSocketFloat, bl_normal_node.inputs[0]).default_value = 0.5
            bl_material.node_tree.links.new(bl_combine_node.outputs[0], bl_normal_node.inputs[1])
            bl_material.node_tree.links.new(bl_normal_node.outputs[0], bl_diffuse_shader_node.inputs[2])

        bl_mix_node = bl_material.node_tree.nodes.new("ShaderNodeMixShader")
        bl_mix_node.location = (500, 0)
        bl_material.node_tree.links.new(bl_transparent_node.outputs[0], bl_mix_node.inputs[1])
        bl_material.node_tree.links.new(bl_diffuse_shader_node.outputs[0], bl_mix_node.inputs[2])
        if bl_texture_nodes.diffuse is not None:
            bl_material.node_tree.links.new(bl_texture_nodes.diffuse.outputs[1], bl_mix_node.inputs[0])

        bl_output_node = bl_material.node_tree.nodes.new("ShaderNodeOutputMaterial")
        bl_output_node.location = (700, 0)
        bl_material.node_tree.links.new(bl_mix_node.outputs[0], bl_output_node.inputs[0])

        return bl_material

    def __add_texture_nodes(self, bl_material: bpy.types.Material, tr_collection: Collection, tr_material: Material) -> _TextureNodeSet:
        bl_texture_nodes: list[bpy.types.ShaderNodeTexImage] = []

        texture_resources: list[ResourceKey] = list(tr_material.pixel_shader_textures)
#        texture_resources.extend(
#            Enumerable(tr_mesh_part.texture_indices).where(lambda i: i != 0xFFFFFFFF)
#                                                    .select(lambda i: tr_model.texture_resources[i])
#                                                    .where(lambda r: r is not None)
#                                                    .cast(ResourceKey)
#        )

        diffuse_texture_idx = -1
        diffuse_texture_size = -1

        normal_texture_idx = -1
        normal_texture_size = -1

        if bl_material.node_tree is None:
            raise Exception()

        file_paths: list[str] = Enumerable(texture_resources).select(lambda r: tr_collection.get_resource_file_path(r)).of_type(str).to_list()

        def get_file_path_sort_key(file_path: str) -> str:
            file_name = os.path.splitext(os.path.basename(file_path))[0]
            if re.fullmatch(r"\d+", file_name):
                file_name = f"{file_name:0>10}"

            return file_name

        for file_path in sorted(file_paths, key = get_file_path_sort_key):
            file_name = os.path.splitext(os.path.basename(file_path))[0]
            texture_type: DdsType
            if re.search(r"_n\.\d+$", file_name):
                texture_type = DdsType.NORMAL
            else:
                texture_type = DdsFile.get_type(file_path)

            texture_size = os.path.getsize(file_path)
            if texture_type == DdsType.RGB and (diffuse_texture_idx < 0 or texture_size > diffuse_texture_size):
                diffuse_texture_idx = len(bl_texture_nodes)
                diffuse_texture_size = texture_size
            elif texture_type == DdsType.NORMAL and (normal_texture_idx < 0 or texture_size > normal_texture_size):
                normal_texture_idx = len(bl_texture_nodes)
                normal_texture_size = texture_size

            bl_image = bpy.data.images.get(os.path.basename(file_path))
            if bl_image is None:
                bl_image = bpy.data.images.load(file_path)

            bl_texture_node = cast(bpy.types.ShaderNodeTexImage, bl_material.node_tree.nodes.new("ShaderNodeTexImage"))
            bl_texture_node.image = bl_image
            bl_texture_node.location = (0, len(bl_texture_nodes) * -40)
            bl_texture_node.hide = True
            bl_texture_nodes.append(bl_texture_node)

        return _TextureNodeSet(
            diffuse_texture_idx >= 0 and bl_texture_nodes[diffuse_texture_idx] or None,
            normal_texture_idx  >= 0 and bl_texture_nodes[normal_texture_idx]  or None
        )
