#include "pch.h"

using namespace std;
using namespace Util;

map<string, ResourceType, CaseInsensitiveLess> ResourceNaming::ResourceTypesByExtension =
{
    { ".bnk",               ResourceType::SoundBank },
    { ".dds",               ResourceType::Texture },

    { ".tr11anim",          ResourceType::Animation },
    { ".tr11animlib",       ResourceType::AnimationLib },
    { ".tr11cubelut",       ResourceType::Model },
    { ".tr11cmesh",         ResourceType::CollisionMesh },
    { ".tr11contentref",    ResourceType::GlobalContentReference },
    { ".tr11dtp",           ResourceType::Dtp },
    { ".tr11material",      ResourceType::Material },
    { ".tr11model",         ResourceType::Model },
    { ".tr11modeldata",     ResourceType::Model },
    { ".tr11objectref",     ResourceType::ObjectReference },
    { ".tr11psdres",        ResourceType::PsdRes },
    { ".tr11script",        ResourceType::Script },
    { ".tr11shaderlib",     ResourceType::ShaderLib },
    { ".tr11shresource",    ResourceType::Model }
};
