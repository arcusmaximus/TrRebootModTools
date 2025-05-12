using System;
using System.IO.MemoryMappedFiles;
using System.IO;
using System.Threading;
using TrRebootTools.Shared.Util;
using TrRebootTools.Shared.Cdc;

namespace TrRebootTools.HookTool
{
    internal class CommandChannel : IDisposable
    {
        private readonly EventWaitHandle _availableEvent;
        private readonly EventWaitHandle _processedEvent;
        private readonly MemoryMappedFile _buffer;
        private readonly Stream _stream;
        private readonly BinaryWriter _writer;

        public CommandChannel()
        {
            _availableEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "TrRebootHook_CommandAvailableEvent");
            _processedEvent = new EventWaitHandle(true , EventResetMode.AutoReset, "TrRebootHook_CommandProcessedEvent");
            _buffer = MemoryMappedFile.CreateNew("TrRebootHook_CommandBuffer", 0x1000);
            _stream = _buffer.CreateViewStream();
            _writer = new BinaryWriter(_stream);
        }

        public void LoadNewArchives()
        {
            BeginCommand(CommandType.LoadNewArchives);
            EndCommand();
        }

        public void UnloadMissingArchives()
        {
            BeginCommand(CommandType.UnloadMissingArchives);
            EndCommand();
        }

        public void SetMaterialConstants(int materialId, int pass, ShaderType shaderType, Vec4[] values)
        {
            if (values == null || values.Length == 0)
                return;

            BeginCommand(CommandType.SetMaterialConstants);
            _writer.Write(materialId);
            _writer.Write(pass);
            _writer.Write((int)shaderType);
            _writer.Write(values.Length);
            foreach (Vec4 value in values)
            {
                _writer.Write(value.X);
                _writer.Write(value.Y);
                _writer.Write(value.Z);
                _writer.Write(value.W);
            }
            EndCommand();
        }

        public void ClearStoredMaterialConstants()
        {
            BeginCommand(CommandType.ClearStoredMaterialConstants);
            EndCommand();
        }

        private void BeginCommand(CommandType type)
        {
            if (!_processedEvent.WaitOne(5000))
                throw new Exception("Failed to send command");

            _stream.Position = 0;
            _writer.Write((byte)type);
        }

        private void EndCommand()
        {
            _availableEvent.Set();
        }

        private enum CommandType
        {
            None,
            LoadNewArchives,
            UnloadMissingArchives,
            SetMaterialConstants,
            ClearStoredMaterialConstants
        }

        public void Dispose()
        {
            _writer.Dispose();
            _stream.Dispose();
            _buffer.Dispose();
            _processedEvent.Dispose();
            _availableEvent.Dispose();
        }
    }
}
