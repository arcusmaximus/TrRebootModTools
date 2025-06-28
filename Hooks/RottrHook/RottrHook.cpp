#include "pch.h"

using namespace Tr;

void TrHook::InitAddresses()
{
    TrAddr_GameLoopStart            = Game::ImageBase + 0x459310;
    TrAddr_IsGameWindowActive       = Game::ImageBase + 0xCA2DA0;
    TrAddr_RequestFile              = Game::ImageBase + 0x199B50;
    TrAddr_ParseMaterial            = Game::ImageBase + 0x344DF7;
    TrAddr_MSFileSystemFile_dtor    = Game::ImageBase + 0x19AB80;
}

void TrHook::InstallGameSpecificHooks()
{
}

void TrHook::ApplyGameSpecificPatches()
{
    // Skip launcher. We *could* of course just pass the -nolauncher command line argument instead,
    // but ironically, that may result in a safety warning from Steam.
    *(BYTE*)(Game::ImageBase + 0xC8309D) = 0xEB;

    // Bugfix: The function that loads new archives raises a fatal error if it didn't load bigfile.000.tiger,
    // but this also gets triggered if the archive was already loaded previously. Skip this check.
    *(BYTE*)(Game::ImageBase + 0x54CA7E) = 0xEB;

    // Don't block writing on open .tiger files
    *(BYTE*)(Game::ImageBase + 0x19B7B8) = FILE_SHARE_READ | FILE_SHARE_WRITE;
}
