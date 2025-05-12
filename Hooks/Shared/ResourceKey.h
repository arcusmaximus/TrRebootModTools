#pragma once

enum class ResourceType
{
    Unknown = 0,
    Animation = 2,
    PsdRes = 4,
    Texture = 5,
    SoundBank = 6,
    Dtp = 7,
    Script = 8,
    ShaderLib = 9,
    Material = 10,
    GlobalContentReference = 11,
    Model = 12,
    CollisionMesh = 13,
    ObjectReference = 14,
    AnimationLib = 15
};

struct ResourceKey
{
    ResourceType Type;
    int Id;

    ResourceKey()
    {
        Type = ResourceType::Unknown;
        Id = 0;
    }

    ResourceKey(ResourceType type, int id)
    {
        Type = type;
        Id = id;
    }

    bool operator<(const ResourceKey& other) const
    {
        if (Type < other.Type)
            return true;

        if (Type > other.Type)
            return false;

        return Id < other.Id;
    }

    bool operator==(const ResourceKey& other) const
    {
        return Type == other.Type && Id == other.Id;
    }
};
