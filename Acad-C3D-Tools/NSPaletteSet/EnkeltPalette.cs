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
using static NSPaletteSet.PaletteUtils;
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using IntersectUtilities.UtilsCommon.Enums;

namespace NSPaletteSet
{
    public partial class EnkeltPalette : UserControl
    {
        HashSet<CheckBox> dnButtons = new HashSet<CheckBox>();
        HashSet<CheckBox> seriesButtons = new HashSet<CheckBox>();
        HashSet<CheckBox> fremReturButtons = new HashSet<CheckBox> { };
        public EnkeltPalette()
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
            dnButtons.Add(checkBox13);
            dnButtons.Add(checkBox14);
            dnButtons.Add(checkBox15);
            dnButtons.Add(checkBox16);
            dnButtons.Add(checkBox17);
            dnButtons.Add(checkBox18);
            dnButtons.Add(checkBox21);
            dnButtons.Add(checkBox22);
            dnButtons.Add(checkBox23);
            dnButtons.Add(checkBox24);
            #endregion

            #region Add frem/retur buttons to fr collection
            fremReturButtons.Add(checkBox19);
            fremReturButtons.Add(checkBox20);
            #endregion

            //Change appearance to that of a button
            foreach (var cb in dnButtons) cb.Appearance = Appearance.Button;
            foreach (var cb in fremReturButtons) cb.Appearance = Appearance.Button;

            //Init frem check box
            checkBox19.Checked = true;
        }

        private void dnButtonCheckBox_Click(object sender, EventArgs e)
        {
            System.Windows.Forms.CheckBox cb = (System.Windows.Forms.CheckBox)sender;
            foreach (System.Windows.Forms.CheckBox checkBox in dnButtons)
                if (checkBox.Checked && cb.Name != checkBox.Name) checkBox.Checked = false;

            PipeTypeEnum fr = PipeTypeEnum.Frem;
            foreach (var frb in fremReturButtons)
                if (frb.Checked) fr = (PipeTypeEnum)Enum.Parse(typeof(PipeTypeEnum), frb.Text);

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

            ActivateLayer(PipeSystemEnum.Stål, fr, dn);
        }

        private void frButtonCheckBox_Click(object sender, EventArgs e)
        {
            System.Windows.Forms.CheckBox cb = (System.Windows.Forms.CheckBox)sender;
            foreach (System.Windows.Forms.CheckBox checkBox in fremReturButtons)
                if (checkBox.Checked && cb.Name != checkBox.Name) checkBox.Checked = false;

            foreach (var btn in dnButtons)
            {
                if (btn.Checked)
                {
                    PipeTypeEnum fr = (PipeTypeEnum)Enum.Parse(typeof(PipeTypeEnum), cb.Text);

                    PipeSystemEnum system;
                    string dn;
                    if (btn.Text.StartsWith("DN "))
                    {
                        system = PipeSystemEnum.Stål;
                        dn = btn.Text.Replace("DN ", "");
                    }
                    else if (btn.Text.StartsWith("ALUPEX "))
                    {
                        system = PipeSystemEnum.AluPex;
                        dn = btn.Text.Replace("ALUPEX ", "");
                    }
                    else if (btn.Text.StartsWith("CU "))
                    {
                        system = PipeSystemEnum.Kobberflex;
                        dn = btn.Text.Replace("CU ", "");
                    }
                    else throw new System.Exception($"Unknown pipe button text: {cb.Text}!");

                    ActivateLayer(system, fr, dn);
                }
            }
        }
    }
}
