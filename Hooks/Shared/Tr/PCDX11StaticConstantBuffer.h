#pragma once

#if TR_VERSION == 9

namespace Tr
{
    class PCDX11StaticConstantBuffer
    {
    public:
        virtual ~PCDX11StaticConstantBuffer() = 0;

        int Size() const { return _count; }

        void SetConstants(std::span<Vec4> values);

    private:
        ID3D11Buffer* _pDXBuffer;
        Vec4* _pData;
        int _count;
        int _sizeInBytes;
    };
}

#endif
