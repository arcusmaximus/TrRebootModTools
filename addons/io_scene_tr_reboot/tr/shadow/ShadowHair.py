from ctypes import sizeof
from typing import TYPE_CHECKING, cast
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.rise.RiseHair import HairAssetBoundingBox, HairAssetCollisionData, HairAssetMasterStrands, HairAssetMasterVertices, HairAssetRange, HairAssetSlaveStrands, HairAssetSlaveVertices, IHairAsset, IHairAssetRenderingData, RiseHair
from io_scene_tr_reboot.util.CStruct import CFloat, CInt, CStruct64, CUInt
from io_scene_tr_reboot.util.CStructTypeMappings import CVec3

class HairAssetRenderingData(CStruct64, IHairAssetRenderingData if TYPE_CHECKING else object):
    slave_strip_indices: HairAssetRange
    offset_direction: HairAssetRange

assert(sizeof(HairAssetRenderingData) == 0x10)

class HairAsset(CStruct64, IHairAsset if TYPE_CHECKING else object):
    tag: CUInt
    version: CInt
    mode: CInt
    num_mesh_columns: CInt
    size: CUInt
    num_groups: CInt
    num_skinning_transforms: CInt
    num_master_strands: CInt
    num_master_vertices: CInt
    num_slave_strands: CInt
    num_slave_vertices: CInt
    bind_pose_aabb: HairAssetBoundingBox
    bind_pose_slave_offsets_reference_position: CVec3
    slave_offsets_scale: CFloat
    occlusion_sphere_radius: CFloat
    group_name_offsets: HairAssetRange
    master_strands: HairAssetMasterStrands
    master_vertices: HairAssetMasterVertices
    slave_strands: HairAssetSlaveStrands
    slave_vertices: HairAssetSlaveVertices
    rendering_data: HairAssetRenderingData      # type: ignore
    collision_data: HairAssetCollisionData

assert(sizeof(HairAsset) == 0x100)

class ShadowHair(RiseHair):
    def read_asset(self, reader: ResourceReader) -> IHairAsset:
        return reader.read_struct(HairAsset)

    def create_asset(self) -> IHairAsset:
        asset = HairAsset()
        asset.version = 24
        asset.num_mesh_columns = 1
        return asset

    def create_rendering_data(self) -> IHairAssetRenderingData:
        return HairAssetRenderingData()

    def append_rendering_data(self, writer: ResourceBuilder, asset: IHairAsset) -> None:
        super().append_rendering_data(writer, asset)

        rendering_data = cast(HairAssetRenderingData, asset.rendering_data)
        rendering_data.offset_direction = self.append_float_array(writer, [2, -1, 1])
