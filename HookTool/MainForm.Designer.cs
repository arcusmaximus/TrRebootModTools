using TrRebootTools.HookTool.Materials;
using TrRebootTools.HookTool.Logging;

namespace TrRebootTools.HookTool
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this._tcMain = new System.Windows.Forms.TabControl();
            this._tpFiles = new System.Windows.Forms.TabPage();
            this._fileLog = new TrRebootTools.HookTool.Logging.FileAccessLogControl();
            this._tpAnimations = new System.Windows.Forms.TabPage();
            this._animationLog = new TrRebootTools.HookTool.Logging.AnimationAccessLogControl();
            this._tpMaterials = new System.Windows.Forms.TabPage();
            this._materialControl = new TrRebootTools.HookTool.Materials.MaterialControl();
            this._btnBrowseModFolder = new System.Windows.Forms.Button();
            this._tcMain.SuspendLayout();
            this._tpFiles.SuspendLayout();
            this._tpAnimations.SuspendLayout();
            this._tpMaterials.SuspendLayout();
            this.SuspendLayout();
            // 
            // _tcMain
            // 
            this._tcMain.Controls.Add(this._tpFiles);
            this._tcMain.Controls.Add(this._tpAnimations);
            this._tcMain.Controls.Add(this._tpMaterials);
            this._tcMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this._tcMain.Location = new System.Drawing.Point(0, 41);
            this._tcMain.Margin = new System.Windows.Forms.Padding(4);
            this._tcMain.Name = "_tcMain";
            this._tcMain.SelectedIndex = 0;
            this._tcMain.Size = new System.Drawing.Size(839, 444);
            this._tcMain.TabIndex = 0;
            // 
            // _tpFiles
            // 
            this._tpFiles.BackColor = System.Drawing.SystemColors.Control;
            this._tpFiles.Controls.Add(this._fileLog);
            this._tpFiles.Location = new System.Drawing.Point(4, 24);
            this._tpFiles.Margin = new System.Windows.Forms.Padding(4);
            this._tpFiles.Name = "_tpFiles";
            this._tpFiles.Padding = new System.Windows.Forms.Padding(4);
            this._tpFiles.Size = new System.Drawing.Size(831, 416);
            this._tpFiles.TabIndex = 0;
            this._tpFiles.Text = "File log";
            // 
            // _fileLog
            // 
            this._fileLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this._fileLog.Location = new System.Drawing.Point(4, 4);
            this._fileLog.Margin = new System.Windows.Forms.Padding(4);
            this._fileLog.Name = "_fileLog";
            this._fileLog.Size = new System.Drawing.Size(823, 408);
            this._fileLog.TabIndex = 0;
            // 
            // _tpAnimations
            // 
            this._tpAnimations.BackColor = System.Drawing.SystemColors.Control;
            this._tpAnimations.Controls.Add(this._animationLog);
            this._tpAnimations.Location = new System.Drawing.Point(4, 24);
            this._tpAnimations.Margin = new System.Windows.Forms.Padding(4);
            this._tpAnimations.Name = "_tpAnimations";
            this._tpAnimations.Padding = new System.Windows.Forms.Padding(4);
            this._tpAnimations.Size = new System.Drawing.Size(831, 416);
            this._tpAnimations.TabIndex = 1;
            this._tpAnimations.Text = "Animation log";
            // 
            // _animationLog
            // 
            this._animationLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this._animationLog.Location = new System.Drawing.Point(4, 4);
            this._animationLog.Margin = new System.Windows.Forms.Padding(4);
            this._animationLog.Name = "_animationLog";
            this._animationLog.Size = new System.Drawing.Size(823, 408);
            this._animationLog.TabIndex = 0;
            // 
            // _tpMaterials
            // 
            this._tpMaterials.BackColor = System.Drawing.SystemColors.Control;
            this._tpMaterials.Controls.Add(this._materialControl);
            this._tpMaterials.Location = new System.Drawing.Point(4, 24);
            this._tpMaterials.Name = "_tpMaterials";
            this._tpMaterials.Padding = new System.Windows.Forms.Padding(3);
            this._tpMaterials.Size = new System.Drawing.Size(831, 416);
            this._tpMaterials.TabIndex = 2;
            this._tpMaterials.Text = "Material editor";
            // 
            // _materialControl
            // 
            this._materialControl.AllowDrop = true;
            this._materialControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this._materialControl.Location = new System.Drawing.Point(3, 3);
            this._materialControl.Margin = new System.Windows.Forms.Padding(4);
            this._materialControl.Name = "_materialControl";
            this._materialControl.Size = new System.Drawing.Size(825, 410);
            this._materialControl.TabIndex = 0;
            this._materialControl.SavingMaterial += new System.EventHandler(this._materialControl_SavingMaterial);
            // 
            // _btnBrowseModFolder
            // 
            this._btnBrowseModFolder.Dock = System.Windows.Forms.DockStyle.Top;
            this._btnBrowseModFolder.Enabled = false;
            this._btnBrowseModFolder.Location = new System.Drawing.Point(0, 0);
            this._btnBrowseModFolder.Name = "_btnBrowseModFolder";
            this._btnBrowseModFolder.Size = new System.Drawing.Size(839, 41);
            this._btnBrowseModFolder.TabIndex = 2;
            this._btnBrowseModFolder.Text = "Open mod folder...";
            this._btnBrowseModFolder.UseVisualStyleBackColor = true;
            this._btnBrowseModFolder.Click += new System.EventHandler(this._btnBrowseModFolder_Click);
            // 
            // MainForm
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(839, 507);
            this.Controls.Add(this._tcMain);
            this.Controls.Add(this._btnBrowseModFolder);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(5);
            this.MinimumSize = new System.Drawing.Size(406, 340);
            this.Name = "MainForm";
            this.Text = "{0} Hook Tool {1}";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.MainForm_FormClosed);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.DragDrop += new System.Windows.Forms.DragEventHandler(this.MainForm_DragDrop);
            this.DragEnter += new System.Windows.Forms.DragEventHandler(this.MainForm_DragEnter);
            this.Controls.SetChildIndex(this._btnBrowseModFolder, 0);
            this.Controls.SetChildIndex(this._tcMain, 0);
            this._tcMain.ResumeLayout(false);
            this._tpFiles.ResumeLayout(false);
            this._tpAnimations.ResumeLayout(false);
            this._tpMaterials.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TabControl _tcMain;
        private System.Windows.Forms.TabPage _tpFiles;
        private System.Windows.Forms.TabPage _tpAnimations;
        private AnimationAccessLogControl _animationLog;
        private System.Windows.Forms.TabPage _tpMaterials;
        private MaterialControl _materialControl;
        private FileAccessLogControl _fileLog;
        private System.Windows.Forms.Button _btnBrowseModFolder;
    }
}