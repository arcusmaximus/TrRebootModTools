// Taken from https://github.com/dk76/VirtualTreeView

using System;
using System.Windows.Forms;
using System.Drawing;
using System.ComponentModel;
using System.Data;
using System.Collections.Generic;

namespace TrRebootTools.Shared.Controls.VirtualTreeView
{


    public delegate void GetNodeCellText(VirtualTreeView tree, VirtualTreeNode node, int column, out string cellText);
    public delegate void GetNodeHintText(VirtualTreeView tree, VirtualTreeNode node, int column, out string hintText);
    public delegate void NodeNewText(VirtualTreeView tree, VirtualTreeNode node, int column, string cellText);
    public delegate void HeaderClick(VirtualTreeView tree, int column);
    public delegate void CompareNode(VirtualTreeView tree, VirtualTreeNode node1, VirtualTreeNode node2, int column, out int result);
    public delegate void NodeDoubleClick(VirtualTreeView tree, VirtualTreeNode node, int column);
    public delegate void NodeExpanded(VirtualTreeView tree, VirtualTreeNode node);
    public delegate void NodePaintText(VirtualTreeView tree, VirtualTreeNode node, int column, ref Font font, ref SolidBrush brush);
    public delegate void GetNodeCellImage(VirtualTreeView tree, VirtualTreeNode node, int column, out Image image);
    public delegate void OnDrawCell(VirtualTreeView tree, VirtualTreeNode node, int column, Graphics g, RectangleF rect, out bool handled);
    public delegate void OnCreateEditor(VirtualTreeView tree, VirtualTreeNode node, int column, out IEditor edit);
    public delegate void CellEditing(VirtualTreeView tree, VirtualTreeNode node, int column, out bool enable);
    public delegate void OnDrawText(VirtualTreeView tree, VirtualTreeNode node, int column, Graphics g, string text, Font font, Brush brush, RectangleF rect, StringFormat format, out bool handled);
    public delegate void BeforeCellPaint(VirtualTreeView tree, VirtualTreeNode node, int column, Graphics g, RectangleF rect);

    public class VirtualTreeView : UserControl, IDisposable
    {


        public NodesEnumarable Nodes { get => new NodesEnumarable(this); }

        private TreeOptionsHelper options = new TreeOptionsHelper();

        bool disposed = false;


        private IEditor FEdit = null;

        private VirtualTreeNode FFirstNode = null;
        private VirtualTreeNode FLastNode = null;

        private VirtualTreeHeader FHeader;
        private bool FUpdating = false;

        private Bitmap bitMap;
        private Graphics gr;

        internal int totalNodeHeight = 0;

        private int FTotalNodes = 0;
        private VirtualTreeNode FRootNode;

        private RectangleNode firstRectangleNode = null, lastRectangleNode = null;


        public event GetNodeCellText OnGetNodeCellText = null;

        public event NodeNewText OnNodeNewText;
        public event HeaderClick OnHeaderClick;
        public event CompareNode OnCompareNode;
        public event NodeDoubleClick OnNodeDoubleClick;
        public event NodeExpanded OnNodeExpanded;
        public event NodePaintText OnPaintText;
        public event GetNodeCellImage OnGetNodeCellImage;
        public event OnDrawCell DrawCell;
        public event OnCreateEditor CreateEditor;
        public event CellEditing Editing;
        public event GetNodeHintText OnGetNodeHintText;
        public event OnDrawText NodeDrawText;
        public event BeforeCellPaint OnBeforeCellPaint;
        public event EventHandler OnSelectionChanged;

        public int editDelay = 50;
        private Timer editTimer = new Timer();

        private Timer hintTimer = new Timer();






        [Category("Data")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public VirtualTreeHeader Header
        {
            get { return FHeader; }
            set
            {
                FHeader = value;
            }
        }



        private int FResizedColumn = -1;


        private Color lineColor = Color.Silver;


        private float lineWidth = 1;


        public Color FBack2Color = Color.FromArgb(0xE5, 0xE5, 0xE5);

        [Category("Appearance")]
        public Color Back2Color
        {
            get { return FBack2Color; }
            set { FBack2Color = value; }

        }






        [Category("Behavior")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public TreeOptionsHelper Options { get => options; set => options = value; }

        [Category("Appearance")]
        public Color LineColor { get => lineColor; set => lineColor = value; }
        [Category("Appearance")]
        public float LineWidth { get => lineWidth; set => lineWidth = value; }

        [Category("Appearance")]
        public ButtonStyle ButtonStyle { get { return buttonStyle; } set { buttonStyle = value; buildTreeButtons(); } }

        [Category("Appearance")]
        public bool ShowHint
        {
            get
            {
                return FShowHint;

            }
            set
            {
                FShowHint = value;

                if (!DesignMode)
                {
                    if (FShowHint)
                        hintTimer.Start();
                    else
                        hintTimer.Stop();
                }
            }


        }


        private ButtonStyle buttonStyle = ButtonStyle.bsRectangle;

        public Color SelectedRowColor = Color.FromKnownColor(KnownColor.Highlight);

        private ScrollBar vertScroll;
        private ScrollBar horzScroll;
        private Brush brushBackColor;
        protected SolidBrush brushSelectedRowColor;

        private SelectedContainer FFirstSelected;
        private SelectedContainer FLastSelected;
        private int FSelectedCount = 0;
        protected SolidBrush brushSelectedColumnColor;
        public Color SelectedColumnColor = Color.FromKnownColor(KnownColor.Highlight);

        private Bitmap FMinusButton;
        private Bitmap FPlusButton;

        private bool FShowHint = true;

        public ExpandedHelper Expanded { get; }

        public new void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposed) return;

            if (disposing)
            {
                editTimer.Stop();
                editTimer.Dispose();
                hintTimer.Stop();
                hintTimer.Dispose();
                base.Dispose(true);
            }

            disposed = true;
        }


        public VirtualTreeView()
        {
            DoubleBuffered = true;

            if (FHeader == null)
                FHeader = new VirtualTreeHeader();

            ScrollBar v = new VScrollBar();
            v.Dock = DockStyle.Right;
            Controls.Add(v);
            vertScroll = v;
            v.ValueChanged += V_Changed;
            v.Visible = false;

            v = new HScrollBar();
            v.Dock = DockStyle.Bottom;
            Controls.Add(v);

            horzScroll = v;
            v.Visible = false;
            v.ValueChanged += H_Changed;
            //PaintOptions = PaintOption.toShowButtons;
            FRootNode = new VirtualTreeNode(this);
            brushBackColor = new SolidBrush(BackColor);
            brushSelectedRowColor = new SolidBrush(SelectedRowColor);
            brushSelectedColumnColor = new SolidBrush(SelectedColumnColor);


            buildTreeButtons();


            Expanded = new ExpandedHelper(this);

            hintTimer.Interval = 100;
            hintTimer.Tick += HintTimer_Tick;





            //hintTimer.Start();


        }



        Point lastPoint = new Point(0, 0);
        DateTime lastMouseTime;
        ToolTip toolTip = new ToolTip();
        string lastToolTipText = "";


        public void ScrollToBottom()
        {
            if (!vertScroll.Visible)
                return;

            vertScroll.Value = vertScroll.Maximum;
            ReDrawTree();
        }


        private void HintTimer_Tick(object sender, EventArgs e)
        {
            if (!FShowHint || FUpdating) return;

            if (lastPoint != Cursor.Position)
            {
                lastPoint = Cursor.Position;
                lastMouseTime = DateTime.Now;
                lastToolTipText = "";


            }
            else
            {
                if (lastMouseTime.AddSeconds(1) >= DateTime.Now)
                {

                    var p = PointToClient(lastPoint);
                    var node = GetNodeAt(p.X, p.Y);
                    if (node != null)
                    {

                        int column = GetColumnIndex(p.X);
                        if (column >= 0)
                        {

                            if (OnGetNodeHintText != null)
                            {
                                var s = "";
                                OnGetNodeHintText(this, node, column, out s);
                                if (s != "")
                                {
                                    toolTip.SetToolTip(this, s);
                                    return;
                                }



                            }


                            if (OnGetNodeCellText != null)
                            {
                                var s = "";
                                OnGetNodeCellText(this, node, column, out s);
                                if (s != "")
                                {
                                    if (s != lastToolTipText)
                                    {
                                        if (bitMap != null)
                                        {
                                            var g = Graphics.FromImage(bitMap);

                                            var r = GetColumnRect(column);
                                            Font f = null;
                                            SolidBrush b = null;



                                            f = Font;
                                            if (OnPaintText != null)
                                                OnPaintText(this, node, column, ref f, ref b);



                                            var pf = g.MeasureString(s, f);
                                            if (pf.Width > r.Width)
                                            {
                                                toolTip.SetToolTip(this, s);
                                                s = lastToolTipText;
                                            }
                                            else
                                                toolTip.SetToolTip(this, "");
                                        }

                                    }
                                }
                            }

                        }




                    }
                    else
                        toolTip.SetToolTip(this, "");



                }

            }


        }

        private void buildTreeButtons()
        {
            //FPlusButton = new Bitmap(VirtualTreeNode.NodeHeightDefault, VirtualTreeNode.NodeHeightDefault);
            //FMinusButton = new Bitmap(VirtualTreeNode.NodeHeightDefault, VirtualTreeNode.NodeHeightDefault);


            FPlusButton = new Bitmap(9, 9);
            FMinusButton = new Bitmap(9, 9);


            Graphics g = Graphics.FromImage(FMinusButton);
            if (ButtonStyle == ButtonStyle.bsRectangle)
            {
                using SolidBrush b = new SolidBrush(Color.Black);
                using Pen pen = new Pen(b, 1);
                g.DrawRectangle(pen, 0, 0, FMinusButton.Width - 1, FMinusButton.Height - 1);
                pen.Color = Color.Black;
                g.DrawLine(pen, 2, FMinusButton.Height / 2, FMinusButton.Width - 3, FMinusButton.Height / 2);
            }
            else
            {
                using SolidBrush b = new SolidBrush(Color.Black);
                Point[] points = { new Point(0, 2), new Point(8, 2), new Point(4, 6) };
                g.FillPolygon(b, points);
            }


            g = Graphics.FromImage(FPlusButton);
            if (buttonStyle == ButtonStyle.bsRectangle)
            {
                using SolidBrush b = new SolidBrush(Color.Black);
                using Pen pen = new Pen(b, 1);
                g.DrawRectangle(pen, 0, 0, FMinusButton.Width - 1, FMinusButton.Height - 1);
                pen.Color = Color.Black;
                g.DrawLine(pen, 2, FMinusButton.Height / 2, FMinusButton.Width - 2 - 1, FMinusButton.Height / 2);
                g.DrawLine(pen, FMinusButton.Width / 2, 2, FMinusButton.Width / 2, FMinusButton.Height - 3);
            }
            else
            {
                using SolidBrush b = new SolidBrush(Color.Black);
                Point[] points = { new Point(2, 0), new Point(6, 4), new Point(2, 8) };
                g.FillPolygon(b, points);
            }





        }


        private void H_Changed(object sender, EventArgs e)
        {
            ReDrawTree();
        }

        private void V_Changed(object sender, EventArgs e)
        {
            ReDrawTree();
        }



        int getNodeTotalHeight(VirtualTreeNode node)
        {
            int result = 0;

            if (isNodeVisible(node))
            {
                result += node.nodeHeight;
                VirtualTreeNode child = node.firstChild;
                if (child != null && (node.state & NodeState.vsExpanded) > 0)
                    while (child != null)
                    {
                        result += getNodeTotalHeight(child);
                        child = child.nextSibling;

                    }



            }

            return result;
        }
        internal bool isNodeVisible(VirtualTreeNode node)
        {

            if (node.level == 0)
                return true;
            else
            {
                if ((node.parent.state & NodeState.vsExpanded) == 0)
                    return false;
                else
                    return isNodeVisible(node.parent);
            }
        }

        public VirtualTreeNode ActiveNode
        {
            get => FLastSelected?.node;
            set
            {
                ClearSelected();
                if (value != null)
                    AddToSelected(value);

                OnSelectionChanged?.Invoke(this, EventArgs.Empty);
                ReDrawTree();
            }
        }

        public void SelectAll()
        {
            FFirstSelected = null;
            FSelectedCount = 0;
            SelectedContainer prevContainer = null;
            foreach (VirtualTreeNode node in Nodes)
            {
                SelectedContainer container = new SelectedContainer { node = node };
                if (prevContainer == null)
                    FFirstSelected = container;
                else
                    prevContainer.next = container;

                prevContainer = container;
                FSelectedCount++;
            }
            FLastSelected = prevContainer;
            OnSelectionChanged?.Invoke(this, EventArgs.Empty);
            ReDrawTree();
        }

        public void UnselectAll()
        {
            ClearSelected();
            OnSelectionChanged?.Invoke(this, EventArgs.Empty);
            ReDrawTree();
        }

        public IEnumerable<VirtualTreeNode> SelectedNodes
        {
            get
            {
                SelectedContainer container = FFirstSelected;
                while (container != null)
                {
                    yield return container.node;
                    container = container.next;
                }
            }
        }


        internal bool isSelected(VirtualTreeNode node)
        {

            var s = FFirstSelected;

            while (s != null)
            {

                if (s.node == node) return true;
                s = s.next;

            }


            return false;
        }


        internal void ClearSelected()
        {
            FFirstSelected = null;
            FLastSelected = null;
            FSelectedCount = 0;
        }


        internal int CompareNodePosition(VirtualTreeNode node1, VirtualTreeNode node2)
        {

            if (node1.level == node2.level)
                return node1.FIndex - node2.FIndex;
            else
                if (node1.level > node2.level)
            {
                while (node2.level != node1.level)
                {
                    node1 = node1.parent;
                }
                return node1.FIndex - node2.FIndex;

            }
            else
            {
                while (node2.level != node1.level)
                {
                    node2 = node2.parent;
                }
                return node1.FIndex - node2.FIndex;
            }

        }



        internal VirtualTreeNode GetMinSelectedNode()
        {
            if (FFirstSelected == null) return null;
            var node = GetFirst();
            VirtualTreeNode m = null;
            while (node != null)
            {
                if (isSelected(node)) { m = node; return m; };

                node = GetNext(node);
            }
            return null;

        }

        internal VirtualTreeNode GetMaxSelectedNode()
        {
            if (FFirstSelected == null) return null;
            var node = GetFirst();
            VirtualTreeNode m = null;
            while (node != null)
            {
                if (isSelected(node)) m = node;

                node = GetNext(node);
            }
            return m;

        }


        internal SelectedContainer AddToSelected(VirtualTreeNode node)
        {
            if (FFirstSelected == null)
            {
                FFirstSelected = new SelectedContainer();
                FFirstSelected.node = node;
                FLastSelected = FFirstSelected;
                FSelectedCount++;
                return FFirstSelected;
            }
            else
            {


                var s = FFirstSelected;
                SelectedContainer l = null;
                while (s != null)
                {
                    l = s;
                    if (s.node == node) return s;
                    s = s.next;
                }

                l.next = new SelectedContainer();
                l.next.node = node;
                FSelectedCount++;
                FLastSelected = l.next;
                return l.next;

                /*var l = FLastSelected;
                l.next = new SelectedContainer();
                l.next.node = node;
                FSelectedCount++;
                FLastSelected = l.next;
                return l.next;*/



            }
        }


        internal void RemoveFromSelected(VirtualTreeNode node)
        {
            if (node == null) return;
            var n = FFirstSelected;

            SelectedContainer l = null;
            while (n != null)
            {

                if (n.node == node)
                {
                    if (l != null)
                    {
                        l.next = n.next;
                    }
                    if (n == FFirstSelected)
                        FFirstSelected = n.next;
                    else if (n == FLastSelected)
                        FLastSelected = l;
                    FSelectedCount--;
                    return;

                }
                l = n;
                n = n.next;
            }



        }

        public void DeleteChildren(VirtualTreeNode node)
        {

            if (node == null || node.childCount <= 0) return;
            bool prevUpdate = FUpdating;
            try
            {
                if (!prevUpdate)
                    BeginUpdate();
                var n = node.firstChild;

                while (n != null)
                {
                    var n_cur = n;
                    n = n_cur.nextSibling;
                    DeleteNode(n_cur);
                }
            }
            finally
            {
                if (!prevUpdate)
                    EndUpdate();
            }
        }


        public void DeleteNode(VirtualTreeNode node, bool rebuildIndex = true)
        {
            if (node == null)
                return;
            bool prevUpdate = FUpdating;
            try
            {
                if (!prevUpdate)
                    BeginUpdate();
                if (isNodeVisible(node))
                {
                    int h = getNodeTotalHeight(node);

                    totalNodeHeight -= h;
                }

                if (node.prevSibling != null)
                    node.prevSibling.nextSibling = node.nextSibling;

                if (node.nextSibling != null)
                    node.nextSibling.prevSibling = node.prevSibling;

                if (node.parent != null && node.level > 0)
                {
                    node.parent.FChildCount--;
                    if (node == node.parent.firstChild)
                        node.parent.firstChild = node.nextSibling;
                    else if (node == node.parent.lastChild)
                        node.parent.lastChild = node.prevSibling;
                }

                if (node == FFirstNode)
                    FFirstNode = node.nextSibling;
                else if (node == FLastNode)
                    FLastNode = node.prevSibling;

                if (rebuildIndex)
                {
                    VirtualTreeNode n;
                    if (node.level == 0)
                        n = FFirstNode;
                    else
                        n = node.parent.firstChild;

                    RebuildIndex(n);
                }
                RemoveFromSelected(node);

            }
            finally
            {
                if (!prevUpdate)
                    EndUpdate();

            }

            RedrawVertScroll();


            if (!FUpdating) ReDrawTree();



        }
        public VirtualTreeNode InsertNode(VirtualTreeNode node, NodeAttachMode mode, object data, bool rebuildIndex = true)
        {
            VirtualTreeNode newNode = new VirtualTreeNode(data);
            newNode.FLevel = 0;
            FTotalNodes++;



            if (mode == NodeAttachMode.amInsertAfter || mode == NodeAttachMode.amInsertBefore || node == null)
            {
                if (node == null)
                {
                    if (FFirstNode == null)
                    {
                        FFirstNode = newNode;
                        FLastNode = newNode;

                        totalNodeHeight += newNode.nodeHeight;

                    }
                    else
                    {
                        if (mode == NodeAttachMode.amInsertAfter || mode == NodeAttachMode.amAddChildLast)
                        {
                            FLastNode.nextSibling = newNode;
                            newNode.prevSibling = FLastNode;
                            newNode.FIndex = FLastNode.FIndex + 1;
                            FLastNode = newNode;

                            totalNodeHeight += newNode.nodeHeight;
                        }
                        else
                        {
                            FFirstNode.prevSibling = newNode;
                            newNode.nextSibling = FFirstNode;
                            newNode.FIndex = FFirstNode.FIndex;
                            if (rebuildIndex)
                                RenumberTree(FFirstNode, newNode.FIndex + 1);
                            FFirstNode = newNode;

                            totalNodeHeight += newNode.nodeHeight;

                        }
                    }
                    newNode.FParent = FRootNode;
                }
                else
                {
                    newNode.FLevel = node.FLevel;
                    if (newNode.FLevel == 0)
                        newNode.FParent = FRootNode;
                    else
                        newNode.FParent = node.FParent;

                    if (mode == NodeAttachMode.amInsertAfter)
                    {
                        newNode.prevSibling = node;
                        newNode.nextSibling = node.nextSibling;

                        if (node.nextSibling != null)
                            node.nextSibling.prevSibling = newNode;

                        node.nextSibling = newNode;


                        if (rebuildIndex)
                            RenumberTree(newNode, node.FIndex + 1);

                        if (FLastNode == node)
                            FLastNode = newNode;

                        if (isNodeVisible(newNode))
                            totalNodeHeight += newNode.nodeHeight;
                    }
                    else
                    {
                        newNode.prevSibling = node.prevSibling;
                        newNode.nextSibling = node;
                        newNode.FIndex = node.FIndex;

                        if (node.prevSibling != null)
                            node.prevSibling.nextSibling = newNode;

                        node.prevSibling = newNode;

                        if (rebuildIndex)
                            RenumberTree(node, newNode.FIndex + 1);

                        if (FFirstNode == node)
                            FFirstNode = newNode;

                        if (isNodeVisible(newNode))
                            totalNodeHeight += newNode.nodeHeight;
                    }


                }
            }
            else if (mode == NodeAttachMode.amAddChildFirst || mode == NodeAttachMode.amAddChildLast)
            {
                if (node.firstChild != null)
                {
                    if (mode == NodeAttachMode.amAddChildFirst)
                    {
                        node.firstChild.prevSibling = newNode;
                        newNode.nextSibling = node.firstChild;
                        newNode.FLevel = node.FLevel + 1;
                        if (rebuildIndex)
                            RenumberTree(node.firstChild, node.firstChild.FIndex + 1);
                        node.firstChild = newNode;
                        newNode.FParent = node;

                        if (isNodeVisible(newNode))
                            totalNodeHeight += newNode.nodeHeight;
                    }
                    else
                    {
                        node.lastChild.nextSibling = newNode;
                        newNode.FLevel = node.FLevel + 1;
                        newNode.FIndex = node.lastChild.FIndex + 1;
                        newNode.prevSibling = node.lastChild;
                        node.lastChild = newNode;
                        newNode.FParent = node;

                        if (isNodeVisible(newNode))
                            totalNodeHeight += newNode.nodeHeight;
                    }
                }
                else
                {
                    newNode.FLevel = node.FLevel + 1;
                    node.firstChild = newNode;
                    node.lastChild = newNode;
                    newNode.FParent = node;

                    if (isNodeVisible(newNode))
                        totalNodeHeight += newNode.nodeHeight;
                }
            }

            if (newNode.FParent != null)
                newNode.FParent.FChildCount++;

            if (!FUpdating) ReDrawTree();

            return newNode;
        }




        void RenumberTree(VirtualTreeNode node, int newIndex)
        {
            if (node == null) return;
            node.FIndex = newIndex;
            if (node.nextSibling == null)
                return;
            newIndex++;
            do
            {
                node = node.nextSibling;
                node.FIndex = newIndex;
                newIndex++;
            }
            while (node.nextSibling != null);


        }


        public void RebuildIndex(VirtualTreeNode node)
        {
            if (node == null)
                RenumberTree(FFirstNode, 1);
            else
            {
                if (node.FParent != null && node.FParent.firstChild != null)
                {
                    RenumberTree(node.FParent.firstChild, 1);
                }
                else
                    RebuildIndex(null);
            }
        }


        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (Width == 0 || Height == 0) return;


            if (bitMap == null)
                bitMap = new Bitmap(Width, Height);
            else
                bitMap = new Bitmap(bitMap, Width, Height);
            gr = Graphics.FromImage(bitMap);




        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);




            ReDrawTree(e.Graphics);


            if (DesignMode)
            {
                using Pen pen = new Pen(LineColor, LineWidth);
                RectangleF r = e.Graphics.VisibleClipBounds;
                r.Width -= 1;
                r.Height -= 1;
                e.Graphics.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);


                if (options.Paint.ShowHorzGridLines)
                {
                    var y = e.Graphics.VisibleClipBounds.Y + Header.Height;
                    int i = 0;

                    using var b = new SolidBrush(BackColor);
                    using var b2 = new SolidBrush(Back2Color);
                    var brush = b;

                    while (true)
                    {


                        if (!Options.Paint.Back2Color)
                            brush = b;
                        else
                        if (i % 2 == 0)
                            brush = b;
                        else
                            brush = b2;

                        e.Graphics.FillRectangle(brush, e.Graphics.VisibleClipBounds.X + 1, y + 1, e.Graphics.VisibleClipBounds.Width - 2, y + VirtualTreeNode.NodeHeightDefault - 1);

                        e.Graphics.DrawLine(pen, e.Graphics.VisibleClipBounds.X, y, e.Graphics.VisibleClipBounds.Right, y);


                        y += VirtualTreeNode.NodeHeightDefault;
                        i++;

                        if (y > e.Graphics.VisibleClipBounds.Bottom) break;
                    }


                    float w = 0;

                    if (Options.Paint.FullVertGridLines)
                        for (i = 0; i < FHeader.Columns.Count; i++)
                        {
                            var r1 = GetColumnRect(i);


                            RectangleF rf = new RectangleF(r1.X, r1.Y, r1.Width, r1.Height);
                            w += r1.Width;
                            e.Graphics.DrawLine(pen, r1.Right, e.Graphics.VisibleClipBounds.Top, r1.Right, e.Graphics.VisibleClipBounds.Bottom);






                        }



                }
                else
                {
                    if (BorderStyle != BorderStyle.None)
                    {
                        using Pen borderPen = new Pen(lineColor, lineWidth);
                        e.Graphics.DrawRectangle(borderPen, Rectangle.Ceiling(e.Graphics.VisibleClipBounds));

                    }



                }


            }



        }

        int getChildNodeTotalHeight(VirtualTreeNode node)
        {
            int total = 0;
            VirtualTreeNode n = node.firstChild;
            while (n != null)
            {
                total += n.nodeHeight;
                if (n.childCount > 0 && (n.state & NodeState.vsExpanded) > 0)
                    total += getChildNodeTotalHeight(n);

                n = n.nextSibling;
            }


            return total;
        }


        public void ExpandNode(VirtualTreeNode node)
        {
            if ((node.state & NodeState.vsExpanded) > 0)
            {
                totalNodeHeight -= getChildNodeTotalHeight(node);
                node.state = node.state ^ NodeState.vsExpanded;
            }
            else
            {
                totalNodeHeight += getChildNodeTotalHeight(node);
                node.state = node.state | NodeState.vsExpanded;
            }

        }


        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            if (!vertScroll.Visible) return;

            var i = vertScroll.Value - e.Delta;

            if (i >= vertScroll.Minimum && i <= vertScroll.Maximum)
                vertScroll.Value = i;
            else
            if (i <= vertScroll.Minimum)
                vertScroll.Value = vertScroll.Minimum;
            else
            if (i >= vertScroll.Maximum)
                vertScroll.Value = vertScroll.Maximum;


            ReDrawTree();




        }

        private int FLastX = 0;
        private bool FLeftPressed = false;
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButtons.Left)
            {
                FLeftPressed = true;
                FResizedColumn = GetColumnIndex(e.X);

                var r = GetColumnRect(FResizedColumn);

                if (FResizedColumn > 0 && e.X - r.X < r.Width / 2)
                {
                    FResizedColumn--;
                }


            }

        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                FLeftPressed = false;
                FResizedColumn = -1;
                FLastX = 0;
                Cursor = Cursors.Arrow;
            }
        }

        private void changeWidthColumn(int index, int dx)
        {
            //for(int i=index;i<FHeader.Columns.Count;i++)
            // {
            FHeader.Columns[index].Width -= dx;
            ReDrawTree();
            // }

        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool FResize = false;

            int i;



            if (FResizedColumn < 0 || FResizedColumn >= FHeader.Columns.Count) return;

            i = FResizedColumn;
            {
                var r = GetColumnRect(i);
                if (e.Y > r.Bottom)
                {
                    Cursor = Cursors.Arrow;
                    return;
                }

                int offsetX = horzScroll.Visible ? horzScroll.Value : 0;

                //


                {
                    Cursor = Cursors.VSplit;
                    if (FLeftPressed)
                    {
                        int dx = FLastX - e.X;

                        if (FLastX != 0 && dx != 0)
                        {
                            changeWidthColumn(i, dx);
                        }

                        FLastX = e.X;
                    }
                    else
                        FLastX = 0;


                    FResize = true;
                    //break;   
                }

            }

            if (!FResize) { Cursor = Cursors.Default; FLastX = 0; }

            base.OnMouseMove(e);
        }
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (FUpdating) return;
            int i;
            var node = GetNodeAt(e.X, e.Y, out i);
            if (node != null && OnNodeDoubleClick != null)
            {
                OnNodeDoubleClick(this, node, i);
            }
        }


        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Right:
                case Keys.Left:
                case Keys.Up:
                case Keys.Down:
                    return true;

            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);



        }

        void GetEditNodeAndColumn(out VirtualTreeNode node, out int column)
        {
            node = FFirstSelected.node;
            column = FFirstSelected.column;

        }


        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);

            if (FUpdating) return;

            if (e.KeyCode == Keys.F2 || e.KeyCode == Keys.Return)
            {
                if (FFirstSelected != null && FFirstSelected.column >= 0)
                {
                    //BeginUpdate();
                    if (FFirstSelected != null)
                        CreateEditorProc(FFirstSelected.node, FFirstSelected.column);

                }



            }
            else
            if (e.KeyCode == Keys.PageDown)
            {
                vertScroll.Value += vertScroll.LargeChange;
                V_Changed(vertScroll, EventArgs.Empty);
            }
            else
            if (e.KeyCode == Keys.PageUp)
            {
                if (vertScroll.Value - vertScroll.LargeChange >= 0)
                {
                    vertScroll.Value -= vertScroll.LargeChange;
                    V_Changed(vertScroll, EventArgs.Empty);
                }
                else
                {
                    vertScroll.Value = 0;
                    V_Changed(vertScroll, EventArgs.Empty);

                }
            }
            else
            if (e.KeyCode == Keys.Down)
            {
                var node = FFirstSelected?.node;
                if (node == null) return;
                if (!Options.Misc.MultiSelect)
                {
                    VirtualTreeNode next = null;
                    if (node.childCount == 0 || (NodeState.vsExpanded & node.state) != NodeState.vsExpanded) next = GetNextSibling(node);
                    else
                        next = GetFirstChild(node);
                    if (next == null && node.parent != null) next = GetNextSibling(node.parent);
                    if (next != null)
                    {
                        var i = FFirstSelected?.column;
                        RemoveFromSelected(node);
                        var s = AddToSelected(next);
                        s.column = (int)i;
                        ReDrawTree();
                    }
                }
            }
            else
            if (e.KeyCode == Keys.Up)
            {
                var node = FFirstSelected?.node;
                if (node == null) return;
                if (!Options.Misc.MultiSelect)
                {
                    VirtualTreeNode next = null;
                    next = GetPrevSibling(node);

                    if (next != null && next.childCount > 0 && (NodeState.vsExpanded & next.state) == NodeState.vsExpanded)
                        next = GetLastChild(next);


                    if (next == null && node.level > 0) next = node.parent;






                    if (next != null)
                    {
                        var i = FFirstSelected?.column;
                        RemoveFromSelected(node);
                        var s = AddToSelected(next);
                        s.column = (int)i;
                        ReDrawTree();
                    }
                }
            }
            else
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)
            {
                if (FFirstSelected == null || FFirstSelected.column < 0) return;
                int c = e.KeyCode == Keys.Left ? FFirstSelected.column - 1 : FFirstSelected.column + 1;
                if (c >= 0 && c < FHeader.Columns.Count) FFirstSelected.column = c;
                ReDrawTree();
            }
            else
            if (e.KeyCode == Keys.Add || e.KeyCode == Keys.Oemplus)
            {
                if (FFirstSelected == null || FFirstSelected.node == null || FFirstSelected.node.childCount == 0 || (FFirstSelected.node.state & NodeState.vsExpanded) == NodeState.vsExpanded) return;
                ExpandNode(FFirstSelected.node);
                ReDrawTree();
            }
            else
            if (e.KeyCode == Keys.Subtract || e.KeyCode == Keys.OemMinus)
            {
                if (FFirstSelected == null || FFirstSelected.node == null || FFirstSelected.node.childCount == 0 || (FFirstSelected.node.state & NodeState.vsExpanded) != NodeState.vsExpanded) return;
                ExpandNode(FFirstSelected.node);
                ReDrawTree();
            }



        }

        protected void editOnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                VirtualTreeNode node = null;
                int column = -1;
                GetEditNodeAndColumn(out node, out column);
                string s = "";
                if (CreateEditor == null)
                    s = (sender as TextBox).Text;



                NewText(node, column, s);

                Controls.Remove((Control)sender);
                EndUpdate();
                ReDrawTree();

            }
            else
            if (e.KeyCode == Keys.Escape)
            {

                Controls.Remove((Control)sender);
                EndUpdate();
                ReDrawTree();

            }

        }

        internal void NewText(VirtualTreeNode node, int column, string s)
        {
            if (FUpdating) EndUpdate();

            if (OnNodeNewText != null)
            {
                OnNodeNewText(this, node, column, s);

                ReDrawTree();


            }


        }

        internal void RemoveControl(Control sender)
        {
            FEdit = null;
            Controls.Remove(sender);

        }


        private void CreateEditorProc(VirtualTreeNode node, int column)
        {
            if (!options.Misc.Editable) return;

            if (Editing != null)
            {
                bool b;
                Editing(this, node, column, out b);

                if (!b) return;

            }


            BeginUpdate();
            Rectangle r = GetCellRect(node, column);
            Rectangle rc = GetColumnRect(column);



            GetNodeCellText getText;
            if (OnGetNodeCellText != null)
                getText = OnGetNodeCellText;
            else
                getText = GetText;
            string s;
            getText(this, node, column, out s);


            if (CreateEditor != null)
            {
                IEditor edit = null;

                CreateEditor(this, node, column, out edit);

                if (edit != null)
                {
                    /*edit.Top = r.Y;
                    edit.Left = rc.X;
                    edit.Width = rc.Width;
                    edit.Height = r.Height;*/
                    //edit.Text = s;
                    //edit.KeyUp += editOnKeyUp;
                    Rectangle r1 = r;
                    r1.Width = rc.Width;


                    if (horzScroll.Visible)
                        r1.X = rc.X - horzScroll.Value;
                    else
                        r1.X = rc.X;

                    edit.PrepareEdit(r1);
                    edit.setText(s);
                    Controls.Add(edit.getEdit());
                    edit.Focus();

                }

                FEdit = edit;

            }
            else
            {
                var edit = new TextEditor(this, node, column);

                Rectangle r1 = r;
                r1.Width = rc.Width;

                if (horzScroll.Visible)
                    r1.X = rc.X - horzScroll.Value;
                else
                    r1.X = rc.X;

                edit.PrepareEdit(r1);
                edit.setText(s);
                Controls.Add(edit.getEdit());
                edit.Focus();

                FEdit = edit;


            }



        }

        internal void SendExpandedEvent(VirtualTreeNode node)
        {
            OnNodeExpanded?.Invoke(this, node);
        }


        int FEditClickX, FEditClickY;

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            if (e.Button != MouseButtons.Left)
                return;



            int columnIdx;
            var node = GetNodeAt(e.X, e.Y, out columnIdx);


            /*if (FEdit != null)
            {
                NewText(FEdit.getNode(), FEdit.getColumn(), FEdit.getText());
                this.Controls.Remove(FEdit.getEdit());
            }*/



            if (node != null && columnIdx == 0 && node.childCount > 0 && ModifierKeys == 0)
            {

                OnNodeExpanded?.Invoke(this, node);

                ExpandNode(node);
                ReDrawTree();



            }

            if (node != null)
            {

                SelectedContainer s = null;
                if (!Options.Misc.MultiSelect)
                {
                    RemoveFromSelected(FFirstSelected?.node);
                    s = AddToSelected(node);
                }
                else
                {
                    if (ModifierKeys == Keys.Control)
                    {
                        if (!isSelected(node))
                            s = AddToSelected(node);
                        else
                            RemoveFromSelected(node);
                    }
                    else
                    if (ModifierKeys == Keys.Shift & FLastSelected != null && FLastSelected.node.parent == node.parent)
                    {
                        var m = FLastSelected.node;


                        if (m.FIndex < node.FIndex)
                        {

                            var c = m;
                            do
                            {
                                AddToSelected(c);
                                if (c == node) break;
                                c = c.nextSibling;
                            } while (c != null);


                        }
                        else
                        {
                            var c = node;
                            do
                            {
                                AddToSelected(c);
                                if (c == m) break;
                                c = c.nextSibling;
                            } while (c != null);
                        }
                    }
                    else
                    {
                        ClearSelected();
                        s = AddToSelected(node);
                    }
                }

                if (s != null && columnIdx >= 0)
                    s.column = columnIdx;


                ReDrawTree();
                OnSelectionChanged?.Invoke(this, EventArgs.Empty);
            }
            else if (ModifierKeys == Keys.None && FSelectedCount > 0)
            {
                ClearSelected();
                ReDrawTree();
                OnSelectionChanged?.Invoke(this, EventArgs.Empty);
            }

            if (e.Y <= FHeader.Height)
            {
                int column = GetColumnIndex(e.X);
                if (column >= 0 && OnHeaderClick != null && Cursor != Cursors.VSplit)
                    OnHeaderClick(this, column);
            }

            if (node != null && columnIdx >= 0 && options.Misc.Editable)
            {
                FEditClickX = e.X;
                FEditClickY = e.Y;
                editTimer.Interval = editDelay;
                editTimer.Tick += EditTimer_Tick;
                editTimer.Start();
            }

            if (node != null && node.checkType == CheckType.ctCheckBox && columnIdx == 0)
            {
                if (node.checkState == CheckState.csCheckedNormal)
                    node.checkState = CheckState.csUncheckedNormal;
                else
                    node.checkState = CheckState.csCheckedNormal;
                ReDrawTree();
            }

        }

        private void EditTimer_Tick(object sender, EventArgs e)
        {
            editTimer.Stop();
            var point = PointToClient(Cursor.Position);
            if (point.X == FEditClickX && point.Y == FEditClickY)
            {
                FEditClickX++;
                //BeginUpdate();
                if (FFirstSelected != null)
                    CreateEditorProc(FFirstSelected.node, FFirstSelected.column);

            }
        }

        public VirtualTreeNode GetNodeAt(int X, int Y)
        {
            VirtualTreeNode node = null;
            RectangleNode rn = null;
            rn = firstRectangleNode;
            while (rn != null)
            {
                if (rn.isInRect(X, Y)) return rn.node;
                rn = rn.next;
            }
            return node;
        }

        public int GetColumnIndex(int X)
        {
            int index = -1;

            int x = 0;

            int offsetX = horzScroll.Visible ? horzScroll.Value : 0;
            X += offsetX;

            for (int i = 0; i < FHeader.Columns.Count; i++)
            {
                if (X >= x && X <= x + FHeader.Columns[i].Width)
                {
                    index = i;
                    return index;
                }
                x += FHeader.Columns[i].Width;

            }
            return index;
        }

        public VirtualTreeNode GetNodeAt(int X, int Y, out int Column)
        {
            VirtualTreeNode node = null;
            Column = -1;
            node = GetNodeAt(X, Y);
            if (node == null) return null;
            int x = 0;

            int offsetX = horzScroll.Visible ? horzScroll.Value : 0;
            X += offsetX;

            for (int i = 0; i < FHeader.Columns.Count; i++)
            {
                if (X >= x && X <= x + FHeader.Columns[i].Width)
                {
                    Column = i;
                    return node;
                }
                x += FHeader.Columns[i].Width;
            }
            return node;
        }


        Rectangle GetCellRect(VirtualTreeNode node, int column)
        {
            Rectangle r = Rectangle.Empty;
            if (node == null || column < 0) return r;
            RectangleNode rn = null;
            rn = firstRectangleNode;
            while (rn != null)
            {
                if (rn.node == node)
                {
                    var rh = GetColumnRect(column);
                    r.Y = rn.rect.Y;
                    r.Height = rn.rect.Height;
                    r.X = rn.rect.X;
                    r.Width = rn.rect.Width;
                    return r;
                }
                rn = rn.next;
            }
            return r;
        }

        Rectangle GetColumnRect(int column)
        {
            Rectangle r = Rectangle.Empty;

            if (FHeader.Columns.Count > column)
            {
                int x = 0;
                int w = 0;
                for (int i = 0; i <= column; i++)
                {
                    w = FHeader.Columns[i].Width;
                    x += w;



                }
                r.X = x - w;
                r.Y = 0;
                r.Height = FHeader.Height;
                r.Width = w;



            }


            return r;
        }

        private void InitTreeHeaderForPaint(Graphics g)
        {
            if (!FHeader.Visible) return;
            using SolidBrush brush = new SolidBrush(FHeader.BackColor);
            g.FillRectangle(brush, new Rectangle((int)g.VisibleClipBounds.X, (int)g.VisibleClipBounds.Y, (int)g.VisibleClipBounds.Width, FHeader.Height + 1));
        }

        private void DrawHeader(Graphics g)
        {




            InitTreeHeaderForPaint(g);
            int offsetX = horzScroll.Visible ? horzScroll.Value : 0;

            using StringFormat format = new StringFormat { LineAlignment = StringAlignment.Center };

            using SolidBrush brush = new SolidBrush(FHeader.ForeColor);
            using Pen pen = new Pen(LineColor, LineWidth);

            bool drawVertLine = options.Paint.FullVertGridLines;
            int w = 0;

            for (int i = 0; i < FHeader.Columns.Count; i++)
            {
                var r = GetColumnRect(i);
                r.X -= offsetX;

                RectangleF rf = new RectangleF(r.X, r.Y, r.Width, r.Height);
                w += r.Width;
                if (drawVertLine)
                {
                    g.DrawLine(pen, r.Right, g.VisibleClipBounds.Y, r.Right, g.VisibleClipBounds.Bottom);

                }

                if (FHeader.Visible)
                {
                    g.DrawLine(pen, r.Left, r.Bottom, r.Right, r.Bottom);
                    g.DrawLine(pen, r.Left, r.Top, r.Right, r.Top);
                }


                format.Alignment = FHeader.Columns[i].CaptionAlignment;
                format.LineAlignment = FHeader.Columns[i].LineAlignment;

                if (FHeader.Visible && FHeader.Columns[i].Width > 0)
                    g.DrawString(FHeader.Columns[i].Name, FHeader.Font, brush, rf, format);




            }
            var workWidth = Width - (vertScroll.Visible ? vertScroll.Width : 0);
            if (w > workWidth)
            {
                horzScroll.Visible = true;
                horzScroll.Maximum = w - workWidth;
            }
            else
            {
                horzScroll.Visible = false;
            }
        }

        public void GetText(VirtualTreeView tree, VirtualTreeNode node, int Column, out string s)
        {
            s = "";
            if (node == null) return;
            if (node.data == null)
                s = "Node " + node.FIndex.ToString() + " " + Column.ToString();
            else
                s = "unknown";

            return;
        }


        private void InitTreeForPaint(Graphics g)
        {
            using SolidBrush brush = new SolidBrush(BackColor);
            g.FillRectangle(brush, new Rectangle((int)g.VisibleClipBounds.X, FHeader.Height + 1, (int)g.VisibleClipBounds.Width, (int)g.VisibleClipBounds.Height - FHeader.Height));
        }


        private VirtualTreeNode getParentNextSilbling(VirtualTreeNode node)
        {
            VirtualTreeNode next = null;
            if (node == null || node.level == 0 && node.nextSibling == null) return null;
            next = node.parent.nextSibling;
            if (next == null) next = getParentNextSilbling(node.parent);
            return next;
        }

        private void DrawTree(Graphics g)
        {

            int offsetX = horzScroll.Visible ? horzScroll.Value : 0;

            StringFormat format = new StringFormat(StringFormatFlags.LineLimit | StringFormatFlags.NoWrap, 1003) { Trimming = StringTrimming.None };

            InitTreeForPaint(g);
            VirtualTreeNode node;
            node = FFirstNode;
            if (node == null)
                return;
            using SolidBrush foregroundBrush = new SolidBrush(ForeColor);
            using SolidBrush selectedForegroundBrush = new SolidBrush(Color.FromKnownColor(KnownColor.HighlightText));
            using SolidBrush background2Brush = new SolidBrush(Back2Color);
            using Pen pen = new Pen(LineColor, LineWidth);

            GetNodeCellText getText;
            if (OnGetNodeCellText != null)
                getText = OnGetNodeCellText;
            else
                getText = GetText;

            bool drawHorzLine = options.Paint.ShowHorzGridLines;
            bool fillBack2 = options.Paint.Back2Color;
            bool showButtons = options.Paint.ShowButtons;
            float buttonWidth = 16;
            if (showButtons)
            {
                //var size = g.MeasureString("+", Font);
                //buttonWidth = size.Width;

            }
            else
                buttonWidth = 0;


            string s;
            bool firstPaint = true;
            RectangleNode currentRectangleNode = null;

            firstRectangleNode = null;
            lastRectangleNode = null;

            for (int i = 0; i < FHeader.Columns.Count; i++)
            {
                var r = GetColumnRect(i);
                r.X -= offsetX;

                RectangleF rf = new RectangleF(r.X, r.Y, r.Width, r.Height);
                if (i == 0 && showButtons)
                {
                    rf.X += buttonWidth;
                    r.Width -= (int)buttonWidth;
                }
                //Pen pen=new Pen()
                int y = 1;
                if (FHeader.Visible)
                    y = FHeader.Height + 1;

                node = FFirstNode;
                int topVisibleY = y + vertScroll.Value;
                int bottomVisibleY = y + topVisibleY + (int)g.VisibleClipBounds.Bottom;




                bool b = false;

                while (node != null)
                {
                    b = !b;
                    if (y >= topVisibleY - node.nodeHeight)
                    {


                        if (firstPaint)
                        {
                            firstPaint = false;
                            if (firstRectangleNode == null)
                            {
                                firstRectangleNode = new RectangleNode();
                                currentRectangleNode = firstRectangleNode;
                            }
                            else
                            {
                                currentRectangleNode = firstRectangleNode;
                            }
                        }
                        else
                        if (i == 0)
                        {
                            if (currentRectangleNode.next != null)
                                currentRectangleNode = currentRectangleNode.next;
                            else
                            {
                                currentRectangleNode.next = new RectangleNode();
                                currentRectangleNode = currentRectangleNode.next;

                            }
                        }


                        if (i == 0)
                        {
                            currentRectangleNode.node = node;
                            currentRectangleNode.rect = new Rectangle((int)g.VisibleClipBounds.X, y - vertScroll.Value, (int)g.VisibleClipBounds.Width, node.nodeHeight);

                        }



                        if (fillBack2 && b && i == 0)
                        {
                            g.FillRectangle(background2Brush, new Rectangle((int)g.VisibleClipBounds.X, y - vertScroll.Value, (int)g.VisibleClipBounds.Width, node.nodeHeight));
                        }
                        if (drawHorzLine)
                        {
                            g.DrawLine(pen, g.VisibleClipBounds.Left, y - vertScroll.Value, g.VisibleClipBounds.Right, y - vertScroll.Value);

                        }

                        if (/*node == FFirstSelected?.node*/isSelected(node) && i == 0)
                        {
                            g.FillRectangle(brushSelectedRowColor, new Rectangle((int)g.VisibleClipBounds.X, y - vertScroll.Value, (int)g.VisibleClipBounds.Width, node.nodeHeight));

                        }





                        rf.Y = y - vertScroll.Value;
                        rf.Height = node.nodeHeight;
                        getText(this, node, i, out s);


                        if (/*node == FFirstSelected?.node*/isSelected(node) && i == FFirstSelected.column)
                        {
                            if (i == 0 && showButtons)
                            {
                                rf.X -= buttonWidth;
                                r.Width += (int)buttonWidth;
                            }
                            var selRect = rf;
                            selRect.X++;
                            selRect.Y++;
                            selRect.Width--;
                            selRect.Height--;
                            g.FillRectangle(brushSelectedColumnColor, selRect);


                            if (i == 0 && showButtons)
                            {
                                rf.X += buttonWidth;
                                r.Width -= (int)buttonWidth;
                            }

                        }

                        rf.X += node.level * buttonWidth;

                        var fw = rf.Width;
                        rf.Width -= node.level * buttonWidth;

                        if (i == 0 && Options.Paint.ShowButtons)
                            rf.Width -= buttonWidth;


                        rf = DrawCellText(g, format, node, isSelected(node) ? selectedForegroundBrush : foregroundBrush, showButtons, buttonWidth, s, i, rf);

                        rf.X -= node.level * buttonWidth;
                        rf.Width = fw;




                    }




                    if (node.childCount == 0 || (node.state & NodeState.vsExpanded) == 0)
                    {
                        y += node.nodeHeight;
                        var prev = node;
                        node = node.nextSibling;
                        if (node != null)
                        { }//y += node.nodeHeight;
                        else
                        {

                            node = prev;
                            node = getParentNextSilbling(node);
                            /*   if (node != null)
                                   y += node.nodeHeight;*/
                        }

                    }
                    else
                    {
                        y += node.nodeHeight;
                        node = node.firstChild;
                        //y += node.nodeHeight;                        
                    }



                    if (y > bottomVisibleY)
                    {
                        if (i == 0)
                        {
                            currentRectangleNode.next = null;
                            lastRectangleNode = currentRectangleNode;

                        }

                        break;
                    }
                }
                drawHorzLine = false;
                fillBack2 = false;




            }

            RedrawVertScroll();

        }


        private string getShortText(string s, float width, Font f, Graphics g, string endSymb = "...")
        {
            if (s == null) return "";
            if (s.Length == 0) return s;

            var tf = g.MeasureString(s, f);
            if (tf.Width <= width) return s;
            string sPrev = s[0].ToString();

            for (var i = 0; i < s.Length; i++)
            {
                var sCur = s.Substring(0, i) + endSymb;
                tf = g.MeasureString(sCur, f);
                if (tf.Width > width) return sPrev;
                sPrev = sCur;
            }

            return s;
        }


        private RectangleF DrawCellText(Graphics g, StringFormat format, VirtualTreeNode node, SolidBrush brush, bool showButtons, float buttonWidth, string s, int column, RectangleF rf)
        {

            if (rf.Width == 0) return rf;

            var font = Font;
            var rfOrig = rf;
            var sFormat = format;

            var b = brush.Color;
            try
            {

                var f = Font;



                if (OnBeforeCellPaint != null)
                {
                    RectangleF bf = rf;
                    bf.Y += LineWidth;
                    bf.Height -= LineWidth;


                    bf.X -= node.level * buttonWidth;

                    var fw = rf.Width;
                    bf.Width += node.level * buttonWidth;

                    if (column == 0 && Options.Paint.ShowButtons)
                    {
                        bf.X -= buttonWidth;
                        bf.Width += buttonWidth;
                    }



                    OnBeforeCellPaint(this, node, column, g, bf);
                }

                if (OnPaintText != null)
                    OnPaintText(this, node, column, ref f, ref brush);




                if (OnGetNodeCellImage != null)
                {
                    OnGetNodeCellImage(this, node, column, out Image image);
                    if (image != null)
                    {
                        g.DrawImage(image, (int)rf.X, (int)rf.Y);
                        rf.X += image.Width;
                    }
                }

                format.LineAlignment = FHeader.Columns[column].LineAlignment;
                format.Alignment = FHeader.Columns[column].Alignment;
                rf.Y += 1;

                bool handled = false;





                if (DrawCell != null)
                    DrawCell(this, node, column, g, rf, out handled);


                if (!handled)
                {
                    if (column > 0 || node.checkType == CheckType.ctNone)
                    {

                        var shortText = getShortText(s, rf.Width, f, g);


                        handled = false;
                        if (NodeDrawText != null)
                            NodeDrawText(this, node, column, g, s, f, brush, rf, format, out handled);

                        if (!handled)
                            g.DrawString(shortText, f, brush, rf, format);

                    }
                    else
                    {

                        System.Windows.Forms.VisualStyles.CheckBoxState cs = System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal;

                        if (node.checkState == CheckState.csCheckedNormal)
                            cs = System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal;

                        CheckBoxRenderer.DrawCheckBox(g, new Point((int)rf.X, (int)rf.Y), cs);
                        var size = CheckBoxRenderer.GetGlyphSize(g, cs);

                        var rf1 = rf;
                        rf1.X += size.Width;

                        handled = false;
                        if (NodeDrawText != null)
                            NodeDrawText(this, node, column, g, s, f, brush, rf, format, out handled);

                        if (!handled)
                            g.DrawString(s, f, brush, rf1, format);


                    }
                }




                if (node.childCount > 0 && column == 0 && showButtons)
                {
                    if ((node.state & NodeState.vsExpanded) == 0)
                        g.DrawImage(FPlusButton, new PointF(rf.X - buttonWidth * 2 + 2, rf.Y + 3));

                    //g.DrawString("+", f, brush, new PointF(rf.X - buttonWidth, rf.Y));


                    else
                        g.DrawImage(FMinusButton, new PointF(rf.X - buttonWidth * 2 + 2, rf.Y + 3));
                    //g.DrawString("-", f, brush, new PointF(rf.X - buttonWidth, rf.Y));
                }



            }
            finally

            {

                Font = font;
                brush.Color = b;
                rf = rfOrig;
                format = sFormat;
            }


            return rf;
        }

        private void RedrawVertScroll()
        {
            if (totalNodeHeight - Height + 37 > 37)
            {
                vertScroll.Maximum = totalNodeHeight - Height + 37;
                if (horzScroll.Visible)
                    vertScroll.Maximum += horzScroll.Height;
                vertScroll.Visible = true;
            }
            else
            {
                vertScroll.Visible = false;
            }
        }




        public object GetNodeData(VirtualTreeNode node)
        {
            return node.data;


        }

        public T GetNodeData<T>(VirtualTreeNode node)
        {
            if (node == null || node.data == null)
                return default;

            return (T)node.data;


        }





        internal void ReDrawTree(Graphics g = null)
        {
            if (FUpdating) return;

            if (bitMap == null)
            {
                bitMap = new Bitmap(Width, Height);
                gr = Graphics.FromImage(bitMap);
            }






            gr.FillRectangle(brushBackColor, gr.ClipBounds);

            DrawTree(gr);
            DrawHeader(gr);
            Graphics g1;
            if (g == null)
                g1 = CreateGraphics();
            else
                g1 = g;

            g1.DrawImage(bitMap, 0, 0);
        }

        public void BeginUpdate()
        {
            FUpdating = true;
            hintTimer.Stop();
            vertScroll.Enabled = false;
        }
        public void EndUpdate()
        {
            FUpdating = false;
            vertScroll.Enabled = true;
            if (FShowHint) hintTimer.Start();
            Invalidate();
        }

        public VirtualTreeNode GetFirst()
        {
            return FFirstNode;
        }

        public VirtualTreeNode GetLast()
        {
            return FLastNode;
        }

        public VirtualTreeNode GetNextSibling(VirtualTreeNode node)
        {
            return node?.nextSibling;
        }

        public VirtualTreeNode GetNext(VirtualTreeNode node)
        {
            if (node.childCount > 0)
                return node.firstChild;
            else
                if (node.nextSibling == null && node.parent != null)
                return node.parent.nextSibling;
            else
                return node.nextSibling;
        }


        public VirtualTreeNode GetNextSelected(VirtualTreeNode node)
        {
            if (node == null || FFirstSelected == null) return null;

            var n = FFirstSelected;
            while (n != null)
            {
                if (n.node == node)
                {
                    if (n.next != null) return n.next.node;
                    else
                        return null;

                }


                n = n.next;
            }

            return null;

        }


        public VirtualTreeNode GetPrevSibling(VirtualTreeNode node)
        {
            return node?.prevSibling;
        }

        public int GetTotalNodes()
        {
            return FTotalNodes;
        }



        VirtualTreeNode IntersectSorted(VirtualTreeNode node1, VirtualTreeNode node2, int column, SortDirection sortDirection, CompareNode compare)
        {
            VirtualTreeNode current, n1, n2, first;

            n1 = node1;
            n2 = node2;
            int result;
            int d = sortDirection == SortDirection.sdDescending ? -1 : 1;


            compare(this, n1, n2, column, out result);
            if (d * result <= 0)
            {
                current = n1;
                n1 = n1.nextSibling;
            }
            else
            {
                current = n2;
                n2 = n2.nextSibling;
            }
            first = current;
            while (n1 != null && n2 != null)
            {
                compare(this, n1, n2, column, out result);
                if (d * result <= 0)
                {
                    current.nextSibling = n1;
                    current = n1;
                    n1 = n1.nextSibling;
                }
                else
                {
                    current.nextSibling = n2;
                    current = n2;
                    n2 = n2.nextSibling;
                }
            }

            if (n1 != null)
                current.nextSibling = n1;
            else
                current.nextSibling = n2;
            return first;

        }


        class SortStackItem
        {
            public int level;
            public VirtualTreeNode node;
        }




        public void SortTree(int column, SortDirection direction)
        {
            int stackSize = 32;
            SortStackItem[] stack = new SortStackItem[stackSize];
            for (int i = 0; i < stackSize; i++)
                stack[i] = new SortStackItem();

            try
            {
                BeginUpdate();
                int stackPos = 0;
                VirtualTreeNode node = FFirstNode;
                while (node != null)
                {
                    stack[stackPos].level = 1;
                    stack[stackPos].node = node;
                    node = node.nextSibling;
                    stack[stackPos].node.nextSibling = null;
                    stackPos++;
                    while (stackPos > 1 && stack[stackPos - 1].level == stack[stackPos - 1].level)
                    {
                        stack[stackPos - 2].node = IntersectSorted(stack[stackPos - 2].node, stack[stackPos - 1].node, column, direction, OnCompareNode);
                        stack[stackPos - 2].level++;
                        stackPos--;
                    }
                }
                while (stackPos > 1)
                {
                    stack[stackPos - 2].node = IntersectSorted(stack[stackPos - 2].node, stack[stackPos - 1].node, column, direction, OnCompareNode);
                    stack[stackPos - 2].level++;
                    stackPos--;
                }

                if (stackPos > 0)
                    FFirstNode = stack[0].node;


                RebuildIndex(FFirstNode);

                node = FFirstNode;
                while (node != null)
                {
                    if (node.nextSibling != null) node.nextSibling.prevSibling = node;
                    node = node.nextSibling;
                }

            }
            finally
            {
                EndUpdate();
            }

            ReDrawTree();
        }

        public void Clear()
        {
            FFirstNode = null;
            FLastNode = null;
            FTotalNodes = 0;
            totalNodeHeight = 0;
            ClearSelected();
            firstRectangleNode = null;
            lastRectangleNode = null;
            vertScroll.Value = vertScroll.Minimum;
            horzScroll.Value = horzScroll.Minimum;
            if (!FUpdating) ReDrawTree();
            OnSelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public VirtualTreeNode GetFirstChild(VirtualTreeNode node)
        {
            return node?.firstChild;
        }

        public VirtualTreeNode GetLastChild(VirtualTreeNode node)
        {
            var n = node.firstChild;
            while (n != null)
            {
                if (n.nextSibling == null) return n;
                n = n.nextSibling;
            }
            return null;
        }


        public string TextNode(VirtualTreeNode node, int column)
        {
            if (OnGetNodeCellText != null)
            {
                OnGetNodeCellText(this, node, column, out string s);
                return s;
            }
            else
                return "";


        }



        public delegate bool IsImportNode(VirtualTreeNode node);


        bool IsDigitsOnly(string str)
        {
            foreach (char c in str)
            {

                if (!char.IsDigit(c))
                    return false;
            }

            return true;
        }

        public DataSet VirtualTreeViewToDataSet(IsImportNode isImport = null, bool importChild = false)
        {

            var tree = this;

            DataSet ds = new DataSet();

            DataTable table = new DataTable();

            for (int i = 0; i < tree.Header.Columns.Count; i++)
            {



                if (tree.Header.Columns[i].fieldInfo == null)
                    table.Columns.Add(tree.Header.Columns[i].Name, Type.GetType("System.String"));
                else
                    table.Columns.Add(tree.Header.Columns[i].Name, tree.Header.Columns[i].fieldInfo.FieldType);



            }





            var node = tree.GetFirst();


            while (node != null)
            {

                var row = table.NewRow();
                for (int i = 0; i < tree.Header.Columns.Count; i++)
                {

                    if (tree.Header.Columns[i].fieldInfo == null)
                    {
                        string s = "";
                        OnGetNodeCellText?.Invoke(tree, node, i, out s);

                        if (s.Length > 10 && IsDigitsOnly(s))
                            row[i] = "'" + s;
                        else
                            row[i] = s;

                    }
                    else
                    {
                        if (node.data != null)
                        {
                            row[i] = tree.Header.Columns[i].fieldInfo.GetValue(node.data);

                        }
                        else
                        {
                            row[i] = null;
                        }


                    }



                }

                if (isImport == null || isImport(node))
                    table.Rows.Add(row);

                if (importChild)
                    node = tree.GetNext(node);
                else
                    node = tree.GetNextSibling(node);
            }


            ds.Tables.Add(table);

            return ds;
        }


    }




}
