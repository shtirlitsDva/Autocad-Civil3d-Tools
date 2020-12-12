using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
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
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;
using Autodesk.Gis.Map.Constants;
using Autodesk.Gis.Map.Utilities;

using oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.Utils;
using static IntersectUtilities.Enums;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;

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

                    string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\CivilNET\\LayerNames.txt";

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

        [CommandMethod("listintlaycheck")]
        public void listintlaycheck()
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

                    #region Open XREF and tx
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
                    #endregion

                    #region Gather Xref layer names
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
                    //StringBuilder sb = new StringBuilder();
                    //foreach (string name in layNames) sb.AppendLine(name); 
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

                    foreach (string name in layNames)
                    {
                        string nameInFile = ReadStringParameterFromDataTable(name, dtKrydsninger, "Navn", 0);
                        if (nameInFile.IsNOE())
                        {
                            editor.WriteMessage($"\nDefinition af ledningslag '{name}' mangler i Krydsninger.csv!");
                        }
                        else
                        {
                            string typeInFile = ReadStringParameterFromDataTable(name, dtKrydsninger, "Type", 0);

                            if (typeInFile == "IGNORE")
                            {
                                editor.WriteMessage($"\nAdvarsel: Ledningslag" +
                                        $" '{name}' er sat til 'IGNORE' og dermed ignoreres.");
                            }
                            else
                            {
                                string layerInFile = ReadStringParameterFromDataTable(name, dtKrydsninger, "Layer", 0);
                                if (layerInFile.IsNOE())
                                    editor.WriteMessage($"\nFejl: Definition af kolonne \"Layer\" for ledningslag" +
                                        $" '{name}' mangler i Krydsninger.csv!");

                                if (typeInFile.IsNOE())
                                    editor.WriteMessage($"\nFejl: Definition af kolonne \"Type\" for ledningslag" +
                                        $" '{name}' mangler i Krydsninger.csv!");
                            }
                        }
                    }

                    //string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                    //    + "\\CivilNET\\LayerNames.txt";

                    //Utils.ClrFile(path);
                    //Utils.OutputWriter(path, sb.ToString());

                    #region Read Krydsninger data

                    #endregion
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

                    #endregion

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

                    #region Intersect
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
                    #endregion
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
                }
                else
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

        [CommandMethod("createcrossings")]
        public void longitudinalprofilecrossings()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                tx.TransactionManager.QueueForGraphicsFlush();

                try
                {
                    #region Select and open XREF
                    PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions("\n Select a LER XREF : ");
                    promptEntityOptions1.SetRejectMessage("\n Not a XREF");
                    promptEntityOptions1.AddAllowedClass(typeof(Autodesk.AutoCAD.DatabaseServices.BlockReference), true);
                    PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    Autodesk.AutoCAD.DatabaseServices.ObjectId blkObjId = entity1.ObjectId;
                    Autodesk.AutoCAD.DatabaseServices.BlockReference blkRef
                        = tx.GetObject(blkObjId, OpenMode.ForRead, false)
                        as Autodesk.AutoCAD.DatabaseServices.BlockReference;

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

                    #endregion

                    #region ModelSpaces
                    oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(xRefDB);
                    oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);
                    #endregion

                    #region Load linework from Xref
                    //List<Line> lines = xRefDB.ListOfType<Line>(xrefTx);
                    //editor.WriteMessage($"\nNr. of lines: {lines.Count}");
                    //List<Polyline> plines = xRefDB.ListOfType<Polyline>(xrefTx);
                    //editor.WriteMessage($"\nNr. of plines: {plines.Count}");
                    List<Polyline3d> allLinework = xRefDB.ListOfType<Polyline3d>(xrefTx);
                    editor.WriteMessage($"\nNr. of 3D polies: {allLinework.Count}");
                    //List<Spline> splines = xRefDB.ListOfType<Spline>(xrefTx);
                    //editor.WriteMessage($"\nNr. of splines: {splines.Count}");
                    #endregion

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

                    #region Clone remote objects to local dwg

                    BlockTable acBlkTbl = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord acBlkTblRec =
                        tx.GetObject(acBlkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;


                    Plane plane = new Plane();

                    int intersections = 0;

                    ObjectIdCollection sourceIds = new ObjectIdCollection();
                    List<Entity> sourceEnts = new List<Entity>();

                    //Gather the intersected objectIds
                    foreach (Entity ent in allLinework)
                    {
                        using (Point3dCollection p3dcol = new Point3dCollection())
                        {
                            alignment.IntersectWith(ent, 0, plane, p3dcol, new IntPtr(0), new IntPtr(0));
                            string type = ReadStringParameterFromDataTable(ent.Layer, dtKrydsninger, "Type", 0);
                            if (type.IsNOE())
                            {
                                editor.WriteMessage($"\nFejl: For xref lag {ent.Layer} mangler der enten" +
                                    $"selve definitionen eller 'Type'!");
                                return;
                            }
                            //Create feature line if there's an intersection and
                            //if the type of the layer is not "IGNORE"
                            if (p3dcol.Count > 0 && type != "IGNORE")
                            {
                                intersections++;
                                sourceIds.Add(ent.ObjectId);
                                sourceEnts.Add(ent);
                            }
                        }
                    }

                    //Deepclone the objects
                    IdMapping mapping = new IdMapping();
                    xRefDB.WblockCloneObjects(
                        sourceIds, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);

                    editor.WriteMessage($"\nTotal {intersections} intersections detected.");
                    #endregion

                    #region Load linework from local db
                    //List<Line> localLines = localDb.ListOfType<Line>(tx);
                    //editor.WriteMessage($"\nNr. of local lines: {localLines.Count}. Should be 0.");
                    //All polylines are converted in the source drawing to poly3d
                    //So this should be empty
                    //List<Polyline> localPlines = localDb.ListOfType<Polyline>(tx);
                    //editor.WriteMessage($"\nNr. of local plines: {localPlines.Count}. Should be 0.");
                    List<Polyline3d> localPlines3d = localDb.ListOfType<Polyline3d>(tx);
                    editor.WriteMessage($"\nNr. of local 3D polies: {localPlines3d.Count}");

                    //Splines cannot be used to create Feature Lines
                    //They are converted to polylines, so no splines must be added
                    //List<Spline> localSplines = localDb.ListOfType<Spline>(tx);
                    //editor.WriteMessage($"\nNr. of local splines: {localSplines.Count}. Should be 0.");
                    //if (localSplines.Count > 0)
                    //{
                    //    editor.WriteMessage($"\nFejl: {localSplines.Count} splines detected!");
                    //    return;
                    //}
                    //if (localLines.Count > 0)
                    //{
                    //    editor.WriteMessage($"\nFejl: {localLines.Count} lines detected!");
                    //    return;
                    //}
                    //if (localPlines.Count > 0)
                    //{
                    //    editor.WriteMessage($"\nFejl: {localPlines.Count} polylines detected!");
                    //    return;
                    //}

                    List<Entity> allLocalLinework = new List<Entity>(
                        //localLines.Count +
                        //localPlines.Count +
                        localPlines3d.Count
                        );

                    //allLocalLinework.AddRange(localLines.Cast<Entity>());
                    //allLocalLinework.AddRange(localPlines.Cast<Entity>());
                    allLocalLinework.AddRange(localPlines3d.Cast<Entity>());
                    #endregion

                    #region Try creating FeatureLines
                    Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;
                    int flCounter = 1;
                    foreach (Entity localEntity in allLocalLinework)
                    {
                        using (Transaction tx2 = tx.TransactionManager.StartTransaction())
                        {
                            editor.WriteMessage($"\nProcessing entity handle: {localEntity.Handle}.");

                            tx2.TransactionManager.QueueForGraphicsFlush();

                            //if ((Enumerable.Range(20, 40).ToArray()).Contains(flCounter))
                            //{
                            MapValue handleValue = ReadRecordData(
                                tables, localEntity.ObjectId, "IdRecord", "Handle");

                            string flName = "";

                            if (handleValue != null) flName = handleValue.StrValue;
                            else flName = "Reading of Handle failed.";

                            oid flOid = FeatureLine.Create(flName, localEntity.ObjectId);

                            editor.WriteMessage($"\nCreate nr. {flCounter} returned {flOid.ToString()}");

                            doc.TransactionManager.EnableGraphicsFlush(true);
                            doc.TransactionManager.QueueForGraphicsFlush();
                            Autodesk.AutoCAD.Internal.Utils.FlushGraphics();

                            //Modify the feature lines not assigned type '3D' to drape on surface
                            FeatureLine fl = flOid.Go<FeatureLine>(tx);
                            string type = ReadStringParameterFromDataTable(fl.Layer, dtKrydsninger, "Type", 0);
                            if (type.IsNOE())
                            {
                                editor.WriteMessage($"\nFejl: For lag {fl.Layer} mangler der enten" +
                                    $"selve definitionen eller 'Type'!");
                                return;
                            }

                            if (flOid.ToString() != "(0)" && type != "3D")
                            {
                                fl.UpgradeOpen();
                                fl.Layer = localEntity.Layer;
                                fl.AssignElevationsFromSurface(surfaceObjId, true);
                                fl.
                            }

                            localEntity.UpgradeOpen();
                            localEntity.Erase(true);

                            flCounter++;
                            tx2.Commit();
                        }
                        //}
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                }
                tx.Commit();
            }
        }

        [CommandMethod("createids")]
        public void createids()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Load linework
                    List<Line> lines = localDb.ListOfType<Line>(tx);
                    editor.WriteMessage($"\nNr. of lines: {lines.Count}");
                    List<Polyline> plines = localDb.ListOfType<Polyline>(tx);
                    editor.WriteMessage($"\nNr. of plines: {plines.Count}");
                    List<Polyline3d> plines3d = localDb.ListOfType<Polyline3d>(tx);
                    editor.WriteMessage($"\nNr. of 3D polies: {plines3d.Count}");
                    List<Spline> splines = localDb.ListOfType<Spline>(tx);
                    editor.WriteMessage($"\nNr. of splines: {splines.Count}");

                    List<Entity> allLinework = new List<Entity>(
                        lines.Count + plines.Count + plines3d.Count + splines.Count);

                    allLinework.AddRange(lines.Cast<Entity>());
                    allLinework.AddRange(plines.Cast<Entity>());
                    allLinework.AddRange(plines3d.Cast<Entity>());
                    allLinework.AddRange(splines.Cast<Entity>());
                    #endregion

                    #region Try creating records

                    string m_tableName = "IdRecord";
                    string columnName = "Handle";

                    Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

                    if (DoesTableExist(tables, m_tableName))
                    {
                        editor.WriteMessage("\nTable already exists!");
                    }
                    else
                    {
                        if (CreateTable(
                            tables, m_tableName, "Object handle", columnName, "Handle to string",
                            Autodesk.Gis.Map.Constants.DataType.Character))
                        {
                            editor.WriteMessage($"\nCreated table {m_tableName}.");
                        }
                        else
                        {
                            editor.WriteMessage("\nFailed to create the ObjectData table.");
                            return;
                        }
                    }
                    int successCounter = 0;
                    int failureCounter = 0;
                    foreach (Entity ent in allLinework)
                    {
                        string value = ent.Handle.ToString().Replace("(", "").Replace(")", "");

                        if (DoesRecordExist(tables, ent.ObjectId, "Id"))
                        {
                            UpdateODRecord(tables, m_tableName, columnName, ent.ObjectId, value);
                        }
                        else if (AddODRecord(tables, m_tableName, ent.ObjectId, value))
                        {
                            successCounter++;
                        }
                        else failureCounter++;
                    }

                    editor.WriteMessage($"\nId record created successfully for {successCounter} entities.");
                    editor.WriteMessage($"\nId record creation failed for {failureCounter} entities.");

                    #endregion
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                }
                tx.Commit();
            }
        }

        [CommandMethod("addsize")]
        public void addsize()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            bool cont = true;
            while (cont)
            {
                cont = AddSize(localDb, editor);
            }
        }

        private static bool AddSize(Database localDb, Editor editor, Entity entity = null)
        {
            Entity Entity = entity;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    if (Entity == null)
                    {
                        #region Select Line
                        PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                            "\nSelect polyline3d to add a size:");
                        promptEntityOptions1.SetRejectMessage("\n Not a polyline3d!");
                        promptEntityOptions1.AddAllowedClass(typeof(Polyline3d), true);
                        PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                        if (((PromptResult)entity1).Status != PromptStatus.OK) return false;
                        Autodesk.AutoCAD.DatabaseServices.ObjectId alObjId = entity1.ObjectId;
                        Entity = tx.GetObject(alObjId, OpenMode.ForRead, false) as Entity;
                        #endregion
                    }

                    PromptIntegerResult result = editor.GetInteger("\nEnter pipe size (whole numbers):");
                    if (((PromptResult)result).Status != PromptStatus.OK) return false;

                    int size = result.Value;

                    #region Try creating records

                    string m_tableName = "SizeTable";
                    string columnName = "Size";

                    Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

                    if (DoesTableExist(tables, m_tableName))
                    {
                        editor.WriteMessage("\nTable already exists!");
                    }
                    else
                    {
                        if (CreateTable(
                            tables, m_tableName, "Pipe size table", columnName, "Size of pipe",
                            Autodesk.Gis.Map.Constants.DataType.Integer))
                        {
                            editor.WriteMessage($"\nCreated table {m_tableName}.");
                        }
                        else
                        {
                            editor.WriteMessage("\nFailed to create the ObjectData table.");
                            return false;
                        }
                    }

                    if (DoesRecordExist(tables, Entity.ObjectId, columnName))
                    {
                        UpdateODRecord(tables, m_tableName, columnName, Entity.ObjectId, size);
                    }
                    else if (AddODRecord(tables, m_tableName, Entity.ObjectId, size))
                    {
                        editor.WriteMessage("\nSize added!");
                    }
                    else 
                    {
                        editor.WriteMessage("\nAdding size failed!");
                        return false;
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                }
                tx.Commit();
                return true;
            }
        }

        [CommandMethod("destroyids")]
        public void destroyids()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Try destroying records table

                    string m_tableName = "IdRecord";
                    string columnName = "Handle";

                    Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

                    if (DoesTableExist(tables, m_tableName))
                    {
                        if (RemoveTable(tables, m_tableName))
                        {
                            editor.WriteMessage($"\nTable {m_tableName} removed!");
                        }
                        else editor.WriteMessage($"\nRemoval of table {m_tableName} failed!");
                    }
                    else
                    {
                        editor.WriteMessage($"\nTable {m_tableName} does not exist!");
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                }
                tx.Commit();
            }
        }

        [CommandMethod("convertlinework")]
        public void convertlinework()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    // Open the Block table for read
                    BlockTable acBlkTbl = tx.GetObject(localDb.BlockTableId,
                                                       OpenMode.ForRead) as BlockTable;
                    // Open the Block table record Model space for write
                    BlockTableRecord acBlkTblRec = tx.GetObject(acBlkTbl[BlockTableRecord.ModelSpace],
                                                          OpenMode.ForWrite) as BlockTableRecord;

                    #region Load linework and convert splines
                    List<Spline> splines = localDb.ListOfType<Spline>(tx);
                    editor.WriteMessage($"\nNr. of splines: {splines.Count}");

                    foreach (Spline spline in splines)
                    {
                        Curve curve = spline.ToPolylineWithPrecision(10);

                        curve.Layer = spline.Layer;
                        acBlkTblRec.AppendEntity(curve);
                        tx.AddNewlyCreatedDBObject(curve, true);
                    }
                    #endregion

                    List<Polyline> polies = localDb.ListOfType<Polyline>(tx);
                    editor.WriteMessage($"\nNr. of polylines: {polies.Count}");

                    foreach (Polyline pline in polies)
                    {
                        pline.PolyClean_RemoveDuplicatedVertex();

                        Point3dCollection p3dcol = new Point3dCollection();
                        int vn = pline.NumberOfVertices;

                        for (int i = 0; i < vn; i++) p3dcol.Add(pline.GetPoint3dAt(i));

                        Polyline3d polyline3D = new Polyline3d(Poly3dType.SimplePoly, p3dcol, false);
                        polyline3D.Layer = pline.Layer;
                        acBlkTblRec.AppendEntity(polyline3D);
                        tx.AddNewlyCreatedDBObject(polyline3D, true);
                    }

                    List<Line> lines = localDb.ListOfType<Line>(tx);
                    editor.WriteMessage($"\nNr. of lines: {lines.Count}");

                    foreach (Line line in lines)
                    {
                        Point3dCollection p3dcol = new Point3dCollection();

                        p3dcol.Add(line.StartPoint);
                        p3dcol.Add(line.EndPoint);

                        Polyline3d polyline3D = new Polyline3d(Poly3dType.SimplePoly, p3dcol, false);
                        polyline3D.Layer = line.Layer;
                        acBlkTblRec.AppendEntity(polyline3D);
                        tx.AddNewlyCreatedDBObject(polyline3D, true);
                    }

                    foreach (Line line in lines)
                    {
                        line.UpgradeOpen();
                        line.Erase(true);
                    }

                    foreach (Spline spline in splines)
                    {
                        spline.UpgradeOpen();
                        spline.Erase(true);
                    }

                    foreach (Polyline pl in polies)
                    {
                        pl.UpgradeOpen();
                        pl.Erase(true);
                    }
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                }
                tx.Commit();
            }
        }

        [CommandMethod("selectbyhandle")]
        public void selectbyhandle()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    PromptResult pr = editor.GetString("\nEnter handle of object to select: ");

                    if (pr.Status == PromptStatus.OK)
                    {
                        // Convert hexadecimal string to 64-bit integer
                        long ln = Convert.ToInt64(pr.StringResult, 16);
                        // Now create a Handle from the long integer
                        Handle hn = new Handle(ln);
                        // And attempt to get an ObjectId for the Handle
                        oid id = localDb.GetObjectId(false, hn, 0);
                        // Finally let's open the object and erase it
                        editor.SetImpliedSelection(new[] { id });

                    }
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                }
                tx.Commit();
            }
        }

        [CommandMethod("isolatecrossings")]
        public void isolatecrossings()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Load linework from local db
                    List<Line> localLines = localDb.ListOfType<Line>(tx);
                    editor.WriteMessage($"\nNr. of local lines: {localLines.Count}");
                    List<Polyline3d> localPlines3d = localDb.ListOfType<Polyline3d>(tx);
                    editor.WriteMessage($"\nNr. of local 3D polies: {localPlines3d.Count}");

                    //Splines cannot be used to create Feature Lines
                    //They are converted to polylines, so no splines must be added
                    List<Spline> localSplines = localDb.ListOfType<Spline>(tx);
                    editor.WriteMessage($"\nNr. of local splines: {localSplines.Count}");
                    if (localSplines.Count > 0)
                    {
                        editor.WriteMessage($"\n{localSplines.Count} splines detected! Run 'convertlinework'.");
                        return;
                    }
                    //All polylines are converted in the source drawing to poly3d
                    //So this should be empty
                    List<Polyline> localPlines = localDb.ListOfType<Polyline>(tx);
                    editor.WriteMessage($"\nNr. of local plines: {localPlines.Count}");
                    if (localPlines.Count > 0)
                    {
                        editor.WriteMessage($"\n{localPlines.Count} polylines detected! Run 'convertlinework'.");
                        return;
                    }

                    List<Entity> allLocalLinework = new List<Entity>(
                        localLines.Count +
                        localPlines3d.Count
                        );

                    allLocalLinework.AddRange(localLines.Cast<Entity>());
                    allLocalLinework.AddRange(localPlines3d.Cast<Entity>());
                    #endregion

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

                    Plane plane = new Plane();

                    List<oid> sourceIds = new List<oid>();

                    //Gather the intersected objectIds
                    foreach (Entity ent in allLocalLinework)
                    {
                        using (Point3dCollection p3dcol = new Point3dCollection())
                        {
                            alignment.IntersectWith(ent, 0, plane, p3dcol, new IntPtr(0), new IntPtr(0));

                            if (p3dcol.Count > 0) sourceIds.Add(ent.ObjectId);
                        }
                    }

                    //Additional object classes to keep showing
                    List<DBPoint> points = localDb.ListOfType<DBPoint>(tx)
                                                  .Where(x => x.Position.Z > 0.1)
                                                  .ToList();
                    List<DBText> text = localDb.ListOfType<DBText>(tx);
                    //Add additional objects to isolation
                    foreach (DBPoint item in points) sourceIds.Add(item.ObjectId);
                    foreach (DBText item in text) sourceIds.Add(item.ObjectId);

                    sourceIds.Add(alignment.ObjectId);

                    editor.SetImpliedSelection(sourceIds.ToArray());
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                }
                tx.Commit();
            }
        }

        [CommandMethod("movep3dverticestopoints")]
        public void movep3dverticestopoints()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Load linework from local db
                    HashSet<Line> localLines = localDb.HashSetOfType<Line>(tx);
                    editor.WriteMessage($"\nNr. of local lines: {localLines.Count}");
                    HashSet<Polyline3d> localPlines3d = localDb.HashSetOfType<Polyline3d>(tx);
                    editor.WriteMessage($"\nNr. of local 3D polies: {localPlines3d.Count}");
                    #endregion

                    //Points to intersect
                    HashSet<DBPoint> points = new HashSet<DBPoint>(localDb.ListOfType<DBPoint>(tx)
                                                  .Where(x => x.Position.Z > 0.1),
                                                  new PointDBHorizontalComparer());
                    editor.WriteMessage($"\nNr. of local points: {points.Count}");
                    editor.WriteMessage($"\nTotal number of combinations: " +
                        $"{points.Count * (localLines.Count + localPlines3d.Count)}");

                    foreach (Polyline3d pline3d in localPlines3d)
                    {
                        var vertices = pline3d.GetVertices(tx);

                        for (int i = 0; i < vertices.Length; i++)
                        {
                            DBPoint match = points.Where(x => x.Position.HorizontalEqualz(vertices[i].Position)).FirstOrDefault();
                            if (match != null)
                            {
                                vertices[i].UpgradeOpen();
                                vertices[i].Position = new Point3d(
                                    vertices[i].Position.X, vertices[i].Position.Y, match.Position.Z);
                                vertices[i].DowngradeOpen();
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                }
                tx.Commit();
            }
        }

        [CommandMethod("editelevations")]
        public void editelevations()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            #region Choose manual input or parsing of elevations
            const string kwd1 = "Manual";
            const string kwd2 = "Text";

            PromptKeywordOptions pKeyOpts = new PromptKeywordOptions("");
            pKeyOpts.Message = "\nChoose elevation input method: ";
            pKeyOpts.Keywords.Add(kwd1);
            pKeyOpts.Keywords.Add(kwd2);
            pKeyOpts.AllowNone = true;
            pKeyOpts.Keywords.Default = kwd1;
            PromptResult pKeyRes = editor.GetKeywords(pKeyOpts);

            ElevationInputMethod eim = ElevationInputMethod.None;
            switch (pKeyRes.StringResult)
            {
                case kwd1:
                    eim = ElevationInputMethod.Manual;
                    break;
                case kwd2:
                    eim = ElevationInputMethod.Text;
                    break;
                default:
                    return;
            }
            #endregion

            while (true)
            {
                #region Select pline3d
                PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                    "\nSelect polyline3d to modify:");
                promptEntityOptions1.SetRejectMessage("\n Not a polyline3d!");
                promptEntityOptions1.AddAllowedClass(typeof(Polyline3d), true);
                PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                Autodesk.AutoCAD.DatabaseServices.ObjectId pline3dId = entity1.ObjectId;
                #endregion

                #region Select point
                PromptPointOptions pPtOpts = new PromptPointOptions("");
                // Prompt for the start point
                pPtOpts.Message = "\nEnter location where to modify the pline3d (must be a vertex):";
                PromptPointResult pPtRes = editor.GetPoint(pPtOpts);
                Point3d point = pPtRes.Value;
                // Exit if the user presses ESC or cancels the command
                if (pPtRes.Status != PromptStatus.OK) return;
                #endregion

                #region Get elevation depending on method
                double elevation = 0;
                switch (eim)
                {
                    case ElevationInputMethod.None:
                        return;
                    case ElevationInputMethod.Manual:
                        PromptDoubleResult result = editor.GetDouble("\nEnter elevation in meters:");
                        if (((PromptResult)result).Status != PromptStatus.OK) return;
                        elevation = result.Value;
                        break;
                    case ElevationInputMethod.Text:
                        #region Select and parse text
                        PromptEntityOptions promptEntityOptions2 = new PromptEntityOptions(
                            "\nSelect to parse as elevation:");
                        promptEntityOptions2.SetRejectMessage("\n Not a text!");
                        promptEntityOptions2.AddAllowedClass(typeof(DBText), true);
                        PromptEntityResult entity2 = editor.GetEntity(promptEntityOptions2);
                        if (((PromptResult)entity2).Status != PromptStatus.OK) return;
                        Autodesk.AutoCAD.DatabaseServices.ObjectId DBtextId = entity2.ObjectId;

                        using (Transaction tx2 = localDb.TransactionManager.StartTransaction())
                        {
                            DBText dBText = DBtextId.Go<DBText>(tx2);
                            string readValue = dBText.TextString;

                            double parsedResult;

                            if (double.TryParse(readValue, NumberStyles.AllowDecimalPoint,
                                    CultureInfo.InvariantCulture, out parsedResult))
                            {
                                elevation = parsedResult;
                            }
                            else
                            {
                                editor.WriteMessage("\nParsing of text failed!");
                                return;
                            }
                            tx2.Commit();
                        }
                        break;
                    default:
                        return;
                        #endregion
                }
                #endregion

                #region Modify elevation of pline3d
                using (Transaction tx = localDb.TransactionManager.StartTransaction())
                {
                    try
                    {
                        Polyline3d pline3d = pline3dId.Go<Polyline3d>(tx);

                        var vertices = pline3d.GetVertices(tx);

                        bool matchNotFound = true;
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            if (vertices[i].Position.HorizontalEqualz(point))
                            {
                                vertices[i].UpgradeOpen();
                                vertices[i].Position = new Point3d(
                                    vertices[i].Position.X, vertices[i].Position.Y, elevation);
                                vertices[i].DowngradeOpen();
                                matchNotFound = false;
                            }
                        }

                        if (matchNotFound)
                        {
                            editor.WriteMessage("\nNo match found for vertices! P3d was not modified!");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        editor.WriteMessage("\n" + ex.Message);
                    }
                    tx.Commit();
                }
                #endregion

                #region Choose next action
                const string ckwd1 = "Next pline3d";
                const string ckwd2 = "Add size to current pline3d";

                PromptKeywordOptions pKeyOpts2 = new PromptKeywordOptions("");
                pKeyOpts2.Message = "\nChoose next action: ";
                pKeyOpts2.Keywords.Add(ckwd1);
                pKeyOpts2.Keywords.Add(ckwd2);
                pKeyOpts2.AllowNone = true;
                pKeyOpts2.Keywords.Default = ckwd1;
                PromptResult pKeyRes2 = editor.GetKeywords(pKeyOpts2);
                #endregion

                if (pKeyRes2.StringResult == ckwd1) continue;

                #region Add size
                using (Transaction tx = localDb.TransactionManager.StartTransaction())
                {
                    AddSize(localDb, editor, pline3dId.Go<Polyline3d>(tx));
                    tx.Commit();
                }
                #endregion
            }
        }
    }
}