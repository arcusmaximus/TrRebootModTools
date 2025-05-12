#pragma once

namespace Tr
{
    class MultiFileSystem : public IFileSystem
    {
    public:
        void AddChild(IFileSystem* pFS, bool inFront);
        void RemoveChild(IFileSystem* pFS);

        IFileSystem* GetChildByName(const char* pszName)
        {
            for (int i = 0; i < _numChildren; i++)
            {
                if (_strcmpi(_children[i].Name, pszName) == 0)
                    return _children[i].pFS;
            }
            return nullptr;
        }

    private:
        struct Child
        {
        public:
            IFileSystem* pFS;
            char Name[32];
            bool Arg;
        };

#if TR_VERSION == 11
        static constexpr int MaxChildren = 256; 
#else
        static constexpr int MaxChildren = 35;
#endif
        Child _children[MaxChildren];
        int _numChildren;
    };
}
