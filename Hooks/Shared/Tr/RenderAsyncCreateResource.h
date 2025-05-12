#pragma once

namespace Tr
{
    class RenderAsyncCreateResource
    {
    public:
        virtual void Enqueue(void* param)
        {
        }

    private:
        int field_8;
        int field_C;
        void* _pQueue;
        bool _enqueued;
    };
};
