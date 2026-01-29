using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TrRebootTools.Shared.Controls
{
    public partial class FastTreeView : UserControl
    {
        private IEnumerable<IFastTreeNode> _nodes = [];
        private string? _searchText;

        public FastTreeView()
        {
            InitializeComponent();
        }

        public SelectionMode SelectionMode
        {
            get => _listBox.SelectionMode;
            set => _listBox.SelectionMode = value;
        }

        public string[] DefaultExpandedRootNodeNames
        {
            get;
            set;
        } = [];

        public IEnumerable<IFastTreeNode> Nodes
        {
            get
            {
                return _nodes;
            }
            set
            {
                _nodes = value;
                _listBox.Items.Clear();
                UpdateNodeVisibilitiesForSearchRecursive(_nodes);
                ApplyDefaultExpandedRootNodes();
                AddNodesRecursive(0, _nodes, null);
            }
        }

        public IFastTreeNode? ActiveNode
        {
            get
            {
                if ((_listBox.SelectedItems?.Count ?? 0) != 1)
                    return null;

                return (IFastTreeNode?)_listBox.SelectedItem;
            }
        }

        public IEnumerable<IFastTreeNode> SelectedNodes
        {
            get
            {
                if (_listBox.SelectedItems == null)
                    return [];

                return _listBox.SelectedItems.Cast<IFastTreeNode>();
            }
        }

        private void OnListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? SelectionChanged;

        public string? SearchText
        {
            get
            {
                return _searchText;
            }
            set
            {
                if (value == _searchText)
                    return;

                _listBox.Items.Clear();
                _searchText = value;
                UpdateNodeVisibilitiesForSearchRecursive(_nodes);
                ApplyDefaultExpandedRootNodes();
                _listBox.HighlightText = SearchText;
                AddNodesRecursive(0, _nodes, null);
            }
        }

        private bool UpdateNodeVisibilitiesForSearchRecursive(IEnumerable<IFastTreeNode> nodes)
        {
            IFastTreeNode? firstVisibleNode = null;
            int numVisibleNodes = 0;
            foreach (IFastTreeNode node in nodes)
            {
                bool anyChildrenVisible = UpdateNodeVisibilitiesForSearchRecursive(node.Children);
                node.Visible = anyChildrenVisible || string.IsNullOrWhiteSpace(_searchText) || node.Name.Contains(_searchText, StringComparison.InvariantCultureIgnoreCase);
                if (node.Visible)
                {
                    firstVisibleNode ??= node;
                    numVisibleNodes++;
                }
                node.Expanded = false;
            }

            if (firstVisibleNode != null && numVisibleNodes == 1 && firstVisibleNode.Children.Any())
                firstVisibleNode.Expanded = true;

            return numVisibleNodes > 0;
        }

        private void ApplyDefaultExpandedRootNodes()
        {
            foreach (IFastTreeNode node in _nodes)
            {
                node.Expanded = DefaultExpandedRootNodeNames.Contains(node.Name);
            }
        }

        private void OnItemExpandedChanged(FastTreeViewItem item)
        {
            int itemIndex = _listBox.IndexFromContainer(item);
            IFastTreeNode? node = item.Node;
            if (node == null)
                return;

            if (node.Expanded)
            {
                AddNodesRecursive(itemIndex + 1, node.Children, node);
            }
            else
            {
                RemoveNodesRecursive(itemIndex + 1, node.Children);
            }
        }

        private int AddNodesRecursive(int startIndex, IEnumerable<IFastTreeNode> nodes, IFastTreeNode? parentNode)
        {
            int nodesAdded = 0;
            foreach (IFastTreeNode node in nodes)
            {
                if (!node.Visible)
                    continue;

                node.Depth = (parentNode?.Depth ?? -1) + 1;
                _listBox.Items.Insert(startIndex + nodesAdded, node);
                nodesAdded++;

                if (node.Expanded)
                    nodesAdded += AddNodesRecursive(startIndex + nodesAdded, node.Children, node);
            }
            return nodesAdded;
        }

        private void RemoveNodesRecursive(int startIndex, IEnumerable<IFastTreeNode> nodes)
        {
            int numItems = nodes.Sum(CountNodesRecursive);
            for (int i = numItems - 1; i >= 0; i--)
            {
                _listBox.Items.RemoveAt(startIndex + i);
            }
            return;

            int CountNodesRecursive(IFastTreeNode node)
            {
                if (!node.Visible)
                    return 0;

                int count = 1;
                if (node.Expanded)
                {
                    foreach (IFastTreeNode child in node.Children)
                    {
                        count += CountNodesRecursive(child);
                    }
                }
                return count;
            }
        }
    }
}
