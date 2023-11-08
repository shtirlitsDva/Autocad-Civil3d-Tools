using IntersectUtilities.UtilsCommon;

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
        private enum PrimaryNorsynColors
        {
            Dark = 1455948,
            Blue = 7777478,
            DarkGreen = 3828594,
            LightGreen = 10928576,
            LightBrown = 14142900,
            Grey = 15198183
        }
        private TableLayoutPanel panel;

        public StringGridForm(IEnumerable<string> stringList, int columns = 6, string name = "")
        {
            InitializeComponent();
            #region Init buttons
            SelectedValue = null;

            this.FormBorderStyle = FormBorderStyle.None;

            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;

            panel = new TableLayoutPanel()
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
                btn.GotFocus += button_GotFocus;
                btn.LostFocus += button_LostFocus;
                btn.KeyDown += StringGridForm_KeyDown;
                btn.PreviewKeyDown += StringGridForm_PreviewKeyDown;

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

            panel.ColumnCount = columns + 1;
            panel.RowCount = row + 1;

            int panelWidth = maxButtonWidth * columns;
            int panelHeight = maxButtonHeight * row; //(row + 1);

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
            #endregion

            if (name.IsNotNoE()) this.Name = name;
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

        private void StringGridForm_Load(object sender, EventArgs e)
        {
            StartPosition = FormStartPosition.Manual;

            Point cursorPosition = Cursor.Position;
            int formX = cursorPosition.X - this.Width / 2;
            int formY = cursorPosition.Y + this.Height - this.ClientSize.Height;

            // Set the form's Location property
            Location = new Point(formX, formY);

            // Check if there are any buttons in the TableLayoutPanel
            if (panel.Controls.Count > 0)
            {
                // Set focus to the first button
                panel.Controls[0].Focus();
            }
        }

        private void button_GotFocus(object sender, EventArgs e)
        {
            Button focusedButton = (Button)sender;
            focusedButton.BackColor = Color.LightSkyBlue;
            focusedButton.ForeColor = Color.Red;
        }

        private void button_LostFocus(object sender, EventArgs e)
        {
            Button focusedButton = (Button)sender;
            focusedButton.BackColor = Color.FromArgb(50, 50, 50);
            focusedButton.ForeColor = Color.FromArgb(200, 200, 200);
        }

        private void StringGridForm_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.Left:
                case Keys.Right:
                    e.IsInputKey = true;
                    break;
            }
        }

        private void StringGridForm_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            Button focusedButton = panel.Controls.OfType<Button>().Where(b => b.Focused).FirstOrDefault();

            if (focusedButton == null) return;

            int currentRow = panel.GetRow(focusedButton);
            int currentColumn = panel.GetColumn(focusedButton);

            // Navigate based on the pressed key
            switch (e.KeyCode)
            {
                case Keys.Up:
                    {
                        Button btn = null;
                        while (btn == null)
                        {
                            currentRow--;
                            if (currentRow < 0) currentRow = panel.RowCount - 1;
                            btn = panel.GetControlFromPosition(currentColumn, currentRow) as Button;
                        }

                        btn.Focus();
                    }
                    break;
                case Keys.Down:
                    {
                        Button btn = null;
                        while (btn == null)
                        {
                            currentRow++;
                            if (currentRow > panel.RowCount - 1) currentRow = 0;
                            btn = panel.GetControlFromPosition(currentColumn, currentRow) as Button;
                        }

                        btn.Focus();
                    }
                    break;
                case Keys.Left:
                    {
                        Button btn = null;
                        while (btn == null)
                        {
                            currentColumn--;
                            if (currentColumn < 0) currentColumn = panel.ColumnCount - 1;
                            btn = panel.GetControlFromPosition(currentColumn, currentRow) as Button;
                        }

                        btn.Focus();
                    }
                    break;
                case Keys.Right:
                    {
                        Button btn = null;
                        while (btn == null)
                        {
                            currentColumn++;
                            if (currentColumn > panel.ColumnCount - 1) currentColumn = 0;
                            btn = panel.GetControlFromPosition(currentColumn, currentRow) as Button;
                        }

                        btn.Focus();
                    }
                    break;
                case Keys.Escape:
                    {
                        this.Close();
                    }
                    break;
            }
        }
    }
}
