using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Aec.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;

using oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using static IntersectUtilities.HelperMethods;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;

namespace IntersectUtilities
{
    /// <summary>
    /// Class for intersection tools.
    /// </summary>
    public class Intersect : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            //doc.Editor.WriteMessage("\n-> Command: intut");
            doc.Editor.WriteMessage("\n-> Intersect alignment with XREF: INTAL");
            doc.Editor.WriteMessage("\n-> Write a list of all XREF layers: LISTINTLAY");
            doc.Editor.WriteMessage("\n-> Change the elevation of CogoPoint by selecting projection label: CHEL");
        }

        public void Terminate()
        {
        }
        #endregion

        /// <summary>
        /// Finds all intersections between a selected polyline and all lines.
        /// Creates a point object at the intersection.
        /// </summary>

        [CommandMethod("chel")]
        public void changeelevationofprojectedcogopoint()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database db = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Select Label
                    //Get alignment
                    PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                        "\nSelect label of Cogo Point on Profile View:");
                    promptEntityOptions1.SetRejectMessage("\n Not a label");
                    promptEntityOptions1.AddAllowedClass(typeof(ProfileProjectionLabel), true);
                    PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    Autodesk.AutoCAD.DatabaseServices.ObjectId alObjId = entity1.ObjectId;
                    LabelBase label = tx.GetObject(alObjId, OpenMode.ForRead, false) as LabelBase;
                    #endregion

                    oid fId = label.FeatureId;

                    CogoPoint p = tx.GetObject(fId, OpenMode.ForWrite) as CogoPoint;
                    PromptDoubleResult result = editor.GetDouble("\nValue to modify elevation:");
                    if (((PromptResult)result).Status != PromptStatus.OK) return;

                    double distToMove = result.Value;

                    editor.WriteMessage($"\nCogoElevation: {p.Elevation}");

                    editor.WriteMessage($"\nCalculated elevation: {p.Elevation + distToMove}");

                    p.Elevation += distToMove;
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                }
                tx.Commit();
            }
        }

        [CommandMethod("listintlay")]
        public void listintlay()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database db = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Select XREF
                    PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions("\n Select a LER XREF : ");
                    promptEntityOptions1.SetRejectMessage("\n Not a XREF");
                    promptEntityOptions1.AddAllowedClass(typeof(Autodesk.AutoCAD.DatabaseServices.BlockReference), true);
                    PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    Autodesk.AutoCAD.DatabaseServices.ObjectId blkObjId = entity1.ObjectId;
                    Autodesk.AutoCAD.DatabaseServices.BlockReference blkRef
                        = tx.GetObject(blkObjId, OpenMode.ForRead, false)
                        as Autodesk.AutoCAD.DatabaseServices.BlockReference;
                    #endregion

                    // open the block definition?
                    BlockTableRecord blockDef = tx.GetObject(blkRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                    // is not from external reference, exit
                    if (!blockDef.IsFromExternalReference) return;

                    // open the xref database
                    Database xRefDB = new Database(false, true);
                    editor.WriteMessage($"\nPathName of the blockDef -> {blockDef.PathName}");

                    //Relative path handling
                    //I
                    string curPathName = blockDef.PathName;
                    bool isFullPath = IsFullPath(curPathName);
                    if (isFullPath == false)
                    {
                        string sourcePath = Path.GetDirectoryName(doc.Name);
                        editor.WriteMessage($"\nSourcePath -> {sourcePath}");
                        curPathName = GetAbsolutePath(sourcePath, blockDef.PathName);
                        editor.WriteMessage($"\nTargetPath -> {curPathName}");
                    }

                    xRefDB.ReadDwgFile(curPathName, System.IO.FileShare.Read, false, string.Empty);

                    //Transaction from Database of the Xref
                    Transaction xrefTx = xRefDB.TransactionManager.StartTransaction();

                    List<Line> lines = xRefDB.ListOfType<Line>(xrefTx);
                    editor.WriteMessage($"\nNr. of lines: {lines.Count}");
                    List<Polyline> plines = xRefDB.ListOfType<Polyline>(xrefTx);
                    editor.WriteMessage($"\nNr. of plines: {plines.Count}");
                    List<Polyline3d> plines3d = xRefDB.ListOfType<Polyline3d>(xrefTx);
                    editor.WriteMessage($"\nNr. of 3D polies: {plines3d.Count}");
                    List<Spline> splines = xRefDB.ListOfType<Spline>(xrefTx);
                    editor.WriteMessage($"\nNr. of splines: {splines.Count}");

                    List<string> layNames = new List<string>(lines.Count + plines.Count + plines3d.Count + splines.Count);

                    //Local function to avoid duplicate code
                    List<string> LocalListNames<T>(List<string> list, List<T> ents)
                    {
                        foreach (Entity ent in ents.Cast<Entity>())
                        {
                            LayerTableRecord layer = (LayerTableRecord)xrefTx.GetObject(ent.LayerId, OpenMode.ForRead);
                            if (layer.IsFrozen) continue;

                            list.Add(layer.Name);
                        }
                        return list;
                    }

                    layNames = LocalListNames(layNames, lines);
                    layNames = LocalListNames(layNames, plines);
                    layNames = LocalListNames(layNames, plines3d);
                    layNames = LocalListNames(layNames, splines);

                    xrefTx.Dispose();

                    layNames = layNames.Distinct().ToList();
                    StringBuilder sb = new StringBuilder();
                    foreach (string name in layNames) sb.AppendLine(name);

                    string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\LayerNames.txt";

                    Utils.ClrFile(path);
                    Utils.OutputWriter(path, sb.ToString());
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                }
                tx.Commit();
            }
        }

        [CommandMethod("intal")]
        public void intersectalignment()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database db = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Select XREF
                    PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions("\n Select a LER XREF : ");
                    promptEntityOptions1.SetRejectMessage("\n Not a XREF");
                    promptEntityOptions1.AddAllowedClass(typeof(Autodesk.AutoCAD.DatabaseServices.BlockReference), true);
                    PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    Autodesk.AutoCAD.DatabaseServices.ObjectId blkObjId = entity1.ObjectId;
                    Autodesk.AutoCAD.DatabaseServices.BlockReference blkRef
                        = tx.GetObject(blkObjId, OpenMode.ForRead, false)
                        as Autodesk.AutoCAD.DatabaseServices.BlockReference;
                    #endregion

                    // open the block definition?
                    BlockTableRecord blockDef = tx.GetObject(blkRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                    // is not from external reference, exit
                    if (!blockDef.IsFromExternalReference) return;

                    // open the xref database
                    Database xRefDB = new Database(false, true);
                    editor.WriteMessage($"\nPathName of the blockDef -> {blockDef.PathName}");

                    //Relative path handling
                    //I
                    string curPathName = blockDef.PathName;
                    bool isFullPath = IsFullPath(curPathName);
                    if (isFullPath == false)
                    {
                        string sourcePath = Path.GetDirectoryName(doc.Name);
                        editor.WriteMessage($"\nSourcePath -> {sourcePath}");
                        curPathName = GetAbsolutePath(sourcePath, blockDef.PathName);
                        editor.WriteMessage($"\nTargetPath -> {curPathName}");
                    }

                    xRefDB.ReadDwgFile(curPathName, System.IO.FileShare.Read, false, string.Empty);

                    //Transaction from Database of the Xref
                    Transaction xrefTx = xRefDB.TransactionManager.StartTransaction();

                    List<Line> lines = xRefDB.ListOfType<Line>(xrefTx);
                    editor.WriteMessage($"\nNr. of lines: {lines.Count}");
                    List<Polyline> plines = xRefDB.ListOfType<Polyline>(xrefTx);
                    editor.WriteMessage($"\nNr. of plines: {plines.Count}");
                    List<Polyline3d> plines3d = xRefDB.ListOfType<Polyline3d>(xrefTx);
                    editor.WriteMessage($"\nNr. of 3D polies: {plines3d.Count}");
                    List<Spline> splines = xRefDB.ListOfType<Spline>(xrefTx);
                    editor.WriteMessage($"\nNr. of splines: {splines.Count}");

                    #region Select Alignment
                    //Get alignment
                    PromptEntityOptions promptEntityOptions2 = new PromptEntityOptions("\n Select alignment to intersect: ");
                    promptEntityOptions2.SetRejectMessage("\n Not an alignment");
                    promptEntityOptions2.AddAllowedClass(typeof(Alignment), true);
                    PromptEntityResult entity2 = editor.GetEntity(promptEntityOptions2);
                    if (((PromptResult)entity2).Status != PromptStatus.OK) return;
                    Autodesk.AutoCAD.DatabaseServices.ObjectId alObjId = entity2.ObjectId;
                    Alignment alignment = tx.GetObject(alObjId, OpenMode.ForRead, false) as Alignment;
                    #endregion

                    #region Select surface
                    //Get surface
                    PromptEntityOptions promptEntityOptions3 = new PromptEntityOptions("\n Select surface to place points: ");
                    promptEntityOptions3.SetRejectMessage("\n Not a surface");
                    promptEntityOptions3.AddAllowedClass(typeof(TinSurface), true);
                    promptEntityOptions3.AddAllowedClass(typeof(GridSurface), true);
                    PromptEntityResult entity3 = editor.GetEntity(promptEntityOptions3);
                    if (((PromptResult)entity3).Status != PromptStatus.OK) return;
                    Autodesk.AutoCAD.DatabaseServices.ObjectId surfaceObjId = entity3.ObjectId;
                    CivSurface surface = surfaceObjId.GetObject(OpenMode.ForRead, false) as CivSurface;
                    #endregion

                    #region Read Csv Data for Layers and Depth

                    //Establish the pathnames to files
                    //Files should be placed in a specific folder on desktop
                    string pathKrydsninger = Environment.GetFolderPath(
                        Environment.SpecialFolder.Desktop) + "\\CivilNET\\Krydsninger.csv";
                    string pathDybde = Environment.GetFolderPath(
                        Environment.SpecialFolder.Desktop) + "\\CivilNET\\Dybde.csv";

                    System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
                    System.Data.DataTable dtDybde = CsvReader.ReadCsvToDataTable(pathDybde, "Dybde");

                    #endregion

                    //Create a plane to project all intersections on
                    //Needed to avoid missing objects with non zero Z values
                    Plane plane = new Plane();

                    //Access CivilDocument cogopoints manager
                    CogoPointCollection cogoPoints = civilDoc.CogoPoints;

                    //List to hold the names not in the Krydsninger
                    List<string> layerNamesNotPresent = new List<string>();

                    int lineCnt = IntersectEntities(tx, db, xrefTx, lines, alignment, plane,
                                                    cogoPoints, surface, dtKrydsninger,
                                                    dtDybde, layerNamesNotPresent);

                    int plineCnt = IntersectEntities(tx, db, xrefTx, plines, alignment, plane,
                                                    cogoPoints, surface, dtKrydsninger,
                                                    dtDybde, layerNamesNotPresent);

                    int pline3dCnt = IntersectEntities(tx, db, xrefTx, plines3d, alignment, plane,
                                                    cogoPoints, surface, dtKrydsninger,
                                                    dtDybde, layerNamesNotPresent);

                    int splineCnt = IntersectEntities(tx, db, xrefTx, splines, alignment, plane,
                                                    cogoPoints, surface, dtKrydsninger,
                                                    dtDybde, layerNamesNotPresent);

                    layerNamesNotPresent = layerNamesNotPresent.Distinct().ToList();
                    editor.WriteMessage("\nFollowing layers were NOT present in Krydsninger.csv:");
                    foreach (string name in layerNamesNotPresent)
                    {
                        editor.WriteMessage(name);
                    }

                    editor.WriteMessage($"\nTotal number of points created: {lineCnt + plineCnt + pline3dCnt + splineCnt}" +
                        $"\n{lineCnt} Line(s), {plineCnt} Polyline(s), {pline3dCnt} 3D polyline(s), {splineCnt} Spline(s)");

                    xrefTx.Dispose();
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                }
                tx.Commit();
            }
        }

        private static int IntersectEntities<T>(
            Transaction tx,
            Database db,
            Transaction xrefTx,
            List<T> entitiesToInt,
            Alignment alignment,
            Plane plane,
            CogoPointCollection cogoPoints,
            CivSurface surface,
            System.Data.DataTable krydsninger,
            System.Data.DataTable dybde,
            List<string> layerNamesNotPresent)
        {
            int count = 0;

            foreach (Entity ent in entitiesToInt.Cast<Entity>())
            {

                LayerTableRecord xrefLayer = (LayerTableRecord)xrefTx.GetObject(ent.LayerId, OpenMode.ForRead);
                if (xrefLayer.IsFrozen) continue;

                #region Layer name
                string localLayerName = Utils.ReadStringParameterFromDataTable(
                            xrefLayer.Name, krydsninger, "Layer", 0);

                bool localLayerExists = false;

                if (!localLayerName.IsNOE() || localLayerName != null)
                {
                    LayerTable lt = tx.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                    if (lt.Has(localLayerName))
                    {
                        localLayerExists = true;
                    }
                    else
                    {
                        //Create layer if it doesn't exist
                        try
                        {
                            //Validate the name of layer
                            //It throws an exception if not, so need to catch it
                            SymbolUtilityServices.ValidateSymbolName(localLayerName, false);

                            LayerTableRecord ltr = new LayerTableRecord();
                            ltr.Name = localLayerName;

                            //Make layertable writable
                            lt.UpgradeOpen();

                            //Add the new layer to layer table
                            oid ltId = lt.Add(ltr);
                            tx.AddNewlyCreatedDBObject(ltr, true);

                            //Flag that the layer exists now
                            localLayerExists = true;

                        }
                        catch (System.Exception)
                        {
                            //Eat the exception and continue
                            //localLayerExists must remain false
                        }
                    }
                } else
                {
                    layerNamesNotPresent.Add(xrefLayer.Name);
                }

                #endregion

                #region Type and depth

                string type = Utils.ReadStringParameterFromDataTable(
                            xrefLayer.Name, krydsninger, "Type", 0);

                bool typeExists = false;
                double depth = 0;

                if (!type.IsNOE() || type != null)
                {
                    typeExists = true;
                    depth = Utils.ReadDoubleParameterFromDataTable(type, dybde, "Dybde", 0);
                }

                #endregion

                using (Point3dCollection p3dcol = new Point3dCollection())
                {
                    alignment.IntersectWith(ent, 0, plane, p3dcol, new IntPtr(0), new IntPtr(0));

                    foreach (Point3d p3d in p3dcol)
                    {
                        oid pointId = cogoPoints.Add(p3d, true);
                        CogoPoint cogoPoint = pointId.GetObject(OpenMode.ForWrite) as CogoPoint;
                        //var layer = xrefTx.GetObject(line.LayerId, OpenMode.ForRead) as SymbolTableRecord;

                        //Set the layer
                        if (localLayerExists) cogoPoint.Layer = localLayerName;

                        var intPoint = surface.GetIntersectionPoint(p3d, new Vector3d(0, 0, 1));
                        double zElevation = intPoint.Z;

                        //Subtract the depth if non-zero
                        if (depth != 0) zElevation -= depth;

                        cogoPoint.Elevation = zElevation;

                        cogoPoint.PointName = xrefLayer.Name + " " + count;
                        cogoPoint.RawDescription = "Udfyld RAW DESCRIPTION";

                        count++;
                    }
                }
            }

            return count;
        }
    }
}
