#pragma once

namespace Tr
{
    class MSFileSystemFile
    {
    public:
        virtual void* CreateRequest(void* pReceiver, const char* pszPath, DWORD filePos) = 0;

        const char* GetPath() const
        {
            return szPath;
        }
        
        void Close()
        {
            if (_hFile == (decltype(_hFile))INVALID_HANDLE_VALUE)
                return;

            CloseHandle((HANDLE)_hFile);
            _hFile = (decltype(_hFile))INVALID_HANDLE_VALUE;
        }

    private:
        IFileSystem* _pFS;
        void* _pSourceDisk;
#if TR_VERSION == 10
        DWORD _hFile;
#else
        HANDLE _hFile;
#endif
        char szPath[256];
        void* pRequest;
        MSFileSystemFile* _pNextOpenFile;
        MSFileSystemFile* _pPrevOpenFile;
    };
}
