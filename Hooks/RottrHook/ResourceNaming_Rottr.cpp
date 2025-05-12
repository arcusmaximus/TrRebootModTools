#include "pch.h"

using namespace std;
using namespace Util;

map<string, ResourceType, CaseInsensitiveLess> ResourceNaming::ResourceTypesByExtension =
{
    { ".anim",              ResourceType::Animation },
    { ".dds",               ResourceType::Texture },
    { ".dtp",               ResourceType::Dtp },
    { ".grplist",           ResourceType::ObjectReference },
    { ".material",          ResourceType::Material },
    { ".object",            ResourceType::GlobalContentReference },
    { ".psdres",            ResourceType::PsdRes },
    { ".script",            ResourceType::Script },
    { ".sound",             ResourceType::SoundBank },

    { ".tr10anim",          ResourceType::Animation },
    { ".tr10animlib",       ResourceType::AnimationLib },
    { ".tr10cmesh",         ResourceType::CollisionMesh },
    { ".tr10dtp",           ResourceType::Dtp },
    { ".tr10contentref",    ResourceType::GlobalContentReference },
    { ".tr10material",      ResourceType::Material },
    { ".tr10cubelut",       ResourceType::Model },
    { ".tr10modeldata",     ResourceType::Model },
    { ".tr10shresource",    ResourceType::Model },
    { ".tr10objectref",     ResourceType::ObjectReference },
    { ".tr10psdres",        ResourceType::PsdRes },
    { ".tr10script",        ResourceType::Script },
    { ".tr10shaderlib",     ResourceType::ShaderLib },
    { ".tr10sound",         ResourceType::SoundBank },
    
    { ".tr2cmesh",          ResourceType::CollisionMesh },
    { ".tr2mesh",           ResourceType::Model },
    { ".tr2pcd",            ResourceType::Texture },
    { ".trigger",           ResourceType::AnimationLib }
};
