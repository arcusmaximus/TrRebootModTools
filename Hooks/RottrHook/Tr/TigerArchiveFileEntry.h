#pragma once

namespace Tr
{
    struct TigerArchiveFileEntry
    {
        DWORD nameHash;
        DWORD locale;
        int uncompressedSize;
        int compressedSize;
        short archivePart;
        short archiveId;
        int offset;
    };
}
