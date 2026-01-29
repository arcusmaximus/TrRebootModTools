using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace TrRebootTools.Shared
{
    public static class ProcessHelper
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

            TaskCompletionSource<object?> source = new();
            process.Exited += (s, e) => source.SetResult(null);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await source.Task;

            return log.ToString();
        }

        public static async Task<string> RunVgmStreamAsync(string inputFilePath, string outputFilePath, bool throwOnFailure)
        {
            string vgmstreamPath = OperatingSystem.IsWindows() ? Path.Combine(AppContext.BaseDirectory, "vgmstream", "vgmstream-cli.exe")
                                                               : Path.Combine(AppContext.BaseDirectory, "vgmstream-cli");
            if (!File.Exists(vgmstreamPath))
            {
                if (throwOnFailure)
                    throw new FileNotFoundException($"{vgmstreamPath} not found");
                else
                    return string.Empty;
            }

            if (inputFilePath.StartsWith(@"\\?\"))
                inputFilePath = inputFilePath.Substring(4);

            string messages = await RunAsync(vgmstreamPath, $"-i -o \"{outputFilePath}\" \"{inputFilePath}\"");

            if (throwOnFailure && !File.Exists(outputFilePath))
                throw new Exception($"Failed to convert {inputFilePath} to {outputFilePath}");

            return messages;
        }
    }
}
