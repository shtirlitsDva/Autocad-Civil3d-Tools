using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using static DriPaletteSet.PaletteUtils;
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

namespace DriPaletteSet
{
    public partial class TwinPalette : System.Windows.Forms.UserControl
    {
        HashSet<System.Windows.Forms.CheckBox> dnButtons = new HashSet<System.Windows.Forms.CheckBox>();
        HashSet<System.Windows.Forms.CheckBox> seriesButtons = new HashSet<System.Windows.Forms.CheckBox>();

        public TwinPalette()
        {
            InitializeComponent();

            #region Add dn buttons to buttons collection
            dnButtons.Add(checkBox1);
            dnButtons.Add(checkBox2);
            dnButtons.Add(checkBox3);
            dnButtons.Add(checkBox4);
            dnButtons.Add(checkBox5);
            dnButtons.Add(checkBox6);
            dnButtons.Add(checkBox7);
            dnButtons.Add(checkBox8);
            dnButtons.Add(checkBox9);
            dnButtons.Add(checkBox10);
            dnButtons.Add(checkBox11);
            dnButtons.Add(checkBox12);
            dnButtons.Add(checkBox16);
            dnButtons.Add(checkBox17);
            dnButtons.Add(checkBox18);
            dnButtons.Add(checkBox19);
            #endregion

            #region Add series buttons to series buttons collection
            seriesButtons.Add(checkBox13);
            seriesButtons.Add(checkBox14);
            seriesButtons.Add(checkBox15);
            #endregion

            //Change appearance to that of a button
            foreach (var cb in dnButtons) cb.Appearance = Appearance.Button;
            foreach (var cb in seriesButtons) cb.Appearance = Appearance.Button;

            //Initialize series settings
            checkBox15.Checked = true;
            PaletteUtils.CurrentSeries = PipeSeriesEnum.S3;
        }

        private void dnButtonCheckBox_Click(object sender, EventArgs e)
        {
            System.Windows.Forms.CheckBox cb = (System.Windows.Forms.CheckBox)sender;
            foreach (System.Windows.Forms.CheckBox checkBox in dnButtons)
                if (checkBox.Checked && cb.Name != checkBox.Name) checkBox.Checked = false;

            PipeSystemEnum system;
            string dn;
            if (cb.Text.StartsWith("DN "))
            {
                system = PipeSystemEnum.Stål;
                dn = cb.Text.Replace("DN ", "");
            }
            else if (cb.Text.StartsWith("ALUPEX "))
            {
                system = PipeSystemEnum.AluPex;
                dn = cb.Text.Replace("ALUPEX ", "");
            }
            else if (cb.Text.StartsWith("CU "))
            {
                system = PipeSystemEnum.Kobberflex;
                dn = cb.Text.Replace("CU ", "");
            }
            else throw new System.Exception($"Unknown pipe button text: {cb.Text}!");

            ActivateLayer(system, PipeTypeEnum.Twin, dn);
        }

        private void seriesButtonCheckBox_Click(object sender, EventArgs e)
        {
            System.Windows.Forms.CheckBox cb = (System.Windows.Forms.CheckBox)sender;
            foreach (System.Windows.Forms.CheckBox checkBox in seriesButtons)
                if (checkBox.Checked && cb.Name != checkBox.Name) checkBox.Checked = false;

            PipeSeriesEnum pipeSeriesEnum = 
                (PipeSeriesEnum)Enum.Parse(typeof(PipeSeriesEnum), cb.Text);

            PaletteUtils.CurrentSeries = pipeSeriesEnum;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            PaletteUtils.UpdateWidths();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            PaletteUtils.labelpipe();
        }

        private void button_NulstillGlobalWidth_Click(object sender, EventArgs e)
        {
            PaletteUtils.ResetWidths();
        }
    }
}
