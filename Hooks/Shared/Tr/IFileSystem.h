#pragma once

namespace Tr
{
    class IFileSystem
    {
    public:
        virtual void* CreateRequest(void* pReceiver, const char* pszPath, int state) = 0;
        virtual void* OpenFile(const char* pszPath) = 0;
        virtual bool FileExists(const char* pszPath) = 0;
        virtual DWORD GetFileSize(const char* pszPath) = 0;
        virtual FILETIME GetFileDate(const char* pszPath) = 0;
        virtual IFileSystem* GetRespondingFS(const char* pszPath) = 0;
        virtual void SetGameLocale(Locale_t pLocale) = 0;
        virtual Locale_t GetGameLocale() = 0;
        virtual void EnsureEvents(void* pEventManager) = 0;
        virtual bool HasActiveRequests() = 0;

#if TR_VERSION == 11
        virtual bool HasPrimaryActiveRequests() = 0;
#endif

        virtual void ProcessRequests() = 0;
        virtual void ProcessUntilAllRequestsComplete() = 0;
        virtual void ProcessUntilRequestComplete(void* pRequest) = 0;
        virtual void AddSynchronizeBarrier() = 0;
        virtual void Suspend() = 0;
        virtual bool Resume() = 0;
        virtual bool IsSuspended() = 0;
        virtual BYTE* GetBufferPointer(void* pRequest, int* pReadBufferOffset) = 0;
        virtual void PushMode(int mode) = 0;
        virtual void PopMode(int mode) = 0;
        virtual bool InMode(int mode, void*) = 0;

#if TR_VERSION == 11
        virtual DWORD GetActiveRequestsTotalBytesRead() = 0;
        virtual void* GetSourceDiskForPath(const char* pszPath, char* pszTransformedPath) = 0;
#endif

        virtual ~IFileSystem() {}
    };
}
