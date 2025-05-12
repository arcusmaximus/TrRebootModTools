#pragma once

namespace Util
{
    struct CaseInsensitiveLess
    {
        bool operator()(const std::string& a, const std::string& b) const
        {
            return _strcmpi(a.c_str(), b.c_str()) < 0;
        }
    };
}
