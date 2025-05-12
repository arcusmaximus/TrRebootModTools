#pragma once

namespace Tr
{
    struct TigerArchiveFileEntry
    {
        DWORD nameHash;
        DWORD locale;
        DWORD size;
        DWORD packedOffset;
    };
}
