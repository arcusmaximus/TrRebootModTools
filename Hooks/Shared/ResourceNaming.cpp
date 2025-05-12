#include "pch.h"

using namespace std;

bool ResourceNaming::TryGetResourceKey(const string& filePath, ResourceKey* pResourceKey)
{
    regex re(R"((?:^|[\\/\.])(\d+)(\.\w+)$)");
    smatch match;
    if (!regex_search(filePath, match, re))
        return false;

    int resourceId = atoi(match[1].str().c_str());
    auto typeIt = ResourceTypesByExtension.find(match[2].str());
    if (typeIt == ResourceTypesByExtension.end())
        return false;

    *pResourceKey = ResourceKey(typeIt->second, resourceId);
    return true;
}
