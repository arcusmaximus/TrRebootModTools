using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.IO;
using System.Reflection;

namespace TrRebootTools.Shared.Util
{
    public static class TypedAssetLoader
    {
        public static Bitmap LoadProgramBitmap(string path)
        {
            return LoadBitmap(Assembly.GetEntryAssembly()!, path);
        }

        public static Bitmap LoadSharedBitmap(string path)
        {
            return LoadBitmap(Assembly.GetExecutingAssembly(), path);
        }

        private static Bitmap LoadBitmap(Assembly assembly, string path)
        {
            Uri uri = new("avares://" + assembly.GetName().Name + path);
            using Stream stream = AssetLoader.Open(uri);
            return new Bitmap(stream);
        }
    }
}
