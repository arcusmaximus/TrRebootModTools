#include "pch.h"

namespace Tr
{
    ArchiveSet* ArchiveSet::GetInstance()
    {
        return *(ArchiveSet**)(Game::ImageBase + 0x3600618);
    }

    typedef void LoadNewArchivesAsync_t(ArchiveSet* pArchiveSet, int maxMount);
    typedef void UnloadMissingArchivesAsync_t(ArchiveSet* pArchiveSet, int maxUnmount);

    void ArchiveSet::LoadNewArchivesAsync()
    {
        ((LoadNewArchivesAsync_t*)(Game::ImageBase + 0x61AFF0))(this, 1);
    }

    void ArchiveSet::UnloadMissingArchivesAsync()
    {
        ((UnloadMissingArchivesAsync_t*)(Game::ImageBase + 0x61B0E0))(this, 1);
    }
}
