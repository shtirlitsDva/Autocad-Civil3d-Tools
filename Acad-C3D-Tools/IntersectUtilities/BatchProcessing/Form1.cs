using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;

namespace IntersectUtilities
{
    public partial class BatchProccessingForm : Form
    {
        private Type _classType;
        private List<MethodInfo> _methods;
        private int w1 = 50;
        private int w2 = 300;
        private int w3 = 70;
        private TableLayoutPanel tblp;
        public List<MethodInfo> methodsToExecute = new List<MethodInfo>();

        public BatchProccessingForm()
        {
            InitializeComponent();

            //get type information
            _classType = typeof(BatchProcesses);
            _methods = _classType.GetMethods(
                BindingFlags.Public | BindingFlags.Static)
                .ToList();

            //Measure longest label
            foreach (var method in _methods)
            {
                var attr = method.GetCustomAttribute(typeof(MethodDescription));
                MethodDescription methodDescription = (MethodDescription)attr;
                w2 = Math.Max(w2, TextRenderer.MeasureText(
                    methodDescription.ShortDescription, this.Font).Width) + 50;
            }

            //Set form size
            this.Width = w1 + w2 + w3 + 50;
            this.Height = 100 + _methods.Count * 25;

            #region Create_TLP
            //create a row in the TableLayoutPanel for each method
            tblp = new TableLayoutPanel();
            tblp.ColumnCount = 3;
            tblp.RowCount = 2;
            tblp.Dock = DockStyle.Fill;
            tblp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, w1));
            tblp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, w2));
            tblp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, w3));
            this.Controls.Add(tblp);
            #endregion

            #region Run button and headings
            //Add "Run" button
            Button btn_Run = new Button();
            btn_Run.Text = "Run!";
            tblp.Controls.Add(btn_Run, 2, 0);
            tblp.SetColumnSpan(btn_Run, tblp.ColumnCount);
            btn_Run.Click += (sender, args) => ExecuteSelectedMethods();

            //Add labels
            Label lbl0 = new Label();
            lbl0.Text = "Run";
            lbl0.Width = w1;
            lbl0.TextAlign = ContentAlignment.MiddleLeft;
            tblp.Controls.Add(lbl0, 0, 1);

            Label lbl1 = new Label();
            lbl1.Text = "Beskrivelse af funktion";
            lbl1.TextAlign = ContentAlignment.MiddleLeft;
            lbl1.Width = 300;
            tblp.Controls.Add(lbl1, 1, 1);

            Label lbl2 = new Label();
            lbl2.Text = "Indstillinger";
            lbl2.TextAlign = ContentAlignment.MiddleLeft;
            tblp.Controls.Add(lbl2, 2, 1);
            #endregion

            for (int i = 0; i < _methods.Count; i++)
            {
                tblp.RowCount++;
                tblp.RowStyles.Add(
                    new RowStyle(SizeType.AutoSize));
                AddControlsForMethod(i);
            }
        }

        private void AddControlsForMethod(int index)
        {
            var method = _methods[index];
            int rowIdx = index + 2;

            var checkbox = new CheckBox()
            {
                Checked = false
            };
            checkbox.Dock = DockStyle.Fill;

            //access the custom attribute to retreive 
            var attr = method.GetCustomAttribute(typeof(MethodDescription));
            MethodDescription methodDescription = (MethodDescription)attr;

            var label = new Label()
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

            var settingsButton = new Button()
            {
                Text = "Edit",
                Enabled = false,
            };
            settingsButton.Dock = DockStyle.Fill;

            //Add controls to tblp
            tblp.Controls.Add(checkbox, 0, rowIdx);
            tblp.Controls.Add(label, 1, rowIdx);
            tblp.Controls.Add(settingsButton, 2, rowIdx);

            //Add events
            checkbox.CheckedChanged += (sender, args) =>
            {
                // enable/disable button based on checkbox state
                settingsButton.Enabled = checkbox.Checked;
            };

            //Event to run settings
            settingsButton.Click += (sender, args) =>
            {
                //Create form to input method arguments
                var form = new Form()
                {
                    Text = method.Name + " Settings",
                    Size = new Size(400, 200),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedSingle,
                    ControlBox = true,
                    ShowInTaskbar = false,
                };
                form.Controls.Add(new Label()
                {
                    Text = "Enter arguments:",
                    AutoSize = true,
                    Dock = DockStyle.Top,
                });

                // create input controls for each method parameter
                var parameters = method.GetParameters();
                for (int i = 0; i < parameters.Length; i++)
                {
                    var parameter = parameters[i];
                    form.Controls.Add(new Label()
                    {
                        Text = parameter.Name + ":",
                        AutoSize = true,
                        Dock = DockStyle.Top,
                    });

                    var textBox = new TextBox()
                    {
                        Dock = DockStyle.Top,
                    };
                    if (parameter.ParameterType == typeof(List<string>))
                    {
                        textBox.Text = "arg1,arg2,arg3";
                    }
                    form.Controls.Add(textBox);
                }

                form.ShowDialog();
            };
        }
        private void ExecuteSelectedMethods()
        {
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

            methodsToExecute = methods;
        }
    }
}
