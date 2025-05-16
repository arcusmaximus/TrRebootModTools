namespace TrRebootTools.ModManager
{
    partial class VariationSelectionForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this._lblIntro = new System.Windows.Forms.Label();
            this._lstVariation = new System.Windows.Forms.ListBox();
            this._btnOK = new System.Windows.Forms.Button();
            this._btnCancel = new System.Windows.Forms.Button();
            this._pbPreview = new System.Windows.Forms.PictureBox();
            this._txtDescription = new System.Windows.Forms.TextBox();
            this._spltScreenshot = new System.Windows.Forms.SplitContainer();
            this._spltMain = new System.Windows.Forms.SplitContainer();
            ((System.ComponentModel.ISupportInitialize)(this._pbPreview)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._spltScreenshot)).BeginInit();
            this._spltScreenshot.Panel1.SuspendLayout();
            this._spltScreenshot.Panel2.SuspendLayout();
            this._spltScreenshot.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._spltMain)).BeginInit();
            this._spltMain.Panel1.SuspendLayout();
            this._spltMain.Panel2.SuspendLayout();
            this._spltMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // _lblIntro
            // 
            this._lblIntro.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this._lblIntro.Location = new System.Drawing.Point(10, 7);
            this._lblIntro.Name = "_lblIntro";
            this._lblIntro.Size = new System.Drawing.Size(567, 28);
            this._lblIntro.TabIndex = 0;
            this._lblIntro.Text = "(Intro)";
            // 
            // _lstVariation
            // 
            this._lstVariation.Dock = System.Windows.Forms.DockStyle.Fill;
            this._lstVariation.FormattingEnabled = true;
            this._lstVariation.IntegralHeight = false;
            this._lstVariation.ItemHeight = 12;
            this._lstVariation.Location = new System.Drawing.Point(0, 0);
            this._lstVariation.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this._lstVariation.Name = "_lstVariation";
            this._lstVariation.Size = new System.Drawing.Size(188, 312);
            this._lstVariation.TabIndex = 0;
            this._lstVariation.SelectedIndexChanged += new System.EventHandler(this._lstVariation_SelectedIndexChanged);
            this._lstVariation.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this._lstVariation_MouseDoubleClick);
            // 
            // _btnOK
            // 
            this._btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._btnOK.Enabled = false;
            this._btnOK.Location = new System.Drawing.Point(377, 355);
            this._btnOK.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this._btnOK.Name = "_btnOK";
            this._btnOK.Size = new System.Drawing.Size(98, 34);
            this._btnOK.TabIndex = 2;
            this._btnOK.Text = "OK";
            this._btnOK.UseVisualStyleBackColor = true;
            this._btnOK.Click += new System.EventHandler(this._btnOK_Click);
            // 
            // _btnCancel
            // 
            this._btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._btnCancel.Location = new System.Drawing.Point(480, 355);
            this._btnCancel.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this._btnCancel.Name = "_btnCancel";
            this._btnCancel.Size = new System.Drawing.Size(98, 34);
            this._btnCancel.TabIndex = 3;
            this._btnCancel.Text = "Cancel";
            this._btnCancel.UseVisualStyleBackColor = true;
            this._btnCancel.Click += new System.EventHandler(this._btnCancel_Click);
            // 
            // _pbPreview
            // 
            this._pbPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this._pbPreview.Location = new System.Drawing.Point(0, 0);
            this._pbPreview.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this._pbPreview.Name = "_pbPreview";
            this._pbPreview.Size = new System.Drawing.Size(374, 230);
            this._pbPreview.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this._pbPreview.TabIndex = 3;
            this._pbPreview.TabStop = false;
            // 
            // _txtDescription
            // 
            this._txtDescription.Dock = System.Windows.Forms.DockStyle.Fill;
            this._txtDescription.Location = new System.Drawing.Point(0, 0);
            this._txtDescription.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this._txtDescription.Multiline = true;
            this._txtDescription.Name = "_txtDescription";
            this._txtDescription.ReadOnly = true;
            this._txtDescription.Size = new System.Drawing.Size(374, 79);
            this._txtDescription.TabIndex = 0;
            // 
            // _spltScreenshot
            // 
            this._spltScreenshot.Dock = System.Windows.Forms.DockStyle.Fill;
            this._spltScreenshot.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this._spltScreenshot.Location = new System.Drawing.Point(0, 0);
            this._spltScreenshot.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this._spltScreenshot.Name = "_spltScreenshot";
            this._spltScreenshot.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // _spltScreenshot.Panel1
            // 
            this._spltScreenshot.Panel1.Controls.Add(this._pbPreview);
            // 
            // _spltScreenshot.Panel2
            // 
            this._spltScreenshot.Panel2.Controls.Add(this._txtDescription);
            this._spltScreenshot.Size = new System.Drawing.Size(374, 312);
            this._spltScreenshot.SplitterDistance = 230;
            this._spltScreenshot.SplitterWidth = 3;
            this._spltScreenshot.TabIndex = 1;
            // 
            // _spltMain
            // 
            this._spltMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this._spltMain.Location = new System.Drawing.Point(12, 38);
            this._spltMain.Name = "_spltMain";
            // 
            // _spltMain.Panel1
            // 
            this._spltMain.Panel1.Controls.Add(this._lstVariation);
            // 
            // _spltMain.Panel2
            // 
            this._spltMain.Panel2.Controls.Add(this._spltScreenshot);
            this._spltMain.Size = new System.Drawing.Size(566, 312);
            this._spltMain.SplitterDistance = 188;
            this._spltMain.TabIndex = 4;
            // 
            // VariationSelectionForm
            // 
            this.AcceptButton = this._btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._btnCancel;
            this.ClientSize = new System.Drawing.Size(588, 398);
            this.Controls.Add(this._spltMain);
            this.Controls.Add(this._btnCancel);
            this.Controls.Add(this._btnOK);
            this.Controls.Add(this._lblIntro);
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.MinimumSize = new System.Drawing.Size(422, 288);
            this.Name = "VariationSelectionForm";
            this.ShowIcon = false;
            this.Text = "Select mod variation";
            ((System.ComponentModel.ISupportInitialize)(this._pbPreview)).EndInit();
            this._spltScreenshot.Panel1.ResumeLayout(false);
            this._spltScreenshot.Panel2.ResumeLayout(false);
            this._spltScreenshot.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._spltScreenshot)).EndInit();
            this._spltScreenshot.ResumeLayout(false);
            this._spltMain.Panel1.ResumeLayout(false);
            this._spltMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._spltMain)).EndInit();
            this._spltMain.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label _lblIntro;
        private System.Windows.Forms.ListBox _lstVariation;
        private System.Windows.Forms.Button _btnOK;
        private System.Windows.Forms.Button _btnCancel;
        private System.Windows.Forms.PictureBox _pbPreview;
        private System.Windows.Forms.TextBox _txtDescription;
        private System.Windows.Forms.SplitContainer _spltScreenshot;
        private System.Windows.Forms.SplitContainer _spltMain;
    }
}
