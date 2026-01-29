using System;
using System.IO;

namespace TrRebootTools.Shared.Controls
{
    public class FsFileTreeView : FileTreeView<FileInfo>
    {
        public void Populate(DirectoryInfo rootDirectory, string? searchPattern = null, Func<FileInfo, bool>? filter = null)
        {
            FileTreeNode rootNode = CreateNode(rootDirectory, searchPattern, filter);
            Populate(rootNode.Children);
        }

        private FileTreeNode CreateNode(DirectoryInfo directory, string? searchPattern, Func<FileInfo, bool>? filter)
        {
            FileTreeNode directoryNode = new(directory.Name, FileTreeNodeType.Folder);
            foreach (DirectoryInfo subDir in directory.EnumerateDirectories())
            {
                directoryNode.Add(CreateNode(subDir, searchPattern, filter));
            }
            foreach (FileInfo file in (searchPattern != null ? directory.EnumerateFiles(searchPattern) : directory.EnumerateFiles()))
            {
                if (filter != null && !filter(file))
                    continue;

                directoryNode.Add(
                    new FileTreeNode(file.Name, FileTreeNodeType.File)
                    {
                        File = file
                    }
                );
            }
            return directoryNode;
        }
    }
}
