using System.Collections.Generic;
using System.Drawing;
using System.IO;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Controls;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Extractor.Controls
{
    internal class ArchiveFileTreeViewBase : FileTreeView<ArchiveFileReference>
    {
    }

    internal class ArchiveFileTreeView : ArchiveFileTreeViewBase
    {
        private static readonly Dictionary<string, Image> ExtensionImages =
            new Dictionary<string, Image>
            {
                { ".bin", Properties.Resources.Localization },
                { ".drm", Properties.Resources.List }
            };

        public void Populate(ArchiveSet archiveSet)
        {
            FileTreeNode rootFileNode = CreateFileNodes(archiveSet);
            Populate(rootFileNode);
        }

        private static FileTreeNode CreateFileNodes(ArchiveSet archiveSet)
        {
            FileTreeNode rootNode = new FileTreeNode(null);
            CdcGameInfo gameInfo = CdcGameInfo.Get(archiveSet.Game);
            foreach (ArchiveFileReference file in archiveSet.Files)
            {
                string name = CdcHash.Lookup(file.NameHash, archiveSet.Game);
                if (name == null)
                    continue;

                FileTreeNode fileNode = rootNode.Add(name);
                fileNode.Image = ExtensionImages.GetOrDefault(Path.GetExtension(name)) ?? FileImage;

                if (fileNode.Children.Count == 0)
                {
                    if (fileNode.File == null)
                    {
                        fileNode.File = file;
                    }
                    else
                    {
                        FileTreeNode prevLocaleNode = new FileTreeNode(gameInfo.LocaleToLanguageCode(fileNode.File.Locale))
                        {
                            File = fileNode.File,
                            Type = FileTreeNodeType.Locale,
                            Image = fileNode.Image
                        };
                        fileNode.Add(prevLocaleNode);

                        FileTreeNode localeNode = new FileTreeNode(gameInfo.LocaleToLanguageCode(file.Locale))
                        {
                            File = file,
                            Type = FileTreeNodeType.Locale,
                            Image = fileNode.Image
                        };
                        fileNode.Add(localeNode);

                        fileNode.File = null;
                    }
                }
                else
                {
                    FileTreeNode localeNode = new FileTreeNode(gameInfo.LocaleToLanguageCode(file.Locale))
                    {
                        File = file,
                        Type = FileTreeNodeType.Locale,
                        Image = fileNode.Image
                    };
                    fileNode.Add(localeNode);
                }
            }
            archiveSet.CloseStreams();
            return rootNode;
        }
    }
}
