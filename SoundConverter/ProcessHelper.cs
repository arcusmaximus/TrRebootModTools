using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace TrRebootTools.SoundConverter
{
    internal static class ProcessHelper
    {
        public static async Task<string> RunAsync(string processPath, string arguments)
        {
            using Process process =
                new()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = processPath,
                        Arguments = arguments,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    },
                    EnableRaisingEvents = true
                };

            StringBuilder log = new();
            process.OutputDataReceived += (s, e) => log.AppendLine(e.Data);
            process.ErrorDataReceived += (s, e) => log.AppendLine(e.Data);

            TaskCompletionSource<object> source = new();
            process.Exited += (s, e) => source.SetResult(null);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await source.Task;

            return log.ToString();
        }
    }
}
