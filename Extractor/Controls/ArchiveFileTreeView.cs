using Avalonia.Media.Imaging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Controls;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Extractor.Controls
{
    internal class ArchiveFileTreeView : FileTreeView<ArchiveFileReference>
    {
        private static readonly Dictionary<string, Bitmap> ExtensionImages =
            new()
            {
                { ".bin", TypedAssetLoader.LoadProgramBitmap("/Resources/Localization.png") },
                { ".drm", TypedAssetLoader.LoadProgramBitmap("/Resources/List.png") }
            };

        public ArchiveFileTreeView()
        {
            _treeView.DefaultExpandedRootNodeNames = ["pc-w", "pcx64-w"];
        }

        public void Populate(ArchiveSet archiveSet)
        {
            IList<FileTreeNode> nodes = CreateFileNodes(archiveSet);
            Populate(nodes);
        }

        private static IList<FileTreeNode> CreateFileNodes(ArchiveSet archiveSet)
        {
            FileTreeNode rootNode = new("", FileTreeNodeType.Folder);
            CdcGameInfo gameInfo = CdcGameInfo.Get(archiveSet.Game);
            foreach (ArchiveFileReference file in archiveSet.Files)
            {
                string? name = CdcHash.Lookup(file.NameHash, archiveSet.Game);
                if (name == null)
                    continue;

                FileTreeNode fileNode = rootNode.Add(name);
                fileNode.Icon = ExtensionImages.GetValueOrDefault(Path.GetExtension(name)) ?? FileIcon;

                if (!fileNode.Children.Any())
                {
                    if (fileNode.File == null)
                    {
                        fileNode.File = file;
                    }
                    else
                    {
                        FileTreeNode prevLocaleNode = new(gameInfo.LocaleToLanguageCode(fileNode.File.Locale), FileTreeNodeType.Locale)
                        {
                            File = fileNode.File,
                            Icon = fileNode.Icon
                        };
                        fileNode.Add(prevLocaleNode);

                        FileTreeNode localeNode = new(gameInfo.LocaleToLanguageCode(file.Locale), FileTreeNodeType.Locale)
                        {
                            File = file,
                            Icon = fileNode.Icon
                        };
                        fileNode.Add(localeNode);

                        fileNode.File = null;
                    }
                }
                else
                {
                    FileTreeNode localeNode = new(gameInfo.LocaleToLanguageCode(file.Locale), FileTreeNodeType.Locale)
                    {
                        File = file,
                        Icon = fileNode.Icon
                    };
                    fileNode.Add(localeNode);
                }
            }
            archiveSet.CloseStreams();
            return rootNode.Children;
        }
    }
}
