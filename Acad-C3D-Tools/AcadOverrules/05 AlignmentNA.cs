using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Aec.PropertyData;
using Autodesk.Aec.PropertyData.DatabaseServices;
//using Autodesk.AutoCAD.GraphicsInterface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Data;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities;

using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.UtilsCommon.UtilsODData;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = Autodesk.Civil.DatabaseServices.Label;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;

namespace AcadOverrules
{
    public class AlignmentNaMark : Autodesk.AutoCAD.GraphicsInterface.DrawableOverrule
    {
        //Settings
        private const double labelOffset = 1.2;
        private const double labelHeight = 1.0;

        #region Style definition
        private Autodesk.AutoCAD.GraphicsInterface.TextStyle style =
            new Autodesk.AutoCAD.GraphicsInterface.TextStyle
            (
                "Arial",
                "Arial",
                labelHeight,
                0.0,
                0.0,
                0.0,
                false,
                false,
                false,
                false,
                false,
                false,
                "MyStd"
                );
        #endregion

        PSetDefs.DriPipelineData psPd = new PSetDefs.DriPipelineData();

        public AlignmentNaMark()
        {
            base.SetCustomFilter();
        }

        //public bool Enabled { get; set; } = false;
        public override bool IsApplicable(RXObject overruledSubject)
        {
            //Put a check of Enabled here if using that also
            return ((Polyline)overruledSubject).NumberOfVertices > 1 && isApplicable(overruledSubject);
        }
        private bool isApplicable(RXObject overruledSubject)
        {
            if (overruledSubject is Polyline pline)
            {
                if (pline.Database == null) return false;
                if (PropertySetManager.IsPropertySetAttached(pline, psPd.SetName))
                {
                    if (PropertySetManager.ReadNonDefinedPropertySetString(
                        pline, psPd.SetName.ToString(), psPd.BelongsToAlignment.ToString()) == "NA")
                        return true;
                    else return false;
                }
                else return false;
            }
            return false;
        }
        public override bool WorldDraw(
            Autodesk.AutoCAD.GraphicsInterface.Drawable drawable,
            Autodesk.AutoCAD.GraphicsInterface.WorldDraw wd)
        {
            base.WorldDraw(drawable, wd);

            Polyline pline = (Polyline)drawable;

            #region Size label
            double length = pline.Length;
            double dist = length / 2;
            Point3d pt = pline.GetPointAtDist(dist);
            int dn = PropertySetManager.ReadNonDefinedPropertySetInt(pline, "DriGasDimOgMat", "Dimension");
            string mat = PropertySetManager.ReadNonDefinedPropertySetString(pline, "DriGasDimOgMat", "Material");

            if (mat.IsNoE() && dn == 0) return true;
            if (!mat.IsNoE()) mat = " " + mat;

            string label = $"{dn}{mat}";

            if (
                pline.Layer == "GAS-ude af drift" ||
                pline.Layer == "GAS-ude af drift-2D"
                ) label += " UAD";

            Vector3d deriv = pline.GetFirstDerivative(pt);
            deriv = deriv.GetNormal();

            Vector3d perp = deriv.GetPerpendicularVector();

            wd.Geometry.Text(
                pt + perp * labelOffset, Vector3d.ZAxis, deriv, label, true, style);
            //pt + perp * labelOffset, Vector3d.ZAxis, deriv, labelHeight, 1.0, 0.0, label);

            //wd.Geometry.Text(
            //    pt + perp * labelOffset, Vector3d.ZAxis, deriv, labelHeight, 1.0, 0.0, label);
            #endregion


            return true;
        }
    }
}
