#pragma once

template <typename T>
class mem_func_ptr
{
public:
    mem_func_ptr(void* ptr)
    {
        _value.voidPtr = ptr;
    }

    operator T() const
    {
        return _value.memFuncPtr;
    }

private:
    union
    {
        void* voidPtr;
        T memFuncPtr;
    } _value;
};
