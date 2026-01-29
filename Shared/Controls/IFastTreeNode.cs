using Avalonia.Media.Imaging;
using System.Collections.Generic;

namespace TrRebootTools.Shared.Controls
{
    public interface IFastTreeNode
    {
        Bitmap Icon { get; }
        string Name { get; }
        IEnumerable<IFastTreeNode> Children { get; }

        int Depth { get; set; }
        bool Expanded { get; set; }
        bool Visible { get; set; }
    }
}
