#include "pch.h"

namespace Tr
{
    typedef CommonMaterial* __stdcall GetById_t(int id);

    CommonMaterial* CommonMaterial::GetById(int id)
    {
        GetById_t* pFunc = (GetById_t*)(Game::ImageBase + 0x28BB60);
        return pFunc(id);
    }
}
