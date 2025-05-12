#include "pch.h"

namespace Tr
{
    PCDX12RenderDevice* PCDX12RenderDevice::GetInstance()
    {
        return *(PCDX12RenderDevice**)(Game::ImageBase + 0x1A53E40);
    }

    PCDX12UploadPool* PCDX12RenderDevice::GetUploadPool() const
    {
        return *(PCDX12UploadPool**)((BYTE*)this + 0x2B490);
    }
}
