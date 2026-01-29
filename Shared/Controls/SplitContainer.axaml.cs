using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Metadata;

namespace TrRebootTools.Shared.Controls
{
    public partial class SplitContainer : UserControl
    {
        private bool[] _panelsCollapsed = [];

        public SplitContainer()
        {
            InitializeComponent();
        }

        public static readonly StyledProperty<Orientation> OrientationProperty = AvaloniaProperty.Register<SplitContainer, Orientation>(nameof(Orientation));

        public Orientation Orientation
        {
            get => GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        [Content]
        public Avalonia.Controls.Controls ContentControls { get; } = [];

        public RowDefinitions PanelSizes { get; set; } = [];

        protected override void OnInitialized()
        {
            base.OnInitialized();
            _panelsCollapsed = new bool[ContentControls.Count - 1];
            RebuildLayout();
        }

        public bool IsPanelCollapsed(int index)
        {
            return _panelsCollapsed[index];
        }

        public void SetPanelCollapsed(int index, bool collapsed)
        {
            if (collapsed == _panelsCollapsed[index])
                return;

            _panelsCollapsed[index] = collapsed;
            RebuildLayout();
        }

        private void RebuildLayout()
        {
            _grid.Children.Clear();
            _grid.RowDefinitions.Clear();
            _grid.ColumnDefinitions.Clear();

            if (ContentControls.Count > 0 && ContentControls[0] == _grid)
            {
                Content = _grid;
                ContentControls.RemoveAt(0);
            }

            for (int i = 0; i < ContentControls.Count; i++)
            {
                if (_panelsCollapsed[i])
                    continue;

                if (_grid.Children.Count > 0)
                    AddGridChild(new GridSplitter(), new GridLength(4, GridUnitType.Pixel));

                AddGridChild(
                    ContentControls[i],
                    i < PanelSizes.Count ? PanelSizes[i].Height : GridLength.Star
                );
            }
        }

        private void AddGridChild(Control control, GridLength size)
        {
            if (Orientation == Orientation.Horizontal)
            {
                _grid.ColumnDefinitions.Add(new(size));
                Grid.SetRow(control, 0);
                Grid.SetColumn(control, _grid.Children.Count);
            }
            else
            {
                _grid.RowDefinitions.Add(new(size));
                Grid.SetRow(control, _grid.Children.Count);
                Grid.SetColumn(control, 0);
            }
            _grid.Children.Add(control);
        }
    }
}
