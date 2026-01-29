using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.HookTool
{
    internal class NotificationChannel : IDisposable
    {
        private readonly EventWaitHandle _availableEvent;
        private readonly EventWaitHandle _receivedEvent;
        private readonly MemoryMappedFile _buffer;
        private readonly Stream _stream;
        private readonly BinaryReader _reader;

        public NotificationChannel()
        {
            _availableEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "TrRebootHook_NotificationAvailableEvent");
            _receivedEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "TrRebootHook_NotificationReceivedEvent");
            _buffer = MemoryMappedFile.CreateNew("TrRebootHook_NotificationBuffer", 0x1000);
            _stream = _buffer.CreateViewStream();
            _reader = new BinaryReader(_stream);
        }

        public void Listen(CancellationToken cancellationToken)
        {
            Action[] handlers = [
                RaiseGameEntered,
                RaiseOpeningFile,
                RaisePlayingAnimation
            ];

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!_availableEvent.WaitOne(5000))
                        continue;
                }
                catch
                {
                    break;
                }

                _stream.Position = 0;
                int type = _reader.ReadByte();
                handlers[type]();
            }
        }

        private void RaiseGameEntered()
        {
            _receivedEvent.Set();
            GameEntered?.Invoke();
        }

        private void RaiseOpeningFile()
        {
            ulong nameHash = _reader.ReadUInt64();
            ulong locale = _reader.ReadUInt64();
            string path = _reader.ReadZeroTerminatedString();
            _receivedEvent.Set();
            OpeningFile?.Invoke(new ArchiveFileKey(nameHash, locale), path);
        }

        private void RaisePlayingAnimation()
        {
            int id = _reader.ReadInt32();
            string name = _reader.ReadZeroTerminatedString();
            _receivedEvent.Set();
            PlayingAnimation?.Invoke(id, name);
        }

        public event Action? GameEntered;

        public delegate void OpeningFileHandler(ArchiveFileKey key, string path);
        public event OpeningFileHandler? OpeningFile;

        public delegate void PlayingAnimationHandler(int id, string name);
        public event PlayingAnimationHandler? PlayingAnimation;

        public void Dispose()
        {
            _reader.Dispose();
            _stream.Dispose();
            _buffer.Dispose();
            _receivedEvent.Dispose();
            _availableEvent.Dispose();
        }
    }
}
