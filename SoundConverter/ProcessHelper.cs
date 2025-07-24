using System.Diagnostics;
using System.Threading.Tasks;

namespace TrRebootTools.SoundConverter
{
    internal static class ProcessHelper
    {
        public static async Task RunAsync(string processPath, string arguments)
        {
            using Process process =
                new()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = processPath,
                        Arguments = arguments,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    },
                    EnableRaisingEvents = true
                };
            TaskCompletionSource<object> source = new();
            process.Exited += (s, e) => source.SetResult(null);
            process.Start();
            await source.Task;
        }
    }
}
