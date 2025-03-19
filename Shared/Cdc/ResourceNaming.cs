using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TrRebootTools.Shared.Cdc.Rise;
using TrRebootTools.Shared.Cdc.Shadow;
using TrRebootTools.Shared.Cdc.Tr2013;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Cdc
{
    public abstract class ResourceNaming
    {
        private static readonly Dictionary<CdcGame, ResourceNaming> Instances =
            new Dictionary<CdcGame, ResourceNaming>
            {
                { CdcGame.Tr2013, new Tr2013ResourceNaming() },
                { CdcGame.Rise, new RiseResourceNaming() },
                { CdcGame.Shadow, new ShadowResourceNaming() }
            };

        private static ResourceNaming For(CdcGame game)
        {
            return Instances[game];
        }

        public static (ResourceType, ResourceSubType) GetType(string filePath, CdcGame game)
        {
            return For(game).GetType(filePath);
        }

        public static bool TryGetResourceKey(string filePath, out ResourceKey resourceKey, CdcGame game)
        {
            return For(game).TryGetResourceKeyInstance(filePath, out resourceKey);
        }

        public static string GetExtension(ResourceType type, ResourceSubType subType, CdcGame game)
        {
            return For(game).GetExtension(type, subType);
        }

        public static string GetFileName(ArchiveSet archiveSet, ResourceReference resourceRef, bool useOriginalFilePath = true)
        {
            return For(archiveSet.Game).GetFileNameInstance(archiveSet, resourceRef, useOriginalFilePath);
        }

        public static string GetFilePath(ArchiveSet archiveSet, ResourceReference resourceRef, bool useOriginalFilePath = true)
        {
            return For(archiveSet.Game).GetFilePathInstance(archiveSet, resourceRef, useOriginalFilePath);
        }

        public static string ReadOriginalFilePath(ArchiveSet archiveSet, ResourceReference resourceRef)
        {
            return For(archiveSet.Game).ReadOriginalFilePathInstance(archiveSet, resourceRef);
        }

        public static string ReadOriginalFilePath(Stream stream, ResourceType type, CdcGame game)
        {
            return For(game).ReadOriginalFilePathInstance(stream, type);
        }

        protected abstract Dictionary<(ResourceType, ResourceSubType), string[]> Mappings { get; }

        private (ResourceType, ResourceSubType) GetType(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            foreach (KeyValuePair<(ResourceType, ResourceSubType), string[]> mapping in Mappings)
            {
                if (mapping.Value.Contains(extension, StringComparer.InvariantCultureIgnoreCase))
                    return mapping.Key;
            }
            return (ResourceType.Unknown, 0);
        }

        private bool TryGetResourceKeyInstance(string filePath, out ResourceKey resourceKey)
        {
            resourceKey = default;

            (ResourceType type, ResourceSubType subType) = GetType(filePath);
            if (type == 0)
                return false;

            Match match = Regex.Match(filePath, @"[\\/\.](\d+)\.\w+$");
            if (!match.Success)
                return false;

            int id = int.Parse(match.Groups[1].Value);
            resourceKey = new ResourceKey(type, subType, id);
            return true;
        }

        private string GetExtension(ResourceType type, ResourceSubType subType)
        {
            return Mappings.GetOrDefault((type, subType))?.FirstOrDefault() ??
                   Mappings.GetOrDefault((type, (ResourceSubType)0))?.FirstOrDefault() ??
                   ".type" + (int)type;
        }

        private string GetFileNameInstance(ArchiveSet archiveSet, ResourceReference resourceRef, bool useOriginalFilePath)
        {
            string name = useOriginalFilePath ? Sanitize(ReadOriginalFilePath(archiveSet, resourceRef)) : null;
            name = name != null ? $"{name}.{resourceRef.Id}" : resourceRef.Id.ToString();
            string extension = GetExtension(resourceRef.Type, resourceRef.SubType);
            return name + extension;
        }

        private string GetFilePathInstance(ArchiveSet archiveSet, ResourceReference resourceRef, bool useOriginalFilePath)
        {
            return $"{resourceRef.Type}\\{GetFileNameInstance(archiveSet, resourceRef, useOriginalFilePath)}";
        }

        protected virtual string ReadOriginalFilePathInstance(ArchiveSet archiveSet, ResourceReference resourceRef)
        {
            return null;
        }

        protected virtual string ReadOriginalFilePathInstance(Stream stream, ResourceType type)
        {
            return null;
        }

        private static string Sanitize(string name)
        {
            if (name == null || name.StartsWith("Section "))
                return null;

            return Regex.Replace(name, @"[^- \.\w\\]", "_");
        }
    }
}
