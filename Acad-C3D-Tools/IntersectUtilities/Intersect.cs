using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.Text.RegularExpressions;
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
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;

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
                    return;
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

                    layNames = layNames.Distinct().OrderBy(x => x).ToList();
                    StringBuilder sb = new StringBuilder();
                    foreach (string name in layNames) sb.AppendLine(name);

                    string path = "X:\\AutoCAD DRI - 01 Civil 3D\\LayerNames.txt";

                    Utils.ClrFile(path);
                    Utils.OutputWriter(path, sb.ToString());
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("listintlaycheckall")]
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
                    //editor.WriteMessage($"\nNr. of plines: {plines.Count}");
                    List<Polyline3d> plines3d = xRefDB.ListOfType<Polyline3d>(xrefTx);
                    editor.WriteMessage($"\nNr. of 3D polies: {plines3d.Count}");

                    List<string> layNames = new List<string>(plines3d.Count);

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

                    layNames = LocalListNames(layNames, plines3d);

                    xrefTx.Dispose();

                    layNames = layNames.Distinct().ToList();
                    //StringBuilder sb = new StringBuilder();
                    //foreach (string name in layNames) sb.AppendLine(name); 
                    #endregion

                    #region Read Csv Data for Layers and Depth

                    //Establish the pathnames to files
                    //Files should be placed in a specific folder on desktop
                    string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";
                    string pathDybde = "X:\\AutoCAD DRI - 01 Civil 3D\\Dybde.csv";

                    System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
                    System.Data.DataTable dtDybde = CsvReader.ReadCsvToDataTable(pathDybde, "Dybde");

                    #endregion

                    foreach (string name in layNames)
                    {
                        string nameInFile = ReadStringParameterFromDataTable(name, dtKrydsninger, "Navn", 0);
                        if (nameInFile.IsNoE())
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
                                if (layerInFile.IsNoE())
                                    editor.WriteMessage($"\nFejl: Definition af kolonne \"Layer\" for ledningslag" +
                                        $" '{name}' mangler i Krydsninger.csv!");

                                if (typeInFile.IsNoE())
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
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("listintlaycheckalignment")]
        public void listintlaycheckalignment()
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
                    //editor.WriteMessage($"\nNr. of plines: {plines.Count}");
                    List<Polyline3d> plines3d = xRefDB.ListOfType<Polyline3d>(xrefTx);
                    editor.WriteMessage($"\nNr. of 3D polies: {plines3d.Count}");

                    #region Select Alignment
                    //Get alignment
                    PromptEntityOptions promptEntityOptions2 = new PromptEntityOptions("\n Select alignment to check: ");
                    promptEntityOptions2.SetRejectMessage("\n Not an alignment");
                    promptEntityOptions2.AddAllowedClass(typeof(Alignment), true);
                    PromptEntityResult entity2 = editor.GetEntity(promptEntityOptions2);
                    if (((PromptResult)entity2).Status != PromptStatus.OK) return;
                    Autodesk.AutoCAD.DatabaseServices.ObjectId alObjId = entity2.ObjectId;
                    Alignment alignment = tx.GetObject(alObjId, OpenMode.ForRead, false) as Alignment;
                    #endregion
                    //int counter = 1;
                    //editor.WriteMessage($"Linemarking {counter}."); counter++;
                    plines3d = FilterForCrossingEntities(plines3d, alignment);

                    List<string> layNames = new List<string>(plines3d.Count);

                    //Local function to avoid duplicate code
                    List<string> LocalListNames<T>(List<string> list, List<T> ents)
                    {
                        foreach (Entity ent in ents.Cast<Entity>())
                        {
                            LayerTableRecord layer = (LayerTableRecord)xrefTx.GetObject(ent.LayerId, OpenMode.ForRead);
                            //if (layer.IsFrozen) continue;

                            list.Add(layer.Name);
                        }
                        return list;
                    }

                    layNames = LocalListNames(layNames, plines3d);

                    xrefTx.Dispose();

                    layNames = layNames.Distinct().ToList();
                    //StringBuilder sb = new StringBuilder();
                    //foreach (string name in layNames) sb.AppendLine(name); 
                    #endregion

                    #region Read Csv Data for Layers and Depth

                    //Establish the pathnames to files
                    //Files should be placed in a specific folder on desktop
                    string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";
                    string pathDybde = "X:\\AutoCAD DRI - 01 Civil 3D\\Dybde.csv";

                    if (!File.Exists(pathKrydsninger) || !File.Exists(pathDybde))
                    {
                        editor.WriteMessage("\nCSV input files cannot be reached!");
                        return;
                    }

                    System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
                    System.Data.DataTable dtDybde = CsvReader.ReadCsvToDataTable(pathDybde, "Dybde");

                    #endregion

                    foreach (string name in layNames)
                    {
                        string nameInFile = ReadStringParameterFromDataTable(name, dtKrydsninger, "Navn", 0);
                        if (nameInFile.IsNoE())
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
                                editor.WriteMessage($"\nKontrollerer lag {name}:");

                                string layerInFile = ReadStringParameterFromDataTable(name, dtKrydsninger, "Layer", 0);
                                if (layerInFile.IsNoE())
                                    editor.WriteMessage($"\nFejl: Definition af kolonne \"Layer\" for ledningslag" +
                                        $" '{name}' mangler i Krydsninger.csv!");

                                if (typeInFile.IsNoE())
                                    editor.WriteMessage($"\nFejl: Definition af kolonne \"Type\" for ledningslag" +
                                        $" '{name}' mangler i Krydsninger.csv!");

                                string blockInFile = ReadStringParameterFromDataTable(name, dtKrydsninger, "Block", 0);
                                if (blockInFile.IsNoE())
                                    editor.WriteMessage($"\nAdvarsel: Definition af kolonne \"Block\" for ledningslag" +
                                        $" '{name}' mangler i Krydsninger.csv! Intet figur vil blive tegnet ved detaljering!");

                                string descrInFile = ReadStringParameterFromDataTable(name, dtKrydsninger, "Description", 0);
                                if (descrInFile.IsNoE())
                                    editor.WriteMessage($"\nAdvarsel: Definition af kolonne \"Description\" for ledningslag" +
                                        $" '{name}' mangler i Krydsninger.csv! Intet beskrivelse vil blive skrevet i labels!");
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("listintlaycheckalignments")]
        public void listintlaycheckalignments()
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
                    //Transaction xrefTx = xRefDB.TransactionManager.StartTransaction();
                    #endregion

                    #region Read Csv Data for Layers and Depth

                    //Establish the pathnames to files
                    //Files should be placed in a specific folder on desktop
                    string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";
                    string pathDybde = "X:\\AutoCAD DRI - 01 Civil 3D\\Dybde.csv";

                    System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
                    System.Data.DataTable dtDybde = CsvReader.ReadCsvToDataTable(pathDybde, "Dybde");

                    #endregion
                    Transaction xrefTx = xRefDB.TransactionManager.StartTransaction();
                    List<Polyline3d> allPlines3d = xRefDB.ListOfType<Polyline3d>(xrefTx);

                    List<Alignment> alignments = db.ListOfType<Alignment>(tx);

                    List<string> layNames = new List<string>();

                    foreach (Alignment al in alignments)
                    {
                        editor.WriteMessage($"\n++++++++ Indlæser alignment {al.Name}. ++++++++");

                        List<Polyline3d> plines3d = FilterForCrossingEntities(allPlines3d, al);

                        if (allPlines3d.Count == 0)
                        {
                            editor.WriteMessage("\nNo 3D polylines found in drawing!" +
                                " Did you remember to run 'convertlinework'?");
                            return;
                        }

                        //Local function to avoid duplicate code
                        List<string> LocalListNames<T>(List<string> list, List<T> ents)
                        {
                            foreach (Entity ent in ents.Cast<Entity>())
                            {
                                LayerTableRecord layer = (LayerTableRecord)xrefTx.GetObject(ent.LayerId, OpenMode.ForRead);
                                //if (layer.IsFrozen) continue;

                                list.Add(layer.Name);
                            }
                            return list;
                        }

                        layNames.AddRange(LocalListNames(layNames, plines3d));
                        layNames = layNames.Distinct().ToList();
                    }

                    layNames = layNames.Distinct().OrderBy(x => x).ToList();

                    editor.WriteMessage($"\n++++++++ KONTROL ++++++++");

                    foreach (string name in layNames)
                    {
                        string nameInFile = ReadStringParameterFromDataTable(name, dtKrydsninger, "Navn", 0);
                        if (nameInFile.IsNoE())
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
                                editor.WriteMessage($"\nKontrollerer lag {name}:");

                                string layerInFile = ReadStringParameterFromDataTable(name, dtKrydsninger, "Layer", 0);
                                if (layerInFile.IsNoE())
                                    editor.WriteMessage($"\nFejl: Definition af kolonne \"Layer\" for ledningslag" +
                                        $" '{name}' mangler i Krydsninger.csv!");

                                if (typeInFile.IsNoE())
                                    editor.WriteMessage($"\nFejl: Definition af kolonne \"Type\" for ledningslag" +
                                        $" '{name}' mangler i Krydsninger.csv!");

                                string blockInFile = ReadStringParameterFromDataTable(name, dtKrydsninger, "Block", 0);
                                if (blockInFile.IsNoE())
                                    editor.WriteMessage($"\nAdvarsel: Definition af kolonne \"Block\" for ledningslag" +
                                        $" '{name}' mangler i Krydsninger.csv! Intet figur vil blive tegnet ved detaljering!");

                                string descrInFile = ReadStringParameterFromDataTable(name, dtKrydsninger, "Description", 0);
                                if (descrInFile.IsNoE())
                                    editor.WriteMessage($"\nAdvarsel: Definition af kolonne \"Description\" for ledningslag" +
                                        $" '{name}' mangler i Krydsninger.csv! Intet beskrivelse vil blive skrevet i labels!");
                            }
                        }
                    }

                    xrefTx.Commit();
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("check3delevations")]
        public void check3delevations()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            #region Read Csv Data for Layers and Depth

            //Establish the pathnames to files
            //Files should be placed in a specific folder on desktop
            string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";

            System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
            #endregion

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                string etapeName = GetEtapeName(editor);

                editor.WriteMessage("\n" + GetPathToDataFiles(etapeName, "Ler"));

                #region Load linework from LER Xref

                // open the LER dwg database
                Database xRefLerDB = new Database(false, true);

                xRefLerDB.ReadDwgFile(GetPathToDataFiles(etapeName, "Ler"),
                    System.IO.FileShare.Read, false, string.Empty);
                Transaction xRefLerTx = xRefLerDB.TransactionManager.StartTransaction();

                HashSet<Polyline3d> allLinework = xRefLerDB
                    .HashSetOfType<Polyline3d>(xRefLerTx)
                    .Where(x => ReadStringParameterFromDataTable(x.Layer, dtKrydsninger, "Type", 0) == "3D")
                    .ToHashSet();
                editor.WriteMessage($"\nNr. of 3D polies: {allLinework.Count}");
                #endregion

                HashSet<Alignment> alignments = localDb.HashSetOfType<Alignment>(tx)
                                                       .OrderBy(x => x.Name).ToHashSet();
                Plane plane = new Plane();

                try
                {
                    foreach (Alignment al in alignments)
                    {
                        editor.WriteMessage($"\n++++++++ Indlæser alignment {al.Name}. ++++++++");
                        System.Windows.Forms.Application.DoEvents();

                        //Filtering is required because else I would be dealing with all layers
                        //We need to limit the processed layers only to the crossed ones.
                        HashSet<Polyline3d> filteredLinework = FilterForCrossingEntities(allLinework, al);

                        foreach (Entity ent in filteredLinework)
                        {
                            #region Create points
                            using (Point3dCollection p3dcol = new Point3dCollection())
                            {
                                al.IntersectWith(ent, 0, plane, p3dcol, new IntPtr(0), new IntPtr(0));

                                int count = 1;
                                foreach (Point3d p3d in p3dcol)
                                {
                                    //Id of the new Poly3d if type == 3D
                                    oid newPolyId;

                                    #region Assign elevation based on 3D conditions
                                    double zElevation = 0;

                                    //Create vertical line to intersect the Ler line
                                    using (Transaction txp3d = localDb.TransactionManager.StartTransaction())
                                    {
                                        Point3dCollection newP3dCol = new Point3dCollection();
                                        //Intersection at 0
                                        newP3dCol.Add(p3d);
                                        //New point at very far away
                                        newP3dCol.Add(new Point3d(p3d.X, p3d.Y, 1000));

                                        Polyline3d newPoly = new Polyline3d(Poly3dType.SimplePoly, newP3dCol, false);

                                        //Open modelspace
                                        BlockTable acBlkTbl = txp3d.GetObject(
                                            localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                                        BlockTableRecord acBlkTblRec = txp3d.GetObject(acBlkTbl[BlockTableRecord.ModelSpace],
                                                         OpenMode.ForWrite) as BlockTableRecord;

                                        acBlkTblRec.AppendEntity(newPoly);
                                        txp3d.AddNewlyCreatedDBObject(newPoly, true);
                                        newPolyId = newPoly.ObjectId;
                                        txp3d.Commit();
                                    }

                                    Polyline3d newPoly3d = newPolyId.Go<Polyline3d>(tx);
                                    using (Point3dCollection p3dIntCol = new Point3dCollection())
                                    {
                                        ent.IntersectWith(newPoly3d, 0, p3dIntCol, new IntPtr(0), new IntPtr(0));

                                        foreach (Point3d p3dInt in p3dIntCol)
                                        {
                                            //Assume only one intersection
                                            if (p3dInt.Z == 0)
                                            {
                                                editor.WriteMessage($"\nEntity {ent.Handle} returned {p3dInt.Z}" +
                                                    $" elevation for a 3D layer.");
                                            }
                                        }
                                    }
                                    newPoly3d.UpgradeOpen();
                                    newPoly3d.Erase(true);
                                    System.Windows.Forms.Application.DoEvents();
                                    #endregion
                                }
                            }
                            #endregion

                            #region Erase the cloned 3D polies
                            ent.UpgradeOpen();
                            ent.Erase(true);
                            #endregion
                        }
                    }
                }
                catch (System.Exception)
                {
                    xRefLerTx.Abort();
                    tx.Abort();
                    throw;
                }

                xRefLerTx.Abort();
                tx.Commit();
            }
        }

        //Bruges ikke
        [Obsolete("Kommando bruges ikke.", false)]
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
                    string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";
                    string pathDybde = "X:\\AutoCAD DRI - 01 Civil 3D\\Dybde.csv";

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
                    return;
                }
                tx.Commit();
            }
        }
        //Bruges ikke
        [Obsolete("Kommando bruges ikke.", false)]
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

                if (!localLayerName.IsNoE() || localLayerName != null)
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

                if (!type.IsNoE() || type != null)
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

        //Bruges ikke
        [Obsolete("Kommando bruges ikke.", false)]
        [CommandMethod("createlerdatausingfls")]
        public void longitudinalprofilecrossings()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            //Using feature lines must be abandoned, because a reference crossing featur
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
                    PromptEntityOptions promptEntityOptions3 = new PromptEntityOptions("\n Select surface to get elevations: ");
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
                    string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";
                    string pathDybde = "X:\\AutoCAD DRI - 01 Civil 3D\\Dybde.csv";

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
                            if (type.IsNoE())
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
                            try
                            {
                                editor.WriteMessage($"\nProcessing entity handle: {localEntity.Handle}.");

                                tx2.TransactionManager.QueueForGraphicsFlush();

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
                                FeatureLine fl = flOid.Go<FeatureLine>(tx2);
                                fl.UpgradeOpen();
                                fl.Layer = localEntity.Layer;

                                #region Populate description field
                                //Populate description field
                                //1. Read size record
                                MapValue sizeRecord = Utils.ReadRecordData(
                                    tables, localEntity.ObjectId, "SizeTable", "Size");
                                int size = 0;
                                string sizeDescrPart = "";
                                if (sizeRecord != null)
                                {
                                    size = sizeRecord.Int16Value;
                                    sizeDescrPart = $"ø{size}";
                                }

                                //2. Read description from Krydsninger
                                string descrFromKrydsninger = ReadStringParameterFromDataTable(
                                    localEntity.Layer, dtKrydsninger, "Description", 0);

                                //Compose description field and assign to FL
                                List<string> descrParts = new List<string>();
                                //1.
                                if (size != 0) descrParts.Add(sizeDescrPart);
                                //2.
                                if (descrFromKrydsninger.IsNotNoE()) descrParts.Add(descrFromKrydsninger);

                                string description = "";
                                if (descrParts.Count == 1) description = descrParts[0];
                                else if (descrParts.Count > 1)
                                    description = string.Join("; ", descrParts);

                                //assign description
                                fl.Description = description;
                                #endregion

                                string type = ReadStringParameterFromDataTable(fl.Layer, dtKrydsninger, "Type", 0);
                                if (type.IsNoE())
                                {
                                    editor.WriteMessage($"\nFejl: For lag {fl.Layer} mangler der enten " +
                                        $"selve definitionen eller 'Type'!");
                                    tx2.Abort();
                                    return;
                                }

                                //Read depth value for type
                                double depth = 0;
                                if (!type.IsNoE())
                                {
                                    depth = Utils.ReadDoubleParameterFromDataTable(type, dtDybde, "Dybde", 0);
                                }
                                //If the geometry is not 3D, offset elevation values
                                if (flOid.ToString() != "(0)" && type != "3D")
                                {
                                    fl.Layer = localEntity.Layer;
                                    fl.AssignElevationsFromSurface(surfaceObjId, true);
                                }

                                localEntity.UpgradeOpen();
                                localEntity.Erase(true);

                                flCounter++;
                            }
                            catch (System.Exception ex)
                            {
                                editor.WriteMessage("\n" + ex.Message);
                                return;
                            }
                            tx2.Commit();
                        }
                    }
                    #endregion

                    #region Try translating FLs by depth
                    using (Transaction tx3 = localDb.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            #region Find only crossing FLs
                            HashSet<FeatureLine> flAllSet = localDb.HashSetOfType<FeatureLine>(tx3);
                            //List to hold only the crossing FLs
                            HashSet<FeatureLine> flCrossingSet = new HashSet<FeatureLine>();
                            foreach (FeatureLine fl in flAllSet)
                            {
                                //Filter out fl's not crossing the alignment in question
                                using (Point3dCollection p3dcol = new Point3dCollection())
                                {
                                    alignment.IntersectWith(fl, 0, plane, p3dcol, new IntPtr(0), new IntPtr(0));

                                    if (p3dcol.Count > 0)
                                    {
                                        flCrossingSet.Add(fl);
                                    }
                                }
                            }
                            #endregion

                            //Debug
                            string pathToLog = "X:\\AutoCAD DRI - 01 Civil 3D\\log.txt";
                            Utils.ClrFile(pathToLog);
                            int counter = 0;

                            foreach (FeatureLine fl in flCrossingSet)
                            {
                                //Read 'Type' value
                                string type = ReadStringParameterFromDataTable(fl.Layer, dtKrydsninger, "Type", 0);
                                if (type.IsNoE())
                                {
                                    editor.WriteMessage($"\nFejl: For lag {fl.Layer} mangler der enten " +
                                        $"selve definitionen eller 'Type'!");
                                    tx3.Abort();
                                    tx.Abort();
                                    return;
                                }

                                //Read depth value for type
                                double depth = 0;
                                if (!type.IsNoE())
                                {
                                    depth = Utils.ReadDoubleParameterFromDataTable(type, dtDybde, "Dybde", 0);
                                }

                                //Bogus 3D poly
                                if (depth > 0.001 && type != "3D")
                                {
                                    fl.UpgradeOpen();

                                    string originalName = fl.Name;
                                    string originalLayer = fl.Layer;
                                    string originalDescription = fl.Description;

                                    oid newPolyId;

                                    //Create a bogus 3d polyline and offset it
                                    using (Transaction tx4 = localDb.TransactionManager.StartTransaction())
                                    {
                                        Point3dCollection p3dcol = fl.GetPoints(FeatureLinePointType.AllPoints);
                                        Point3dCollection newP3dCol = new Point3dCollection();
                                        for (int i = 0; i < p3dcol.Count; i++)
                                        {
                                            Point3d originalP = p3dcol[i];
                                            Point3d newP = new Point3d(originalP.X, originalP.Y,
                                                originalP.Z - depth);
                                            newP3dCol.Add(newP);
                                        }

                                        Polyline3d newPoly = new Polyline3d(Poly3dType.SimplePoly, newP3dCol, false);

                                        //Open modelspace
                                        acBlkTbl = tx4.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                                        acBlkTblRec = tx4.GetObject(acBlkTbl[BlockTableRecord.ModelSpace],
                                                         OpenMode.ForWrite) as BlockTableRecord;

                                        acBlkTblRec.AppendEntity(newPoly);
                                        tx4.AddNewlyCreatedDBObject(newPoly, true);
                                        newPolyId = newPoly.ObjectId;
                                        tx4.Commit();
                                    }

                                    //Erase original FL
                                    fl.Erase(true);

                                    oid flOid = FeatureLine.Create(originalName, newPolyId);
                                    Entity ent1 = newPolyId.Go<Entity>(tx3, OpenMode.ForWrite);
                                    ent1.Erase(true);

                                    FeatureLine newFl = flOid.Go<FeatureLine>(tx3, OpenMode.ForWrite);
                                    newFl.Layer = originalLayer;
                                    newFl.Description = originalDescription;
                                }
                            }

                            #region Choose continue or not (Debugging)
                            //if (AskToContinueOrAbort(editor) == "Continue") continue; else break;

                            //string AskToContinueOrAbort(Editor locEd)
                            //{
                            //    string ckwd1 = "Continue";
                            //    string ckwd2 = "Abort";
                            //    PromptKeywordOptions pKeyOpts2 = new PromptKeywordOptions("");
                            //    pKeyOpts2.Message = "\nChoose next action: ";
                            //    pKeyOpts2.Keywords.Add(ckwd1);
                            //    pKeyOpts2.Keywords.Add(ckwd2);
                            //    pKeyOpts2.AllowNone = true;
                            //    pKeyOpts2.Keywords.Default = ckwd1;
                            //    PromptResult locpKeyRes2 = locEd.GetKeywords(pKeyOpts2);
                            //    return locpKeyRes2.StringResult;
                            //}

                            #endregion

                        }
                        catch (System.Exception ex)
                        {
                            editor.WriteMessage("\n" + ex.Message);
                            return;
                        }

                        tx3.Commit();
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        //[CommandMethod("createlerdata")]
        public void createlerdata(BlockReference blkRef = null, Alignment alignment = null,
                                  CivSurface surface = null, ProfileView pv = null)
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
                    if (blkRef == null)
                    {
                        PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions("\n Select a LER XREF : ");
                        promptEntityOptions1.SetRejectMessage("\n Not a XREF");
                        promptEntityOptions1.AddAllowedClass(typeof(Autodesk.AutoCAD.DatabaseServices.BlockReference), true);
                        PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                        if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                        Autodesk.AutoCAD.DatabaseServices.ObjectId blkObjId = entity1.ObjectId;
                        blkRef = tx.GetObject(blkObjId, OpenMode.ForRead, false)
                            as Autodesk.AutoCAD.DatabaseServices.BlockReference;
                    }

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
                    //oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(xRefDB);
                    oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);
                    #endregion

                    #region Load linework from Xref
                    List<Polyline3d> allLinework = xRefDB.ListOfType<Polyline3d>(xrefTx);
                    editor.WriteMessage($"\nNr. of 3D polies: {allLinework.Count}");
                    #endregion

                    #region Select Alignment
                    if (alignment == null)
                    {
                        //Get alignment
                        PromptEntityOptions promptEntityOptions2 = new PromptEntityOptions("\n Select alignment to intersect: ");
                        promptEntityOptions2.SetRejectMessage("\n Not an alignment");
                        promptEntityOptions2.AddAllowedClass(typeof(Alignment), true);
                        PromptEntityResult entity2 = editor.GetEntity(promptEntityOptions2);
                        if (((PromptResult)entity2).Status != PromptStatus.OK) return;
                        Autodesk.AutoCAD.DatabaseServices.ObjectId alObjId = entity2.ObjectId;
                        alignment = tx.GetObject(alObjId, OpenMode.ForRead, false) as Alignment;
                    }
                    #endregion

                    #region Select surface
                    //Get surface
                    if (surface == null)
                    {
                        PromptEntityOptions promptEntityOptions3 = new PromptEntityOptions("\n Select surface to get elevations: ");
                        promptEntityOptions3.SetRejectMessage("\n Not a surface");
                        promptEntityOptions3.AddAllowedClass(typeof(TinSurface), true);
                        promptEntityOptions3.AddAllowedClass(typeof(GridSurface), true);
                        PromptEntityResult entity3 = editor.GetEntity(promptEntityOptions3);
                        if (((PromptResult)entity3).Status != PromptStatus.OK) return;
                        Autodesk.AutoCAD.DatabaseServices.ObjectId surfaceObjId = entity3.ObjectId;
                        surface = surfaceObjId.GetObject(OpenMode.ForRead, false) as CivSurface;
                    }
                    #endregion

                    #region Read Csv Data for Layers and Depth

                    //Establish the pathnames to files
                    //Files should be placed in a specific folder on desktop
                    string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";
                    string pathDybde = "X:\\AutoCAD DRI - 01 Civil 3D\\Dybde.csv";

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
                            //if (type.IsNoE())
                            //{
                            //    editor.WriteMessage($"\nFejl: For xref lag {ent.Layer} mangler der enten" +
                            //        $"selve definitionen eller 'Type'!");
                            //    return;
                            //}
                            //Create 3d polyline if there's an intersection and
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
                    List<Polyline3d> localPlines3d = localDb.ListOfType<Polyline3d>(tx);
                    editor.WriteMessage($"\nNr. of local 3D polies: {localPlines3d.Count}");

                    List<Entity> allLocalLinework = new List<Entity>(
                        localPlines3d.Count
                        );

                    allLocalLinework.AddRange(localPlines3d.Cast<Entity>());
                    #endregion

                    #region Prepare variables
                    //Load things
                    Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;
                    CogoPointCollection cogoPoints = civilDoc.CogoPoints;
                    HashSet<CogoPoint> allNewlyCreatedPoints = new HashSet<CogoPoint>();
                    #endregion

                    #region Handle PointGroups
                    bool pointGroupAlreadyExists = civilDoc.PointGroups.Contains(alignment.Name);

                    PointGroup pg = null;

                    if (pointGroupAlreadyExists)
                    {
                        pg = civilDoc.PointGroups[alignment.Name].GetObject(OpenMode.ForWrite) as PointGroup;

                        pg.Update();

                        uint[] numbers = pg.GetPointNumbers();

                        CogoPointCollection cpc = civilDoc.CogoPoints;

                        for (int i = 0; i < numbers.Length; i++)
                        {
                            uint number = numbers[i];

                            if (cpc.Contains(number))
                            {
                                cpc.Remove(number);
                            }
                        }

                        pg.Update();
                    }
                    else
                    {
                        oid pgId = civilDoc.PointGroups.Add(alignment.Name);

                        pg = pgId.GetObject(OpenMode.ForWrite) as PointGroup;
                    }


                    #endregion

                    foreach (Entity ent in allLocalLinework)
                    {
                        #region Read data parameters from csvs
                        //Read 'Type' value
                        string type = ReadStringParameterFromDataTable(ent.Layer, dtKrydsninger, "Type", 0);
                        if (type.IsNoE())
                        {
                            editor.WriteMessage($"\nFejl: For lag {ent.Layer} mangler der enten " +
                                $"selve definitionen eller 'Type'!");
                            return;
                        }

                        //Read depth value for type
                        double depth = 0;
                        if (!type.IsNoE())
                        {
                            depth = Utils.ReadDoubleParameterFromDataTable(type, dtDybde, "Dybde", 0);
                        }

                        //Read layer value for the object
                        string localLayerName = Utils.ReadStringParameterFromDataTable(
                                            ent.Layer, dtKrydsninger, "Layer", 0);

                        #region Populate description field
                        //Populate description field
                        //1. Read size record if it exists
                        MapValue sizeRecord = Utils.ReadRecordData(
                            tables, ent.ObjectId, "SizeTable", "Size");
                        int SizeTableSize = 0;
                        string sizeDescrPart = "";
                        if (sizeRecord != null)
                        {
                            SizeTableSize = sizeRecord.Int32Value;
                            sizeDescrPart = $"ø{SizeTableSize}";
                        }

                        //2. Read description from Krydsninger
                        string descrFromKrydsninger = ReadStringParameterFromDataTable(
                            ent.Layer, dtKrydsninger, "Description", 0);

                        //2.1 Read the formatting in the description field
                        List<(string ToReplace, string Data)> descrFormatList = null;
                        if (descrFromKrydsninger.IsNotNoE())
                            descrFormatList = FindDescriptionParts(descrFromKrydsninger);

                        //Finally: Compose description field
                        List<string> descrParts = new List<string>();
                        //1. Add custom size
                        if (SizeTableSize != 0) descrParts.Add(sizeDescrPart);
                        //2. Process and add parts from format bits in OD
                        if (descrFromKrydsninger.IsNotNoE())
                        {
                            //Interpolate description from Krydsninger with format setting, if they exist
                            if (descrFormatList != null && descrFormatList.Count > 0)
                            {
                                for (int i = 0; i < descrFormatList.Count; i++)
                                {
                                    var tuple = descrFormatList[i];
                                    string result = ReadDescriptionPartsFromOD(tables, ent, tuple.Data, dtKrydsninger);
                                    descrFromKrydsninger = descrFromKrydsninger.Replace(tuple.ToReplace, result);
                                }
                            }

                            //Add the description field to parts
                            descrParts.Add(descrFromKrydsninger);
                        }

                        string description = "";
                        if (descrParts.Count == 1) description = descrParts[0];
                        else if (descrParts.Count > 1)
                            description = string.Join("; ", descrParts);

                        #endregion

                        //Source object (xref) handle
                        MapValue handleValue = ReadRecordData(
                                    tables, ent.ObjectId, "IdRecord", "Handle");

                        string pName = "";

                        if (handleValue != null) pName = handleValue.StrValue;
                        else pName = "Reading of Handle failed.";

                        #endregion

                        #region Create points
                        using (Point3dCollection p3dcol = new Point3dCollection())
                        {
                            alignment.IntersectWith(ent, 0, plane, p3dcol, new IntPtr(0), new IntPtr(0));

                            int count = 1;
                            foreach (Point3d p3d in p3dcol)
                            {
                                oid pointId = cogoPoints.Add(p3d, true);
                                CogoPoint cogoPoint = pointId.Go<CogoPoint>(tx, OpenMode.ForWrite);

                                //Id of the new Poly3d if type == 3D
                                oid newPolyId;

                                #region Assign elevation based on 3D conditions
                                double zElevation = 0;
                                if (type != "3D")
                                {
                                    var intPoint = surface.GetIntersectionPoint(p3d, new Vector3d(0, 0, 1));
                                    zElevation = intPoint.Z;

                                    //Subtract the depth (if invalid it is zero, so no modification will occur)
                                    zElevation -= depth;

                                    cogoPoint.Elevation = zElevation;
                                }
                                else if (type == "3D")
                                {
                                    //Create vertical line to intersect the Ler line
                                    using (Transaction txp3d = localDb.TransactionManager.StartTransaction())
                                    {
                                        Point3dCollection newP3dCol = new Point3dCollection();
                                        //Intersection at 0
                                        newP3dCol.Add(p3d);
                                        //New point at very far away
                                        newP3dCol.Add(new Point3d(p3d.X, p3d.Y, 1000));

                                        Polyline3d newPoly = new Polyline3d(Poly3dType.SimplePoly, newP3dCol, false);

                                        //Open modelspace
                                        acBlkTbl = txp3d.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                                        acBlkTblRec = txp3d.GetObject(acBlkTbl[BlockTableRecord.ModelSpace],
                                                         OpenMode.ForWrite) as BlockTableRecord;

                                        acBlkTblRec.AppendEntity(newPoly);
                                        txp3d.AddNewlyCreatedDBObject(newPoly, true);
                                        newPolyId = newPoly.ObjectId;
                                        txp3d.Commit();
                                    }

                                    Polyline3d newPoly3d = newPolyId.Go<Polyline3d>(tx);
                                    using (Point3dCollection p3dIntCol = new Point3dCollection())
                                    {
                                        ent.IntersectWith(newPoly3d, 0, p3dIntCol, new IntPtr(0), new IntPtr(0));

                                        foreach (Point3d p3dInt in p3dIntCol)
                                        {
                                            //Assume only one intersection
                                            cogoPoint.Elevation = p3dInt.Z;
                                        }
                                    }
                                    newPoly3d.UpgradeOpen();
                                    newPoly3d.Erase(true);
                                }
                                #endregion

                                //Set the layer
                                #region Layer handling
                                bool localLayerExists = false;

                                if (!localLayerName.IsNoE() || localLayerName != null)
                                {
                                    LayerTable lt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;
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
                                    editor.WriteMessage($"\nLocal layer name for source layer {ent.Layer} does not" +
                                        $" exist in Krydsninger.csv!");
                                }

                                cogoPoint.Layer = localLayerName;
                                #endregion

                                cogoPoint.PointName = pName + "_" + count;
                                count++;
                                cogoPoint.RawDescription = description;

                                #region Copy OD from polies to the new point
                                //Copy specific OD from cloned 3D polies to the new point

                                List<(string TableName, string RecordName)> odList =
                                    new List<(string TableName, string RecordName)>();
                                odList.Add(("IdRecord", "Handle"));
                                TryCopySpecificOD(tables, ent, cogoPoint, odList);
                                #endregion

                                #region Create Diameter OD
                                odList.Clear();
                                odList.Add(("SizeTable", "Size"));
                                //Fetch diameter definitions if any
                                string diaDef = ReadStringParameterFromDataTable(ent.Layer,
                                    dtKrydsninger, "Diameter", 0);
                                if (diaDef.IsNotNoE())
                                {
                                    var list = FindDescriptionParts(diaDef);
                                    //Be careful if FindDescriptionParts implementation changes
                                    string[] parts = list[0].Item2.Split(':');
                                    odList.Add((parts[0], parts[1]));
                                }

                                foreach (var item in odList)
                                {
                                    MapValue originalValue = ReadRecordData(
                                        tables, ent.ObjectId, item.TableName, item.RecordName);

                                    if (originalValue != null)
                                    {
                                        if (DoesTableExist(tables, "CrossingData"))
                                        {
                                            if (DoesRecordExist(tables, cogoPoint.ObjectId, "Diameter"))
                                            {
                                                UpdateODRecord(tables, "CrossingData", "Diameter",
                                                    cogoPoint.ObjectId, originalValue);
                                            }
                                            else
                                            {
                                                AddODRecord(tables, "CrossingData", "Diameter",
                                                    cogoPoint.ObjectId, originalValue);
                                            }
                                        }
                                        else
                                        {
                                            if (CreateTable(tables, "CrossingData", "Table holding relevant crossing data",
                                                "Diameter", "Diameter of crossing pipe",
                                                Autodesk.Gis.Map.Constants.DataType.Integer))
                                            {
                                                AddODRecord(tables, "CrossingData", "Diameter",
                                                    cogoPoint.ObjectId, originalValue);
                                            }
                                        }
                                    }
                                }
                                #endregion

                                //Reference newly created cogoPoint to gathering collection
                                allNewlyCreatedPoints.Add(cogoPoint);
                            }
                        }
                        #endregion

                        #region Erase the cloned 3D polies
                        ent.UpgradeOpen();
                        ent.Erase(true);
                        #endregion
                    }

                    #region Assign newly created points to projection on a profile view
                    //#region Select profile view
                    ////Get profile view
                    //PromptEntityOptions promptEntityOptions4 = new PromptEntityOptions("\n Select profile view: ");
                    //promptEntityOptions4.SetRejectMessage("\n Not a profile view");
                    //promptEntityOptions4.AddAllowedClass(typeof(ProfileView), true);
                    //PromptEntityResult entity4 = editor.GetEntity(promptEntityOptions4);
                    //if (((PromptResult)entity4).Status != PromptStatus.OK) return;
                    //Autodesk.AutoCAD.DatabaseServices.ObjectId pvObjId = entity4.ObjectId;
                    ////ProfileView pv = pvObjId.Go<ProfileView>(tx);
                    //#endregion

                    #region Build query for PointGroup
                    //Build query
                    StandardPointGroupQuery spgq = new StandardPointGroupQuery();
                    List<string> newPointNumbers = allNewlyCreatedPoints.Select(x => x.PointNumber.ToString()).ToList();
                    string pointNumbersToInclude = string.Join(",", newPointNumbers.ToArray());
                    spgq.IncludeNumbers = pointNumbersToInclude;
                    pg.SetQuery(spgq);
                    pg.Update();
                    #endregion

                    //editor.SetImpliedSelection(allNewlyCreatedPoints.Select(x => x.ObjectId).ToArray());

                    //editor.Command("_AeccProjectObjectsToProf", pvObjId);

                    #endregion
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }
        //Bruges ikke
        [Obsolete("Kommando bruges ikke.", false)]
        /// <summary>
        /// Add featurelines to selected profile view as crossings.
        /// </summary>
        [CommandMethod("createcrossings")]
        public void createcrossings()
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
                    #region Select Alignment
                    //Get alignment
                    PromptEntityOptions promptEntityOptions2 = new PromptEntityOptions("\n Select alignment: ");
                    promptEntityOptions2.SetRejectMessage("\n Not an alignment");
                    promptEntityOptions2.AddAllowedClass(typeof(Alignment), true);
                    PromptEntityResult entity2 = editor.GetEntity(promptEntityOptions2);
                    if (((PromptResult)entity2).Status != PromptStatus.OK) return;
                    Autodesk.AutoCAD.DatabaseServices.ObjectId alObjId = entity2.ObjectId;
                    Alignment alignment = tx.GetObject(alObjId, OpenMode.ForRead, false) as Alignment;
                    #endregion

                    #region Select profile view
                    //Get profile view
                    PromptEntityOptions promptEntityOptions3 = new PromptEntityOptions("\n Select profile view: ");
                    promptEntityOptions3.SetRejectMessage("\n Not a profile view");
                    promptEntityOptions3.AddAllowedClass(typeof(ProfileView), true);
                    PromptEntityResult entity3 = editor.GetEntity(promptEntityOptions3);
                    if (((PromptResult)entity3).Status != PromptStatus.OK) return;
                    Autodesk.AutoCAD.DatabaseServices.ObjectId pvObjId = entity3.ObjectId;
                    //ProfileView pv = pvObjId.Go<ProfileView>(tx);
                    #endregion

                    Plane plane = new Plane();

                    HashSet<FeatureLine> flAllSet = db.HashSetOfType<FeatureLine>(tx);
                    //List to hold only the crossing FLs
                    HashSet<FeatureLine> flCrossingSet = new HashSet<FeatureLine>();
                    foreach (FeatureLine fl in flAllSet)
                    {
                        //Filter out fl's not crossing the alignment in question
                        using (Point3dCollection p3dcol = new Point3dCollection())
                        {
                            alignment.IntersectWith(fl, 0, plane, p3dcol, new IntPtr(0), new IntPtr(0));

                            if (p3dcol.Count > 0)
                            {
                                flCrossingSet.Add(fl);
                            }
                        }
                    }

                    editor.SetImpliedSelection(flCrossingSet.Select(x => x.ObjectId).ToArray());

                    editor.Command("_AeccAddCrossingsProfile", pvObjId);
                }

                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("populateprofile")]
        public void populateprofile()
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
                    #region Read Csv Data for Layers
                    //Establish the pathnames to files
                    //Files should be placed in a specific folder on desktop
                    string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";

                    System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
                    #endregion

                    BlockTableRecord space = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                    BlockTable bt = tx.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;

                    #region Get the selection set of all objects and profile view
                    PromptSelectionOptions pOptions = new PromptSelectionOptions();
                    PromptSelectionResult sSetResult = editor.GetSelection(pOptions);
                    if (sSetResult.Status != PromptStatus.OK) return;
                    HashSet<Entity> allEnts = sSetResult.Value.GetObjectIds().Select(e => e.Go<Entity>(tx)).ToHashSet();
                    #endregion

                    #region Create a block for profile view detailing
                    //First, get the profile view
                    ProfileView pv = (ProfileView)allEnts.Where(p => p is ProfileView).FirstOrDefault();

                    if (pv == null)
                    {
                        editor.WriteMessage($"\nNo profile view found in selection!");
                        tx.Abort();
                        return;
                    }

                    pv.CheckOrOpenForWrite();
                    double x = 0.0;
                    double y = 0.0;
                    if (pv.ElevationRangeMode == ElevationRangeType.Automatic)
                    {
                        pv.ElevationRangeMode = ElevationRangeType.UserSpecified;
                        pv.FindXYAtStationAndElevation(pv.StationStart, pv.ElevationMin, ref x, ref y);
                    }
                    else
                        pv.FindXYAtStationAndElevation(pv.StationStart, pv.ElevationMin, ref x, ref y);

                    #region Erase existing detailing block if it exists
                    if (bt.Has(pv.Name))
                    {
                        if (!EraseBlock(doc, pv.Name))
                        {
                            editor.WriteMessage($"\nFailed to erase block: {pv.Name}.");
                            return;
                        }
                    }
                    #endregion

                    BlockTableRecord detailingBlock = new BlockTableRecord();
                    detailingBlock.Name = pv.Name;
                    detailingBlock.Origin = new Point3d(x, y, 0);

                    bt.Add(detailingBlock);
                    tx.AddNewlyCreatedDBObject(detailingBlock, true);
                    #endregion

                    #region Process labels
                    Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

                    LabelStyleCollection stc = civilDoc.Styles.LabelStyles
                                                       .ProjectionLabelStyles.ProfileViewProjectionLabelStyles;

                    //for (int i = 0; i < stc.Count; i++)
                    //{
                    //    oid id = stc[i];
                    //    LabelStyle ls = tx.GetObject(id, OpenMode.ForRead) as LabelStyle;
                    //    editor.WriteMessage($"\n{ls.Name}.");
                    //}

                    oid prStId = stc["PROFILE PROJEKTION MGO"];

                    foreach (Entity ent in allEnts)
                    {
                        if (ent is Label label)
                        {
                            oid fId = label.FeatureId;
                            Entity fEnt = fId.Go<Entity>(tx);

                            int diaOriginal = ReadIntPropertyValue(tables, fId, "CrossingData", "Diameter");

                            double dia = Convert.ToDouble(diaOriginal) / 1000;

                            if (dia == 0) dia = 0.11;

                            string blockName = ReadStringParameterFromDataTable(
                                fEnt.Layer, dtKrydsninger, "Block", 1);

                            if (blockName.IsNotNoE())
                            {
                                if (blockName == "Cirkel, Bund" || blockName == "Cirkel, Top")
                                {
                                    Circle circle = null;
                                    if (blockName.Contains("Bund"))
                                    {
                                        circle = new Circle(new Point3d(
                                        label.LabelLocation.X, label.LabelLocation.Y + (dia / 2), 0),
                                        Vector3d.ZAxis, dia / 2);
                                    }
                                    else if (blockName.Contains("Top"))
                                    {
                                        circle = new Circle(new Point3d(
                                        label.LabelLocation.X, label.LabelLocation.Y - (dia / 2), 0),
                                        Vector3d.ZAxis, dia / 2);
                                    }

                                    space.AppendEntity(circle);
                                    tx.AddNewlyCreatedDBObject(circle, false);
                                    circle.Layer = fEnt.Layer;

                                    Entity clone = circle.Clone() as Entity;
                                    detailingBlock.AppendEntity(clone);
                                    tx.AddNewlyCreatedDBObject(clone, true);
                                }
                                else if (bt.Has(blockName))
                                {
                                    using (var br = new Autodesk.AutoCAD.DatabaseServices.BlockReference(
                                        label.LabelLocation, bt[blockName]))
                                    {
                                        space.AppendEntity(br);
                                        tx.AddNewlyCreatedDBObject(br, false);
                                        br.Layer = fEnt.Layer;

                                        Entity clone = br.Clone() as Entity;
                                        detailingBlock.AppendEntity(clone);
                                        tx.AddNewlyCreatedDBObject(clone, true);
                                    }
                                }
                            }

                            ent.CheckOrOpenForWrite();
                            ent.Layer = fEnt.Layer;
                            label.CheckOrOpenForWrite();
                            label.StyleId = prStId;
                        }
                    }
                    #endregion

                    using (var br = new Autodesk.AutoCAD.DatabaseServices.BlockReference(
                                    new Point3d(x, y, 0), bt[pv.Name]))
                    {
                        space.AppendEntity(br);
                        tx.AddNewlyCreatedDBObject(br, true);
                    }
                }

                catch (System.Exception ex)
                {
                    tx.Abort();
                    throw new System.Exception(ex.Message);
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        //[CommandMethod("populateprofiles")]
        public void populateprofiles()
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
                    #region Read Csv Data for Layers
                    //Establish the pathnames to files
                    //Files should be placed in a specific folder on desktop
                    string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";

                    System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
                    #endregion

                    BlockTableRecord space = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                    BlockTable bt = tx.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;

                    //#region Get the selection set of all objects and profile view
                    //PromptSelectionOptions pOptions = new PromptSelectionOptions();
                    //PromptSelectionResult sSetResult = editor.GetSelection(pOptions);
                    //if (sSetResult.Status != PromptStatus.OK) return;
                    //HashSet<Entity> allEnts = sSetResult.Value.GetObjectIds().Select(e => e.Go<Entity>(tx)).ToHashSet();
                    //#endregion

                    HashSet<ProfileView> pvs = db.HashSetOfType<ProfileView>(tx);

                    foreach (ProfileView pv in pvs)
                    {
                        #region Create a block for profile view detailing
                        //First, get the profile view

                        if (pv == null)
                        {
                            editor.WriteMessage($"\nNo profile view found in document!");
                            return;
                        }

                        pv.CheckOrOpenForWrite();
                        double x = 0.0;
                        double y = 0.0;
                        if (pv.ElevationRangeMode == ElevationRangeType.Automatic)
                        {
                            pv.ElevationRangeMode = ElevationRangeType.UserSpecified;
                            pv.FindXYAtStationAndElevation(pv.StationStart, pv.ElevationMin, ref x, ref y);
                        }
                        else
                            pv.FindXYAtStationAndElevation(pv.StationStart, pv.ElevationMin, ref x, ref y);

                        #region Erase existing detailing block if it exists
                        if (bt.Has(pv.Name))
                        {
                            if (!EraseBlock(doc, pv.Name))
                            {
                                editor.WriteMessage($"\nFailed to erase block: {pv.Name}.");
                                return;
                            }
                        }
                        #endregion

                        BlockTableRecord detailingBlock = new BlockTableRecord();
                        detailingBlock.Name = pv.Name;
                        detailingBlock.Origin = new Point3d(x, y, 0);

                        bt.Add(detailingBlock);
                        tx.AddNewlyCreatedDBObject(detailingBlock, true);
                        #endregion

                        #region Process labels
                        Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

                        ObjectIdCollection lIds = pv.GetProfileViewLabelIds();

                        foreach (oid lId in lIds)
                        {
                            Label label = lId.Go<Label>(tx);

                            oid fId = label.FeatureId;
                            Entity fEnt = fId.Go<Entity>(tx);

                            int diaOriginal = ReadIntPropertyValue(tables, fId, "CrossingData", "Diameter");

                            double dia = Convert.ToDouble(diaOriginal) / 1000;

                            if (dia == 0) dia = 0.11;

                            string blockName = ReadStringParameterFromDataTable(
                                fEnt.Layer, dtKrydsninger, "Block", 1);

                            if (blockName.IsNotNoE())
                            {
                                if (blockName == "Cirkel, Bund" || blockName == "Cirkel, Top")
                                {
                                    Circle circle = null;
                                    if (blockName.Contains("Bund"))
                                    {
                                        circle = new Circle(new Point3d(
                                        label.LabelLocation.X, label.LabelLocation.Y + (dia / 2), 0),
                                        Vector3d.ZAxis, dia / 2);
                                    }
                                    else if (blockName.Contains("Top"))
                                    {
                                        circle = new Circle(new Point3d(
                                        label.LabelLocation.X, label.LabelLocation.Y - (dia / 2), 0),
                                        Vector3d.ZAxis, dia / 2);
                                    }

                                    space.AppendEntity(circle);
                                    tx.AddNewlyCreatedDBObject(circle, false);
                                    circle.Layer = fEnt.Layer;

                                    Entity clone = circle.Clone() as Entity;
                                    detailingBlock.AppendEntity(clone);
                                    tx.AddNewlyCreatedDBObject(clone, true);
                                }
                                else if (bt.Has(blockName))
                                {
                                    using (var br = new Autodesk.AutoCAD.DatabaseServices.BlockReference(
                                        label.LabelLocation, bt[blockName]))
                                    {
                                        space.AppendEntity(br);
                                        tx.AddNewlyCreatedDBObject(br, false);
                                        br.Layer = fEnt.Layer;

                                        Entity clone = br.Clone() as Entity;
                                        detailingBlock.AppendEntity(clone);
                                        tx.AddNewlyCreatedDBObject(clone, true);
                                    }
                                }
                            }

                            label.CheckOrOpenForWrite();
                            label.Layer = fEnt.Layer;
                        }
                        #endregion

                        using (var br = new Autodesk.AutoCAD.DatabaseServices.BlockReference(
                                        new Point3d(x, y, 0), bt[pv.Name]))
                        {
                            space.AppendEntity(br);
                            tx.AddNewlyCreatedDBObject(br, true);
                        }
                    }
                }

                catch (System.Exception ex)
                {
                    throw new System.Exception(ex.Message);
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        //Bruges ikke
        [Obsolete("Kommando bruges ikke.", false)]
        [CommandMethod("debugfl")]
        public void debugfl()
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

                    HashSet<FeatureLine> flSet = db.HashSetOfType<FeatureLine>(tx);
                    //List to hold only the crossing FLs
                    List<FeatureLine> flList = new List<FeatureLine>();
                    foreach (FeatureLine fl in flSet)
                    {
                        //Filter out fl's not crossing the alignment in question
                        using (Point3dCollection p3dcol = new Point3dCollection())
                        {
                            alignment.IntersectWith(fl, 0, plane, p3dcol, new IntPtr(0), new IntPtr(0));

                            if (p3dcol.Count > 0)
                            {
                                flList.Add(fl);
                            }
                        }
                        editor.WriteMessage($"\nProcessing FL {fl.Handle} derived from {fl.Name}.");
                        int pCount = fl.ElevationPointsCount;
                        editor.WriteMessage($"\nFL number of points {pCount.ToString()}.");
                    }
                }

                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                    return;
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
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("copytexttoattribute")]
        public void copytexttoattribute()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            while (true)
            {
                using (Transaction tx = localDb.TransactionManager.StartTransaction())
                {
                    try
                    {
                        #region Select pline3d
                        PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                            "\nSelect polyline3d to modify:");
                        promptEntityOptions1.SetRejectMessage("\n Not a polyline3d!");
                        promptEntityOptions1.AddAllowedClass(typeof(Polyline3d), true);
                        PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                        if (((PromptResult)entity1).Status != PromptStatus.OK) { return; }
                        Autodesk.AutoCAD.DatabaseServices.ObjectId pline3dId = entity1.ObjectId;
                        #endregion

                        #region Select text
                        PromptEntityOptions promptEntityOptions2 = new PromptEntityOptions(
                            "\nSelect text to copy:");
                        promptEntityOptions2.SetRejectMessage("\n Not a text!");
                        promptEntityOptions2.AddAllowedClass(typeof(DBText), true);
                        PromptEntityResult entity2 = editor.GetEntity(promptEntityOptions2);
                        if (((PromptResult)entity2).Status != PromptStatus.OK) { return; }
                        Autodesk.AutoCAD.DatabaseServices.ObjectId textId = entity2.ObjectId;
                        #endregion

                        #region Try creating records

                        string m_tableName = "GasDimOgMat";
                        string columnName = "DimOgMat";

                        Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

                        if (DoesTableExist(tables, m_tableName))
                        {
                            editor.WriteMessage("\nTable already exists!");
                        }
                        else
                        {
                            if (CreateTable(
                                tables, m_tableName, "Gas data", columnName, "Dimension og material",
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

                        string value = textId.Go<DBText>(tx).TextString;

                        if (DoesRecordExist(tables, pline3dId, columnName))
                        {
                            editor.WriteMessage("\nRecord already exists, updating...");
                            MapValue mapValue = new MapValue(value);
                            if (UpdateODRecord(tables, m_tableName, columnName, pline3dId, mapValue))
                            {
                                successCounter++;
                            }
                        }
                        else if (AddODRecord(tables, m_tableName, pline3dId, value))
                        {
                            successCounter++;
                        }
                        else failureCounter++;

                        if (successCounter > 0)
                        {
                            editor.WriteMessage($"\n{columnName} record created succesfully!");
                            Entity ent = pline3dId.Go<Entity>(tx, OpenMode.ForWrite);
                            ent.ColorIndex = 1;
                        }
                        else editor.WriteMessage($"\n{columnName} record creation failed!");

                        #endregion
                    }
                    catch (System.Exception ex)
                    {
                        editor.WriteMessage("\n" + ex.Message);
                        return;
                    }
                    tx.Commit();
                }
            }
        }

        [CommandMethod("linewidth")]
        public void setlinewidth()
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
                    return;
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
                    return false;
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
                    return;
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

                    Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

                    foreach (Spline spline in splines)
                    {
                        Curve curve = spline.ToPolylineWithPrecision(10);
                        acBlkTblRec.AppendEntity(curve);
                        tx.AddNewlyCreatedDBObject(curve, true);
                        curve.CheckOrOpenForWrite();
                        curve.Layer = spline.Layer;
                        CopyAllOD(tables, spline, curve);
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
                        polyline3D.CheckOrOpenForWrite();
                        polyline3D.Layer = pline.Layer;
                        acBlkTblRec.AppendEntity(polyline3D);
                        tx.AddNewlyCreatedDBObject(polyline3D, true);
                        CopyAllOD(tables, pline, polyline3D);
                    }

                    List<Line> lines = localDb.ListOfType<Line>(tx);
                    editor.WriteMessage($"\nNr. of lines: {lines.Count}");

                    foreach (Line line in lines)
                    {
                        Point3dCollection p3dcol = new Point3dCollection();

                        p3dcol.Add(line.StartPoint);
                        p3dcol.Add(line.EndPoint);

                        Polyline3d polyline3D = new Polyline3d(Poly3dType.SimplePoly, p3dcol, false);
                        polyline3D.CheckOrOpenForWrite();
                        polyline3D.Layer = line.Layer;
                        acBlkTblRec.AppendEntity(polyline3D);
                        tx.AddNewlyCreatedDBObject(polyline3D, true);
                        CopyAllOD(tables, line, polyline3D);
                    }

                    foreach (Line line in lines)
                    {
                        line.CheckOrOpenForWrite();
                        line.Erase(true);
                    }

                    foreach (Spline spline in splines)
                    {
                        spline.CheckOrOpenForWrite();
                        spline.Erase(true);
                    }

                    foreach (Polyline pl in polies)
                    {
                        pl.CheckOrOpenForWrite();
                        pl.Erase(true);
                    }
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }

            //Run create ids also
            createids();
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
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("selectcrossings")]
        public void selectcrossings()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            SelectCrossingObjects(localDb, editor, true);
        }

        private static void SelectCrossingObjects(Database localDb, Editor editor, bool askToKeepPointsAndText = false)
        {
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Load linework from local db
                    HashSet<Polyline3d> plines3d = localDb.HashSetOfType<Polyline3d>(tx);
                    editor.WriteMessage($"\nNr. of 3D polies: {plines3d.Count}");
                    #endregion

                    #region Select Alignment
                    HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);
                    #endregion

                    #region Choose to keep points and text or not
                    bool keepPointsAndText = false;

                    if (askToKeepPointsAndText)
                    {
                        const string ckwd1 = "Keep";
                        const string ckwd2 = "Discard";
                        PromptKeywordOptions pKeyOpts2 = new PromptKeywordOptions("");
                        pKeyOpts2.Message = "\nChoose to (K)eep points and text or (D)iscard: ";
                        pKeyOpts2.Keywords.Add(ckwd1);
                        pKeyOpts2.Keywords.Add(ckwd2);
                        pKeyOpts2.AllowNone = true;
                        pKeyOpts2.Keywords.Default = ckwd1;
                        PromptResult locpKeyRes2 = editor.GetKeywords(pKeyOpts2);

                        keepPointsAndText = locpKeyRes2.StringResult == ckwd1;
                    }
                    #endregion

                    Plane plane = new Plane();

                    List<oid> sourceIds = new List<oid>();

                    foreach (Alignment al in als)
                    {
                        //Gather the intersected objectIds
                        foreach (Polyline3d pl3d in plines3d)
                        {
                            using (Point3dCollection p3dcol = new Point3dCollection())
                            {
                                al.IntersectWith(pl3d, 0, plane, p3dcol, new IntPtr(0), new IntPtr(0));

                                if (p3dcol.Count > 0) sourceIds.Add(pl3d.ObjectId);
                            }
                        }

                        if (keepPointsAndText)
                        {
                            //Additional object classes to keep showing
                            List<DBPoint> points = localDb.ListOfType<DBPoint>(tx)
                                                          .Where(x => x.Position.Z > 0.1)
                                                          .ToList();
                            List<DBText> text = localDb.ListOfType<DBText>(tx);
                            //Add additional objects to isolation
                            foreach (DBPoint item in points) sourceIds.Add(item.ObjectId);
                            foreach (DBText item in text) sourceIds.Add(item.ObjectId);

                            sourceIds.Add(al.ObjectId);
                        }
                    }

                    editor.SetImpliedSelection(sourceIds.ToArray());
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                    return;
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
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("detectknudepunkterandinterpolate")]
        public void detectknudepunkterandinterpolate()
        {
            //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            //Slope in pro mille
            double slope = 20;
            //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            //Prepare list to hold processed lines.
            //For use for secondary interpolation for lines missing one or both end nodes.
            HashSet<Polyline3d> linesWithMissingNodes = new HashSet<Polyline3d>();
            //List to hold the interpolated lines
            HashSet<Polyline3d> interpolatedLines = new HashSet<Polyline3d>();

            //Process all lines and detect with nodes at both ends
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Load linework from local db
                    HashSet<Polyline3d> localPlines3d = localDb
                        .HashSetOfType<Polyline3d>(tx)
                        .Where(x => x.Layer == "AFL_ledning_faelles" ||
                                    x.Layer == "AFL_ikke_ibrug" ||
                                    x.Layer == "Afløb-kloakledning" ||
                                    x.Layer == "Regnvand")
                        .ToHashSet();
                    editor.WriteMessage($"\nNr. of local 3D polies: {localPlines3d.Count}");
                    #endregion

                    Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

                    //Points to intersect
                    HashSet<DBPoint> points = new HashSet<DBPoint>(localDb.ListOfType<DBPoint>(tx),
                                                  new PointDBHorizontalComparer());
                    editor.WriteMessage($"\nNr. of local points: {points.Count}");
                    editor.WriteMessage($"\nTotal number of combinations: " +
                        $"{points.Count * (localPlines3d.Count)}");

                    #region Poly3ds with knudepunkter at ends
                    foreach (Polyline3d pline3d in localPlines3d)
                    {
                        var vertices = pline3d.GetVertices(tx);

                        int endIdx = vertices.Length - 1;

                        //Start point
                        DBPoint startMatch = points.Where(x => x.Position.HorizontalEqualz(vertices[0].Position)).FirstOrDefault();
                        //End point
                        DBPoint endMatch = points.Where(x => x.Position.HorizontalEqualz(vertices[endIdx].Position)).FirstOrDefault();

                        if (startMatch != null && endMatch != null)
                        {
                            double startElevation = ReadDoublePropertyValue(tables, startMatch.ObjectId,
                                "AFL_knude", "BUNDKOTE");

                            double endElevation = ReadDoublePropertyValue(tables, endMatch.ObjectId,
                                "AFL_knude", "BUNDKOTE");

                            if (startElevation != 0 && endElevation != 0)
                            {
                                //Add to interpolated list
                                interpolatedLines.Add(pline3d);

                                //Start match
                                vertices[0].CheckOrOpenForWrite();
                                vertices[0].Position = new Point3d(
                                    vertices[0].Position.X, vertices[0].Position.Y, startElevation);

                                //End match
                                vertices[endIdx].CheckOrOpenForWrite();
                                vertices[endIdx].Position = new Point3d(
                                    vertices[endIdx].Position.X, vertices[endIdx].Position.Y, endElevation);

                                //Trig
                                //Start elevation is higher, thus we must start from backwards
                                if (startElevation > endElevation)
                                {
                                    double AB = pline3d.GetHorizontalLength(tx);
                                    //editor.WriteMessage($"\nAB: {AB}.");
                                    double AAmark = startElevation - endElevation;
                                    //editor.WriteMessage($"\nAAmark: {AAmark}.");
                                    double PB = 0;

                                    for (int i = endIdx; i >= 0; i--)
                                    {
                                        //We don't need to interpolate start and end points,
                                        //So skip them
                                        if (i != 0 && i != endIdx)
                                        {
                                            PB += vertices[i + 1].Position.DistanceHorizontalTo(
                                                 vertices[i].Position);
                                            //editor.WriteMessage($"\nPB: {PB}.");
                                            double newElevation = endElevation + PB * (AAmark / AB);
                                            //editor.WriteMessage($"\nNew elevation: {newElevation}.");
                                            //Change the elevation
                                            vertices[i].CheckOrOpenForWrite();
                                            vertices[i].Position = new Point3d(
                                                vertices[i].Position.X, vertices[i].Position.Y, newElevation);
                                        }
                                    }
                                }
                                else if (startElevation < endElevation)
                                {
                                    double AB = pline3d.GetHorizontalLength(tx);
                                    double AAmark = endElevation - startElevation;
                                    double PB = 0;

                                    for (int i = 0; i < endIdx; i++)
                                    {
                                        //We don't need to interpolate start and end points,
                                        //So skip them
                                        if (i != 0 && i != endIdx)
                                        {
                                            PB += vertices[i - 1].Position.DistanceHorizontalTo(
                                                 vertices[i].Position);
                                            double newElevation = startElevation + PB * (AAmark / AB);
                                            //editor.WriteMessage($"\nNew elevation: {newElevation}.");
                                            //Change the elevation
                                            vertices[i].CheckOrOpenForWrite();
                                            vertices[i].Position = new Point3d(
                                                vertices[i].Position.X, vertices[i].Position.Y, newElevation);
                                        }
                                    }
                                }
                                else
                                {
                                    editor.WriteMessage("\nElevations are the same!");
                                    //Make all elevations the same
                                }
                            }
                        }
                        else
                        {
                            //The rest of the lines assumed missing one or both nodes
                            linesWithMissingNodes.Add(pline3d);
                        }
                    }
                    #endregion

                    //Process lines with missing one or both end nodes
                    using (Transaction tx2 = localDb.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            editor.WriteMessage($"\nInterpolated: {interpolatedLines.Count}, " +
                                                $"Missing ends: {linesWithMissingNodes.Count}.");

                            #region Poly3ds without nodes at ends
                            foreach (Polyline3d pline3dWithMissingNodes in linesWithMissingNodes)
                            {
                                //Create 3d polies at both ends to intersect later
                                oid startPolyId;
                                oid endPolyId;

                                var vertices1 = pline3dWithMissingNodes.GetVertices(tx2);
                                int endIdx = vertices1.Length - 1;

                                using (Transaction tx2p3d = localDb.TransactionManager.StartTransaction())
                                {
                                    //Start point
                                    Point3dCollection newP3dColStart = new Point3dCollection();
                                    newP3dColStart.Add(vertices1[0].Position);
                                    //New point at very far away
                                    newP3dColStart.Add(new Point3d(vertices1[0].Position.X,
                                        vertices1[0].Position.Y, 1000));
                                    Polyline3d newPolyStart = new Polyline3d(Poly3dType.SimplePoly, newP3dColStart, false);

                                    //End point
                                    Point3dCollection newP3dColEnd = new Point3dCollection();
                                    newP3dColEnd.Add(vertices1[endIdx].Position);
                                    //New point at very far away
                                    newP3dColEnd.Add(new Point3d(vertices1[endIdx].Position.X,
                                        vertices1[endIdx].Position.Y, 1000));
                                    Polyline3d newPolyEnd = new Polyline3d(Poly3dType.SimplePoly, newP3dColEnd, false);

                                    //Open modelspace
                                    BlockTable bTbl = tx2p3d.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                                    BlockTableRecord bTblRec = tx2p3d.GetObject(bTbl[BlockTableRecord.ModelSpace],
                                                     OpenMode.ForWrite) as BlockTableRecord;

                                    bTblRec.AppendEntity(newPolyStart);
                                    tx2p3d.AddNewlyCreatedDBObject(newPolyStart, true);
                                    startPolyId = newPolyStart.ObjectId;

                                    bTblRec.AppendEntity(newPolyEnd);
                                    tx2p3d.AddNewlyCreatedDBObject(newPolyEnd, true);
                                    endPolyId = newPolyEnd.ObjectId;

                                    tx2p3d.Commit();
                                }

                                //Analyze intersection points with interpolated lines
                                using (Transaction tx2Other = localDb.TransactionManager.StartTransaction())
                                {
                                    Polyline3d startIntersector = startPolyId.Go<Polyline3d>(tx2Other);
                                    Polyline3d endIntersector = endPolyId.Go<Polyline3d>(tx2Other);

                                    double detectedStartElevation = 0;
                                    double detectedEndElevation = 0;

                                    var vertices2 = pline3dWithMissingNodes.GetVertices(tx2Other);

                                    foreach (Polyline3d poly3dOther in interpolatedLines)
                                    {
                                        #region Detect START elevation
                                        using (Point3dCollection p3dIntCol = new Point3dCollection())
                                        {
                                            poly3dOther.IntersectWith(startIntersector, 0, p3dIntCol, new IntPtr(0), new IntPtr(0));

                                            if (p3dIntCol.Count > 0 && p3dIntCol.Count < 2)
                                            {
                                                foreach (Point3d p3dInt in p3dIntCol)
                                                {
                                                    //Assume only one intersection
                                                    detectedStartElevation = p3dInt.Z;
                                                }
                                            }
                                        }
                                        #endregion
                                        #region Detect END elevation
                                        using (Point3dCollection p3dIntCol = new Point3dCollection())
                                        {
                                            poly3dOther.IntersectWith(endIntersector, 0, p3dIntCol, new IntPtr(0), new IntPtr(0));

                                            if (p3dIntCol.Count > 0 && p3dIntCol.Count < 2)
                                            {
                                                foreach (Point3d p3dInt in p3dIntCol)
                                                {
                                                    //Assume only one intersection
                                                    detectedEndElevation = p3dInt.Z;
                                                }
                                            }
                                        }
                                        #endregion

                                        if (detectedStartElevation > 0 && detectedEndElevation > 0)
                                        {
                                            //Trig
                                            //Start elevation is higher, thus we must start from backwards
                                            if (detectedStartElevation > detectedEndElevation)
                                            {
                                                double AB = pline3dWithMissingNodes.GetHorizontalLength(tx2Other);
                                                double AAmark = detectedStartElevation - detectedEndElevation;
                                                double PB = 0;

                                                for (int i = endIdx; i >= 0; i--)
                                                {
                                                    //We don't need to interpolate start and end points,
                                                    //So skip them
                                                    if (i != 0 && i != endIdx)
                                                    {
                                                        PB += vertices2[i + 1].Position.DistanceHorizontalTo(
                                                             vertices2[i].Position);
                                                        //editor.WriteMessage($"\nPB: {PB}.");
                                                        double newElevation = detectedEndElevation + PB * (AAmark / AB);
                                                        //editor.WriteMessage($"\nNew elevation: {newElevation}.");
                                                        //Change the elevation
                                                        vertices2[i].CheckOrOpenForWrite();
                                                        vertices2[i].Position = new Point3d(
                                                            vertices2[i].Position.X, vertices2[i].Position.Y, newElevation);
                                                    }
                                                    else if (i == 0)
                                                    {
                                                        vertices2[i].CheckOrOpenForWrite();
                                                        vertices2[i].Position = new Point3d(
                                                            vertices2[i].Position.X,
                                                            vertices2[i].Position.Y,
                                                            detectedStartElevation);
                                                    }
                                                    else if (i == endIdx)
                                                    {
                                                        vertices2[i].CheckOrOpenForWrite();
                                                        vertices2[i].Position = new Point3d(
                                                            vertices2[i].Position.X,
                                                            vertices2[i].Position.Y,
                                                            detectedEndElevation);
                                                    }
                                                }
                                            }
                                            else if (detectedStartElevation < detectedEndElevation)
                                            {
                                                double AB = pline3dWithMissingNodes.GetHorizontalLength(tx2Other);
                                                double AAmark = detectedEndElevation - detectedStartElevation;
                                                double PB = 0;

                                                for (int i = 0; i < endIdx; i++)
                                                {
                                                    //We don't need to interpolate start and end points,
                                                    //So skip them
                                                    if (i != 0 && i != endIdx)
                                                    {
                                                        PB += vertices2[i - 1].Position.DistanceHorizontalTo(
                                                             vertices2[i].Position);
                                                        double newElevation = detectedStartElevation + PB * (AAmark / AB);
                                                        //editor.WriteMessage($"\nNew elevation: {newElevation}.");
                                                        //Change the elevation
                                                        vertices2[i].CheckOrOpenForWrite();
                                                        vertices2[i].Position = new Point3d(
                                                            vertices2[i].Position.X, vertices2[i].Position.Y, newElevation);
                                                    }
                                                    else if (i == 0)
                                                    {
                                                        vertices2[i].CheckOrOpenForWrite();
                                                        vertices2[i].Position = new Point3d(
                                                            vertices2[i].Position.X,
                                                            vertices2[i].Position.Y,
                                                            detectedStartElevation);
                                                    }
                                                    else if (i == endIdx)
                                                    {
                                                        vertices2[i].CheckOrOpenForWrite();
                                                        vertices2[i].Position = new Point3d(
                                                            vertices2[i].Position.X,
                                                            vertices2[i].Position.Y,
                                                            detectedEndElevation);
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                editor.WriteMessage("\nElevations are the same!");
                                                //Make all elevations the same
                                            }
                                        }
                                        else if (detectedStartElevation > 0)
                                        {
                                            double PB = 0;

                                            for (int i = 0; i < endIdx; i++)
                                            {

                                                if (i != 0)
                                                {
                                                    PB += vertices2[i - 1].Position.DistanceHorizontalTo(
                                                         vertices2[i].Position);
                                                    double newElevation = detectedStartElevation + PB * slope / 1000;
                                                    //Change the elevation
                                                    vertices2[i].CheckOrOpenForWrite();
                                                    vertices2[i].Position = new Point3d(
                                                        vertices2[i].Position.X, vertices2[i].Position.Y, newElevation);
                                                }
                                                else if (i == 0)
                                                {
                                                    vertices2[i].CheckOrOpenForWrite();
                                                    vertices2[i].Position = new Point3d(
                                                        vertices2[i].Position.X,
                                                        vertices2[i].Position.Y,
                                                        detectedStartElevation);
                                                }
                                            }
                                        }
                                        else if (detectedEndElevation > 0)
                                        {
                                            double PB = 0;

                                            for (int i = endIdx; i >= 0; i--)
                                            {
                                                if (i != endIdx)
                                                {
                                                    PB += vertices2[i + 1].Position.DistanceHorizontalTo(
                                                         vertices2[i].Position);
                                                    double newElevation = detectedEndElevation + PB * slope / 1000;
                                                    //Change the elevation
                                                    vertices2[i].CheckOrOpenForWrite();
                                                    vertices2[i].Position = new Point3d(
                                                        vertices2[i].Position.X, vertices2[i].Position.Y, newElevation);
                                                }

                                                else if (i == endIdx)
                                                {
                                                    vertices2[i].CheckOrOpenForWrite();
                                                    vertices2[i].Position = new Point3d(
                                                        vertices2[i].Position.X,
                                                        vertices2[i].Position.Y,
                                                        detectedEndElevation);
                                                }
                                            }
                                        }
                                    }
                                    startIntersector.UpgradeOpen();
                                    startIntersector.Erase(true);
                                    endIntersector.UpgradeOpen();
                                    endIntersector.Erase(true);

                                    tx2Other.Commit();
                                }
                            }
                            #endregion
                        }
                        catch (System.Exception ex)
                        {
                            editor.WriteMessage("\n" + ex.Message);
                            return;
                        }
                        tx2.Commit();
                    }

                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                    return;
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

                #region Choose manual input or parsing of elevations
                const string kwd1 = "Manual";
                const string kwd2 = "Text";
                const string kwd3 = "OnOtherPl3d";
                const string kwd4 = "CalculateFromSlope";

                PromptKeywordOptions pKeyOpts = new PromptKeywordOptions("");
                pKeyOpts.Message = "\nChoose elevation input method: ";
                pKeyOpts.Keywords.Add(kwd1);
                pKeyOpts.Keywords.Add(kwd2);
                pKeyOpts.Keywords.Add(kwd3);
                pKeyOpts.Keywords.Add(kwd4);
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
                    case kwd3:
                        eim = ElevationInputMethod.OnOtherPl3d;
                        break;
                    case kwd4:
                        eim = ElevationInputMethod.CalculateFromSlope;
                        break;
                    default:
                        return;
                }
                #endregion

                Point3d selectedPoint;

                #region Get elevation depending on method
                double elevation = 0;
                switch (eim)
                {
                    case ElevationInputMethod.None:
                        return;
                    case ElevationInputMethod.Manual:
                        {
                            #region Select point
                            PromptPointOptions pPtOpts = new PromptPointOptions("");
                            // Prompt for the start point
                            pPtOpts.Message = "\nEnter location where to modify the pline3d (must be a vertex):";
                            PromptPointResult pPtRes = editor.GetPoint(pPtOpts);
                            selectedPoint = pPtRes.Value;
                            // Exit if the user presses ESC or cancels the command
                            if (pPtRes.Status != PromptStatus.OK) return;
                            #endregion

                            #region Get elevation
                            PromptDoubleResult result = editor.GetDouble("\nEnter elevation in meters:");
                            if (((PromptResult)result).Status != PromptStatus.OK) return;
                            elevation = result.Value;
                            #endregion 
                        }

                        break;
                    case ElevationInputMethod.Text:
                        {
                            #region Select point
                            PromptPointOptions pPtOpts = new PromptPointOptions("");
                            // Prompt for the start point
                            pPtOpts.Message = "\nEnter location where to modify the pline3d (must be a vertex):";
                            PromptPointResult pPtRes = editor.GetPoint(pPtOpts);
                            selectedPoint = pPtRes.Value;
                            // Exit if the user presses ESC or cancels the command
                            if (pPtRes.Status != PromptStatus.OK) return;
                            #endregion

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
                        }
                        break;
                    #endregion
                    case ElevationInputMethod.OnOtherPl3d:
                        {
                            //Create vertical line to intersect the Ler line

                            #region Select point
                            PromptPointOptions pPtOpts = new PromptPointOptions("");
                            // Prompt for the start point
                            pPtOpts.Message = "\nEnter location where to modify the pline3d (must be a vertex):";
                            PromptPointResult pPtRes = editor.GetPoint(pPtOpts);
                            selectedPoint = pPtRes.Value;
                            // Exit if the user presses ESC or cancels the command
                            if (pPtRes.Status != PromptStatus.OK) return;
                            #endregion

                            oid newPolyId;

                            using (Transaction txp3d = localDb.TransactionManager.StartTransaction())
                            {
                                Point3dCollection newP3dCol = new Point3dCollection();
                                //Intersection at 0
                                newP3dCol.Add(selectedPoint);
                                //New point at very far away
                                newP3dCol.Add(new Point3d(selectedPoint.X, selectedPoint.Y, 1000));

                                Polyline3d newPoly = new Polyline3d(Poly3dType.SimplePoly, newP3dCol, false);

                                //Open modelspace
                                BlockTable bTbl = txp3d.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                                BlockTableRecord bTblRec = txp3d.GetObject(bTbl[BlockTableRecord.ModelSpace],
                                                 OpenMode.ForWrite) as BlockTableRecord;

                                bTblRec.AppendEntity(newPoly);
                                txp3d.AddNewlyCreatedDBObject(newPoly, true);
                                newPolyId = newPoly.ObjectId;
                                txp3d.Commit();
                            }

                            #region Select pline3d
                            PromptEntityOptions promptEntityOptions3 = new PromptEntityOptions(
                                "\nSelect polyline3d to get elevation:");
                            promptEntityOptions3.SetRejectMessage("\n Not a polyline3d!");
                            promptEntityOptions3.AddAllowedClass(typeof(Polyline3d), true);
                            PromptEntityResult entity3 = editor.GetEntity(promptEntityOptions3);
                            if (((PromptResult)entity3).Status != PromptStatus.OK) return;
                            oid pline3dToGetElevationsId = entity3.ObjectId;
                            #endregion

                            using (Transaction txOther = localDb.TransactionManager.StartTransaction())
                            {
                                Polyline3d otherPoly3d = pline3dToGetElevationsId.Go<Polyline3d>(txOther);
                                Polyline3d newPoly3d = newPolyId.Go<Polyline3d>(txOther);
                                using (Point3dCollection p3dIntCol = new Point3dCollection())
                                {
                                    otherPoly3d.IntersectWith(newPoly3d, 0, p3dIntCol, new IntPtr(0), new IntPtr(0));

                                    if (p3dIntCol.Count > 0 && p3dIntCol.Count < 2)
                                    {
                                        foreach (Point3d p3dInt in p3dIntCol)
                                        {
                                            //Assume only one intersection
                                            elevation = p3dInt.Z;
                                        }
                                    }
                                }
                                newPoly3d.UpgradeOpen();
                                newPoly3d.Erase(true);
                                txOther.Commit();
                            }

                        }
                        break;
                    case ElevationInputMethod.CalculateFromSlope:
                        {
                            #region Select point from which to calculate
                            PromptPointOptions pPtOpts2 = new PromptPointOptions("");
                            // Prompt for the start point
                            pPtOpts2.Message = "\nEnter location from where to calculate using slope:";
                            PromptPointResult pPtRes2 = editor.GetPoint(pPtOpts2);
                            Point3d pointFrom = pPtRes2.Value;
                            // Exit if the user presses ESC or cancels the command
                            if (pPtRes2.Status != PromptStatus.OK) return;
                            #endregion

                            #region Get slope value
                            PromptDoubleResult result2 = editor.GetDouble("\nEnter slope in pro mille (negative slope downward):");
                            if (((PromptResult)result2).Status != PromptStatus.OK) return;
                            double slope = result2.Value;
                            #endregion

                            using (Transaction tx = localDb.TransactionManager.StartTransaction())
                            {
                                try
                                {
                                    Polyline3d pline3d = pline3dId.Go<Polyline3d>(tx);

                                    var vertices = pline3d.GetVertices(tx);

                                    //Starts from start
                                    if (pointFrom.HorizontalEqualz(vertices[0].Position))
                                    {
                                        double totalLength = 0;
                                        double startElevation = vertices[0].Position.Z;
                                        for (int i = 0; i < vertices.Length; i++)
                                        {
                                            if (i == 0) continue; //Skip first iteration, assume elevation is set

                                            totalLength += vertices[i - 1].Position
                                                                        .DistanceHorizontalTo(
                                                                               vertices[i].Position);

                                            double newDelta = totalLength * slope / 1000;
                                            double newElevation = startElevation + newDelta;

                                            //Write the new elevation
                                            vertices[i].CheckOrOpenForWrite();
                                            vertices[i].Position = new Point3d(
                                                vertices[i].Position.X, vertices[i].Position.Y, newElevation);
                                            vertices[i].DowngradeOpen();
                                        }
                                    }
                                    //Starts from end
                                    else if (pointFrom.HorizontalEqualz(vertices[vertices.Length - 1].Position))
                                    {
                                        double totalLength = 0;
                                        double startElevation = vertices[vertices.Length - 1].Position.Z;
                                        for (int i = vertices.Length - 1; i >= 0; i--)
                                        {
                                            if (i == vertices.Length - 1) continue; //Skip first iteration, assume elevation is set

                                            totalLength += vertices[i + 1].Position
                                                                        .DistanceHorizontalTo(
                                                                               vertices[i].Position);

                                            double newDelta = totalLength * slope / 1000;
                                            double newElevation = startElevation + newDelta;

                                            //Write the new elevation
                                            vertices[i].CheckOrOpenForWrite();
                                            vertices[i].Position = new Point3d(
                                                vertices[i].Position.X, vertices[i].Position.Y, newElevation);
                                            vertices[i].DowngradeOpen();
                                        }
                                    }
                                    else
                                    {
                                        editor.WriteMessage("\nSelected point is neither start nor end of the poly3d!");
                                        continue;
                                    }
                                }
                                catch (System.Exception ex)
                                {
                                    editor.WriteMessage("\n" + ex.Message);
                                    return;
                                }
                                tx.Commit();
                            }
                            //Continue the while loop
                            continue;
                        }
                    default:
                        return;
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
                            if (vertices[i].Position.HorizontalEqualz(selectedPoint))
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
                        return;
                    }
                    tx.Commit();
                }
                #endregion

                #region Choose next action
                const string ckwd1 = "NextPolyline3d";
                const string ckwd2 = "AddSizeToCurrentPline3d";

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

        [CommandMethod("decoratepolylines")]
        public void decoratepolylines()
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
                    #region Load linework
                    List<Polyline> plines = db.ListOfType<Polyline>(tx);
                    editor.WriteMessage($"\nNr. of plines: {plines.Count}");
                    #endregion

                    #region Layer handling
                    string localLayerName = "0-PLDECORATOR";
                    bool localLayerExists = false;

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
                    #endregion

                    #region Decorate polyline vertices
                    BlockTableRecord space = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                    BlockTable bt = tx.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;

                    #region Delete previous blocks

                    List<string> blockNameList = new List<string>() { "VerticeLine", "VerticeArc" };

                    foreach (string blockName in blockNameList)
                    {
                        var existingBlocks = db.GetBlockReferenceByName(blockName)
                            .Where(x => x.Layer == localLayerName)
                            .ToList();
                        editor.WriteMessage($"\n{existingBlocks.Count} existing blocks found of name {blockName}.");
                        foreach (Autodesk.AutoCAD.DatabaseServices.BlockReference br in existingBlocks)
                        {
                            br.CheckOrOpenForWrite();
                            br.Erase(true);
                        }
                    }

                    #endregion

                    foreach (Polyline pline in plines)
                    {
                        int numOfVerts = pline.NumberOfVertices - 1;
                        for (int i = 0; i < numOfVerts; i++)
                        {
                            switch (pline.GetSegmentType(i))
                            {
                                case SegmentType.Line:
                                    {
                                        string blockName = "VerticeLine";
                                        if (bt.Has(blockName))
                                        {
                                            LineSegment2d lineSegment2dAt = pline.GetLineSegment2dAt(i);

                                            Point2d point2d1 = lineSegment2dAt.StartPoint;
                                            using (var br = new Autodesk.AutoCAD.DatabaseServices.BlockReference(
                                                new Point3d(point2d1.X, point2d1.Y, 0), bt[blockName]))
                                            {
                                                space.AppendEntity(br);
                                                tx.AddNewlyCreatedDBObject(br, true);
                                                br.Layer = localLayerName;
                                            }

                                            Point2d point2d2 = lineSegment2dAt.EndPoint;
                                            using (var br = new Autodesk.AutoCAD.DatabaseServices.BlockReference(
                                                new Point3d(point2d2.X, point2d2.Y, 0), bt[blockName]))
                                            {
                                                space.AppendEntity(br);
                                                tx.AddNewlyCreatedDBObject(br, true);
                                                br.Layer = localLayerName;
                                            }
                                        }
                                    }
                                    break;
                                case SegmentType.Arc:
                                    {
                                        string blockName = "VerticeArc";
                                        if (bt.Has(blockName))
                                        {
                                            CircularArc2d arcSegment2dAt = pline.GetArcSegment2dAt(i);

                                            Point2d point2d1 = arcSegment2dAt.StartPoint;
                                            using (var br = new Autodesk.AutoCAD.DatabaseServices.BlockReference(
                                                new Point3d(point2d1.X, point2d1.Y, 0), bt[blockName]))
                                            {
                                                space.AppendEntity(br);
                                                tx.AddNewlyCreatedDBObject(br, true);
                                                br.Layer = localLayerName;
                                            }

                                            Point2d point2d2 = arcSegment2dAt.EndPoint;
                                            using (var br = new Autodesk.AutoCAD.DatabaseServices.BlockReference(
                                                new Point3d(point2d2.X, point2d2.Y, 0), bt[blockName]))
                                            {
                                                space.AppendEntity(br);
                                                tx.AddNewlyCreatedDBObject(br, true);
                                                br.Layer = localLayerName;
                                            }

                                            Point2d samplePoint = ((Curve2d)arcSegment2dAt).GetSamplePoints(11)[5];
                                            using (var br = new Autodesk.AutoCAD.DatabaseServices.BlockReference(
                                                new Point3d(samplePoint.X, samplePoint.Y, 0), bt[blockName]))
                                            {
                                                space.AppendEntity(br);
                                                tx.AddNewlyCreatedDBObject(br, true);
                                                br.Layer = localLayerName;
                                            }
                                        }
                                    }
                                    break;
                                case SegmentType.Coincident:
                                    break;
                                case SegmentType.Point:
                                    break;
                                case SegmentType.Empty:
                                    break;
                                default:
                                    break;
                            }
                        }
                    }

                    #endregion

                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }

        }

        [CommandMethod("createprofileviews")]
        public void createprofileviews()
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
                    #region Read Csv Data for Layers and Depth

                    //Establish the pathnames to files
                    //Files should be placed in a specific folder on desktop
                    string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";
                    string pathDybde = "X:\\AutoCAD DRI - 01 Civil 3D\\Dybde.csv";

                    System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
                    System.Data.DataTable dtDybde = CsvReader.ReadCsvToDataTable(pathDybde, "Dybde");

                    #endregion

                    BlockTableRecord space = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                    BlockTable bt = tx.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;

                    List<Alignment> allAlignments = db.ListOfType<Alignment>(tx).OrderBy(x => x.Name).ToList();
                    HashSet<ProfileView> pvSetExisting = db.HashSetOfType<ProfileView>(tx);

                    string etapeName = GetEtapeName(editor);

                    editor.WriteMessage("\n" + GetPathToDataFiles(etapeName, "Ler"));
                    editor.WriteMessage("\n" + GetPathToDataFiles(etapeName, "Surface"));

                    #region Read surface from file
                    // open the xref database
                    Database xRefSurfaceDB = new Database(false, true);
                    xRefSurfaceDB.ReadDwgFile(GetPathToDataFiles(etapeName, "Surface"),
                        System.IO.FileShare.Read, false, string.Empty);
                    Transaction xRefSurfaceTx = xRefSurfaceDB.TransactionManager.StartTransaction();

                    CivSurface surface = null;
                    try
                    {

                        surface = xRefSurfaceDB
                            .HashSetOfType<TinSurface>(xRefSurfaceTx)
                            .FirstOrDefault() as CivSurface;
                    }
                    catch (System.Exception)
                    {
                        xRefSurfaceTx.Abort();
                        throw;
                    }

                    if (surface == null)
                    {
                        editor.WriteMessage("\nSurface could not be loaded from the xref!");
                        xRefSurfaceTx.Commit();
                        return;
                    }
                    #endregion

                    #region Load linework from LER Xref
                    // open the LER dwg database
                    Database xRefLerDB = new Database(false, true);

                    xRefLerDB.ReadDwgFile(GetPathToDataFiles(etapeName, "Ler"),
                        System.IO.FileShare.Read, false, string.Empty);
                    Transaction xRefLerTx = xRefLerDB.TransactionManager.StartTransaction();

                    HashSet<Polyline3d> allLinework = xRefLerDB.HashSetOfType<Polyline3d>(xRefLerTx)
                        .Where(x => ReadStringParameterFromDataTable(x.Layer, dtKrydsninger, "Type", 0) != "IGNORE")
                        .ToHashSet();
                    #endregion

                    #region Delete existing points
                    PointGroupCollection pgs = civilDoc.PointGroups;

                    for (int i = 0; i < pgs.Count; i++)
                    {
                        PointGroup pg = tx.GetObject(pgs[i], OpenMode.ForRead) as PointGroup;
                        if (allAlignments.Any(x => x.Name == pg.Name) &&
                            !pvSetExisting.Any(x => x.Name == pg.Name + "_PV"))
                        {
                            pg.CheckOrOpenForWrite();
                            pg.Update();
                            uint[] numbers = pg.GetPointNumbers();

                            CogoPointCollection cpc = civilDoc.CogoPoints;

                            for (int j = 0; j < numbers.Length; j++)
                            {
                                uint number = numbers[j];

                                if (cpc.Contains(number))
                                {
                                    cpc.Remove(number);
                                }
                            }

                            StandardPointGroupQuery spgqEmpty = new StandardPointGroupQuery();
                            spgqEmpty.IncludeNumbers = "";
                            pg.SetQuery(spgqEmpty);

                            pg.Update();
                        }
                    }
                    #endregion

                    #region Create profile views

                    oid profileViewBandSetStyleId = civilDoc.Styles
                            .ProfileViewBandSetStyles["EG-FG Elevations and Stations"];
                    oid profileViewStyleId = civilDoc.Styles
                        .ProfileViewStyles["PROFILE VIEW L TO R NO SCALE"];

                    //Used to keep track of point names
                    HashSet<string> pNames = new HashSet<string>();

                    int index = 1;

                    #region Select point
                    PromptPointOptions pPtOpts = new PromptPointOptions("");
                    // Prompt for the start point
                    pPtOpts.Message = "\nSelect location where to draw first profile view:";
                    PromptPointResult pPtRes = editor.GetPoint(pPtOpts);
                    Point3d selectedPoint = pPtRes.Value;
                    // Exit if the user presses ESC or cancels the command
                    if (pPtRes.Status != PromptStatus.OK) return;
                    #endregion

                    foreach (Alignment alignment in allAlignments)
                    {
                        //If ProfileView already exists -> continue
                        if (pvSetExisting.Any(x => x.Name == $"{alignment.Name}_PV")) continue;

                        editor.WriteMessage($"\n_-*-_ | Processing alignment {alignment.Name}. | _-*-_");
                        System.Windows.Forms.Application.DoEvents();

                        HashSet<Polyline3d> filteredLinework = FilterForCrossingEntities(allLinework, alignment);

                        #region Create profile view
                        #region Calculate point
                        Point3d insertionPoint = new Point3d(selectedPoint.X, selectedPoint.Y + index * -200, 0);
                        #endregion

                        oid pvId = ProfileView.Create(alignment.ObjectId, insertionPoint,
                            $"{alignment.Name}_PV", profileViewBandSetStyleId, profileViewStyleId);

                        index++;
                        #endregion

                        #region Create ler data

                        createlerdataloopnodeepclone(filteredLinework, alignment, surface, pvId.Go<ProfileView>(tx),
                                                     dtKrydsninger, dtDybde, ref pNames);
                        #endregion
                    }

                    #endregion
                    xRefLerTx.Abort();
                    xRefSurfaceTx.Abort();
                }

                catch (System.Exception ex)
                {
                    throw new System.Exception(ex.Message);
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }

                tx.Commit();
            }
        }
        public void createlerdataloopnodeepclone(HashSet<Polyline3d> allLinework, Alignment alignment,
                                      CivSurface surface, ProfileView pv,
                                      System.Data.DataTable dtKrydsninger, System.Data.DataTable dtDybde,
                                      ref HashSet<string> pNames)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            Transaction tx = localDb.TransactionManager.TopTransaction;

            try
            {
                #region ModelSpaces
                //oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(xRefDB);
                oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);
                #endregion

                #region Clone remote objects to local dwg

                BlockTable acBlkTbl = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord acBlkTblRec =
                    tx.GetObject(acBlkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                Plane plane = new Plane();

                editor.WriteMessage($"\nTotal {allLinework.Count} intersections detected.");
                #endregion

                #region Prepare variables
                //Load things
                Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;
                CogoPointCollection cogoPoints = civilDoc.CogoPoints;
                HashSet<CogoPoint> allNewlyCreatedPoints = new HashSet<CogoPoint>();
                #endregion

                #region Handle PointGroups
                bool pointGroupAlreadyExists = civilDoc.PointGroups.Contains(alignment.Name);

                PointGroup pg = null;

                if (pointGroupAlreadyExists)
                {
                    pg = tx.GetObject(civilDoc.PointGroups[alignment.Name], OpenMode.ForWrite) as PointGroup;
                }
                else
                {
                    oid pgId = civilDoc.PointGroups.Add(alignment.Name);

                    pg = pgId.GetObject(OpenMode.ForWrite) as PointGroup;
                }
                #endregion

                foreach (Entity ent in allLinework)
                {
                    #region Read data parameters from csvs
                    //Read 'Type' value
                    string type = ReadStringParameterFromDataTable(ent.Layer, dtKrydsninger, "Type", 0);
                    if (type.IsNoE())
                    {
                        editor.WriteMessage($"\nFejl: For lag {ent.Layer} mangler der enten " +
                            $"selve definitionen eller 'Type'!");
                        return;
                    }

                    //Read depth value for type
                    double depth = 0;
                    if (!type.IsNoE())
                    {
                        depth = Utils.ReadDoubleParameterFromDataTable(type, dtDybde, "Dybde", 0);
                    }

                    //Read layer value for the object
                    string localLayerName = Utils.ReadStringParameterFromDataTable(
                                        ent.Layer, dtKrydsninger, "Layer", 0);

                    #region Populate description field
                    //Populate description field
                    //1. Read size record if it exists
                    MapValue sizeRecord = Utils.ReadRecordData(
                        tables, ent.ObjectId, "SizeTable", "Size");
                    int SizeTableSize = 0;
                    string sizeDescrPart = "";
                    if (sizeRecord != null)
                    {
                        SizeTableSize = sizeRecord.Int32Value;
                        sizeDescrPart = $"ø{SizeTableSize}";
                    }

                    //2. Read description from Krydsninger
                    string descrFromKrydsninger = ReadStringParameterFromDataTable(
                        ent.Layer, dtKrydsninger, "Description", 0);

                    //2.1 Read the formatting in the description field
                    List<(string ToReplace, string Data)> descrFormatList = null;
                    if (descrFromKrydsninger.IsNotNoE())
                        descrFormatList = FindDescriptionParts(descrFromKrydsninger);

                    //Finally: Compose description field
                    List<string> descrParts = new List<string>();
                    //1. Add custom size
                    if (SizeTableSize != 0) descrParts.Add(sizeDescrPart);
                    //2. Process and add parts from format bits in OD
                    if (descrFromKrydsninger.IsNotNoE())
                    {
                        //Interpolate description from Krydsninger with format setting, if they exist
                        if (descrFormatList != null && descrFormatList.Count > 0)
                        {
                            for (int i = 0; i < descrFormatList.Count; i++)
                            {
                                var tuple = descrFormatList[i];
                                string result = ReadDescriptionPartsFromOD(tables, ent, tuple.Data, dtKrydsninger);
                                descrFromKrydsninger = descrFromKrydsninger.Replace(tuple.ToReplace, result);
                            }
                        }

                        //Add the description field to parts
                        descrParts.Add(descrFromKrydsninger);
                    }

                    string description = "";
                    if (descrParts.Count == 1) description = descrParts[0];
                    else if (descrParts.Count > 1)
                        description = string.Join("; ", descrParts);

                    #endregion

                    string handleValue = ent.Handle.ToString();
                    string pName = "";

                    if (handleValue != null) pName = handleValue;
                    else
                    {
                        pName = "Reading of Handle failed.";
                        editor.WriteMessage($"\nEntity on layer {ent.Layer} failed to read Handle!");
                    }

                    #endregion

                    #region Create points
                    using (Point3dCollection p3dcol = new Point3dCollection())
                    {
                        alignment.IntersectWith(ent, 0, plane, p3dcol, new IntPtr(0), new IntPtr(0));

                        foreach (Point3d p3d in p3dcol)
                        {
                            oid pointId = cogoPoints.Add(p3d, true);
                            CogoPoint cogoPoint = pointId.Go<CogoPoint>(tx, OpenMode.ForWrite);

                            //Id of the new Poly3d if type == 3D
                            oid newPolyId;

                            #region Assign elevation based on 3D conditions
                            double zElevation = 0;
                            if (type != "3D")
                            {
                                var intPoint = surface.GetIntersectionPoint(p3d, new Vector3d(0, 0, 1));
                                zElevation = intPoint.Z;

                                //Subtract the depth (if invalid it is zero, so no modification will occur)
                                zElevation -= depth;

                                cogoPoint.Elevation = zElevation;
                            }
                            else if (type == "3D")
                            {
                                //Create vertical line to intersect the Ler line
                                using (Transaction txp3d = localDb.TransactionManager.StartTransaction())
                                {
                                    Point3dCollection newP3dCol = new Point3dCollection();
                                    //Intersection at 0
                                    newP3dCol.Add(p3d);
                                    //New point at very far away
                                    newP3dCol.Add(new Point3d(p3d.X, p3d.Y, 1000));

                                    Polyline3d newPoly = new Polyline3d(Poly3dType.SimplePoly, newP3dCol, false);

                                    //Open modelspace
                                    acBlkTbl = txp3d.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                                    acBlkTblRec = txp3d.GetObject(acBlkTbl[BlockTableRecord.ModelSpace],
                                                     OpenMode.ForWrite) as BlockTableRecord;

                                    acBlkTblRec.AppendEntity(newPoly);
                                    txp3d.AddNewlyCreatedDBObject(newPoly, true);
                                    newPolyId = newPoly.ObjectId;
                                    txp3d.Commit();
                                }

                                Polyline3d newPoly3d = newPolyId.Go<Polyline3d>(tx);
                                using (Point3dCollection p3dIntCol = new Point3dCollection())
                                {
                                    ent.IntersectWith(newPoly3d, 0, p3dIntCol, new IntPtr(0), new IntPtr(0));

                                    foreach (Point3d p3dInt in p3dIntCol)
                                    {
                                        //Assume only one intersection
                                        cogoPoint.Elevation = p3dInt.Z;
                                    }

                                    if (cogoPoint.Elevation == 0)
                                    {
                                        editor.WriteMessage($"\nFor type 3D entity {handleValue}" +
                                            $" layer {ent.Layer}," +
                                            $" elevation is 0!");
                                    }
                                }
                                newPoly3d.UpgradeOpen();
                                newPoly3d.Erase(true);
                            }
                            #endregion

                            //Set the layer
                            #region Layer handling
                            bool localLayerExists = false;

                            if (!localLayerName.IsNoE() || localLayerName != null)
                            {
                                LayerTable lt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;
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
                                editor.WriteMessage($"\nLocal layer name for source layer {ent.Layer} does not" +
                                    $" exist in Krydsninger.csv!");
                            }

                            cogoPoint.Layer = localLayerName;
                            #endregion

                            int count = 1;
                            string pointName = pName + "_" + count;

                            while (pNames.Contains(pointName))
                            {
                                count++;
                                pointName = pName + "_" + count;
                            }
                            pNames.Add(pointName);
                            cogoPoint.PointName = pointName;
                            cogoPoint.RawDescription = description;

                            #region Copy OD from polies to the new point
                            //Copy specific OD from cloned 3D polies to the new point

                            List<(string TableName, string RecordName)> odList =
                                new List<(string TableName, string RecordName)>();
                            odList.Add(("IdRecord", "Handle"));
                            TryCopySpecificOD(tables, ent, cogoPoint, odList);
                            #endregion

                            #region Create Diameter OD
                            odList.Clear();
                            odList.Add(("SizeTable", "Size"));
                            //Fetch diameter definitions if any
                            string diaDef = ReadStringParameterFromDataTable(ent.Layer,
                                dtKrydsninger, "Diameter", 0);
                            if (diaDef.IsNotNoE())
                            {
                                var list = FindDescriptionParts(diaDef);
                                //Be careful if FindDescriptionParts implementation changes
                                string[] parts = list[0].Item2.Split(':');
                                odList.Add((parts[0], parts[1]));
                            }

                            foreach (var item in odList)
                            {
                                MapValue originalValue = ReadRecordData(
                                    tables, ent.ObjectId, item.TableName, item.RecordName);

                                if (originalValue != null)
                                {
                                    if (DoesTableExist(tables, "CrossingData"))
                                    {
                                        if (DoesRecordExist(tables, cogoPoint.ObjectId, "Diameter"))
                                        {
                                            UpdateODRecord(tables, "CrossingData", "Diameter",
                                                cogoPoint.ObjectId, originalValue);
                                        }
                                        else
                                        {
                                            AddODRecord(tables, "CrossingData", "Diameter",
                                                cogoPoint.ObjectId, originalValue);
                                        }
                                    }
                                    else
                                    {
                                        if (CreateTable(tables, "CrossingData", "Table holding relevant crossing data",
                                            "Diameter", "Diameter of crossing pipe",
                                            Autodesk.Gis.Map.Constants.DataType.Integer))
                                        {
                                            AddODRecord(tables, "CrossingData", "Diameter",
                                                cogoPoint.ObjectId, originalValue);
                                        }
                                    }
                                }
                            }
                            #endregion

                            //Reference newly created cogoPoint to gathering collection
                            allNewlyCreatedPoints.Add(cogoPoint);
                        }
                    }
                    #endregion

                    #region Erase the cloned 3D polies
                    ent.UpgradeOpen();
                    ent.Erase(true);
                    #endregion
                }

                #region Assign newly created points to projection on a profile view
                #region Build query for PointGroup
                //Build query
                StandardPointGroupQuery spgq = new StandardPointGroupQuery();
                List<string> newPointNumbers = allNewlyCreatedPoints.Select(x => x.PointNumber.ToString()).ToList();
                string pointNumbersToInclude = string.Join(",", newPointNumbers.ToArray());
                spgq.IncludeNumbers = pointNumbersToInclude;
                pg.SetQuery(spgq);
                pg.Update();
                #endregion

                double elMax = pv.ElevationMax;
                double elMin = pv.ElevationMin;

                double tryGetMin = allNewlyCreatedPoints.Min(x => x.Elevation);

                if (tryGetMin != 0)
                {
                    pv.CheckOrOpenForWrite();
                    elMin = tryGetMin - 1;
                    pv.ElevationRangeMode = ElevationRangeType.UserSpecified;
                    pv.ElevationMin = elMin;
                }

                editor.SetImpliedSelection(allNewlyCreatedPoints.Select(x => x.ObjectId).ToArray());
                editor.Command("_AeccProjectObjectsToProf", pv.ObjectId);

                #endregion
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage("\n" + ex.Message);
                return;
            }
        }
        public void createlerdataloop(Database xRefDB, Alignment alignment,
                                      CivSurface surface, ProfileView pv,
                                      System.Data.DataTable dtKrydsninger, System.Data.DataTable dtDybde,
                                      ref HashSet<string> pNames)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            Transaction tx = localDb.TransactionManager.TopTransaction;

            try
            {
                #region ModelSpaces
                //oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(xRefDB);
                oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);
                #endregion

                #region Load linework from Xref
                //Transaction from Database of the Xref
                Transaction xrefTx = xRefDB.TransactionManager.StartTransaction();
                List<Polyline3d> allLinework = xRefDB.ListOfType<Polyline3d>(xrefTx);
                editor.WriteMessage($"\nNr. of 3D polies: {allLinework.Count}");
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
                        //if (type.IsNoE())
                        //{
                        //    editor.WriteMessage($"\nFejl: For xref lag {ent.Layer} mangler der enten" +
                        //        $"selve definitionen eller 'Type'!");
                        //    return;
                        //}
                        //Create 3d polyline if there's an intersection and
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
                List<Polyline3d> localPlines3d = localDb.ListOfType<Polyline3d>(tx);
                editor.WriteMessage($"\nNr. of local 3D polies: {localPlines3d.Count}");

                List<Entity> allLocalLinework = new List<Entity>(
                    localPlines3d.Count
                    );

                allLocalLinework.AddRange(localPlines3d.Cast<Entity>());
                #endregion

                #region Prepare variables
                //Load things
                Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;
                CogoPointCollection cogoPoints = civilDoc.CogoPoints;
                HashSet<CogoPoint> allNewlyCreatedPoints = new HashSet<CogoPoint>();
                #endregion

                #region Handle PointGroups
                bool pointGroupAlreadyExists = civilDoc.PointGroups.Contains(alignment.Name);

                PointGroup pg = null;

                if (pointGroupAlreadyExists)
                {
                    pg = tx.GetObject(civilDoc.PointGroups[alignment.Name], OpenMode.ForWrite) as PointGroup;
                }
                else
                {
                    oid pgId = civilDoc.PointGroups.Add(alignment.Name);

                    pg = pgId.GetObject(OpenMode.ForWrite) as PointGroup;
                }


                #endregion

                foreach (Entity ent in allLocalLinework)
                {
                    #region Read data parameters from csvs
                    //Read 'Type' value
                    string type = ReadStringParameterFromDataTable(ent.Layer, dtKrydsninger, "Type", 0);
                    if (type.IsNoE())
                    {
                        editor.WriteMessage($"\nFejl: For lag {ent.Layer} mangler der enten " +
                            $"selve definitionen eller 'Type'!");
                        return;
                    }

                    //Read depth value for type
                    double depth = 0;
                    if (!type.IsNoE())
                    {
                        depth = Utils.ReadDoubleParameterFromDataTable(type, dtDybde, "Dybde", 0);
                    }

                    //Read layer value for the object
                    string localLayerName = Utils.ReadStringParameterFromDataTable(
                                        ent.Layer, dtKrydsninger, "Layer", 0);

                    #region Populate description field
                    //Populate description field
                    //1. Read size record if it exists
                    MapValue sizeRecord = Utils.ReadRecordData(
                        tables, ent.ObjectId, "SizeTable", "Size");
                    int SizeTableSize = 0;
                    string sizeDescrPart = "";
                    if (sizeRecord != null)
                    {
                        SizeTableSize = sizeRecord.Int32Value;
                        sizeDescrPart = $"ø{SizeTableSize}";
                    }

                    //2. Read description from Krydsninger
                    string descrFromKrydsninger = ReadStringParameterFromDataTable(
                        ent.Layer, dtKrydsninger, "Description", 0);

                    //2.1 Read the formatting in the description field
                    List<(string ToReplace, string Data)> descrFormatList = null;
                    if (descrFromKrydsninger.IsNotNoE())
                        descrFormatList = FindDescriptionParts(descrFromKrydsninger);

                    //Finally: Compose description field
                    List<string> descrParts = new List<string>();
                    //1. Add custom size
                    if (SizeTableSize != 0) descrParts.Add(sizeDescrPart);
                    //2. Process and add parts from format bits in OD
                    if (descrFromKrydsninger.IsNotNoE())
                    {
                        //Interpolate description from Krydsninger with format setting, if they exist
                        if (descrFormatList != null && descrFormatList.Count > 0)
                        {
                            for (int i = 0; i < descrFormatList.Count; i++)
                            {
                                var tuple = descrFormatList[i];
                                string result = ReadDescriptionPartsFromOD(tables, ent, tuple.Data, dtKrydsninger);
                                descrFromKrydsninger = descrFromKrydsninger.Replace(tuple.ToReplace, result);
                            }
                        }

                        //Add the description field to parts
                        descrParts.Add(descrFromKrydsninger);
                    }

                    string description = "";
                    if (descrParts.Count == 1) description = descrParts[0];
                    else if (descrParts.Count > 1)
                        description = string.Join("; ", descrParts);

                    #endregion

                    //Source object (xref) handle
                    MapValue handleValue = ReadRecordData(
                                tables, ent.ObjectId, "IdRecord", "Handle");

                    string pName = "";

                    if (handleValue != null) pName = handleValue.StrValue;
                    else
                    {
                        pName = "Reading of Handle failed.";
                        editor.WriteMessage($"\nEntity on layer {ent.Layer} failed to read Handle!");
                    }

                    #endregion

                    #region Create points
                    using (Point3dCollection p3dcol = new Point3dCollection())
                    {
                        alignment.IntersectWith(ent, 0, plane, p3dcol, new IntPtr(0), new IntPtr(0));

                        int count = 1;
                        foreach (Point3d p3d in p3dcol)
                        {
                            oid pointId = cogoPoints.Add(p3d, true);
                            CogoPoint cogoPoint = pointId.Go<CogoPoint>(tx, OpenMode.ForWrite);

                            //Id of the new Poly3d if type == 3D
                            oid newPolyId;

                            #region Assign elevation based on 3D conditions
                            double zElevation = 0;
                            if (type != "3D")
                            {
                                var intPoint = surface.GetIntersectionPoint(p3d, new Vector3d(0, 0, 1));
                                zElevation = intPoint.Z;

                                //Subtract the depth (if invalid it is zero, so no modification will occur)
                                zElevation -= depth;

                                cogoPoint.Elevation = zElevation;
                            }
                            else if (type == "3D")
                            {
                                //Create vertical line to intersect the Ler line
                                using (Transaction txp3d = localDb.TransactionManager.StartTransaction())
                                {
                                    Point3dCollection newP3dCol = new Point3dCollection();
                                    //Intersection at 0
                                    newP3dCol.Add(p3d);
                                    //New point at very far away
                                    newP3dCol.Add(new Point3d(p3d.X, p3d.Y, 1000));

                                    Polyline3d newPoly = new Polyline3d(Poly3dType.SimplePoly, newP3dCol, false);

                                    //Open modelspace
                                    acBlkTbl = txp3d.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                                    acBlkTblRec = txp3d.GetObject(acBlkTbl[BlockTableRecord.ModelSpace],
                                                     OpenMode.ForWrite) as BlockTableRecord;

                                    acBlkTblRec.AppendEntity(newPoly);
                                    txp3d.AddNewlyCreatedDBObject(newPoly, true);
                                    newPolyId = newPoly.ObjectId;
                                    txp3d.Commit();
                                }

                                Polyline3d newPoly3d = newPolyId.Go<Polyline3d>(tx);
                                using (Point3dCollection p3dIntCol = new Point3dCollection())
                                {
                                    ent.IntersectWith(newPoly3d, 0, p3dIntCol, new IntPtr(0), new IntPtr(0));

                                    foreach (Point3d p3dInt in p3dIntCol)
                                    {
                                        //Assume only one intersection
                                        cogoPoint.Elevation = p3dInt.Z;
                                    }

                                    if (cogoPoint.Elevation == 0)
                                    {
                                        editor.WriteMessage($"\nFor type 3D entity {handleValue.StrValue}" +
                                            $" layer {ent.Layer}," +
                                            $" elevation is 0!");
                                    }
                                }
                                newPoly3d.UpgradeOpen();
                                newPoly3d.Erase(true);
                            }
                            #endregion

                            //Set the layer
                            #region Layer handling
                            bool localLayerExists = false;

                            if (!localLayerName.IsNoE() || localLayerName != null)
                            {
                                LayerTable lt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;
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
                                editor.WriteMessage($"\nLocal layer name for source layer {ent.Layer} does not" +
                                    $" exist in Krydsninger.csv!");
                            }

                            cogoPoint.Layer = localLayerName;
                            #endregion

                            string pointName = pName + "_" + count;

                            while (pNames.Contains(pointName))
                            {
                                count++;
                                pointName = pName + "_" + count;
                            }
                            pNames.Add(pointName);
                            cogoPoint.PointName = pointName;
                            cogoPoint.RawDescription = description;

                            #region Copy OD from polies to the new point
                            //Copy specific OD from cloned 3D polies to the new point

                            List<(string TableName, string RecordName)> odList =
                                new List<(string TableName, string RecordName)>();
                            odList.Add(("IdRecord", "Handle"));
                            TryCopySpecificOD(tables, ent, cogoPoint, odList);
                            #endregion

                            #region Create Diameter OD
                            odList.Clear();
                            odList.Add(("SizeTable", "Size"));
                            //Fetch diameter definitions if any
                            string diaDef = ReadStringParameterFromDataTable(ent.Layer,
                                dtKrydsninger, "Diameter", 0);
                            if (diaDef.IsNotNoE())
                            {
                                var list = FindDescriptionParts(diaDef);
                                //Be careful if FindDescriptionParts implementation changes
                                string[] parts = list[0].Item2.Split(':');
                                odList.Add((parts[0], parts[1]));
                            }

                            foreach (var item in odList)
                            {
                                MapValue originalValue = ReadRecordData(
                                    tables, ent.ObjectId, item.TableName, item.RecordName);

                                if (originalValue != null)
                                {
                                    if (DoesTableExist(tables, "CrossingData"))
                                    {
                                        if (DoesRecordExist(tables, cogoPoint.ObjectId, "Diameter"))
                                        {
                                            UpdateODRecord(tables, "CrossingData", "Diameter",
                                                cogoPoint.ObjectId, originalValue);
                                        }
                                        else
                                        {
                                            AddODRecord(tables, "CrossingData", "Diameter",
                                                cogoPoint.ObjectId, originalValue);
                                        }
                                    }
                                    else
                                    {
                                        if (CreateTable(tables, "CrossingData", "Table holding relevant crossing data",
                                            "Diameter", "Diameter of crossing pipe",
                                            Autodesk.Gis.Map.Constants.DataType.Integer))
                                        {
                                            AddODRecord(tables, "CrossingData", "Diameter",
                                                cogoPoint.ObjectId, originalValue);
                                        }
                                    }
                                }
                            }
                            #endregion

                            //Reference newly created cogoPoint to gathering collection
                            allNewlyCreatedPoints.Add(cogoPoint);
                        }
                    }
                    #endregion

                    #region Erase the cloned 3D polies
                    ent.UpgradeOpen();
                    ent.Erase(true);
                    #endregion
                }

                #region Assign newly created points to projection on a profile view
                #region Build query for PointGroup
                //Build query
                StandardPointGroupQuery spgq = new StandardPointGroupQuery();
                List<string> newPointNumbers = allNewlyCreatedPoints.Select(x => x.PointNumber.ToString()).ToList();
                string pointNumbersToInclude = string.Join(",", newPointNumbers.ToArray());
                spgq.IncludeNumbers = pointNumbersToInclude;
                pg.SetQuery(spgq);
                pg.Update();
                #endregion

                double elMax = pv.ElevationMax;
                double elMin = pv.ElevationMin;

                double tryGetMin = allNewlyCreatedPoints.Min(x => x.Elevation);

                if (tryGetMin != 0)
                {
                    pv.CheckOrOpenForWrite();
                    elMin = tryGetMin - 1;
                    pv.ElevationRangeMode = ElevationRangeType.UserSpecified;
                    pv.ElevationMin = elMin;
                }

                editor.SetImpliedSelection(allNewlyCreatedPoints.Select(x => x.ObjectId).ToArray());
                editor.Command("_AeccProjectObjectsToProf", pv.ObjectId);

                #endregion

                xrefTx.Commit();
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage("\n" + ex.Message);
                return;
            }
        }

        [CommandMethod("createmultipleprofileviews")]
        public void createmultipleprofileviews()
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
                    BlockTableRecord space = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                    BlockTable bt = tx.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;

                    List<Alignment> allAlignments = db.ListOfType<Alignment>(tx).OrderBy(x => x.Name).ToList();
                    HashSet<ProfileView> pvSetExisting = db.HashSetOfType<ProfileView>(tx);

                    #region Select and open XREF
                    PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions("\n Select a LER XREF : ");
                    promptEntityOptions1.SetRejectMessage("\n Not a XREF");
                    promptEntityOptions1.AddAllowedClass(typeof(Autodesk.AutoCAD.DatabaseServices.BlockReference), true);
                    PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    Autodesk.AutoCAD.DatabaseServices.ObjectId blkObjId = entity1.ObjectId;
                    BlockReference blkRef = tx.GetObject(blkObjId, OpenMode.ForRead, false)
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
                    #endregion

                    #region Read Csv Data for Layers and Depth

                    //Establish the pathnames to files
                    //Files should be placed in a specific folder on desktop
                    string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";
                    string pathDybde = "X:\\AutoCAD DRI - 01 Civil 3D\\Dybde.csv";

                    System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
                    System.Data.DataTable dtDybde = CsvReader.ReadCsvToDataTable(pathDybde, "Dybde");

                    #endregion

                    #region Delete existing points
                    PointGroupCollection pgs = civilDoc.PointGroups;

                    for (int i = 0; i < pgs.Count; i++)
                    {
                        PointGroup pg = tx.GetObject(pgs[i], OpenMode.ForRead) as PointGroup;
                        //If profile views already exist -- skip deleting of points
                        if (allAlignments.Any(x => x.Name == pg.Name) &&
                            !pvSetExisting.Any(x => x.Name.Contains(pg.Name + "_PV")))
                        {
                            pg.CheckOrOpenForWrite();
                            pg.Update();
                            uint[] numbers = pg.GetPointNumbers();

                            CogoPointCollection cpc = civilDoc.CogoPoints;

                            for (int j = 0; j < numbers.Length; j++)
                            {
                                uint number = numbers[j];

                                if (cpc.Contains(number))
                                {
                                    cpc.Remove(number);
                                }
                            }

                            StandardPointGroupQuery spgqEmpty = new StandardPointGroupQuery();
                            spgqEmpty.IncludeNumbers = "";
                            pg.SetQuery(spgqEmpty);

                            pg.Update();
                        }
                    }
                    #endregion

                    #region Create surface profiles and profile views

                    #region Select "surface"
                    //Get surface
                    PromptEntityOptions promptEntityOptions3 = new PromptEntityOptions("\n Select surface to get elevations: ");
                    promptEntityOptions3.SetRejectMessage("\n Not a surface");
                    promptEntityOptions3.AddAllowedClass(typeof(TinSurface), true);
                    promptEntityOptions3.AddAllowedClass(typeof(GridSurface), true);
                    PromptEntityResult entity3 = editor.GetEntity(promptEntityOptions3);
                    if (((PromptResult)entity3).Status != PromptStatus.OK) return;
                    oid surfaceObjId = entity3.ObjectId;
                    CivSurface surface = surfaceObjId.GetObject(OpenMode.ForRead, false) as CivSurface;
                    #endregion

                    #region Get terrain layer id

                    LayerTable lt = db.LayerTableId.GetObject(OpenMode.ForRead) as LayerTable;
                    string terrainLayerName = "0_TERRAIN_PROFILE";
                    oid terrainLayerId = oid.Null;
                    foreach (oid id in lt)
                    {
                        LayerTableRecord ltr = id.GetObject(OpenMode.ForRead) as LayerTableRecord;
                        if (ltr.Name == terrainLayerName) terrainLayerId = ltr.Id;
                    }
                    if (terrainLayerId == oid.Null)
                    {
                        editor.WriteMessage("Terrain layer missing!");
                        return;
                    }

                    #endregion

                    #region ProfileView styles ids
                    oid profileStyleId = civilDoc.Styles.ProfileStyles["Terræn"];
                    oid profileLabelSetStyleId = civilDoc.Styles.LabelSetStyles.ProfileLabelSetStyles["_No Labels"];

                    oid profileViewBandSetStyleId = civilDoc.Styles
                            .ProfileViewBandSetStyles["EG-FG Elevations and Stations"];
                    oid profileViewStyleId = civilDoc.Styles
                        .ProfileViewStyles["PROFILE VIEW L TO R NO SCALE"];
                    #endregion

                    //Used to keep track of point names
                    HashSet<string> pNames = new HashSet<string>();

                    int index = 1;

                    #region Select point
                    PromptPointOptions pPtOpts = new PromptPointOptions("");
                    // Prompt for the start point
                    pPtOpts.Message = "\nSelect location where to draw first profile view:";
                    PromptPointResult pPtRes = editor.GetPoint(pPtOpts);
                    Point3d selectedPoint = pPtRes.Value;
                    // Exit if the user presses ESC or cancels the command
                    if (pPtRes.Status != PromptStatus.OK) return;
                    #endregion

                    foreach (Alignment alignment in allAlignments)
                    {
                        #region Create surface profiles
                        //If ProfileView already exists -> continue
                        if (pvSetExisting.Any(x => x.Name == $"{alignment.Name}_PV")) continue;

                        oid surfaceProfileId = oid.Null;
                        string profileName = $"{alignment.Name}_surface_P";
                        bool noProfileExists = true;
                        ObjectIdCollection pIds = alignment.GetProfileIds();
                        foreach (oid pId in pIds)
                        {
                            Profile p = pId.Go<Profile>(tx);
                            if (p.Name == profileName)
                            {
                                noProfileExists = false;
                                surfaceProfileId = pId;
                            }
                        }

                        if (noProfileExists)
                        {
                            surfaceProfileId = Profile.CreateFromSurface(
                                                profileName, alignment.ObjectId, surfaceObjId,
                                                terrainLayerId, profileStyleId, profileLabelSetStyleId);
                        }
                        #endregion

                        #region Create profile view
                        #region Calculate point
                        Point3d insertionPoint = new Point3d(selectedPoint.X, selectedPoint.Y + index * -200, 0);
                        #endregion

                        //oid pvId = ProfileView.Create(alignment.ObjectId, insertionPoint,
                        //    $"{alignment.Name}_PV", profileViewBandSetStyleId, profileViewStyleId);

                        MultipleProfileViewsCreationOptions mpvco = new MultipleProfileViewsCreationOptions();
                        mpvco.DrawOrder = ProfileViewPlotType.ByRows;
                        mpvco.GapBetweenViewsInColumn = 100;
                        mpvco.GapBetweenViewsInRow = 100;
                        mpvco.LengthOfEachView = 200;
                        mpvco.MaxViewInRowOrColumn = 50;
                        mpvco.StartCorner = ProfileViewStartCornerType.LowerLeft;

                        //Naming format of created multiple PVs
                        //j = row, k = col
                        // {AlignmentName}_PV (jk)
                        ObjectIdCollection mPvIds = ProfileView.CreateMultiple(alignment.ObjectId, insertionPoint,
                            $"{alignment.Name}_PV", profileViewBandSetStyleId, profileViewStyleId, mpvco);

                        index++;
                        #endregion

                        #region Create ler data

                        //createlerdataloop(xRefDB, alignment, surface, pvId.Go<ProfileView>(tx),
                        //                  dtKrydsninger, dtDybde, ref pNames);
                        #endregion
                    }

                    #endregion

                }

                catch (System.Exception ex)
                {
                    throw new System.Exception(ex.Message);
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("createsurfaceprofiles")]
        public void createsurfaceprofiles()
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
                    List<Alignment> allAlignments = db.ListOfType<Alignment>(tx).OrderBy(x => x.Name).ToList();

                    #region Create surface profiles

                    #region Select "surface"
                    //Get surface
                    PromptEntityOptions promptEntityOptions3 = new PromptEntityOptions("\n Select surface to get elevations: ");
                    promptEntityOptions3.SetRejectMessage("\n Not a surface");
                    promptEntityOptions3.AddAllowedClass(typeof(TinSurface), true);
                    promptEntityOptions3.AddAllowedClass(typeof(GridSurface), true);
                    PromptEntityResult entity3 = editor.GetEntity(promptEntityOptions3);
                    if (((PromptResult)entity3).Status != PromptStatus.OK) return;
                    oid surfaceObjId = entity3.ObjectId;
                    CivSurface surface = surfaceObjId.GetObject(OpenMode.ForRead, false) as CivSurface;
                    #endregion

                    #region Get terrain layer id

                    LayerTable lt = db.LayerTableId.GetObject(OpenMode.ForRead) as LayerTable;
                    string terrainLayerName = "0_TERRAIN_PROFILE";
                    oid terrainLayerId = oid.Null;
                    foreach (oid id in lt)
                    {
                        LayerTableRecord ltr = id.GetObject(OpenMode.ForRead) as LayerTableRecord;
                        if (ltr.Name == terrainLayerName) terrainLayerId = ltr.Id;
                    }
                    if (terrainLayerId == oid.Null)
                    {
                        editor.WriteMessage("Terrain layer missing!");
                        return;
                    }

                    #endregion

                    oid profileStyleId = civilDoc.Styles.ProfileStyles["Terræn"];
                    oid profileLabelSetStyleId = civilDoc.Styles.LabelSetStyles.ProfileLabelSetStyles["_No Labels"];

                    foreach (Alignment alignment in allAlignments)
                    {
                        oid surfaceProfileId = oid.Null;
                        string profileName = $"{alignment.Name}_surface_P";
                        bool noProfileExists = true;
                        ObjectIdCollection pIds = alignment.GetProfileIds();
                        foreach (oid pId in pIds)
                        {
                            Profile p = pId.Go<Profile>(tx);
                            if (p.Name == profileName)
                            {
                                noProfileExists = false;
                                surfaceProfileId = pId;
                            }
                        }
                        if (noProfileExists)
                        {
                            surfaceProfileId = Profile.CreateFromSurface(
                                                profileName, alignment.ObjectId, surfaceObjId,
                                                terrainLayerId, profileStyleId, profileLabelSetStyleId);
                        }
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    throw new System.Exception(ex.Message);
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("createlerdata")]
        public void createlerdata()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                string etapeName = GetEtapeName(editor);

                editor.WriteMessage("\n" + GetPathToDataFiles(etapeName, "Ler"));
                editor.WriteMessage("\n" + GetPathToDataFiles(etapeName, "Surface"));

                #region Read surface from file
                // open the xref database
                Database xRefSurfaceDB = new Database(false, true);
                xRefSurfaceDB.ReadDwgFile(GetPathToDataFiles(etapeName, "Surface"),
                    System.IO.FileShare.Read, false, string.Empty);
                Transaction xRefSurfaceTx = xRefSurfaceDB.TransactionManager.StartTransaction();

                CivSurface surface = null;
                try
                {
                    surface = xRefSurfaceDB
                        .HashSetOfType<TinSurface>(xRefSurfaceTx)
                        .FirstOrDefault() as CivSurface;
                }
                catch (System.Exception)
                {
                    xRefSurfaceTx.Abort();
                    throw;
                }

                if (surface == null)
                {
                    editor.WriteMessage("\nSurface could not be loaded from the xref!");
                    xRefSurfaceTx.Commit();
                    return;
                }
                #endregion

                #region Load linework from LER Xref

                // open the LER dwg database
                Database xRefLerDB = new Database(false, true);

                xRefLerDB.ReadDwgFile(GetPathToDataFiles(etapeName, "Ler"),
                    System.IO.FileShare.Read, false, string.Empty);
                Transaction xRefLerTx = xRefLerDB.TransactionManager.StartTransaction();
                List<Polyline3d> allLinework = xRefLerDB.ListOfType<Polyline3d>(xRefLerTx);
                editor.WriteMessage($"\nNr. of 3D polies: {allLinework.Count}");
                #endregion

                try
                {
                    BlockTableRecord space = (BlockTableRecord)tx.GetObject(localDb.CurrentSpaceId, OpenMode.ForWrite);
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForWrite) as BlockTable;

                    List<Alignment> allAlignments = localDb.ListOfType<Alignment>(tx).OrderBy(x => x.Name).ToList();
                    HashSet<ProfileView> pvSetExisting = localDb.HashSetOfType<ProfileView>(tx);

                    #region Read Csv Data for Layers and Depth

                    //Establish the pathnames to files
                    //Files should be placed in a specific folder on desktop
                    string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";
                    string pathDybde = "X:\\AutoCAD DRI - 01 Civil 3D\\Dybde.csv";

                    System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
                    System.Data.DataTable dtDybde = CsvReader.ReadCsvToDataTable(pathDybde, "Dybde");

                    #endregion

                    #region Delete existing points
                    PointGroupCollection pgs = civilDoc.PointGroups;

                    for (int i = 0; i < pgs.Count; i++)
                    {
                        PointGroup pg = tx.GetObject(pgs[i], OpenMode.ForRead) as PointGroup;
                        if (allAlignments.Any(x => x.Name == pg.Name))
                        {
                            pg.CheckOrOpenForWrite();
                            pg.Update();
                            uint[] numbers = pg.GetPointNumbers();

                            CogoPointCollection cpc = civilDoc.CogoPoints;

                            for (int j = 0; j < numbers.Length; j++)
                            {
                                uint number = numbers[j];

                                if (cpc.Contains(number))
                                {
                                    cpc.Remove(number);
                                }
                            }

                            StandardPointGroupQuery spgqEmpty = new StandardPointGroupQuery();
                            spgqEmpty.IncludeNumbers = "";
                            pg.SetQuery(spgqEmpty);

                            pg.Update();
                        }
                    }
                    #endregion

                    #region Name handling of point names
                    //Used to keep track of point names
                    HashSet<string> pNames = new HashSet<string>();

                    int index = 1;
                    #endregion

                    #region CogoPoint style and label reference

                    oid cogoPointStyle = civilDoc.Styles.PointStyles["LER KRYDS"];


                    #endregion

                    foreach (Alignment alignment in allAlignments)
                    {
                        #region Create ler data
                        #region ModelSpaces
                        //oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(xRefDB);
                        oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);
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
                        xRefLerDB.WblockCloneObjects(
                            sourceIds, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);

                        editor.WriteMessage($"\nTotal {intersections} intersections detected.");
                        #endregion

                        #region Load linework from local db
                        List<Polyline3d> localPlines3d = localDb.ListOfType<Polyline3d>(tx);
                        editor.WriteMessage($"\nNr. of local 3D polies: {localPlines3d.Count}");

                        List<Entity> allLocalLinework = new List<Entity>(
                            localPlines3d.Count
                            );

                        allLocalLinework.AddRange(localPlines3d.Cast<Entity>());
                        #endregion

                        #region Prepare variables
                        //Load things
                        Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;
                        CogoPointCollection cogoPoints = civilDoc.CogoPoints;
                        HashSet<CogoPoint> allNewlyCreatedPoints = new HashSet<CogoPoint>();
                        #endregion

                        #region Handle PointGroups
                        bool pointGroupAlreadyExists = civilDoc.PointGroups.Contains(alignment.Name);

                        PointGroup pg = null;

                        if (pointGroupAlreadyExists)
                        {
                            pg = tx.GetObject(civilDoc.PointGroups[alignment.Name], OpenMode.ForWrite) as PointGroup;
                        }
                        else
                        {
                            oid pgId = civilDoc.PointGroups.Add(alignment.Name);

                            pg = pgId.GetObject(OpenMode.ForWrite) as PointGroup;
                        }
                        #endregion

                        #region Create Points, assign elevation, layer and OD
                        foreach (Entity ent in allLocalLinework)
                        {
                            #region Read data parameters from csvs
                            //Read 'Type' value
                            string type = ReadStringParameterFromDataTable(ent.Layer, dtKrydsninger, "Type", 0);
                            if (type.IsNoE())
                            {
                                editor.WriteMessage($"\nFejl: For lag {ent.Layer} mangler der enten " +
                                    $"selve definitionen eller 'Type'!");
                                return;
                            }

                            //Read depth value for type
                            double depth = 0;
                            if (!type.IsNoE())
                            {
                                depth = Utils.ReadDoubleParameterFromDataTable(type, dtDybde, "Dybde", 0);
                            }

                            //Read layer value for the object
                            string localLayerName = Utils.ReadStringParameterFromDataTable(
                                                ent.Layer, dtKrydsninger, "Layer", 0);

                            #region Populate description field
                            //Populate description field
                            //1. Read size record if it exists
                            MapValue sizeRecord = Utils.ReadRecordData(
                                tables, ent.ObjectId, "SizeTable", "Size");
                            int SizeTableSize = 0;
                            string sizeDescrPart = "";
                            if (sizeRecord != null)
                            {
                                SizeTableSize = sizeRecord.Int32Value;
                                sizeDescrPart = $"ø{SizeTableSize}";
                            }

                            //2. Read description from Krydsninger
                            string descrFromKrydsninger = ReadStringParameterFromDataTable(
                                ent.Layer, dtKrydsninger, "Description", 0);

                            //2.1 Read the formatting in the description field
                            List<(string ToReplace, string Data)> descrFormatList = null;
                            if (descrFromKrydsninger.IsNotNoE())
                                descrFormatList = FindDescriptionParts(descrFromKrydsninger);

                            //Finally: Compose description field
                            List<string> descrParts = new List<string>();
                            //1. Add custom size
                            if (SizeTableSize != 0) descrParts.Add(sizeDescrPart);
                            //2. Process and add parts from format bits in OD
                            if (descrFromKrydsninger.IsNotNoE())
                            {
                                //Interpolate description from Krydsninger with format setting, if they exist
                                if (descrFormatList != null && descrFormatList.Count > 0)
                                {
                                    for (int i = 0; i < descrFormatList.Count; i++)
                                    {
                                        var tuple = descrFormatList[i];
                                        string result = ReadDescriptionPartsFromOD(tables, ent, tuple.Data, dtKrydsninger);
                                        descrFromKrydsninger = descrFromKrydsninger.Replace(tuple.ToReplace, result);
                                    }
                                }

                                //Add the description field to parts
                                descrParts.Add(descrFromKrydsninger);
                            }

                            string description = "";
                            if (descrParts.Count == 1) description = descrParts[0];
                            else if (descrParts.Count > 1)
                                description = string.Join("; ", descrParts);

                            #endregion

                            //Source object (xref) handle
                            MapValue handleValue = ReadRecordData(
                                        tables, ent.ObjectId, "IdRecord", "Handle");

                            string pName = "";

                            if (handleValue != null) pName = handleValue.StrValue;
                            else
                            {
                                pName = "Reading of Handle failed.";
                                editor.WriteMessage($"\nEntity on layer {ent.Layer} failed to read Handle!");
                            }

                            #endregion

                            #region Create points
                            using (Point3dCollection p3dcol = new Point3dCollection())
                            {
                                alignment.IntersectWith(ent, 0, plane, p3dcol, new IntPtr(0), new IntPtr(0));

                                int count = 1;
                                foreach (Point3d p3d in p3dcol)
                                {
                                    oid pointId = cogoPoints.Add(p3d, true);
                                    CogoPoint cogoPoint = pointId.Go<CogoPoint>(tx, OpenMode.ForWrite);

                                    //Id of the new Poly3d if type == 3D
                                    oid newPolyId;

                                    #region Assign elevation based on 3D conditions
                                    double zElevation = 0;
                                    if (type != "3D")
                                    {
                                        var intPoint = surface.GetIntersectionPoint(p3d, new Vector3d(0, 0, 1));
                                        zElevation = intPoint.Z;

                                        //Subtract the depth (if invalid it is zero, so no modification will occur)
                                        zElevation -= depth;

                                        cogoPoint.Elevation = zElevation;
                                    }
                                    else if (type == "3D")
                                    {
                                        //Create vertical line to intersect the Ler line
                                        using (Transaction txp3d = localDb.TransactionManager.StartTransaction())
                                        {
                                            Point3dCollection newP3dCol = new Point3dCollection();
                                            //Intersection at 0
                                            newP3dCol.Add(p3d);
                                            //New point at very far away
                                            newP3dCol.Add(new Point3d(p3d.X, p3d.Y, 1000));

                                            Polyline3d newPoly = new Polyline3d(Poly3dType.SimplePoly, newP3dCol, false);

                                            //Open modelspace
                                            acBlkTbl = txp3d.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                                            acBlkTblRec = txp3d.GetObject(acBlkTbl[BlockTableRecord.ModelSpace],
                                                             OpenMode.ForWrite) as BlockTableRecord;

                                            acBlkTblRec.AppendEntity(newPoly);
                                            txp3d.AddNewlyCreatedDBObject(newPoly, true);
                                            newPolyId = newPoly.ObjectId;
                                            txp3d.Commit();
                                        }

                                        Polyline3d newPoly3d = newPolyId.Go<Polyline3d>(tx);
                                        using (Point3dCollection p3dIntCol = new Point3dCollection())
                                        {
                                            ent.IntersectWith(newPoly3d, 0, p3dIntCol, new IntPtr(0), new IntPtr(0));

                                            foreach (Point3d p3dInt in p3dIntCol)
                                            {
                                                //Assume only one intersection
                                                cogoPoint.Elevation = p3dInt.Z;
                                            }

                                            if (cogoPoint.Elevation == 0)
                                            {
                                                editor.WriteMessage($"\nFor type 3D entity {handleValue.StrValue}" +
                                                    $" layer {ent.Layer}," +
                                                    $" elevation is 0!");
                                            }
                                        }
                                        newPoly3d.UpgradeOpen();
                                        newPoly3d.Erase(true);
                                    }
                                    #endregion

                                    //Set the layer
                                    #region Layer handling
                                    bool localLayerExists = false;

                                    if (!localLayerName.IsNoE() || localLayerName != null)
                                    {
                                        LayerTable lt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;
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
                                        editor.WriteMessage($"\nLocal layer name for source layer {ent.Layer} does not" +
                                            $" exist in Krydsninger.csv!");
                                    }

                                    cogoPoint.Layer = localLayerName;
                                    #endregion

                                    #region Point names, avoids duplicate names
                                    string pointName = pName + "_" + count;

                                    while (pNames.Contains(pointName))
                                    {
                                        count++;
                                        pointName = pName + "_" + count;
                                    }
                                    pNames.Add(pointName);
                                    cogoPoint.PointName = pointName;
                                    cogoPoint.RawDescription = description;
                                    cogoPoint.StyleId = cogoPointStyle;
                                    #endregion

                                    #region Copy OD from polies to the new point
                                    //Copy specific OD from cloned 3D polies to the new point

                                    List<(string TableName, string RecordName)> odList =
                                        new List<(string TableName, string RecordName)>();
                                    odList.Add(("IdRecord", "Handle"));
                                    TryCopySpecificOD(tables, ent, cogoPoint, odList);
                                    #endregion

                                    #region Create Diameter OD
                                    odList.Clear();
                                    odList.Add(("SizeTable", "Size"));
                                    //Fetch diameter definitions if any
                                    string diaDef = ReadStringParameterFromDataTable(ent.Layer,
                                        dtKrydsninger, "Diameter", 0);
                                    if (diaDef.IsNotNoE())
                                    {
                                        var list = FindDescriptionParts(diaDef);
                                        //Be careful if FindDescriptionParts implementation changes
                                        string[] parts = list[0].Item2.Split(':');
                                        odList.Add((parts[0], parts[1]));
                                    }

                                    foreach (var item in odList)
                                    {
                                        MapValue originalValue = ReadRecordData(
                                            tables, ent.ObjectId, item.TableName, item.RecordName);

                                        if (originalValue != null)
                                        {
                                            if (DoesTableExist(tables, "CrossingData"))
                                            {
                                                if (DoesRecordExist(tables, cogoPoint.ObjectId, "Diameter"))
                                                {
                                                    UpdateODRecord(tables, "CrossingData", "Diameter",
                                                        cogoPoint.ObjectId, originalValue);
                                                }
                                                else
                                                {
                                                    AddODRecord(tables, "CrossingData", "Diameter",
                                                        cogoPoint.ObjectId, originalValue);
                                                }
                                            }
                                            else
                                            {
                                                if (CreateTable(tables, "CrossingData", "Table holding relevant crossing data",
                                                    "Diameter", "Diameter of crossing pipe",
                                                    Autodesk.Gis.Map.Constants.DataType.Integer))
                                                {
                                                    AddODRecord(tables, "CrossingData", "Diameter",
                                                        cogoPoint.ObjectId, originalValue);
                                                }
                                            }
                                        }
                                    }
                                    #endregion

                                    //Reference newly created cogoPoint to gathering collection
                                    allNewlyCreatedPoints.Add(cogoPoint);
                                }
                            }
                            #endregion

                            #region Erase the cloned 3D polies
                            ent.UpgradeOpen();
                            ent.Erase(true);
                            #endregion
                        }
                        #endregion

                        #region Assign newly created points to projection on a profile view
                        #region Build query for PointGroup
                        //Build query
                        StandardPointGroupQuery spgq = new StandardPointGroupQuery();
                        List<string> newPointNumbers = allNewlyCreatedPoints.Select(x => x.PointNumber.ToString()).ToList();
                        string pointNumbersToInclude = string.Join(",", newPointNumbers.ToArray());
                        spgq.IncludeNumbers = pointNumbersToInclude;
                        pg.SetQuery(spgq);
                        pg.Update();
                        #endregion

                        #region Manage PVs
                        ObjectIdCollection pIds = alignment.GetProfileIds();
                        Profile p = null;
                        foreach (oid Oid in pIds)
                        {
                            Profile pt = Oid.Go<Profile>(tx);
                            if (pt.Name == $"{alignment.Name}_surface_P") p = pt;
                        }
                        if (p == null)
                        {
                            editor.WriteMessage($"\nNo profile named {alignment.Name}_surface_P found!");
                            return;
                        }
                        else editor.WriteMessage($"\nProfile {p.Name} found!");

                        ProfileView[] pvs = localDb.ListOfType<ProfileView>(tx).ToArray();

                        #region Find and set max elevation for PVs
                        foreach (ProfileView pv in pvs)
                        {
                            double pvStStart = pv.StationStart;
                            double pvStEnd = pv.StationEnd;

                            int nrOfIntervals = 100;
                            double delta = (pvStEnd - pvStStart) / nrOfIntervals;
                            HashSet<double> elevs = new HashSet<double>();

                            for (int i = 0; i < nrOfIntervals + 1; i++)
                            {
                                double testEl = 0;
                                try
                                {
                                    testEl = p.ElevationAt(pvStStart + delta * i);
                                }
                                catch (System.Exception)
                                {
                                    editor.WriteMessage($"\n{pvStStart + delta * i} threw an exception! " +
                                        $"PV: {pv.StationStart}-{pv.StationEnd}.");
                                    continue;
                                }
                                elevs.Add(testEl);
                                //editor.WriteMessage($"\nElevation at {i} is {testEl}.");
                            }

                            double maxEl = elevs.Max();
                            editor.WriteMessage($"\nMax elevation of {pv.Name} is {maxEl}.");

                            pv.CheckOrOpenForWrite();
                            pv.ElevationRangeMode = ElevationRangeType.UserSpecified;

                            pv.ElevationMax = Math.Ceiling(maxEl);
                        }
                        #endregion

                        //Create StationPoints and assign PV number to them
                        HashSet<StationPoint> staPoints = new HashSet<StationPoint>(allNewlyCreatedPoints.Count);
                        foreach (CogoPoint cp in allNewlyCreatedPoints)
                        {
                            StationPoint sp = new StationPoint(cp, alignment);

                            int counter = 0;
                            foreach (ProfileView pv in pvs)
                            {
                                counter++;
                                if (sp.Station >= pv.StationStart &&
                                    sp.Station <= pv.StationEnd)
                                {
                                    sp.ProfileViewNumber = counter;
                                    break;
                                }
                            }
                            staPoints.Add(sp);
                        }

                        //Set minimum height
                        for (int i = 0; i < pvs.Length; i++)
                        {
                            int idx = i + 1;
                            double elMin = staPoints.Where(x => x.ProfileViewNumber == idx)
                                                    .Select(x => x.CogoPoint.Elevation)
                                                    .Min();
                            pvs[i].CheckOrOpenForWrite();
                            pvs[i].ElevationRangeMode = ElevationRangeType.UserSpecified;
                            pvs[i].ElevationMin = Math.Floor(elMin) - 1;

                            //Project the points
                            editor.SetImpliedSelection(staPoints
                                .Where(x => x.ProfileViewNumber == idx)
                                .Select(x => x.CogoPoint.ObjectId)
                                .ToArray());
                            editor.Command("_AeccProjectObjectsToProf", pvs[i].ObjectId);
                        }
                        #endregion

                        #endregion

                        #endregion
                    }

                    xRefLerTx.Commit();
                    xRefSurfaceTx.Commit();
                }

                catch (System.Exception ex)
                {
                    xRefLerTx.Abort();
                    xRefSurfaceTx.Abort();
                    throw new System.Exception(ex.Message);
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("testing")]
        public void testing()
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
                    #region Test PV start and end station

                    //HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);
                    //foreach (Alignment al in als)
                    //{
                    //    ObjectIdCollection pIds = al.GetProfileIds();
                    //    Profile p = null;
                    //    foreach (oid Oid in pIds)
                    //    {
                    //        Profile pt = Oid.Go<Profile>(tx);
                    //        if (pt.Name == $"{al.Name}_surface_P") p = pt;
                    //    }
                    //    if (p == null) return;
                    //    else editor.WriteMessage($"\nProfile {p.Name} found!");

                    //    ProfileView[] pvs = localDb.ListOfType<ProfileView>(tx).ToArray();

                    //    foreach (ProfileView pv in pvs)
                    //    {
                    //        editor.WriteMessage($"\nName of pv: {pv.Name}.");

                    #region Test finding of max elevation
                    //double pvStStart = pv.StationStart;
                    //double pvStEnd = pv.StationEnd;

                    //int nrOfIntervals = 100;
                    //double delta = (pvStEnd - pvStStart) / nrOfIntervals;
                    //HashSet<double> elevs = new HashSet<double>();

                    //for (int i = 0; i < nrOfIntervals + 1; i++)
                    //{
                    //    double testEl = p.ElevationAt(pvStStart + delta * i);
                    //    elevs.Add(testEl);
                    //    editor.WriteMessage($"\nElevation at {i} is {testEl}.");
                    //}

                    //double maxEl = elevs.Max();
                    //editor.WriteMessage($"\nMax elevation of {pv.Name} is {maxEl}.");

                    //pv.CheckOrOpenForWrite();
                    //pv.ElevationRangeMode = ElevationRangeType.UserSpecified;

                    //pv.ElevationMax = Math.Ceiling(maxEl); 
                    #endregion
                    //}
                    //}



                    #endregion

                    #region Test station and offset alignment
                    //#region Select point
                    //PromptPointOptions pPtOpts = new PromptPointOptions("");
                    //// Prompt for the start point
                    //pPtOpts.Message = "\nEnter location to test the alignment:";
                    //PromptPointResult pPtRes = editor.GetPoint(pPtOpts);
                    //Point3d selectedPoint = pPtRes.Value;
                    //// Exit if the user presses ESC or cancels the command
                    //if (pPtRes.Status != PromptStatus.OK) return;
                    //#endregion

                    //HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);

                    //foreach (Alignment al in als)
                    //{
                    //    double station = 0;
                    //    double offset = 0;

                    //    al.StationOffset(selectedPoint.X, selectedPoint.Y, ref station, ref offset);

                    //    editor.WriteMessage($"\nReported: ST: {station}, OS: {offset}.");
                    //} 
                    #endregion

                    #region Test assigning labels and point styles
                    //oid cogoPointStyle = civilDoc.Styles.PointStyles["LER KRYDS"];
                    //CogoPointCollection cpc = civilDoc.CogoPoints;

                    //foreach (oid cpOid in cpc)
                    //{
                    //    CogoPoint cp = cpOid.Go<CogoPoint>(tx, OpenMode.ForWrite);
                    //    cp.StyleId = cogoPointStyle;
                    //}

                    #endregion
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }
    }
}