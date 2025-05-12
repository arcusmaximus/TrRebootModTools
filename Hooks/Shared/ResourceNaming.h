#pragma once

class ResourceNaming
{
public:
    static bool TryGetResourceKey(const std::string& filePath, ResourceKey* pResourceKey);

private:
    static std::map<std::string, ResourceType, Util::CaseInsensitiveLess> ResourceTypesByExtension;
};
