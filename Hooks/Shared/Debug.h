#pragma once

class Debug
{
public:
    template<typename... TArgs>
    static void Log(const std::format_string<TArgs...> format, TArgs&&... args)
    {
#if _DEBUG
        std::string msg = std::vformat(format.get(), std::make_format_args(args...));
        msg = std::string("Hook DLL: ") + msg;
        OutputDebugStringA(msg.c_str());
#endif
    }
};
