using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.Linq;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.Shared.Controls
{
    internal class FastTreeViewItem : ListBoxItem
    {
        private static readonly Bitmap ExpandIcon = TypedAssetLoader.LoadSharedBitmap("/Resources/Expand.png");
        private static readonly Bitmap CollapseIcon = TypedAssetLoader.LoadSharedBitmap("/Resources/Collapse.png");

        private readonly StackPanel _panel;
        private Image? _expandButton;
        private readonly Image _icon;
        private readonly TextBlock _textBlock;
        private string? _highlightText;

        public FastTreeViewItem()
        {
            _panel = new()
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Height = 20
            };

            _icon = new()
            {
                Stretch = Stretch.None,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            _panel.Children.Add(_icon);

            _textBlock = new()
            {
                Margin = new(4, 0, 0, 0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            _panel.Children.Add(_textBlock);

            Content = _panel;
        }

        protected override Type StyleKeyOverride => typeof(ListBoxItem);

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            Node = (IFastTreeNode?)DataContext;
            Margin = new((Node?.Depth ?? 0) * 16, 0, 0, 0);

            if (Node != null && Node.Children.Any(n => n.Visible))
            {
                if (_expandButton == null)
                {
                    _expandButton =
                        new()
                        {
                            Stretch = Stretch.None,
                            Width = 16,
                            Height = 16
                        };
                    _expandButton.Tapped += OnExpandButtonClicked;
                    _panel.Children.Insert(0, _expandButton);

                    _icon.Margin = new();
                }
                UpdateExpandButton();
            }
            else
            {
                if (_expandButton != null)
                {
                    _panel.Children.RemoveAt(0);
                    _expandButton = null;
                }
                _icon.Margin = new(16, 0, 0, 0);
            }

            _icon.Source = Node?.Icon;
            UpdateTextBlock();
        }

        public IFastTreeNode? Node
        {
            get;
            private set;
        }

        private void OnExpandButtonClicked(object? sender, TappedEventArgs e)
        {
            if (Node == null)
                return;

            Node.Expanded = !Node.Expanded;
            UpdateExpandButton();
            ExpandedChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateExpandButton()
        {
            if (Node != null && _expandButton != null)
                _expandButton.Source = Node.Expanded ? CollapseIcon : ExpandIcon;
        }

        public event EventHandler? ExpandedChanged;

        public string? HighlightText
        {
            get => _highlightText;
            set
            {
                if (value == _highlightText)
                    return;

                _highlightText = value;
                UpdateTextBlock();
            }
        }

        private void UpdateTextBlock()
        {
            TextBlockHelper.SetTextWithHighlights(_textBlock, Node?.Name, _highlightText);
        }
    }
}
