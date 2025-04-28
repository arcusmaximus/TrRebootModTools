namespace TrRebootTools.Shared.Forms
{
    partial class FormWithProgress
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
            this._statusBar = new System.Windows.Forms.StatusStrip();
            this._lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this._progressBar = new System.Windows.Forms.ToolStripProgressBar();
            this._statusBar.SuspendLayout();
            this.SuspendLayout();
            // 
            // _statusBar
            // 
            this._statusBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._lblStatus,
            this._progressBar});
            this._statusBar.Location = new System.Drawing.Point(0, 565);
            this._statusBar.Name = "_statusBar";
            this._statusBar.Size = new System.Drawing.Size(909, 22);
            this._statusBar.TabIndex = 1;
            // 
            // _lblStatus
            // 
            this._lblStatus.Name = "_lblStatus";
            this._lblStatus.Size = new System.Drawing.Size(761, 17);
            this._lblStatus.Spring = true;
            this._lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _progressBar
            // 
            this._progressBar.Name = "_progressBar";
            this._progressBar.Size = new System.Drawing.Size(100, 16);
            this._progressBar.Visible = false;
            // 
            // FormWithProgress
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(909, 587);
            this.Controls.Add(this._statusBar);
            this.Name = "FormWithProgress";
            this.Text = "FormWithProgress";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormWithProgress_FormClosing);
            this._statusBar.ResumeLayout(false);
            this._statusBar.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.StatusStrip _statusBar;
        private System.Windows.Forms.ToolStripStatusLabel _lblStatus;
        private System.Windows.Forms.ToolStripProgressBar _progressBar;
    }
}
