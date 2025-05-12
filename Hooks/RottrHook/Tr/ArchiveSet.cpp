#include "pch.h"

namespace Tr
{
    ArchiveSet* ArchiveSet::GetInstance()
    {
        return *(ArchiveSet**)(Game::ImageBase + 0x2D8B4D0);
    }

    typedef void LoadNewArchivesAsync_t(ArchiveSet* pArchiveSet, int maxMount);
    typedef void UnloadMissingArchivesAsync_t(ArchiveSet* pArchiveSet, int maxUnmount);

    void ArchiveSet::LoadNewArchivesAsync()
    {
        ((LoadNewArchivesAsync_t*)(Game::ImageBase + 0x54E490))(this, 1);
    }

    void ArchiveSet::UnloadMissingArchivesAsync()
    {
        ((UnloadMissingArchivesAsync_t*)(Game::ImageBase + 0x54E5F0))(this, 1);
    }
}
