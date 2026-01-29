using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TrRebootTools.Shared.Cdc;

namespace TrRebootTools.HookTool
{
    internal class GameProcess : IDisposable
    {
        private record HookInfo(Version ExpectedGameVersion, string DllName);

        private static readonly Dictionary<CdcGame, HookInfo> HookInfos =
            new()
            {
                { CdcGame.Tr2013, new HookInfo(new Version(1, 1, 0, 0),   "Tr2013Hook.dll") },
                { CdcGame.Rise,   new HookInfo(new Version(1, 0, 0, 0),   "RottrHook.dll") },
                { CdcGame.Shadow, new HookInfo(new Version(1, 0, 492, 0), "SottrHook.dll") }
            };

        private readonly HookInfo _hookInfo;
        private readonly string _exePath;
        private readonly CancellationTokenSource _cancellationTokenSource;

        private Task? _eventListenTask;

        public static bool SupportsHooking(string gameExePath, CdcGame game, [NotNullWhen(false)] out string? unsupportedReason)
        {
            HookInfo? hookInfo = HookInfos.GetValueOrDefault(game);
            if (hookInfo == null)
            {
                unsupportedReason = $"Hooking is not supported for {CdcGameInfo.Get(game).ShortName}.";
                return false;
            }

            if (!File.Exists(gameExePath))
            {
                unsupportedReason = $"The game was not found at {gameExePath}";
                return false;
            }

            FileVersionInfo gameVersionInfo = FileVersionInfo.GetVersionInfo(gameExePath);
            Version gameVersion = new Version(gameVersionInfo.FileMajorPart, gameVersionInfo.FileMinorPart, gameVersionInfo.FileBuildPart, gameVersionInfo.FilePrivatePart);
            if (gameVersion != hookInfo.ExpectedGameVersion)
            {
                unsupportedReason = $"Your {CdcGameInfo.Get(game).ShortName} installation has version {gameVersion}, which does not match the expected version {hookInfo.ExpectedGameVersion}.";
                return false;
            }

            string hookDllPath = GetHookDllPath(hookInfo);
            if (!File.Exists(hookDllPath))
            {
                unsupportedReason = $"The hook DLL was not found at {hookDllPath}";
                return false;
            }

            unsupportedReason = null;
            return true;
        }

        private static string GetHookDllPath(HookInfo hookInfo)
        {
            return Path.Combine(AppContext.BaseDirectory, hookInfo.DllName);
        }

        public GameProcess(string exePath, CdcGame game)
        {
            if (!SupportsHooking(exePath, game, out string? unsupportedReason))
                throw new NotSupportedException(unsupportedReason);

            _hookInfo = HookInfos[game];
            _exePath = exePath;
            _cancellationTokenSource = new CancellationTokenSource();

            Game = game;
            try
            {
                Events = new NotificationChannel();
                Commands = new CommandChannel();
            }
            catch (IOException)
            {
                throw new Exception("Failed to create game communication channel. If the game is already running, please close it first.");
            }
        }

        public void Start()
        {
            IntPtr hProcess = DllInjector.CreateProcessWithDll(
                _exePath,
                null,
                Path.GetDirectoryName(_exePath)!,
                GetHookDllPath(_hookInfo)
            );
            ThreadPool.RegisterWaitForSingleObject(new ProcessWaitHandle(hProcess), (state, timedOut) => Exited?.Invoke(), null, -1, true);
            _eventListenTask = Task.Run(() => Events.Listen(_cancellationTokenSource.Token));
        }

        public CdcGame Game
        {
            get;
        }

        public NotificationChannel Events
        {
            get;
        }

        public CommandChannel Commands
        {
            get;
        }

        public event Action? Exited;

        public void Dispose()
        {
            if (_eventListenTask != null)
            {
                _cancellationTokenSource.Cancel();
                _eventListenTask.Wait();
                _eventListenTask = null;
            }
            Events.Dispose();
            Commands.Dispose();
        }

        private class ProcessWaitHandle : WaitHandle
        {
            public ProcessWaitHandle(IntPtr handle)
            {
                SafeWaitHandle = new SafeWaitHandle(handle, true);
            }
        }
    }
}
