#include "pch.h"
#include "CommandChannel.h"

using namespace std;
using namespace Tr;

CommandChannel CommandChannel::Instance;

CommandChannel::CommandChannel()
{
    _hAvailableEvent = OpenEvent(SYNCHRONIZE, FALSE, L"TrRebootHook_CommandAvailableEvent");
    _hProcessedEvent = OpenEvent(EVENT_MODIFY_STATE, FALSE, L"TrRebootHook_CommandProcessedEvent");
    _hBuffer = OpenFileMapping(FILE_MAP_READ, FALSE, L"TrRebootHook_CommandBuffer");
    if (!_hAvailableEvent || !_hProcessedEvent || !_hBuffer)
        return;

    _pBuffer = (BYTE*)MapViewOfFile(_hBuffer, FILE_MAP_READ, 0, 0, BufferSize);
}

CommandChannel::~CommandChannel()
{
    _pReadPos = nullptr;

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

    if (_hProcessedEvent)
    {
        CloseHandle(_hProcessedEvent);
        _hProcessedEvent = nullptr;
    }

    if (_hAvailableEvent)
    {
        CloseHandle(_hAvailableEvent);
        _hAvailableEvent = nullptr;
    }
}

void CommandChannel::HandlePendingCommand()
{
    CommandType type = ReadCommandType();
    if (type == CommandType::None)
        return;

    switch (type)
    {
        case CommandType::LoadNewArchives:
            ArchiveSet::GetInstance()->LoadNewArchivesAsync();
            break;

        case CommandType::UnloadMissingArchives:
            ArchiveSet::GetInstance()->UnloadMissingArchivesAsync();
            break;

        case CommandType::SetMaterialConstants:
            HandleSetMaterialConstants();
            break;

        case CommandType::ClearStoredMaterialConstants:
            MaterialConstantStore::Instance.Clear();
            break;
    }
    SetEvent(_hProcessedEvent);
}

void CommandChannel::HandleSetMaterialConstants()
{
    int materialId = ReadInt();
    int pass = ReadInt();
    ShaderType shaderType = (ShaderType)ReadInt();
    span<Vec4> values = ReadArray<Vec4>();

    MaterialConstantStore::Instance.Add(materialId, pass, shaderType, values);

    CommonMaterial* pMaterial = CommonMaterial::GetById(materialId);
    if (pMaterial)
        pMaterial->SetConstants(pass, shaderType, values);
}

CommandChannel::CommandType CommandChannel::ReadCommandType()
{
    if (!_hAvailableEvent || WaitForSingleObject(_hAvailableEvent, 0) != WAIT_OBJECT_0)
        return CommandType::None;

    _pReadPos = _pBuffer;
    if (!_pBuffer)
        return CommandType::None;

    return (CommandType)*(_pReadPos++);
}

int CommandChannel::ReadInt()
{
    if (RemainingBufferSpace() < 4)
        return 0;

    int result = *(int*)_pReadPos;
    _pReadPos += 4;
    return result;
}

template<typename T>
span<T> CommandChannel::ReadArray()
{
    int count = ReadInt();
    if (count < 0 || RemainingBufferSpace() < count * sizeof(T))
        throw std::exception();

    span<T> result((T*)_pReadPos, (T*)_pReadPos + count);
    _pReadPos += count * sizeof(T);
    return result;
}

string CommandChannel::ReadString()
{
    BYTE* pStart = _pReadPos;
    while (*_pReadPos)
    {
        _pReadPos++;
    }
    string result((char*)pStart, _pReadPos - pStart);
    _pReadPos++;
    return result;
}

int CommandChannel::RemainingBufferSpace() const
{
    if (!_pReadPos)
        return 0;

    return _pBuffer + BufferSize - _pReadPos;
}

