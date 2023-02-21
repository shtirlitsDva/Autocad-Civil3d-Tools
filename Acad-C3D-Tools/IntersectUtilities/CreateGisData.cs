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
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;
using Autodesk.Gis.Map.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.UtilsCommon.UtilsODData;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using System.Windows.Forms;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = Autodesk.Civil.DatabaseServices.Label;

namespace IntersectUtilities
{
    public static class GisData
    {
        private static void GetAllXrefNames(GraphNode i_root, List<string> list, Transaction i_Tx)
        {
            for (int o = 0; o < i_root.NumOut; o++)
            {
                XrefGraphNode child = i_root.Out(o) as XrefGraphNode;
                if (child.XrefStatus == XrefStatus.Resolved)
                {
                    BlockTableRecord bl = i_Tx.GetObject(child.BlockTableRecordId, OpenMode.ForRead) as BlockTableRecord;
                    list.Add(child.Database.Filename);
                    // Name of the Xref (found name)
                    // You can find the original path too:
                    //if (bl.IsFromExternalReference == true)
                    // i_ed.WriteMessage("\n" + i_indent + "Xref path name: "
                    //                      + bl.PathName);
                    GetAllXrefNames(child, list, i_Tx);
                }
            }
        }
    }
}
