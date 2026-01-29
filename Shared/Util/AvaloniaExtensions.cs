using Avalonia.Input;
using Avalonia.Platform.Storage;
using System.Collections.Generic;

namespace TrRebootTools.Shared.Util
{
    public static class AvaloniaExtensions
    {
        public static IEnumerable<string> GetFileSystemPaths(this IDataTransfer transfer)
        {
            foreach (IDataTransferItem item in transfer.GetItems(DataFormat.File))
            {
                string? path = item.TryGetValue(DataFormat.File)?.TryGetLocalPath();
                if (path != null)
                    yield return path;
            }
        }
    }
}
