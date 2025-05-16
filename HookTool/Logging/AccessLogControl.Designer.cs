using TrRebootTools.HookTool.Logging;
using TrRebootTools.HookTool;

namespace TrRebootTools.HookTool.Logging
{
    partial class AccessLogControl
    {
        /// <summary> 
        /// 必要なデザイナー変数です。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        #region コンポーネント デザイナーで生成されたコード

        /// <summary> 
        /// デザイナー サポートに必要なメソッドです。このメソッドの内容を 
        /// コード エディターで変更しないでください。
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AccessLogControl));
            TrRebootTools.Shared.Controls.VirtualTreeView.MiscOptionHelper miscOptionHelper1 = new TrRebootTools.Shared.Controls.VirtualTreeView.MiscOptionHelper();
            TrRebootTools.Shared.Controls.VirtualTreeView.PaintOptionHelper paintOptionHelper1 = new TrRebootTools.Shared.Controls.VirtualTreeView.PaintOptionHelper();
            this._tvLog = new TrRebootTools.HookTool.Logging.LogListView();
            this._btnExtract = new System.Windows.Forms.Button();
            this._toolStrip = new System.Windows.Forms.ToolStrip();
            this._btnEnableLogging = new System.Windows.Forms.ToolStripButton();
            this._btnClearLists = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this._btnCopy = new System.Windows.Forms.ToolStripButton();
            this._btnSave = new System.Windows.Forms.ToolStripButton();
            this._txtFilter = new System.Windows.Forms.ToolStripTextBox();
            this._saveFileDialog = new System.Windows.Forms.SaveFileDialog();
            this._toolStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // _tvLog
            // 
            this._tvLog.ActiveNode = null;
            this._tvLog.Back2Color = System.Drawing.SystemColors.Window;
            this._tvLog.BackColor = System.Drawing.SystemColors.Window;
            this._tvLog.ButtonStyle = TrRebootTools.Shared.Controls.VirtualTreeView.ButtonStyle.bsRectangle;
            this._tvLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this._tvLog.Header.BackColor = System.Drawing.SystemColors.ButtonFace;
            this._tvLog.Header.Columns = ((System.Collections.Generic.List<TrRebootTools.Shared.Controls.VirtualTreeView.VirtualTreeColumn>)(resources.GetObject("resource.Columns")));
            this._tvLog.Header.Font = new System.Drawing.Font("Tahoma", 8F);
            this._tvLog.Header.ForeColor = System.Drawing.Color.Black;
            this._tvLog.Header.Height = 16;
            this._tvLog.Header.Visible = true;
            this._tvLog.LineColor = System.Drawing.Color.Silver;
            this._tvLog.LineWidth = 1F;
            this._tvLog.Location = new System.Drawing.Point(0, 25);
            this._tvLog.Margin = new System.Windows.Forms.Padding(4);
            this._tvLog.Name = "_tvLog";
            miscOptionHelper1.Editable = false;
            miscOptionHelper1.MultiSelect = true;
            this._tvLog.Options.Misc = miscOptionHelper1;
            paintOptionHelper1.Back2Color = false;
            paintOptionHelper1.FullVertGridLines = false;
            paintOptionHelper1.ShowButtons = true;
            paintOptionHelper1.ShowHorzGridLines = false;
            this._tvLog.Options.Paint = paintOptionHelper1;
            this._tvLog.ShowHint = true;
            this._tvLog.Size = new System.Drawing.Size(703, 412);
            this._tvLog.TabIndex = 1;
            this._tvLog.OnGetNodeCellText += new TrRebootTools.Shared.Controls.VirtualTreeView.GetNodeCellText(this.GetNodeCellText);
            this._tvLog.OnSelectionChanged += new System.EventHandler(this._tvLog_OnSelectionChanged);
            this._tvLog.DoubleClick += new System.EventHandler(this._tvLog_DoubleClick);
            this._tvLog.KeyDown += new System.Windows.Forms.KeyEventHandler(this._tvLog_KeyDown);
            // 
            // _btnExtract
            // 
            this._btnExtract.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._btnExtract.Enabled = false;
            this._btnExtract.Location = new System.Drawing.Point(0, 437);
            this._btnExtract.Margin = new System.Windows.Forms.Padding(4);
            this._btnExtract.Name = "_btnExtract";
            this._btnExtract.Size = new System.Drawing.Size(703, 69);
            this._btnExtract.TabIndex = 5;
            this._btnExtract.Text = "Extract";
            this._btnExtract.UseVisualStyleBackColor = true;
            this._btnExtract.Click += new System.EventHandler(this._btnExtract_Click);
            // 
            // _toolStrip
            // 
            this._toolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this._toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._btnEnableLogging,
            this._btnClearLists,
            this.toolStripSeparator1,
            this._btnCopy,
            this._btnSave,
            this._txtFilter});
            this._toolStrip.Location = new System.Drawing.Point(0, 0);
            this._toolStrip.Name = "_toolStrip";
            this._toolStrip.Size = new System.Drawing.Size(703, 25);
            this._toolStrip.TabIndex = 6;
            this._toolStrip.Text = "toolStrip1";
            // 
            // _btnEnableLogging
            // 
            this._btnEnableLogging.Checked = true;
            this._btnEnableLogging.CheckOnClick = true;
            this._btnEnableLogging.CheckState = System.Windows.Forms.CheckState.Checked;
            this._btnEnableLogging.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this._btnEnableLogging.Image = ((System.Drawing.Image)(resources.GetObject("_btnEnableLogging.Image")));
            this._btnEnableLogging.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._btnEnableLogging.Name = "_btnEnableLogging";
            this._btnEnableLogging.Size = new System.Drawing.Size(23, 22);
            this._btnEnableLogging.ToolTipText = "Toggle logging";
            this._btnEnableLogging.Click += new System.EventHandler(this._btnEnableLogging_Click);
            // 
            // _btnClearLists
            // 
            this._btnClearLists.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this._btnClearLists.Image = ((System.Drawing.Image)(resources.GetObject("_btnClearLists.Image")));
            this._btnClearLists.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._btnClearLists.Name = "_btnClearLists";
            this._btnClearLists.Size = new System.Drawing.Size(23, 22);
            this._btnClearLists.ToolTipText = "Clear list";
            this._btnClearLists.Click += new System.EventHandler(this._btnClearLists_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 25);
            // 
            // _btnCopy
            // 
            this._btnCopy.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this._btnCopy.Image = ((System.Drawing.Image)(resources.GetObject("_btnCopy.Image")));
            this._btnCopy.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._btnCopy.Name = "_btnCopy";
            this._btnCopy.Size = new System.Drawing.Size(23, 22);
            this._btnCopy.ToolTipText = "Copy selected rows to clipboard";
            this._btnCopy.Click += new System.EventHandler(this._btnCopy_Click);
            // 
            // _btnSave
            // 
            this._btnSave.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this._btnSave.Image = ((System.Drawing.Image)(resources.GetObject("_btnSave.Image")));
            this._btnSave.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._btnSave.Name = "_btnSave";
            this._btnSave.Size = new System.Drawing.Size(23, 22);
            this._btnSave.ToolTipText = "Save list to .csv";
            this._btnSave.Click += new System.EventHandler(this._btnSave_Click);
            // 
            // _txtFilter
            // 
            this._txtFilter.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this._txtFilter.Font = new System.Drawing.Font("Yu Gothic UI", 9F);
            this._txtFilter.ForeColor = System.Drawing.SystemColors.GrayText;
            this._txtFilter.Name = "_txtFilter";
            this._txtFilter.Size = new System.Drawing.Size(150, 25);
            this._txtFilter.Text = "Filter";
            this._txtFilter.Enter += new System.EventHandler(this._txtFilter_Enter);
            this._txtFilter.Leave += new System.EventHandler(this._txtFilter_Leave);
            this._txtFilter.TextChanged += new System.EventHandler(this._txtFilter_TextChanged);
            // 
            // _saveFileDialog
            // 
            this._saveFileDialog.Filter = "CSV file|*.csv";
            // 
            // AccessLogControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this._tvLog);
            this.Controls.Add(this._toolStrip);
            this.Controls.Add(this._btnExtract);
            this.Name = "AccessLogControl";
            this.Size = new System.Drawing.Size(703, 506);
            this._toolStrip.ResumeLayout(false);
            this._toolStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ToolStrip _toolStrip;
        private System.Windows.Forms.ToolStripButton _btnEnableLogging;
        private System.Windows.Forms.ToolStripButton _btnClearLists;
        protected LogListView _tvLog;
        private System.Windows.Forms.Button _btnExtract;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripButton _btnCopy;
        private System.Windows.Forms.ToolStripButton _btnSave;
        private System.Windows.Forms.SaveFileDialog _saveFileDialog;
        private System.Windows.Forms.ToolStripTextBox _txtFilter;
    }
}
