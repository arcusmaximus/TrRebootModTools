#include "pch.h"

namespace Tr
{
    typedef PCDX11RenderDevice* (GetInstance_t)();

    PCDX11RenderDevice* PCDX11RenderDevice::GetInstance()
    {
        GetInstance_t* pGetInstance = (GetInstance_t*)(Game::ImageBase + 0x2442B0);
        return pGetInstance();
    }

    ID3D11DeviceContext* PCDX11RenderDevice::GetDXContext()
    {
        return *(ID3D11DeviceContext**)((BYTE*)this + 0x16166C);
    }
}
