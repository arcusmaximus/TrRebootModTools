from io_scene_tr_reboot.tr.Enumerations import ResourceType
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey

class ResourceReference(ResourceKey):
    offset: int

    def __init__(self, type: ResourceType, id: int, offset: int) -> None:
        super().__init__(type, id)
        self.offset = offset

    def __str__(self) -> str:
        return f"{self.type}:{self.id} @ {self.offset}"

    def __hash__(self) -> int:
        return hash((self.type, self.id, self.offset))

    def __eq__(self, value: object) -> bool:
        return isinstance(value, ResourceReference) and super().__eq__(value) and self.offset == value.offset
