#include "pch.h"

namespace Tr
{
    const wchar_t* Game::ExeName = L"ROTTR.exe";
    BYTE* Game::ImageBase = (BYTE*)GetModuleHandle(nullptr);
    BYTE* Game::TextSectionBase = Game::ImageBase + 0x1000;
    int Game::TextSectionLength = 0x00CFFC00;
}
