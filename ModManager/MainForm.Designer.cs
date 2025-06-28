using TrRebootTools.ModManager;
using TrRebootTools.Shared.Controls;

namespace TrRebootTools.ModManager
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this._pnlToolbar = new System.Windows.Forms.TableLayoutPanel();
            this._btnSelectGame = new System.Windows.Forms.Button();
            this._btnReinstall = new System.Windows.Forms.Button();
            this._btnRemove = new System.Windows.Forms.Button();
            this._btnAddFromZip = new System.Windows.Forms.Button();
            this._btnAddFromFolder = new System.Windows.Forms.Button();
            this._toolTip = new System.Windows.Forms.ToolTip(this.components);
            this._statusBar = new System.Windows.Forms.StatusStrip();
            this._lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this._progressBar = new System.Windows.Forms.ToolStripProgressBar();
            this._lvMods = new TrRebootTools.Shared.Controls.BindableListView();
            this._colModName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this._folderBrowser = new Ookii.Dialogs.WinForms.VistaFolderBrowserDialog();
            this._fileBrowser = new System.Windows.Forms.OpenFileDialog();
            this._pnlToolbar.SuspendLayout();
            this._statusBar.SuspendLayout();
            this.SuspendLayout();
            // 
            // _pnlToolbar
            // 
            this._pnlToolbar.ColumnCount = 5;
            this._pnlToolbar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this._pnlToolbar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this._pnlToolbar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this._pnlToolbar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._pnlToolbar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this._pnlToolbar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this._pnlToolbar.Controls.Add(this._btnSelectGame, 4, 0);
            this._pnlToolbar.Controls.Add(this._btnReinstall, 3, 0);
            this._pnlToolbar.Controls.Add(this._btnRemove, 2, 0);
            this._pnlToolbar.Controls.Add(this._btnAddFromZip, 0, 0);
            this._pnlToolbar.Controls.Add(this._btnAddFromFolder, 1, 0);
            this._pnlToolbar.Dock = System.Windows.Forms.DockStyle.Top;
            this._pnlToolbar.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.FixedSize;
            this._pnlToolbar.Location = new System.Drawing.Point(0, 0);
            this._pnlToolbar.Name = "_pnlToolbar";
            this._pnlToolbar.RowCount = 1;
            this._pnlToolbar.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._pnlToolbar.Size = new System.Drawing.Size(641, 80);
            this._pnlToolbar.TabIndex = 1;
            // 
            // _btnSelectGame
            // 
            this._btnSelectGame.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this._btnSelectGame.Dock = System.Windows.Forms.DockStyle.Fill;
            this._btnSelectGame.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this._btnSelectGame.Location = new System.Drawing.Point(564, 3);
            this._btnSelectGame.Name = "_btnSelectGame";
            this._btnSelectGame.Size = new System.Drawing.Size(74, 74);
            this._btnSelectGame.TabIndex = 4;
            this._toolTip.SetToolTip(this._btnSelectGame, "Switch to a different game");
            this._btnSelectGame.UseVisualStyleBackColor = true;
            this._btnSelectGame.Click += new System.EventHandler(this._btnSelectGame_Click);
            // 
            // _btnReinstall
            // 
            this._btnReinstall.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this._btnReinstall.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this._btnReinstall.Image = global::TrRebootTools.ModManager.Properties.Resources.Reinstall;
            this._btnReinstall.Location = new System.Drawing.Point(484, 3);
            this._btnReinstall.Name = "_btnReinstall";
            this._btnReinstall.Size = new System.Drawing.Size(74, 74);
            this._btnReinstall.TabIndex = 3;
            this._toolTip.SetToolTip(this._btnReinstall, "Reinstall all mods (may fix game crashes if things worked before)");
            this._btnReinstall.UseVisualStyleBackColor = true;
            this._btnReinstall.Click += new System.EventHandler(this._btnReinstall_Click);
            // 
            // _btnRemove
            // 
            this._btnRemove.Dock = System.Windows.Forms.DockStyle.Fill;
            this._btnRemove.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this._btnRemove.Image = ((System.Drawing.Image)(resources.GetObject("_btnRemove.Image")));
            this._btnRemove.Location = new System.Drawing.Point(163, 3);
            this._btnRemove.Name = "_btnRemove";
            this._btnRemove.Size = new System.Drawing.Size(74, 74);
            this._btnRemove.TabIndex = 2;
            this._toolTip.SetToolTip(this._btnRemove, "Uninstall selected mods");
            this._btnRemove.UseVisualStyleBackColor = true;
            this._btnRemove.Click += new System.EventHandler(this._btnRemove_Click);
            // 
            // _btnAddFromZip
            // 
            this._btnAddFromZip.Dock = System.Windows.Forms.DockStyle.Fill;
            this._btnAddFromZip.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this._btnAddFromZip.Image = global::TrRebootTools.ModManager.Properties.Resources.AddZip;
            this._btnAddFromZip.Location = new System.Drawing.Point(3, 3);
            this._btnAddFromZip.Name = "_btnAddFromZip";
            this._btnAddFromZip.Size = new System.Drawing.Size(74, 74);
            this._btnAddFromZip.TabIndex = 0;
            this._toolTip.SetToolTip(this._btnAddFromZip, "Install mod from archive file (.7z/.zip/.rar)...");
            this._btnAddFromZip.UseVisualStyleBackColor = true;
            this._btnAddFromZip.Click += new System.EventHandler(this._btnAddFromZip_Click);
            // 
            // _btnAddFromFolder
            // 
            this._btnAddFromFolder.Dock = System.Windows.Forms.DockStyle.Fill;
            this._btnAddFromFolder.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this._btnAddFromFolder.Image = ((System.Drawing.Image)(resources.GetObject("_btnAddFromFolder.Image")));
            this._btnAddFromFolder.Location = new System.Drawing.Point(83, 3);
            this._btnAddFromFolder.Name = "_btnAddFromFolder";
            this._btnAddFromFolder.Size = new System.Drawing.Size(74, 74);
            this._btnAddFromFolder.TabIndex = 1;
            this._toolTip.SetToolTip(this._btnAddFromFolder, "Install mod from folder...");
            this._btnAddFromFolder.UseVisualStyleBackColor = true;
            this._btnAddFromFolder.Click += new System.EventHandler(this._btnAddFromFolder_Click);
            // 
            // _statusBar
            // 
            this._statusBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._lblStatus,
            this._progressBar});
            this._statusBar.Location = new System.Drawing.Point(0, 408);
            this._statusBar.Name = "_statusBar";
            this._statusBar.Size = new System.Drawing.Size(641, 22);
            this._statusBar.TabIndex = 2;
            this._statusBar.Text = "statusStrip1";
            // 
            // _lblStatus
            // 
            this._lblStatus.Name = "_lblStatus";
            this._lblStatus.Size = new System.Drawing.Size(626, 17);
            this._lblStatus.Spring = true;
            this._lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _progressBar
            // 
            this._progressBar.Maximum = 1000;
            this._progressBar.Name = "_progressBar";
            this._progressBar.Size = new System.Drawing.Size(100, 16);
            this._progressBar.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this._progressBar.Visible = false;
            // 
            // _lvMods
            // 
            this._lvMods.AllowDrop = true;
            this._lvMods.CheckBoxes = true;
            this._lvMods.CheckedMember = null;
            this._lvMods.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this._colModName});
            this._lvMods.DataSource = null;
            this._lvMods.DisplayMember = null;
            this._lvMods.Dock = System.Windows.Forms.DockStyle.Fill;
            this._lvMods.ForeColorMember = null;
            this._lvMods.FullRowSelect = true;
            this._lvMods.HideSelection = false;
            this._lvMods.Location = new System.Drawing.Point(0, 80);
            this._lvMods.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this._lvMods.Name = "_lvMods";
            this._lvMods.Size = new System.Drawing.Size(641, 328);
            this._lvMods.TabIndex = 3;
            this._lvMods.UseCompatibleStateImageBehavior = false;
            this._lvMods.View = System.Windows.Forms.View.Details;
            this._lvMods.DragDrop += new System.Windows.Forms.DragEventHandler(this._lvMods_DragDrop);
            this._lvMods.DragEnter += new System.Windows.Forms.DragEventHandler(this._lvMods_DragEnter);
            this._lvMods.KeyDown += new System.Windows.Forms.KeyEventHandler(this._lvMods_KeyDown);
            // 
            // _colModName
            // 
            this._colModName.Text = "Mod";
            // 
            // _fileBrowser
            // 
            this._fileBrowser.Filter = "Archives|*.7z;*.zip;*.rar";
            this._fileBrowser.Title = "Select the mod file to install";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(641, 430);
            this.Controls.Add(this._lvMods);
            this.Controls.Add(this._statusBar);
            this.Controls.Add(this._pnlToolbar);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MinimumSize = new System.Drawing.Size(360, 320);
            this.Name = "MainForm";
            this.Text = "{0} Mod Manager {1}";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.Resize += new System.EventHandler(this.MainForm_Resize);
            this._pnlToolbar.ResumeLayout(false);
            this._statusBar.ResumeLayout(false);
            this._statusBar.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button _btnAddFromFolder;
        private System.Windows.Forms.TableLayoutPanel _pnlToolbar;
        private System.Windows.Forms.ToolTip _toolTip;
        private System.Windows.Forms.Button _btnAddFromZip;
        private System.Windows.Forms.StatusStrip _statusBar;
        private System.Windows.Forms.ToolStripStatusLabel _lblStatus;
        private System.Windows.Forms.ToolStripProgressBar _progressBar;
        private BindableListView _lvMods;
        private System.Windows.Forms.ColumnHeader _colModName;
        private Ookii.Dialogs.WinForms.VistaFolderBrowserDialog _folderBrowser;
        private System.Windows.Forms.OpenFileDialog _fileBrowser;
        private System.Windows.Forms.Button _btnReinstall;
        private System.Windows.Forms.Button _btnRemove;
        private System.Windows.Forms.Button _btnSelectGame;
    }
}
