from enum import IntEnum

class CdcGame(IntEnum):
    TR2013 = 9
    ROTTR = 10
    SOTTR = 11

class ResourceType(IntEnum):
    ANIMATION = 2
    BLENDSHAPEDRIVERS = 4
    TEXTURE = 5
    SOUND = 6
    DTP = 7
    SCRIPT = 8
    SHADERLIB = 9
    MATERIAL = 10
    GLOBALCONTENTREFERENCE = 11
    MODEL = 12
    COLLISIONMESH = 13
    OBJECTREFERENCE = 14
    TRIGGER = 15
