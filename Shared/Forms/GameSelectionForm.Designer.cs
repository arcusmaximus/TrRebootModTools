namespace TrRebootTools.Shared.Forms
{
    partial class GameSelectionForm
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
            this.components = new System.ComponentModel.Container();
            this._btnShadow = new System.Windows.Forms.Button();
            this._btnRise = new System.Windows.Forms.Button();
            this._btnTr2013 = new System.Windows.Forms.Button();
            this._toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.SuspendLayout();
            // 
            // _btnShadow
            // 
            this._btnShadow.Image = global::TrRebootTools.Shared.Properties.Resources.Shadow;
            this._btnShadow.Location = new System.Drawing.Point(288, 12);
            this._btnShadow.Name = "_btnShadow";
            this._btnShadow.Size = new System.Drawing.Size(132, 132);
            this._btnShadow.TabIndex = 2;
            this._toolTip.SetToolTip(this._btnShadow, "Shadow of the Tomb Raider");
            this._btnShadow.UseVisualStyleBackColor = true;
            this._btnShadow.Click += new System.EventHandler(this._btnShadow_Click);
            // 
            // _btnRise
            // 
            this._btnRise.Image = global::TrRebootTools.Shared.Properties.Resources.Rise;
            this._btnRise.Location = new System.Drawing.Point(150, 12);
            this._btnRise.Name = "_btnRise";
            this._btnRise.Size = new System.Drawing.Size(132, 132);
            this._btnRise.TabIndex = 1;
            this._toolTip.SetToolTip(this._btnRise, "Rise of the Tomb Raider");
            this._btnRise.UseVisualStyleBackColor = true;
            this._btnRise.Click += new System.EventHandler(this._btnRise_Click);
            // 
            // _btnTr2013
            // 
            this._btnTr2013.Image = global::TrRebootTools.Shared.Properties.Resources.Tr2013;
            this._btnTr2013.Location = new System.Drawing.Point(12, 12);
            this._btnTr2013.Name = "_btnTr2013";
            this._btnTr2013.Size = new System.Drawing.Size(132, 132);
            this._btnTr2013.TabIndex = 0;
            this._toolTip.SetToolTip(this._btnTr2013, "Tomb Raider 2013");
            this._btnTr2013.UseVisualStyleBackColor = true;
            this._btnTr2013.Click += new System.EventHandler(this._btnTr2013_Click);
            // 
            // GameSelectionForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(432, 158);
            this.Controls.Add(this._btnShadow);
            this.Controls.Add(this._btnRise);
            this.Controls.Add(this._btnTr2013);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "GameSelectionForm";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Select Game";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button _btnTr2013;
        private System.Windows.Forms.Button _btnRise;
        private System.Windows.Forms.Button _btnShadow;
        private System.Windows.Forms.ToolTip _toolTip;
    }
}