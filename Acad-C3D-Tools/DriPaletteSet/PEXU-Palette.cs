using IntersectUtilities.PipeScheduleV2;
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
            var listDns = PipeScheduleV2.ListAllDnsForPipeSystemTypeSerie(
                Utils.PipeSystemEnum.PexU, Utils.PipeTypeEnum.Enkelt, Utils.PipeSeriesEnum.S3)
                .OrderBy(x => x).ToList();
            PopulateButtons(listDns, 2, tableLayoutPanelButtonsEnkelt);
        }

        public void PopulateButtons(
            List<int> buttonsData, int colCount, TableLayoutPanel tLP)
        {
            int rowCount = buttonsData.Count / colCount;
            if (buttonsData.Count % colCount != 0) rowCount++;

            tLP.RowCount = rowCount;
            tLP.ColumnCount = colCount;

            for (int i = 0; i < rowCount; i++)
                tLP.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));

            for (int j = 0; j < colCount; j++)
                tLP.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / colCount));

            int dataIndex = 0;
            for (int i = 0; i < rowCount && dataIndex < buttonsData.Count(); i++)
            {
                for (int j = 0; j < colCount && dataIndex < buttonsData.Count(); j++)
                {
                    CheckBox chk = new CheckBox
                    {
                        Text = buttonsData[dataIndex].ToString(),
                        Appearance = Appearance.Button,
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter,
                        BackColor = Color.LightGray,
                        ForeColor = Color.DarkBlue,
                        //FlatStyle = FlatStyle.Flat,
                    };
                    chk.CheckedChanged += CheckBox_CheckedChanged;

                    checkBoxes.Add(chk);
                    tLP.Controls.Add(chk, j, i);
                    dataIndex++;
                }
            }

            GroupBoxEnkelt.Height = rowCount * 30 + 20; // +20 for padding for GroupBox header.
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
