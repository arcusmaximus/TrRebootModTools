#include "pch.h"

#if TR_VERSION >= 10

using namespace std;

namespace Tr
{
    void PCDX12StaticConstantBuffer::SetConstants(span<Vec4> values)
    {
        if (values.size() != _count)
            throw exception();

        PCDX12UploadPool* pUploadPool = PCDX12RenderDevice::GetInstance()->GetUploadPool();
        pUploadPool->ExecuteBufferUpload(_pGpuNodeResourceSet, values.data(), _sizeInBytes);
    }
}

#endif
