#include "pch.h"

namespace Tr
{
    typedef void ExecuteBufferUpload_t(
        PCDX12UploadPool* pThis,
        void* pGpuNodeResourceSet,
        int size,
        void* pData,
        D3D12_RESOURCE_STATES resourceUsage,
        int nodeBitmask
    );

    void PCDX12UploadPool::ExecuteBufferUpload(void* pGpuNodeResourceSet, void* pData, int size)
    {
        ExecuteBufferUpload_t* pFunc = (ExecuteBufferUpload_t*)(Game::ImageBase + 0x369E20);
        pFunc(this, pGpuNodeResourceSet, size, pData, D3D12_RESOURCE_STATE_VERTEX_AND_CONSTANT_BUFFER, -1);
    }
}
