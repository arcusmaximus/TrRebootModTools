using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace TrRebootTools.Shared.Cdc
{
    public class ArchiveMetaData
    {
        private readonly string _enabledFilePath;

        private ArchiveMetaData(string filePath)
        {
            FilePath = filePath;
            _enabledFilePath = Regex.Replace(filePath, @"\.disabled", "");
        }

        public static ArchiveMetaData Create(string filePath, int gameId, int version, int packageId, int dlcIndex)
        {
            ArchiveMetaData metaData = new ArchiveMetaData(filePath)
                                       {
                                           Name = Path.GetFileNameWithoutExtension(filePath)
                                                      .Replace("bigfile.", "")
                                                      .Replace(".000", ""),
                                           GameId = gameId,
                                           Version = version,
                                           PackageId = packageId,
                                           DlcIndex = dlcIndex
                                       };
            metaData.Save();
            return metaData;
        }

        public string FilePath
        {
            get;
            private set;
        }

        public bool Enabled
        {
            get { return File.Exists(_enabledFilePath); }
            set
            {
                string disabledFilePath = _enabledFilePath + ".disabled";
                if (value)
                {
                    if (File.Exists(disabledFilePath))
                    {
                        File.Move(disabledFilePath, _enabledFilePath);
                        FilePath = _enabledFilePath;
                    }
                }
                else
                {
                    if (File.Exists(_enabledFilePath))
                    {
                        File.Move(_enabledFilePath, disabledFilePath);
                        FilePath = disabledFilePath;
                    }
                }
            }
        }

        public static ArchiveMetaData Load(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Archive metadata file {filePath} does not exist");

            ArchiveMetaData metaData = new ArchiveMetaData(filePath);

            using StreamReader reader = new StreamReader(filePath);
            string? line = reader.ReadLine();
            if (line == null)
                throw new InvalidDataException($"Archive metadata file {filePath} is empty");

            string[] parts = line.Split(' ');
            metaData.Name = parts[0];
            for (int i = 1; i < parts.Length; i += 2)
            {
                string key = parts[i];
                int value = int.Parse(parts[i + 1]);
                metaData.SetProperty(key, value);
            }

            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                int spaceIdx = line.IndexOf(' ');
                if (spaceIdx < 0)
                    continue;

                string key = line.Substring(0, spaceIdx);
                string value = line.Substring(spaceIdx + 1);
                switch (key)
                {
                    case "feature":
                        metaData.Features.Add(value);
                        break;

                    case "license":
                        metaData.Licenses.Add(value);
                        break;

                    case "custom":
                        metaData.CustomEntries.Add(value);
                        break;

                    default:
                        throw new InvalidDataException($"Unrecognized .nfo entry: {key}");
                }
            }

            return metaData;
        }

        public void Save()
        {
            using Stream stream = File.Create(FilePath);
            using StreamWriter writer = new StreamWriter(stream);

            writer.Write(Name);
            foreach ((string propertyName, int propertyValue) in GetProperties())
            {
                writer.Write($" {propertyName} {propertyValue}");
            }
            writer.WriteLine();

            foreach (string feature in Features)
            {
                writer.WriteLine($"feature {feature}");
            }

            foreach (string license in Licenses)
            {
                writer.WriteLine($"license {license}");
            }

            foreach (string custom in CustomEntries)
            {
                writer.WriteLine($"custom {custom}");
            }
        }

        public string? Name
        {
            get;
            private set;
        }

        public int GameId
        {
            get;
            set;
        }

        public int PackageId
        {
            get;
            set;
        }

        public int Chunk
        {
            get;
            set;
        }

        public int Version
        {
            get;
            set;
        }

        public bool Required
        {
            get;
            set;
        } = true;

        public bool BigFile
        {
            get;
            set;
        } = true;

        public int DlcIndex
        {
            get;
            set;
        }

        public bool Mount
        {
            get;
            set;
        }

        public bool Unmount
        {
            get;
            set;
        } = true;

        public List<string> Features
        {
            get;
        } = [];

        public List<string> Licenses
        {
            get;
        } = [];

        public List<string> CustomEntries
        {
            get;
        } = [];

        private IEnumerable<(string, int)> GetProperties()
        {
            yield return ("bap", GameId);
            yield return ("package", PackageId);
            yield return ("chunk", Chunk);
            yield return ("version", Version);
            yield return ("required", Convert.ToInt32(Required));
            yield return ("bigfile", Convert.ToInt32(BigFile));
            yield return ("dlcindex", DlcIndex);
            yield return ("mount", Convert.ToInt32(Mount));
            yield return ("unmount", Convert.ToInt32(Unmount));
        }

        private void SetProperty(string name, int value)
        {
            switch (name)
            {
                case "bap":
                    GameId = value;
                    break;

                case "package":
                    PackageId = value;
                    break;

                case "chunk":
                    Chunk = value;
                    break;

                case "version":
                    Version = value;
                    break;

                case "required":
                    Required = Convert.ToBoolean(value);
                    break;

                case "bigfile":
                    BigFile = Convert.ToBoolean(value);
                    break;

                case "dlcindex":
                    DlcIndex = value;
                    break;

                case "mount":
                    Mount = Convert.ToBoolean(value);
                    break;

                case "unmount":
                    Unmount = Convert.ToBoolean(value);
                    break;

                default:
                    throw new ArgumentException($"Unrecognized .nfo property name \"{name}\"");
            }
        }

        public override string ToString()
        {
            return Name ?? FilePath;
        }
    }
}
