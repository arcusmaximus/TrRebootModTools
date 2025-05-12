#include "pch.h"

namespace Tr
{
    PCDX12RenderDevice* PCDX12RenderDevice::GetInstance()
    {
        return *(PCDX12RenderDevice**)(Game::ImageBase + 0x2333A30);
    }

    PCDX12UploadPool* PCDX12RenderDevice::GetUploadPool() const
    {
        return *(PCDX12UploadPool**)((BYTE*)this + 0x4C988);
    }
}
