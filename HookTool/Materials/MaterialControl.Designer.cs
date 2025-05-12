using TrRebootTools.HookTool;
namespace TrRebootTools.HookTool.Materials
{
    partial class MaterialControl
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MaterialControl));
            TrRebootTools.Shared.Controls.VirtualTreeView.MiscOptionHelper miscOptionHelper1 = new TrRebootTools.Shared.Controls.VirtualTreeView.MiscOptionHelper();
            TrRebootTools.Shared.Controls.VirtualTreeView.PaintOptionHelper paintOptionHelper1 = new TrRebootTools.Shared.Controls.VirtualTreeView.PaintOptionHelper();
            this._spltMain = new System.Windows.Forms.SplitContainer();
            this._pnlMaterials = new System.Windows.Forms.TableLayoutPanel();
            this._lvPasses = new TrRebootTools.Shared.Controls.VirtualTreeView.VirtualTreeView();
            this._tvMaterials = new TrRebootTools.Shared.Controls.FsFileTreeView();
            this._constantsControl = new TrRebootTools.HookTool.Materials.MaterialConstantArrayControl();
            this._pnlShaderTypes = new System.Windows.Forms.TableLayoutPanel();
            this._radVertexShader = new System.Windows.Forms.RadioButton();
            this._radPixelShader = new System.Windows.Forms.RadioButton();
            this._lblMaterial = new System.Windows.Forms.Label();
            this._pnlActions = new System.Windows.Forms.Panel();
            this._btnSave = new System.Windows.Forms.Button();
            this._btnRevert = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this._spltMain)).BeginInit();
            this._spltMain.Panel1.SuspendLayout();
            this._spltMain.Panel2.SuspendLayout();
            this._spltMain.SuspendLayout();
            this._pnlMaterials.SuspendLayout();
            this._pnlShaderTypes.SuspendLayout();
            this._pnlActions.SuspendLayout();
            this.SuspendLayout();
            // 
            // _spltMain
            // 
            this._spltMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this._spltMain.Location = new System.Drawing.Point(0, 0);
            this._spltMain.Name = "_spltMain";
            // 
            // _spltMain.Panel1
            // 
            this._spltMain.Panel1.Controls.Add(this._pnlMaterials);
            // 
            // _spltMain.Panel2
            // 
            this._spltMain.Panel2.Controls.Add(this._constantsControl);
            this._spltMain.Panel2.Controls.Add(this._pnlShaderTypes);
            this._spltMain.Panel2.Controls.Add(this._lblMaterial);
            this._spltMain.Panel2.Controls.Add(this._pnlActions);
            this._spltMain.Size = new System.Drawing.Size(782, 500);
            this._spltMain.SplitterDistance = 330;
            this._spltMain.TabIndex = 1;
            // 
            // _pnlMaterials
            // 
            this._pnlMaterials.ColumnCount = 2;
            this._pnlMaterials.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._pnlMaterials.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this._pnlMaterials.Controls.Add(this._lvPasses, 1, 0);
            this._pnlMaterials.Controls.Add(this._tvMaterials, 0, 0);
            this._pnlMaterials.Dock = System.Windows.Forms.DockStyle.Fill;
            this._pnlMaterials.Location = new System.Drawing.Point(0, 0);
            this._pnlMaterials.Name = "_pnlMaterials";
            this._pnlMaterials.RowCount = 1;
            this._pnlMaterials.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._pnlMaterials.Size = new System.Drawing.Size(330, 500);
            this._pnlMaterials.TabIndex = 1;
            // 
            // _lvPasses
            // 
            this._lvPasses.ActiveNode = null;
            this._lvPasses.Back2Color = System.Drawing.SystemColors.Window;
            this._lvPasses.BackColor = System.Drawing.SystemColors.Window;
            this._lvPasses.ButtonStyle = TrRebootTools.Shared.Controls.VirtualTreeView.ButtonStyle.bsRectangle;
            this._lvPasses.Dock = System.Windows.Forms.DockStyle.Fill;
            this._lvPasses.Header.BackColor = System.Drawing.SystemColors.ButtonFace;
            this._lvPasses.Header.Columns = ((System.Collections.Generic.List<TrRebootTools.Shared.Controls.VirtualTreeView.VirtualTreeColumn>)(resources.GetObject("resource.Columns")));
            this._lvPasses.Header.Font = new System.Drawing.Font("Tahoma", 8F);
            this._lvPasses.Header.ForeColor = System.Drawing.Color.Black;
            this._lvPasses.Header.Height = 0;
            this._lvPasses.Header.Visible = false;
            this._lvPasses.LineColor = System.Drawing.Color.Silver;
            this._lvPasses.LineWidth = 1F;
            this._lvPasses.Location = new System.Drawing.Point(233, 3);
            this._lvPasses.Name = "_lvPasses";
            miscOptionHelper1.Editable = false;
            miscOptionHelper1.MultiSelect = false;
            this._lvPasses.Options.Misc = miscOptionHelper1;
            paintOptionHelper1.Back2Color = false;
            paintOptionHelper1.FullVertGridLines = false;
            paintOptionHelper1.ShowButtons = false;
            paintOptionHelper1.ShowHorzGridLines = false;
            this._lvPasses.Options.Paint = paintOptionHelper1;
            this._lvPasses.ShowHint = true;
            this._lvPasses.Size = new System.Drawing.Size(94, 494);
            this._lvPasses.TabIndex = 1;
            this._lvPasses.OnSelectionChanged += new System.EventHandler(this._lvPasses_OnSelectionChanged);
            // 
            // _tvMaterials
            // 
            this._tvMaterials.Dock = System.Windows.Forms.DockStyle.Fill;
            this._tvMaterials.Location = new System.Drawing.Point(3, 3);
            this._tvMaterials.MultiSelect = false;
            this._tvMaterials.Name = "_tvMaterials";
            this._tvMaterials.Size = new System.Drawing.Size(224, 494);
            this._tvMaterials.TabIndex = 2;
            this._tvMaterials.SelectionChanged += new System.EventHandler(this._tvMaterials_SelectionChanged);
            // 
            // _constantsControl
            // 
            this._constantsControl.AutoScroll = true;
            this._constantsControl.DataSource = null;
            this._constantsControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this._constantsControl.Location = new System.Drawing.Point(0, 71);
            this._constantsControl.Name = "_constantsControl";
            this._constantsControl.Size = new System.Drawing.Size(448, 372);
            this._constantsControl.TabIndex = 5;
            // 
            // _pnlShaderTypes
            // 
            this._pnlShaderTypes.ColumnCount = 2;
            this._pnlShaderTypes.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this._pnlShaderTypes.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this._pnlShaderTypes.Controls.Add(this._radVertexShader, 1, 0);
            this._pnlShaderTypes.Controls.Add(this._radPixelShader, 0, 0);
            this._pnlShaderTypes.Dock = System.Windows.Forms.DockStyle.Top;
            this._pnlShaderTypes.Enabled = false;
            this._pnlShaderTypes.Location = new System.Drawing.Point(0, 34);
            this._pnlShaderTypes.Name = "_pnlShaderTypes";
            this._pnlShaderTypes.RowCount = 1;
            this._pnlShaderTypes.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this._pnlShaderTypes.Size = new System.Drawing.Size(448, 37);
            this._pnlShaderTypes.TabIndex = 4;
            // 
            // _radVertexShader
            // 
            this._radVertexShader.Anchor = System.Windows.Forms.AnchorStyles.None;
            this._radVertexShader.AutoSize = true;
            this._radVertexShader.Location = new System.Drawing.Point(288, 10);
            this._radVertexShader.Name = "_radVertexShader";
            this._radVertexShader.Size = new System.Drawing.Size(95, 16);
            this._radVertexShader.TabIndex = 1;
            this._radVertexShader.Text = "Vertex shader";
            this._radVertexShader.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this._radVertexShader.UseVisualStyleBackColor = true;
            // 
            // _radPixelShader
            // 
            this._radPixelShader.Anchor = System.Windows.Forms.AnchorStyles.None;
            this._radPixelShader.AutoSize = true;
            this._radPixelShader.Checked = true;
            this._radPixelShader.Location = new System.Drawing.Point(69, 10);
            this._radPixelShader.Name = "_radPixelShader";
            this._radPixelShader.Size = new System.Drawing.Size(86, 16);
            this._radPixelShader.TabIndex = 0;
            this._radPixelShader.TabStop = true;
            this._radPixelShader.Text = "Pixel shader";
            this._radPixelShader.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this._radPixelShader.UseVisualStyleBackColor = true;
            this._radPixelShader.CheckedChanged += new System.EventHandler(this._radPixelShader_CheckedChanged);
            // 
            // _lblMaterial
            // 
            this._lblMaterial.BackColor = System.Drawing.SystemColors.ControlDark;
            this._lblMaterial.Dock = System.Windows.Forms.DockStyle.Top;
            this._lblMaterial.Location = new System.Drawing.Point(0, 0);
            this._lblMaterial.Name = "_lblMaterial";
            this._lblMaterial.Size = new System.Drawing.Size(448, 34);
            this._lblMaterial.TabIndex = 0;
            this._lblMaterial.Text = "Material";
            this._lblMaterial.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // _pnlActions
            // 
            this._pnlActions.Controls.Add(this._btnSave);
            this._pnlActions.Controls.Add(this._btnRevert);
            this._pnlActions.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._pnlActions.Location = new System.Drawing.Point(0, 443);
            this._pnlActions.Name = "_pnlActions";
            this._pnlActions.Size = new System.Drawing.Size(448, 57);
            this._pnlActions.TabIndex = 3;
            // 
            // _btnSave
            // 
            this._btnSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this._btnSave.Location = new System.Drawing.Point(210, 9);
            this._btnSave.Name = "_btnSave";
            this._btnSave.Size = new System.Drawing.Size(111, 38);
            this._btnSave.TabIndex = 0;
            this._btnSave.Text = "Save";
            this._btnSave.UseVisualStyleBackColor = true;
            this._btnSave.Click += new System.EventHandler(this._btnSave_Click);
            // 
            // _btnRevert
            // 
            this._btnRevert.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this._btnRevert.Location = new System.Drawing.Point(327, 9);
            this._btnRevert.Name = "_btnRevert";
            this._btnRevert.Size = new System.Drawing.Size(111, 38);
            this._btnRevert.TabIndex = 1;
            this._btnRevert.Text = "Revert changes";
            this._btnRevert.UseVisualStyleBackColor = true;
            this._btnRevert.Click += new System.EventHandler(this._btnRevert_Click);
            // 
            // MaterialControl
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this._spltMain);
            this.Name = "MaterialControl";
            this.Size = new System.Drawing.Size(782, 500);
            this._spltMain.Panel1.ResumeLayout(false);
            this._spltMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._spltMain)).EndInit();
            this._spltMain.ResumeLayout(false);
            this._pnlMaterials.ResumeLayout(false);
            this._pnlShaderTypes.ResumeLayout(false);
            this._pnlShaderTypes.PerformLayout();
            this._pnlActions.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.SplitContainer _spltMain;
        private System.Windows.Forms.Label _lblMaterial;
        private System.Windows.Forms.RadioButton _radPixelShader;
        private System.Windows.Forms.RadioButton _radVertexShader;
        private System.Windows.Forms.Panel _pnlActions;
        private System.Windows.Forms.Button _btnSave;
        private System.Windows.Forms.Button _btnRevert;
        private System.Windows.Forms.TableLayoutPanel _pnlMaterials;
        private Shared.Controls.VirtualTreeView.VirtualTreeView _lvPasses;
        private System.Windows.Forms.TableLayoutPanel _pnlShaderTypes;
        private MaterialConstantArrayControl _constantsControl;
        private Shared.Controls.FsFileTreeView _tvMaterials;
    }
}
