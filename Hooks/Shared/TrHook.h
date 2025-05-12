#pragma once

class TrHook
{
public:
    static void Install();

private:
    static void InitAddresses();
    static void InstallGameSpecificHooks();
    static void ApplyGameSpecificPatches();
};

extern "C"
{
    extern void* TrAddr_GameLoopStart;
    extern void* TrAddr_IsGameWindowActive;
    extern void* TrAddr_RequestFile;
    extern void* TrAddr_ParseMaterial;
    extern void* TrAddr_MSFileSystemFile_dtor;

    void TrHook_GameLoopStart();
    void TrHook_IsGameWindowActive();
    void TrHook_RequestFile();
    void TrHook_ParseMaterial();
    void TrHook_MSFileSystemFile_dtor();
}
