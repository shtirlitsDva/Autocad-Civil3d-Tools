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
    public partial class PipePalette : UserControl
    {
        private Properties.Settings sets;
        private List<CheckBox> dnCheckBoxes = new List<CheckBox>();
        private List<CheckBox> seriesCheckBoxes = new List<CheckBox>();
        private int pad = 20;

        public PipePalette()
        {
            InitializeComponent();
            sets = Properties.Settings.Default;
            this.Load += PEXU_Palette_Load;
            PaletteUtils.CurrentSeries = PipeSeriesEnum.S3;
        }

        private void PEXU_Palette_Load(object sender, EventArgs e)
        {
            PopulateComboBox(comboBox_PipeTypeSelector);
            int selectedIndex = sets.pipePalette_PipeSystemTypeIndex;
            if (selectedIndex < 0 || selectedIndex >= comboBox_PipeTypeSelector.Items.Count)
                selectedIndex = 0;
            comboBox_PipeTypeSelector.SelectedIndexChanged += ComboBox_PipeTypeSelector_SelectedIndexChanged;
            comboBox_PipeTypeSelector.SelectedIndex = selectedIndex;

            groupBox_ComboBoxHost.Height = comboBox_PipeTypeSelector.Height + pad;
        }

        private void ComboBox_PipeTypeSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            sets.pipePalette_PipeSystemTypeIndex = comboBox_PipeTypeSelector.SelectedIndex;
            sets.Save();

            var item = (PipeSystemTypeCombination)comboBox_PipeTypeSelector.SelectedItem;

            Font font = new Font("Microsoft Sans Serif", 12F);
            float fontHeight;

            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
            {
                fontHeight = font.GetHeight(g);
            }

            //Rebuild form
            basePanel.Controls.Clear();

            #region Dn buttons
            //First create DN buttons
            var gpBox = new GroupBox();
            basePanel.Controls.Add(gpBox);
            gpBox.Dock = DockStyle.Top;
            gpBox.Text = item.ToString();
            gpBox.ForeColor = Color.White;
            gpBox.Font = font;
            var tblPnl = new TableLayoutPanel();
            gpBox.Controls.Add(tblPnl);
            tblPnl.Dock = DockStyle.Fill;

            var listDns = PipeScheduleV2.ListAllDnsForPipeSystemType(
                item.System, item.Type)
                .OrderBy(x => x).Select(x => x.ToString()).ToList();

            PopulateButtons(listDns, 3, tblPnl, dnButtonCheckBox_Click, dnCheckBoxes);
            gpBox.Height = tblPnl.Height + pad + (int)Math.Ceiling(fontHeight);
            #endregion

            #region Series buttons
            gpBox = new GroupBox();
            basePanel.Controls.Add(gpBox);
            gpBox.Dock = DockStyle.Top;
            gpBox.Text = "Serie";
            gpBox.ForeColor = Color.White;
            gpBox.Font = font;
            tblPnl = new TableLayoutPanel();
            gpBox.Controls.Add(tblPnl);
            tblPnl.Dock = DockStyle.Fill;

            var listSeries = PipeScheduleV2.ListAllSeriesForPipeSystemType(item.System, item.Type)
                .Select(x => x.ToString()).ToList();

            PopulateButtons(listSeries, 3, tblPnl, seriesButtonCheckBox_Click, seriesCheckBoxes);
            gpBox.Height = tblPnl.Height + pad + (int)Math.Ceiling(fontHeight);

            seriesCheckBoxes.OrderBy(x => x.Text).Last().Checked = true;
            seriesButtonCheckBox_Click(seriesCheckBoxes.OrderBy(x => x.Text).Last(), EventArgs.Empty);
            #endregion
        }

        private void PopulateComboBox(ComboBox comboBox)
        {
            foreach (PipeSystemEnum system in Enum.GetValues(typeof(PipeSystemEnum))
                .Cast<PipeSystemEnum>().ToList().Skip(1))
                foreach (PipeTypeEnum type in PipeScheduleV2.GetPipeSystemAvailableTypes(system))
                    comboBox.Items.Add(
                        new PipeSystemTypeCombination(system, type));
        }
        private void PopulateButtons(
            List<string> buttonsData, int colCount, TableLayoutPanel tLP, EventHandler clickEventHandler, List<CheckBox> checkBoxes)
        {
            float dpiScalingFactor = GetDpiScalingFactor(this);
            float buttonHeight = 50 * dpiScalingFactor;

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
                    CheckBox chk = new CheckBox
                    {
                        Text = buttonsData[dataIndex].ToString(),
                        Appearance = Appearance.Button,
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter,
                        BackColor = Color.LightGray,
                        ForeColor = Color.DarkBlue,
                        Font = new Font("Arial", 12 * dpiScalingFactor, FontStyle.Bold),
                        //FlatStyle = FlatStyle.Flat,
                    };
                    if (clickEventHandler != null) chk.Click += clickEventHandler;

                    checkBoxes.Add(chk);
                    tLP.Controls.Add(chk, j, i);
                    dataIndex++;
                }
            }

            tLP.Parent.Height = rowCount * ((int)Math.Round(buttonHeight)) + 20;
        }
        private void dnButtonCheckBox_Click(object sender, EventArgs e)
        {
            CheckBox cb = (CheckBox)sender;

            // Set the back color of the clicked checkbox
            cb.BackColor = Color.DarkGray;

            foreach (CheckBox checkBox in dnCheckBoxes)
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

            GroupBox granddad = (GroupBox)cb.Parent.Parent;
            if (granddad.Text == "Enkeltrør")
            {
                PaletteUtils.ActivateLayer(PipeSystemEnum.PexU, PipeTypeEnum.Enkelt, cb.Text);
            }
            else if (granddad.Text == "Twinrør")
            {
                PaletteUtils.ActivateLayer(PipeSystemEnum.PexU, PipeTypeEnum.Twin, cb.Text);
            }
        }
        private void seriesButtonCheckBox_Click(object sender, EventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            cb.BackColor = Color.DarkGray;
            foreach (CheckBox checkBox in seriesCheckBoxes)
                if (checkBox.Checked && cb.Name != checkBox.Name) 
                { 
                    checkBox.Checked = false; 
                    checkBox.BackColor = Color.LightGray;
                }

            PipeSeriesEnum pipeSeriesEnum =
                (PipeSeriesEnum)Enum.Parse(typeof(PipeSeriesEnum), cb.Text);



            PaletteUtils.CurrentSeries = pipeSeriesEnum;
        }
        private float GetDpiScalingFactor(Control control)
        {
            using (Graphics graphics = Graphics.FromHwnd(control.Handle))
            {
                return graphics.DpiX / 96.0f; // 96 DPI is the standard DPI.
            }
        }
        private void OpdaterBredde_Click(object sender, EventArgs e)
        {
            PaletteUtils.UpdateWidths();
        }
        private void NulstilBredde_Click(object sender, EventArgs e)
        {
            PaletteUtils.ResetWidths();
        }
        private void SetLabel_Click(object sender, EventArgs e)
        {
            PaletteUtils.labelpipe();
        }
    }
}
