#pragma once

class CommandChannel
{
public:
    CommandChannel();
    ~CommandChannel();

    void HandlePendingCommand();

    static CommandChannel Instance;

private:
    enum class CommandType
    {
        None,
        LoadNewArchives,
        UnloadMissingArchives,
        SetMaterialConstants,
        ClearStoredMaterialConstants
    };

    void HandleSetMaterialConstants();

    CommandType ReadCommandType();
    int ReadInt();
    template<typename T> std::span<T> ReadArray();
    std::string ReadString();

    int RemainingBufferSpace() const;

    HANDLE _hAvailableEvent = nullptr;
    HANDLE _hProcessedEvent = nullptr;

    HANDLE _hBuffer = nullptr;
    BYTE* _pBuffer = nullptr;
    BYTE* _pReadPos = nullptr;

    const int BufferSize = 0x1000;
};
