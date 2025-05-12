#include "pch.h"

using namespace std;
using namespace Tr;

extern "C"
{
    void* TrAddr_ArchivesLoaded;
    void* TrAddr_GameLoopStart;
    void* TrAddr_IsGameWindowActive;
    void* TrAddr_RequestFile;
    void* TrAddr_ParseMaterial;
    void* TrAddr_MSFileSystemFile_dtor;

    void TrHandler_GameLoopStart()
    {
        static bool gameEnteredNotificationSent = false;
        if (!gameEnteredNotificationSent)
        {
            NotificationChannel::Instance.NotifyGameEntered();
            gameEnteredNotificationSent = true;
        }
        CommandChannel::Instance.HandlePendingCommand();
    }

    void TrHandler_RequestFile(const TigerArchiveFileEntry* pEntry, const char* pszPath)
    {
#if TR_VERSION == 11
        static unordered_set<QWORD> notifiedWemNameHashes;
        int len = strlen(pszPath);
        if (len > 4 && strcmp(pszPath + len - 4, ".wem") == 0)
        {
            if (notifiedWemNameHashes.contains(pEntry->nameHash))
                return;

            notifiedWemNameHashes.insert(pEntry->nameHash);
        }

        NotificationChannel::Instance.NotifyOpeningFile(pEntry->nameHash, pEntry->locale, pszPath);
#else
        NotificationChannel::Instance.NotifyOpeningFile(pEntry->nameHash, pEntry->locale | 0xFFFFFFFF00000000, pszPath);
#endif
    }

    void TrHandler_ParseMaterial(int materialId, MaterialData* pData)
    {
        MaterialConstantStore::Instance.Apply(materialId, pData);
    }

    void TrHandler_MSFileSystemFile_dtor(MSFileSystemFile* pFile)
    {
        // Close file handles that the game would otherwise leave open
        pFile->Close();
    }
}

void TrHook::Install()
{
    InitAddresses();

    DetourTransactionBegin();
    DetourAttach(&TrAddr_GameLoopStart, TrHook_GameLoopStart);

    if (TrAddr_IsGameWindowActive)
        DetourAttach(&TrAddr_IsGameWindowActive, TrHook_IsGameWindowActive);

    DetourAttach(&TrAddr_RequestFile,           TrHook_RequestFile);
    DetourAttach(&TrAddr_ParseMaterial,         TrHook_ParseMaterial);
    DetourAttach(&TrAddr_MSFileSystemFile_dtor, TrHook_MSFileSystemFile_dtor);
    InstallGameSpecificHooks();
    DetourTransactionCommit();

    DWORD oldProtect;
    VirtualProtect(Game::TextSectionBase, Game::TextSectionLength, PAGE_READWRITE, &oldProtect);
    ApplyGameSpecificPatches();
    VirtualProtect(Game::TextSectionBase, Game::TextSectionLength, oldProtect, &oldProtect);
}
