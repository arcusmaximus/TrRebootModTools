#pragma once

#if TR_VERSION >= 10

namespace Tr
{
    class PCDX12RenderDevice
    {
    public:
        static PCDX12RenderDevice* GetInstance();

        PCDX12UploadPool* GetUploadPool() const;
    };
}

#endif
