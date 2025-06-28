from io_scene_tr_reboot.util.Crc32 import Crc32

def _Hashes__hash_simple(data: bytes) -> int:
    hash = 0
    for byte in data:
        hash ^= (hash << 5) + (hash >> 2) + byte
        hash &= 0xFFFFFFFF

    return hash

class Hashes:
    position            = Crc32.calculate(b"Position")
    normal              = Crc32.calculate(b"Normal")
    tesselation_normal  = Crc32.calculate(b"TessellationNormal")
    tangent             = Crc32.calculate(b"Tangent")
    binormal            = Crc32.calculate(b"Binormal")
    skin_weights        = Crc32.calculate(b"SkinWeights")
    skin_indices        = Crc32.calculate(b"SkinIndices")
    color1              = Crc32.calculate(b"Color1")
    color2              = Crc32.calculate(b"Color2")
    texcoord1           = Crc32.calculate(b"Texcoord1")
    texcoord2           = Crc32.calculate(b"Texcoord2")
    texcoord3           = Crc32.calculate(b"Texcoord3")
    texcoord4           = Crc32.calculate(b"Texcoord4")

    invmass             = Crc32.calculate(b"InvMass")
    local_rot           = Crc32.calculate(b"LocalRot")
    global_rot          = Crc32.calculate(b"GlobalRot")
    refvecs             = Crc32.calculate(b"RefVecs")
    thickness           = Crc32.calculate(b"Thickness")

    cloth                               = _Hashes__hash_simple(b"cloth")
    collisionmarker                     = _Hashes__hash_simple(b"collisionmarker")
    genericboxshapelist                 = _Hashes__hash_simple(b"genericboxshapelist")
    genericcapsuleshapelist             = _Hashes__hash_simple(b"genericcapsuleshapelist")
    genericdoubleradiicapsuleshapelist  = _Hashes__hash_simple(b"genericdoubleradiicapsuleshapelist")
    genericsphereshapelist              = _Hashes__hash_simple(b"genericsphereshapelist")
    meshref                             = _Hashes__hash_simple(b"meshref")
    modelhost                           = _Hashes__hash_simple(b"modelhost")
    objectref                           = _Hashes__hash_simple(b"objectref")