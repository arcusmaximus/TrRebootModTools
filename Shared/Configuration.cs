using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrRebootTools.Shared.Cdc;

namespace TrRebootTools.Shared
{
    public class Configuration
    {
        public CdcGame? SelectedGame
        {
            get;
            set;
        }

        public Dictionary<CdcGame, string> GameFolderPaths
        {
            get;
            set;
        } = [];

        public string? ExtractionOutputFolder
        {
            get;
            set;
        }

        public Dictionary<string, string> ExtraSettings
        {
            get;
            set;
        } = new();

        public static Configuration Load()
        {
            string filePath = GetFilePath();
            if (!File.Exists(filePath))
                return new();

            using Stream stream = File.OpenRead(filePath);
            return JsonSerializer.Deserialize(stream, ConfigurationSerializerContext.Default.Configuration) ?? new();
        }

        public void Save()
        {
            using Stream stream = File.Create(GetFilePath());
            JsonSerializer.Serialize(stream, this, ConfigurationSerializerContext.Default.Configuration);
        }

        private static string GetFilePath()
        {
            if (OperatingSystem.IsLinux())
            {
                string folderPath = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ??
                                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
                Directory.CreateDirectory(folderPath);
                return Path.Combine(folderPath, "tr-reboot-tools.json");
            }
            else
            {
                return Path.Combine(AppContext.BaseDirectory, "settings.json");
            }
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(Configuration))]
    internal partial class ConfigurationSerializerContext : JsonSerializerContext { }
}
