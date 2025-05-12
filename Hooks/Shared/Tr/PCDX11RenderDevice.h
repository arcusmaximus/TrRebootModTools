#pragma once

#if TR_VERSION == 9

namespace Tr
{
    class PCDX11RenderDevice
    {
    public:
        static PCDX11RenderDevice* GetInstance();

        ID3D11DeviceContext* GetDXContext();
    };
}

#endif
