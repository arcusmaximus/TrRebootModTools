#include "pch.h"

using namespace std;
using namespace Tr;

extern "C"
{
    void* TrAddr_GetAnimation;
    void TrHook_GetAnimation();

    void TrHandler_GetAnimation(AnimLibItem* pAnim)
    {
        NotificationChannel::Instance.NotifyPlayingAnimation(pAnim->id, pAnim->pszName);
    }
}

void TrHook::InitAddresses()
{
    TrAddr_GameLoopStart            = Game::ImageBase + 0x4E3B70;
    TrAddr_IsGameWindowActive       = Game::ImageBase + 0x1003800;
    TrAddr_RequestFile              = Game::ImageBase + 0x1C8018;
    TrAddr_ParseMaterial            = Game::ImageBase + 0x3B4BD7;
    TrAddr_MSFileSystemFile_dtor    = Game::ImageBase + 0x1CE940;

    TrAddr_GetAnimation             = Game::ImageBase + 0x12BCA2;
}

void TrHook::InstallGameSpecificHooks()
{
    DetourAttach(&TrAddr_GetAnimation, TrHook_GetAnimation);
}

void TrHook::ApplyGameSpecificPatches()
{
    // Bugfix: The function that loads new archives raises a fatal error if it didn't load bigfile.000.tiger,
    // but this also gets triggered if the archive was already loaded previously. Skip this check.
    *(WORD*)(Game::ImageBase + 0x619CB7) = 0x7FEB;
    *(DWORD*)(Game::ImageBase + 0x619CB9) = 0x90909090;

    // Don't block writing on open .tiger files
    *(BYTE*)(Game::ImageBase + 0x1CF648) = FILE_SHARE_READ | FILE_SHARE_WRITE;
}
