using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace IntersectUtilities.Forms
{
    public partial class StringGridForm : Form
    {
        public string SelectedValue { get; private set; }
        public enum PrimaryNorsynColors
        {
            Dark = 1455948,
            Blue = 7777478,
            DarkGreen = 3828594,
            LightGreen = 10928576,
            LightBrown = 14142900,
            Grey = 15198183
        }

        public StringGridForm(IEnumerable<string> stringList, int columns = 6)
        {
            InitializeComponent();
            SelectedValue = null;

            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;

            TableLayoutPanel panel = new TableLayoutPanel()
            {
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                Dock = DockStyle.Fill,
                AutoSize = true,
            };

            int row = 0, col = 0;
            int maxButtonWidth = 0;
            int maxButtonHeight = 0;

            foreach (var str in stringList)
            {
                Button btn = new Button
                {
                    Text = str,
                    AutoSize = true,
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(50, 50, 50),
                    ForeColor = Color.FromArgb(200, 200, 200),
                    Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                };
                btn.FlatAppearance.BorderColor = Color.FromArgb(40, 40, 40);
                btn.FlatAppearance.BorderSize = 1;
                btn.Click += (sender, e) => { ButtonClicked(str); };

                // Calculate maximum button size
                Size textSize = TextRenderer.MeasureText(str, btn.Font);
                maxButtonWidth = Math.Max(maxButtonWidth, textSize.Width + 20);  // +20 for padding
                maxButtonHeight = Math.Max(maxButtonHeight, textSize.Height + 20);  // +20 for padding

                if (col == 0)
                {
                    panel.RowStyles.Add(new RowStyle(SizeType.Absolute, maxButtonHeight));
                }
                panel.Controls.Add(btn, col, row);

                col++;
                if (col >= columns)
                {
                    col = 0;
                    row++;
                }
            }

            for (int i = 0; i < columns; i++)
            {
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, maxButtonWidth));
            }

            int panelWidth = maxButtonWidth * columns;
            int panelHeight = maxButtonHeight * (row + 1);

            // Ensure form does not exceed screen dimensions
            int maxWidth = Screen.PrimaryScreen.WorkingArea.Width;
            int maxHeight = Screen.PrimaryScreen.WorkingArea.Height;

            this.ClientSize = new Size(
                Math.Min(panelWidth, maxWidth),
                Math.Min(panelHeight, maxHeight)
            );

            this.Controls.Add(panel);
            this.AutoScroll = true;
            this.AutoSize = true;
        }

        private void ButtonClicked(string value)
        {
            SelectedValue = value;
            Console.WriteLine("Selected Value: " + SelectedValue);
            this.Close();
        }

        private void StringGridForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (SelectedValue == null)
            {
                Console.WriteLine("Form Closed without selection.");
            }
        }
    }
}
