using Avalonia.Controls;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Controls
{
    public partial class FileTreeViewBase : UserControl
    {
        public FileTreeViewBase()
        {
            InitializeComponent();
        }
    }

    public class FileTreeView<TFile> : FileTreeViewBase
        where TFile : class
    {
        protected static readonly Bitmap FolderIcon = TypedAssetLoader.LoadSharedBitmap("/Resources/Folder.png");
        protected static readonly Bitmap FileIcon = TypedAssetLoader.LoadSharedBitmap("/Resources/File.png");

        private int _searchCounter;
        private bool _clearSearchClicked;

        public FileTreeView()
        {
            _txtSearch.TextChanged += OnSearchTextChanged;

            _btnClearSearch.IsVisible = false;
            _btnClearSearch.Click +=
                (s, e) =>
                {
                    _clearSearchClicked = true;
                    _txtSearch.Text = null;
                };

            _treeView.SelectionChanged += (s, e) => SelectionChanged?.Invoke(this, EventArgs.Empty);
            _treeView.DoubleTapped +=
                (s, e) =>
                {
                    FileTreeNode? node = (FileTreeNode?)_treeView.ActiveNode;
                    if (node?.File != null)
                        FileDoubleClicked?.Invoke(this, new(node.File));
                };
        }

        public SelectionMode SelectionMode
        {
            get => _treeView.SelectionMode;
            set => _treeView.SelectionMode = value;
        }

        public event EventHandler? SelectionChanged;

        public event EventHandler<FileEventArgs>? FileDoubleClicked;

        public TFile? ActiveFile
        {
            get
            {
                return ((FileTreeNode?)_treeView.ActiveNode)?.File;
            }
        }

        public List<TFile> SelectedFiles
        {
            get
            {
                List<TFile> files = [];
                foreach (FileTreeNode fileNode in _treeView.SelectedNodes)
                {
                    AddFilesRecursive(fileNode, files);
                }
                return files;
            }
        }

        protected void Populate(IList<FileTreeNode> rootNodes)
        {
            _treeView.Nodes = rootNodes;
        }

        public void Clear()
        {
            _treeView.Nodes = [];
        }

        private static void AddFilesRecursive(FileTreeNode fileNode, List<TFile> files)
        {
            if (fileNode.File != null)
                files.Add(fileNode.File);

            foreach (FileTreeNode childNode in fileNode.Children)
            {
                AddFilesRecursive(childNode, files);
            }
        }

        private async void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
        {
            string? searchText = _txtSearch.Text;

            if (!_clearSearchClicked)
            {
                int searchCounter = ++_searchCounter;
                await Task.Delay(1000);
                if (searchCounter != _searchCounter)
                    return;
            }

            if (string.IsNullOrEmpty(searchText))
                searchText = null;

            _treeView.SearchText = searchText;
            _btnClearSearch.IsVisible = searchText != null;
            _clearSearchClicked = false;
        }

        protected enum FileTreeNodeType
        {
            Folder,
            File,
            Locale
        }

        private record struct FileTreeNodeKey(FileTreeNodeType Type, string Name);

        protected class FileTreeNode : IFastTreeNode
        {
            private readonly SortedList<FileTreeNodeKey, FileTreeNode> _children = new(new FileTreeNodeKeyComparer());

            public FileTreeNode(string name, FileTreeNodeType type)
            {
                Name = name;
                Type = type;
                Icon = type == FileTreeNodeType.Folder ? FolderIcon : FileIcon;
            }

            public string Name
            {
                get;
            }

            public FileTreeNodeType Type
            {
                get;
                set;
            }

            public TFile? File
            {
                get;
                set;
            }

            public Bitmap Icon
            {
                get;
                set;
            }

            IEnumerable<IFastTreeNode> IFastTreeNode.Children => _children.Values;

            public IList<FileTreeNode> Children => _children.Values;

            public int Depth
            {
                get;
                set;
            }

            public bool Expanded
            {
                get;
                set;
            }

            public bool Visible
            {
                get;
                set;
            } = true;

            public void Add(FileTreeNode node)
            {
                _children.Add(new FileTreeNodeKey(node.Type, node.Name), node);
            }

            public FileTreeNode Add(string path)
            {
                return Add(path.Split('\\'), 0);
            }

            public FileTreeNode Add(string[] parts, int index)
            {
                string name = parts[index];
                FileTreeNodeType type = index == parts.Length - 1 ? FileTreeNodeType.File : FileTreeNodeType.Folder;
                FileTreeNode child = _children.GetOrAdd(
                    new FileTreeNodeKey(type, name),
                    () => new FileTreeNode(name, type)
                );
                if (index < parts.Length - 1)
                    return child.Add(parts, index + 1);

                return child;
            }

            public override string ToString()
            {
                return Name;
            }
        }

        private class FileTreeNodeKeyComparer : IComparer<FileTreeNodeKey>
        {
            public int Compare(FileTreeNodeKey x, FileTreeNodeKey y)
            {
                int comparison = x.Type.CompareTo(y.Type);
                if (comparison != 0)
                    return comparison;

                if (int.TryParse(Path.GetFileNameWithoutExtension(x.Name), out int xId) &&
                    int.TryParse(Path.GetFileNameWithoutExtension(y.Name), out int yId))
                {
                    return xId - yId;
                }

                return x.Name.CompareTo(y.Name);
            }
        }

        public class FileEventArgs(TFile file) : EventArgs
        {
            public TFile File => file;
        }
    }
}
