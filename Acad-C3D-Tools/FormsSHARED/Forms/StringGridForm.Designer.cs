﻿namespace IntersectUtilities.Forms
{
    partial class StringGridForm
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
            this.SuspendLayout();
            // 
            // StringGridForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Name = "StringGridForm";
            this.Text = "StringGridForm";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.StringGridForm_FormClosing);
            this.Load += new System.EventHandler(this.StringGridForm_Load);
            this.Shown += new System.EventHandler(this.StringGridForm_Shown);
            this.ResumeLayout(false);

        }

        #endregion
    }
}