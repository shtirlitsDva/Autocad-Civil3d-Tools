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
using static IntersectUtilities.PipeSchedule;

namespace DriPaletteSet
{
    public partial class EnkeltPalette : UserControl
    {
        HashSet<System.Windows.Forms.CheckBox> dnButtons = new HashSet<System.Windows.Forms.CheckBox>();
        HashSet<System.Windows.Forms.CheckBox> seriesButtons = new HashSet<System.Windows.Forms.CheckBox>();
        HashSet<System.Windows.Forms.CheckBox> fremReturButtons = new HashSet<System.Windows.Forms.CheckBox> { };
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
            {
                if (checkBox.Checked && cb.Name != checkBox.Name)
                {
                    checkBox.Checked = false;
                }
            }

            string dn = string.Concat(cb.Text.Where(c => !char.IsWhiteSpace(c)));
            PipeDnEnum pipeDn = (PipeDnEnum)Enum.Parse(typeof(PipeDnEnum), dn);

            PipeTypeEnum fr = PipeTypeEnum.Frem;
            foreach (var frb in fremReturButtons)
            {
                if (frb.Checked) fr = (PipeTypeEnum)Enum.Parse(typeof(PipeTypeEnum), frb.Text);
            }

            ActivateLayer(fr, pipeDn);
        }

        private void frButtonCheckBox_Click(object sender, EventArgs e)
        {
            System.Windows.Forms.CheckBox cb = (System.Windows.Forms.CheckBox)sender;
            foreach (System.Windows.Forms.CheckBox checkBox in fremReturButtons)
            {
                if (checkBox.Checked && cb.Name != checkBox.Name)
                {
                    checkBox.Checked = false;
                }
            }

            foreach (var btn in dnButtons)
            {
                if (btn.Checked)
                {
                    string dn = string.Concat(btn.Text.Where(c => !char.IsWhiteSpace(c)));
                    PipeDnEnum pipeDn = (PipeDnEnum)Enum.Parse(typeof(PipeDnEnum), dn);

                    PipeTypeEnum fr = (PipeTypeEnum)Enum.Parse(typeof(PipeTypeEnum), cb.Text);

                    ActivateLayer(fr, pipeDn);
                }
            }
        }
    }
}
