#include "pch.h"

using namespace Tr;

extern "C"
{
    void* TrAddr_LoadArchives;

    void TrHandler_GameLoopStart();
    void TrHandler_RequestFile(const TigerArchiveFileEntry* pEntry, const char* pszPath);
    void TrHandler_ParseMaterial(int materialId, MaterialData* pData);
    void TrHandler_MSFileSystemFile_dtor(MSFileSystemFile* pFile);

    void TrHandler_LoadArchives()
    {
        ArchiveSet::GetInstance()->LoadNewArchivesAsync();
    }

    void __declspec(naked) TrHook_GameLoopStart()
    {
        __asm
        {
            call TrHandler_GameLoopStart
            jmp [TrAddr_GameLoopStart]
        }
    }

    void TrHook_IsGameWindowActive()
    {
        // Handled with NOPs instead
    }

    void __declspec(naked) TrHook_RequestFile()
    {
        __asm
        {
            push ebx
            push edi
            call TrHandler_RequestFile
            add esp, 8
            jmp [TrAddr_RequestFile]
        }
    }

    void __declspec(naked) TrHook_ParseMaterial()
    {
        __asm
        {
            push ecx

            push [esp+4]
            push [ebp+8]
            call TrHandler_ParseMaterial
            add esp, 8

            pop ecx
            jmp [TrAddr_ParseMaterial]
        }
    }

    void __declspec(naked) TrHook_MSFileSystemFile_dtor()
    {
        __asm
        {
            push ecx

            push ecx
            call TrHandler_MSFileSystemFile_dtor
            add esp, 4

            pop ecx
            jmp [TrAddr_MSFileSystemFile_dtor]
        }
    }

    void __declspec(naked) TrHook_LoadArchives()
    {
        __asm
        {
            call TrHandler_LoadArchives
            jmp [TrAddr_LoadArchives]
        }
    }
}

void TrHook::InitAddresses()
{
    TrAddr_GameLoopStart            = Game::ImageBase + 0x227D60;
    TrAddr_RequestFile              = Game::ImageBase + 0x137C25;
    TrAddr_ParseMaterial            = Game::ImageBase + 0x28C76A;
    TrAddr_MSFileSystemFile_dtor    = Game::ImageBase + 0x138350;

    TrAddr_LoadArchives             = Game::ImageBase + 0x135674;
}

void TrHook::InstallGameSpecificHooks()
{
    DetourAttach(&TrAddr_LoadArchives, TrHook_LoadArchives);
}

void TrHook::ApplyGameSpecificPatches()
{
    // Execute game loop even if we're not the foreground window
    *(WORD*)(Game::ImageBase + 0x13DE19) = 0x9090;

    // Don't block writing on open .tiger files
    *(BYTE*)(Game::ImageBase + 0x138EEE) = FILE_SHARE_READ | FILE_SHARE_WRITE;

    // Change parameters for static constant buffers
    *(DWORD*)(Game::ImageBase + 0x21A1B1) = D3D11_USAGE_DYNAMIC;
    *(DWORD*)(Game::ImageBase + 0x21A1D5) = D3D11_CPU_ACCESS_WRITE;
}
