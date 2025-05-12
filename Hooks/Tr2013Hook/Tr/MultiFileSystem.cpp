#include "pch.h"

namespace Tr
{
    typedef void (MultiFileSystem::*AddChild_t)(IFileSystem* pFS, bool movable, bool inFront);
    typedef void (MultiFileSystem::* RemoveChild_t)(IFileSystem* pFS);

    void MultiFileSystem::AddChild(IFileSystem* pFS, bool inFront)
    {
        AddChild_t pFunc = mem_func_ptr<AddChild_t>(Game::ImageBase + 0x134900);
        (this->*pFunc)(pFS, false, inFront);
    }

    void MultiFileSystem::RemoveChild(IFileSystem* pFS)
    {
        RemoveChild_t pFunc = mem_func_ptr<RemoveChild_t>(Game::ImageBase + 0x1364D0);
        (this->*pFunc)(pFS);
    }
}
