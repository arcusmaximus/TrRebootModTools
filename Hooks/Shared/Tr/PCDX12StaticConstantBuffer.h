#pragma once

#if TR_VERSION >= 10

namespace Tr
{
    class PCDX12StaticConstantBuffer
    {
    public:
        virtual ~PCDX12StaticConstantBuffer() = 0;
        virtual void Finalize() = 0;
        virtual Vec4* Map() = 0;
        virtual void Unmap() = 0;
        virtual void Free() = 0;
        virtual bool IsReady() = 0;

        int Size() const { return _count; }

        void SetConstants(std::span<Vec4> values);

    private:
        Vec4* _pData;
        int _count;
        int _sizeInBytes;
        void* _hGpuNodeDescriptors[4];
        QWORD _offsetInAllocatedBuffer;
        int _roundedSizeInBytes;
        int field_44;
        RenderAsyncCreateResource _asyncCreateResource;
        void* _pGpuNodeResourceSet;
    };
}

#endif
