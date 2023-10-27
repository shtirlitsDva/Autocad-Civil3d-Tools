namespace DriPaletteSet
{
    partial class PEXU_Palette
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.GroupBoxEnkelt = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanelButtonsEnkelt = new System.Windows.Forms.TableLayoutPanel();
            this.GroupBoxEnkelt.SuspendLayout();
            this.SuspendLayout();
            // 
            // GroupBoxEnkelt
            // 
            this.GroupBoxEnkelt.Controls.Add(this.tableLayoutPanelButtonsEnkelt);
            this.GroupBoxEnkelt.Dock = System.Windows.Forms.DockStyle.Top;
            this.GroupBoxEnkelt.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.GroupBoxEnkelt.ForeColor = System.Drawing.SystemColors.Control;
            this.GroupBoxEnkelt.Location = new System.Drawing.Point(0, 0);
            this.GroupBoxEnkelt.Name = "GroupBoxEnkelt";
            this.GroupBoxEnkelt.Size = new System.Drawing.Size(464, 286);
            this.GroupBoxEnkelt.TabIndex = 0;
            this.GroupBoxEnkelt.TabStop = false;
            this.GroupBoxEnkelt.Text = "Enkeltrør";
            // 
            // tableLayoutPanelButtons
            // 
            this.tableLayoutPanelButtonsEnkelt.ColumnCount = 2;
            this.tableLayoutPanelButtonsEnkelt.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelButtonsEnkelt.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelButtonsEnkelt.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelButtonsEnkelt.Location = new System.Drawing.Point(3, 22);
            this.tableLayoutPanelButtonsEnkelt.Name = "tableLayoutPanelButtons";
            this.tableLayoutPanelButtonsEnkelt.RowCount = 2;
            this.tableLayoutPanelButtonsEnkelt.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelButtonsEnkelt.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelButtonsEnkelt.Size = new System.Drawing.Size(458, 261);
            this.tableLayoutPanelButtonsEnkelt.TabIndex = 0;
            // 
            // PEXU_Palette
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            this.Controls.Add(this.GroupBoxEnkelt);
            this.Name = "PEXU_Palette";
            this.Size = new System.Drawing.Size(464, 655);
            this.GroupBoxEnkelt.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox GroupBoxEnkelt;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelButtonsEnkelt;
    }
}
