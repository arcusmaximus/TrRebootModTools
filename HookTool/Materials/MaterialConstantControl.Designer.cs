using TrRebootTools.Shared.Controls;
using TrRebootTools.HookTool;

namespace TrRebootTools.HookTool.Materials
{
    partial class MaterialConstantControl
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
            this._layout = new System.Windows.Forms.TableLayoutPanel();
            this._lblIndex = new System.Windows.Forms.Label();
            this._xControl = new TrRebootTools.Shared.Controls.DraggableNumberControl();
            this._yControl = new TrRebootTools.Shared.Controls.DraggableNumberControl();
            this._zControl = new TrRebootTools.Shared.Controls.DraggableNumberControl();
            this._wControl = new TrRebootTools.Shared.Controls.DraggableNumberControl();
            this._layout.SuspendLayout();
            this.SuspendLayout();
            // 
            // _layout
            // 
            this._layout.ColumnCount = 5;
            this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this._layout.Controls.Add(this._lblIndex, 0, 0);
            this._layout.Controls.Add(this._xControl, 1, 0);
            this._layout.Controls.Add(this._yControl, 2, 0);
            this._layout.Controls.Add(this._zControl, 3, 0);
            this._layout.Controls.Add(this._wControl, 4, 0);
            this._layout.Dock = System.Windows.Forms.DockStyle.Fill;
            this._layout.Location = new System.Drawing.Point(0, 0);
            this._layout.Name = "_layout";
            this._layout.RowCount = 1;
            this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._layout.Size = new System.Drawing.Size(670, 33);
            this._layout.TabIndex = 0;
            // 
            // _lblIndex
            // 
            this._lblIndex.Dock = System.Windows.Forms.DockStyle.Fill;
            this._lblIndex.Location = new System.Drawing.Point(3, 0);
            this._lblIndex.Name = "_lblIndex";
            this._lblIndex.Size = new System.Drawing.Size(44, 33);
            this._lblIndex.TabIndex = 0;
            this._lblIndex.Text = "0";
            this._lblIndex.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // _xControl
            // 
            this._xControl.Cursor = System.Windows.Forms.Cursors.SizeWE;
            this._xControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this._xControl.Location = new System.Drawing.Point(53, 3);
            this._xControl.Name = "_xControl";
            this._xControl.Size = new System.Drawing.Size(149, 27);
            this._xControl.TabIndex = 1;
            this._xControl.Value = 0F;
            // 
            // _yControl
            // 
            this._yControl.Cursor = System.Windows.Forms.Cursors.SizeWE;
            this._yControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this._yControl.Location = new System.Drawing.Point(208, 3);
            this._yControl.Name = "_yControl";
            this._yControl.Size = new System.Drawing.Size(149, 27);
            this._yControl.TabIndex = 2;
            this._yControl.Value = 0F;
            // 
            // _zControl
            // 
            this._zControl.Cursor = System.Windows.Forms.Cursors.SizeWE;
            this._zControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this._zControl.Location = new System.Drawing.Point(363, 3);
            this._zControl.Name = "_zControl";
            this._zControl.Size = new System.Drawing.Size(149, 27);
            this._zControl.TabIndex = 3;
            this._zControl.Value = 0F;
            // 
            // _wControl
            // 
            this._wControl.Cursor = System.Windows.Forms.Cursors.SizeWE;
            this._wControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this._wControl.Location = new System.Drawing.Point(518, 3);
            this._wControl.Name = "_wControl";
            this._wControl.Size = new System.Drawing.Size(149, 27);
            this._wControl.TabIndex = 4;
            this._wControl.Value = 0F;
            // 
            // MaterialConstantControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this._layout);
            this.Name = "MaterialConstantControl";
            this.Size = new System.Drawing.Size(670, 33);
            this._layout.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel _layout;
        private System.Windows.Forms.Label _lblIndex;
        private DraggableNumberControl _xControl;
        private DraggableNumberControl _yControl;
        private DraggableNumberControl _zControl;
        private DraggableNumberControl _wControl;
    }
}
