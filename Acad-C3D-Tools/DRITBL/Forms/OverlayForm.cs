using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

public class OverlayForm : Form
{
    private Form parentForm;

    public OverlayForm(Form parentForm, string message)
    {
        InitializeComponent();
        this.parentForm = parentForm;

        // Set up the overlay form
        this.FormBorderStyle = FormBorderStyle.None;
        this.StartPosition = FormStartPosition.Manual;
        this.ShowInTaskbar = false;
        this.BackColor = Color.Black; // Or any color that fits the design
        //this.ForeColor = Color.Yellow;
        
        //this.Opacity = 0.8; // Make the overlay slightly transparent
        //this.AutoSize = true;
        //this.AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold);
        Size textSize = TextRenderer.MeasureText(message, font);

        // Create a label to show the message
        Label label = new Label
        {
            Text = message,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
            ForeColor = Color.White,
            Font = font,
            Size = new Size(textSize.Width, textSize.Height),
        };

        this.Controls.Add(label);

        // Make the overlay form topmost
        this.TopMost = true;

        this.ClientSize = new Size(label.Width, label.Height);
    }

    private void InitializeComponent()
    {
            this.SuspendLayout();
            // 
            // OverlayForm
            // 
            this.ClientSize = new System.Drawing.Size(211, 27);
            this.Name = "OverlayForm";
            this.Shown += new System.EventHandler(this.OverlayForm_Shown);
            this.ResumeLayout(false);
    }

    private void OverlayForm_Shown(object sender, EventArgs e)
    {
        // Position the overlay form above the parent form
        this.Location = new Point(
            parentForm.Location.X + (parentForm.Width - this.Width) / 2,
            parentForm.Location.Y - this.Height - 10);
    }
}
