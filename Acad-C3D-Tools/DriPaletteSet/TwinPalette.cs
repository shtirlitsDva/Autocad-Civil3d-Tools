using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace DriPaletteSet
{
    public partial class TwinPalette : System.Windows.Forms.UserControl
    {
        Editor editor = Application.DocumentManager.MdiActiveDocument.Editor;

        HashSet<CheckBox> dnButtonsCol = new HashSet<CheckBox>();

        public TwinPalette()
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
            #endregion

            #region Add dn buttons to buttons collection
            dnButtonsCol.Add(checkBox1);
            dnButtonsCol.Add(checkBox2);
            dnButtonsCol.Add(checkBox3);
            dnButtonsCol.Add(checkBox4);
            dnButtonsCol.Add(checkBox5);
            dnButtonsCol.Add(checkBox6);
            dnButtonsCol.Add(checkBox7);
            dnButtonsCol.Add(checkBox8);
            dnButtonsCol.Add(checkBox9);
            dnButtonsCol.Add(checkBox10);
            dnButtonsCol.Add(checkBox11);
            dnButtonsCol.Add(checkBox12);
            #endregion
        }

        private void dnButtonCheckBox_Click(object sender, EventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            foreach (CheckBox checkBox in dnButtonsCol)
            {
                if (checkBox.Checked && cb.Name != checkBox.Name)
                {
                    checkBox.Checked = false;
                }
            }
        }
    }
}
