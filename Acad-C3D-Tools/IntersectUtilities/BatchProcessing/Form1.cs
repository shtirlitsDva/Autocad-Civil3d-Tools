﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;
using Autodesk.AutoCAD.DatabaseServices;
using DarkUI.Forms;
using DarkUI.Controls;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager;

namespace IntersectUtilities
{
    public partial class BatchProccessingForm : DarkForm
    {
        private Type _classType;
        private List<MethodInfo> _methods;
        private int w1 = 50;
        private int w2 = 200;
        private int w3 = 70;
        private TableLayoutPanel tblp;
        private Dictionary<string, Dictionary<string, object>> _argsDict =
            new Dictionary<string, Dictionary<string, object>>();
        public List<MethodInfo> MethodsToExecute = new List<MethodInfo>();
        public Dictionary<string, object[]> ArgsToExecute =
            new Dictionary<string, object[]>();

        public BatchProccessingForm(Counter counter)
        {
            InitializeComponent();
            this.Load += (sender, args) => BatchProccessingForm_Load(counter);
        }

        private void AddControlsForMethod(int index, Counter counter)
        {
            var method = _methods[index];
            int rowIdx = index + 2;

            var checkbox = new DarkCheckBox()
            {
                Checked = false
            };
            checkbox.Dock = DockStyle.Fill;

            //access the custom attribute to retreive 
            var attr = method.GetCustomAttribute(typeof(MethodDescription));
            MethodDescription methodDescription = (MethodDescription)attr;

            var label = new DarkLabel()
            {
                Text = methodDescription.ShortDescription,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            label.Dock = DockStyle.Fill;

            //add a tooltip to label
            label.MouseHover += (sender, args) =>
            {
                ToolTip toolTip = new ToolTip();
                toolTip.ToolTipTitle = "Beskrivelse";
                toolTip.SetToolTip(label, methodDescription.LongDescription);
            };

            var settingsButton = new DarkButton()
            {
                Text = "Edit",
                Enabled = false,
            };
            settingsButton.Dock = DockStyle.Fill;

            //Add controls to tblp
            tblp.Controls.Add(checkbox, 0, rowIdx);
            tblp.Controls.Add(label, 1, rowIdx);
            tblp.Controls.Add(settingsButton, 2, rowIdx);
            if (method.GetParameters().Length < 2)
                settingsButton.Text = "N/A";

            //Add events
            checkbox.CheckedChanged += (sender, args) =>
            {
                // enable/disable button based on checkbox state
                settingsButton.Enabled = checkbox.Checked;
            };

            //Event to run settings
            settingsButton.Click += (sender, args) =>
            {
                var parameters = method.GetParameters();
                //check to see if method has parameters that need to be set
                if (parameters.Length == 1) return;

                //Create form to input method arguments
                var form = new DarkForm()
                {
                    Text = methodDescription.ShortDescription,
                    Size = new Size(400, 300),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedSingle,
                    ShowInTaskbar = false,
                };

                var panel2 = new Panel()
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                };
                form.Controls.Add(panel2);

                TableLayoutPanel tblSet = new TableLayoutPanel();
                tblSet.ColumnCount = 2;
                tblSet.Dock = DockStyle.Fill;
                tblSet.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                //form.Controls.Add(tblSet);
                panel2.Controls.Add(tblSet);

                // create input controls for each method parameter
                for (int i = 0; i < parameters.Length; i++)
                {
                    var parameter = parameters[i];
                    //First parameter is always database
                    if (parameter.ParameterType == typeof(Database)) continue;

                    //Add row to tblSet
                    tblSet.RowCount++;

                    tblSet.Controls.Add(new DarkLabel()
                    {
                        Text = methodDescription.ArgDescriptions[i - 1] + ":",
                        AutoSize = true,
                        Dock = DockStyle.Top,
                    }, 0, tblSet.RowCount - 1);

                    switch (parameter.ParameterType)
                    {
                        case Type t when t == typeof(string):
                            {
                                var textBox = new DarkTextBox()
                                {
                                    Name = parameter.Name,
                                    Dock = DockStyle.Fill,
                                };
                                textBox.TextChanged += (sender2, args2) =>
                                {
                                    _argsDict[method.Name][parameter.Name] = 
                                        textBox.Text.IsNoE() ? string.Empty : textBox.Text;
                                };
                                tblSet.Controls.Add(textBox, 1, tblSet.RowCount - 1);
                            }
                            break;
                        case Type t when t == typeof(DataReferencesOptions):
                            {
                                var droBtn = new DarkButton()
                                {
                                    Text = "Click",
                                    AutoSize = true,
                                    Dock = DockStyle.Fill,
                                };
                                tblSet.Controls.Add(droBtn, 1, tblSet.RowCount - 1);
                                droBtn.Click += (sender3, args3) =>
                                {
                                    DataReferencesOptions dro = new DataReferencesOptions();
                                    _argsDict[method.Name][parameter.Name] = dro;
                                };
                            }
                            break;
                        case Type t when t == typeof(Counter):
                            {
                                var counterBox = new DarkTextBox()
                                {
                                    Dock = DockStyle.Fill,
                                    Text = "Counter",
                                    ReadOnly = true,
                                    Enabled = false,
                                };
                                tblSet.Controls.Add(counterBox, 1, tblSet.RowCount - 1);

                                _argsDict[method.Name][parameter.Name] = counter;
                            }
                            break;
                        case Type t when t == typeof(int):
                            {
                                var intBox = new DarkTextBox()
                                {
                                    Dock = DockStyle.Fill,
                                };

                                intBox.TextChanged += (sender4, args4) =>
                                {
                                    int value = 0;
                                    if (int.TryParse(intBox.Text, out value))
                                    {
                                        _argsDict[method.Name][parameter.Name] = value;
                                    }
                                };
                                tblSet.Controls.Add(intBox, 1, tblSet.RowCount - 1);
                            }
                            break;

                        default:
                            throw new Exception(
                                $"Type {parameter.ParameterType.Name} " +
                                $"is not implemented! (Error 0x02394054)");
                    }
                }

                form.ShowDialog();
            };
        }

        private void ExecuteSelectedMethods()
        {
            #region Gather checked methods
            List<MethodInfo> methods = new List<MethodInfo>();
            var checkboxes = tblp.Controls.OfType<CheckBox>();
            foreach (CheckBox checkbox in checkboxes)
            {
                if (checkbox.Checked)
                {
                    int rowNumber = tblp.GetRow(checkbox);
                    methods.Add(_methods[rowNumber - 2]);
                }
            }
            MethodsToExecute = methods;
            #endregion

            foreach (MethodInfo method in MethodsToExecute)
            {
                var parameters = method.GetParameters();
                object[] args = new object[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    args[i] = _argsDict[method.Name][parameters[i].Name];
                }

                ArgsToExecute.Add(method.Name, args);
            }

            this.Close();
        }

        private void BatchProccessingForm_Load(Counter counter)
        {
            //get type information
            _classType = typeof(BatchProcesses);
            _methods = _classType.GetMethods(
                BindingFlags.Public | BindingFlags.Static)
                .ToList();

            //Populate methods arguments dict
            //this dict is needed to keep track of MethodInfos and their arguments
            foreach (var method in _methods)
            {
                _argsDict.Add(method.Name, new Dictionary<string, object>());

                foreach (var param in method.GetParameters())
                {
                    _argsDict[method.Name].Add(param.Name, new object());
                }
            }

            //Measure longest label
            foreach (var method in _methods)
            {
                var attr = method.GetCustomAttribute(typeof(MethodDescription));
                MethodDescription methodDescription = (MethodDescription)attr;
                w2 = Math.Max(w2, TextRenderer.MeasureText(
                    methodDescription.ShortDescription, this.Font).Width) + 50;
            }

            #region Create_TLP
            //create a row in the TableLayoutPanel for each method
            tblp = new TableLayoutPanel();
            tblp.ColumnCount = 3;
            tblp.RowCount = 2 + _methods.Count;
            tblp.Dock = DockStyle.Fill;
            tblp.AutoScroll = true;
            tblp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, w1));
            tblp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, w2));
            tblp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, w3));

            tblp.GrowStyle = TableLayoutPanelGrowStyle.AddRows;
            tblp.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            tblp.Padding = 
                new Padding(0, 0, 
                SystemInformation.VerticalScrollBarWidth, 
                SystemInformation.HorizontalScrollBarHeight);

            //this.Controls.Add(tblp);
            panel1.Controls.Add(tblp);
            #endregion

            #region Run button and headings
            //Add "Run" button
            DarkButton btn_Run = new();
            btn_Run.Text = "Run!";
            tblp.Controls.Add(btn_Run, 2, 0);
            tblp.SetColumnSpan(btn_Run, tblp.ColumnCount);
            btn_Run.Click += (sender, args) => ExecuteSelectedMethods();

            //Add labels
            DarkLabel lbl0 = new();
            lbl0.Text = "Run";
            lbl0.Width = w1;
            lbl0.TextAlign = ContentAlignment.MiddleLeft;
            tblp.Controls.Add(lbl0, 0, 1);

            DarkLabel lbl1 = new();
            lbl1.Text = "Beskrivelse af funktion";
            lbl1.TextAlign = ContentAlignment.MiddleLeft;
            lbl1.Width = 300;
            tblp.Controls.Add(lbl1, 1, 1);

            DarkLabel lbl2 = new();
            lbl2.Text = "Indstillinger";
            lbl2.TextAlign = ContentAlignment.MiddleLeft;
            tblp.Controls.Add(lbl2, 2, 1);
            #endregion

            for (int i = 0; i < _methods.Count; i++)
            {
                AddControlsForMethod(i, counter);
            }

            //Set form size
            this.Width = w1 + w2 + w3 + 50;
            this.Height = 100 + _methods.Count * 25;
        }
    }
}
