
using TrRebootTools.Shared.Controls;


namespace TrRebootTools.Shared.Controls
{
    partial class FileTreeViewBase
    {
        /// <summary> 
        /// 必要なデザイナー変数です。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 使用中のリソースをすべてクリーンアップします。
        /// </summary>
        /// <param name="disposing">マネージド リソースを破棄する場合は true を指定し、その他の場合は false を指定します。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region コンポーネント デザイナーで生成されたコード

        /// <summary> 
        /// デザイナー サポートに必要なメソッドです。このメソッドの内容を 
        /// コード エディターで変更しないでください。
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FileTreeViewBase));
            TrRebootTools.Shared.Controls.VirtualTreeView.MiscOptionHelper miscOptionHelper1 = new TrRebootTools.Shared.Controls.VirtualTreeView.MiscOptionHelper();
            TrRebootTools.Shared.Controls.VirtualTreeView.PaintOptionHelper paintOptionHelper1 = new TrRebootTools.Shared.Controls.VirtualTreeView.PaintOptionHelper();
            this._tvFiles = new TrRebootTools.Shared.Controls.VirtualTreeView.VirtualTreeView();
            this._txtSearch = new System.Windows.Forms.TextBox();
            this._pbSearch = new System.Windows.Forms.PictureBox();
            this._pnlSearch = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this._pbSearch)).BeginInit();
            this._pnlSearch.SuspendLayout();
            this.SuspendLayout();
            // 
            // _tvFiles
            // 
            this._tvFiles.ActiveNode = null;
            this._tvFiles.Back2Color = System.Drawing.Color.FromArgb(((int)(((byte)(229)))), ((int)(((byte)(229)))), ((int)(((byte)(229)))));
            this._tvFiles.BackColor = System.Drawing.SystemColors.Window;
            this._tvFiles.ButtonStyle = TrRebootTools.Shared.Controls.VirtualTreeView.ButtonStyle.bsRectangle;
            this._tvFiles.Dock = System.Windows.Forms.DockStyle.Fill;
            this._tvFiles.Header.BackColor = System.Drawing.SystemColors.Window;
            this._tvFiles.Header.Font = new System.Drawing.Font("Tahoma", 8F);
            this._tvFiles.Header.ForeColor = System.Drawing.Color.Black;
            this._tvFiles.Header.Height = 1;
            this._tvFiles.Header.Visible = true;
            this._tvFiles.LineColor = System.Drawing.Color.Silver;
            this._tvFiles.LineWidth = 1F;
            this._tvFiles.Location = new System.Drawing.Point(0, 32);
            this._tvFiles.Name = "_tvFiles";
            miscOptionHelper1.Editable = false;
            miscOptionHelper1.MultiSelect = true;
            this._tvFiles.Options.Misc = miscOptionHelper1;
            paintOptionHelper1.Back2Color = false;
            paintOptionHelper1.FullVertGridLines = false;
            paintOptionHelper1.ShowButtons = true;
            paintOptionHelper1.ShowHorzGridLines = false;
            this._tvFiles.Options.Paint = paintOptionHelper1;
            this._tvFiles.ShowHint = true;
            this._tvFiles.Size = new System.Drawing.Size(467, 296);
            this._tvFiles.TabIndex = 1;
            // 
            // _txtSearch
            // 
            this._txtSearch.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._txtSearch.Location = new System.Drawing.Point(31, 6);
            this._txtSearch.Name = "_txtSearch";
            this._txtSearch.Size = new System.Drawing.Size(432, 19);
            this._txtSearch.TabIndex = 0;
            // 
            // _pbSearch
            // 
            this._pbSearch.Image = global::TrRebootTools.Shared.Properties.Resources.Search;
            this._pbSearch.Location = new System.Drawing.Point(2, 2);
            this._pbSearch.Name = "_pbSearch";
            this._pbSearch.Size = new System.Drawing.Size(24, 27);
            this._pbSearch.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this._pbSearch.TabIndex = 1;
            this._pbSearch.TabStop = false;
            // 
            // _pnlSearch
            // 
            this._pnlSearch.Controls.Add(this._pbSearch);
            this._pnlSearch.Controls.Add(this._txtSearch);
            this._pnlSearch.Dock = System.Windows.Forms.DockStyle.Top;
            this._pnlSearch.Location = new System.Drawing.Point(0, 0);
            this._pnlSearch.Name = "_pnlSearch";
            this._pnlSearch.Size = new System.Drawing.Size(467, 32);
            this._pnlSearch.TabIndex = 2;
            // 
            // FileTreeViewBase
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this._tvFiles);
            this.Controls.Add(this._pnlSearch);
            this.MinimumSize = new System.Drawing.Size(100, 100);
            this.Name = "FileTreeViewBase";
            this.Size = new System.Drawing.Size(467, 328);
            ((System.ComponentModel.ISupportInitialize)(this._pbSearch)).EndInit();
            this._pnlSearch.ResumeLayout(false);
            this._pnlSearch.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        protected TrRebootTools.Shared.Controls.VirtualTreeView.VirtualTreeView _tvFiles;
        protected System.Windows.Forms.TextBox _txtSearch;
        private System.Windows.Forms.PictureBox _pbSearch;
        private System.Windows.Forms.Panel _pnlSearch;
    }
}
