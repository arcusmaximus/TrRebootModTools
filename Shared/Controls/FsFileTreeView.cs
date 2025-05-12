using System;
using System.IO;

namespace TrRebootTools.Shared.Controls
{
    public class FsFileTreeView : FileTreeView<FileInfo>
    {
        public void Populate(DirectoryInfo rootDirectory, string searchPattern = null, Func<FileInfo, bool> filter = null)
        {
            Populate(CreateNode(rootDirectory, searchPattern, filter));
        }

        private FileTreeNode CreateNode(DirectoryInfo directory, string searchPattern, Func<FileInfo, bool> filter)
        {
            FileTreeNode directoryNode = new FileTreeNode(directory.Name)
                                         {
                                             Type = FileTreeNodeType.Folder,
                                             Image = FolderImage
                                         };
            foreach (DirectoryInfo subDir in directory.EnumerateDirectories())
            {
                directoryNode.Add(CreateNode(subDir, searchPattern, filter));
            }
            foreach (FileInfo file in (searchPattern != null ? directory.EnumerateFiles(searchPattern) : directory.EnumerateFiles()))
            {
                if (filter != null && !filter(file))
                    continue;

                directoryNode.Add(
                    new FileTreeNode(file.Name)
                    {
                        Type = FileTreeNodeType.File,
                        Image = FileImage,
                        File = file
                    }
                );
            }
            return directoryNode;
        }
    }
}
