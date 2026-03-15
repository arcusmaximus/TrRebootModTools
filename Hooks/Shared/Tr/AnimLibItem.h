#pragma once

namespace Tr
{
    struct AnimLibItem
    {
        WORD id;
        WORD loadedId;
        void* pFxList;
        const char* pszName;
    };
}
