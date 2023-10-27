using IntersectUtilities.PipeScheduleV2;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DriPaletteSet
{
    public partial class PEXU_Palette : UserControl
    {
        private List<CheckBox> checkBoxes = new List<CheckBox>();

        public PEXU_Palette()
        {
            InitializeComponent();
            this.Load += PEXU_Palette_Load;
        }

        private void PEXU_Palette_Load(object sender, EventArgs e)
        {
            PipeScheduleV2.pip
            PopulateButtons(4, 5, tableLayoutPanelButtonsEnkelt);  // For example, 4 rows and 5 columns. Adjust as needed.
        }

        public void PopulateButtons(
            int rowCount, int colCount, TableLayoutPanel tLP)
        {
            tLP.RowCount = rowCount;
            tLP.ColumnCount = colCount;

            for (int i = 0; i < rowCount; i++)
            {
                tLP.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rowCount));
            }

            for (int j = 0; j < colCount; j++)
            {
                tLP.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / colCount));
            }

            for (int i = 0; i < rowCount; i++)
            {
                for (int j = 0; j < colCount; j++)
                {
                    CheckBox chk = new CheckBox
                    {
                        Text = $"Btn {i}-{j}",
                        Appearance = Appearance.Button,
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter,
                        BackColor = Color.LightGray
                    };
                    chk.CheckedChanged += CheckBox_CheckedChanged;

                    checkBoxes.Add(chk);
                    tLP.Controls.Add(chk, j, i);
                }
            }
        }
        private void CheckBox_CheckedChanged(object sender, EventArgs e)
        {
            foreach (var chk in checkBoxes)
            {
                if (chk != sender)
                {
                    chk.Checked = false;
                    chk.BackColor = Color.LightGray;
                }
                else
                {
                    chk.BackColor = chk.Checked ? Color.DarkGray : Color.LightGray;
                }
            }
        }
    }
}
