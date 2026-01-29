using Avalonia;
using System;
using TrRebootTools.Shared;

namespace TrRebootTools.SoundConverter
{
    internal static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            App.Run(() => App.ShowDialogAsync(new MainWindow()));
            return 0;
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return App.Build(() => new MainWindow());
        }
    }
}
