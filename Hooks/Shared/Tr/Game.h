#pragma once

namespace Tr
{
    class Game
    {
    public:
        static const wchar_t* ExeName;
        static BYTE* ImageBase;
        static BYTE* TextSectionBase;
        static int TextSectionLength;
    };
}
