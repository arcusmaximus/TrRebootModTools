#include "pch.h"

using namespace std;
using namespace Util;

map<string, ResourceType, CaseInsensitiveLess> ResourceNaming::ResourceTypesByExtension =
{
    { ".dds",           ResourceType::Texture },
    { ".mesh",          ResourceType::Model },
    { ".pcd9",          ResourceType::Texture },

    { ".tr9anim",       ResourceType::Animation },
    { ".tr9animlib",    ResourceType::AnimationLib },
    { ".tr9cmesh",      ResourceType::CollisionMesh },
    { ".tr9dtp",        ResourceType::Dtp },
    { ".tr9material",   ResourceType::Material },
    { ".tr9modeldata",  ResourceType::Model },
    { ".tr9objectref",  ResourceType::GlobalContentReference },
    { ".tr9psdres",     ResourceType::PsdRes },
    { ".tr9script",     ResourceType::Script },
    { ".tr9shaderlib",  ResourceType::ShaderLib },
    { ".tr9sound",      ResourceType::SoundBank },
};
