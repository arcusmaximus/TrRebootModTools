using Avalonia.Controls;

namespace TrRebootTools.Shared.Controls
{
    public class ListViewColumn
    {
        public ListViewColumn()
        {
        }

        public ListViewColumn(string name, GridLength width)
        {
            Name = name;
            Width = width;
        }

        public string Name
        {
            get;
            set;
        } = string.Empty;

        public GridLength Width
        {
            get;
            set;
        } = new(1, GridUnitType.Star);
    }
}
