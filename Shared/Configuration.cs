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
        private const string FileName = "settings.json";

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
            return Path.Combine(AppContext.BaseDirectory, FileName);
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(Configuration))]
    internal partial class ConfigurationSerializerContext : JsonSerializerContext { }
}
