using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using DarkUI;
using DarkUI.Forms;
using DarkUI.Controls;

using IntersectUtilities.PipeScheduleV2;

namespace IntersectUtilities.PipelineNetworkSystem
{
    public partial class PipeSettingsForm : DarkForm
    {
        PipeSettings _settings;

        public PipeSettingsForm()
        {
            InitializeComponent();
        }
        public void CreatePipeSettingsGrid(PipeSettings settings)
        {
            _settings = settings;
            BorderStyle borderStyle = BorderStyle.None;
            
            TableLayoutPanel table = new TableLayoutPanel
            {
                ColumnCount = 2,  // Set two columns
                RowCount = 0,
                Dock = DockStyle.Fill,
                AutoScroll = true
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50F)); // Column for labels
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Column for radio buttons
            this.Controls.Add(table);

            // Title label
            DarkLabel titleLabel = new DarkLabel
            {
                Text = settings.Name,
                Font = new Font("Arial", 16, FontStyle.Bold),
                Dock = DockStyle.Fill,
                Height = 30,
                BorderStyle = borderStyle
            };
            table.Controls.Add(titleLabel, 0, table.RowCount);
            table.SetColumnSpan(titleLabel, 2); // This makes titleLabel span two columns
            table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));

            foreach (var settingSystem in settings.Settings.Values)
            {
                var options = PipeScheduleV2.PipeScheduleV2.GetStdLengthsForSystem(settingSystem.PipeTypeSystem);
                if (options.Length == 1) continue;

                DarkLabel systemLabel = new DarkLabel
                {
                    Text = settingSystem.Name,
                    Font = new Font("Arial", 12, FontStyle.Regular),
                    Dock = DockStyle.Fill,
                    BorderStyle = borderStyle
                };
                table.Controls.Add(systemLabel, 0, table.RowCount);
                table.SetColumnSpan(systemLabel, 2);
                table.RowCount++;
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));

                foreach (var settingType in settingSystem.Settings.Values)
                {
                    DarkLabel typeLabel = new DarkLabel
                    {
                        Text = settingType.Name.ToString(),
                        Dock = DockStyle.Fill,
                        BorderStyle = borderStyle
                    };
                    table.Controls.Add(typeLabel, 0, table.RowCount);
                    table.SetColumnSpan(typeLabel, 2);
                    table.RowCount++;
                    table.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));

                    foreach (var sizeSetting in settingType.Settings.Keys)
                    {
                        DarkLabel sizeLabel = new DarkLabel
                        {
                            Text = sizeSetting.ToString(),
                            Dock = DockStyle.Fill,
                            BorderStyle = borderStyle,
                            TextAlign = ContentAlignment.MiddleRight,
                        };
                        table.Controls.Add(sizeLabel, 0, table.RowCount);

                        FlowLayoutPanel flowPanel = new FlowLayoutPanel
                        {
                            Dock = DockStyle.Fill
                        };
                        table.Controls.Add(flowPanel, 1, table.RowCount++);
                        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));

                        foreach (int option in options)
                        {
                            DarkRadioButton rb = new DarkRadioButton
                            {
                                Text = option.ToString(),
                                Tag = new { 
                                    PipeTypeName = settingSystem.Name, 
                                    PipeTypeType = settingType.Name,
                                    Size = sizeSetting,
                                    Option = option 
                                },
                                Checked = settingType.Settings[sizeSetting] == option,
                                AutoSize = true,
                                Margin = new Padding(2)
                            };
                            rb.CheckedChanged += RadioButton_CheckedChanged;
                            flowPanel.Controls.Add(rb);
                        }
                    }
                }
            }
        }
        private void RadioButton_CheckedChanged(object sender, EventArgs e)
        {
            RadioButton rb = sender as RadioButton;
            if (rb.Checked)
            {
                dynamic tag = rb.Tag;
                Console.WriteLine($"Size {tag.Size}: Set to {tag.Option}");
            }
        }
        private void PipeSettingsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            foreach (Control c1 in this.Controls)
            {
                if (c1 is TableLayoutPanel panel)
                {
                    foreach (Control c2 in panel.Controls)
                    {
                        if (c2 is FlowLayoutPanel flowPanel)
                        {
                            foreach (RadioButton rb in flowPanel.Controls.OfType<RadioButton>())
                            {
                                if (rb.Checked)
                                {
                                    dynamic tag = rb.Tag;
                                    _settings.Settings[tag.PipeTypeName]
                                        .Settings[tag.PipeTypeType]
                                        .Settings[tag.Size] = tag.Option;
                                }
                            }
                        } 
                    }
                }
            }
        }
    }
}
