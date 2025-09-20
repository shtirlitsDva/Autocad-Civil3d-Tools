namespace DriPaletteSet
{
    partial class PipePalette
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
            this.groupBox_ComboBoxHost = new System.Windows.Forms.GroupBox();
            this.comboBox_PipeTypeSelector = new System.Windows.Forms.ComboBox();
            this.basePanel = new System.Windows.Forms.Panel();
            this.groupBox_ComboBoxHost.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox_ComboBoxHost
            // 
            this.groupBox_ComboBoxHost.Controls.Add(this.comboBox_PipeTypeSelector);
            this.groupBox_ComboBoxHost.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBox_ComboBoxHost.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F);
            this.groupBox_ComboBoxHost.ForeColor = System.Drawing.SystemColors.ButtonFace;
            this.groupBox_ComboBoxHost.Location = new System.Drawing.Point(0, 0);
            this.groupBox_ComboBoxHost.Name = "groupBox_ComboBoxHost";
            this.groupBox_ComboBoxHost.Size = new System.Drawing.Size(746, 90);
            this.groupBox_ComboBoxHost.TabIndex = 0;
            this.groupBox_ComboBoxHost.TabStop = false;
            this.groupBox_ComboBoxHost.Text = "Vælg system:";
            // 
            // comboBox_PipeTypeSelector
            // 
            this.comboBox_PipeTypeSelector.Dock = System.Windows.Forms.DockStyle.Fill;
            this.comboBox_PipeTypeSelector.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox_PipeTypeSelector.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
            this.comboBox_PipeTypeSelector.FormattingEnabled = true;
            this.comboBox_PipeTypeSelector.Location = new System.Drawing.Point(3, 35);
            this.comboBox_PipeTypeSelector.Name = "comboBox_PipeTypeSelector";
            this.comboBox_PipeTypeSelector.Size = new System.Drawing.Size(740, 50);
            this.comboBox_PipeTypeSelector.TabIndex = 0;
            // 
            // basePanel
            // 
            this.basePanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.basePanel.Location = new System.Drawing.Point(0, 90);
            this.basePanel.Name = "basePanel";
            this.basePanel.Size = new System.Drawing.Size(746, 1152);
            this.basePanel.TabIndex = 1;
            // 
            // PipePalette
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            this.Controls.Add(this.basePanel);
            this.Controls.Add(this.groupBox_ComboBoxHost);
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Name = "PipePalette";
            this.Size = new System.Drawing.Size(746, 1242);
            this.groupBox_ComboBoxHost.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox_ComboBoxHost;
        private System.Windows.Forms.ComboBox comboBox_PipeTypeSelector;
        private System.Windows.Forms.Panel basePanel;
    }
}
