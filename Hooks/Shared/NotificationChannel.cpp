#include "pch.h"

NotificationChannel NotificationChannel::Instance;

NotificationChannel::NotificationChannel()
{
    _hAvailableEvent = OpenEvent(EVENT_MODIFY_STATE, FALSE, L"TrRebootHook_NotificationAvailableEvent");
    _hReceivedEvent = OpenEvent(SYNCHRONIZE, FALSE, L"TrRebootHook_NotificationReceivedEvent");
    _hBuffer = OpenFileMapping(FILE_MAP_WRITE, FALSE, L"TrRebootHook_NotificationBuffer");
    if (!_hAvailableEvent || !_hReceivedEvent || !_hBuffer)
        return;

    _hMutex = CreateMutex(nullptr, false, nullptr);
    _pBuffer = (BYTE*)MapViewOfFile(_hBuffer, FILE_MAP_WRITE, 0, 0, BufferSize);
}

NotificationChannel::~NotificationChannel()
{
    Close();
}

void NotificationChannel::NotifyGameEntered()
{
    BeginNotification(EventType::GameEntered);
    EndNotification();
}

void NotificationChannel::NotifyOpeningFile(QWORD nameHash, QWORD locale, const char* pszPath)
{
    BeginNotification(EventType::OpeningFile);
    Write(nameHash);
    Write(locale);
    Write(pszPath);
    EndNotification();
}

void NotificationChannel::NotifyPlayingAnimation(int id, const char* pszName)
{
    BeginNotification(EventType::PlayingAnimation);
    Write(id);
    Write(pszName);
    EndNotification();
}

void NotificationChannel::BeginNotification(EventType type)
{
    if (!_hAvailableEvent || !_hReceivedEvent || !_hMutex || !_hBuffer || !_pBuffer || WaitForSingleObject(_hMutex, 1000) != WAIT_OBJECT_0)
    {
        _pWritePos = nullptr;
        return;
    }

    _pWritePos = _pBuffer;
    *(_pWritePos++) = (BYTE)type;
}

void NotificationChannel::Write(int value)
{
    if (!_pWritePos || RemainingBufferSpace() < 4)
        return;

    *(int*)_pWritePos = value;
    _pWritePos += 4;
}

void NotificationChannel::Write(QWORD value)
{
    if (!_pWritePos || RemainingBufferSpace() < 8)
        return;

    *(QWORD*)_pWritePos = value;
    _pWritePos += 8;
}

void NotificationChannel::Write(const char* pszText)
{
    int textLength = strlen(pszText);
    int remainingSpace = RemainingBufferSpace();
    if (!_pWritePos || textLength + 1 > remainingSpace)
        return;

    strcpy_s((char*)_pWritePos, remainingSpace, pszText);
    _pWritePos += textLength + 1;
}

void NotificationChannel::EndNotification()
{
    if (!_pWritePos)
        return;

    SetEvent(_hAvailableEvent);

    if (WaitForSingleObject(_hReceivedEvent, 5000) != WAIT_OBJECT_0)
    {
        Close();
        return;
    }
    ReleaseMutex(_hMutex);
}

void NotificationChannel::Close()
{
    _pWritePos = nullptr;

    if (_pBuffer)
    {
        UnmapViewOfFile(_pBuffer);
        _pBuffer = nullptr;
    }

    if (_hBuffer)
    {
        CloseHandle(_hBuffer);
        _hBuffer = nullptr;
    }

    if (_hMutex)
    {
        CloseHandle(_hMutex);
        _hMutex = nullptr;
    }

    if (_hReceivedEvent)
    {
        CloseHandle(_hReceivedEvent);
        _hReceivedEvent = nullptr;
    }

    if (_hAvailableEvent)
    {
        CloseHandle(_hAvailableEvent);
        _hAvailableEvent = nullptr;
    }
}

int NotificationChannel::RemainingBufferSpace() const
{
    return _pBuffer + BufferSize - _pWritePos;
}
