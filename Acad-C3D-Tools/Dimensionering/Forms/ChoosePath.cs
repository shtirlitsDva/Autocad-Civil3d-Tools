using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Forms;
using DimensioneringV2.Properties;

namespace IntersectUtilities.Dimensionering.Forms
{
    public partial class ChoosePath : Form
    {

        public string Path = string.Empty;
        private string[] dirsSparse;

        public ChoosePath()
        {
            InitializeComponent();

            string[] dirs = Directory.GetDirectories(@"X:\AutoCAD DRI - QGIS\BBR UDTRÆK");

            dirsSparse = new string[dirs.Length];

            for (int i = 0; i < dirs.Length; i++)
            {
                dirsSparse[i] = dirs[i].Split('\\').Last();
            }

            this.comboBox1.DataSource = dirsSparse;

            if (Settings.Default.ChosenPath != string.Empty)
            {
                this.comboBox1.SelectedIndex = Array.IndexOf(
                    dirsSparse, Settings.Default.ChosenPath);

            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Path = dirsSparse[comboBox1.SelectedIndex];
            Settings.Default.ChosenPath = Path;
            Settings.Default.Save();
            this.Close();
        }

        private void ChoosePath_Shown(object sender, EventArgs e)
        {
            button1.Focus();
        }
    }
}
