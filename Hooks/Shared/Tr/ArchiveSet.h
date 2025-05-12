#pragma once

namespace Tr
{
    class ArchiveSet
    {
    public:
        static ArchiveSet* GetInstance();

        void LoadNewArchivesAsync();
        void UnloadMissingArchivesAsync();
    };
}
