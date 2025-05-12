#include "pch.h"

#if TR_VERSION == 9

using namespace std;

namespace Tr
{
    void PCDX11StaticConstantBuffer::SetConstants(span<Vec4> values)
    {
        if (values.size() > _count)
            throw exception();

        ID3D11DeviceContext* pContext = PCDX11RenderDevice::GetInstance()->GetDXContext();

        D3D11_MAPPED_SUBRESOURCE mappedResource;
        pContext->Map(_pDXBuffer, 0, D3D11_MAP_WRITE_DISCARD, 0, &mappedResource);
        memcpy(mappedResource.pData, values.data(), values.size() * sizeof(Vec4));
        pContext->Unmap(_pDXBuffer, 0);
    }
}

#endif
