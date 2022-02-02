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
using Autodesk.Civil.DataShortcuts;
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
    public class FjvPolylineLabel : Autodesk.AutoCAD.GraphicsInterface.DrawableOverrule
    {
        public FjvPolylineLabel()
        {
            base.SetCustomFilter();
        }

        //Settings
        private const double labelDist = 10;
        private const double labelOffset = 1.2;
        private const double labelHeight = 1.25;
        //public bool Enabled { get; set; } = false;
        public override bool IsApplicable(RXObject overruledSubject)
        {
            //Put a check of Enabled here if using that also
            return ((Polyline)overruledSubject).NumberOfVertices > 1 && isFjvPline(overruledSubject);
        }
        private bool isFjvPline(RXObject overruledSubject)
        {
            if (overruledSubject is Polyline pline)
            {
                if (pline.Database == null) return false;

                if (pline.Layer.Contains("FJV-TWIN") ||
                    pline.Layer.Contains("FJV-FREM") ||
                    pline.Layer.Contains("FJV-RETUR")) return true;
            }
            return false;
        }
        public override bool WorldDraw(
            Autodesk.AutoCAD.GraphicsInterface.Drawable drawable, 
            Autodesk.AutoCAD.GraphicsInterface.WorldDraw wd)
        {
            base.WorldDraw(drawable, wd);

            Polyline pline = (Polyline)drawable;

            double length = pline.Length;
            int numberOfLabels = (int)(length / labelDist);
            if (numberOfLabels == 0) numberOfLabels = 1;

            for (int i = 0; i < numberOfLabels; i++)
            {
                double dist = labelDist * i;
                if (numberOfLabels == 1) dist = length / 2;
                Point3d pt = pline.GetPointAtDist(dist);
                int dn = IntersectUtilities.PipeSchedule.GetPipeDN(pline);
                string system = 
                    IntersectUtilities.PipeSchedule.GetPipeSystem(pline) == "Twin" ?
                    "T" : "E";
                string label = $"{system}{dn}";

                Vector3d deriv = pline.GetFirstDerivative(pt);
                deriv = deriv.GetNormal();

                Vector3d perp = deriv.GetPerpendicularVector();

                wd.Geometry.Text(
                    pt + perp * labelOffset, Vector3d.ZAxis, deriv, labelHeight, 1.0, 0.0, label);
            }

            return true;
        }
    }

    public class Commands
    {
        private static FjvPolylineLabel _fjvPolylineLabelOverrule;

        [CommandMethod("TOGGLEFJVLABEL")]
        public static void togglefjvlabeloverrule()
        {
            if (_fjvPolylineLabelOverrule == null)
            {
                _fjvPolylineLabelOverrule = new FjvPolylineLabel();
                Overrule.AddOverrule(RXObject.GetClass(typeof(Polyline)), _fjvPolylineLabelOverrule, false);
                Overrule.Overruling = true;
            }
            else
            {
                Overrule.RemoveOverrule(RXObject.GetClass(typeof(Polyline)), _fjvPolylineLabelOverrule);
                _fjvPolylineLabelOverrule.Dispose();
                _fjvPolylineLabelOverrule = null;
            }
            Application.DocumentManager.MdiActiveDocument.Editor.Regen();
        }
    }
}
