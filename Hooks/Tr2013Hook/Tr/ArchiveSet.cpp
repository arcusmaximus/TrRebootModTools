#include "pch.h"

using namespace std;

namespace Tr
{
    const char* HookToolArchiveName = "bigfile._mod.hooktool.000.000";

    typedef IFileSystem* MountArchive_t(const char* pszName, bool lockless);

    MultiFileSystem* GetTigersMultiFS()
    {
        return *(MultiFileSystem**)(Game::ImageBase + 0x106C270);
    }

    bool IsHookToolArchiveAvailable()
    {
        string nfoFileName = HookToolArchiveName;
        nfoFileName += ".nfo";
        return GetFileAttributesA(nfoFileName.c_str()) != INVALID_FILE_ATTRIBUTES;
    }

    ArchiveSet* ArchiveSet::GetInstance()
    {
        return *(ArchiveSet**)(Game::ImageBase + 0x1E96F60);
    }

    void ArchiveSet::LoadNewArchivesAsync()
    {
        if (!IsHookToolArchiveAvailable() || GetTigersMultiFS()->GetChildByName(HookToolArchiveName) != nullptr)
            return;

        string archiveFileName = HookToolArchiveName;
        archiveFileName += ".tiger";

        Locale_t locale = GetTigersMultiFS()->GetGameLocale();

        MountArchive_t* pFunc = (MountArchive_t*)(Game::ImageBase + 0x1357B0);
        IFileSystem* pFS = pFunc(archiveFileName.c_str(), false);
        pFS->SetGameLocale(locale);
    }

    void ArchiveSet::UnloadMissingArchivesAsync()
    {
        if (IsHookToolArchiveAvailable())
            return;

        IFileSystem* pFS = GetTigersMultiFS()->GetChildByName(HookToolArchiveName);
        if (pFS == nullptr)
            return;

        GetTigersMultiFS()->RemoveChild(pFS);
        delete pFS;
    }
}
