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
        HashSet<CheckBox> dnButtons = new HashSet<CheckBox>();
        HashSet<CheckBox> seriesButtons = new HashSet<CheckBox>();
        HashSet<CheckBox> fremReturButtons = new HashSet<CheckBox> { };
        public EnkeltPalette()
        {
            InitializeComponent();

            #region Change appearance
            checkBox1.Appearance = System.Windows.Forms.Appearance.Button;
            checkBox2.Appearance = System.Windows.Forms.Appearance.Button;
            checkBox3.Appearance = System.Windows.Forms.Appearance.Button;
            checkBox4.Appearance = System.Windows.Forms.Appearance.Button;
            checkBox5.Appearance = System.Windows.Forms.Appearance.Button;
            checkBox6.Appearance = System.Windows.Forms.Appearance.Button;
            checkBox7.Appearance = System.Windows.Forms.Appearance.Button;
            checkBox8.Appearance = System.Windows.Forms.Appearance.Button;
            checkBox9.Appearance = System.Windows.Forms.Appearance.Button;
            checkBox10.Appearance = System.Windows.Forms.Appearance.Button;
            checkBox11.Appearance = System.Windows.Forms.Appearance.Button;
            checkBox12.Appearance = System.Windows.Forms.Appearance.Button;
            checkBox13.Appearance = System.Windows.Forms.Appearance.Button;
            checkBox14.Appearance = System.Windows.Forms.Appearance.Button;
            checkBox15.Appearance = System.Windows.Forms.Appearance.Button;
            checkBox16.Appearance = System.Windows.Forms.Appearance.Button;
            checkBox17.Appearance = System.Windows.Forms.Appearance.Button;
            checkBox18.Appearance = System.Windows.Forms.Appearance.Button;
            checkBox19.Appearance = System.Windows.Forms.Appearance.Button;
            checkBox20.Appearance = System.Windows.Forms.Appearance.Button;
            #endregion

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
            #endregion

            #region Add frem/retur buttons to fr collection
            fremReturButtons.Add(checkBox19);
            fremReturButtons.Add(checkBox20);
            #endregion

            //Init frem check box
            checkBox19.Checked = true;
        }

        private void dnButtonCheckBox_Click(object sender, EventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            foreach (CheckBox checkBox in dnButtons)
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
            CheckBox cb = (CheckBox)sender;
            foreach (CheckBox checkBox in fremReturButtons)
            {
                if (checkBox.Checked && cb.Name != checkBox.Name)
                {
                    checkBox.Checked = false;
                }
            }
        }
    }
}
