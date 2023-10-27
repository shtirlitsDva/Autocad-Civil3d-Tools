using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;

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
        private List<System.Windows.Forms.CheckBox> checkBoxes = new List<System.Windows.Forms.CheckBox>();

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
            PopulateButtons(listDns, 3, tableLayoutPanelButtonsEnkelt);
            listDns = PipeScheduleV2.ListAllDnsForPipeSystemTypeSerie(
                Utils.PipeSystemEnum.PexU, Utils.PipeTypeEnum.Twin, Utils.PipeSeriesEnum.S3)
                .OrderBy(x => x).ToList();
            PopulateButtons(listDns, 3, tableLayoutPanelButtonsTwin);
        }

        private void PopulateButtons(
            List<int> buttonsData, int colCount, TableLayoutPanel tLP)
        {
            float dpiScalingFactor = GetDpiScalingFactor(this);
            float buttonHeight = 30 * dpiScalingFactor;

            int rowCount = buttonsData.Count / colCount;
            if (buttonsData.Count % colCount != 0) rowCount++;

            tLP.RowCount = rowCount;
            tLP.ColumnCount = colCount;

            for (int i = 0; i < rowCount; i++)
                tLP.RowStyles.Add(new RowStyle(SizeType.Absolute, buttonHeight));

            for (int j = 0; j < colCount; j++)
                tLP.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / colCount));

            int dataIndex = 0;
            for (int i = 0; i < rowCount && dataIndex < buttonsData.Count(); i++)
            {
                for (int j = 0; j < colCount && dataIndex < buttonsData.Count(); j++)
                {
                    System.Windows.Forms.CheckBox chk = new System.Windows.Forms.CheckBox
                    {
                        Text = buttonsData[dataIndex].ToString(),
                        Appearance = Appearance.Button,
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter,
                        BackColor = Color.LightGray,
                        ForeColor = Color.DarkBlue,
                        //FlatStyle = FlatStyle.Flat,
                    };
                    chk.Click += dnButtonCheckBox_Click;

                    checkBoxes.Add(chk);
                    tLP.Controls.Add(chk, j, i);
                    dataIndex++;
                }
            }

            tLP.Parent.Height = rowCount * ((int)Math.Round(buttonHeight)) + 20;
        }
        private void dnButtonCheckBox_Click(object sender, EventArgs e)
        {
            System.Windows.Forms.CheckBox cb = (System.Windows.Forms.CheckBox)sender;

            // Set the back color of the clicked checkbox
            cb.BackColor = Color.DarkGray;

            foreach (System.Windows.Forms.CheckBox checkBox in checkBoxes)
            {
                if (cb != checkBox)
                {
                    checkBox.BackColor = Color.LightGray;
                    if (checkBox.Checked)
                    {
                        checkBox.Checked = false; 
                    }
                }
            }


            //ActivateLayer(PipeTypeEnum.Twin, pipeDn);
        }

        private float GetDpiScalingFactor(Control control)
        {
            using (Graphics graphics = Graphics.FromHwnd(control.Handle))
            {
                return graphics.DpiX / 96.0f; // 96 DPI is the standard DPI.
            }
        }
    }
}
