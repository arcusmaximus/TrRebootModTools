from io_scene_tr_reboot.tr.rise.RiseCollisionShape import RiseCollisionShape

class ShadowCollisionShape(RiseCollisionShape):
    @staticmethod
    def _convert_dtp_bone_id_to_global(dtp_bone_id: int, global_bone_ids: list[int | None]) -> int:
        return dtp_bone_id

    @staticmethod
    def _convert_global_bone_id_to_dtp(global_bone_id: int, global_bone_ids: list[int | None]) -> int:
        return global_bone_id
