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
        #region Cached variables
        private Properties.Settings sets;
        private List<CheckBox> dnCheckBoxes = new List<CheckBox>();
        private List<CheckBox> seriesCheckBoxes = new List<CheckBox>();
        private float buttonHeight = 50;
        private int pad = 20;

        private PipeSystemTypeCombination currentComb;

        private GroupBox labelGB;
        private TableLayoutPanel labelTLP;
        private Button labelButton;

        private GroupBox widthManagementGB;
        private TableLayoutPanel widthManagementTLP;
        private Button updateWidthsButton;
        private Button resetWidthsButton;

        private GroupBox typeGB;
        private TableLayoutPanel typeTLP;
        private Button fremButton;
        private Button returButton; 
        #endregion

        public PipePalette()
        {
            InitializeComponent();
            sets = Properties.Settings.Default;
            this.Load += PipePalette_Load;
            PaletteUtils.CurrentSeries = PipeSeriesEnum.S3;
        }
        private void PipePalette_Load(object sender, EventArgs e)
        {
            PopulateComboBox(comboBox_PipeTypeSelector);
            int selectedIndex = sets.pipePalette_PipeSystemTypeIndex;
            if (selectedIndex < 0 || selectedIndex >= comboBox_PipeTypeSelector.Items.Count)
                selectedIndex = 0;
            comboBox_PipeTypeSelector.SelectedIndex = selectedIndex;

            #region Settings for buttons and font
            Font font = new Font("Microsoft Sans Serif", 12F);
            float fontHeight;

            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
            {
                fontHeight = font.GetHeight(g);
            }

            float dpiScalingFactor = GetDpiScalingFactor(this);
            float btnHght = buttonHeight * dpiScalingFactor;
            #endregion

            #region Label management init
            labelTLP = new TableLayoutPanel();
            labelTLP.Dock = DockStyle.Fill;
            labelTLP.RowCount = 1;
            labelTLP.ColumnCount = 1;
            labelTLP.RowStyles.Add(new RowStyle(SizeType.Absolute, btnHght));
            labelTLP.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            labelButton = new Button()
            {
                Text = "Label",
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                Font = new Font("Arial", 12 * dpiScalingFactor, FontStyle.Bold),
                ForeColor = Color.DarkBlue,
                BackColor = Color.LightGray,
                FlatAppearance = { BorderSize = 0 },
                Height = (int)Math.Ceiling(fontHeight) * 2,
            };
            labelButton.Click += SetLabel_Click;
            labelTLP.Controls.Add(labelButton, 0, 0);

            labelGB = new GroupBox();
            labelGB.Dock = DockStyle.Top;
            labelGB.Text = "Sæt label";
            labelGB.ForeColor = Color.White;
            labelGB.Font = font;
            labelGB.Controls.Add(labelTLP);
            labelGB.Height = labelTLP.Height + pad + (int)Math.Ceiling(fontHeight);
            #endregion

            #region Width management init
            //Initialize buttons for width management
            //They are the same on all panels, so we can do it here
            //So the width management is:
            //1: GroupBox
            //  1.1: TableLayoutPanel
            //      1.1.1: Button
            //      1.1.2: Button
            widthManagementTLP = new TableLayoutPanel();
            widthManagementTLP.Dock = DockStyle.Fill;
            widthManagementTLP.RowCount = 1;
            widthManagementTLP.ColumnCount = 2;
            widthManagementTLP.RowStyles.Add(new RowStyle(SizeType.Absolute, btnHght));
            widthManagementTLP.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            widthManagementTLP.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            updateWidthsButton = new Button()
            {
                Text = "Opdater",
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                Font = new Font("Arial", 12 * dpiScalingFactor, FontStyle.Bold),
                ForeColor = Color.DarkBlue,
                BackColor = Color.LightGray,
                FlatAppearance = { BorderSize = 0 },
                Height = (int)Math.Ceiling(fontHeight) * 2,
            };
            updateWidthsButton.Click += OpdaterBredde_Click;
            widthManagementTLP.Controls.Add(updateWidthsButton, 0, 0);

            resetWidthsButton = new Button()
            {
                Text = "Nulstil",
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                Font = new Font("Arial", 12 * dpiScalingFactor, FontStyle.Bold),
                ForeColor = Color.DarkBlue,
                BackColor = Color.LightGray,
                FlatAppearance = { BorderSize = 0 },
                Height = (int)Math.Ceiling(fontHeight) * 2,
            };
            resetWidthsButton.Click += NulstilBredde_Click;
            widthManagementTLP.Controls.Add(resetWidthsButton, 1, 0);

            widthManagementGB = new GroupBox();
            widthManagementGB.Dock = DockStyle.Top;
            widthManagementGB.Text = "Polylinjer bredde";
            widthManagementGB.ForeColor = Color.White;
            widthManagementGB.Font = font;
            widthManagementGB.Controls.Add(widthManagementTLP);
            widthManagementGB.Height = widthManagementTLP.Height + pad + (int)Math.Ceiling(fontHeight);
            #endregion

            comboBox_PipeTypeSelector.SelectedIndexChanged += ComboBox_PipeTypeSelector_SelectedIndexChanged;
            ComboBox_PipeTypeSelector_SelectedIndexChanged(null, null);

            groupBox_ComboBoxHost.Height = comboBox_PipeTypeSelector.Height + pad;
        }
        private void ComboBox_PipeTypeSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            sets.pipePalette_PipeSystemTypeIndex = comboBox_PipeTypeSelector.SelectedIndex;
            sets.Save();

            currentComb = (PipeSystemTypeCombination)comboBox_PipeTypeSelector.SelectedItem;

            Font font = new Font("Microsoft Sans Serif", 12F);
            float fontHeight;

            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
            {
                fontHeight = font.GetHeight(g);
            }

            //Rebuild form
            basePanel.Controls.Clear();
            dnCheckBoxes.Clear();
            seriesCheckBoxes.Clear();

            GroupBox gpBox;
            TableLayoutPanel tblPnl;

            #region PL width update and reset buttons
            basePanel.Controls.Add(labelGB);
            basePanel.Controls.Add(widthManagementGB);
            #endregion

            #region Series buttons
            var listSeries = PipeScheduleV2.ListAllSeriesForPipeSystemType(currentComb.System, currentComb.Type)
                .Select(x => x.ToString()).ToList();

            if (listSeries.Count > 1)
            {
                gpBox = new GroupBox();
                basePanel.Controls.Add(gpBox);
                gpBox.Dock = DockStyle.Top;
                gpBox.Text = "Serie";
                gpBox.ForeColor = Color.White;
                gpBox.Font = font;
                tblPnl = new TableLayoutPanel();
                gpBox.Controls.Add(tblPnl);
                tblPnl.Dock = DockStyle.Fill;

                PopulateButtons(listSeries, 3, tblPnl, seriesButtonCheckBox_Click, seriesCheckBoxes);
                gpBox.Height = tblPnl.Height + pad + (int)Math.Ceiling(fontHeight);

                seriesCheckBoxes.OrderBy(x => x.Text).Last().Checked = true;
                seriesButtonCheckBox_Click(seriesCheckBoxes.OrderBy(x => x.Text).Last(), EventArgs.Empty);
            } else PaletteUtils.CurrentSeries = PipeSeriesEnum.S3;
            #endregion

            #region Settings for buttons
            float dpiScalingFactor = GetDpiScalingFactor(this);
            float btnHght = buttonHeight * dpiScalingFactor;
            #endregion

            #region Frem/retur init
            //Initialize buttons for frem/retur management
            //They are the same on all panels, so we can do it here
            //So the width management is:
            //1: GroupBox
            //  1.1: TableLayoutPanel
            //      1.1.1: Button
            //      1.1.2: Button
            if (currentComb.Type == PipeTypeEnum.Frem ||
                currentComb.Type == PipeTypeEnum.Retur ||
                currentComb.Type == PipeTypeEnum.Enkelt)
            {
                typeTLP = new TableLayoutPanel();
                typeTLP.Dock = DockStyle.Fill;
                typeTLP.RowCount = 1;
                typeTLP.ColumnCount = 2;
                typeTLP.RowStyles.Add(new RowStyle(SizeType.Absolute, btnHght));
                typeTLP.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
                typeTLP.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

                fremButton = new Button()
                {
                    Text = "Frem",
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill,
                    Font = new Font("Arial", 12 * dpiScalingFactor, FontStyle.Bold),
                    ForeColor = Color.DarkBlue,
                    BackColor = Color.LightGray,
                    FlatAppearance = { BorderSize = 0 },
                    Height = (int)Math.Ceiling(fontHeight) * 2,
                };
                fremButton.Click += fremButton_Click;
                typeTLP.Controls.Add(fremButton, 0, 0);

                returButton = new Button()
                {
                    Text = "Retur",
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill,
                    Font = new Font("Arial", 12 * dpiScalingFactor, FontStyle.Bold),
                    ForeColor = Color.DarkBlue,
                    BackColor = Color.LightGray,
                    FlatAppearance = { BorderSize = 0 },
                    Height = (int)Math.Ceiling(fontHeight) * 2,
                };
                returButton.Click += returButton_Click;
                typeTLP.Controls.Add(returButton, 1, 0);

                typeGB = new GroupBox();
                typeGB.Dock = DockStyle.Top;
                typeGB.Text = "Frem/Retur";
                typeGB.ForeColor = Color.White;
                typeGB.Font = font;
                typeGB.Controls.Add(typeTLP);
                typeGB.Height = typeTLP.Height + pad + (int)Math.Ceiling(fontHeight);

                basePanel.Controls.Add(typeGB);

                fremButton_Click(null, null);
            }
            #endregion
            
            #region Dn buttons
            //First create DN buttons
            gpBox = new GroupBox();
            basePanel.Controls.Add(gpBox);
            gpBox.Dock = DockStyle.Top;
            gpBox.Text = currentComb.ToString();
            gpBox.ForeColor = Color.White;
            gpBox.Font = font;
            tblPnl = new TableLayoutPanel();
            gpBox.Controls.Add(tblPnl);
            tblPnl.Dock = DockStyle.Fill;

            var listDns = PipeScheduleV2.ListAllDnsForPipeSystemType(
                currentComb.System, currentComb.Type)
                .OrderBy(x => x).Select(x => x.ToString()).ToList();

            PopulateButtons(listDns, 3, tblPnl, dnButtonCheckBox_Click, dnCheckBoxes);
            gpBox.Height = tblPnl.Height + pad + (int)Math.Ceiling(fontHeight);
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
            float btnHght = buttonHeight * dpiScalingFactor;

            int rowCount = buttonsData.Count / colCount;
            if (buttonsData.Count % colCount != 0) rowCount++;

            if (buttonsData.Count == 2) colCount = 2;
            else if (buttonsData.Count == 1) colCount = 1;

            tLP.RowCount = rowCount;
            tLP.ColumnCount = colCount;

            for (int i = 0; i < rowCount; i++)
                tLP.RowStyles.Add(new RowStyle(SizeType.Absolute, btnHght));

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

            tLP.Parent.Height = rowCount * ((int)Math.Round(btnHght)) + 20;
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

            PaletteUtils.ActivateLayer(currentComb.System, currentComb.Type, cb.Text);
        }
        private void seriesButtonCheckBox_Click(object sender, EventArgs e)
        {
            CheckBox cb = (CheckBox)sender;

            // Set the back color of the clicked checkbox
            cb.BackColor = Color.DarkGray;

            foreach (CheckBox checkBox in seriesCheckBoxes)
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
        private void fremButton_Click(object sender, EventArgs e)
        {
            currentComb.Type = PipeTypeEnum.Frem;

            fremButton.BackColor = Color.DarkGray;
            returButton.BackColor = Color.LightGray;
        }
        private void returButton_Click(object sender, EventArgs e)
        {
            currentComb.Type = PipeTypeEnum.Retur;

            returButton.BackColor = Color.DarkGray;
            fremButton.BackColor = Color.LightGray;
        }
    }
}
