using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared
{
    public partial class App : Application
    {
        internal static readonly Stack<Window> WindowStack = [];

        public static AppBuilder Build(Func<Window> createWindow)
        {
            return AppBuilder.Configure(() => new App(createWindow))
                             .UsePlatformDetect();
        }

        public static void Run(Func<Task> func)
        {
            Run(
                async () =>
                {
                    await func();
                    return 0;
                }
            );
        }

        public static T? Run<T>(Func<Task<T>> func)
        {
            AppDomain.CurrentDomain.UnhandledException += LogUnhandledException;

            T? result = default;
            ExceptionDispatchInfo? exception = null;
            AppBuilder builder = Build(CreateDummyWindow);
            builder.StartWithClassicDesktopLifetime([]);
            exception?.Throw();
            return result;

            Window CreateDummyWindow()
            {
                Window window = new()
                                {
                                    Width = 1,
                                    Height = 1,
                                    ShowInTaskbar = false,
                                    SystemDecorations = SystemDecorations.None
                                };
                WindowStack.Push(window);
                window.Loaded += OnWindowLoaded;
                return window;
            }

            async void OnWindowLoaded(object? sender, RoutedEventArgs e)
            {
                Window window = WindowStack.Peek();
                try
                {
                    window.Hide();
                    result = await func();
                }
                catch (Exception ex)
                {
                    await MessageBox.ShowErrorAsync(ex);
                }
                window.Close();
                WindowStack.Pop();
            }
        }

        public static async Task ShowDialogAsync(Window window)
        {
            Window owner = WindowStack.Peek();
            WindowStack.Push(window);
            if (owner.IsVisible)
            {
                await window.ShowDialog(owner);
            }
            else
            {
                TaskCompletionSource completion = new();
                window.Closed += (s, e) => completion.SetResult();
                window.Show();
                await completion.Task;
            }
            WindowStack.Pop();
        }

        public static async Task<string?> OpenFilePickerAsync(string title, Dictionary<string, string[]> patterns)
        {
            List<string> filePaths = await OpenFilesPickerAsync(title, patterns, false);
            return filePaths.Count == 1 ? filePaths[0] : null;
        }

        public static async Task<List<string>> OpenFilesPickerAsync(string title, Dictionary<string, string[]> patterns)
        {
            return await OpenFilesPickerAsync(title, patterns, true);
        }

        private static async Task<List<string>> OpenFilesPickerAsync(string title, Dictionary<string, string[]> patterns, bool allowMultiple)
        {
            Window owner = WindowStack.Peek();
            IReadOnlyList<IStorageFile> files = await owner.StorageProvider.OpenFilePickerAsync(
                new()
                {
                    Title = title,
                    FileTypeFilter = patterns.Select(p => new FilePickerFileType(p.Key) { Patterns = p.Value }).ToList(),
                    AllowMultiple = allowMultiple
                }
            );
            List<string> filePaths = [];
            foreach (IStorageFile file in files)
            {
                string? filePath = file.TryGetLocalPath();
                if (filePath != null)
                    filePaths.Add(filePath);
            }
            return filePaths;
        }

        public static async Task<string?> SaveFilePickerAsync(string title, Dictionary<string, string[]> patterns)
        {
            Window owner = WindowStack.Peek();
            IStorageFile? file = await owner.StorageProvider.SaveFilePickerAsync(
                new()
                {
                    Title = title,
                    FileTypeChoices = patterns.Select(p => new FilePickerFileType(p.Key) { Patterns = p.Value }).ToList()
                }
            );
            return file?.TryGetLocalPath();
        }

        public static async Task<string?> OpenFolderPickerAsync(string title)
        {
            Window owner = WindowStack.Peek();
            IReadOnlyList<IStorageFolder> folders = await owner.StorageProvider.OpenFolderPickerAsync(
                new()
                {
                    Title = title
                }
            );
            if (folders.Count != 1)
                return null;

            return folders[0].TryGetLocalPath();
        }

        public static IClipboard? Clipboard => WindowStack.Peek().Clipboard;

        private readonly Func<Window> _createWindow;

        public App()
        {
        }

        public App(Func<Window> createWindow)
        {
            _createWindow = createWindow;
        }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = _createWindow();

            base.OnFrameworkInitializationCompleted();
        }

        private static void LogUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            using StreamWriter writer = new("error.log");
            writer.WriteLine(e.ExceptionObject.ToString());
        }
    }
}