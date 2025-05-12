#pragma once

#if TR_VERSION >= 10

namespace Tr
{
    class PCDX12UploadPool
    {
    public:
        void ExecuteBufferUpload(void* pGpuNodeResourceSet, void* pData, int size);
    };
}

#endif
