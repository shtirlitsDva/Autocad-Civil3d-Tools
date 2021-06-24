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
using IntersectUtilities;
using static IntersectUtilities.Enums;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.Utils;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = Autodesk.Civil.DatabaseServices.Label;

namespace ExportShapeFiles
{
    public class ExportShapeFiles : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            //doc.Editor.WriteMessage("\n-> Command: intut");
            //doc.Editor.WriteMessage("\n-> Intersect alignment with XREF: INTAL");
            //doc.Editor.WriteMessage("\n-> Write a list of all XREF layers: LISTINTLAY");
            //doc.Editor.WriteMessage("\n-> Change the elevation of CogoPoint by selecting projection label: CHEL");
        }

        public void Terminate()
        {
        }
        #endregion

        [CommandMethod("EXPORTSHAPEFILES")]
        public void exportshapefiles()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            MapApplication mapApp = HostMapApplicationServices.Application;
            Autodesk.Gis.Map.ImportExport.Exporter exporter = mapApp.Exporter;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    PromptResult pr = editor.GetString("\nEnter filename of shapefile: ");
                    if (pr.Status != PromptStatus.OK)
                        prdDbg("Input of file name string failed!");
                    string fileNameToExport = pr.StringResult;

                    HashSet<Polyline> pls = localDb.HashSetOfType<Polyline>(tx);
                    HashSet<Line> ls = localDb.HashSetOfType<Line>(tx);
                    ObjectIdCollection ids = new ObjectIdCollection();
                    pls.Where(x =>
                        x.Layer.Contains("FJV-TWIN") ||
                        x.Layer.Contains("FJV-FREM") ||
                        x.Layer.Contains("FJV-RETUR"))
                        .Select(x => ids.Add(x.Id));
                    ls.Where(x =>
                        x.Layer.Contains("FJV-TWIN") ||
                        x.Layer.Contains("FJV-FREM") ||
                        x.Layer.Contains("FJV-RETUR"))
                        .Select(x => ids.Add(x.Id));

                    exporter.Init("SHP", fileNameToExport);
                    exporter.SetStorageOptions(
                        Autodesk.Gis.Map.ImportExport.StorageType.FileOneEntityType,
                        Autodesk.Gis.Map.ImportExport.GeometryType.Line, null);
                    exporter.SetSelectionSet(ids);

                    exporter.Export(false);
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Abort();
            }
        }
    }
}
