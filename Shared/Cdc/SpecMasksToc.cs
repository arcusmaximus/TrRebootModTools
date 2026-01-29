using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace TrRebootTools.Shared.Cdc
{
    internal class SpecMasksToc
    {
        public SpecMasksToc(ulong unsupportedMask = 0xC7FFF1C00FFF0400)
        {
            UnsupportedMask = unsupportedMask;
        }

        public static SpecMasksToc Load(string filePath)
        {
            using StreamReader reader = new(filePath);
            string? line = reader.ReadLine();
            if (line == null)
                throw new InvalidDataException($"{filePath} is empty");

            ulong unsupportedMask = ulong.Parse(line, NumberStyles.AllowHexSpecifier);
            SpecMasksToc toc = new(unsupportedMask);
            
            while ((line = reader.ReadLine()) != null)
            {
                int spacePos = line.IndexOf(' ');
                if (spacePos < 0)
                    continue;

                ulong localeMask = ulong.Parse(line.Substring(0, spacePos), NumberStyles.AllowHexSpecifier);
                string archiveFileName = line.Substring(spacePos + 1);
                toc.Entries[localeMask] = archiveFileName;
            }

            return toc;
        }

        public ulong UnsupportedMask
        {
            get;
            set;
        }

        public Dictionary<ulong, string> Entries
        {
            get;
        } = new();

        public void Write(string filePath)
        {
            using Stream stream = File.Create(filePath);
            using StreamWriter writer = new StreamWriter(stream);
            writer.WriteLine(UnsupportedMask.ToString("x016"));
            foreach ((ulong localeMask, string archiveFileName) in Entries)
            {
                writer.WriteLine($"{localeMask:x016} {archiveFileName}");
            }
        }

        public static string GetFilePathForArchive(string baseArchiveFilePath)
        {
            string specMasksName = Path.GetFileName(baseArchiveFilePath);
            if (specMasksName == "bigfile.000.tiger")
            {
                specMasksName = "game";
            }
            else
            {
                specMasksName = Regex.Replace(specMasksName, @"^bigfile\.", "");
                specMasksName = Regex.Replace(specMasksName, @"\.\d{3}\.\d{3}\.tiger$", "");
            }

            return Path.Combine(Path.GetDirectoryName(baseArchiveFilePath)!, specMasksName + ".specmasks.toc");
        }
    }
}
