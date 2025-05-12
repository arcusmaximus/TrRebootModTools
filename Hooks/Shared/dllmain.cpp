#include "pch.h"

using namespace Tr;

BOOL DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    switch (ul_reason_for_call)
    {
        case DLL_PROCESS_ATTACH:
            TrHook::Install();
            break;
    }
    return TRUE;
}

