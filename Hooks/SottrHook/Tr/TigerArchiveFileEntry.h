#pragma once

namespace Tr
{
    struct TigerArchiveFileEntry
    {
        QWORD nameHash;
        QWORD locale;
        int uncompressedSize;
        int compressedSize;
        short archivePart;
        short archiveId;
        int offset;
    };
}
