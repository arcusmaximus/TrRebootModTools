#include "pch.h"

namespace Tr
{
    typedef CommonMaterial* GetById_t(void* pMaterialSection, int id);

    CommonMaterial* CommonMaterial::GetById(int id)
    {
        GetById_t* pFunc = (GetById_t*)(Game::ImageBase + 0x3B0D70);
        return pFunc(nullptr, id);
    }
}
