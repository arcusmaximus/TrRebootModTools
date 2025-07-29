using TrRebootTools.SoundConverter;

namespace TrRebootTools.SoundConverter
{
    partial class MainForm
    {
        /// <summary>
        /// 必要なデザイナー変数です。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        #region Windows フォーム デザイナーで生成されたコード

        /// <summary>
        /// デザイナー サポートに必要なメソッドです。このメソッドの内容を
        /// コード エディターで変更しないでください。
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this._lstInputFiles = new System.Windows.Forms.ListBox();
            this._lblWavFiles = new System.Windows.Forms.Label();
            this._lblOutputFolder = new System.Windows.Forms.Label();
            this._btnAddInputFiles = new System.Windows.Forms.Button();
            this._btnRemoveSelectedInputFiles = new System.Windows.Forms.Button();
            this._btnClearInputFiles = new System.Windows.Forms.Button();
            this._txtOutputFolder = new System.Windows.Forms.TextBox();
            this._btnBrowseOutputFolder = new System.Windows.Forms.Button();
            this._btnConvert = new System.Windows.Forms.Button();
            this._progressBar = new System.Windows.Forms.ProgressBar();
            this._btnCancel = new System.Windows.Forms.Button();
            this._pnlOptions = new System.Windows.Forms.Panel();
            this._radShadow = new System.Windows.Forms.RadioButton();
            this._radRise = new System.Windows.Forms.RadioButton();
            this._radTr2013 = new System.Windows.Forms.RadioButton();
            this.label1 = new System.Windows.Forms.Label();
            this._dlgSelectInputFiles = new System.Windows.Forms.OpenFileDialog();
            this._dlgSelectOutputFolder = new Ookii.Dialogs.WinForms.VistaFolderBrowserDialog();
            this._pnlOptions.SuspendLayout();
            this.SuspendLayout();
            // 
            // _lstInputFiles
            // 
            this._lstInputFiles.AllowDrop = true;
            this._lstInputFiles.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this._lstInputFiles.FormattingEnabled = true;
            this._lstInputFiles.IntegralHeight = false;
            this._lstInputFiles.ItemHeight = 12;
            this._lstInputFiles.Location = new System.Drawing.Point(8, 65);
            this._lstInputFiles.Name = "_lstInputFiles";
            this._lstInputFiles.SelectionMode = System.Windows.Forms.SelectionMode.MultiSimple;
            this._lstInputFiles.Size = new System.Drawing.Size(660, 185);
            this._lstInputFiles.TabIndex = 0;
            this._lstInputFiles.DragDrop += new System.Windows.Forms.DragEventHandler(this._lstInputFiles_DragDrop);
            this._lstInputFiles.DragEnter += new System.Windows.Forms.DragEventHandler(this._lstInputFiles_DragEnter);
            // 
            // _lblWavFiles
            // 
            this._lblWavFiles.AutoSize = true;
            this._lblWavFiles.Location = new System.Drawing.Point(3, 50);
            this._lblWavFiles.Name = "_lblWavFiles";
            this._lblWavFiles.Size = new System.Drawing.Size(58, 12);
            this._lblWavFiles.TabIndex = 1;
            this._lblWavFiles.Text = "Input files:";
            // 
            // _lblOutputFolder
            // 
            this._lblOutputFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this._lblOutputFolder.AutoSize = true;
            this._lblOutputFolder.Location = new System.Drawing.Point(6, 320);
            this._lblOutputFolder.Name = "_lblOutputFolder";
            this._lblOutputFolder.Size = new System.Drawing.Size(74, 12);
            this._lblOutputFolder.TabIndex = 2;
            this._lblOutputFolder.Text = "Output folder:";
            // 
            // _btnAddInputFiles
            // 
            this._btnAddInputFiles.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this._btnAddInputFiles.Location = new System.Drawing.Point(8, 256);
            this._btnAddInputFiles.Name = "_btnAddInputFiles";
            this._btnAddInputFiles.Size = new System.Drawing.Size(124, 36);
            this._btnAddInputFiles.TabIndex = 3;
            this._btnAddInputFiles.Text = "Add files...";
            this._btnAddInputFiles.UseVisualStyleBackColor = true;
            this._btnAddInputFiles.Click += new System.EventHandler(this._btnAddInputFiles_Click);
            // 
            // _btnRemoveSelectedInputFiles
            // 
            this._btnRemoveSelectedInputFiles.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._btnRemoveSelectedInputFiles.Location = new System.Drawing.Point(391, 256);
            this._btnRemoveSelectedInputFiles.Name = "_btnRemoveSelectedInputFiles";
            this._btnRemoveSelectedInputFiles.Size = new System.Drawing.Size(151, 36);
            this._btnRemoveSelectedInputFiles.TabIndex = 3;
            this._btnRemoveSelectedInputFiles.Text = "Remove selected";
            this._btnRemoveSelectedInputFiles.UseVisualStyleBackColor = true;
            this._btnRemoveSelectedInputFiles.Click += new System.EventHandler(this._btnRemoveSelectedInputFiles_Click);
            // 
            // _btnClearInputFiles
            // 
            this._btnClearInputFiles.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._btnClearInputFiles.Location = new System.Drawing.Point(548, 256);
            this._btnClearInputFiles.Name = "_btnClearInputFiles";
            this._btnClearInputFiles.Size = new System.Drawing.Size(120, 36);
            this._btnClearInputFiles.TabIndex = 3;
            this._btnClearInputFiles.Text = "Clear";
            this._btnClearInputFiles.UseVisualStyleBackColor = true;
            this._btnClearInputFiles.Click += new System.EventHandler(this._btnClearInputFiles_Click);
            // 
            // _txtOutputFolder
            // 
            this._txtOutputFolder.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this._txtOutputFolder.Location = new System.Drawing.Point(8, 335);
            this._txtOutputFolder.Name = "_txtOutputFolder";
            this._txtOutputFolder.Size = new System.Drawing.Size(613, 19);
            this._txtOutputFolder.TabIndex = 4;
            // 
            // _btnBrowseOutputFolder
            // 
            this._btnBrowseOutputFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._btnBrowseOutputFolder.Location = new System.Drawing.Point(627, 334);
            this._btnBrowseOutputFolder.Name = "_btnBrowseOutputFolder";
            this._btnBrowseOutputFolder.Size = new System.Drawing.Size(41, 20);
            this._btnBrowseOutputFolder.TabIndex = 5;
            this._btnBrowseOutputFolder.Text = "...";
            this._btnBrowseOutputFolder.UseVisualStyleBackColor = true;
            this._btnBrowseOutputFolder.Click += new System.EventHandler(this._btnBrowseOutputFolder_Click);
            // 
            // _btnConvert
            // 
            this._btnConvert.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this._btnConvert.Location = new System.Drawing.Point(277, 391);
            this._btnConvert.Name = "_btnConvert";
            this._btnConvert.Size = new System.Drawing.Size(141, 42);
            this._btnConvert.TabIndex = 6;
            this._btnConvert.Text = "Convert";
            this._btnConvert.UseVisualStyleBackColor = true;
            this._btnConvert.Click += new System.EventHandler(this._btnConvert_Click);
            // 
            // _progressBar
            // 
            this._progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this._progressBar.Location = new System.Drawing.Point(14, 398);
            this._progressBar.Name = "_progressBar";
            this._progressBar.Size = new System.Drawing.Size(558, 29);
            this._progressBar.Step = 1;
            this._progressBar.TabIndex = 7;
            this._progressBar.Visible = false;
            // 
            // _btnCancel
            // 
            this._btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._btnCancel.Location = new System.Drawing.Point(578, 398);
            this._btnCancel.Name = "_btnCancel";
            this._btnCancel.Size = new System.Drawing.Size(102, 29);
            this._btnCancel.TabIndex = 8;
            this._btnCancel.Text = "Cancel";
            this._btnCancel.UseVisualStyleBackColor = true;
            this._btnCancel.Visible = false;
            this._btnCancel.Click += new System.EventHandler(this._btnCancel_Click);
            // 
            // _pnlOptions
            // 
            this._pnlOptions.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this._pnlOptions.Controls.Add(this._radShadow);
            this._pnlOptions.Controls.Add(this._radRise);
            this._pnlOptions.Controls.Add(this._radTr2013);
            this._pnlOptions.Controls.Add(this._btnBrowseOutputFolder);
            this._pnlOptions.Controls.Add(this._txtOutputFolder);
            this._pnlOptions.Controls.Add(this._btnClearInputFiles);
            this._pnlOptions.Controls.Add(this._btnRemoveSelectedInputFiles);
            this._pnlOptions.Controls.Add(this._btnAddInputFiles);
            this._pnlOptions.Controls.Add(this._lblOutputFolder);
            this._pnlOptions.Controls.Add(this.label1);
            this._pnlOptions.Controls.Add(this._lblWavFiles);
            this._pnlOptions.Controls.Add(this._lstInputFiles);
            this._pnlOptions.Location = new System.Drawing.Point(12, 6);
            this._pnlOptions.Name = "_pnlOptions";
            this._pnlOptions.Size = new System.Drawing.Size(670, 379);
            this._pnlOptions.TabIndex = 9;
            // 
            // _radShadow
            // 
            this._radShadow.AutoSize = true;
            this._radShadow.Location = new System.Drawing.Point(192, 18);
            this._radShadow.Name = "_radShadow";
            this._radShadow.Size = new System.Drawing.Size(60, 16);
            this._radShadow.TabIndex = 6;
            this._radShadow.Text = "SOTTR";
            this._radShadow.UseVisualStyleBackColor = true;
            // 
            // _radRise
            // 
            this._radRise.AutoSize = true;
            this._radRise.Location = new System.Drawing.Point(97, 18);
            this._radRise.Name = "_radRise";
            this._radRise.Size = new System.Drawing.Size(61, 16);
            this._radRise.TabIndex = 6;
            this._radRise.Text = "ROTTR";
            this._radRise.UseVisualStyleBackColor = true;
            // 
            // _radTr2013
            // 
            this._radTr2013.AutoSize = true;
            this._radTr2013.Checked = true;
            this._radTr2013.Location = new System.Drawing.Point(8, 18);
            this._radTr2013.Name = "_radTr2013";
            this._radTr2013.Size = new System.Drawing.Size(62, 16);
            this._radTr2013.TabIndex = 6;
            this._radTr2013.TabStop = true;
            this._radTr2013.Text = "TR2013";
            this._radTr2013.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(3, 3);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(36, 12);
            this.label1.TabIndex = 1;
            this.label1.Text = "Game:";
            // 
            // _dlgSelectInputFiles
            // 
            this._dlgSelectInputFiles.Multiselect = true;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(694, 444);
            this.Controls.Add(this._btnConvert);
            this.Controls.Add(this._pnlOptions);
            this.Controls.Add(this._btnCancel);
            this.Controls.Add(this._progressBar);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MinimumSize = new System.Drawing.Size(590, 360);
            this.Name = "MainForm";
            this.Text = "TR Reboot Sound Converter";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this._pnlOptions.ResumeLayout(false);
            this._pnlOptions.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListBox _lstInputFiles;
        private System.Windows.Forms.Label _lblWavFiles;
        private System.Windows.Forms.Label _lblOutputFolder;
        private System.Windows.Forms.Button _btnAddInputFiles;
        private System.Windows.Forms.Button _btnRemoveSelectedInputFiles;
        private System.Windows.Forms.Button _btnClearInputFiles;
        private System.Windows.Forms.TextBox _txtOutputFolder;
        private System.Windows.Forms.Button _btnBrowseOutputFolder;
        private System.Windows.Forms.Button _btnConvert;
        private System.Windows.Forms.ProgressBar _progressBar;
        private System.Windows.Forms.Button _btnCancel;
        private System.Windows.Forms.Panel _pnlOptions;
        private System.Windows.Forms.OpenFileDialog _dlgSelectInputFiles;
        private Ookii.Dialogs.WinForms.VistaFolderBrowserDialog _dlgSelectOutputFolder;
        private System.Windows.Forms.RadioButton _radShadow;
        private System.Windows.Forms.RadioButton _radRise;
        private System.Windows.Forms.RadioButton _radTr2013;
        private System.Windows.Forms.Label label1;
    }
}

