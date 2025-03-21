﻿
using TrRebootTools.Extractor;
using TrRebootTools.Extractor.Controls;

namespace TrRebootTools.Extractor
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this._tvFiles = new TrRebootTools.Extractor.Controls.ArchiveFileTreeView();
            this._pnlButtons = new System.Windows.Forms.TableLayoutPanel();
            this._btnExtract = new System.Windows.Forms.Button();
            this._btnSwitchGame = new System.Windows.Forms.Button();
            this._toolTip = new System.Windows.Forms.ToolTip(this.components);
            this._lblLoading = new System.Windows.Forms.Label();
            this._pnlButtons.SuspendLayout();
            this.SuspendLayout();
            // 
            // _tvFiles
            // 
            this._tvFiles.Dock = System.Windows.Forms.DockStyle.Fill;
            this._tvFiles.Location = new System.Drawing.Point(0, 0);
            this._tvFiles.Margin = new System.Windows.Forms.Padding(5);
            this._tvFiles.Name = "_tvFiles";
            this._tvFiles.Size = new System.Drawing.Size(623, 544);
            this._tvFiles.TabIndex = 0;
            this._tvFiles.SelectionChanged += new System.EventHandler(this._tvFiles_SelectionChanged);
            // 
            // _pnlButtons
            // 
            this._pnlButtons.ColumnCount = 2;
            this._pnlButtons.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._pnlButtons.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 72F));
            this._pnlButtons.Controls.Add(this._btnExtract, 0, 0);
            this._pnlButtons.Controls.Add(this._btnSwitchGame, 1, 0);
            this._pnlButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._pnlButtons.Location = new System.Drawing.Point(0, 544);
            this._pnlButtons.Margin = new System.Windows.Forms.Padding(4);
            this._pnlButtons.Name = "_pnlButtons";
            this._pnlButtons.RowCount = 1;
            this._pnlButtons.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._pnlButtons.Size = new System.Drawing.Size(623, 72);
            this._pnlButtons.TabIndex = 3;
            // 
            // _btnExtract
            // 
            this._btnExtract.Dock = System.Windows.Forms.DockStyle.Fill;
            this._btnExtract.Enabled = false;
            this._btnExtract.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this._btnExtract.Location = new System.Drawing.Point(4, 4);
            this._btnExtract.Margin = new System.Windows.Forms.Padding(4);
            this._btnExtract.Name = "_btnExtract";
            this._btnExtract.Size = new System.Drawing.Size(543, 64);
            this._btnExtract.TabIndex = 2;
            this._btnExtract.Text = "Extract";
            this._btnExtract.UseVisualStyleBackColor = true;
            this._btnExtract.Click += new System.EventHandler(this._btnExtract_Click);
            // 
            // _btnSwitchGame
            // 
            this._btnSwitchGame.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this._btnSwitchGame.Dock = System.Windows.Forms.DockStyle.Fill;
            this._btnSwitchGame.Location = new System.Drawing.Point(554, 3);
            this._btnSwitchGame.Name = "_btnSwitchGame";
            this._btnSwitchGame.Size = new System.Drawing.Size(66, 66);
            this._btnSwitchGame.TabIndex = 3;
            this._toolTip.SetToolTip(this._btnSwitchGame, "Switch to a different game");
            this._btnSwitchGame.UseVisualStyleBackColor = true;
            this._btnSwitchGame.Click += new System.EventHandler(this._btnSwitchGame_Click);
            // 
            // _lblLoading
            // 
            this._lblLoading.Anchor = System.Windows.Forms.AnchorStyles.None;
            this._lblLoading.AutoSize = true;
            this._lblLoading.BackColor = System.Drawing.SystemColors.Window;
            this._lblLoading.Location = new System.Drawing.Point(263, 276);
            this._lblLoading.Name = "_lblLoading";
            this._lblLoading.Size = new System.Drawing.Size(96, 15);
            this._lblLoading.TabIndex = 4;
            this._lblLoading.Text = "Loading file list...";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(623, 638);
            this.Controls.Add(this._lblLoading);
            this.Controls.Add(this._tvFiles);
            this.Controls.Add(this._pnlButtons);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(5);
            this.MinimumSize = new System.Drawing.Size(639, 428);
            this.Name = "MainForm";
            this.Text = "{0} Extractor {1}";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.Controls.SetChildIndex(this._pnlButtons, 0);
            this.Controls.SetChildIndex(this._tvFiles, 0);
            this.Controls.SetChildIndex(this._lblLoading, 0);
            this._pnlButtons.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private ArchiveFileTreeView _tvFiles;
        private System.Windows.Forms.Button _btnExtract;
        private System.Windows.Forms.TableLayoutPanel _pnlButtons;
        private System.Windows.Forms.ToolTip _toolTip;
        private System.Windows.Forms.Button _btnSwitchGame;
        private System.Windows.Forms.Label _lblLoading;
    }
}

