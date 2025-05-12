#include "pch.h"

namespace Tr
{
    struct DataRange
    {
        void* Data;
        UINT64 Size;
    };

    typedef void ExecuteBufferUpload_t(
        PCDX12UploadPool* pThis,
        void* pGpuNodeResourceSet,
        D3D12_RESOURCE_DESC* pResourceDesc,
        DataRange* pData,
        D3D12_RESOURCE_STATES resourceUsage,
        int nodeBitmask
    );

    void PCDX12UploadPool::ExecuteBufferUpload(void* pGpuNodeResourceSet, void* pData, int size)
    {
        D3D12_RESOURCE_DESC resourceDesc;
        resourceDesc.Dimension = D3D12_RESOURCE_DIMENSION_BUFFER;
        resourceDesc.Alignment = 0;
        resourceDesc.Width = size;
        resourceDesc.Height = 1;
        resourceDesc.DepthOrArraySize = 1;
        resourceDesc.MipLevels = 1;
        resourceDesc.Format = DXGI_FORMAT_UNKNOWN;
        resourceDesc.SampleDesc.Count = 1;
        resourceDesc.SampleDesc.Quality = 0;
        resourceDesc.Layout = D3D12_TEXTURE_LAYOUT_ROW_MAJOR;
        resourceDesc.Flags = D3D12_RESOURCE_FLAG_DENY_SHADER_RESOURCE;

        DataRange range;
        range.Data = pData;
        range.Size = size;

        ExecuteBufferUpload_t* pFunc = (ExecuteBufferUpload_t*)(Game::ImageBase + 0x309670);
        pFunc(this, pGpuNodeResourceSet, &resourceDesc, &range, D3D12_RESOURCE_STATE_VERTEX_AND_CONSTANT_BUFFER, -1);
    }
}
