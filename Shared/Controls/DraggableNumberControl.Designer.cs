namespace TrRebootTools.Shared.Controls
{
    partial class DraggableNumberControl
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
            this._txtValue = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // _txtValue
            // 
            this._txtValue.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._txtValue.Location = new System.Drawing.Point(17, 6);
            this._txtValue.Name = "_txtValue";
            this._txtValue.Size = new System.Drawing.Size(260, 19);
            this._txtValue.TabIndex = 0;
            this._txtValue.Visible = false;
            this._txtValue.KeyDown += new System.Windows.Forms.KeyEventHandler(this._txtValue_KeyDown);
            this._txtValue.Leave += new System.EventHandler(this._txtValue_Leave);
            // 
            // DraggableNumberControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this._txtValue);
            this.Cursor = System.Windows.Forms.Cursors.SizeWE;
            this.DoubleBuffered = true;
            this.Name = "DraggableNumberControl";
            this.Size = new System.Drawing.Size(293, 31);
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.DraggableNumberControl_Paint);
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.DraggableNumberControl_MouseDown);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.DraggableNumberControl_MouseMove);
            this.MouseUp += new System.Windows.Forms.MouseEventHandler(this.DraggableNumberControl_MouseUp);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox _txtValue;
    }
}
