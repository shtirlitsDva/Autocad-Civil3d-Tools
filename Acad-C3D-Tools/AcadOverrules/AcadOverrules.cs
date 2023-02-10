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
        
        private static GasPolylineLabel _GasPolylineLabelOverrule;

        [CommandMethod("TOGGLEGASLABEL")]
        public static void togglegaslabeloverrule()
        {
            if (_GasPolylineLabelOverrule == null)
            {
                _GasPolylineLabelOverrule = new GasPolylineLabel();
                Overrule.AddOverrule(RXObject.GetClass(typeof(Polyline)), _GasPolylineLabelOverrule, false);
                Overrule.Overruling = true;
            }
            else
            {
                Overrule.RemoveOverrule(RXObject.GetClass(typeof(Polyline)), _GasPolylineLabelOverrule);
                _GasPolylineLabelOverrule.Dispose();
                _GasPolylineLabelOverrule = null;
            }
            Application.DocumentManager.MdiActiveDocument.Editor.Regen();
        }

        private static AlignmentNaMark _AlignmentNaMark;

        //Not working, development suspended
        //[CommandMethod("TOGGLEALNA")]
        public static void togglealnaoverrule()
        {
            if (_AlignmentNaMark == null)
            {
                _AlignmentNaMark = new AlignmentNaMark();
                Overrule.AddOverrule(RXObject.GetClass(typeof(Polyline)), _AlignmentNaMark, false);
                Overrule.Overruling = true;
            }
            else
            {
                Overrule.RemoveOverrule(RXObject.GetClass(typeof(Polyline)), _AlignmentNaMark);
                _AlignmentNaMark.Dispose();
                _AlignmentNaMark = null;
            }
            Application.DocumentManager.MdiActiveDocument.Editor.Regen();
        }

        private static PolylineDirection _polylineDirection;

        [CommandMethod("TOGGLEPOLYDIR")]
        public static void togglepolydiroverrule()
        {
            if (_polylineDirection == null)
            {
                _polylineDirection = new PolylineDirection();
                Overrule.AddOverrule(RXObject.GetClass(typeof(Polyline)), _polylineDirection, false);
                Overrule.Overruling = true;
            }
            else
            {
                Overrule.RemoveOverrule(RXObject.GetClass(typeof(Polyline)), _polylineDirection);
                _polylineDirection.Dispose();
                _polylineDirection = null;
            }
            Application.DocumentManager.MdiActiveDocument.Editor.Regen();
        }

        private static PolylineDirFjv _polylineDirFjv;

        [CommandMethod("TOGGLEFJVDIR")]
        public static void togglefjvdiroverrule()
        {
            if (_polylineDirFjv == null)
            {
                _polylineDirFjv = new PolylineDirFjv();
                Overrule.AddOverrule(RXObject.GetClass(typeof(Polyline)), _polylineDirFjv, false);
                Overrule.Overruling = true;
            }
            else
            {
                Overrule.RemoveOverrule(RXObject.GetClass(typeof(Polyline)), _polylineDirFjv);
                _polylineDirFjv.Dispose();
                _polylineDirFjv = null;
            }
            Application.DocumentManager.MdiActiveDocument.Editor.Regen();
        }

        private static GripVectorOverrule _gripVectorOverrule;

        [CommandMethod("TOGGLEGRIPOR")]
        public static void togglegripoverrule()
        {
            if (_gripVectorOverrule == null)
            {
                _gripVectorOverrule = new GripVectorOverrule();
                Overrule.AddOverrule(RXObject.GetClass(typeof(Polyline)), _gripVectorOverrule, false);
                Overrule.Overruling = true;
            }
            else
            {
                Overrule.RemoveOverrule(RXObject.GetClass(typeof(Polyline)), _gripVectorOverrule);
                _gripVectorOverrule.Dispose();
                _gripVectorOverrule = null;
            }
            Application.DocumentManager.MdiActiveDocument.Editor.Regen();
        }
    }
}
