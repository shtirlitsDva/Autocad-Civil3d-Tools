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
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;
using Autodesk.Gis.Map.Utilities;
using Autodesk.Aec.PropertyData;
using Autodesk.Aec.PropertyData.DatabaseServices;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Data;
using MoreLinq;
using GroupByCluster;
using IntersectUtilities.UtilsCommon;

using static IntersectUtilities.Enums;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.Utils;
using static IntersectUtilities.PipeSchedule;

using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.UtilsCommon.UtilsODData;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = Autodesk.Civil.DatabaseServices.Label;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;

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

                    Oid fId = label.FeatureId;

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
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("selbylabel")]
        public void selectcogopointbyprojectionlabel()
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

                    Oid fId = label.FeatureId;
                    editor.SetImpliedSelection(new[] { fId });
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
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
                    #region Select XREF -- OBSOLETE
                    //PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions("\n Select a LER XREF : ");
                    //promptEntityOptions1.SetRejectMessage("\n Not a XREF");
                    //promptEntityOptions1.AddAllowedClass(typeof(Autodesk.AutoCAD.DatabaseServices.BlockReference), true);
                    //PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    //if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    //Autodesk.AutoCAD.DatabaseServices.ObjectId blkObjId = entity1.ObjectId;
                    //Autodesk.AutoCAD.DatabaseServices.BlockReference blkRef
                    //    = tx.GetObject(blkObjId, OpenMode.ForRead, false)
                    //    as Autodesk.AutoCAD.DatabaseServices.BlockReference;
                    #endregion

                    #region Open XREF and tx -- OBSOLETE
                    //// open the block definition?
                    //BlockTableRecord blockDef = tx.GetObject(blkRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                    //// is not from external reference, exit
                    //if (!blockDef.IsFromExternalReference) return;

                    //// open the xref database
                    //Database xRefDB = new Database(false, true);
                    //editor.WriteMessage($"\nPathName of the blockDef -> {blockDef.PathName}");

                    ////Relative path handling
                    ////I
                    //string curPathName = blockDef.PathName;
                    //bool isFullPath = IsFullPath(curPathName);
                    //if (isFullPath == false)
                    //{
                    //    string sourcePath = Path.GetDirectoryName(doc.Name);
                    //    editor.WriteMessage($"\nSourcePath -> {sourcePath}");
                    //    curPathName = GetAbsolutePath(sourcePath, blockDef.PathName);
                    //    editor.WriteMessage($"\nTargetPath -> {curPathName}");
                    //}

                    //xRefDB.ReadDwgFile(curPathName, System.IO.FileShare.Read, false, string.Empty);

                    ////Transaction from Database of the Xref
                    //Transaction xrefTx = xRefDB.TransactionManager.StartTransaction();
                    #endregion

                    #region Gather layer names
                    HashSet<Line> lines = db.HashSetOfType<Line>(tx);
                    HashSet<Spline> splines = db.HashSetOfType<Spline>(tx);
                    HashSet<Polyline> plines = db.HashSetOfType<Polyline>(tx);
                    HashSet<Polyline3d> plines3d = db.HashSetOfType<Polyline3d>(tx);
                    HashSet<Arc> arcs = db.HashSetOfType<Arc>(tx);
                    editor.WriteMessage($"\nNr. of lines: {lines.Count}");
                    editor.WriteMessage($"\nNr. of splines: {splines.Count}");
                    editor.WriteMessage($"\nNr. of plines: {plines.Count}");
                    editor.WriteMessage($"\nNr. of plines3d: {plines3d.Count}");
                    editor.WriteMessage($"\nNr. of arcs: {arcs.Count}");

                    HashSet<string> layNames = new HashSet<string>();

                    //Local function to avoid duplicate code
                    HashSet<string> LocalListNames<T>(HashSet<string> list, HashSet<T> ents)
                    {
                        foreach (Entity ent in ents.Cast<Entity>())
                        {
                            list.Add(ent.Layer);
                        }
                        return list;
                    }

                    layNames = LocalListNames(layNames, lines);
                    layNames = LocalListNames(layNames, splines);
                    layNames = LocalListNames(layNames, plines);
                    layNames = LocalListNames(layNames, plines3d);
                    layNames = LocalListNames(layNames, arcs);

                    #endregion

                    #region Read Csv Data for Layers and Depth

                    //Establish the pathnames to files
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
                    #region Read Csv Data for Layers and Depth

                    //Establish the pathnames to files
                    //Files should be placed in a specific folder on desktop
                    string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";
                    string pathDybde = "X:\\AutoCAD DRI - 01 Civil 3D\\Dybde.csv";

                    System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
                    System.Data.DataTable dtDybde = CsvReader.ReadCsvToDataTable(pathDybde, "Dybde");

                    #endregion

                    #region Choose working drawing
                    const string kwd1 = "Ler";
                    const string kwd2 = "Alignments";

                    PromptKeywordOptions pKeyOpts = new PromptKeywordOptions("");
                    pKeyOpts.Message = "\nChoose working drawing: ";
                    pKeyOpts.Keywords.Add(kwd1);
                    pKeyOpts.Keywords.Add(kwd2);
                    pKeyOpts.AllowNone = true;
                    pKeyOpts.Keywords.Default = kwd1;
                    PromptResult pKeyRes = editor.GetKeywords(pKeyOpts);
                    string workingDrawing = pKeyRes.StringResult;
                    #endregion

                    DataReferencesOptions dro = new DataReferencesOptions();
                    string projectName = dro.ProjectName;
                    string etapeName = dro.EtapeName;

                    HashSet<Entity> allLinework = new HashSet<Entity>();
                    HashSet<Alignment> alignments = new HashSet<Alignment>();

                    if (workingDrawing == kwd2)
                    {
                        #region Load linework from LER Xref
                        editor.WriteMessage("\n" + GetPathToDataFiles(projectName, etapeName, "Ler"));

                        // open the LER dwg database
                        Database xRefLerDB = new Database(false, true);

                        xRefLerDB.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Ler"),
                            System.IO.FileShare.Read, false, string.Empty);
                        Transaction xRefLerTx = xRefLerDB.TransactionManager.StartTransaction();

                        HashSet<Line> lines = xRefLerDB.HashSetOfType<Line>(xRefLerTx);
                        HashSet<Spline> splines = xRefLerDB.HashSetOfType<Spline>(xRefLerTx);
                        HashSet<Polyline> plines = xRefLerDB.HashSetOfType<Polyline>(xRefLerTx);
                        HashSet<Polyline3d> plines3d = xRefLerDB.HashSetOfType<Polyline3d>(xRefLerTx);
                        HashSet<Arc> arcs = xRefLerDB.HashSetOfType<Arc>(xRefLerTx);
                        editor.WriteMessage($"\nNr. of lines: {lines.Count}");
                        editor.WriteMessage($"\nNr. of splines: {splines.Count}");
                        editor.WriteMessage($"\nNr. of plines: {plines.Count}");
                        editor.WriteMessage($"\nNr. of plines3d: {plines3d.Count}");
                        editor.WriteMessage($"\nNr. of arcs: {arcs.Count}");

                        allLinework.UnionWith(lines.Cast<Entity>().ToHashSet());
                        allLinework.UnionWith(splines.Cast<Entity>().ToHashSet());
                        allLinework.UnionWith(plines.Cast<Entity>().ToHashSet());
                        allLinework.UnionWith(plines3d.Cast<Entity>().ToHashSet());
                        allLinework.UnionWith(arcs.Cast<Entity>().ToHashSet());

                        allLinework = allLinework
                            .Where(x => ReadStringParameterFromDataTable(x.Layer, dtKrydsninger, "Type", 0) != "IGNORE")
                            .ToHashSet();

                        alignments = db.HashSetOfType<Alignment>(tx);

                        try
                        {
                            Analyze();
                        }
                        catch (System.Exception e)
                        {
                            xRefLerTx.Abort();
                            xRefLerTx.Dispose();
                            xRefLerDB.Dispose();
                            tx.Abort();
                            editor.WriteMessage($"\n{e.Message}");
                            return;
                        }

                        xRefLerTx.Abort();
                        xRefLerTx.Dispose();
                        xRefLerDB.Dispose();
                        #endregion
                    }
                    else if (workingDrawing == kwd1)
                    {
                        #region Load alignments from alignments Xref
                        editor.WriteMessage("\n" + GetPathToDataFiles(projectName, etapeName, "Alignments"));

                        // open the LER dwg database
                        Database xRefAlsDB = new Database(false, true);

                        xRefAlsDB.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Alignments"),
                            System.IO.FileShare.Read, false, string.Empty);
                        Transaction xRefAlsTx = xRefAlsDB.TransactionManager.StartTransaction();

                        alignments = xRefAlsDB.HashSetOfType<Alignment>(xRefAlsTx);

                        HashSet<Line> lines = db.HashSetOfType<Line>(tx);
                        HashSet<Spline> splines = db.HashSetOfType<Spline>(tx);
                        HashSet<Polyline> plines = db.HashSetOfType<Polyline>(tx);
                        HashSet<Polyline3d> plines3d = db.HashSetOfType<Polyline3d>(tx);
                        HashSet<Arc> arcs = db.HashSetOfType<Arc>(tx);
                        editor.WriteMessage($"\nNr. of lines: {lines.Count}");
                        editor.WriteMessage($"\nNr. of splines: {splines.Count}");
                        editor.WriteMessage($"\nNr. of plines: {plines.Count}");
                        editor.WriteMessage($"\nNr. of plines3d: {plines3d.Count}");
                        editor.WriteMessage($"\nNr. of arcs: {arcs.Count}");

                        allLinework.UnionWith(lines.Cast<Entity>().ToHashSet());
                        allLinework.UnionWith(splines.Cast<Entity>().ToHashSet());
                        allLinework.UnionWith(plines.Cast<Entity>().ToHashSet());
                        allLinework.UnionWith(plines3d.Cast<Entity>().ToHashSet());
                        allLinework.UnionWith(arcs.Cast<Entity>().ToHashSet());

                        allLinework = allLinework
                            .Where(x => ReadStringParameterFromDataTable(x.Layer, dtKrydsninger, "Type", 0) != "IGNORE")
                            .ToHashSet();

                        try
                        {
                            Analyze();
                        }
                        catch (System.Exception e)
                        {
                            xRefAlsTx.Abort();
                            xRefAlsTx.Dispose();
                            xRefAlsDB.Dispose();
                            tx.Abort();
                            editor.WriteMessage($"\n{e.Message}");
                            return;
                        }

                        xRefAlsTx.Abort();
                        xRefAlsTx.Dispose();
                        xRefAlsDB.Dispose();
                        #endregion
                    }

                    void Analyze()
                    {

                        HashSet<string> layNames = new HashSet<string>();
                        System.Windows.Forms.Application.DoEvents();

                        foreach (Alignment al in alignments)
                        {
                            editor.WriteMessage($"\n++++++++ Indlæser alignment {al.Name}. ++++++++");
                            System.Windows.Forms.Application.DoEvents();

                            HashSet<Entity> entities = FilterForCrossingEntities(allLinework, al);

                            if (entities.Count == 0)
                                editor.WriteMessage($"\nNo crossing entities found for alignment {al.Name}!");

                            //Local function to avoid duplicate code
                            HashSet<string> LocalListNames<T>(HashSet<string> list, HashSet<T> ents)
                            {
                                foreach (Entity ent in ents.Cast<Entity>())
                                {
                                    list.Add(ent.Layer);
                                }
                                return list;
                            }

                            layNames.UnionWith(LocalListNames(layNames, entities));
                        }

                        layNames = layNames.OrderBy(x => x).ToHashSet();

                        editor.WriteMessage($"\n++++++++ KONTROL ++++++++");
                        System.Windows.Forms.Application.DoEvents();
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
                                    editor.WriteMessage($"\nKontrollerer lag {name}: type {typeInFile}.");

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
                }
                catch (System.Exception e)
                {
                    tx.Abort();
                    editor.WriteMessage($"\n{e.Message}");
                    return;
                }

                tx.Abort();
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
                DataReferencesOptions dro = new DataReferencesOptions();
                string projectName = dro.ProjectName;
                string etapeName = dro.EtapeName;

                editor.WriteMessage("\n" + GetPathToDataFiles(projectName, etapeName, "Alignments"));

                #region Load alignments from drawing
                HashSet<Alignment> alignments = null;
                //To be able to check local or external alignments following hack is implemented
                alignments = localDb.HashSetOfType<Alignment>(tx);

                // open the LER dwg database
                Database xRefAlsDB = new Database(false, true);

                xRefAlsDB.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Alignments"),
                    System.IO.FileShare.Read, false, string.Empty);
                Transaction xRefAlsTx = xRefAlsDB.TransactionManager.StartTransaction();

                if (alignments.Count < 1)
                {
                    alignments = xRefAlsDB.HashSetOfType<Alignment>(xRefAlsTx)
                                                                     .OrderBy(x => x.Name).ToHashSet();
                }

                HashSet<Polyline3d> allLinework = localDb
                    .HashSetOfType<Polyline3d>(tx)
                    .Where(x => ReadStringParameterFromDataTable(x.Layer, dtKrydsninger, "Type", 0) == "3D")
                    .ToHashSet();
                editor.WriteMessage($"\nNr. of 3D polies: {allLinework.Count}");
                #endregion

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
                        editor.WriteMessage($"\nCrossing lines: {filteredLinework.Count}.");

                        int count = 0;
                        foreach (Entity ent in filteredLinework.Cast<Entity>())
                        {
                            #region Create points
                            using (Point3dCollection p3dcol = new Point3dCollection())
                            {
                                al.IntersectWith(ent, 0, plane, p3dcol, new IntPtr(0), new IntPtr(0));

                                foreach (Point3d p3d in p3dcol)
                                {
                                    //Id of the new Poly3d if type == 3D
                                    Oid newPolyId;

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
                                            count++;
                                            if (p3dInt.Z <= 0)
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
                        }

                        editor.WriteMessage($"\nIntersections detected: {count}.");
                    }
                }
                catch (System.Exception e)
                {
                    xRefAlsTx.Abort();
                    xRefAlsTx.Dispose();
                    xRefAlsDB.Dispose();
                    tx.Abort();
                    editor.WriteMessage($"\n{e.Message}");
                    return;
                }

                xRefAlsTx.Abort();
                xRefAlsTx.Dispose();
                xRefAlsDB.Dispose();
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
                string localLayerName = ReadStringParameterFromDataTable(
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
                            Oid ltId = lt.Add(ltr);
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

                string type = ReadStringParameterFromDataTable(
                            xrefLayer.Name, krydsninger, "Type", 0);

                bool typeExists = false;
                double depth = 0;

                if (!type.IsNoE() || type != null)
                {
                    typeExists = true;
                    depth = ReadDoubleParameterFromDataTable(type, dybde, "Dybde", 0);
                }

                #endregion

                using (Point3dCollection p3dcol = new Point3dCollection())
                {
                    alignment.IntersectWith(ent, 0, plane, p3dcol, new IntPtr(0), new IntPtr(0));

                    foreach (Point3d p3d in p3dcol)
                    {
                        Oid pointId = cogoPoints.Add(p3d, true);
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
                    Oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(xRefDB);
                    Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);
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

                                Oid flOid = FeatureLine.Create(flName, localEntity.ObjectId);

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
                                MapValue sizeRecord = ReadRecordData(
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
                                    depth = ReadDoubleParameterFromDataTable(type, dtDybde, "Dybde", 0);
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
                                    depth = ReadDoubleParameterFromDataTable(type, dtDybde, "Dybde", 0);
                                }

                                //Bogus 3D poly
                                if (depth > 0.001 && type != "3D")
                                {
                                    fl.UpgradeOpen();

                                    string originalName = fl.Name;
                                    string originalLayer = fl.Layer;
                                    string originalDescription = fl.Description;

                                    Oid newPolyId;

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

                                    Oid flOid = FeatureLine.Create(originalName, newPolyId);
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
                            tx.Abort();
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

                    Oid prStId = stc["PROFILE PROJEKTION MGO"];

                    foreach (Entity ent in allEnts)
                    {
                        if (ent is Label label)
                        {
                            label.CheckOrOpenForWrite();
                            label.StyleId = prStId;

                            Oid fId = label.FeatureId;
                            Entity fEnt = fId.Go<Entity>(tx);

                            int diaOriginal = ReadIntPropertyValue(tables, fEnt.Id, "CrossingData", "Diameter");
                            prdDbg(fEnt.Handle.ToString() + ": " + diaOriginal.ToString());

                            double dia = Convert.ToDouble(diaOriginal) / 1000;

                            if (dia == 0) dia = 0.11;
                            if (diaOriginal == 999) dia = 0.04;

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
                                    circle.CheckOrOpenForWrite();
                                    circle.Erase(true);
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

                                        br.CheckOrOpenForWrite();
                                        br.Erase(true);
                                    }
                                }
                            }

                            ent.CheckOrOpenForWrite();
                            ent.Layer = fEnt.Layer;
                            //label.CheckOrOpenForWrite();
                            //label.StyleId = prStId;
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
                    //throw new System.Exception(ex.Message);
                    editor.WriteMessage("\nMain caught it: " + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("POPULATEDISTANCES")]
        public void populatedistances()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

            #region Open fremtidig db
            DataReferencesOptions dro = new DataReferencesOptions();
            string projectName = dro.ProjectName;
            string etapeName = dro.EtapeName;

            // open the xref database
            Database fremDb = new Database(false, true);
            fremDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Fremtid"),
                System.IO.FileShare.Read, false, string.Empty);
            Transaction fremTx = fremDb.TransactionManager.StartTransaction();
            HashSet<Curve> allCurves = fremDb.HashSetOfType<Curve>(fremTx);
            HashSet<BlockReference> allBrs = fremDb.HashSetOfType<BlockReference>(fremTx);
            // open the LER database
            Database lerDb = new Database(false, true);
            lerDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Ler"),
                FileShare.Read, false, string.Empty);
            Transaction lerTx = lerDb.TransactionManager.StartTransaction();
            #endregion

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Read Csv Data for Layers
                    //Establish the pathnames to files
                    //Files should be placed in a specific folder on desktop
                    string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";
                    string pathDistnces = "X:\\AutoCAD DRI - 01 Civil 3D\\Distances.csv";

                    System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
                    System.Data.DataTable dtDistances = CsvReader.ReadCsvToDataTable(pathDistnces, "Distancer");
                    #endregion

                    BlockTableRecord space = (BlockTableRecord)tx.GetObject(localDb.CurrentSpaceId, OpenMode.ForWrite);
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForWrite) as BlockTable;

                    #region Create layer for draft profile
                    string afstandsMarkeringLayerName = "0-PROFILE_AFSTANDS_MARKERING";
                    using (Transaction txLag = localDb.TransactionManager.StartTransaction())
                    {
                        LayerTable lt = txLag.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;

                        if (!lt.Has(afstandsMarkeringLayerName))
                        {
                            LayerTableRecord ltr = new LayerTableRecord();
                            ltr.Name = afstandsMarkeringLayerName;
                            ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 0);
                            ltr.LineWeight = LineWeight.LineWeight000;

                            //Make layertable writable
                            lt.CheckOrOpenForWrite();

                            //Add the new layer to layer table
                            Oid ltId = lt.Add(ltr);
                            txLag.AddNewlyCreatedDBObject(ltr, true);
                        }
                        txLag.Commit();
                    }
                    #endregion

                    HashSet<ProfileView> pvs = localDb.HashSetOfType<ProfileView>(tx);
                    foreach (ProfileView pv in pvs)
                    {
                        System.Windows.Forms.Application.DoEvents();
                        Alignment al = pv.AlignmentId.Go<Alignment>(tx);
                        Point3d pvOrigin = pv.Location;
                        double originX = pvOrigin.X;
                        double originY = pvOrigin.Y;

                        double pvStStart = pv.StationStart;
                        double pvStEnd = pv.StationEnd;
                        double pvElBottom = pv.ElevationMin;
                        double pvElTop = pv.ElevationMax;
                        double pvLength = pvStEnd - pvStStart;
                        double stepLength = 0.1;
                        int nrOfSteps = (int)(pvLength / stepLength);

                        #region GetCurvesAndBRs from fremtidig
                        HashSet<Curve> curves = allCurves
                            .Where(x => x.XrecFilter("Alignment", new[] { al.Name }))
                            .ToHashSet();
                        #endregion

                        #region Build size array
                        (int dn, double station, double kod)[] sizeArray = new (int dn, double station, double kod)[0];
                        int previousDn = 0;
                        int currentDn = 0;
                        for (int i = 0; i < nrOfSteps + 1; i++)
                        {
                            double curStationBA = pvStStart + stepLength * i;
                            Point3d curSamplePoint = default;
                            try { curSamplePoint = al.GetPointAtDist(curStationBA); }
                            catch (System.Exception) { continue; }

                            HashSet<(Curve curve, double dist, double kappeOd)> curveDistTuples =
                                new HashSet<(Curve curve, double dist, double kappeOd)>();

                            foreach (Curve curve in curves)
                            {
                                if (curve.GetDistanceAtParameter(curve.EndParam) < 1.0) continue;
                                Point3d closestPoint = curve.GetClosestPointTo(curSamplePoint, false);
                                if (closestPoint != default)
                                    curveDistTuples.Add(
                                        (curve, curSamplePoint.DistanceHorizontalTo(closestPoint), GetPipeKOd(curve)));
                            }
                            var result = curveDistTuples.MinBy(x => x.dist).FirstOrDefault();
                            //Detect current dn
                            currentDn = GetPipeDN(result.curve);
                            if (currentDn != previousDn)
                            {
                                //Set the previous segment end station unless there's 0 segments
                                if (sizeArray.Length != 0) sizeArray[sizeArray.Length - 1].station = curStationBA;
                                //Add the new segment
                                sizeArray = sizeArray.Append((currentDn, 0, result.kappeOd)).ToArray();
                            }
                            //Hand over DN to cache in "previous" variable
                            previousDn = currentDn;
                            //on the last iteration set the last segment distance
                            if (i == nrOfSteps) sizeArray[sizeArray.Length - 1].station = al.Length;
                        }

                        prdDbg("");
                        prdDbg($"****{al.Name}****");
                        for (int i = 0; i < sizeArray.Length; i++)
                        {
                            prdDbg($"{sizeArray[i].dn.ToString("D3")} || " +
                                   $"{sizeArray[i].station.ToString("0.00")} || " +
                                   $"{sizeArray[i].kod.ToString("0.0")}");
                        }
                        #endregion

                        BlockTableRecord btr;
                        if (bt.Has(pv.Name))
                        {
                            btr = bt[pv.Name].Go<BlockTableRecord>(tx, OpenMode.ForWrite);
                        }
                        else throw new System.Exception($"Block {pv.Name} is missing!");

                        ObjectIdCollection brefIds = btr.GetBlockReferenceIds(true, true);
                        if (brefIds.Count == 0) throw new System.Exception($"Block {pv.Name} does not have any references!");
                        Oid brefId = brefIds[0];
                        BlockReference bref = brefId.Go<BlockReference>(tx);

                        HashSet<ProfileProjectionLabel> ppls = localDb.HashSetOfType<ProfileProjectionLabel>(tx);

                        foreach (ProfileProjectionLabel ppl in ppls)
                        {
                            Oid pId = ppl.FeatureId;
                            if (!pId.IsDerivedFrom<CogoPoint>()) continue;
                            CogoPoint cp = pId.Go<CogoPoint>(tx);
                            if (ReadStringPropertyValue(tables, pId, "CrossingData", "Alignment") != al.Name) continue;

                            //Get original object from LER dwg
                            string handle = ReadStringPropertyValue(tables, pId, "IdRecord", "Handle");
                            //If returned handle string is empty
                            //For any reason, fx missing OD data
                            //Fall back to the name of the cogopoint
                            //else continue
                            if (handle.IsNoE())
                            {
                                string pName = cp.PointName;
                                string[] res = pName.Split('_');
                                handle = res[0];
                            }
                            if (handle.IsNoE()) continue;
                            long ln;
                            Handle hn;
                            try
                            {
                                ln = Convert.ToInt64(handle, 16);
                                hn = new Handle(ln);
                            }
                            catch (System.Exception ex)
                            {
                                prdDbg("Creation of handle failed!");
                                throw;
                            }
                            Oid originalId = Oid.Null;
                            try
                            { originalId = lerDb.GetObjectId(false, hn, 0); }
                            catch (System.Exception ex)
                            {
                                prdDbg($"Getting object by handle failed! {ex.Message}");
                                throw;
                            }
                            Entity originalEnt = originalId.Go<Entity>(lerTx);

                            //Determine type and distance
                            string distanceType = ReadStringParameterFromDataTable(originalEnt.Layer, dtKrydsninger, "Distance", 0);
                            string blockType = ReadStringParameterFromDataTable(originalEnt.Layer, dtKrydsninger, "Block", 0);
                            double distance = ReadDoubleParameterFromDataTable(distanceType, dtDistances, "Distance", 0);
                            int originalDia = ReadIntPropertyValue(tables, pId, "CrossingData", "Diameter");
                            double dia = Convert.ToDouble(originalDia) / 1000;
                            if (dia == 0) dia = 0.11;

                            //Determine kOd
                            double station = 0;
                            double elevation = 0;
                            if (!pv.FindStationAndElevationAtXY(ppl.LabelLocation.X, ppl.LabelLocation.Y, ref station, ref elevation))
                                throw new System.Exception($"Point {ppl.Handle} couldn't finde elevation and station!!!");

                            //Determine dn
                            double kappeOd = 0;
                            for (int i = 0; i < sizeArray.Length; i++)
                            {
                                if (station <= sizeArray[i].station) { kappeOd = sizeArray[i].kod / 1000; break; }
                            }

                            Circle circle = null;
                            switch (blockType)
                            {
                                case "Cirkel, Bund":
                                    {
                                        foreach (Oid oid in btr)
                                        {
                                            if (!oid.IsDerivedFrom<Circle>()) continue;
                                            Circle tempC = oid.Go<Circle>(tx);
                                            //prdDbg("C: " + tempC.Center.ToString());
                                            Point3d theoreticalLocation = new Point3d(ppl.LabelLocation.X, ppl.LabelLocation.Y + (dia / 2), 0);
                                            theoreticalLocation = theoreticalLocation.TransformBy(bref.BlockTransform.Inverse());
                                            //prdDbg("T: " + theoreticalLocation.ToString());
                                            //prdDbg($"dX: {tempC.Center.X - theoreticalLocation.X}, dY: {tempC.Center.Y - theoreticalLocation.Y}");
                                            if (tempC.Center.DistanceHorizontalTo(theoreticalLocation) < 0.0001)
                                            {
                                                //prdDbg("Found Cirkel, Bund!");
                                                circle = tempC;
                                                break;
                                            }
                                        }
                                    }
                                    break;
                                case "Cirkel, Top":
                                    {
                                        foreach (Oid oid in btr)
                                        {
                                            if (!oid.IsDerivedFrom<Circle>()) continue;
                                            Circle tempC = oid.Go<Circle>(tx);
                                            Point3d theoreticalLocation = new Point3d(ppl.LabelLocation.X, ppl.LabelLocation.Y - (dia / 2), 0);
                                            theoreticalLocation = theoreticalLocation.TransformBy(bref.BlockTransform.Inverse());
                                            if (tempC.Center.DistanceHorizontalTo(theoreticalLocation) < 0.0001)
                                            {
                                                //prdDbg("Found Cirkel, Top!");
                                                circle = tempC;
                                                break;
                                            }
                                        }
                                    }
                                    break;
                                case "EL 0.4kV":
                                    foreach (Oid oid in btr)
                                    {
                                        if (!oid.IsDerivedFrom<BlockReference>()) continue;
                                        BlockReference tempBref = oid.Go<BlockReference>(tx);
                                        //prdDbg("C: " + tempBref.Position.ToString());
                                        BlockTableRecord tempBtr = tempBref.BlockTableRecord.Go<BlockTableRecord>(tx);
                                        Point3d theoreticalLocation = new Point3d(ppl.LabelLocation.X, ppl.LabelLocation.Y, 0);
                                        theoreticalLocation = theoreticalLocation.TransformBy(bref.BlockTransform.Inverse());
                                        //prdDbg("T: " + theoreticalLocation.ToString());
                                        //prdDbg($"dX: {tempBref.Position.X - theoreticalLocation.X}, dY: {tempBref.Position.Y - theoreticalLocation.Y}");
                                        if (tempBref.Position.DistanceHorizontalTo(theoreticalLocation) < 0.0001)
                                        {
                                            //prdDbg("Found block!");
                                            Extents3d ext = tempBref.GeometricExtents;
                                            //prdDbg(ext.ToString());
                                            using (Polyline pl = new Polyline(4))
                                            {
                                                pl.AddVertexAt(0, new Point2d(ext.MinPoint.X, ext.MinPoint.Y), 0.0, 0.0, 0.0);
                                                pl.AddVertexAt(1, new Point2d(ext.MinPoint.X, ext.MaxPoint.Y), 0.0, 0.0, 0.0);
                                                pl.AddVertexAt(2, new Point2d(ext.MaxPoint.X, ext.MaxPoint.Y), 0.0, 0.0, 0.0);
                                                pl.AddVertexAt(3, new Point2d(ext.MaxPoint.X, ext.MinPoint.Y), 0.0, 0.0, 0.0);
                                                pl.Closed = true;
                                                pl.SetDatabaseDefaults();
                                                pl.ReverseCurve();

                                                using (DBObjectCollection col = pl.GetOffsetCurves(distance))
                                                {
                                                    foreach (var obj in col)
                                                    {
                                                        Entity ent = (Entity)obj;
                                                        ent.Layer = afstandsMarkeringLayerName;
                                                        btr.AppendEntity(ent);
                                                        tx.AddNewlyCreatedDBObject(ent, true);
                                                    }
                                                }
                                                using (DBObjectCollection col = pl.GetOffsetCurves(distance + kappeOd / 2))
                                                {
                                                    foreach (var obj in col)
                                                    {
                                                        Entity ent = (Entity)obj;
                                                        ent.Layer = afstandsMarkeringLayerName;
                                                        btr.AppendEntity(ent);
                                                        tx.AddNewlyCreatedDBObject(ent, true);
                                                    }
                                                }
                                            }
                                            break;
                                        }
                                    }
                                    break;
                                default:
                                    break;
                            }

                            if (circle != null)
                            {
                                using (DBObjectCollection col = circle.GetOffsetCurves(distance))
                                {
                                    foreach (var obj in col)
                                    {
                                        Entity ent = (Entity)obj;
                                        ent.Layer = afstandsMarkeringLayerName;
                                        btr.AppendEntity(ent);
                                        tx.AddNewlyCreatedDBObject(ent, true);
                                    }
                                }
                                using (DBObjectCollection col = circle.GetOffsetCurves(distance + kappeOd / 2))
                                {
                                    foreach (var obj in col)
                                    {
                                        Entity ent = (Entity)obj;
                                        ent.Layer = afstandsMarkeringLayerName;
                                        btr.AppendEntity(ent);
                                        tx.AddNewlyCreatedDBObject(ent, true);
                                    }
                                }
                            }
                        }

                        //Update block references
                        ObjectIdCollection brs = btr.GetBlockReferenceIds(true, true);
                        foreach (Oid oid in brs)
                        {
                            BlockReference br = oid.Go<BlockReference>(tx, OpenMode.ForWrite);
                            br.RecordGraphicsModified(true);
                        }
                    }
                }

                catch (System.Exception ex)
                {
                    fremTx.Abort();
                    fremTx.Dispose();
                    fremDb.Dispose();
                    lerTx.Abort();
                    lerTx.Dispose();
                    lerDb.Dispose();
                    tx.Abort();
                    editor.WriteMessage(ex.ToString());
                    return;
                }
                fremTx.Abort();
                fremTx.Dispose();
                fremDb.Dispose();
                lerTx.Abort();
                lerTx.Dispose();
                lerDb.Dispose();
                tx.Commit();
            }
        }

        [CommandMethod("populateprofiles")]
        public void populateprofiles()
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
                    #region Read Csv Data for Layers
                    //Establish the pathnames to files
                    //Files should be placed in a specific folder on desktop
                    string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";

                    System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
                    #endregion

                    BlockTableRecord space = (BlockTableRecord)tx.GetObject(localDb.CurrentSpaceId, OpenMode.ForWrite);
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForWrite) as BlockTable;

                    //#region Get the selection set of all objects and profile view
                    //PromptSelectionOptions pOptions = new PromptSelectionOptions();
                    //PromptSelectionResult sSetResult = editor.GetSelection(pOptions);
                    //if (sSetResult.Status != PromptStatus.OK) return;
                    //HashSet<Entity> allEnts = sSetResult.Value.GetObjectIds().Select(e => e.Go<Entity>(tx)).ToHashSet();
                    //#endregion

                    HashSet<ProfileView> pvs = localDb.HashSetOfType<ProfileView>(tx);

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

                        LabelStyleCollection stc = civilDoc.Styles.LabelStyles
                                                       .ProjectionLabelStyles.ProfileViewProjectionLabelStyles;

                        Oid prStId = stc["PROFILE PROJEKTION MGO"];

                        HashSet<Label> labels = localDb.HashSetOfType<Label>(tx);
                        Extents3d extentsPv = pv.GeometricExtents;

                        var pvLabels = labels.Where(l => extentsPv.IsPointInsideXY(l.LabelLocation));
                        prdDbg($"Number of labels inside extents: {pvLabels.Count()}");

                        foreach (Label label in pvLabels)
                        {
                            label.CheckOrOpenForWrite();
                            label.StyleId = prStId;

                            Oid fId = label.FeatureId;
                            Entity fEnt = fId.Go<Entity>(tx);

                            var diaOriginal = (int)ReadPropertyValueFromPS(fEnt, "CrossingData", "Diameter");

                            double dia = Convert.ToDouble(diaOriginal) / 1000;

                            if (dia == 0 || diaOriginal == 999) dia = 0.11;

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

                                    circle.Erase(true);
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

                                        br.Erase(true);
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
                    string[] columnNames = new string[1] { "Handle" };
                    string[] columnDescrs = new string[1] { "Handle to string" };
                    DataType[] dataTypes = new DataType[1] { DataType.Character };

                    Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

                    if (DoesTableExist(tables, m_tableName))
                    {
                        editor.WriteMessage("\nTable already exists!");
                    }
                    else
                    {
                        if (CreateTable(
                            tables, m_tableName, "Object handle", columnNames, columnDescrs,
                            dataTypes))
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

                        if (DoesRecordExist(tables, ent.ObjectId, m_tableName, "Id"))
                        {
                            UpdateODRecord(tables, m_tableName, columnNames[0], ent.ObjectId, value);
                        }
                        else if (AddODRecord(tables, m_tableName, columnNames[0], ent.ObjectId,
                            new MapValue(value)))
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
        [CommandMethod("ca")]
        public void copytexttoattribute()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

            while (true)
            {
                using (Transaction tx = localDb.TransactionManager.StartTransaction())
                {
                    try
                    {
                        LayerTable lt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;

                        #region Select pline3d
                        PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                            "\nSelect (poly)line(3d) to modify:");
                        promptEntityOptions1.SetRejectMessage("\n Not a polyline3d!");
                        promptEntityOptions1.AddAllowedClass(typeof(Polyline3d), true);
                        promptEntityOptions1.AddAllowedClass(typeof(Polyline), true);
                        promptEntityOptions1.AddAllowedClass(typeof(Line), true);
                        PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                        if (((PromptResult)entity1).Status != PromptStatus.OK) { tx.Abort(); return; }
                        Autodesk.AutoCAD.DatabaseServices.ObjectId pline3dId = entity1.ObjectId;
                        #endregion

                        #region Select text
                        PromptEntityOptions promptEntityOptions2 = new PromptEntityOptions(
                            "\nSelect text to copy <Push ENTER to enter text manually>:");
                        promptEntityOptions2.SetRejectMessage("\n Not a text!");
                        promptEntityOptions2.AddAllowedClass(typeof(DBText), true);
                        promptEntityOptions2.AllowNone = true;
                        PromptEntityResult entity2 = editor.GetEntity(promptEntityOptions2);

                        string readTextValue = "";
                        if (((PromptResult)entity2).Status == PromptStatus.None)
                        {
                            PromptStringOptions strPromptOptions = new PromptStringOptions(
                                "\nEnter dimension and material data manually: ");
                            strPromptOptions.AllowSpaces = true;
                            PromptResult pr = editor.GetString(strPromptOptions);
                            if (pr.Status != PromptStatus.OK) { tx.Abort(); return; }
                            readTextValue = pr.StringResult;
                        }
                        else if (((PromptResult)entity2).Status == PromptStatus.OK)
                        {
                            Autodesk.AutoCAD.DatabaseServices.ObjectId textId = entity2.ObjectId;
                            readTextValue = textId.Go<DBText>(tx).TextString.Trim();
                        }
                        else { tx.Abort(); return; }
                        #endregion

                        #region Er ledningen i brug
                        const string kwd1 = "Ja";
                        const string kwd2 = "Nej";

                        PromptKeywordOptions pKeyOpts = new PromptKeywordOptions("");
                        pKeyOpts.Message = "\nEr ledningen i brug? ";
                        pKeyOpts.Keywords.Add(kwd1);
                        pKeyOpts.Keywords.Add(kwd2);
                        pKeyOpts.AllowNone = true;
                        pKeyOpts.Keywords.Default = kwd1;
                        PromptResult pKeyRes = editor.GetKeywords(pKeyOpts);

                        bool ledningIbrug = pKeyRes.StringResult == kwd1;

                        #endregion

                        #region Try creating records

                        string m_tableName = OdTables.Gas.GetTableName();
                        string[] columnNames = OdTables.Gas.GetColumnNames();
                        string[] columnDescriptions = OdTables.Gas.GetColumnDescriptions();
                        Autodesk.Gis.Map.Constants.DataType[] dataTypes = OdTables.Gas.GetDataTypes();
                        MapValue[] values = new MapValue[3];

                        #region Prepare OdTable for gas
                        CheckOrCreateTable(
                            tables,
                            OdTables.Gas.GetTableName(),
                            OdTables.Gas.GetTableDescription(),
                            OdTables.Gas.GetColumnNames(),
                            OdTables.Gas.GetColumnDescriptions(),
                            OdTables.Gas.GetDataTypes());
                        #endregion

                        int parsedInt = 0;
                        string parsedMat = string.Empty;
                        if (readTextValue.Contains(" "))
                        {
                            //Gas specific handling
                            string[] output = readTextValue.Split((char[])null); //Splits by whitespace

                            int.TryParse(output[0], out parsedInt);
                            //Material
                            parsedMat = output[1];
                        }
                        else
                        {
                            string[] output = readTextValue.Split('/');
                            string a = ""; //For number
                            string b = ""; //For material

                            for (int i = 0; i < output[0].Length; i++)
                            {
                                if (Char.IsDigit(output[0][i])) a += output[0][i];
                                else b += output[0][i];
                            }

                            int.TryParse(a, out parsedInt);
                            parsedMat = b;
                        }

                        //Aggregate
                        values[0] = new MapValue(parsedInt);
                        values[1] = new MapValue(parsedMat);
                        if (ledningIbrug) values[2] = new MapValue("");
                        else values[2] = new MapValue("Ikke i brug");

                        for (int i = 0; i < columnNames.Length; i++)
                        {
                            bool success = CheckAddUpdateRecordValue(
                                tables,
                                pline3dId,
                                m_tableName,
                                columnNames[i],
                                values[i]);

                            if (success)
                            {
                                editor.WriteMessage($"\nUpdating color and layer properties!");
                                Entity ent = pline3dId.Go<Entity>(tx, OpenMode.ForWrite);

                                //Check layer name
                                if (!lt.Has("GAS-ude af drift"))
                                {
                                    LayerTableRecord ltr = new LayerTableRecord();
                                    ltr.Name = "GAS-ude af drift";
                                    ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 221);

                                    //Make layertable writable
                                    lt.CheckOrOpenForWrite();

                                    //Add the new layer to layer table
                                    Oid ltId = lt.Add(ltr);
                                    tx.AddNewlyCreatedDBObject(ltr, true);

                                    lt.DowngradeOpen();
                                }

                                if (ledningIbrug) ent.ColorIndex = 1;
                                else { ent.Layer = "GAS-ude af drift"; ent.ColorIndex = 130; }
                            }
                            else editor.WriteMessage($"\n{columnNames[i]} record creation failed!");
                        }

                        #endregion
                    }
                    catch (System.Exception ex)
                    {
                        tx.Abort();
                        editor.WriteMessage("\n" + ex.Message);
                        return;
                    }
                    tx.Commit();
                }
            }


        }

        [CommandMethod("gasikkeibrug")]
        [CommandMethod("cg")]
        public void gasikkeibrug()
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
                        LayerTable lt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;

                        #region Select pline3d
                        PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                            "\nSelect (poly)line(3d) to modify:");
                        promptEntityOptions1.SetRejectMessage("\n Not a polyline3d!");
                        promptEntityOptions1.AddAllowedClass(typeof(Polyline), true);
                        promptEntityOptions1.AddAllowedClass(typeof(Line), true);
                        promptEntityOptions1.AddAllowedClass(typeof(Polyline3d), true);
                        PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                        if (((PromptResult)entity1).Status != PromptStatus.OK) { tx.Abort(); return; }
                        Autodesk.AutoCAD.DatabaseServices.ObjectId pline3dId = entity1.ObjectId;
                        #endregion

                        #region Select text - NOT USED
                        //PromptEntityOptions promptEntityOptions2 = new PromptEntityOptions(
                        //    "\nSelect text to copy:");
                        //promptEntityOptions2.SetRejectMessage("\n Not a text!");
                        //promptEntityOptions2.AddAllowedClass(typeof(DBText), true);
                        //PromptEntityResult entity2 = editor.GetEntity(promptEntityOptions2);
                        //if (((PromptResult)entity2).Status != PromptStatus.OK) { tx.Abort(); return; }
                        //Autodesk.AutoCAD.DatabaseServices.ObjectId textId = entity2.ObjectId;
                        #endregion

                        #region Er ledningen i brug - NOT USED
                        //const string kwd1 = "Ja";
                        //const string kwd2 = "Nej";

                        //PromptKeywordOptions pKeyOpts = new PromptKeywordOptions("");
                        //pKeyOpts.Message = "\nEr ledningen i brug? ";
                        //pKeyOpts.Keywords.Add(kwd1);
                        //pKeyOpts.Keywords.Add(kwd2);
                        //pKeyOpts.AllowNone = true;
                        //pKeyOpts.Keywords.Default = kwd1;
                        //PromptResult pKeyRes = editor.GetKeywords(pKeyOpts);

                        //bool ledningIbrug = pKeyRes.StringResult == kwd1;

                        #endregion

                        #region Try creating records

                        string m_tableName = "GasDimOgMat";
                        string columnName = "Bemærk";
                        Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

                        if (DoesTableExist(tables, m_tableName)) editor.WriteMessage("\nTable exists!");
                        else throw new System.Exception("Table does not exist!");

                        //Aggregate
                        MapValue value = new MapValue("Ikke i brug");

                        bool success = false;

                        if (DoesRecordExist(tables, pline3dId, m_tableName, columnName))
                        {
                            editor.WriteMessage($"\nRecord {columnName} already exists, updating...");

                            if (UpdateODRecord(tables, m_tableName, columnName, pline3dId, value))
                            {
                                editor.WriteMessage($"\nUpdating record {columnName} succeded!");
                                success = true;
                            }
                            else editor.WriteMessage($"\nUpdating record {columnName} failed!");
                        }
                        else
                        {
                            throw new System.Exception("Record does not exist! Run CA first!");
                        }

                        if (success)
                        {
                            editor.WriteMessage($"\nUpdating color and layer properties!");
                            Entity ent = pline3dId.Go<Entity>(tx, OpenMode.ForWrite);

                            //Check layer name
                            if (!lt.Has("GAS-ude af drift"))
                            {
                                LayerTableRecord ltr = new LayerTableRecord();
                                ltr.Name = "GAS-ude af drift";
                                ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 221);

                                //Make layertable writable
                                lt.CheckOrOpenForWrite();

                                //Add the new layer to layer table
                                Oid ltId = lt.Add(ltr);
                                tx.AddNewlyCreatedDBObject(ltr, true);

                                lt.DowngradeOpen();
                            }

                            ent.Layer = "GAS-ude af drift"; ent.ColorIndex = 130;
                        }
                        #endregion
                    }
                    catch (System.Exception ex)
                    {
                        tx.Abort();
                        editor.WriteMessage("\n" + ex.Message);
                        return;
                    }
                    tx.Commit();
                }
            }
        }

        [CommandMethod("COPYODGAS")]
        [CommandMethod("CD")]
        public void copyodgas()
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
                    #region Select entities
                    PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                        "\nSelect entity FROM where to copy OD:");
                    promptEntityOptions1.SetRejectMessage("\n Not an entity!");
                    promptEntityOptions1.AddAllowedClass(typeof(Entity), false);
                    PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    if (((PromptResult)entity1).Status != PromptStatus.OK) { tx.Abort(); return; }
                    Autodesk.AutoCAD.DatabaseServices.ObjectId sourceId = entity1.ObjectId;

                    PromptEntityOptions promptEntityOptions2 = new PromptEntityOptions(
                        "\nSelect entity where to copy OD TO:");
                    promptEntityOptions1.SetRejectMessage("\n Not an entity!");
                    promptEntityOptions1.AddAllowedClass(typeof(Entity), false);
                    PromptEntityResult entity2 = editor.GetEntity(promptEntityOptions2);
                    if (((PromptResult)entity2).Status != PromptStatus.OK) { tx.Abort(); return; }
                    Autodesk.AutoCAD.DatabaseServices.ObjectId targetId = entity2.ObjectId;
                    #endregion

                    #region Choose table
                    CopyAllOD(HostMapApplicationServices.Application.ActiveProject.ODTables,
                        sourceId, targetId);
                    #endregion

                    Entity targetEnt = targetId.Go<Entity>(tx, OpenMode.ForWrite);
                    Entity sourceEnt = sourceId.Go<Entity>(tx);

                    if (sourceEnt.Layer == "GAS-ude af drift")
                    {
                        targetEnt.Layer = "GAS-ude af drift";
                        targetEnt.ColorIndex = 130;
                    }
                    else targetEnt.ColorIndex = 1;
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
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
                    string[] columnNames = new string[1] { "Size" };
                    string[] columnDescrs = new string[1] { "Size of pipe" };
                    DataType[] dataTypes = new DataType[1] { DataType.Integer };

                    Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

                    if (DoesTableExist(tables, m_tableName))
                    {
                        editor.WriteMessage("\nTable already exists!");
                    }
                    else
                    {
                        if (CreateTable(
                            tables, m_tableName, "Pipe size table", columnNames, columnDescrs,
                            dataTypes))
                        {
                            editor.WriteMessage($"\nCreated table {m_tableName}.");
                        }
                        else
                        {
                            editor.WriteMessage("\nFailed to create the ObjectData table.");
                            return false;
                        }
                    }

                    if (DoesRecordExist(tables, Entity.ObjectId, m_tableName, columnNames[0]))
                    {
                        UpdateODRecord(tables, m_tableName, columnNames[0], Entity.ObjectId, size);
                    }
                    else if (AddODRecord(tables, m_tableName, columnNames[0], Entity.ObjectId,
                        new MapValue(size)))
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

        [CommandMethod("convertlineworkpss")]
        public void convertlineworkpss()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            prdDbg("Remember that the PropertySets need be defined in advance!!!");

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
                        PropertySetCopyFromEntToEnt(spline, curve);
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
                        PropertySetCopyFromEntToEnt(pline, polyline3D);
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
                        PropertySetCopyFromEntToEnt(line, polyline3D);
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
                        Oid id = localDb.GetObjectId(false, hn, 0);
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
                #region Get alignments
                DataReferencesOptions dro = new DataReferencesOptions();
                string projectName = dro.ProjectName;
                string etapeName = dro.EtapeName;

                editor.WriteMessage("\n" + GetPathToDataFiles(projectName, etapeName, "Alignments"));

                // open the LER dwg database
                Database xRefAlDB = new Database(false, true);

                xRefAlDB.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Alignments"),
                    System.IO.FileShare.Read, false, string.Empty);
                Transaction xRefAlTx = xRefAlDB.TransactionManager.StartTransaction();
                HashSet<Alignment> als = xRefAlDB.HashSetOfType<Alignment>(xRefAlTx);
                editor.WriteMessage($"\nNr. of alignments: {als.Count}");
                #endregion

                try
                {
                    #region Load linework from local db
                    HashSet<Polyline3d> plines3d = localDb.HashSetOfType<Polyline3d>(tx);
                    editor.WriteMessage($"\nNr. of 3D polies: {plines3d.Count}");
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

                    #region Select to filter text for gas or not
                    bool NoFilter = true;
                    //Subsection for Gasfilter
                    {
                        const string ckwd1 = "No filter";
                        const string ckwd2 = "Gas filter";
                        PromptKeywordOptions pKeyOpts2 = new PromptKeywordOptions("");
                        pKeyOpts2.Message = "\nChoose if text should be filtered: ";
                        pKeyOpts2.Keywords.Add(ckwd1);
                        pKeyOpts2.Keywords.Add(ckwd2);
                        pKeyOpts2.AllowNone = true;
                        pKeyOpts2.Keywords.Default = ckwd1;
                        PromptResult locpKeyRes2 = editor.GetKeywords(pKeyOpts2);

                        NoFilter = locpKeyRes2.StringResult == ckwd1;
                    }
                    #endregion

                    Plane plane = new Plane();

                    List<Oid> sourceIds = new List<Oid>();

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
                            foreach (DBText item in text)
                            {
                                if (NoFilter) sourceIds.Add(item.ObjectId);
                                else
                                {
                                    //Gas specific filtering
                                    string[] output = item.TextString.Split((char[])null); //Splits by whitespace
                                    int parsedInt = 0;
                                    if (int.TryParse(output[0], out parsedInt))
                                    {
                                        if (parsedInt <= 90) continue;
                                        sourceIds.Add(item.ObjectId);
                                    }
                                }
                            }
                            //This is in memory only object now
                            //sourceIds.Add(al.ObjectId);
                        }
                    }

                    editor.SetImpliedSelection(sourceIds.ToArray());


                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    xRefAlTx.Abort();
                    xRefAlDB.Dispose();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                xRefAlTx.Commit();
                tx.Commit();
                xRefAlDB.Dispose();
            }
        }

        /// <summary>
        /// Used to move 3d polylines of Novafos data to elevation points
        /// </summary>
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
            HashSet<Polyline3d> readyLines = new HashSet<Polyline3d>();
            //List with one of nodes missing
            HashSet<(Polyline3d line, DBPoint node)> linesWithOneMissingNode =
                new HashSet<(Polyline3d line, DBPoint node)>();
            //For use for secondary interpolation for lines missing one or both end nodes.
            HashSet<Polyline3d> linesWithMissingBothNodes = new HashSet<Polyline3d>();

            //Dictionary for the tables for layers
            Dictionary<string, string> tableNameDict = new Dictionary<string, string>()
            {
                {"AFL_ikke_ibrug", "AFL_ikke_ibrug" },
                {"AFL_ledning_draen", "AFL_ledning_draen" },
                {"AFL_ledning_dræn", "AFL_ledning_draen" },
                {"AFL_ledning_faelles", "AFL_ledning_faelles" },
                {"AFL_ledning_fælles", "AFL_ledning_faelles" },
                {"AFL_ledning_regn", "AFL_ledning_regn" },
                {"AFL_ledning_spild", "AFL_ledning_spild" },
                {"Afløb-kloakledning", "AFL_ledning_faelles" },
                {"Drænvand", "AFL_ledning_draen" },
                {"Regnvand", "AFL_ledning_regn" }
            };

            //Process all lines and detect with nodes at both ends
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Load linework from local db
                    HashSet<Polyline3d> localPlines3d = localDb
                        .HashSetOfType<Polyline3d>(tx)
                        .Where(x => x.Layer == "AFL_ledning_faelles" ||
                                    x.Layer == "AFL_ledning_spild" ||
                                    x.Layer == "AFL_ikke_ibrug" ||
                                    x.Layer == "Afløb-kloakledning" ||
                                    x.Layer == "AFL_ledning_fælles" ||
                                    x.Layer == "AFL_ledning_draen" ||
                                    x.Layer == "AFL_ledning_regn" ||
                                    x.Layer == "Regnvand" ||
                                    x.Layer == "Drænvand")
                        .ToHashSet();
                    editor.WriteMessage($"\nNr. of local 3D polies: {localPlines3d.Count}");
                    #endregion

                    #region Points and tables
                    Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

                    //Points to intersect
                    HashSet<DBPoint> points = new HashSet<DBPoint>(localDb.ListOfType<DBPoint>(tx))
                        .Where(x => x.Layer == "AFL_knude").ToHashSet(new PointDBHorizontalComparer(0.1));
                    editor.WriteMessage($"\nNr. of local points: {points.Count}");
                    editor.WriteMessage($"\nTotal number of combinations: " +
                        $"{points.Count * (localPlines3d.Count)}");
                    #endregion

                    #region Poly3ds with knudepunkter at ends
                    foreach (Polyline3d pline3d in localPlines3d)
                    {
                        var vertices = pline3d.GetVertices(tx);

                        int endIdx = vertices.Length - 1;

                        //Start point
                        DBPoint startMatch = points.Where(x => x.Position.HorizontalEqualz(vertices[0].Position, 0.1)).FirstOrDefault();
                        //End point
                        DBPoint endMatch = points.Where(x => x.Position.HorizontalEqualz(vertices[endIdx].Position, 0.1)).FirstOrDefault();

                        if (startMatch != null && endMatch != null)
                        {
                            double startElevation = ReadDoublePropertyValue(tables, startMatch.ObjectId,
                                "AFL_knude", "BUNDKOTE");

                            double endElevation = ReadDoublePropertyValue(tables, endMatch.ObjectId,
                                "AFL_knude", "BUNDKOTE");

                            if (startElevation != 0 && endElevation != 0)
                            {
                                //Add to interpolated listv
                                readyLines.Add(pline3d);

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

                                    for (int i = 0; i < endIdx + 1; i++)
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
                                    editor.WriteMessage($"\nElevations are the same! " +
                                        $"Start: {startElevation}, End: {endElevation}.");
                                    for (int i = 0; i < endIdx + 1; i++)
                                    {
                                        //We don't need to interpolate start and end points,
                                        //So skip them
                                        if (i != 0 && i != endIdx)
                                        {
                                            //Change the elevation
                                            vertices[i].CheckOrOpenForWrite();
                                            vertices[i].Position = new Point3d(
                                                vertices[i].Position.X, vertices[i].Position.Y, startElevation);
                                        }
                                    }
                                }
                            }
                        }
                        else if (startMatch != null || endMatch != null)
                        {
                            if (startMatch != null) linesWithOneMissingNode.Add((pline3d, startMatch));
                            else linesWithOneMissingNode.Add((pline3d, endMatch));
                        }
                        else
                        {
                            //The rest of the lines assumed missing one or both nodes
                            linesWithMissingBothNodes.Add(pline3d);
                        }
                    }
                    #endregion

                    editor.WriteMessage($"\nReady lines: {readyLines.Count}, " +
                                          $"Lines missing one node: {linesWithOneMissingNode.Count}, " +
                                          $"Missing both ends: {linesWithMissingBothNodes.Count}.");

                    //Process lines with missin one of the nodes
                    //Assume that the slope data is present, else move it to missing both nodes
                    foreach ((Polyline3d line, DBPoint node) in linesWithOneMissingNode)
                    {
                        int KNUDEID = ReadIntPropertyValue(tables, node.Id, "AFL_knude", "KNUDEID");
                        bool isUpstreamNode = true;
                        if (ReadIntPropertyValue(tables, line.Id, tableNameDict[line.Layer],
                            "NEDSTROEMK") == KNUDEID) isUpstreamNode = false;

                        double actualSlope = ReadDoublePropertyValue(tables, line.Id,
                            tableNameDict[line.Layer], "FALD");

                        if (isUpstreamNode) actualSlope = -actualSlope;

                        double detectedElevation = ReadDoublePropertyValue(tables, node.Id,
                                "AFL_knude", "BUNDKOTE");

                        bool startsAtStart = false;

                        var vertices = line.GetVertices(tx);
                        int endIdx = vertices.Length - 1;
                        editor.WriteMessage($"\nendIdx: {endIdx}");
                        if (vertices[0].Position.HorizontalEqualz(node.Position)) startsAtStart = true;

                        if (startsAtStart)
                        {
                            //Forward iteration
                            editor.WriteMessage($"\nForward iteration!");
                            double PB = 0;

                            for (int i = 0; i < endIdx + 1; i++)
                            {
                                if (i != 0)
                                {
                                    PB += vertices[i - 1].Position.DistanceHorizontalTo(
                                         vertices[i].Position);
                                    editor.WriteMessage($"\nPB: {PB}");
                                    double newElevation = detectedElevation + PB * slope / 1000;
                                    editor.WriteMessage($"\nPB: {newElevation}");

                                    //Change the elevation
                                    vertices[i].CheckOrOpenForWrite();
                                    vertices[i].Position = new Point3d(
                                        vertices[i].Position.X, vertices[i].Position.Y, newElevation);
                                }
                                else if (i == 0)
                                {
                                    vertices[i].CheckOrOpenForWrite();
                                    vertices[i].Position = new Point3d(
                                        vertices[i].Position.X,
                                        vertices[i].Position.Y,
                                        detectedElevation);
                                }
                            }
                        }
                        else
                        {
                            //Backward iteration
                            editor.WriteMessage($"\nBackward iteration!");
                            double PB = 0;

                            for (int i = endIdx; i > -1; i--)
                            {
                                if (i != endIdx)
                                {
                                    PB += vertices[i + 1].Position.DistanceHorizontalTo(
                                         vertices[i].Position);
                                    editor.WriteMessage($"\nPB: {PB}");
                                    double newElevation = detectedElevation + PB * slope / 1000;
                                    editor.WriteMessage($"\nPB: {newElevation}");
                                    //Change the elevation
                                    vertices[i].CheckOrOpenForWrite();
                                    vertices[i].Position = new Point3d(
                                        vertices[i].Position.X, vertices[i].Position.Y, newElevation);
                                }

                                else if (i == endIdx)
                                {
                                    vertices[i].CheckOrOpenForWrite();
                                    vertices[i].Position = new Point3d(
                                        vertices[i].Position.X,
                                        vertices[i].Position.Y,
                                        detectedElevation);
                                }
                            }
                        }
                        readyLines.Add(line);
                    }

                    //Process lines with missing both end nodes
                    using (Transaction tx2 = localDb.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            editor.WriteMessage($"\nReady lines: {readyLines.Count}, " +
                                                $"Missing both ends: {linesWithMissingBothNodes.Count}.");

                            #region Poly3ds without nodes at ends
                            foreach (Polyline3d pline3dWithMissingNodes in linesWithMissingBothNodes)
                            {
                                //Create 3d polies at both ends to intersect later
                                Oid startPolyId;
                                Oid endPolyId;

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

                                    bool startElevationUnknown = true;
                                    bool endElevationUnknown = true;

                                    var vertices2 = pline3dWithMissingNodes.GetVertices(tx2Other);

                                    Polyline3d[] poly3dOtherArray = readyLines.ToArray();

                                    for (int i = 0; i < poly3dOtherArray.Length; i++)
                                    {
                                        Polyline3d poly3dOther = poly3dOtherArray[i];

                                        #region Detect START elevation
                                        if (startElevationUnknown)
                                        {
                                            double startDistance = startIntersector.GetGeCurve().GetDistanceTo(
                                                                                poly3dOther.GetGeCurve());
                                            if (startDistance < 0.1)
                                            {
                                                PointOnCurve3d[] intPoints = startIntersector.GetGeCurve().GetClosestPointTo(
                                                                             poly3dOther.GetGeCurve());

                                                //Assume one intersection
                                                Point3d result = intPoints.First().Point;
                                                detectedStartElevation = result.Z;
                                                startElevationUnknown = false;
                                            }
                                        }
                                        #endregion

                                        #region Detect END elevation
                                        if (endElevationUnknown)
                                        {
                                            double endDistance = endIntersector.GetGeCurve().GetDistanceTo(
                                                                                poly3dOther.GetGeCurve());
                                            if (endDistance < 0.1)
                                            {
                                                PointOnCurve3d[] intPoints = endIntersector.GetGeCurve().GetClosestPointTo(
                                                                             poly3dOther.GetGeCurve());

                                                //Assume one intersection
                                                Point3d result = intPoints.First().Point;
                                                detectedEndElevation = result.Z;
                                                endElevationUnknown = false;
                                            }
                                        }
                                        #endregion
                                    }

                                    //Interpolate line based on detected elevations
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

                                            for (int i = 0; i < endIdx + 1; i++)
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

                                        for (int i = 0; i < endIdx + 1; i++)
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

                                        ////if (pline3dWithMissingNodes.Handle.ToString() == "692C0")
                                        ////{
                                        //editor.WriteMessage($"\nHandle: {pline3dWithMissingNodes.Handle.ToString()}");
                                        //editor.WriteMessage($"\nEnd elevation: {detectedEndElevation}");
                                        ////}
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
                            tx2.Abort();
                            tx.Abort();
                            editor.WriteMessage("\n" + ex.Message);
                            return;
                        }
                        tx2.Commit();
                    }

                }
                catch (System.Exception ex)
                {
                    tx.Abort();
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

                            Oid newPolyId;

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
                            Oid pline3dToGetElevationsId = entity3.ObjectId;
                            #endregion

                            using (Transaction txOther = localDb.TransactionManager.StartTransaction())
                            {
                                Polyline3d otherPoly3d = pline3dToGetElevationsId.Go<Polyline3d>(txOther);
                                Polyline3d newPoly3d = newPolyId.Go<Polyline3d>(txOther);
                                PointOnCurve3d[] intPoints = newPoly3d.GetGeCurve().GetClosestPointTo(
                                    otherPoly3d.GetGeCurve());

                                //Assume one intersection
                                Point3d result = intPoints.First().Point;
                                elevation = result.Z;

                                //using (Point3dCollection p3dIntCol = new Point3dCollection())
                                //{
                                //    otherPoly3d.IntersectWith(newPoly3d, 0, p3dIntCol, new IntPtr(0), new IntPtr(0));

                                //    if (p3dIntCol.Count > 0 && p3dIntCol.Count < 2)
                                //    {
                                //        foreach (Point3d p3dInt in p3dIntCol)
                                //        {
                                //            //Assume only one intersection
                                //            elevation = p3dInt.Z;
                                //        }
                                //    }
                                //}
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
                Database xRefFjvDB = null;
                Transaction xRefFjvTx = null;
                try
                {
                    #region Load linework
                    DataReferencesOptions dro = new DataReferencesOptions();
                    string projectName = dro.ProjectName;
                    string etapeName = dro.EtapeName;
                    editor.WriteMessage("\n" + GetPathToDataFiles(projectName, etapeName, "Fremtid"));

                    // open the LER dwg database
                    xRefFjvDB = new Database(false, true);

                    xRefFjvDB.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Fremtid"),
                        System.IO.FileShare.Read, false, string.Empty);
                    xRefFjvTx = xRefFjvDB.TransactionManager.StartTransaction();

                    HashSet<Line> lines = xRefFjvDB.HashSetOfType<Line>(xRefFjvTx, true);
                    //HashSet<Spline> splines = xRefFjvDB.HashSetOfType<Spline>(xRefLerTx);
                    HashSet<Polyline> plines = xRefFjvDB.HashSetOfType<Polyline>(xRefFjvTx, true);
                    //HashSet<Polyline3d> plines3d = xRefFjvDB.HashSetOfType<Polyline3d>(xRefLerTx);
                    HashSet<Arc> arcs = xRefFjvDB.HashSetOfType<Arc>(xRefFjvTx, true);
                    editor.WriteMessage($"\nNr. of lines: {lines.Count}");
                    //editor.WriteMessage($"\nNr. of splines: {splines.Count}");
                    editor.WriteMessage($"\nNr. of plines: {plines.Count}");
                    //editor.WriteMessage($"\nNr. of plines3d: {plines3d.Count}");
                    editor.WriteMessage($"\nNr. of arcs: {arcs.Count}");

                    HashSet<Entity> allLinework = new HashSet<Entity>();
                    allLinework.UnionWith(lines.Cast<Entity>().ToHashSet());
                    //allLinework.UnionWith(splines.Cast<Entity>().ToHashSet());
                    allLinework.UnionWith(plines.Cast<Entity>().ToHashSet());
                    //allLinework.UnionWith(plines3d.Cast<Entity>().ToHashSet());
                    allLinework.UnionWith(arcs.Cast<Entity>().ToHashSet());
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
                            Oid ltId = lt.Add(ltr);
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

                    foreach (string name in blockNameList)
                    {
                        var existingBlocks = db.GetBlockReferenceByName(name)
                            .Where(x => x.Layer == localLayerName)
                            .ToList();
                        editor.WriteMessage($"\n{existingBlocks.Count} existing blocks found of name {name}.");
                        foreach (Autodesk.AutoCAD.DatabaseServices.BlockReference br in existingBlocks)
                        {
                            br.CheckOrOpenForWrite();
                            br.Erase(true);
                        }
                    }

                    #endregion

                    string blockName = "";

                    foreach (Entity ent in allLinework)
                    {
                        switch (ent)
                        {
                            case Polyline pline:
                                int numOfVerts = pline.NumberOfVertices - 1;
                                for (int i = 0; i < numOfVerts; i++)
                                {
                                    switch (pline.GetSegmentType(i))
                                    {
                                        case SegmentType.Line:

                                            blockName = "VerticeLine";
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

                                            break;
                                        case SegmentType.Arc:

                                            blockName = "VerticeArc";
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
                                break;
                            case Line line:
                                blockName = "VerticeLine";
                                if (bt.Has(blockName))
                                {
                                    Point3d point3d1 = line.StartPoint;
                                    using (var br = new Autodesk.AutoCAD.DatabaseServices.BlockReference(
                                        new Point3d(point3d1.X, point3d1.Y, 0), bt[blockName]))
                                    {
                                        space.AppendEntity(br);
                                        tx.AddNewlyCreatedDBObject(br, true);
                                        br.Layer = localLayerName;
                                    }

                                    Point3d point3d2 = line.EndPoint;
                                    using (var br = new Autodesk.AutoCAD.DatabaseServices.BlockReference(
                                        new Point3d(point3d2.X, point3d2.Y, 0), bt[blockName]))
                                    {
                                        space.AppendEntity(br);
                                        tx.AddNewlyCreatedDBObject(br, true);
                                        br.Layer = localLayerName;
                                    }
                                }
                                break;
                            case Arc arc:
                                blockName = "VerticeArc";
                                if (bt.Has(blockName))
                                {
                                    Point3d point3d1 = arc.StartPoint;
                                    using (var br = new Autodesk.AutoCAD.DatabaseServices.BlockReference(
                                        new Point3d(point3d1.X, point3d1.Y, 0), bt[blockName]))
                                    {
                                        space.AppendEntity(br);
                                        tx.AddNewlyCreatedDBObject(br, true);
                                        br.Layer = localLayerName;
                                    }

                                    Point3d point3d2 = arc.EndPoint;
                                    using (var br = new Autodesk.AutoCAD.DatabaseServices.BlockReference(
                                        new Point3d(point3d2.X, point3d2.Y, 0), bt[blockName]))
                                    {
                                        space.AppendEntity(br);
                                        tx.AddNewlyCreatedDBObject(br, true);
                                        br.Layer = localLayerName;
                                    }
                                    Point3d samplePoint = arc.GetPointAtDist(arc.Length / 2);
                                    using (var br = new Autodesk.AutoCAD.DatabaseServices.BlockReference(
                                        new Point3d(samplePoint.X, samplePoint.Y, 0), bt[blockName]))
                                    {
                                        space.AppendEntity(br);
                                        tx.AddNewlyCreatedDBObject(br, true);
                                        br.Layer = localLayerName;
                                    }
                                }
                                break;
                            default:
                                break;
                        }
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    xRefFjvTx?.Abort();
                    xRefFjvDB?.Dispose();
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                xRefFjvTx?.Abort();
                xRefFjvDB?.Dispose();
                tx.Commit();
            }
        }

        [CommandMethod("CREATEPOINTSATVERTICES")]
        public void createpointsatvertices()
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
                    HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx);
                    #endregion

                    #region Layer handling
                    string localLayerName = "0-PLDECORATOR";
                    bool localLayerExists = false;

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
                            Oid ltId = lt.Add(ltr);
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
                    BlockTableRecord space = (BlockTableRecord)tx.GetObject(localDb.CurrentSpaceId, OpenMode.ForWrite);
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForWrite) as BlockTable;

                    foreach (Polyline pline in plines)
                    {
                        int numOfVerts = pline.NumberOfVertices - 1;
                        for (int i = 0; i < numOfVerts; i++)
                        {
                            Point3d location = pline.GetPoint3dAt(i);
                            using (var pt = new DBPoint(new Point3d(location.X, location.Y, 0)))
                            {
                                space.AppendEntity(pt);
                                tx.AddNewlyCreatedDBObject(pt, true);
                                pt.Layer = localLayerName;
                            }
                        }
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
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
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                HashSet<Polyline3d> allLinework = new HashSet<Polyline3d>();

                try
                {
                    //Prepare for saving all the time
                    HostApplicationServices hs = HostApplicationServices.Current;

                    string path = hs.FindFile(doc.Name, doc.Database, FindFileHint.Default);

                    #region Prepare OD: Check, create or update Crossing Data
                    Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

                    //Definition of "CrossingData" table
                    string[] columnNames = new string[2] { "Diameter", "Alignment" };
                    string[] columnDescrs = new string[2] { "Diameter of crossing pipe", "Alignment name" };
                    DataType[] dataTypes = new DataType[2] { DataType.Character, DataType.Character };
                    //

                    //Check or create table, or check or create all columns
                    if (DoesTableExist(tables, "CrossingData"))
                    {//Table exists
                        if (DoAllColumnsExist(tables, "CrossingData", columnNames))
                        {
                            //The table is in order, continue to data creation
                        }
                        //If not create missing columns
                        else CreateMissingColumns(tables, "CrossingData", columnNames, columnDescrs, dataTypes);
                    }
                    else
                    {
                        //Table does not exist
                        if (CreateTable(tables, "CrossingData", "Table holding relevant crossing data",
                            columnNames, columnDescrs, dataTypes))
                        {
                            //Table ready for populating with data
                        }
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

                    BlockTableRecord space = (BlockTableRecord)tx.GetObject(localDb.CurrentSpaceId, OpenMode.ForWrite);
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForWrite) as BlockTable;

                    List<Alignment> allAlignments = localDb.ListOfType<Alignment>(tx)
                        .OrderBy(x => x.Name)
                        //.Where(x => x.Name == "20 Rybjerg Allé")
                        .ToList();
                    prdDbg(allAlignments.Count.ToString());
                    HashSet<ProfileView> pvSetExisting = localDb.HashSetOfType<ProfileView>(tx);
                    HashSet<string> pvNames = pvSetExisting.Select(x => x.Name).ToHashSet();
                    //Filter out already created profile views
                    allAlignments = allAlignments.Where(x => !pvNames.Contains(x.Name + "_PV")).OrderBy(x => x.Name).ToList();

                    DataReferencesOptions dro = new DataReferencesOptions();
                    string projectName = dro.ProjectName;
                    string etapeName = dro.EtapeName;

                    editor.WriteMessage("\n" + GetPathToDataFiles(projectName, etapeName, "Ler"));
                    editor.WriteMessage("\n" + GetPathToDataFiles(projectName, etapeName, "Surface"));

                    #region Read surface from file
                    // open the xref database
                    Database xRefSurfaceDB = new Database(false, true);
                    xRefSurfaceDB.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Surface"),
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
                        xRefSurfaceDB.Dispose();
                        return;
                    }
                    #endregion

                    // open the LER dwg database
                    using (Database xRefLerDb = new Database(false, true))
                    {
                        xRefLerDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Ler"),
                                                            System.IO.FileShare.Read, false, string.Empty);

                        using (Transaction xRefLerTx = xRefLerDb.TransactionManager.StartTransaction())
                        {
                            try
                            {
                                HashSet<Polyline3d> remoteLinework = xRefLerDb.HashSetOfType<Polyline3d>(xRefLerTx)
                                                        .Where(x => ReadStringParameterFromDataTable(x.Layer, dtKrydsninger, "Type", 0) != "IGNORE")
                                                        .ToHashSet();

                                #region ModelSpaces
                                Oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(xRefLerDb);
                                Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);

                                ObjectIdCollection remoteP3dIds = new ObjectIdCollection();
                                foreach (Polyline3d p3d in remoteLinework) remoteP3dIds.Add(p3d.ObjectId);

                                IdMapping mapping = new IdMapping();
                                xRefLerDb.WblockCloneObjects(remoteP3dIds, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);
                                //Pick up new objects
                                foreach (IdPair pair in mapping)
                                    if (pair.Value.IsDerivedFrom<Polyline3d>()) allLinework.Add(pair.Value.Go<Polyline3d>(tx));
                                prdDbg($"Number of cloned objects: {allLinework.Count}.");
                                #endregion

                                PointGroupCollection pgs = civilDoc.PointGroups;

                                #region Create profile views

                                Oid profileViewBandSetStyleId = civilDoc.Styles
                                        .ProfileViewBandSetStyles["EG-FG Elevations and Stations"];
                                Oid profileViewStyleId = civilDoc.Styles
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


                                //allAlignments = new List<Alignment>(2) { allAlignments.Where(x => x.Name == "32 Berlingsbakke").FirstOrDefault() };
                                //allAlignments = allAlignments.Where(x => x.Name == "35 Brogårdsvej" ||//)
                                //                             x.Name == "36 Søtoften")
                                //                             .ToList();
                                //allAlignments = allAlignments.GetRange(1, 3);
                                //allAlignments = allAlignments.OrderBy(x => x.Name).ToList().GetRange(20, 11);
                                //allAlignments = allAlignments.OrderBy(x => x.Name).Skip(32).ToList();
                                if (allAlignments.Count == 0) throw new System.Exception("Selection of alignment(s) failed!");

                                foreach (Alignment alignment in allAlignments)
                                {
                                    //Profile view Id init
                                    Oid pvId = Oid.Null;

                                    editor.WriteMessage($"\n_-*-_ | Processing alignment {alignment.Name}. | _-*-_");
                                    System.Windows.Forms.Application.DoEvents();

                                    #region Delete existing points
                                    //TODO: what to do about if the group already exists???
                                    Oid pgId = Oid.Null;
                                    if (pgs.Contains(alignment.Name))
                                    {
                                        pgId = pgs[alignment.Name];

                                        PointGroup pg = tx.GetObject(pgId, OpenMode.ForRead) as PointGroup;

                                        pg.CheckOrOpenForWrite();
                                        pg.Update();
                                        pg.DeletePoints();
                                        //uint[] numbers = pg.GetPointNumbers();
                                        //CogoPointCollection cpc = civilDoc.CogoPoints;
                                        //for (int j = 0; j < numbers.Length; j++)
                                        //{
                                        //    uint number = numbers[j];

                                        //    if (cpc.Contains(number))
                                        //    {
                                        //        cpc.Remove(number);
                                        //    }
                                        //}
                                        pg.Update();
                                        StandardPointGroupQuery spgqEmpty = new StandardPointGroupQuery();
                                        spgqEmpty.IncludeNumbers = "";
                                        pg.SetQuery(spgqEmpty);

                                        pg.Update();
                                    }
                                    #endregion

                                    HashSet<Polyline3d> filteredLinework = FilterForCrossingEntities(allLinework, alignment);

                                    #region Create profile view
                                    #region Calculate point
                                    Point3d insertionPoint = new Point3d(selectedPoint.X, selectedPoint.Y + (index - 1) * -120, 0);
                                    #endregion

                                    // ***** Existing PVs are filtered at start, so the if is redudant *****
                                    ////If ProfileView already exists -> continue
                                    //if (pvSetExisting.Any(x => x.Name == $"{alignment.Name}_PV"))
                                    //{
                                    //    var existingPv = pvSetExisting.Where(x => x.Name == $"{alignment.Name}_PV").FirstOrDefault();
                                    //    if (existingPv == null) throw new System.Exception("Selection of existing PV failed!");
                                    //    pvId = existingPv.Id;
                                    //}
                                    //else
                                    //{
                                    pvId = ProfileView.Create(alignment.ObjectId, insertionPoint,
                                        $"{alignment.Name}_PV", profileViewBandSetStyleId, profileViewStyleId);
                                    //}
                                    index++;
                                    #endregion

                                    #region Create ler data
                                    using (Transaction loopTx = localDb.TransactionManager.StartTransaction())
                                    {
                                        try
                                        {
                                            createlerdataloopwithdeepclone(filteredLinework, alignment, surface, pvId.Go<ProfileView>(loopTx),
                                                                         dtKrydsninger, dtDybde, loopTx, ref pNames);
                                        }
                                        catch (System.Exception e)
                                        {
                                            loopTx.Abort();
                                            xRefLerTx.Abort();
                                            xRefLerDb.Dispose();
                                            xRefSurfaceTx.Abort();
                                            xRefSurfaceDB.Dispose();
                                            editor.WriteMessage($"\n{e.Message}");
                                            return;
                                        }
                                        loopTx.Commit();
                                        //doc.Database.SaveAs(path, true, DwgVersion.Current, doc.Database.SecurityParameters);
                                    }
                                    #endregion
                                }
                            }
                            catch (System.Exception e)
                            {
                                xRefLerTx.Abort();
                                xRefLerDb.Dispose();
                                xRefSurfaceTx.Abort();
                                xRefSurfaceDB.Dispose();
                                editor.WriteMessage($"\n{e.Message}");
                                return;
                            }
                            xRefLerTx.Abort();
                        }
                    }

                    #endregion
                    xRefSurfaceTx.Abort();
                    xRefSurfaceDB.Dispose();

                    //Clean up the cloned linework
                    foreach (Polyline3d p3d in allLinework)
                    {
                        p3d.CheckOrOpenForWrite();
                        p3d.Erase(true);
                    }
                }
                catch (System.Exception e)
                {
                    tx.Abort();
                    editor.WriteMessage($"\n{e.Message}");
                    return;
                }

                tx.Commit();
            }
        }

        [CommandMethod("CREATEPROFILES")]
        public void createprofiles()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                #region Open fremtidig db
                DataReferencesOptions dro = new DataReferencesOptions();
                string projectName = dro.ProjectName;
                string etapeName = dro.EtapeName;

                // open the xref database
                Database fremDb = new Database(false, true);
                fremDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Fremtid"),
                    System.IO.FileShare.Read, false, string.Empty);
                Transaction fremTx = fremDb.TransactionManager.StartTransaction();
                HashSet<Curve> allCurves = fremDb.HashSetOfType<Curve>(fremTx);
                HashSet<BlockReference> allBrs = fremDb.HashSetOfType<BlockReference>(fremTx);
                #endregion

                //////////////////////////////////////
                string draftProfileLayerName = "0-FJV-PROFILE-DRAFT";
                string komponentBlockName = "DRISizeChangeAnno";
                string bueBlockName = "DRIPipeArcAnno";
                //////////////////////////////////////

                try
                {
                    #region Create layer for draft profile
                    using (Transaction txLag = localDb.TransactionManager.StartTransaction())
                    {

                        LayerTable lt = txLag.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;

                        if (!lt.Has(draftProfileLayerName))
                        {
                            LayerTableRecord ltr = new LayerTableRecord();
                            ltr.Name = draftProfileLayerName;
                            ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 40);
                            ltr.LineWeight = LineWeight.LineWeight030;
                            ltr.IsPlottable = false;

                            //Make layertable writable
                            lt.CheckOrOpenForWrite();

                            //Add the new layer to layer table
                            Oid ltId = lt.Add(ltr);
                            txLag.AddNewlyCreatedDBObject(ltr, true);
                        }
                        txLag.Commit();
                    }
                    #endregion

                    #region Common variables
                    BlockTableRecord modelSpace = localDb.GetModelspaceForWrite();
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    Plane plane = new Plane(); //For intersecting
                    HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);
                    #endregion

                    #region Import blocks if missing
                    if (!bt.Has(komponentBlockName) || !bt.Has(bueBlockName))
                    {
                        prdDbg("Block for size annotation is missing! Importing...");
                        Database blockDb = new Database(false, true);
                        blockDb.ReadDwgFile("X:\\AutoCAD DRI - 01 Civil 3D\\DynBlokke\\Symboler.dwg",
                            System.IO.FileShare.Read, false, string.Empty);
                        Transaction blockTx = blockDb.TransactionManager.StartTransaction();

                        Oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(blockDb);
                        Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);

                        BlockTable sourceBt = blockTx.GetObject(blockDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                        ObjectIdCollection idsToClone = new ObjectIdCollection();
                        if (!bt.Has(komponentBlockName)) idsToClone.Add(sourceBt[komponentBlockName]);
                        if (!bt.Has(bueBlockName)) idsToClone.Add(sourceBt[bueBlockName]);

                        IdMapping mapping = new IdMapping();
                        blockDb.WblockCloneObjects(idsToClone, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);
                        blockTx.Commit();
                        blockTx.Dispose();
                        blockDb.Dispose();
                    }
                    #endregion

                    #region Delete previous lines
                    //Delete previous blocks
                    var existingPlines = localDb.HashSetOfType<Polyline>(tx, true).Where(x => x.Layer == draftProfileLayerName).ToHashSet();
                    foreach (Entity ent in existingPlines)
                    {
                        ent.CheckOrOpenForWrite();
                        ent.Erase(true);
                    }
                    #endregion

                    #region Delete previous blocks
                    //Delete previous blocks
                    var existingBlocks = localDb.GetBlockReferenceByName(komponentBlockName);
                    foreach (BlockReference br in existingBlocks)
                    {
                        br.CheckOrOpenForWrite();
                        br.Erase(true);
                    }
                    //Delete previous blocks
                    existingBlocks = localDb.GetBlockReferenceByName(bueBlockName);
                    foreach (BlockReference br in existingBlocks)
                    {
                        br.CheckOrOpenForWrite();
                        br.Erase(true);
                    }
                    #endregion

                    foreach (Alignment al in als)
                    {
                        prdDbg($"\nProcessing: {al.Name}...");
                        #region If exist get surface profile and profile view
                        ObjectIdCollection profileIds = al.GetProfileIds();
                        ObjectIdCollection profileViewIds = al.GetProfileViewIds();

                        ProfileView pv = null;
                        foreach (Oid oid in profileViewIds)
                        {
                            ProfileView pTemp = oid.Go<ProfileView>(tx);
                            if (pTemp.Name == $"{al.Name}_PV") pv = pTemp;
                        }
                        if (pv == null)
                        {
                            prdDbg($"No profile view found for alignment: {al.Name}, skip to next.");
                            continue;
                        }

                        Profile surfaceProfile = null;
                        foreach (Oid oid in profileIds)
                        {
                            Profile pTemp = oid.Go<Profile>(tx);
                            if (pTemp.Name == $"{al.Name}_surface_P") surfaceProfile = pTemp;
                        }
                        if (surfaceProfile == null)
                        {
                            prdDbg($"No surface profile found for alignment: {al.Name}, skip to next.");
                            continue;
                        }
                        prdDbg(pv.Name);
                        prdDbg(surfaceProfile.Name);
                        #endregion

                        #region GetCurvesAndBRs from fremtidig
                        HashSet<Curve> curves = allCurves
                            .Where(x => x.XrecFilter("Alignment", new[] { al.Name }))
                            .ToHashSet();
                        HashSet<BlockReference> brs = allBrs
                            .Where(x => x.XrecFilter("Alignment", new[] { al.Name }))
                            .ToHashSet();
                        prdDbg($"Curves: {curves.Count}, Components: {brs.Count}");
                        #endregion

                        #region Variables and settings
                        Point3d pvOrigin = pv.Location;
                        double originX = pvOrigin.X;
                        double originY = pvOrigin.Y;

                        double pvStStart = pv.StationStart;
                        double pvStEnd = pv.StationEnd;
                        double pvElBottom = pv.ElevationMin;
                        double pvElTop = pv.ElevationMax;
                        double pvLength = pvStEnd - pvStStart;

                        //Settings
                        double weedAngle = 5; //In degrees
                        double weedAngleRad = weedAngle.ToRadians();
                        double DouglasPeuckerTolerance = .05;

                        double stepLength = 0.1;
                        int nrOfSteps = (int)(pvLength / stepLength);
                        #endregion

                        #region Build size array
                        (int dn, double station, double kod)[] sizeArray = new (int dn, double station, double kod)[0];
                        int previousDn = 0;
                        int currentDn = 0;
                        for (int i = 0; i < nrOfSteps + 1; i++)
                        {
                            double curStationBA = pvStStart + stepLength * i;
                            Point3d curSamplePoint = default;
                            try { curSamplePoint = al.GetPointAtDist(curStationBA); }
                            catch (System.Exception) { continue; }

                            HashSet<(Curve curve, double dist, double kappeOd)> curveDistTuples =
                                new HashSet<(Curve curve, double dist, double kappeOd)>();

                            foreach (Curve curve in curves)
                            {
                                if (curve.GetDistanceAtParameter(curve.EndParam) < 1.0) continue;
                                Point3d closestPoint = curve.GetClosestPointTo(curSamplePoint, false);
                                if (closestPoint != default)
                                    curveDistTuples.Add(
                                        (curve, curSamplePoint.DistanceHorizontalTo(closestPoint), GetPipeKOd(curve)));
                            }
                            var result = curveDistTuples.MinBy(x => x.dist).FirstOrDefault();
                            //Detect current dn
                            currentDn = GetPipeDN(result.curve);
                            if (currentDn != previousDn)
                            {
                                //Set the previous segment end station unless there's 0 segments
                                if (sizeArray.Length != 0) sizeArray[sizeArray.Length - 1].station = curStationBA;
                                //Add the new segment
                                sizeArray = sizeArray.Append((currentDn, 0, result.kappeOd)).ToArray();
                            }
                            //Hand over DN to cache in "previous" variable
                            previousDn = currentDn;
                            //TODO: on the last iteration set the last segment distance
                            if (i == nrOfSteps) sizeArray[sizeArray.Length - 1].station = al.Length;
                        }

                        for (int i = 0; i < sizeArray.Length; i++)
                        {
                            prdDbg($"{sizeArray[i].dn.ToString("D3")} || " +
                                   $"{sizeArray[i].station.ToString("0000.00")} || " +
                                   $"{sizeArray[i].kod.ToString("0.0")}");
                        }
                        #endregion

                        #region Place size change blocks
                        //-1 is because of lookahead
                        for (int i = 0; i < sizeArray.Length; i++)
                        {
                            double curStationBL = 0;
                            double sampledSurfaceElevation = 0;
                            double curX = 0, curY = 0;
                            if (i == 0)
                            {
                                sampledSurfaceElevation = SampleProfile(surfaceProfile, curStationBL);
                                curX = originX + curStationBL;
                                curY = originY + sampledSurfaceElevation - pvElBottom;
                                BlockReference brAt0 =
                                    localDb.CreateBlockWithAttributes(komponentBlockName, new Point3d(curX, curY, 0));
                                brAt0.SetAttributeStringValue("LEFTSIZE", "");
                                brAt0.SetAttributeStringValue("RIGHTSIZE", $"DN {sizeArray[0].dn}");
                            }
                            if (i == 0 && sizeArray.Length == 1)
                            {
                                curStationBL = al.Length;
                                sampledSurfaceElevation = SampleProfile(surfaceProfile, curStationBL - .1);
                                curX = originX + curStationBL;
                                curY = originY + sampledSurfaceElevation - pvElBottom;
                                BlockReference brAtEnd =
                                    localDb.CreateBlockWithAttributes(komponentBlockName, new Point3d(curX, curY, 0));
                                brAtEnd.SetAttributeStringValue("LEFTSIZE", $"DN {sizeArray[0].dn}");
                                brAtEnd.SetAttributeStringValue("RIGHTSIZE", "");
                            }
                            if (i == sizeArray.Length - 1) continue;
                            if (sizeArray.Length != 1)
                            {
                                curStationBL = sizeArray[i].station;
                                sampledSurfaceElevation = SampleProfile(surfaceProfile, curStationBL);
                                curX = originX + curStationBL;
                                curY = originY + sampledSurfaceElevation - pvElBottom;
                                BlockReference brInt =
                                    localDb.CreateBlockWithAttributes(komponentBlockName, new Point3d(curX, curY, 0));
                                brInt.SetAttributeStringValue("LEFTSIZE", $"DN {sizeArray[i].dn}");
                                brInt.SetAttributeStringValue("RIGHTSIZE", $"DN {sizeArray[i + 1].dn}");
                            }
                            if (i == sizeArray.Length - 2) //This should give last iteration on arrays larger than 1
                            {
                                curStationBL = sizeArray[i + 1].station;
                                sampledSurfaceElevation = SampleProfile(surfaceProfile, curStationBL - .1);
                                curX = originX + curStationBL;
                                curY = originY + sampledSurfaceElevation - pvElBottom;
                                BlockReference brAtEnd =
                                    localDb.CreateBlockWithAttributes(komponentBlockName, new Point3d(curX, curY, 0));
                                brAtEnd.SetAttributeStringValue("LEFTSIZE", $"DN {sizeArray[i + 1].dn}");
                                brAtEnd.SetAttributeStringValue("RIGHTSIZE", "");
                            }
                        }
                        #endregion

                        #region Local method to sample profiles
                        //Local method to sample profiles
                        double SampleProfile(Profile profile, double station)
                        {
                            double sampledElevation = 0;
                            try { sampledElevation = profile.ElevationAt(station); }
                            catch (System.Exception)
                            {
                                prdDbg($"Station {station} threw an exception when placing size change blocks! Skipping...");
                                return 0;
                            }
                            return sampledElevation;
                        }
                        #endregion

                        #region Place component blocks
                        System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(
                            @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");
                        foreach (BlockReference br in brs)
                        {
                            string type = ReadStringParameterFromDataTable(br.RealName(), fjvKomponenter, "Type", 0);
                            if (type == "Reduktion") continue;
                            //Point3d firstIteration = al.GetClosestPointTo(br.Position, false);
                            //Point3d brLocation = al.GetClosestPointTo(firstIteration, false);
                            Point3d brLocation = al.GetClosestPointTo(br.Position, false);

                            double station;
                            try
                            {
                                station = al.GetDistAtPoint(brLocation);
                            }
                            catch (System.Exception)
                            {
                                prdDbg(br.Position.ToString());
                                prdDbg(brLocation.ToString());
                                throw;
                            }

                            double sampledSurfaceElevation = SampleProfile(surfaceProfile, station);
                            double X = originX + station;
                            double Y = originY + sampledSurfaceElevation - pvElBottom;
                            BlockReference brSign = localDb.CreateBlockWithAttributes(komponentBlockName, new Point3d(X, Y, 0));
                            brSign.SetAttributeStringValue("LEFTSIZE", type);
                            if ((new[] { "Parallelafgrening", "Lige afgrening", "Afgrening med spring", "Påsvejsning" }).Contains(type))
                                brSign.SetAttributeStringValue("RIGHTSIZE", br.XrecReadStringAtIndex("Alignment", 1));
                            else brSign.SetAttributeStringValue("RIGHTSIZE", "");
                        }

                        #endregion

                        #region Sample profile with cover
                        double startStation = 0;
                        double endStation = 0;
                        double curStation = 0;
                        for (int i = 0; i < sizeArray.Length; i++)
                        {
                            List<Point2d> allSteps = new List<Point2d>();
                            //Station management
                            endStation = sizeArray[i].station;
                            double segmentLength = endStation - startStation;
                            nrOfSteps = (int)(segmentLength / stepLength);
                            //Cover depth management
                            int curDn = sizeArray[i].dn;
                            double cover = curDn <= 65 ? 0.6 : 1.0; //CWO info
                            double halfKappeOd = sizeArray[i].kod / 2.0 / 1000.0;
                            prdDbg($"S: {startStation.ToString("0000.0")}, " +
                                   $"E: {endStation.ToString("0000.00")}, " +
                                   $"L: {segmentLength.ToString("0000.00")}, " +
                                   $"Steps: {nrOfSteps.ToString("D5")}");
                            //Sample elevation at each step and create points at current offset from surface
                            for (int j = 0; j < nrOfSteps + 1; j++) //+1 because first step is an "extra" step
                            {
                                curStation = startStation + stepLength * j;
                                double sampledSurfaceElevation = SampleProfile(surfaceProfile, curStation);
                                allSteps.Add(new Point2d(originX + curStation, originY + (sampledSurfaceElevation - pvElBottom - cover - halfKappeOd)));
                            }
                            #region Apply Douglas Peucker reduction
                            List<Point2d> reducedSteps = DouglasPeuckerReduction.DouglasPeuckerReductionMethod(allSteps, DouglasPeuckerTolerance);
                            #endregion

                            #region Draw middle profile
                            Polyline draftProfile = new Polyline();
                            draftProfile.SetDatabaseDefaults();
                            draftProfile.Layer = draftProfileLayerName;
                            for (int j = 0; j < reducedSteps.Count; j++)
                            {
                                var curStep = reducedSteps[j];
                                draftProfile.AddVertexAt(j, curStep, 0, 0, 0);
                            }
                            draftProfile.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                            modelSpace.AppendEntity(draftProfile);
                            tx.AddNewlyCreatedDBObject(draftProfile, true);
                            #endregion

                            #region Draw offset profiles
                            using (DBObjectCollection col = draftProfile.GetOffsetCurves(halfKappeOd))
                            {
                                foreach (var ent in col)
                                {
                                    if (ent is Polyline poly)
                                    {
                                        poly.Color = Color.FromColorIndex(ColorMethod.ByLayer, 256);
                                        modelSpace.AppendEntity(poly);
                                        tx.AddNewlyCreatedDBObject(poly, true);
                                    }
                                }
                            }
                            using (DBObjectCollection col = draftProfile.GetOffsetCurves(-halfKappeOd))
                            {
                                foreach (var ent in col)
                                {
                                    if (ent is Polyline poly)
                                    {
                                        poly.Color = Color.FromColorIndex(ColorMethod.ByLayer, 256);
                                        modelSpace.AppendEntity(poly);
                                        tx.AddNewlyCreatedDBObject(poly, true);
                                    }
                                }
                            }
                            #endregion

                            startStation = sizeArray[i].station;
                        }
                        #endregion

                        #region Test Douglas Peucker reduction
                        ////Test Douglas Peucker reduction
                        //List<double> coverList = new List<double>();
                        //int factor = 10; //Using factor to get more sampling points
                        //for (int i = 0; i < (nrOfSteps + 1) * factor; i++) //+1 because first step is an "extra" step
                        //{
                        //    double sampledSurfaceElevation = 0;

                        //    double curStation = pvStStart + stepLength / factor * i;
                        //    try
                        //    {
                        //        sampledSurfaceElevation = surfaceProfile.ElevationAt(curStation);
                        //    }
                        //    catch (System.Exception)
                        //    {
                        //        //prdDbg($"\nStation {curStation} threw an exception! Skipping...");
                        //        continue;
                        //    }

                        //    //To find point perpendicularly beneath the surface point
                        //    //Use graphical method of intersection with a helper line
                        //    //Cannot find or think of a mathematical solution
                        //    //Create new line to intersect with the draft profile
                        //    Line intersectLine = new Line(
                        //        new Point3d(originX + curStation, originY + (sampledSurfaceElevation - pvElBottom), 0),
                        //        new Point3d(originX + curStation, originY + (sampledSurfaceElevation - pvElBottom) - 10, 0));

                        //    //Intersect and get the intersection point
                        //    Point3dCollection intersectionPoints = new Point3dCollection();

                        //    intersectLine.IntersectWith(draftProfile, 0, plane, intersectionPoints, new IntPtr(0), new IntPtr(0));
                        //    if (intersectionPoints.Count < 1) continue;

                        //    Point3d intersection = intersectionPoints[0];
                        //    coverList.Add(intersection.DistanceTo(
                        //        new Point3d(originX + curStation, originY + (sampledSurfaceElevation - pvElBottom), 0)));
                        //}

                        //prdDbg($"Max. cover: {(int)(coverList.Max() * 1000)} mm");
                        //prdDbg($"Min. cover: {(int)(coverList.Min() * 1000)} mm");
                        //prdDbg($"Average cover: {(int)((coverList.Sum() / coverList.Count) * 1000)} mm");
                        //prdDbg($"Percent values below cover req.: " +
                        //    $"{((coverList.Count(x => x < cover) / Convert.ToDouble(coverList.Count)) * 100.0).ToString("0.##")} %");
                        //#endregion

                        //#region Test Douglas Peucker reduction again
                        //////Test Douglas Peucker reduction
                        ////coverList = new List<double>();

                        ////for (int i = 0; i < (nrOfSteps + 1) * factor; i++) //+1 because first step is an "extra" step
                        ////{
                        ////    double sampledSurfaceElevation = 0;

                        ////    double curStation = pvStStart + stepLength / factor * i;
                        ////    try
                        ////    {
                        ////        sampledSurfaceElevation = surfaceProfile.ElevationAt(curStation);
                        ////    }
                        ////    catch (System.Exception)
                        ////    {
                        ////        //prdDbg($"\nStation {curStation} threw an exception! Skipping...");
                        ////        continue;
                        ////    }

                        ////    //To find point perpendicularly beneath the surface point
                        ////    //Use graphical method of intersection with a helper line
                        ////    //Cannot find or think of a mathematical solution
                        ////    //Create new line to intersect with the draft profile
                        ////    Line intersectLine = new Line(
                        ////        new Point3d(originX + curStation, originY + (sampledSurfaceElevation - pvElBottom), 0),
                        ////        new Point3d(originX + curStation, originY + (sampledSurfaceElevation - pvElBottom) - 10, 0));

                        ////    //Intersect and get the intersection point
                        ////    Point3dCollection intersectionPoints = new Point3dCollection();

                        ////    intersectLine.IntersectWith(draftProfile, 0, plane, intersectionPoints, new IntPtr(0), new IntPtr(0));
                        ////    if (intersectionPoints.Count < 1) continue;

                        ////    Point3d intersection = intersectionPoints[0];
                        ////    coverList.Add(intersection.DistanceTo(
                        ////        new Point3d(originX + curStation, originY + (sampledSurfaceElevation - pvElBottom), 0)));
                        ////}

                        ////prdDbg("After fitting polyline:");
                        ////prdDbg($"Max. cover: {(int)(coverList.Max() * 1000)} mm");
                        ////prdDbg($"Min. cover: {(int)(coverList.Min() * 1000)} mm");
                        ////prdDbg($"Average cover: {(int)((coverList.Sum() / coverList.Count) * 1000)} mm");
                        ////prdDbg($"Percent values below cover req.: " +
                        ////    $"{((coverList.Count(x => x < cover) / Convert.ToDouble(coverList.Count)) * 100.0).ToString("0.##")} %");
                        //#endregion

                        #endregion

                        #region Find curves and annotate
                        foreach (Curve curve1 in curves)
                        {
                            if (curve1 is Polyline pline)
                            {
                                //Detect arcs and determine if it is a buerør or not
                                for (int i = 0; i < pline.NumberOfVertices; i++)
                                {
                                    TypeOfSegment tos;
                                    double bulge = pline.GetBulgeAt(i);
                                    if (bulge == 0) tos = TypeOfSegment.Straight;
                                    else
                                    {
                                        //Calculate radius
                                        double u = pline.GetPoint2dAt(i).GetDistanceTo(pline.GetPoint2dAt(i + 1));
                                        double radius = u * ((1 + bulge.Pow(2)) / (4 * Math.Abs(bulge)));
                                        double minRadius = GetPipeMinElasticRadius(pline);

                                        if (radius < minRadius) tos = TypeOfSegment.CurvedPipe;
                                        else tos = TypeOfSegment.ElasticArc;

                                        //Acquire start and end stations
                                        double curveStartStation = al.GetDistAtPoint(al.GetClosestPointTo(pline.GetPoint3dAt(i), false));
                                        double curveEndStation = al.GetDistAtPoint(al.GetClosestPointTo(pline.GetPoint3dAt(i + 1), false));
                                        double length = curveEndStation - curveStartStation;
                                        double midStation = curveStartStation + length / 2;

                                        double sampledSurfaceElevation = 0;
                                        double curX = 0, curY = 0;

                                        sampledSurfaceElevation = SampleProfile(surfaceProfile, midStation);
                                        curX = originX + midStation;
                                        curY = originY + sampledSurfaceElevation - pvElBottom;
                                        BlockReference brCurve =
                                            localDb.CreateBlockWithAttributes(bueBlockName, new Point3d(curX, curY, 0));

                                        DynamicBlockReferencePropertyCollection dbrpc = brCurve.DynamicBlockReferencePropertyCollection;
                                        foreach (DynamicBlockReferenceProperty dbrp in dbrpc)
                                        {
                                            if (dbrp.PropertyName == "Length")
                                            {
                                                prdDbg(length.ToString());
                                                dbrp.Value = Math.Abs(length);
                                            }
                                        }

                                        switch (tos)
                                        {
                                            case TypeOfSegment.ElasticArc:
                                                brCurve.SetAttributeStringValue("TEXT", "Elastisk bue");
                                                break;
                                            case TypeOfSegment.CurvedPipe:
                                                brCurve.SetAttributeStringValue("TEXT", "Buerør");
                                                break;
                                            default:
                                                break;
                                        }
                                    }
                                }
                            }
                        }
                        #endregion
                    }
                }
                catch (System.Exception ex)
                {
                    fremTx.Abort();
                    fremTx.Dispose();
                    fremDb.Dispose();
                    tx.Abort();
                    prdDbg(ex.ExceptionInfo());
                    prdDbg(ex.ToString());
                    return;
                }
                fremTx.Abort();
                fremTx.Dispose();
                fremDb.Dispose();
                tx.Commit();
            }
        }

        [CommandMethod("DELETEWELDPOINTS")]
        [CommandMethod("DWP")]
        public void deleteweldpoints()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                string blockLayerName = "0-SVEJSEPKT";
                string blockName = "SVEJSEPUNKT";
                string textLayerName = "0-DEBUG-TXT";
                //////////////////////////////////////

                #region Delete previous blocks
                //Delete previous blocks
                var existingBlocks = localDb.GetBlockReferenceByName(blockName);
                foreach (BlockReference br in existingBlocks)
                {
                    br.CheckOrOpenForWrite();
                    br.Erase(true);
                }
                #endregion
                tx.Commit();
            }
        }

        [CommandMethod("CREATEWELDPOINTS")]
        [CommandMethod("CWP")]
        public void createweldpoints()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                #region Open alignment db
                DataReferencesOptions dro = new DataReferencesOptions();
                string projectName = dro.ProjectName;
                string etapeName = dro.EtapeName;

                // open the xref database
                Database alDb = new Database(false, true);
                alDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Alignments"),
                    System.IO.FileShare.Read, false, string.Empty);
                Transaction alTx = alDb.TransactionManager.StartTransaction();
                HashSet<Alignment> als = alDb.HashSetOfType<Alignment>(alTx);
                #endregion

                //////////////////////////////////////
                string blockLayerName = "0-SVEJSEPKT";
                string blockName = "SVEJSEPUNKT";
                string textLayerName = "0-DEBUG-TXT";
                //////////////////////////////////////

                //******************************//
                PropertySetManager.DefinedSets propertySetName =
                    PropertySetManager.DefinedSets.DriPipelineData;
                string belongsToAlignmentProperty = "BelongsToAlignment";
                string branchesOffToAlignmentProperty = "BranchesOffToAlignment";
                //******************************//

                #region Initialize property set
                PropertySetManager psm = new PropertySetManager(
                    localDb,
                    propertySetName);
                #endregion

                #region Delete previous blocks
                //Delete previous blocks
                var existingBlocks = localDb.GetBlockReferenceByName(blockName);
                foreach (BlockReference br in existingBlocks)
                {
                    br.CheckOrOpenForWrite();
                    br.Erase(true);
                }
                #endregion

                try
                {
                    #region Create layer for weld blocks and
                    Utils.CheckOrCreateLayer(localDb, blockLayerName);
                    Utils.CheckOrCreateLayer(localDb, textLayerName);
                    #endregion

                    #region Read components file
                    System.Data.DataTable komponenter = CsvReader.ReadCsvToDataTable(
                            @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");
                    #endregion

                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                    #region Import weld block if missing
                    if (!bt.Has(blockName))
                    {
                        prdDbg("Block for weld annotation is missing! Importing...");
                        Database blockDb = new Database(false, true);
                        blockDb.ReadDwgFile("X:\\AutoCAD DRI - 01 Civil 3D\\DynBlokke\\Symboler.dwg",
                            System.IO.FileShare.Read, false, string.Empty);
                        Transaction blockTx = blockDb.TransactionManager.StartTransaction();

                        Oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(blockDb);
                        Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);

                        BlockTable sourceBt = blockTx.GetObject(blockDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                        ObjectIdCollection idsToClone = new ObjectIdCollection();
                        idsToClone.Add(sourceBt[blockName]);

                        IdMapping mapping = new IdMapping();
                        blockDb.WblockCloneObjects(idsToClone, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);
                        blockTx.Commit();
                        blockTx.Dispose();
                        blockDb.Dispose();
                    }
                    #endregion

                    //List to gather ALL weld points
                    var wps = new List<WeldPointData>();

                    foreach (Alignment al in als.OrderBy(x => x.Name))
                    {
                        ////////////////////////////////////////////
                        //if (al.Name != "01 Rybjerg Allé") continue;
                        ////////////////////////////////////////////
                        prdDbg($"\nProcessing: {al.Name}...");

                        #region GetCurvesAndBRs from fremtidig
                        HashSet<Curve> curves = localDb.ListOfType<Curve>(tx, true)
                            .Where(x => psm.FilterPropetyString(x, belongsToAlignmentProperty, al.Name))
                            .ToHashSet();
                        HashSet<BlockReference> brs = localDb.ListOfType<BlockReference>(tx, true)
                            .Where(x => psm.FilterPropetyString(x, belongsToAlignmentProperty, al.Name))
                            .ToHashSet();
                        prdDbg($"Curves: {curves.Count}, Components: {brs.Count}");
                        #endregion

                        TypeOfIteration iterType = 0;

                        //Sort curves according to their DN -> bigger DN at start
                        Queue<Curve> kø = Utils.GetSortedQueue(localDb, al, curves, ref iterType);

                        #region Gather weldpoints for curves
                        double pipeStdLength = 0;
                        while (kø.Count > 0)
                        {
                            Curve curve = kø.Dequeue();
                            pipeStdLength = GetPipeStdLength(curve);
                            double pipeLength = curve.GetDistanceAtParameter(curve.EndParam);
                            double division = pipeLength / pipeStdLength;
                            int nrOfSections = (int)division;
                            double remainder = division - nrOfSections;

                            //if (string.Equals(curve.Handle.ToString(), "19caf", StringComparison.OrdinalIgnoreCase))
                            //{
                            //    prdDbg($"pipeStdLength: {pipeStdLength}");
                            //    prdDbg($"pipeLength: {pipeLength}");
                            //    prdDbg($"Division: {division}");
                            //    prdDbg($"nrOfSections: {nrOfSections}");
                            //    prdDbg($"remainder: {remainder}");
                            //    prdDbg($"QA: {nrOfSections * pipeStdLength + remainder * pipeStdLength} = {pipeLength}");
                            //}

                            for (int j = 1; j < nrOfSections + 1; j++)
                            {//1 to skip start, which is handled separately
                                Point3d wPt = curve.GetPointAtDist(j * pipeStdLength);
                                Point3d tempPt = al.GetClosestPointTo(wPt, false);
                                //double station = al.GetDistAtPoint(tempPt);
                                double station;
                                try
                                {
                                    station = al.GetDistAtPoint(tempPt);
                                }
                                catch (System.Exception)
                                {
                                    prdDbg(al.Name);
                                    prdDbg(wPt.ToString());
                                    prdDbg(tempPt.ToString());
                                    prdDbg(curve.Handle.ToString());
                                    throw;
                                }
                                //Create weldpoint
                                wps.Add(new WeldPointData()
                                {
                                    WeldPoint = wPt,
                                    Alignment = al,
                                    IterationType = iterType,
                                    Station = station,
                                    SourceEntity = curve,
                                    DN = GetPipeDN(curve),
                                    System = GetPipeSystem(curve)
                                });
                            }

                            //Handle start and end points separately
                            wps.Add(new WeldPointData()
                            {
                                WeldPoint = curve.GetPointAtParameter(curve.StartParam),
                                Alignment = al,
                                IterationType = iterType,
                                Station = al.GetDistAtPoint(al.GetClosestPointTo(
                                        curve.GetPointAtParameter(curve.StartParam), false)),
                                SourceEntity = curve,
                                DN = GetPipeDN(curve),
                                System = GetPipeSystem(curve)
                            });
                            wps.Add(new WeldPointData()
                            {
                                WeldPoint = curve.GetPointAtParameter(curve.EndParam),
                                Alignment = al,
                                IterationType = iterType,
                                Station = al.GetDistAtPoint(al.GetClosestPointTo(
                                        curve.GetPointAtParameter(curve.EndParam), false)),
                                SourceEntity = curve,
                                DN = GetPipeDN(curve),
                                System = GetPipeSystem(curve)
                            });

                            #region Debug
                            //if (curve is Polyline pline)
                            //{
                            //    Point3d midPoint = pline.GetPointAtDist(pline.Length / 2);
                            //    DBText text = new DBText();
                            //    text.SetDatabaseDefaults();
                            //    text.TextString = (i + 1).ToString("D2");
                            //    text.Height = 10;
                            //    text.Position = midPoint;
                            //    text.Layer = textLayerName;
                            //    text.AddEntityToDbModelSpace(localDb);
                            //} 
                            #endregion
                        }
                        #endregion

                        #region Gather weldpoints for blocks
                        foreach (BlockReference br in brs)
                        {
                            BlockTableRecord btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);
                            foreach (Oid oid in btr)
                            {
                                if (!oid.IsDerivedFrom<BlockReference>()) continue;
                                BlockReference nestedBr = oid.Go<BlockReference>(tx);
                                if (!nestedBr.Name.Contains("MuffeIntern")) continue;
                                Point3d wPt = nestedBr.Position;
                                wPt = wPt.TransformBy(br.BlockTransform);

                                #region Read DN
                                int DN = 0;
                                bool parseSuccess = false;
                                if (nestedBr.Name.Contains("BRANCH"))
                                {
                                    parseSuccess = int.TryParse(
                                        ODDataReader.DynKomponenter.ReadComponentDN2(br, komponenter).StrValue, out DN);
                                }
                                else //Else catches "MAIN" and ordinary case
                                {
                                    parseSuccess = int.TryParse(
                                        ODDataReader.DynKomponenter.ReadComponentDN1(br, komponenter).StrValue, out DN);
                                }

                                if (!parseSuccess)
                                {
                                    prdDbg($"ERROR: Parsing of DN failed for block handle: {br.Handle}! " +
                                        $"Returned value: {ODDataReader.DynKomponenter.ReadComponentDN1(br, komponenter).StrValue}");
                                }
                                #endregion

                                #region Read System
                                string system = ODDataReader.DynKomponenter.ReadComponentSystem(br, komponenter).StrValue;

                                if (system.IsNoE())
                                {
                                    prdDbg($"ERROR: Parsing of DN failed for block handle: {br.Handle}!");
                                    system = "";
                                }
                                #endregion

                                #region Determine correct alignment name
                                //This is to mitigate parallelafgreninger which place
                                //Branch weld on the wrong alignment
                                Alignment alignment = al;
                                if (br.RealName() == "PA TWIN S3" ||
                                    br.RealName() == "T ENKELT S3" ||
                                    br.RealName() == "T TWIN S3")
                                {
                                    HashSet<(double dist, Alignment al)> alDistTuples =
                                        new HashSet<(double, Alignment)>();
                                    try
                                    {
                                        foreach (Alignment newAl in als)
                                        {
                                            if (newAl.Length < 1) continue;
                                            Point3d closestPoint = newAl.GetClosestPointTo(wPt, false);
                                            if (closestPoint != null)
                                            {
                                                alDistTuples.Add((wPt.DistanceHorizontalTo(closestPoint), newAl));
                                            }
                                        }
                                    }
                                    catch (System.Exception)
                                    {
                                        prdDbg("Error in GetClosestPointTo -> loop incomplete!");
                                    }

                                    alignment = alDistTuples.MinBy(x => x.dist).FirstOrDefault().al;
                                }
                                #endregion

                                wps.Add(new WeldPointData()
                                {
                                    WeldPoint = wPt,
                                    Alignment = alignment,
                                    IterationType = iterType,
                                    Station = al.GetDistAtPoint(al.GetClosestPointTo(wPt, false)),
                                    SourceEntity = br,
                                    DN = DN,
                                    System = system
                                });
                            }
                        }
                        #endregion

                        System.Windows.Forms.Application.DoEvents();
                    }

                    #region Place weldpoints
                    var ordered = wps.OrderBy(x => x.WeldPoint.X).ThenBy(x => x.WeldPoint.Y);
                    IEnumerable<IGrouping<WeldPointData, WeldPointData>> clusters
                        = ordered.GroupByCluster((x, y) => GetDistance(x, y), 0.02);

                    double GetDistance(WeldPointData first, WeldPointData second)
                    {
                        return first.WeldPoint.DistanceHorizontalTo(second.WeldPoint);
                    }

                    var distinct = clusters.Select(x => x.First());
                    var groupedByAlignment = distinct.GroupBy(x => x.Alignment.Name);

                    //Prepare modelspace
                    BlockTableRecord modelSpace = localDb.GetModelspaceForWrite();
                    modelSpace.CheckOrOpenForWrite();
                    //Prepare block table record
                    if (!bt.Has(blockName)) throw new System.Exception("Block for weld points is missing!");
                    Oid btrId = bt[blockName];
                    BlockTableRecord btrWp = btrId.Go<BlockTableRecord>(tx);
                    List<AttributeDefinition> attDefs = new List<AttributeDefinition>();
                    foreach (Oid arOid in btrWp)
                    {
                        if (!arOid.IsDerivedFrom<AttributeDefinition>()) continue;
                        AttributeDefinition at = arOid.Go<AttributeDefinition>(tx);
                        if (!at.Constant) attDefs.Add(at);
                    }

                    foreach (var alGroup in groupedByAlignment.OrderBy(x => x.Key))
                    {
                        prdDbg($"Placing welds for alignment: {alGroup.First().Alignment.Name}...");
                        System.Windows.Forms.Application.DoEvents();
                        IOrderedEnumerable<WeldPointData> orderedByDist;
                        if (alGroup.First().IterationType == TypeOfIteration.Forward)
                            orderedByDist = alGroup.OrderBy(x => x.Station);
                        else orderedByDist = alGroup.OrderByDescending(x => x.Station);

                        Regex regex = new Regex(@"(?<number>^\d\d)");
                        string currentPipelineNumber = "";
                        if (regex.IsMatch(alGroup.First().Alignment.Name))
                        {
                            Match match = regex.Match(alGroup.First().Alignment.Name);
                            currentPipelineNumber = match.Groups["number"].Value;
                        }

                        int idx = 1;
                        foreach (var wp in orderedByDist)
                        {
                            Vector3d deriv = wp.Alignment.GetFirstDerivative(
                                wp.Alignment.GetClosestPointTo(wp.WeldPoint, false));
                            double rotation = Math.Atan2(deriv.Y, deriv.X);
                            //BlockReference wpBr = localDb.CreateBlockWithAttributes(blockName, wp.WeldPoint, rotation);
                            var wpBr = new BlockReference(wp.WeldPoint, btrId);
                            modelSpace.AppendEntity(wpBr);
                            tx.AddNewlyCreatedDBObject(wpBr, true);
                            wpBr.Rotation = rotation;
                            wpBr.Layer = blockLayerName;

                            foreach (AttributeDefinition attDef in attDefs)
                            {
                                AttributeReference atRef = new AttributeReference();
                                atRef.SetAttributeFromBlock(attDef, wpBr.BlockTransform);
                                atRef.Position = attDef.Position.TransformBy(wpBr.BlockTransform);
                                atRef.TextString = attDef.getTextWithFieldCodes();
                                wpBr.AttributeCollection.AppendAttribute(atRef);
                                tx.AddNewlyCreatedDBObject(atRef, true);
                            }

                            wpBr.SetAttributeStringValue("NUMMER", currentPipelineNumber + "." + idx.ToString("D3"));

                            //if (idx == 1) DisplayDynBlockProperties(editor, wpBr, wpBr.Name);
                            SetDynBlockProperty(wpBr, "Type", wp.DN.ToString());
                            SetDynBlockProperty(wpBr, "System", wp.System);

                            psm.GetOrAttachPropertySet(wpBr);
                            psm.WritePropertyString(belongsToAlignmentProperty, wp.Alignment.Name);

                            idx++;
                        }
                    }
                    #endregion

                    //BlockTableRecord btr = bt[blockName].Go<BlockTableRecord>(tx);
                    //btr.SynchronizeAttributes();
                }
                catch (System.Exception ex)
                {
                    alTx.Abort();
                    alTx.Dispose();
                    alDb.Dispose();
                    tx.Abort();
                    prdDbg(ex.ExceptionInfo());
                    prdDbg(ex.ToString());
                    return;
                }
                alTx.Abort();
                alTx.Dispose();
                alDb.Dispose();
                tx.Commit();
            }
        }

        [CommandMethod("CORRECTPIPESTOCUTLENGTHS")]
        [CommandMethod("CPTCL")]
        public void correctpipestocutlengths()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                #region Open alignment db
                DataReferencesOptions dro = new DataReferencesOptions();
                string projectName = dro.ProjectName;
                string etapeName = dro.EtapeName;

                // open the xref database
                Database alDb = new Database(false, true);
                alDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Alignments"),
                    System.IO.FileShare.Read, false, string.Empty);
                Transaction alTx = alDb.TransactionManager.StartTransaction();
                HashSet<Alignment> als = alDb.HashSetOfType<Alignment>(alTx);
                #endregion

                try
                {
                    #region Read components file
                    System.Data.DataTable komponenter = CsvReader.ReadCsvToDataTable(
                            @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");
                    #endregion

                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                    #region Propertyset init
                    //******************************//
                    PropertySetManager.DefinedSets propertySetName =
                        PropertySetManager.DefinedSets.DriPipelineData;
                    string belongsToAlignmentProperty = "BelongsToAlignment";
                    //******************************//

                    PropertySetManager psm = new PropertySetManager(
                        localDb,
                        propertySetName);
                    #endregion

                    foreach (Alignment al in als.OrderBy(x => x.Name))
                    {
                        #region GetCurvesAndBRs
                        prdDbg($"\nProcessing: {al.Name}...");
                        HashSet<Curve> curves = localDb.ListOfType<Curve>(tx, true)
                            .Where(x => psm.FilterPropetyString(x, belongsToAlignmentProperty, al.Name))
                            .ToHashSet();
                        HashSet<BlockReference> brs = localDb.ListOfType<BlockReference>(tx, true)
                            .Where(x => psm.FilterPropetyString(x, belongsToAlignmentProperty, al.Name))
                            .Where(x => x.RealName() != "SVEJSEPUNKT")
                            .ToHashSet();
                        prdDbg($"Curves: {curves.Count}, Components: {brs.Count}");

                        HashSet<(Entity ent, double dist)> distTuples = new HashSet<(Entity ent, double dist)>();
                        #endregion

                        //Sort curves according to their DN -> bigger DN at start
                        TypeOfIteration iterType = 0;
                        Queue<Curve> kø = Utils.GetSortedQueue(localDb, al, curves, ref iterType);
                        LinkedList<Curve> ll = new LinkedList<Curve>(kø.ToList());

                        #region Analyze curves and correct lengths
                        while (ll.Count > 0)
                        {
                            Curve curve = ll.First.Value;
                            curve.CheckOrOpenForWrite();
                            ll.RemoveFirst();

                            //Detect the component at curve end
                            //If it is a transition --> analyze and correct
                            //If not --> continue
                            Point3d endPoint = curve.GetPointAtParameter(curve.EndParam);
                            var distsToEndPoint = CreateDistTuples(endPoint, brs).OrderBy(x => x.dist);
                            var first = distsToEndPoint.First();
                            var nearestBlock = first.ent as BlockReference;
                            if (nearestBlock.RealName() != "RED KDLR" &&
                                nearestBlock.RealName() != "RED KDLR x2")
                                continue;
                            //Limit the distance or buerør will give false true
                            if (first.dist > 0.5)
                                continue;

                            double pipeStdLegnth = GetPipeStdLength(curve);
                            double pipeLength = curve.GetDistanceAtParameter(curve.EndParam);
                            double division = pipeLength / pipeStdLegnth;
                            int nrOfSections = (int)division;
                            double modulo = division - nrOfSections;
                            double remainder = modulo * pipeStdLegnth;
                            double missingLength = pipeStdLegnth - remainder;

                            Polyline pline = curve as Polyline;
                            pline.CheckOrOpenForWrite();
                            pline.ConstantWidth = pline.GetStartWidthAt(0);
                            double globalWidth = pline.ConstantWidth;
                            //prdDbg($"Width: {globalWidth}");

                            if (remainder > 1e-3 && pipeStdLegnth - remainder > 1e-3)
                            {
                                prdDbg($"Remainder: {remainder}, missing length: {missingLength}");
                                double transitionLength = GetTransitionLength(tx, nearestBlock);

                                Curve nextCurve = null;
                                nextCurve = ll.First?.Value;
                                if (nextCurve == null) continue;
                                nextCurve.CheckOrOpenForWrite();
                                ll.RemoveFirst();

                                if (missingLength <= transitionLength)
                                {
                                    prdDbg("Case 1");
                                    //Case where the point is in transition
                                    //Extend the current curve
                                    curve.CheckOrOpenForWrite();
                                    Vector3d v = curve.GetFirstDerivative(endPoint).GetNormal();
                                    Point3d newEndPoint = endPoint + v * missingLength;
                                    curve.Extend(false, newEndPoint);

                                    //Move block
                                    nearestBlock.CheckOrOpenForWrite();
                                    Vector3d moveVector = endPoint.GetVectorTo(newEndPoint);
                                    nearestBlock.TransformBy(Matrix3d.Displacement(moveVector));

                                    //Split the piece from next curve
                                    List<double> splitPars = new List<double>();
                                    splitPars.Add(nextCurve.GetParameterAtDistance(missingLength));
                                    try
                                    {
                                        DBObjectCollection objs = nextCurve
                                            .GetSplitCurves(new DoubleCollection(splitPars.ToArray()));
                                        Curve toAdd = objs[1] as Curve;
                                        toAdd.AddEntityToDbModelSpace(localDb);
                                        //Add the newly created curve to linkedlist
                                        ll.AddFirst(toAdd);

                                        psm.CopyAllProperties(nextCurve, toAdd);

                                        nextCurve.CheckOrOpenForWrite();
                                        nextCurve.Erase(true);
                                    }
                                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                                    {
                                        Application.ShowAlertDialog(ex.Message + "\n" + ex.StackTrace);
                                        throw new System.Exception("Splitting of pline failed!");
                                    }

                                    //Yellow line
                                    //When the remainder is shorter than the length of transition
                                    Line line = new Line(new Point3d(), newEndPoint);
                                    line.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);
                                    line.AddEntityToDbModelSpace(localDb);
                                }
                                else if (missingLength > transitionLength && missingLength < pipeStdLegnth - 2)
                                {
                                    prdDbg("Case 2");
                                    //Case where the point is on the next curve
                                    //Find the location of new endpoint
                                    double newEndDist = missingLength - transitionLength;
                                    //Catch a case where the missing length is longer than the next
                                    //Curves length
                                    if (newEndDist > nextCurve.GetDistanceAtParameter(nextCurve.EndParam))
                                    {
                                        prdDbg("Case 2.1 (Next line shorter than needed->Continue)");

                                        //Red line
                                        Line line2 = new Line(new Point3d(), endPoint);
                                        line2.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                        line2.AddEntityToDbModelSpace(localDb);

                                        //Return the curve to the queue
                                        ll.AddFirst(nextCurve);
                                        continue;
                                    }

                                    Point3d newEndPoint = nextCurve.GetPointAtDist(newEndDist);
                                    double parameter = Math.Truncate(nextCurve.GetParameterAtPoint(newEndPoint));
                                    SegmentType st = ((Polyline)nextCurve).GetSegmentType((int)parameter);

                                    if (st == SegmentType.Arc)
                                    {
                                        prdDbg("Case 2.2 (Next line is an arc -> abort)");
                                        //Red line
                                        //When segment is an arc -- abort -- must be done manually
                                        //Generally a transition must not be on a curve
                                        Line line2 = new Line(new Point3d(), newEndPoint);
                                        line2.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                        line2.AddEntityToDbModelSpace(localDb);

                                        //Return the curve to the queue
                                        ll.AddFirst(nextCurve);
                                        continue;
                                    }
                                    else
                                    {
                                        //Move block and rotate
                                        nearestBlock.CheckOrOpenForWrite();
                                        Vector3d moveVector = endPoint.GetVectorTo(newEndPoint);
                                        nearestBlock.TransformBy(Matrix3d.Displacement(moveVector));

                                        //prdDbg($"L: 6102: {nearestBlock.Handle} - {newEndDist}");
                                        //Vector3d deriv = nextCurve.GetFirstDerivative(
                                        //    nextCurve.GetPointAtDist(newEndDist + transitionLength / 2));
                                        //double rotation = Math.Atan2(deriv.Y, deriv.X) - Math.PI / 2;
                                        //nearestBlock.Rotation = rotation;

                                        //Split the piece from next curve
                                        List<double> splitPars = new List<double>();
                                        splitPars.Add(nextCurve.GetParameterAtDistance(newEndDist));
                                        splitPars.Add(nextCurve.GetParameterAtDistance(newEndDist + transitionLength));
                                        try
                                        {
                                            DBObjectCollection objs = nextCurve
                                                .GetSplitCurves(new DoubleCollection(splitPars.ToArray()));
                                            Polyline toMerge = objs[0] as Polyline;

                                            for (int i = 0; i < toMerge.NumberOfVertices; i++)
                                            {
                                                Point2d cp = new Point2d(toMerge.GetPoint3dAt(i).X, toMerge.GetPoint3dAt(i).Y);
                                                pline.AddVertexAt(
                                                    pline.NumberOfVertices,
                                                    cp, toMerge.GetBulgeAt(i), 0, 0);
                                            }
                                            pline.ConstantWidth = globalWidth;
                                            RemoveColinearVerticesPolyline(pline);

                                            Curve toAdd = objs[2] as Curve;
                                            //Add the newly created curve to linkedlist
                                            toAdd.AddEntityToDbModelSpace(localDb);

                                            psm.CopyAllProperties(nextCurve, toAdd);

                                            ll.AddFirst(toAdd);

                                            nextCurve.CheckOrOpenForWrite();
                                            nextCurve.Erase(true);
                                        }
                                        catch (Autodesk.AutoCAD.Runtime.Exception ex)
                                        {
                                            Application.ShowAlertDialog(ex.Message + "\n" + ex.StackTrace);
                                            throw new System.Exception("Splitting of pline failed!");
                                        }

                                        //Cyan line
                                        //When the remainder is longer than the length of transition
                                        Line line = new Line(new Point3d(), newEndPoint);
                                        line.Color = Color.FromColorIndex(ColorMethod.ByAci, 4);
                                        line.AddEntityToDbModelSpace(localDb);
                                    }
                                }
                                else if (missingLength >= pipeStdLegnth - 2)
                                {
                                    prdDbg("Case 4 (Missing length is small -> moving backwards).");
                                    //Take care to reverse them back!!!
                                    curve.ReverseCurve();
                                    nextCurve.ReverseCurve();
                                    //Exchange references to be able to reuse code with minimal changes
                                    var temp = curve;
                                    curve = nextCurve;
                                    nextCurve = temp;

                                    //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                                    //*******************************************
                                    //Missing length redefined!!!
                                    missingLength = pipeStdLegnth - missingLength;
                                    //End point redefined!!!
                                    endPoint = curve.GetPointAtParameter(curve.EndParam);

                                    //Case where the transition is moved back instead of forward
                                    if (missingLength <= transitionLength)
                                    {
                                        prdDbg($"Case 4.1 (MissingLength {missingLength} is SHORTER than transition.");
                                        //Case where the point is in transition
                                        //Extend the current curve
                                        curve.CheckOrOpenForWrite();
                                        Vector3d v = curve.GetFirstDerivative(endPoint).GetNormal();
                                        Point3d newEndPoint = endPoint + v * missingLength;
                                        curve.Extend(false, newEndPoint);

                                        //Move block
                                        nearestBlock.CheckOrOpenForWrite();
                                        Vector3d moveVector = endPoint.GetVectorTo(newEndPoint);
                                        nearestBlock.TransformBy(Matrix3d.Displacement(moveVector));

                                        //Split the piece from next curve
                                        List<double> splitPars = new List<double>();
                                        splitPars.Add(nextCurve.GetParameterAtDistance(missingLength));
                                        try
                                        {
                                            DBObjectCollection objs = nextCurve
                                                .GetSplitCurves(new DoubleCollection(splitPars.ToArray()));
                                            Curve toAdd = objs[1] as Curve;
                                            toAdd.AddEntityToDbModelSpace(localDb);
                                            //Add the newly created curve to linkedlist
                                            //REMEMBER: it is reversed still!
                                            ll.AddFirst(curve);

                                            psm.CopyAllProperties(nextCurve, toAdd);

                                            nextCurve.CheckOrOpenForWrite();
                                            nextCurve.Erase(true);

                                            //Reverse curves back again
                                            toAdd.ReverseCurve();
                                            curve.ReverseCurve();
                                        }
                                        catch (Autodesk.AutoCAD.Runtime.Exception ex)
                                        {
                                            Application.ShowAlertDialog(ex.Message + "\n" + ex.StackTrace);
                                            throw new System.Exception("Splitting of pline failed!");
                                        }

                                        //Magenta line
                                        //When the remainder is shorter than the length of transition
                                        //And THE MOVEMENT IS REVERSED!
                                        Line line = new Line(new Point3d(), newEndPoint);
                                        line.Color = Color.FromColorIndex(ColorMethod.ByAci, 6);
                                        line.AddEntityToDbModelSpace(localDb);
                                    }
                                    else if (missingLength > transitionLength && missingLength <= 2)
                                    {
                                        prdDbg("Case 4.2 (MissingLength is LONGER than transition)");
                                        //Case where the point is on the next curve
                                        //Find the location of new endpoint
                                        double newEndDist = missingLength - transitionLength;
                                        //Catch a case where the missing length is longer than the next
                                        //Curves length
                                        if (newEndDist > nextCurve.GetDistanceAtParameter(nextCurve.EndParam))
                                        {
                                            prdDbg($"L: 6077: {nearestBlock.Handle} - {newEndDist}");
                                            prdDbg("Case 4.2.1 (Curve is shorter than required -> abort)");

                                            //Red line
                                            Line line2 = new Line(new Point3d(), endPoint);
                                            line2.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                            line2.AddEntityToDbModelSpace(localDb);

                                            //Return the curve to the queue
                                            //Remember: REVERSED!!
                                            ll.AddFirst(curve);
                                            curve.ReverseCurve();
                                            nextCurve.ReverseCurve();
                                            continue;
                                        }

                                        Point3d newEndPoint = nextCurve.GetPointAtDist(newEndDist);
                                        double parameter = Math.Truncate(nextCurve.GetParameterAtPoint(newEndPoint));
                                        SegmentType st = ((Polyline)nextCurve).GetSegmentType((int)parameter);

                                        if (st == SegmentType.Arc)
                                        {
                                            prdDbg("Case 4.2.2 (NewEndPoint lands on arc segment -> abort)");
                                            //Red line
                                            //When segment is an arc -- abort -- must be done manually
                                            //Generally a transition must not be on a curve
                                            Line line2 = new Line(new Point3d(), newEndPoint);
                                            line2.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                            line2.AddEntityToDbModelSpace(localDb);

                                            //Return the curve to the queue
                                            //Remember: REVERSED!!
                                            ll.AddFirst(curve);
                                            curve.ReverseCurve();
                                            nextCurve.ReverseCurve();
                                            continue;
                                        }
                                        else
                                        {
                                            //Move block and rotate
                                            nearestBlock.CheckOrOpenForWrite();
                                            Vector3d moveVector = endPoint.GetVectorTo(newEndPoint);
                                            nearestBlock.TransformBy(Matrix3d.Displacement(moveVector));

                                            //prdDbg($"L: 6102: {nearestBlock.Handle} - {newEndDist}");
                                            //Vector3d deriv = nextCurve.GetFirstDerivative(
                                            //    nextCurve.GetPointAtDist(newEndDist + transitionLength / 2));
                                            //double rotation = Math.Atan2(deriv.Y, deriv.X) - Math.PI / 2;
                                            //nearestBlock.Rotation = rotation;

                                            //Split the piece from next curve
                                            List<double> splitPars = new List<double>();
                                            splitPars.Add(nextCurve.GetParameterAtDistance(newEndDist));
                                            splitPars.Add(nextCurve.GetParameterAtDistance(newEndDist + transitionLength));
                                            try
                                            {
                                                DBObjectCollection objs = nextCurve
                                                    .GetSplitCurves(new DoubleCollection(splitPars.ToArray()));
                                                Polyline toMerge = objs[0] as Polyline;

                                                //Remember exchanged references!!!
                                                pline = curve as Polyline;
                                                for (int i = 0; i < toMerge.NumberOfVertices; i++)
                                                {
                                                    Point2d cp = new Point2d(toMerge.GetPoint3dAt(i).X, toMerge.GetPoint3dAt(i).Y);
                                                    pline.AddVertexAt(
                                                        pline.NumberOfVertices,
                                                        cp, toMerge.GetBulgeAt(i), 0, 0);
                                                }
                                                pline.ConstantWidth = globalWidth;
                                                RemoveColinearVerticesPolyline(pline);

                                                Curve toAdd = objs[2] as Curve;
                                                //Add the newly created curve to linkedlist
                                                toAdd.AddEntityToDbModelSpace(localDb);

                                                psm.CopyAllProperties(nextCurve, toAdd);
                                                toAdd.ReverseCurve();

                                                //Remember exchanged references!!!
                                                ll.AddFirst(curve);

                                                nextCurve.CheckOrOpenForWrite();
                                                nextCurve.Erase(true);

                                                //Remember: REVERSED!!
                                                curve.ReverseCurve();
                                            }
                                            catch (Autodesk.AutoCAD.Runtime.Exception ex)
                                            {
                                                Application.ShowAlertDialog(ex.Message + "\n" + ex.StackTrace);
                                                throw new System.Exception("Splitting of pline failed!");
                                            }

                                            //20 line
                                            //When the remainder is longer than the length of transition
                                            Line line = new Line(new Point3d(), newEndPoint);
                                            line.Color = Color.FromColorIndex(ColorMethod.ByAci, 20);
                                            line.AddEntityToDbModelSpace(localDb);
                                        }
                                    }
                                }
                            }
                        }
                        #endregion
                    }
                }
                catch (System.Exception ex)
                {
                    alTx.Abort();
                    alTx.Dispose();
                    alDb.Dispose();
                    tx.Abort();
                    prdDbg(ex.ExceptionInfo());
                    prdDbg(ex.ToString());
                    return;
                }
                alTx.Abort();
                alTx.Dispose();
                alDb.Dispose();
                tx.Commit();
            }
        }

        [CommandMethod("CREATEOFFSETPROFILES")]
        public void createoffsetprofiles()
        {
            createoffsetprofilesmethod();
        }
        public void createoffsetprofilesmethod(Profile p = null, ProfileView pv = null,
            DataReferencesOptions dataReferencesOptions = null)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                Profile profile;
                ProfileView profileView;

                profile = p;
                profileView = pv;

                #region Select Profile
                if (p == null)
                {
                    PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions("\n Select a profile: ");
                    promptEntityOptions1.SetRejectMessage("\n Not a profile!");
                    promptEntityOptions1.AddAllowedClass(typeof(Profile), true);
                    PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    Autodesk.AutoCAD.DatabaseServices.ObjectId profileId = entity1.ObjectId;
                    profile = profileId.Go<Profile>(tx);
                }

                #endregion

                #region Select Profile View
                if (profileView == null)
                {
                    PromptEntityOptions promptEntityOptions2 = new PromptEntityOptions("\n Select a ProfileView: ");
                    promptEntityOptions2.SetRejectMessage("\n Not a ProfileView");
                    promptEntityOptions2.AddAllowedClass(typeof(ProfileView), true);
                    PromptEntityResult entity2 = editor.GetEntity(promptEntityOptions2);
                    if (((PromptResult)entity2).Status != PromptStatus.OK) return;
                    profileView = tx.GetObject(entity2.ObjectId, OpenMode.ForWrite) as ProfileView;
                }
                #endregion

                #region Open fremtidig db
                DataReferencesOptions dro = dataReferencesOptions;
                if (dro == null) dro = new DataReferencesOptions();
                string projectName = dro.ProjectName;
                string etapeName = dro.EtapeName;

                //open the xref database
                Database fremDb = new Database(false, true);
                fremDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Fremtid"),
                    System.IO.FileShare.Read, false, string.Empty);
                Transaction fremTx = fremDb.TransactionManager.StartTransaction();
                HashSet<Curve> allCurves = fremDb.HashSetOfType<Curve>(fremTx);
                HashSet<BlockReference> allBrs = fremDb.HashSetOfType<BlockReference>(fremTx);
                #endregion

                //////////////////////////////////////
                string profileLayerName = "0-FJV-PROFILE";
                //////////////////////////////////////

                try
                {
                    #region Create layer for profile
                    using (Transaction txLag = localDb.TransactionManager.StartTransaction())
                    {

                        LayerTable lt = txLag.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;

                        if (!lt.Has(profileLayerName))
                        {
                            LayerTableRecord ltr = new LayerTableRecord();
                            ltr.Name = profileLayerName;
                            ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 6);
                            ltr.LineWeight = LineWeight.LineWeight030;
                            ltr.IsPlottable = false;

                            //Make layertable writable
                            lt.CheckOrOpenForWrite();

                            //Add the new layer to layer table
                            Oid ltId = lt.Add(ltr);
                            txLag.AddNewlyCreatedDBObject(ltr, true);
                        }
                        txLag.Commit();
                    }
                    #endregion

                    #region Initialize variables
                    Plane plane = new Plane(); //For intersecting

                    Alignment al = profileView.AlignmentId.Go<Alignment>(tx);

                    Point3d pvOrigin = profileView.Location;
                    double originX = pvOrigin.X;
                    double originY = pvOrigin.Y;

                    double pvStStart = profileView.StationStart;
                    double pvStEnd = profileView.StationEnd;
                    double pvElBottom = profileView.ElevationMin;
                    double pvElTop = profileView.ElevationMax;
                    double pvLength = pvStEnd - pvStStart;

                    //Settings
                    //double weedAngle = 5; //In degrees
                    //double weedAngleRad = weedAngle.ToRadians();
                    //double DouglasPeuckerTolerance = .05;

                    double stepLength = 0.1;
                    int nrOfSteps = (int)(pvLength / stepLength);
                    #endregion

                    #region GetCurvesAndBRs from fremtidig
                    HashSet<Curve> curves = allCurves
                        .Where(x => x.XrecFilter("Alignment", new[] { al.Name }))
                        .ToHashSet();
                    HashSet<BlockReference> brs = allBrs
                        .Where(x => x.XrecFilter("Alignment", new[] { al.Name }))
                        .ToHashSet();
                    prdDbg($"Curves: {curves.Count}, Components: {brs.Count}");
                    #endregion

                    //PipelineSizeArray sizeArray = new PipelineSizeArray(al, curves);
                    //prdDbg("Curves:");
                    //prdDbg(sizeArray.ToString());

                    prdDbg("Blocks:");
                    PipelineSizeArray sizeArray = new PipelineSizeArray(al, curves, brs);
                    prdDbg(sizeArray.ToString());

                    #region Create polyline from centre profile
                    ProfileEntityCollection entities = profile.Entities;
                    prdDbg($"Count of entities: {entities.Count}");
                    HashSet<string> types = new HashSet<string>();

                    Polyline pline = new Polyline(entities.Count + 1);

                    //Place first point
                    ProfileEntity pe = entities.EntityAtId(entities.FirstEntity);
                    double startX = originX, startY = originY;
                    profileView.FindXYAtStationAndElevation(pe.StartStation, pe.StartElevation, ref startX, ref startY);
                    Point2d startPoint = new Point2d(startX, startY);
                    Point2d endPoint = new Point2d(originX, originY);
                    pline.AddVertexAt(0, startPoint, pe.GetBulge(profileView), 0, 0);
                    int vertIdx = 1;
                    for (int i = 0; i < entities.Count + 1; i++)
                    {
                        endPoint = profileView.GetPoint2dAtStaAndEl(pe.EndStation, pe.EndElevation);
                        double bulge = entities.LookAheadAndGetBulge(pe, profileView);
                        pline.AddVertexAt(vertIdx, endPoint, bulge, 0, 0);
                        vertIdx++;
                        startPoint = endPoint;
                        try { pe = entities.EntityAtId(pe.EntityAfter); }
                        catch (System.Exception) { break; }
                    }
                    #endregion

                    #region Create partial curves
                    HashSet<Polyline> offsetCurvesTop = new HashSet<Polyline>();
                    HashSet<Polyline> offsetCurvesBund = new HashSet<Polyline>();
                    //Small offset to avoid vertical segments in profile
                    //************************************************//
                    double pDelta = 0.125;
                    //************************************************//
                    //Create lines to split the offset curves
                    //And it happens for each size segment
                    for (int i = 0; i < sizeArray.Length; i++)
                    {
                        var size = sizeArray[i];
                        double halfKod = size.Kod / 2.0 / 1000.0;

                        HashSet<Line> splitLines = new HashSet<Line>();
                        if (i != 0)
                        {
                            Point3d sP = new Point3d(originX + sizeArray[i - 1].EndStation + pDelta, originY, 0);
                            Point3d eP = new Point3d(originX + sizeArray[i - 1].EndStation + pDelta, originY + 100, 0);
                            Line splitLineStart = new Line(sP, eP);
                            splitLines.Add(splitLineStart);
                        }
                        if (i != sizeArray.Length - 1)
                        {
                            Point3d sP = new Point3d(originX + sizeArray[i].EndStation - pDelta, originY, 0);
                            Point3d eP = new Point3d(originX + sizeArray[i].EndStation - pDelta, originY + 100, 0);
                            Line splitLineEnd = new Line(sP, eP);
                            splitLines.Add(splitLineEnd);
                        }

                        //Top offset
                        //Handle case of only one size pipe
                        if (sizeArray.Length == 1)
                        {
                            using (DBObjectCollection col = pline.GetOffsetCurves(halfKod))
                            {
                                offsetCurvesTop.Add(col[0] as Polyline);
                            }
                        }
                        else
                        {
                            using (DBObjectCollection col = pline.GetOffsetCurves(halfKod))
                            {
                                if (col.Count == 0) throw new System.Exception("Offsetting pline failed!");
                                Polyline offsetPline = col[0] as Polyline;
                                List<double> splitPts = new List<double>();
                                foreach (Line line in splitLines)
                                {
                                    Point3dCollection ipts = new Point3dCollection();
                                    offsetPline.IntersectWith(line,
                                        Autodesk.AutoCAD.DatabaseServices.Intersect.OnBothOperands,
                                        ipts, new IntPtr(0), new IntPtr(0));
                                    foreach (Point3d pt in ipts)
                                        splitPts.Add(offsetPline.GetParameterAtPoint(offsetPline.GetClosestPointTo(pt, false)));
                                }
                                if (splitPts.Count == 0) throw new System.Exception("Getting split points failed!");
                                splitPts.Sort();
                                try
                                {
                                    DBObjectCollection objs = offsetPline
                                        .GetSplitCurves(new DoubleCollection(splitPts.ToArray()));
                                    if (i == 0) offsetCurvesTop.Add(objs[0] as Polyline);
                                    else offsetCurvesTop.Add(objs[1] as Polyline);
                                }
                                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                                {
                                    Application.ShowAlertDialog(ex.Message + "\n" + ex.StackTrace);
                                    throw new System.Exception("Splitting of pline failed!");
                                }
                            }
                        }
                        //Bund offset
                        if (sizeArray.Length == 1)
                        {
                            using (DBObjectCollection col = pline.GetOffsetCurves(-halfKod))
                            {
                                offsetCurvesBund.Add(col[0] as Polyline);
                            }
                        }
                        else
                        {
                            using (DBObjectCollection col = pline.GetOffsetCurves(-halfKod))
                            {
                                if (col.Count == 0) throw new System.Exception("Offsetting pline failed!");
                                Polyline offsetPline = col[0] as Polyline;
                                List<double> splitPts = new List<double>();
                                foreach (Line line in splitLines)
                                {
                                    Point3dCollection ipts = new Point3dCollection();
                                    offsetPline.IntersectWith(line,
                                        Autodesk.AutoCAD.DatabaseServices.Intersect.OnBothOperands,
                                        ipts, new IntPtr(0), new IntPtr(0));
                                    foreach (Point3d pt in ipts)
                                        splitPts.Add(offsetPline.GetParameterAtPoint(offsetPline.GetClosestPointTo(pt, false)));
                                }
                                if (splitPts.Count == 0) throw new System.Exception("Getting split points failed!");
                                splitPts.Sort();
                                try
                                {
                                    DBObjectCollection objs = offsetPline
                                        .GetSplitCurves(new DoubleCollection(splitPts.ToArray()));
                                    if (i == 0) offsetCurvesBund.Add(objs[0] as Polyline);
                                    else offsetCurvesBund.Add(objs[1] as Polyline);
                                }
                                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                                {
                                    Application.ShowAlertDialog(ex.Message + "\n" + ex.StackTrace);
                                    throw new System.Exception("Splitting of pline failed!");
                                }
                            }
                        }
                    }
                    #endregion

                    #region Combine partial plines and convert to profile
                    //Get the number of the alignment
                    Regex regx = new Regex(@"^(?<number>\d\d\s)");
                    string number = "";
                    if (regx.IsMatch(al.Name))
                    {
                        number = regx.Match(al.Name).Groups["number"].Value;
                    }

                    //Combine to polylines
                    Polyline plineTop = new Polyline();
                    foreach (Polyline partPline in offsetCurvesTop)
                    {
                        for (int i = 0; i < partPline.NumberOfVertices; i++)
                        {
                            Point2d cp = new Point2d(partPline.GetPoint3dAt(i).X, partPline.GetPoint3dAt(i).Y);
                            plineTop.AddVertexAt(
                                plineTop.NumberOfVertices,
                                cp, partPline.GetBulgeAt(i), 0, 0);
                        }
                    }
                    Profile profileTop = CreateProfileFromPolyline(
                        number + "BUND",
                        profileView,
                        al.Name,
                        profileLayerName,
                        "PROFIL STYLE MGO",
                        "_No Labels",
                        plineTop
                        );

                    //Combine to polylines
                    Polyline plineBund = new Polyline();
                    foreach (Polyline partPline in offsetCurvesBund)
                    {
                        for (int i = 0; i < partPline.NumberOfVertices; i++)
                        {
                            Point2d cp = new Point2d(partPline.GetPoint3dAt(i).X, partPline.GetPoint3dAt(i).Y);
                            plineBund.AddVertexAt(
                                plineBund.NumberOfVertices,
                                cp, partPline.GetBulgeAt(i), 0, 0);
                        }
                    }
                    Profile profileBund = CreateProfileFromPolyline(
                        number + "TOP",
                        profileView,
                        al.Name,
                        profileLayerName,
                        "PROFIL STYLE MGO",
                        "_No Labels",
                        plineBund
                        );
                    #endregion
                }
                catch (System.Exception ex)
                {
                    fremTx.Abort();
                    fremTx.Dispose();
                    fremDb.Dispose();
                    tx.Abort();
                    prdDbg(ex.ToString());
                    return;
                }
                fremTx.Abort();
                fremTx.Dispose();
                fremDb.Dispose();
                tx.Commit();
            }
        }
        /// <summary>
        /// Creates offset profiles for all middle profiles
        /// FOR USE ONLY WITH CONTINUOUS PVs!!!
        /// </summary>
        [CommandMethod("CREATEOFFSETPROFILESALL")]
        public void createoffsetprofilesall()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                DataReferencesOptions dro = new DataReferencesOptions();

                HashSet<Profile> pvs = localDb.HashSetOfType<Profile>(tx);
                foreach (Profile profile in pvs)
                {
                    if (profile.Name.Contains("MIDT"))
                    {
                        Alignment al = profile.AlignmentId.Go<Alignment>(tx);
                        prdDbg($"Processing: {al.Name}...");
                        ProfileView pv = al.GetProfileViewIds()[0].Go<ProfileView>(tx);

                        createoffsetprofilesmethod(profile, pv, dro);
                    }
                    System.Windows.Forms.Application.DoEvents();
                }
                tx.Commit();
            }
        }

        [CommandMethod("DELETEOFFSETPROFILES")]
        public void deleteoffsetprofiles()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                HashSet<Profile> pvs = localDb.HashSetOfType<Profile>(tx);
                foreach (Profile profile in pvs)
                {
                    if (profile.Name.Contains("TOP") ||
                        profile.Name.Contains("BUND"))
                    {
                        profile.CheckOrOpenForWrite();
                        profile.Erase(true);
                    }
                }
                tx.Commit();
            }
        }

        [CommandMethod("LISTMIDTPROFILESSTARTENDSTATIONS")]
        public void listmidtprofilesstartendstations()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                HashSet<Profile> pvs = localDb.HashSetOfType<Profile>(tx);
                foreach (Profile profile in pvs)
                {
                    if (profile.Name.Contains("MIDT"))
                    {
                        Alignment al = profile.AlignmentId.Go<Alignment>(tx);


                        bool success = true;
                        double startElevation = SampleProfile(profile, 0, ref success);
                        double endElevation = SampleProfile(profile, al.EndingStation, ref success);
                        if (!success)
                        {
                            prdDbg($"Processing: {al.Name}...");
                            prdDbg($"S: 0 -> {startElevation.ToString("0.0")}, E: {al.EndingStation.ToString("0.0")} -> {endElevation.ToString("0.0")}");
                        }
                    }
                    System.Windows.Forms.Application.DoEvents();
                }
                tx.Commit();
            }

            //Local method to sample profiles
            double SampleProfile(Profile profile, double station, ref bool success)
            {
                double sampledElevation = 0;
                try { sampledElevation = profile.ElevationAt(station); }
                catch (System.Exception)
                {
                    //prdDbg($"Station {station} threw an exception when sampling!");
                    success = false;
                    return 0;
                }
                return sampledElevation;
            }
        }

        //[CommandMethod("FIXMIDTPROFILESSTARTENDSTATIONS")]
        public void fixmidtprofilesstartendstations()
        {
            //Not finished!!!
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                HashSet<Profile> pvs = localDb.HashSetOfType<Profile>(tx);
                foreach (Profile profile in pvs)
                {
                    if (profile.Name.Contains("MIDT"))
                    {
                        Alignment al = profile.AlignmentId.Go<Alignment>(tx);

                        bool success = true;
                        double startElevation = SampleProfile(profile, 0, ref success);
                        if (!success)
                        {
                            //Start needs fixing
                            prdDbg($"Processing: {al.Name}...");
                            ProfileEntityCollection entities = profile.Entities;
                            ProfileEntity entity = profile.Entities[0];
                            prdDbg(entity.EntityType.ToString());
                        }

                        success = true;
                        //double endElevation = SampleProfile(profile, al.EndingStation, ref success);
                        //if (!success)
                        //{
                        //    prdDbg($"Processing: {al.Name}...");
                        //    prdDbg($"S: 0 -> {startElevation.ToString("0.0")}, E: {al.EndingStation.ToString("0.0")} -> {endElevation.ToString("0.0")}");
                        //}
                    }
                    System.Windows.Forms.Application.DoEvents();
                }
                tx.Commit();
            }

            //Local method to sample profiles
            double SampleProfile(Profile profile, double station, ref bool success)
            {
                double sampledElevation = 0;
                try { sampledElevation = profile.ElevationAt(station); }
                catch (System.Exception)
                {
                    //prdDbg($"Station {station} threw an exception when sampling!");
                    success = false;
                    return 0;
                }
                return sampledElevation;
            }
        }

        #region Obsolete code
        //public void createlerdataloopnodeepclone(HashSet<Polyline3d> allLinework, Alignment alignment,
        //                              CivSurface surface, ProfileView pv,
        //                              System.Data.DataTable dtKrydsninger, System.Data.DataTable dtDybde,
        //                              ref HashSet<string> pNames)
        //{
        //    DocumentCollection docCol = Application.DocumentManager;
        //    Database localDb = docCol.MdiActiveDocument.Database;
        //    Editor editor = docCol.MdiActiveDocument.Editor;
        //    Document doc = docCol.MdiActiveDocument;
        //    CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

        //    Transaction tx = localDb.TransactionManager.TopTransaction;

        //    try
        //    {
        //        #region ModelSpaces
        //        //oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(xRefDB);
        //        oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);
        //        #endregion

        //        #region Prepare variables
        //        BlockTable acBlkTbl = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
        //        BlockTableRecord acBlkTblRec =
        //            tx.GetObject(acBlkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

        //        Plane plane = new Plane();

        //        editor.WriteMessage($"\nTotal {allLinework.Count} intersections detected.");

        //        //Load things
        //        CogoPointCollection cogoPoints = civilDoc.CogoPoints;
        //        HashSet<CogoPoint> allNewlyCreatedPoints = new HashSet<CogoPoint>();
        //        #endregion

        //        #region Handle PointGroups
        //        bool pointGroupAlreadyExists = civilDoc.PointGroups.Contains(alignment.Name);

        //        PointGroup pg = null;

        //        if (pointGroupAlreadyExists)
        //        {
        //            pg = tx.GetObject(civilDoc.PointGroups[alignment.Name], OpenMode.ForWrite) as PointGroup;
        //        }
        //        else
        //        {
        //            oid pgId = civilDoc.PointGroups.Add(alignment.Name);

        //            pg = pgId.GetObject(OpenMode.ForWrite) as PointGroup;
        //        }
        //        #endregion

        //        foreach (Entity ent in allLinework)
        //        {
        //            #region Read data parameters from csvs
        //            //Read 'Type' value
        //            string type = ReadStringParameterFromDataTable(ent.Layer, dtKrydsninger, "Type", 0);
        //            if (type.IsNoE())
        //            {
        //                editor.WriteMessage($"\nFejl: For lag {ent.Layer} mangler der enten " +
        //                    $"selve definitionen eller 'Type'!");
        //                return;
        //            }

        //            //Read depth value for type
        //            double depth = 0;
        //            if (!type.IsNoE())
        //            {
        //                depth = Utils.ReadDoubleParameterFromDataTable(type, dtDybde, "Dybde", 0);
        //            }

        //            //Read layer value for the object
        //            string localLayerName = Utils.ReadStringParameterFromDataTable(
        //                                ent.Layer, dtKrydsninger, "Layer", 0);

        //            //prdDbg(localLayerName);

        //            #region Populate description field

        //            Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

        //            string description = string.Empty;

        //            ////Populate description field
        //            ////1. Read size record if it exists
        //            //MapValue sizeRecord = Utils.ReadRecordData(
        //            //    tables, ent.ObjectId, "SizeTable", "Size");
        //            //int SizeTableSize = 0;
        //            //string sizeDescrPart = "";
        //            //if (sizeRecord != null)
        //            //{
        //            //    SizeTableSize = sizeRecord.Int32Value;
        //            //    sizeDescrPart = $"ø{SizeTableSize}";
        //            //}

        //            //2. Read description from Krydsninger
        //            string descrFromKrydsninger = ReadStringParameterFromDataTable(
        //                ent.Layer, dtKrydsninger, "Description", 0);

        //            //2.1 Read the formatting in the description field
        //            List<(string ToReplace, string Data)> descrFormatList = null;
        //            if (descrFromKrydsninger.IsNotNoE())
        //                descrFormatList = FindDescriptionParts(descrFromKrydsninger);

        //            //Finally: Compose description field
        //            List<string> descrParts = new List<string>();
        //            //1. Add custom size
        //            //if (SizeTableSize != 0) descrParts.Add(sizeDescrPart);
        //            //2. Process and add parts from format bits in OD
        //            if (descrFromKrydsninger.IsNotNoE())
        //            {
        //                //Interpolate description from Krydsninger with format setting, if they exist
        //                if (descrFormatList != null && descrFormatList.Count > 0)
        //                {
        //                    for (int i = 0; i < descrFormatList.Count; i++)
        //                    {
        //                        var tuple = descrFormatList[i];
        //                        string result = ReadDescriptionPartsFromOD(tables, ent, tuple.Data, dtKrydsninger);
        //                        descrFromKrydsninger = descrFromKrydsninger.Replace(tuple.ToReplace, result);
        //                        prdDbg(descrFromKrydsninger);
        //                    }
        //                }

        //                //Add the description field to parts
        //                descrParts.Add(descrFromKrydsninger);
        //            }

        //            description = "";
        //            if (descrParts.Count == 1) description = descrParts[0];
        //            else if (descrParts.Count > 1)
        //                description = string.Join("; ", descrParts);

        //            #endregion

        //            string handleValue = ent.Handle.ToString();
        //            string pName = "";

        //            if (handleValue != null) pName = handleValue;
        //            else
        //            {
        //                pName = "Reading of Handle failed.";
        //                editor.WriteMessage($"\nEntity on layer {ent.Layer} failed to read Handle!");
        //            }

        //            #endregion

        //            #region Create points
        //            using (Point3dCollection p3dcol = new Point3dCollection())
        //            {
        //                alignment.IntersectWith(ent, 0, plane, p3dcol, new IntPtr(0), new IntPtr(0));

        //                foreach (Point3d p3d in p3dcol)
        //                {
        //                    oid pointId = cogoPoints.Add(p3d, true);
        //                    CogoPoint cogoPoint = pointId.Go<CogoPoint>(tx, OpenMode.ForWrite);

        //                    //Id of the new Poly3d if type == 3D
        //                    oid newPolyId;

        //                    #region Assign elevation based on 3D conditions
        //                    double zElevation = 0;
        //                    if (type != "3D")
        //                    {
        //                        var intPoint = surface.GetIntersectionPoint(p3d, new Vector3d(0, 0, 1));
        //                        zElevation = intPoint.Z;

        //                        //Subtract the depth (if invalid it is zero, so no modification will occur)
        //                        zElevation -= depth;

        //                        cogoPoint.Elevation = zElevation;
        //                    }
        //                    else if (type == "3D")
        //                    {
        //                        //Create vertical line to intersect the Ler line
        //                        using (Transaction txp3d = localDb.TransactionManager.StartTransaction())
        //                        {
        //                            Point3dCollection newP3dCol = new Point3dCollection();
        //                            //Intersection at 0
        //                            newP3dCol.Add(p3d);
        //                            //New point at very far away
        //                            newP3dCol.Add(new Point3d(p3d.X, p3d.Y, 1000));

        //                            Polyline3d newPoly = new Polyline3d(Poly3dType.SimplePoly, newP3dCol, false);

        //                            //Open modelspace
        //                            acBlkTbl = txp3d.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
        //                            acBlkTblRec = txp3d.GetObject(acBlkTbl[BlockTableRecord.ModelSpace],
        //                                             OpenMode.ForWrite) as BlockTableRecord;

        //                            acBlkTblRec.AppendEntity(newPoly);
        //                            txp3d.AddNewlyCreatedDBObject(newPoly, true);
        //                            newPolyId = newPoly.ObjectId;
        //                            txp3d.Commit();
        //                        }

        //                        Polyline3d newPoly3d = newPolyId.Go<Polyline3d>(tx);
        //                        using (Point3dCollection p3dIntCol = new Point3dCollection())
        //                        {
        //                            ent.IntersectWith(newPoly3d, 0, p3dIntCol, new IntPtr(0), new IntPtr(0));

        //                            foreach (Point3d p3dInt in p3dIntCol)
        //                            {
        //                                //Assume only one intersection
        //                                cogoPoint.Elevation = p3dInt.Z;
        //                            }

        //                            if (cogoPoint.Elevation == 0)
        //                            {
        //                                editor.WriteMessage($"\nFor type 3D entity {handleValue}" +
        //                                    $" layer {ent.Layer}," +
        //                                    $" elevation is 0!");
        //                            }
        //                        }
        //                        newPoly3d.UpgradeOpen();
        //                        newPoly3d.Erase(true);
        //                    }
        //                    #endregion

        //                    //Set the layer
        //                    #region Layer handling
        //                    bool localLayerExists = false;

        //                    if (!localLayerName.IsNoE() || localLayerName != null)
        //                    {
        //                        LayerTable lt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;
        //                        if (lt.Has(localLayerName))
        //                        {
        //                            localLayerExists = true;
        //                        }
        //                        else
        //                        {
        //                            //Create layer if it doesn't exist
        //                            try
        //                            {
        //                                //Validate the name of layer
        //                                //It throws an exception if not, so need to catch it
        //                                SymbolUtilityServices.ValidateSymbolName(localLayerName, false);

        //                                LayerTableRecord ltr = new LayerTableRecord();
        //                                ltr.Name = localLayerName;

        //                                //Make layertable writable
        //                                lt.UpgradeOpen();

        //                                //Add the new layer to layer table
        //                                oid ltId = lt.Add(ltr);
        //                                tx.AddNewlyCreatedDBObject(ltr, true);

        //                                //Flag that the layer exists now
        //                                localLayerExists = true;

        //                            }
        //                            catch (System.Exception)
        //                            {
        //                                //Eat the exception and continue
        //                                //localLayerExists must remain false
        //                            }
        //                        }
        //                    }
        //                    else
        //                    {
        //                        editor.WriteMessage($"\nLocal layer name for source layer {ent.Layer} does not" +
        //                            $" exist in Krydsninger.csv!");
        //                    }

        //                    cogoPoint.Layer = localLayerName;
        //                    #endregion

        //                    int count = 1;
        //                    string pointName = pName + "_" + count;

        //                    while (pNames.Contains(pointName))
        //                    {
        //                        count++;
        //                        pointName = pName + "_" + count;
        //                    }
        //                    pNames.Add(pointName);
        //                    cogoPoint.PointName = pointName;
        //                    cogoPoint.RawDescription = description;

        //                    #region Copy OD from polies to the new point
        //                    //Copy specific OD from cloned 3D polies to the new point

        //                    List<(string TableName, string RecordName)> odList =
        //                        new List<(string TableName, string RecordName)>();
        //                    odList.Add(("IdRecord", "Handle"));
        //                    TryCopySpecificOD(tables, ent, cogoPoint, odList);
        //                    #endregion

        //                    #region Check, create or update Crossing Data
        //                    //Definition of "CrossingData" table
        //                    string[] columnNames = new string[2] { "Diameter", "Alignment" };
        //                    string[] columnDescrs = new string[2] { "Diameter of crossing pipe", "Alignment name" };
        //                    DataType[] dataTypes = new DataType[2] { DataType.Character, DataType.Character };
        //                    //

        //                    //Check or create table, or check or create all columns
        //                    if (DoesTableExist(tables, "CrossingData"))
        //                    {//Table exists
        //                        if (DoAllColumnsExist(tables, "CrossingData", columnNames))
        //                        {
        //                            //The table is in order, continue to data creation
        //                        }
        //                        //If not create missing columns
        //                        else CreateMissingColumns(tables, "CrossingData", columnNames, columnDescrs, dataTypes);
        //                    }
        //                    else
        //                    {
        //                        //Table does not exist
        //                        if (CreateTable(tables, "CrossingData", "Table holding relevant crossing data",
        //                            columnNames, columnDescrs, dataTypes))
        //                        {
        //                            //Table ready for populating with data
        //                        }
        //                    }

        //                    #endregion

        //                    #region Create Diameter OD in "CrossingData"
        //                    odList.Clear();
        //                    odList.Add(("SizeTable", "Size"));
        //                    //Fetch diameter definitions if any
        //                    string diaDef = ReadStringParameterFromDataTable(ent.Layer,
        //                        dtKrydsninger, "Diameter", 0);
        //                    if (diaDef.IsNotNoE())
        //                    {
        //                        var list = FindDescriptionParts(diaDef);
        //                        //Be careful if FindDescriptionParts implementation changes
        //                        string[] parts = list[0].Item2.Split(':');
        //                        odList.Add((parts[0], parts[1]));
        //                    }

        //                    foreach (var item in odList)
        //                    {
        //                        MapValue originalValue = ReadRecordData(
        //                            tables, ent.ObjectId, item.TableName, item.RecordName);

        //                        if (originalValue != null)
        //                        {
        //                            if (DoesTableExist(tables, "CrossingData"))
        //                            {
        //                                if (DoesRecordExist(tables, cogoPoint.ObjectId, "Diameter"))
        //                                {
        //                                    UpdateODRecord(tables, "CrossingData", "Diameter",
        //                                        cogoPoint.ObjectId, originalValue);
        //                                    UpdateODRecord(tables, "CrossingData", "Alignment",
        //                                        cogoPoint.ObjectId, new MapValue(alignment.Name));
        //                                }
        //                                else
        //                                {
        //                                    AddODRecord(tables, "CrossingData", "Diameter",
        //                                        cogoPoint.ObjectId, originalValue);
        //                                    AddODRecord(tables, "CrossingData", "Alignment",
        //                                        cogoPoint.ObjectId, new MapValue(alignment.Name));
        //                                }
        //                            }
        //                            else
        //                            {

        //                                AddODRecord(tables, "CrossingData", "Diameter",
        //                                    cogoPoint.ObjectId, originalValue);
        //                                AddODRecord(tables, "CrossingData", "Alignment",
        //                                    cogoPoint.ObjectId, new MapValue(alignment.Name));

        //                            }
        //                        }
        //                    }
        //                    #endregion

        //                    #region Create alignment "CrossingData"
        //                    if (DoesTableExist(tables, "CrossingData"))
        //                    {
        //                        if (DoesRecordExist(tables, cogoPoint.ObjectId, "Diameter"))
        //                        {
        //                            UpdateODRecord(tables, "CrossingData", "Alignment",
        //                                cogoPoint.ObjectId, new MapValue(alignment.Name));
        //                        }
        //                        else
        //                        {
        //                            AddODRecord(tables, "CrossingData", "Alignment",
        //                                cogoPoint.ObjectId, new MapValue(alignment.Name));
        //                        }
        //                    }
        //                    else
        //                    {
        //                        //This should not be possible!!!
        //                        //The table is checked or created earlier.
        //                    }

        //                    #endregion

        //                    //Reference newly created cogoPoint to gathering collection
        //                    allNewlyCreatedPoints.Add(cogoPoint);
        //                }
        //            }
        //            #endregion
        //        }

        //        #region Assign newly created points to projection on a profile view
        //        #region Build query for PointGroup
        //        //Build query
        //        StandardPointGroupQuery spgq = new StandardPointGroupQuery();
        //        List<string> newPointNumbers = allNewlyCreatedPoints.Select(x => x.PointNumber.ToString()).ToList();
        //        string pointNumbersToInclude = string.Join(",", newPointNumbers.ToArray());
        //        spgq.IncludeNumbers = pointNumbersToInclude;
        //        pg.SetQuery(spgq);
        //        pg.Update();
        //        #endregion

        //        double elMax = pv.ElevationMax;
        //        double elMin = pv.ElevationMin;

        //        double tryGetMin = allNewlyCreatedPoints.Min(x => x.Elevation);

        //        if (tryGetMin != 0)
        //        {
        //            pv.CheckOrOpenForWrite();
        //            elMin = tryGetMin - 1;
        //            pv.ElevationRangeMode = ElevationRangeType.UserSpecified;
        //            pv.ElevationMin = elMin;
        //        }

        //        editor.SetImpliedSelection(allNewlyCreatedPoints.Select(x => x.ObjectId).ToArray());
        //        editor.Command("_AeccProjectObjectsToProf", pv.ObjectId);

        //        #endregion
        //    }
        //    catch (System.Exception ex)
        //    {
        //        editor.WriteMessage("\n" + ex.Message);
        //        return;
        //    }
        //} 
        #endregion

        public void createlerdataloopwithdeepclone(HashSet<Polyline3d> linework, Alignment alignment,
                                      CivSurface surface, ProfileView pv,
                                      System.Data.DataTable dtKrydsninger, System.Data.DataTable dtDybde,
                                      Transaction tx,
                                      ref HashSet<string> pNames)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            try
            {
                #region Prepare variables
                BlockTable acBlkTbl = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord acBlkTblRec =
                    tx.GetObject(acBlkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                Plane plane = new Plane();

                editor.WriteMessage($"\nTotal {linework.Count} intersections detected.");

                //Load things
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
                    Oid pgId = civilDoc.PointGroups.Add(alignment.Name);

                    pg = pgId.GetObject(OpenMode.ForWrite) as PointGroup;
                }
                #endregion

                foreach (Entity ent in linework)
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
                        depth = ReadDoubleParameterFromDataTable(type, dtDybde, "Dybde", 0);
                    }

                    //Read layer value for the object
                    string localLayerName = ReadStringParameterFromDataTable(
                                        ent.Layer, dtKrydsninger, "Layer", 0);

                    //if (localLayerName.IsNoE()) prdDbg($"Entity didn't have ");

                    #region Populate description field

                    Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

                    string description = string.Empty;

                    ////Populate description field
                    ////1. Read size record if it exists
                    //MapValue sizeRecord = Utils.ReadRecordData(
                    //    tables, ent.ObjectId, "SizeTable", "Size");
                    //int SizeTableSize = 0;
                    //string sizeDescrPart = "";
                    //if (sizeRecord != null)
                    //{
                    //    SizeTableSize = sizeRecord.Int32Value;
                    //    sizeDescrPart = $"ø{SizeTableSize}";
                    //}

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
                    //if (SizeTableSize != 0) descrParts.Add(sizeDescrPart);
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
                                //prdDbg(descrFromKrydsninger);
                            }
                        }

                        //Add the description field to parts
                        descrParts.Add(descrFromKrydsninger);
                    }

                    description = "";
                    if (descrParts.Count == 1) description = descrParts[0];
                    else if (descrParts.Count > 1)
                        description = string.Join("; ", descrParts);

                    #endregion
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

                        foreach (Point3d p3d in p3dcol)
                        {
                            Oid pointId = cogoPoints.Add(p3d, true);
                            CogoPoint cogoPoint = pointId.Go<CogoPoint>(tx, OpenMode.ForWrite);
                            cogoPoint.StyleId = civilDoc.Styles.PointStyles["PIL"];
                            cogoPoint.LabelStyleId = civilDoc.Styles.LabelStyles.PointLabelStyles.LabelStyles["_No labels"];

                            //Id of the new Poly3d if type == 3D
                            Oid newPolyId;

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

                                        //Read the layer color value
                                        System.Data.DataTable dtLayers = CsvReader.ReadCsvToDataTable(
                                            "X:\\AutoCAD DRI - 01 Civil 3D\\Lag.csv", "Lag");
                                        int color = ReadIntParameterFromDataTable(localLayerName, dtLayers, "Color", 0);
                                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, Convert.ToInt16(color));

                                        //Make layertable writable
                                        lt.UpgradeOpen();

                                        //Add the new layer to layer table
                                        Oid ltId = lt.Add(ltr);
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

                            #region Create Diameter OD in "CrossingData"
                            odList.Clear();
                            //odList.Add(("SizeTable", "Size"));
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

                            #region Make sure diameter contains at least zero
                            if (DoesTableExist(tables, "CrossingData"))
                            {
                                if (DoesRecordExist(tables, cogoPoint.ObjectId, "CrossingData", "Diameter"))
                                {
                                    UpdateODRecord(tables, "CrossingData", "Diameter",
                                        cogoPoint.ObjectId, new MapValue("0"));
                                }
                                else
                                {
                                    AddODRecord(tables, "CrossingData", "Diameter",
                                        cogoPoint.ObjectId, new MapValue("0"));
                                }
                            }
                            #endregion

                            foreach (var item in odList)
                            {
                                MapValue originalValue = ReadRecordData(
                                    tables, ent.ObjectId, item.TableName, item.RecordName);

                                if (originalValue != null)
                                {
                                    if (DoesTableExist(tables, "CrossingData"))
                                    {
                                        if (DoesRecordExist(tables, cogoPoint.ObjectId, "CrossingData", "Diameter"))
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

                                        AddODRecord(tables, "CrossingData", "Diameter",
                                            cogoPoint.ObjectId, originalValue);

                                    }
                                }
                            }
                            #endregion

                            #region Create alignment "CrossingData"
                            if (DoesTableExist(tables, "CrossingData"))
                            {
                                if (DoesRecordExist(tables, cogoPoint.ObjectId, "CrossingData", "Alignment"))
                                {
                                    UpdateODRecord(tables, "CrossingData", "Alignment",
                                        cogoPoint.ObjectId, new MapValue(alignment.Name));
                                }
                                else
                                {
                                    AddODRecord(tables, "CrossingData", "Alignment",
                                        cogoPoint.ObjectId, new MapValue(alignment.Name));
                                }
                            }
                            else
                            {
                                //This should not be possible!!!
                                //The table is checked or created earlier.
                            }

                            #endregion

                            //Reference newly created cogoPoint to gathering collection
                            allNewlyCreatedPoints.Add(cogoPoint);
                        }
                    }
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
                if (elMax == 0) prdDbg("pv.ElevationMax is 0! Is surface profile missing?");
                //prdDbg(elMax.ToString());
                double elMin = pv.ElevationMin;
                //prdDbg(elMin.ToString());

                double tryGetMin = allNewlyCreatedPoints.Min(x => x.Elevation);
                //prdDbg(tryGetMin.ToString());

                if (tryGetMin != 0 && tryGetMin - 1 > elMax)
                {
                    pv.CheckOrOpenForWrite();
                    elMin = tryGetMin - 3;
                    //2prdDbg(elMin.ToString());
                    pv.ElevationRangeMode = ElevationRangeType.UserSpecified;
                    pv.ElevationMin = elMin;
                }

                editor.SetImpliedSelection(allNewlyCreatedPoints.Select(x => x.ObjectId).ToArray());
                editor.Command("_AeccProjectObjectsToProf", pv.ObjectId);

                #endregion
            }
            catch (System.Exception ex)
            {
                throw;
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
                    Oid surfaceObjId = entity3.ObjectId;
                    CivSurface surface = surfaceObjId.GetObject(OpenMode.ForRead, false) as CivSurface;
                    #endregion

                    #region Get terrain layer id

                    LayerTable lt = db.LayerTableId.GetObject(OpenMode.ForRead) as LayerTable;
                    string terrainLayerName = "0_TERRAIN_PROFILE";
                    Oid terrainLayerId = Oid.Null;
                    foreach (Oid id in lt)
                    {
                        LayerTableRecord ltr = id.GetObject(OpenMode.ForRead) as LayerTableRecord;
                        if (ltr.Name == terrainLayerName) terrainLayerId = ltr.Id;
                    }
                    if (terrainLayerId == Oid.Null)
                    {
                        editor.WriteMessage("Terrain layer missing!");
                        return;
                    }

                    #endregion

                    #region ProfileView styles ids
                    Oid profileStyleId = civilDoc.Styles.ProfileStyles["Terræn"];
                    Oid profileLabelSetStyleId = civilDoc.Styles.LabelSetStyles.ProfileLabelSetStyles["_No Labels"];

                    Oid profileViewBandSetStyleId = civilDoc.Styles
                            .ProfileViewBandSetStyles["EG-FG Elevations and Stations"];
                    Oid profileViewStyleId = civilDoc.Styles
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

                        Oid surfaceProfileId = Oid.Null;
                        string profileName = $"{alignment.Name}_surface_P";
                        bool noProfileExists = true;
                        ObjectIdCollection pIds = alignment.GetProfileIds();
                        foreach (Oid pId in pIds)
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
                DataReferencesOptions dro = new DataReferencesOptions();
                string projectName = dro.ProjectName;
                string etapeName = dro.EtapeName;

                editor.WriteMessage("\n" + GetPathToDataFiles(projectName, etapeName, "Surface"));

                #region Read surface from file
                // open the xref database
                Database xRefSurfaceDB = new Database(false, true);
                xRefSurfaceDB.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Surface"),
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

                try
                {
                    List<Alignment> allAlignments = db.ListOfType<Alignment>(tx).OrderBy(x => x.Name).ToList();

                    #region Create surface profiles

                    #region Get terrain layer id

                    LayerTable lt = db.LayerTableId.GetObject(OpenMode.ForRead) as LayerTable;
                    string terrainLayerName = "0_TERRAIN_PROFILE";
                    Oid terrainLayerId = Oid.Null;
                    if (!lt.Has(terrainLayerName))
                    {
                        LayerTableRecord ltr = new LayerTableRecord();
                        ltr.Name = terrainLayerName;
                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 34);
                        lt.CheckOrOpenForWrite();
                        terrainLayerId = lt.Add(ltr);
                        tx.AddNewlyCreatedDBObject(ltr, true);
                    }
                    else terrainLayerId = lt[terrainLayerName];

                    if (terrainLayerId == Oid.Null)
                    {
                        editor.WriteMessage("Terrain layer missing!");
                        throw new System.Exception("Terrain layer missing!");
                    }

                    #endregion

                    Oid profileStyleId = civilDoc.Styles.ProfileStyles["Terræn"];
                    Oid profileLabelSetStyleId = civilDoc.Styles.LabelSetStyles.ProfileLabelSetStyles["_No Labels"];

                    foreach (Alignment alignment in allAlignments)
                    {
                        Oid surfaceProfileId = Oid.Null;
                        string profileName = $"{alignment.Name}_surface_P";
                        bool noProfileExists = true;
                        ObjectIdCollection pIds = alignment.GetProfileIds();
                        foreach (Oid pId in pIds)
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
                                                profileName, alignment.ObjectId, surface.ObjectId,
                                                terrainLayerId, profileStyleId, profileLabelSetStyleId);
                            editor.WriteMessage($"\nSurface profile created for {alignment.Name}.");
                        }

                        System.Windows.Forms.Application.DoEvents();
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    xRefSurfaceTx.Abort();
                    xRefSurfaceTx.Dispose();
                    xRefSurfaceDB.Dispose();
                    tx.Abort();
                    throw new System.Exception(ex.Message);
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                xRefSurfaceTx.Commit();
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
                DataReferencesOptions dro = new DataReferencesOptions();
                string projectName = dro.ProjectName;
                string etapeName = dro.EtapeName;

                editor.WriteMessage("\n" + GetPathToDataFiles(projectName, etapeName, "Ler"));
                editor.WriteMessage("\n" + GetPathToDataFiles(projectName, etapeName, "Surface"));

                #region Read surface from file
                // open the xref database
                Database xRefSurfaceDB = new Database(false, true);
                xRefSurfaceDB.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Surface"),
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

                xRefLerDB.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Ler"),
                    System.IO.FileShare.Read, false, string.Empty);
                Transaction xRefLerTx = xRefLerDB.TransactionManager.StartTransaction();
                List<Polyline3d> allLinework = xRefLerDB.ListOfType<Polyline3d>(xRefLerTx);
                editor.WriteMessage($"\nNr. of 3D polies: {allLinework.Count}");
                #endregion

                try
                {
                    BlockTableRecord space = (BlockTableRecord)tx.GetObject(localDb.CurrentSpaceId, OpenMode.ForWrite);
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForWrite) as BlockTable;

                    List<Alignment> allAlignments = localDb.ListOfType<Alignment>(tx)
                        .OrderBy(x => x.Name)
                        //.Where(x => x.Name == "20 Rybjerg Allé")
                        .ToList();
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
                    //PointGroupCollection pgs = civilDoc.PointGroups;

                    //for (int i = 0; i < pgs.Count; i++)
                    //{
                    //    PointGroup pg = tx.GetObject(pgs[i], OpenMode.ForRead) as PointGroup;
                    //    if (allAlignments.Any(x => x.Name == pg.Name))
                    //    {
                    //        pg.CheckOrOpenForWrite();
                    //        pg.Update();
                    //        uint[] numbers = pg.GetPointNumbers();

                    //        CogoPointCollection cpc = civilDoc.CogoPoints;

                    //        for (int j = 0; j < numbers.Length; j++)
                    //        {
                    //            uint number = numbers[j];

                    //            if (cpc.Contains(number))
                    //            {
                    //                cpc.Remove(number);
                    //            }
                    //        }

                    //        StandardPointGroupQuery spgqEmpty = new StandardPointGroupQuery();
                    //        spgqEmpty.IncludeNumbers = "";
                    //        pg.SetQuery(spgqEmpty);

                    //        pg.Update();
                    //    }
                    //}
                    #endregion

                    #region Name handling of point names
                    //Used to keep track of point names
                    HashSet<string> pNames = new HashSet<string>();

                    int index = 1;
                    #endregion

                    #region CogoPoint style and label reference

                    Oid cogoPointStyle = civilDoc.Styles.PointStyles["LER KRYDS"];


                    #endregion

                    foreach (Alignment alignment in allAlignments)
                    {
                        #region Create ler data
                        #region ModelSpaces
                        //oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(xRefDB);
                        Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);
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
                        //HostMapApplicationServices.Application. .ActiveProject.
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
                            Oid pgId = civilDoc.PointGroups.Add(alignment.Name);

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
                                xRefLerTx.Abort();
                                xRefSurfaceTx.Abort();
                                xRefLerDB.Dispose();
                                xRefSurfaceDB.Dispose();
                                return;
                            }

                            //Read depth value for type
                            double depth = 0;
                            if (!type.IsNoE())
                            {
                                depth = ReadDoubleParameterFromDataTable(type, dtDybde, "Dybde", 0);
                            }

                            //Read layer value for the object
                            string localLayerName = ReadStringParameterFromDataTable(
                                                ent.Layer, dtKrydsninger, "Layer", 0);

                            #region Populate description field
                            //Populate description field
                            //1. Read size record if it exists
                            MapValue sizeRecord = ReadRecordData(
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
                                    Oid pointId = cogoPoints.Add(p3d, true);
                                    CogoPoint cogoPoint = pointId.Go<CogoPoint>(tx, OpenMode.ForWrite);

                                    //Id of the new Poly3d if type == 3D
                                    Oid newPolyId;

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
                                                Oid ltId = lt.Add(ltr);
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
                                                if (DoesRecordExist(tables, cogoPoint.ObjectId, "CrossingData", "Diameter"))
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
                                                string[] columnNames = new string[1] { "Diameter" };
                                                string[] columnDescrs = new string[1] { "Diameter of crossing pipe" };

                                                if (CreateTable(tables, "CrossingData", "Table holding relevant crossing data",
                                                    columnNames, columnDescrs,
                                                    new Autodesk.Gis.Map.Constants.DataType[1]
                                                    {Autodesk.Gis.Map.Constants.DataType.Character }))
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
                        foreach (Oid oid in pIds)
                        {
                            Profile pt = oid.Go<Profile>(tx);
                            if (pt.Name == $"{alignment.Name}_surface_P") p = pt;
                        }
                        if (p == null)
                        {
                            editor.WriteMessage($"\nNo profile named {alignment.Name}_surface_P found!");
                            xRefLerTx.Abort();
                            xRefSurfaceTx.Abort();
                            xRefLerDB.Dispose();
                            xRefSurfaceDB.Dispose();
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
                            StationPoint sp;
                            try
                            {
                                sp = new StationPoint(cp, alignment);
                            }
                            catch (System.Exception)
                            {
                                continue;
                            }

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
                    xRefLerDB.Dispose();
                    xRefSurfaceDB.Dispose();
                }

                catch (System.Exception ex)
                {
                    xRefLerTx.Abort();
                    xRefLerDB.Dispose();

                    xRefSurfaceTx.Abort();
                    xRefSurfaceDB.Dispose();
                    throw new System.Exception(ex.Message);
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        /// <summary>
        /// New method to create Ler intersection data using PropertySets
        /// Because ODTables are unstable and sometimes do not work
        /// as expected when cloning the objects from drawing.
        /// It is also possible to access data across databases
        /// while ODTables require cloning of objects to host drawing
        /// which is slow and not always works
        /// </summary>
        [CommandMethod("createlerdatapss")]
        public void createlerdatapss()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                DataReferencesOptions dro = new DataReferencesOptions();
                string projectName = dro.ProjectName;
                string etapeName = dro.EtapeName;

                editor.WriteMessage("\n" + GetPathToDataFiles(projectName, etapeName, "Ler"));
                editor.WriteMessage("\n" + GetPathToDataFiles(projectName, etapeName, "Surface"));

                #region Read surface from file
                // open the xref database
                Database xRefSurfaceDB = new Database(false, true);
                xRefSurfaceDB.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Surface"),
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

                xRefLerDB.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Ler"),
                    System.IO.FileShare.Read, false, string.Empty);
                Transaction xRefLerTx = xRefLerDB.TransactionManager.StartTransaction();
                List<Polyline3d> allLinework = xRefLerDB.ListOfType<Polyline3d>(xRefLerTx);
                editor.WriteMessage($"\nNr. of 3D polies: {allLinework.Count}");
                #endregion

                try
                {
                    BlockTableRecord space = (BlockTableRecord)tx.GetObject(localDb.CurrentSpaceId, OpenMode.ForWrite);
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForWrite) as BlockTable;

                    List<Alignment> allAlignments = localDb.ListOfType<Alignment>(tx)
                        .OrderBy(x => x.Name)
                        //.Where(x => x.Name == "20 Rybjerg Allé")
                        .ToList();
                    HashSet<ProfileView> pvSetExisting = localDb.HashSetOfType<ProfileView>(tx);

                    #region Check or create propertysetdata to store crossing data
                    //Create property set for CrossingData
                    string diaPropertySetDefName = "CrossingData";
                    PropertySetDefinition propSetDef = default;
                    var dictPropSetDef = new DictionaryPropertySetDefinitions(localDb);
                    if (!dictPropSetDef.Has(diaPropertySetDefName, tx))
                    {
                        //Has not! then create new.
                        propSetDef = new PropertySetDefinition();
                        propSetDef.SetToStandard(localDb);
                        propSetDef.SubSetDatabaseDefaults(localDb);

                        propSetDef.Description = "Table to hold useful crossing data.";
                        bool isStyle = false;
                        var appliedTo = new StringCollection();
                        appliedTo.Add("AeccDbCogoPoint");
                        propSetDef.SetAppliesToFilter(appliedTo, isStyle);

                        var propDefManual = new PropertyDefinition();
                        propDefManual.SetToStandard(localDb);
                        propDefManual.SubSetDatabaseDefaults(localDb);
                        propDefManual.Name = "Diameter";
                        propDefManual.Description = "Stores crossing pipe's diameter";
                        propDefManual.DataType = Autodesk.Aec.PropertyData.DataType.Integer;
                        propDefManual.DefaultData = 0;
                        // add to the prop set def
                        propSetDef.Definitions.Add(propDefManual);

                        using (Transaction propTx = localDb.TransactionManager.StartTransaction())
                        {
                            dictPropSetDef.AddNewRecord(diaPropertySetDefName, propSetDef);
                            propTx.AddNewlyCreatedDBObject(propSetDef, true);
                            propTx.Commit();
                        }
                    }

                    Oid diaPropSetDefId = dictPropSetDef.GetAt(diaPropertySetDefName);
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
                    //PointGroupCollection pgs = civilDoc.PointGroups;

                    //for (int i = 0; i < pgs.Count; i++)
                    //{
                    //    PointGroup pg = tx.GetObject(pgs[i], OpenMode.ForRead) as PointGroup;
                    //    if (allAlignments.Any(x => x.Name == pg.Name))
                    //    {
                    //        pg.CheckOrOpenForWrite();
                    //        pg.Update();
                    //        uint[] numbers = pg.GetPointNumbers();

                    //        CogoPointCollection cpc = civilDoc.CogoPoints;

                    //        for (int j = 0; j < numbers.Length; j++)
                    //        {
                    //            uint number = numbers[j];

                    //            if (cpc.Contains(number))
                    //            {
                    //                cpc.Remove(number);
                    //            }
                    //        }

                    //        StandardPointGroupQuery spgqEmpty = new StandardPointGroupQuery();
                    //        spgqEmpty.IncludeNumbers = "";
                    //        pg.SetQuery(spgqEmpty);

                    //        pg.Update();
                    //    }
                    //}
                    #endregion

                    #region Name handling of point names
                    //Used to keep track of point names
                    HashSet<string> pNames = new HashSet<string>();

                    int index = 1;
                    #endregion

                    #region CogoPoint style and label reference

                    Oid cogoPointStyle = civilDoc.Styles.PointStyles["LER KRYDS"];

                    #endregion

                    foreach (Alignment alignment in allAlignments)
                    {
                        #region Create ler data
                        #region ModelSpaces
                        //oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(xRefDB);
                        Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);
                        #endregion

                        #region Do not clone anymore, use property sets

                        BlockTable acBlkTbl = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                        BlockTableRecord acBlkTblRec =
                            tx.GetObject(acBlkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                        Plane plane = new Plane();

                        int intersections = 0;

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
                                    sourceEnts.Add(ent);
                                }
                            }
                        }

                        editor.WriteMessage($"\nTotal {intersections} intersections detected.");
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
                            Oid pgId = civilDoc.PointGroups.Add(alignment.Name);

                            pg = pgId.GetObject(OpenMode.ForWrite) as PointGroup;
                        }
                        #endregion

                        #region Create Points, assign elevation, layer and OD
                        foreach (Entity ent in sourceEnts)
                        {
                            #region Read data parameters from csvs
                            //Read 'Type' value
                            string type = ReadStringParameterFromDataTable(ent.Layer, dtKrydsninger, "Type", 0);
                            if (type.IsNoE())
                            {
                                editor.WriteMessage($"\nFejl: For lag {ent.Layer} mangler der enten " +
                                    $"selve definitionen eller 'Type'!");
                                xRefLerTx.Abort();
                                xRefSurfaceTx.Abort();
                                xRefLerDB.Dispose();
                                xRefSurfaceDB.Dispose();
                                return;
                            }

                            //Read depth value for type
                            double depth = 0;
                            if (!type.IsNoE())
                            {
                                depth = ReadDoubleParameterFromDataTable(type, dtDybde, "Dybde", 0);
                            }

                            //Read layer value for the object
                            string localLayerName = ReadStringParameterFromDataTable(
                                                ent.Layer, dtKrydsninger, "Layer", 0);
                            #endregion

                            #region Populate description field
                            //???????????????????????????
                            ////Populate description field
                            ////1. Read size record if it exists
                            //MapValue sizeRecord = Utils.ReadRecordData(
                            //    tables, ent.ObjectId, "SizeTable", "Size");
                            //int SizeTableSize = 0;
                            //string sizeDescrPart = "";
                            //if (sizeRecord != null)
                            //{
                            //    SizeTableSize = sizeRecord.Int32Value;
                            //    sizeDescrPart = $"ø{SizeTableSize}";
                            //}
                            //???????????????????????????

                            //2. Read description from Krydsninger
                            string descrFromKrydsninger = ReadStringParameterFromDataTable(
                                ent.Layer, dtKrydsninger, "Description", 0);

                            //2.1 Read the formatting in the description field
                            List<(string ToReplace, string ColumnName)> descriptionReplacePartsList = null;
                            if (descrFromKrydsninger.IsNotNoE())
                                descriptionReplacePartsList = FindDescriptionParts(descrFromKrydsninger);

                            //Finally: Compose description field
                            List<string> descrParts = new List<string>();

                            //??????????????????????????????????????????
                            //1. Add custom size
                            //if (SizeTableSize != 0) descrParts.Add(sizeDescrPart);
                            //??????????????????????????????????????????

                            ObjectIdCollection psIds = PropertyDataServices.GetPropertySets(ent);
                            List<PropertySet> pss = new List<PropertySet>();
                            foreach (Oid oid in psIds) pss.Add(oid.Go<PropertySet>(xRefLerTx));

                            //2. Process and add parts from format bits in OD
                            if (descrFromKrydsninger.IsNotNoE())
                            {
                                //Interpolate description from Krydsninger with format setting, if they exist
                                if (descriptionReplacePartsList != null && descriptionReplacePartsList.Count > 0)
                                {
                                    for (int i = 0; i < descriptionReplacePartsList.Count; i++)
                                    {
                                        var tuple = descriptionReplacePartsList[i];
                                        string result = ReadDescriptionPartValueFromPS(
                                            pss, ent, tuple.ColumnName, dtKrydsninger);
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
                            string pName = "";
                            pName = ent.Handle.ToString();

                            #region Create points
                            using (Point3dCollection p3dcol = new Point3dCollection())
                            {
                                alignment.IntersectWith(ent, 0, plane, p3dcol, new IntPtr(0), new IntPtr(0));

                                int count = 1;
                                foreach (Point3d p3d in p3dcol)
                                {
                                    Oid pointId = cogoPoints.Add(p3d, true);
                                    CogoPoint cogoPoint = pointId.Go<CogoPoint>(tx, OpenMode.ForWrite);

                                    //Id of the new Poly3d if type == 3D
                                    Oid newPolyId;

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
                                                editor.WriteMessage($"\nFor type 3D entity {ent.Handle.ToString()}" +
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
                                                Oid ltId = lt.Add(ltr);
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

                                    #region Copy OD from polies to the new point -> Not any longer
                                    //Copy specific OD from cloned 3D polies to the new point

                                    //Use PropertySets now!!!

                                    //List<(string TableName, string RecordName)> odList =
                                    //    new List<(string TableName, string RecordName)>();
                                    //odList.Add(("IdRecord", "Handle"));
                                    //TryCopySpecificOD(tables, ent, cogoPoint, odList);
                                    #endregion

                                    #region Attach property set
                                    //Attach property set to CogoPoint
                                    //Check or creation of the set happen at the top now
                                    using (Transaction txAttachPs = localDb.TransactionManager.StartTransaction())
                                    {
                                        cogoPoint.CheckOrOpenForWrite();
                                        PropertyDataServices.AddPropertySet(cogoPoint, diaPropSetDefId);
                                        txAttachPs.Commit();
                                    }
                                    #endregion

                                    #region Get the newly attached property set
                                    ObjectIdCollection propertySetIds = PropertyDataServices.GetPropertySets(cogoPoint);
                                    List<PropertySet> propertySets = new List<PropertySet>();
                                    foreach (Oid oid in propertySetIds) propertySets.Add(oid.Go<PropertySet>(tx, OpenMode.ForWrite));
                                    PropertySet pSet = propertySets.Find(
                                        x => x.PropertySetDefinitionName == diaPropertySetDefName);
                                    #endregion

                                    #region Populate diameter property
                                    List<(string TableName,
                                            string RecordName,
                                            string PropertyName,
                                            Autodesk.Aec.PropertyData.DataType dataType)> odList =
                                        new List<(
                                            string TableName,
                                            string RecordName,
                                            string PropertyName,
                                            Autodesk.Aec.PropertyData.DataType dataType)>();
                                    //Fetch diameter definitions if any
                                    string diaDef = ReadStringParameterFromDataTable(ent.Layer,
                                        dtKrydsninger, "Diameter", 0);

                                    if (diaDef.IsNotNoE())
                                    {
                                        var list = FindDescriptionParts(diaDef);
                                        //Be careful if FindDescriptionParts implementation changes
                                        string[] parts = list[0].Item2.Split(':');
                                        odList.Add((parts[0], parts[1], "Diameter", Autodesk.Aec.PropertyData.DataType.Integer));
                                    }

                                    foreach (var item in odList)
                                    {
                                        object value = ReadPropertyValueFromPS(
                                            pss, ent, item.TableName, item.RecordName);

                                        if (value != null)
                                        {
                                            int psIdCurrent = pSet.PropertyNameToId(item.PropertyName);
                                            pSet.SetAt(psIdCurrent, value);
                                        }
                                    }
                                    #endregion

                                    //Reference newly created cogoPoint to gathering collection
                                    allNewlyCreatedPoints.Add(cogoPoint);
                                }
                            }
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
                        foreach (Oid oid in pIds)
                        {
                            Profile pt = oid.Go<Profile>(tx);
                            if (pt.Name == $"{alignment.Name}_surface_P") p = pt;
                        }
                        if (p == null)
                        {
                            editor.WriteMessage($"\nNo profile named {alignment.Name}_surface_P found!");
                            xRefLerTx.Abort();
                            xRefSurfaceTx.Abort();
                            xRefLerDB.Dispose();
                            xRefSurfaceDB.Dispose();
                            return;
                        }
                        else editor.WriteMessage($"\nProfile {p.Name} found!");

                        //Sorting is not verified!!!
                        //Must be sorted from start alignment to end
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
                            StationPoint sp;
                            try
                            {
                                sp = new StationPoint(cp, alignment);
                            }
                            catch (System.Exception)
                            {
                                continue;
                            }

                            int counter = 0;
                            foreach (ProfileView pv in pvs)
                            {
                                //Sorting of ProfileViews is not verified!!!
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
                    xRefLerDB.Dispose();
                    xRefSurfaceDB.Dispose();
                }

                catch (System.Exception ex)
                {
                    xRefLerTx.Abort();
                    xRefLerDB.Dispose();

                    xRefSurfaceTx.Abort();
                    xRefSurfaceDB.Dispose();
                    throw new System.Exception(ex.Message);
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("updateprofile")]
        public void updateprofile()
        {

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                DataReferencesOptions dro = new DataReferencesOptions();
                string projectName = dro.ProjectName;
                string etapeName = dro.EtapeName;

                editor.WriteMessage("\n" + GetPathToDataFiles(projectName, etapeName, "Ler"));
                editor.WriteMessage("\n" + GetPathToDataFiles(projectName, etapeName, "Fremtid"));

                #region Load linework from LER Xref
                // open the LER dwg database
                Database xRefLerDB = new Database(false, true);

                xRefLerDB.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Ler"),
                    System.IO.FileShare.Read, false, string.Empty);
                Transaction xRefLerTx = xRefLerDB.TransactionManager.StartTransaction();
                #endregion

                #region Load linework from Fremtid Xref
                // open the Fremtid dwg database
                Database xRefFremtidDB = new Database(false, true);

                xRefFremtidDB.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Fremtid"),
                    System.IO.FileShare.Read, false, string.Empty);
                Transaction xRefFremtidTx = xRefFremtidDB.TransactionManager.StartTransaction();
                #endregion

                try
                {
                    #region Load blocks from Fremtid -- NOT USED NOW
                    //HashSet<string> allExistingNames = File.ReadAllLines(@"X:\AutoCAD DRI - 01 Civil 3D\SymbolNames.txt")
                    //                                       .Distinct().ToHashSet();

                    //BlockTable bt = xRefFremtidTx.GetObject(xRefFremtidDB.BlockTableId, OpenMode.ForRead) as BlockTable;
                    //BlockTableRecord btr = tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead)
                    //    as BlockTableRecord;
                    //foreach (oid oid in btr)
                    //{
                    //    if (oid.ObjectClass.Name == "AcDbBlockReference")
                    //    {
                    //        BlockReference br = oid.Go<BlockReference>(tx);
                    //        if (!allExistingNames.Contains(br.Name))
                    //        {
                    //            editor.WriteMessage($"\n{br.Name}");
                    //        }
                    //    }
                    //}
                    #endregion

                    HashSet<Alignment> allAls = localDb.HashSetOfType<Alignment>(tx);

                    Alignment selectedAl = allAls.Where(x => x.Name == "11 Brogårdsvej - Tjørnestien").FirstOrDefault();
                    if (selectedAl == null) throw new System.Exception("Selection of alignment failed! -> null");


                }
                catch (System.Exception ex)
                {
                    xRefLerTx.Abort();
                    xRefLerDB.Dispose();
                    xRefFremtidTx.Abort();
                    xRefFremtidDB.Dispose();
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                xRefLerTx.Abort();
                xRefLerDB.Dispose();
                xRefFremtidTx.Abort();
                xRefFremtidDB.Dispose();
                tx.Commit();
            }
        }

        [CommandMethod("scaleallblocks")]
        public void scaleallblocks()
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
                    System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(
                                            @"X:\AutoCAD DRI - 01 Civil 3D\FJV Komponenter.csv", "FjvKomponenter");

                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead)
                        as BlockTableRecord;

                    foreach (Oid oid in btr)
                    {
                        if (oid.ObjectClass.Name == "AcDbBlockReference")
                        {
                            BlockReference br = oid.Go<BlockReference>(tx);

                            if (ReadStringParameterFromDataTable(br.Name, fjvKomponenter, "Navn", 0) != null &&
                                ReadStringParameterFromDataTable(br.Name, fjvKomponenter, "Type", 0) == "Reduktion")
                            {
                                br.CheckOrOpenForWrite();
                                br.ScaleFactors = new Scale3d(2);
                                //prdDbg(br.Name);
                                //prdDbg(br.Rotation.ToString());
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

        [CommandMethod("FLIPBLOCK", CommandFlags.UsePickSet)]
        [CommandMethod("FB", CommandFlags.UsePickSet)]
        public static void flipblock()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                // Get the PickFirst selection set
                PromptSelectionResult acSSPrompt;
                acSSPrompt = ed.SelectImplied();
                SelectionSet acSSet;
                // If the prompt status is OK, objects were selected before
                // the command was started

                if (acSSPrompt.Status == PromptStatus.OK)
                {
                    acSSet = acSSPrompt.Value;
                    var Ids = acSSet.GetObjectIds();
                    foreach (Oid oid in Ids)
                    {
                        if (oid.ObjectClass.Name != "AcDbBlockReference") continue;
                        using (Transaction tx = localDb.TransactionManager.StartTransaction())
                        {
                            try
                            {
                                BlockReference br = oid.Go<BlockReference>(tx, OpenMode.ForWrite);
                                br.Rotation = br.Rotation + 3.14159;

                            }
                            catch (System.Exception ex)
                            {
                                tx.Abort();
                                prdDbg("3: " + ex.Message);
                                continue;
                            }
                            tx.Commit();
                        }
                    }
                }
                else
                {
                    ed.WriteMessage("\nSelect before running command!");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage(ex.Message);
            }
        }

        [CommandMethod("exportallblocknames")]
        public void exportallblocknames()
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
                    System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(@"X:\AutoCAD DRI - 01 Civil 3D\FJV Komponenter.csv", "FjvKomponenter");

                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead)
                        as BlockTableRecord;
                    HashSet<string> allNamesNotInDb = new HashSet<string>();

                    //int count = fjvKomponenter.Rows.Count;
                    //HashSet<string> dbNames = new HashSet<string>();
                    //for (int i = 0; i < count; i++)
                    //{
                    //    System.Data.DataRow row = fjvKomponenter.Rows[i];
                    //    dbNames.Add(row.ItemArray[0].ToString());
                    //}

                    StringBuilder sb = new StringBuilder();

                    //sb.AppendLine("Navn;");

                    foreach (Oid oid in btr)
                    {
                        if (oid.ObjectClass.Name == "AcDbBlockReference")
                        {
                            BlockReference br = oid.Go<BlockReference>(tx);
                            //if (!dbNames.Contains(br.Name))
                            //{
                            //    allNamesNotInDb.Add(br.Name);
                            //}
                            if (ReadStringParameterFromDataTable(br.Name, fjvKomponenter, "Navn", 0) != null)
                            {
                                sb.AppendLine(br.Name + ";");
                                allNamesNotInDb.Add(br.Name);
                            }
                        }
                    }

                    ClrFile(@"X:\0371-1158 - Gentofte Fase 4 - Dokumenter\01 Intern\02 Tegninger\" +
                            @"01 Autocad\Autocad\01 Views\4.3\Komponenter\Komponenter export 4.3.csv");

                    OutputWriter(@"X:\0371-1158 - Gentofte Fase 4 - Dokumenter\01 Intern\02 Tegninger\" +
                                 @"01 Autocad\Autocad\01 Views\4.3\Komponenter\Komponenter export 4.3.csv", sb.ToString());

                    foreach (string name in allNamesNotInDb.OrderBy(x => x))
                    {
                        editor.WriteMessage($"\n{name}");
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("LISTNONSTANDARDBLOCKNAMES")]
        public void listnonstandardblocknames()
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
                    System.Data.DataTable stdBlocks = CsvReader.ReadCsvToDataTable(@"X:\AutoCAD DRI - 01 Civil 3D\FJV Komponenter.csv", "FjvKomponenter");
                    System.Data.DataTable dynBlocks = CsvReader.ReadCsvToDataTable(@"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");

                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead)
                        as BlockTableRecord;
                    HashSet<string> allNamesNotInDb = new HashSet<string>();

                    //int count = fjvKomponenter.Rows.Count;
                    //HashSet<string> dbNames = new HashSet<string>();
                    //for (int i = 0; i < count; i++)
                    //{
                    //    System.Data.DataRow row = fjvKomponenter.Rows[i];
                    //    dbNames.Add(row.ItemArray[0].ToString());
                    //}

                    foreach (Oid oid in btr)
                    {
                        if (oid.ObjectClass.Name == "AcDbBlockReference")
                        {
                            BlockReference br = oid.Go<BlockReference>(tx);
                            //if (!dbNames.Contains(br.Name))
                            //{
                            //    allNamesNotInDb.Add(br.Name);
                            //}

                            string effectiveName = br.IsDynamicBlock ?
                                        ((BlockTableRecord)tx.GetObject(
                                            br.DynamicBlockTableRecord, OpenMode.ForRead)).Name :
                                            br.Name;

                            if (br.IsDynamicBlock)
                            {
                                //Dynamic
                                if (ReadStringParameterFromDataTable(effectiveName, dynBlocks, "Navn", 0) == null)
                                {
                                    allNamesNotInDb.Add(effectiveName);
                                }
                            }
                            else
                            {
                                //Ordinary
                                if (ReadStringParameterFromDataTable(br.Name, stdBlocks, "Navn", 0) == null)
                                {
                                    allNamesNotInDb.Add(effectiveName);
                                }
                            }

                        }
                    }

                    foreach (string name in allNamesNotInDb.OrderBy(x => x))
                    {
                        editor.WriteMessage($"\n{name}");
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("LISTDYNAMICANDANONBLOCKS")]
        public void listanonblocks()
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
                    System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(@"X:\AutoCAD DRI - 01 Civil 3D\FJV Komponenter.csv", "FjvKomponenter");

                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead)
                        as BlockTableRecord;
                    HashSet<string> allNamesNotInDb = new HashSet<string>();

                    foreach (Oid oid in btr)
                    {
                        if (oid.ObjectClass.Name == "AcDbBlockReference")
                        {
                            BlockReference br = oid.Go<BlockReference>(tx);

                            if (br.Name.StartsWith("*") || br.IsDynamicBlock)
                            {
                                using (Transaction tr = Application.DocumentManager.MdiActiveDocument.TransactionManager.StartTransaction())
                                {
                                    string effectiveName = br.IsDynamicBlock ?
                                                                ((BlockTableRecord)tr.GetObject(
                                                                    br.DynamicBlockTableRecord, OpenMode.ForRead)).Name : br.Name;
                                    allNamesNotInDb.Add($"{br.Name} -> {effectiveName}");
                                    tr.Commit();
                                }
                            }
                        }
                    }

                    foreach (string name in allNamesNotInDb.OrderBy(x => x))
                    {
                        editor.WriteMessage($"\n{name}");
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("testanonblocks", CommandFlags.UsePickSet)]
        public void testanonblocks()
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
                    System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(@"X:\AutoCAD DRI - 01 Civil 3D\FJV Komponenter.csv", "FjvKomponenter");

                    HashSet<string> allNamesNotInDb = new HashSet<string>();

                    // Get the PickFirst selection set
                    PromptSelectionResult acSSPrompt;
                    acSSPrompt = editor.SelectImplied();
                    SelectionSet acSSet;
                    // If the prompt status is OK, objects were selected before
                    // the command was started

                    if (acSSPrompt.Status == PromptStatus.OK)
                    {
                        acSSet = acSSPrompt.Value;
                        var Ids = acSSet.GetObjectIds();
                        foreach (Oid oid in Ids)
                        {
                            if (oid.ObjectClass.Name == "AcDbBlockReference")
                            {
                                BlockReference br = oid.Go<BlockReference>(tx);

                                prdDbg(br.Name);

                                BlockTableRecord btr = tx.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;

                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        public void assignblockstoalignmentsOLD()
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
                    System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(@"X:\AutoCAD DRI - 01 Civil 3D\FJV Komponenter.csv", "FjvKomponenter");

                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead)
                        as BlockTableRecord;
                    HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);

                    #region CreateLayers
                    LayerTable lt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;
                    foreach (Alignment al in als)
                    {
                        if (!lt.Has(al.Name))
                        {
                            //Create layer if it doesn't exist
                            try
                            {
                                //Validate the name of layer
                                //It throws an exception if not, so need to catch it
                                SymbolUtilityServices.ValidateSymbolName(al.Name, false);
                                LayerTableRecord ltr = new LayerTableRecord();
                                ltr.Name = al.Name;
                                //Make layertable writable
                                lt.UpgradeOpen();
                                //Add the new layer to layer table
                                Oid ltId = lt.Add(ltr);
                                tx.AddNewlyCreatedDBObject(ltr, true);
                            }
                            catch (System.Exception)
                            {
                                //Eat the exception and continue
                                //localLayerExists must remain false
                            }
                        }
                    }
                    #endregion

                    foreach (Oid oid in btr)
                    {
                        if (oid.ObjectClass.Name == "AcDbBlockReference")
                        {
                            BlockReference br = oid.Go<BlockReference>(tx);
                            if (ReadStringParameterFromDataTable(br.Name, fjvKomponenter, "Navn", 0) != null)
                            {
                                HashSet<(BlockReference block, double dist, string layName)> alDistTuples = new HashSet<(BlockReference, double, string)>();
                                try
                                {
                                    foreach (Alignment al in als)
                                    {
                                        Point3d closestPoint = al.GetClosestPointTo(br.Position, false);
                                        if (closestPoint != null)
                                        {
                                            alDistTuples.Add((br, br.Position.DistanceHorizontalTo(closestPoint), al.Name));
                                        }
                                    }
                                }
                                catch (System.Exception) { };

                                var result = alDistTuples.MinBy(x => x.Item2).FirstOrDefault();

                                if (default != result)
                                {
                                    br.CheckOrOpenForWrite();
                                    br.Layer = result.layName;
                                    AttributeCollection atts = br.AttributeCollection;
                                    foreach (Oid attOid in atts)
                                    {
                                        AttributeReference att = attOid.Go<AttributeReference>(tx, OpenMode.ForWrite);
                                        if (att.Tag == "Delstrækning")
                                        {
                                            att.CheckOrOpenForWrite();
                                            att.TextString = result.layName;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        public void assignblocksandplinestoalignmentsOLD()
        {

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            const string kwd1 = "Ja";
            const string kwd2 = "Nej";

            PromptKeywordOptions pKeyOpts = new PromptKeywordOptions("");
            pKeyOpts.Message = "\nOverskriv? ";
            pKeyOpts.Keywords.Add(kwd1);
            pKeyOpts.Keywords.Add(kwd2);
            pKeyOpts.AllowNone = true;
            pKeyOpts.Keywords.Default = kwd2;
            PromptResult pKeyRes = editor.GetKeywords(pKeyOpts);
            bool overwrite = pKeyRes.StringResult == kwd1;

            DataReferencesOptions dro = new DataReferencesOptions();
            string projectName = dro.ProjectName;
            string etapeName = dro.EtapeName;

            // open the xref database
            Database alDb = new Database(false, true);
            alDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Alignments"),
                System.IO.FileShare.Read, false, string.Empty);
            Transaction alTx = alDb.TransactionManager.StartTransaction();

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(
                        @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");

                    HashSet<Alignment> als = alDb.HashSetOfType<Alignment>(alTx);
                    HashSet<Curve> curves = localDb.HashSetOfType<Curve>(tx);
                    HashSet<BlockReference> brs = localDb.HashSetOfType<BlockReference>(tx);

                    //******************************//
                    string xRecordName = "Alignment";
                    //******************************//

                    foreach (BlockReference br in brs)
                    {
                        if (ReadStringParameterFromDataTable(br.RealName(), fjvKomponenter, "Navn", 0) != null)
                        {
                            //Skip if record already exists
                            if (!overwrite)
                            {
                                if (br.XrecReadStringAtIndex(xRecordName, 0).IsNotNoE()) continue;
                                prdDbg("Passed into blocks!");
                            }

                            HashSet<(BlockReference block, double dist, Alignment al)> alDistTuples =
                                new HashSet<(BlockReference, double, Alignment)>();
                            try
                            {
                                foreach (Alignment al in als)
                                {
                                    if (al.Length < 1) continue;
                                    Point3d closestPoint = al.GetClosestPointTo(br.Position, false);
                                    if (closestPoint != null)
                                    {
                                        alDistTuples.Add((br, br.Position.DistanceHorizontalTo(closestPoint), al));
                                    }
                                }
                            }
                            catch (System.Exception)
                            {
                                prdDbg("Error in GetClosestPointTo -> loop incomplete!");
                            }

                            double distThreshold = 0.15;
                            var result = alDistTuples.Where(x => x.dist < distThreshold);

                            if (result.Count() == 0)
                            {
                                //If the component cannot find an alignment
                                //Repeat with increasing threshold
                                for (int i = 0; i < 4; i++)
                                {
                                    distThreshold += 0.1;
                                    if (result.Count() != 0) break;
                                    if (i == 3)
                                    {
                                        //Red line means check result
                                        //This is caught if no result found at ALL
                                        Line line = new Line(new Point3d(), br.Position);
                                        line.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                        line.AddEntityToDbModelSpace(localDb);
                                    }
                                }

                                if (result.Count() > 0)
                                {
                                    //This is caught if a result was found after some iterations
                                    //So the result must be checked to see, if components
                                    //Not belonging to the alignment got selected
                                    //Magenta
                                    Line line = new Line(new Point3d(), br.Position);
                                    line.Color = Color.FromColorIndex(ColorMethod.ByAci, 6);
                                    line.AddEntityToDbModelSpace(localDb);
                                }
                            }

                            if (result.Count() == 0)
                            {
                                XrecordCreateWriteUpdateString(br, xRecordName, new[] { "NA" });
                            }
                            else if (result.Count() == 2)
                            {//Should be ordinary branch
                                var first = result.First();
                                var second = result.Skip(1).First();

                                double rotation = br.Rotation;
                                Vector3d brDir = new Vector3d(Math.Cos(rotation), Math.Sin(rotation), 0);

                                //First
                                Point3d firstClosestPoint = first.al.GetClosestPointTo(br.Position, false);
                                Vector3d firstDeriv = first.al.GetFirstDerivative(firstClosestPoint);
                                double firstDotProduct = Math.Abs(brDir.DotProduct(firstDeriv));
                                //prdDbg($"Rotation: {rotation} - First: {first.al.Name}: {Math.Atan2(firstDeriv.Y, firstDeriv.X)}");
                                //prdDbg($"Dot product: {brDir.DotProduct(firstDeriv)}");

                                //Second
                                Point3d secondClosestPoint = second.al.GetClosestPointTo(br.Position, false);
                                Vector3d secondDeriv = second.al.GetFirstDerivative(secondClosestPoint);
                                double secondDotProduct = Math.Abs(brDir.DotProduct(secondDeriv));
                                //prdDbg($"Rotation: {rotation} - Second: {second.al.Name}: {Math.Atan2(secondDeriv.Y, secondDeriv.X)}");
                                //prdDbg($"Dot product: {brDir.DotProduct(secondDeriv)}");

                                Alignment mainAl = null;
                                Alignment branchAl = null;

                                if (firstDotProduct > 0.9)
                                {
                                    mainAl = first.al;
                                    branchAl = second.al;
                                }
                                else if (secondDotProduct > 0.9)
                                {
                                    mainAl = second.al;
                                    branchAl = first.al;
                                }
                                else
                                {
                                    //Case: Inconclusive
                                    //When the main axis of the block
                                    //Is not aligned with one of the runs
                                    //Annotate with a line for checking
                                    //And must be manually annotated
                                    //Yellow
                                    Line line = new Line(new Point3d(), first.block.Position);
                                    line.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);
                                    line.AddEntityToDbModelSpace(localDb);
                                    continue;
                                }

                                XrecordCreateWriteUpdateString(br, xRecordName,
                                    new[] { mainAl.Name, branchAl.Name });
                            }
                            else if (result.Count() > 2)
                            {//More alignments meeting in one place?
                             //Possible but not seen yet
                             //Cyan
                                var first = result.First();
                                Line line = new Line(new Point3d(), first.block.Position);
                                line.Color = Color.FromColorIndex(ColorMethod.ByAci, 4);
                                line.AddEntityToDbModelSpace(localDb);
                            }
                            else if (result.Count() == 1)
                            {
                                XrecordCreateWriteUpdateString(br, xRecordName,
                                    new[] { result.First().al.Name });
                            }
                        }
                    }

                    foreach (Curve curve in curves)
                    {
                        //Skip if record already exists
                        if (!overwrite)
                        {
                            if (curve.XrecReadStringAtIndex(xRecordName, 0).IsNotNoE()) continue;
                        }
                        prdDbg(curve.Layer);
                        if (!(curve.Layer.Contains("FREM") ||
                            curve.Layer.Contains("RETUR") ||
                            curve.Layer.Contains("TWIN"))) continue;

                        HashSet<(Curve curve, double dist, Alignment al)> alDistTuples =
                            new HashSet<(Curve curve, double dist, Alignment al)>();

                        try
                        {
                            foreach (Alignment al in als)
                            {
                                if (al.Length < 1) continue;
                                double midParam = curve.EndParam / 2.0;
                                Point3d curveMidPoint = curve.GetPointAtParameter(midParam);
                                Point3d closestPoint = al.GetClosestPointTo(curveMidPoint, false);
                                if (closestPoint != null)
                                    alDistTuples.Add((curve, curveMidPoint.DistanceHorizontalTo(closestPoint), al));
                            }
                        }
                        catch (System.Exception)
                        {
                            prdDbg("Error in Curves GetClosestPointTo -> loop incomplete!");
                        }

                        double distThreshold = 1;
                        var result = alDistTuples.Where(x => x.dist < distThreshold);

                        if (result.Count() == 0)
                        {
                            XrecordCreateWriteUpdateString(curve, xRecordName, new[] { "NA" });
                            //Yellow line means check result
                            //This is caught if no result found at ALL
                            Line line = new Line(new Point3d(), curve.GetPointAtParameter(curve.EndParam / 2));
                            line.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);
                            line.AddEntityToDbModelSpace(localDb);
                        }
                        else if (result.Count() == 1)
                        {
                            XrecordCreateWriteUpdateString(curve, xRecordName, new[] { result.First().al.Name });
                        }
                        else if (result.Count() > 1)
                        {
                            //If multiple result
                            //Means midpoint is close to two alignments
                            //Sample more points to determine

                            double oneFourthParam = curve.EndParam / 4;
                            Point3d oneFourthPoint = curve.GetPointAtParameter(oneFourthParam);
                            double threeFourthParam = curve.EndParam / 4 * 3;
                            Point3d threeFourthPoint = curve.GetPointAtParameter(threeFourthParam);

                            var resArray = result.ToArray();

                            distThreshold = 0.1;
                            double distIncrement = 0.1;

                            bool alDetected = false;
                            Alignment detectedAl = null;
                            while (!alDetected)
                            {
                                for (int i = 0; i < resArray.Count(); i++)
                                {
                                    if (alDetected) break;
                                    detectedAl = resArray[i].al;

                                    Point3d oneFourthClosestPoint = detectedAl.GetClosestPointTo(oneFourthPoint, false);
                                    Point3d threeFourthClosestPoint = detectedAl.GetClosestPointTo(threeFourthPoint, false);

                                    //DBPoint p1 = new DBPoint(oneFourthClosestPoint);
                                    //if (i == 0) p1.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                    //else p1.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);
                                    //p1.AddEntityToDbModelSpace(localDb);
                                    //DBPoint p2 = new DBPoint(threeFourthClosestPoint);
                                    //if (i == 0) p2.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                    //else p2.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);
                                    //p2.AddEntityToDbModelSpace(localDb);

                                    if (oneFourthPoint.DistanceHorizontalTo(oneFourthClosestPoint) < distThreshold &&
                                        threeFourthPoint.DistanceHorizontalTo(threeFourthClosestPoint) < distThreshold)
                                        alDetected = true;
                                }

                                distThreshold += distIncrement;
                            }

                            XrecordCreateWriteUpdateString(curve, xRecordName, new[] { detectedAl?.Name ?? "NA" });

                            //Red line means check result
                            //This is caught if multiple results
                            Line line = new Line(new Point3d(), curve.GetPointAtParameter(curve.EndParam / 2));
                            line.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                            line.AddEntityToDbModelSpace(localDb);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    alTx.Abort();
                    alTx.Dispose();
                    alDb.Dispose();
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                alTx.Abort();
                alTx.Dispose();
                alDb.Dispose();
                tx.Commit();
            }
        }

        [CommandMethod("ASSIGNBLOCKSANDPLINESTOALIGNMENTS")]
        public void assignblocksandplinestoalignments()
        {

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            const string kwd1 = "Ja";
            const string kwd2 = "Nej";

            PromptKeywordOptions pKeyOpts = new PromptKeywordOptions("");
            pKeyOpts.Message = "\nOverskriv? ";
            pKeyOpts.Keywords.Add(kwd1);
            pKeyOpts.Keywords.Add(kwd2);
            pKeyOpts.AllowNone = true;
            pKeyOpts.Keywords.Default = kwd2;
            PromptResult pKeyRes = editor.GetKeywords(pKeyOpts);
            bool overwrite = pKeyRes.StringResult == kwd1;

            DataReferencesOptions dro = new DataReferencesOptions();
            string projectName = dro.ProjectName;
            string etapeName = dro.EtapeName;

            // open the xref database
            Database alDb = new Database(false, true);
            alDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Alignments"),
                System.IO.FileShare.Read, false, string.Empty);
            Transaction alTx = alDb.TransactionManager.StartTransaction();

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(
                        @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");

                    HashSet<Alignment> als = alDb.HashSetOfType<Alignment>(alTx);
                    HashSet<Curve> curves = localDb.HashSetOfType<Curve>(tx);
                    HashSet<BlockReference> brs = localDb.HashSetOfType<BlockReference>(tx);

                    //******************************//
                    PropertySetManager.DefinedSets propertySetName =
                        PropertySetManager.DefinedSets.DriPipelineData;
                    string belongsToAlignmentProperty = "BelongsToAlignment";
                    string branchesOffToAlignmentProperty = "BranchesOffToAlignment";
                    //******************************//

                    #region Initialize property set
                    PropertySetManager psm = new PropertySetManager(
                        localDb,
                        propertySetName);
                    #endregion

                    #region Blocks
                    foreach (BlockReference br in brs)
                    {
                        //Guard against unknown blocks
                        if (ReadStringParameterFromDataTable(
                            br.RealName(), fjvKomponenter, "Navn", 0) == null)
                            continue;

                        //Check if a property set is attached
                        //Attach if not
                        psm.GetOrAttachPropertySet(br);

                        //Skip if record already exists
                        if (!overwrite)
                        {
                            if (psm.ReadPropertyString(belongsToAlignmentProperty).IsNotNoE() ||
                                psm.ReadPropertyString(branchesOffToAlignmentProperty).IsNotNoE()) continue;
                        }

                        HashSet<(BlockReference block, double dist, Alignment al)> alDistTuples =
                            new HashSet<(BlockReference, double, Alignment)>();
                        try
                        {
                            foreach (Alignment al in als)
                            {
                                if (al.Length < 1) continue;
                                Point3d closestPoint = al.GetClosestPointTo(br.Position, false);
                                if (closestPoint != null)
                                {
                                    alDistTuples.Add((br, br.Position.DistanceHorizontalTo(closestPoint), al));
                                }
                            }
                        }
                        catch (System.Exception)
                        {
                            prdDbg("Error in GetClosestPointTo -> loop incomplete!");
                        }

                        double distThreshold = 0.15;
                        var result = alDistTuples.Where(x => x.dist < distThreshold);

                        if (result.Count() == 0)
                        {
                            //If the component cannot find an alignment
                            //Repeat with increasing threshold
                            for (int i = 0; i < 4; i++)
                            {
                                distThreshold += 0.1;
                                if (result.Count() != 0) break;
                                if (i == 3)
                                {
                                    //Red line means check result
                                    //This is caught if no result found at ALL
                                    Line line = new Line(new Point3d(), br.Position);
                                    line.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                    line.AddEntityToDbModelSpace(localDb);
                                }
                            }

                            if (result.Count() > 0)
                            {
                                //This is caught if a result was found after some iterations
                                //So the result must be checked to see, if components
                                //Not belonging to the alignment got selected
                                //Magenta
                                Line line = new Line(new Point3d(), br.Position);
                                line.Color = Color.FromColorIndex(ColorMethod.ByAci, 6);
                                line.AddEntityToDbModelSpace(localDb);
                            }
                        }

                        if (result.Count() == 0)
                        {
                            psm.WritePropertyString(belongsToAlignmentProperty, "NA");
                        }
                        else if (result.Count() == 2)
                        {//Should be ordinary branch
                            var first = result.First();
                            var second = result.Skip(1).First();

                            double rotation = br.Rotation;
                            Vector3d brDir = new Vector3d(Math.Cos(rotation), Math.Sin(rotation), 0);

                            //First
                            Point3d firstClosestPoint = first.al.GetClosestPointTo(br.Position, false);
                            Vector3d firstDeriv = first.al.GetFirstDerivative(firstClosestPoint);
                            double firstDotProduct = Math.Abs(brDir.DotProduct(firstDeriv));
                            //prdDbg($"Rotation: {rotation} - First: {first.al.Name}: {Math.Atan2(firstDeriv.Y, firstDeriv.X)}");
                            //prdDbg($"Dot product: {brDir.DotProduct(firstDeriv)}");

                            //Second
                            Point3d secondClosestPoint = second.al.GetClosestPointTo(br.Position, false);
                            Vector3d secondDeriv = second.al.GetFirstDerivative(secondClosestPoint);
                            double secondDotProduct = Math.Abs(brDir.DotProduct(secondDeriv));
                            //prdDbg($"Rotation: {rotation} - Second: {second.al.Name}: {Math.Atan2(secondDeriv.Y, secondDeriv.X)}");
                            //prdDbg($"Dot product: {brDir.DotProduct(secondDeriv)}");

                            Alignment mainAl = null;
                            Alignment branchAl = null;

                            if (firstDotProduct > 0.9)
                            {
                                mainAl = first.al;
                                branchAl = second.al;
                            }
                            else if (secondDotProduct > 0.9)
                            {
                                mainAl = second.al;
                                branchAl = first.al;
                            }
                            else
                            {
                                //Case: Inconclusive
                                //When the main axis of the block
                                //Is not aligned with one of the runs
                                //Annotate with a line for checking
                                //And must be manually annotated
                                //Yellow
                                Line line = new Line(new Point3d(), first.block.Position);
                                line.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);
                                line.AddEntityToDbModelSpace(localDb);
                                continue;
                            }

                            psm.WritePropertyString(belongsToAlignmentProperty, mainAl.Name);
                            psm.WritePropertyString(branchesOffToAlignmentProperty, branchAl.Name);
                        }
                        else if (result.Count() > 2)
                        {//More alignments meeting in one place?
                         //Possible but not seen yet
                         //Cyan
                            var first = result.First();
                            Line line = new Line(new Point3d(), first.block.Position);
                            line.Color = Color.FromColorIndex(ColorMethod.ByAci, 4);
                            line.AddEntityToDbModelSpace(localDb);
                        }
                        else if (result.Count() == 1)
                        {
                            psm.WritePropertyString(belongsToAlignmentProperty, result.First().al.Name);
                        }
                    }
                    #endregion

                    #region Curves
                    foreach (Curve curve in curves)
                    {
                        if (!(curve.Layer.Contains("FREM") ||
                            curve.Layer.Contains("RETUR") ||
                            curve.Layer.Contains("TWIN"))) continue;

                        //Check if a property set is attached
                        //Attach if not
                        psm.GetOrAttachPropertySet(curve);

                        //Skip if record already exists
                        if (!overwrite)
                        {
                            if (psm.ReadPropertyString(belongsToAlignmentProperty).IsNotNoE() ||
                                psm.ReadPropertyString(branchesOffToAlignmentProperty).IsNotNoE()) continue;
                        }

                        HashSet<(Curve curve, double dist, Alignment al)> alDistTuples =
                            new HashSet<(Curve curve, double dist, Alignment al)>();

                        try
                        {
                            foreach (Alignment al in als)
                            {
                                if (al.Length < 1) continue;
                                double midParam = curve.EndParam / 2.0;
                                Point3d curveMidPoint = curve.GetPointAtParameter(midParam);
                                Point3d closestPoint = al.GetClosestPointTo(curveMidPoint, false);
                                if (closestPoint != null)
                                    alDistTuples.Add((curve, curveMidPoint.DistanceHorizontalTo(closestPoint), al));
                            }
                        }
                        catch (System.Exception)
                        {
                            prdDbg("Error in Curves GetClosestPointTo -> loop incomplete!");
                        }

                        double distThreshold = 1;
                        var result = alDistTuples.Where(x => x.dist < distThreshold);

                        if (result.Count() == 0)
                        {
                            psm.WritePropertyString(belongsToAlignmentProperty, "NA");
                            //Yellow line means check result
                            //This is caught if no result found at ALL
                            Line line = new Line(new Point3d(), curve.GetPointAtParameter(curve.EndParam / 2));
                            line.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);
                            line.AddEntityToDbModelSpace(localDb);
                        }
                        else if (result.Count() == 1)
                        {
                            psm.WritePropertyString(belongsToAlignmentProperty, result.First().al.Name);
                        }
                        else if (result.Count() > 1)
                        {
                            //If multiple result
                            //Means midpoint is close to two alignments
                            //Sample more points to determine

                            double oneFourthParam = curve.EndParam / 4;
                            Point3d oneFourthPoint = curve.GetPointAtParameter(oneFourthParam);
                            double threeFourthParam = curve.EndParam / 4 * 3;
                            Point3d threeFourthPoint = curve.GetPointAtParameter(threeFourthParam);

                            var resArray = result.ToArray();

                            distThreshold = 0.1;
                            double distIncrement = 0.1;

                            bool alDetected = false;
                            Alignment detectedAl = null;
                            while (!alDetected)
                            {
                                for (int i = 0; i < resArray.Count(); i++)
                                {
                                    if (alDetected) break;
                                    detectedAl = resArray[i].al;

                                    Point3d oneFourthClosestPoint = detectedAl.GetClosestPointTo(oneFourthPoint, false);
                                    Point3d threeFourthClosestPoint = detectedAl.GetClosestPointTo(threeFourthPoint, false);

                                    //DBPoint p1 = new DBPoint(oneFourthClosestPoint);
                                    //if (i == 0) p1.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                    //else p1.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);
                                    //p1.AddEntityToDbModelSpace(localDb);
                                    //DBPoint p2 = new DBPoint(threeFourthClosestPoint);
                                    //if (i == 0) p2.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                    //else p2.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);
                                    //p2.AddEntityToDbModelSpace(localDb);

                                    if (oneFourthPoint.DistanceHorizontalTo(oneFourthClosestPoint) < distThreshold &&
                                        threeFourthPoint.DistanceHorizontalTo(threeFourthClosestPoint) < distThreshold)
                                        alDetected = true;
                                }

                                distThreshold += distIncrement;
                            }

                            psm.WritePropertyString(belongsToAlignmentProperty, detectedAl?.Name ?? "NA");

                            //Red line means check result
                            //This is caught if multiple results
                            Line line = new Line(new Point3d(), curve.GetPointAtParameter(curve.EndParam / 2));
                            line.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                            line.AddEntityToDbModelSpace(localDb);
                        }
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    alTx.Abort();
                    alTx.Dispose();
                    alDb.Dispose();
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                alTx.Abort();
                alTx.Dispose();
                alDb.Dispose();
                tx.Commit();
            }
        }

        [CommandMethod("CHECKXRECORDS")]
        public void checkxrecords()
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
                    System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(
                        @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");

                    HashSet<Curve> curves = localDb.HashSetOfType<Curve>(tx);
                    HashSet<BlockReference> brs = localDb.HashSetOfType<BlockReference>(tx);

                    //******************************//
                    string xRecordName = "Alignment";
                    //******************************//

                    foreach (BlockReference br in brs)
                    {
                        if (ReadStringParameterFromDataTable(br.RealName(), fjvKomponenter, "Navn", 0) != null)
                        {
                            Oid extId = br.ExtensionDictionary;
                            if (extId == Oid.Null)
                            {
                                ErrorLine(br.Position);
                                continue;
                            }
                            DBDictionary dbExt = extId.Go<DBDictionary>(tx);
                            Oid xrecId = Oid.Null;
                            try { xrecId = dbExt.GetAt(xRecordName); }
                            catch (Autodesk.AutoCAD.Runtime.Exception) { ErrorLine(br.Position); }
                        }
                    }

                    Line ErrorLine(Point3d position)
                    {
                        Line line = new Line(new Point3d(), position);
                        line.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                        line.AddEntityToDbModelSpace(localDb);
                        return line;
                    }

                    foreach (Curve curve in curves)
                    {
                        if (!(curve.Layer.Contains("FREM") ||
                            curve.Layer.Contains("RETUR") ||
                            curve.Layer.Contains("TWIN"))) continue;

                        Oid extId = curve.ExtensionDictionary;
                        if (extId == Oid.Null)
                        {
                            ErrorLine(curve.GetPointAtParameter(curve.EndParam / 2));
                            continue;
                        }
                        DBDictionary dbExt = extId.Go<DBDictionary>(tx);
                        Oid xrecId = Oid.Null;
                        try { xrecId = dbExt.GetAt(xRecordName); }
                        catch (Autodesk.AutoCAD.Runtime.Exception) { ErrorLine(curve.GetPointAtParameter(curve.EndParam / 2)); }
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("XRECLIST")]
        public void xreclist()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                        "\nSelect entity to list XRECs:");
            promptEntityOptions1.SetRejectMessage("\n Not an entity!");
            PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
            if (((PromptResult)entity1).Status != PromptStatus.OK) { return; }
            Oid sourceId = entity1.ObjectId;
            if (!sourceId.IsDerivedFrom<Autodesk.AutoCAD.DatabaseServices.DBObject>())
            {
                prdDbg("Selected object is not derived from <DBObject>!");
                return;
            }

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    Entity ent = sourceId.Go<Entity>(tx);
                    prdDbg($"Handle: {ent.Handle}");
                    Oid extId = ent.ExtensionDictionary;
                    if (extId == Oid.Null) throw new System.Exception("No extension dictionary found!");
                    DBDictionary dbExt = extId.Go<DBDictionary>(tx);
                    string[] keys = new string[dbExt.Count];
                    ((IDictionary)dbExt).Keys.CopyTo(keys, 0);
                    for (int i = 0; i < keys.Length; i++)
                    {
                        prdDbg($"Key: {keys[i]}");
                        Oid valId = dbExt.GetAt(keys[i]);
                        if (!valId.IsDerivedFrom<Xrecord>()) continue;
                        Xrecord xrec = valId.Go<Xrecord>(tx);
                        TypedValue[] data = xrec.Data.AsArray();
                        for (int j = 0; j < data.Length; j++)
                        {
                            prdDbg($"Value {j + 1}: {data[j].Value.ToString()}");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("XRECWRITE")]
        public void xrecwrite()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                        "\nSelect entity to write XREC:");
            promptEntityOptions1.SetRejectMessage("\n Not an entity!");
            PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
            if (((PromptResult)entity1).Status != PromptStatus.OK) { return; }
            Oid sourceId = entity1.ObjectId;
            if (!sourceId.IsDerivedFrom<Autodesk.AutoCAD.DatabaseServices.DBObject>())
            {
                prdDbg("Selected object is not derived from <DBObject>!");
                return;
            }

            PromptResult sRes = editor.GetString("Enter name of the XREC: ");
            string xRecName = sRes.StringResult;

            PromptStringOptions pso = new PromptStringOptions("Enter string value to write: ");
            pso.AllowSpaces = true;
            PromptResult sRes2 = editor.GetString(pso);
            string xRecValue = sRes2.StringResult;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    XrecordCreateWriteUpdateString(sourceId.Go<DBObject>(tx, OpenMode.ForWrite),
                        xRecName, new[] { xRecValue });
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("addattributetoallblocks")]
        public void addattributetoallblocks()
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
                    System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(@"X:\AutoCAD DRI - 01 Civil 3D\FJV Komponenter.csv", "FjvKomponenter");

                    PromptResult pr = editor.GetString("\nEnter name of attribute to create: ");
                    if (pr.Status != PromptStatus.OK) return;
                    string attName = pr.StringResult;

                    PromptResult pr1 = editor.GetString("\nEnter value to assign: ");
                    if (pr1.Status != PromptStatus.OK) return;
                    string valueToAssign = pr1.StringResult;

                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForWrite) as BlockTable;
                    BlockTableRecord btrMs = tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead)
                        as BlockTableRecord;

                    foreach (Oid oid in btrMs)
                    {
                        if (oid.ObjectClass.Name == "AcDbBlockReference")
                        {
                            BlockReference br = oid.Go<BlockReference>(tx, OpenMode.ForRead);
                            if (ReadStringParameterFromDataTable(br.Name, fjvKomponenter, "Navn", 0) != null)
                            {
                                AttributeCollection aCol = br.AttributeCollection;
                                if (aCol.Count < 1)
                                {
                                    AddAttribute(localDb, tx, attName, valueToAssign, bt, br);
                                }
                                else
                                {
                                    bool attExists = false;

                                    foreach (Oid attOid in aCol)
                                    {
                                        AttributeReference att = attOid.Go<AttributeReference>(tx, OpenMode.ForWrite);
                                        if (att.Tag == attName) attExists = true;
                                    }

                                    if (!attExists)
                                    {
                                        AddAttribute(localDb, tx, attName, valueToAssign, bt, br);
                                    }
                                }
                            }
                        }
                    }

                    void AddAttribute(Database db, Transaction tr, string nameOfAtt, string assignToValue, BlockTable btLocal, BlockReference brLocal)
                    {
                        BlockTableRecord btr = tr.GetObject(btLocal[brLocal.Name], OpenMode.ForWrite) as BlockTableRecord;
                        ObjectIdCollection brefIds = btr.GetBlockReferenceIds(false, true);
                        AttributeDefinition attDef = new AttributeDefinition();
                        attDef.SetDatabaseDefaults(db);
                        attDef.Tag = nameOfAtt;
                        attDef.TextString = assignToValue;
                        attDef.Invisible = true;
                        attDef.Justify = AttachmentPoint.MiddleCenter;
                        attDef.Height = 0.1;
                        btr.AppendEntity(attDef);
                        tr.AddNewlyCreatedDBObject(attDef, true);

                        foreach (Oid brOid in brefIds)
                        {
                            AttributeReference attRef = new AttributeReference();
                            attRef.SetDatabaseDefaults(db);
                            attRef.Tag = nameOfAtt;
                            attRef.TextString = assignToValue;
                            attRef.Invisible = true;
                            attRef.Justify = AttachmentPoint.MiddleCenter;
                            attRef.Height = 0.1;

                            BlockReference bref = tr.GetObject(brOid, OpenMode.ForWrite) as BlockReference;
                            bref.AttributeCollection.AppendAttribute(attRef);
                            //editor.Command("_.attsync", "_name", blockName);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("attsyncall")]
        public void attsyncall()
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
                    System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(@"X:\AutoCAD DRI - 01 Civil 3D\FJV Komponenter.csv", "FjvKomponenter");

                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForWrite) as BlockTable;
                    BlockTableRecord btrMs = tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead)
                        as BlockTableRecord;

                    foreach (Oid oid in btrMs)
                    {
                        if (oid.ObjectClass.Name == "AcDbBlockReference")
                        {
                            BlockReference br = oid.Go<BlockReference>(tx, OpenMode.ForRead);
                            if (ReadStringParameterFromDataTable(br.Name, fjvKomponenter, "Navn", 0) != null)
                            {
                                editor.Command("_.attsync", "n ", br.Name);
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("listblockswithnoattributes")]
        public void listblockswithnoattributes()
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
                    System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(@"X:\AutoCAD DRI - 01 Civil 3D\FJV Komponenter.csv", "FjvKomponenter");

                    HashSet<string> list = new HashSet<string>();
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForWrite) as BlockTable;
                    BlockTableRecord btrMs = tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead)
                        as BlockTableRecord;

                    foreach (Oid oid in btrMs)
                    {
                        if (oid.ObjectClass.Name == "AcDbBlockReference")
                        {
                            BlockReference blkRef = oid.Go<BlockReference>(tx, OpenMode.ForRead);
                            AttributeCollection aCol = blkRef.AttributeCollection;
                            if (aCol.Count < 1)
                            {
                                if (ReadStringParameterFromDataTable(blkRef.Name, fjvKomponenter, "Navn", 0) != null &&
                                !blkRef.Name.StartsWith("*"))
                                {
                                    list.Add(blkRef.Name);
                                }
                            }
                        }
                    }

                    foreach (string name in list.OrderBy(x => x))
                    {
                        editor.WriteMessage($"\n{name}");
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

        [CommandMethod("ok", CommandFlags.UsePickSet)]
        public void componentsok()
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
                    // Get the PickFirst selection set
                    PromptSelectionResult acSSPrompt;
                    acSSPrompt = editor.SelectImplied();
                    SelectionSet acSSet;
                    // If the prompt status is OK, objects were selected before
                    // the command was started
                    if (acSSPrompt.Status == PromptStatus.OK)
                    {
                        acSSet = acSSPrompt.Value;
                        var Ids = acSSet.GetObjectIds();
                        foreach (Oid oid in Ids)
                        {
                            Entity ent = oid.Go<Entity>(tx, OpenMode.ForWrite);
                            ent.Layer = "0-Komponent";
                        }
                    }
                    else
                    {

                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("staggerlabels")]
        [CommandMethod("sg")]
        public void staggerlabels()
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
                    #region Get the selection set of all objects and profile view
                    PromptSelectionOptions pOptions = new PromptSelectionOptions();
                    PromptSelectionResult sSetResult = editor.GetSelection(pOptions);
                    if (sSetResult.Status != PromptStatus.OK) return;
                    HashSet<Entity> allEnts = sSetResult.Value.GetObjectIds().Select(e => e.Go<Entity>(tx)).ToHashSet();
                    #endregion

                    #region Setup styles
                    LabelStyleCollection stc = civilDoc.Styles.LabelStyles
                                                                   .ProjectionLabelStyles.ProfileViewProjectionLabelStyles;

                    Oid profileProjection_RIGHT_Style = Oid.Null;
                    Oid profileProjection_LEFT_Style = Oid.Null;

                    try
                    {
                        profileProjection_RIGHT_Style = stc["PROFILE PROJECTION RIGHT"];
                    }
                    catch (System.Exception)
                    {
                        editor.WriteMessage($"\nPROFILE PROJECTION RIGHT style missing!");
                        tx.Abort();
                        return;
                    }

                    try
                    {
                        profileProjection_LEFT_Style = stc["PROFILE PROJECTION LEFT"];
                    }
                    catch (System.Exception)
                    {
                        editor.WriteMessage($"\nPROFILE PROJECTION LEFT style missing!");
                        tx.Abort();
                        return;
                    }
                    #endregion

                    #region Choose left or right orientation

                    string AskToChooseDirection(Editor locEd)
                    {
                        const string kwd1 = "Right";
                        const string kwd2 = "Left";
                        PromptKeywordOptions pKeyOpts2 = new PromptKeywordOptions("");
                        pKeyOpts2.Message = "\nChoose next label direction: ";
                        pKeyOpts2.Keywords.Add(kwd1);
                        pKeyOpts2.Keywords.Add(kwd2);
                        pKeyOpts2.AllowNone = true;
                        pKeyOpts2.Keywords.Default = kwd1;
                        PromptResult locpKeyRes2 = locEd.GetKeywords(pKeyOpts2);
                        return locpKeyRes2.StringResult;
                    }
                    #endregion

                    bool dirRight = AskToChooseDirection(editor) == "Right";

                    #region Labels
                    HashSet<ProfileProjectionLabel> unSortedLabels = new HashSet<ProfileProjectionLabel>();

                    foreach (Entity ent in allEnts)
                        if (ent is ProfileProjectionLabel label) unSortedLabels.Add(label);

                    ProfileProjectionLabel[] labels;

                    if (dirRight)
                    {
                        labels = unSortedLabels.OrderByDescending(x => x.LabelLocation.X).ToArray();
                    }
                    else
                    {
                        labels = unSortedLabels.OrderBy(x => x.LabelLocation.X).ToArray();
                    }

                    for (int i = 0; i < labels.Length - 1; i++)
                    {
                        ProfileProjectionLabel firstLabel = labels[i];
                        ProfileProjectionLabel secondLabel = labels[i + 1];

                        Point3d firstLocationPoint = firstLabel.LabelLocation;
                        Point3d secondLocationPoint = secondLabel.LabelLocation;

                        double firstAnchorDimensionInMeters = firstLabel.DimensionAnchorValue * 250 + 0.0625;

                        double locationDelta = firstLocationPoint.Y - secondLocationPoint.Y;

                        double secondAnchorDimensionInMeters = (locationDelta + firstAnchorDimensionInMeters + 0.75) / 250;

                        Oid styleId = dirRight ? profileProjection_RIGHT_Style : profileProjection_LEFT_Style;

                        //Handle first label
                        if (i == 0)
                        {
                            firstLabel.CheckOrOpenForWrite();
                            firstLabel.StyleId = styleId;
                        }

                        secondLabel.CheckOrOpenForWrite();
                        secondLabel.DimensionAnchorValue = secondAnchorDimensionInMeters;
                        secondLabel.StyleId = styleId;
                        secondLabel.DowngradeOpen();

                        //editor.WriteMessage($"\nAnchorDimensionValue: {firstLabel.DimensionAnchorValue}.");
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("staggerlabelsall")]
        [CommandMethod("sgall")]
        public void staggerlabelsall()
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
                    HashSet<ProfileView> pvs = localDb.HashSetOfType<ProfileView>(tx);
                    HashSet<ProfileProjectionLabel> labelsSet = localDb.HashSetOfType<ProfileProjectionLabel>(tx);

                    #region Setup styles
                    LabelStyleCollection stc = civilDoc.Styles.LabelStyles
                                                                   .ProjectionLabelStyles.ProfileViewProjectionLabelStyles;

                    Oid profileProjection_RIGHT_Style = Oid.Null;
                    Oid profileProjection_LEFT_Style = Oid.Null;

                    try
                    {
                        profileProjection_RIGHT_Style = stc["PROFILE PROJECTION RIGHT"];
                    }
                    catch (System.Exception)
                    {
                        editor.WriteMessage($"\nPROFILE PROJECTION RIGHT style missing!");
                        tx.Abort();
                        return;
                    }

                    try
                    {
                        profileProjection_LEFT_Style = stc["PROFILE PROJECTION LEFT"];
                    }
                    catch (System.Exception)
                    {
                        editor.WriteMessage($"\nPROFILE PROJECTION LEFT style missing!");
                        tx.Abort();
                        return;
                    }
                    #endregion

                    #region Labels
                    Extents3d extents = default;
                    var labelsInView = labelsSet.Where(x => extents.IsPointInsideXY(x.LabelLocation));

                    Oid styleId = profileProjection_RIGHT_Style;

                    foreach (var pv in pvs)
                    {
                        ProfileProjectionLabel[] labels;
                        extents = pv.GeometricExtents;
                        labels = labelsInView.OrderByDescending(x => x.LabelLocation.X).ToArray();

                        for (int i = 0; i < labels.Length - 1; i++)
                        {
                            ProfileProjectionLabel firstLabel = labels[i];
                            ProfileProjectionLabel secondLabel = labels[i + 1];

                            //Handle first label
                            if (i == 0)
                            {
                                double length = 32;
                                firstLabel.CheckOrOpenForWrite();
                                firstLabel.DimensionAnchorValue = length / 250 / 4;
                                firstLabel.StyleId = styleId;
                            }

                            Point3d firstLocationPoint = firstLabel.LabelLocation;
                            Point3d secondLocationPoint = secondLabel.LabelLocation;

                            double firstAnchorDimensionInMeters = firstLabel.DimensionAnchorValue * 250 + 0.0625;

                            double locationDelta = firstLocationPoint.Y - secondLocationPoint.Y;

                            double secondAnchorDimensionInMeters = (locationDelta + firstAnchorDimensionInMeters + 0.75) / 250;

                            secondLabel.CheckOrOpenForWrite();
                            secondLabel.DimensionAnchorValue = secondAnchorDimensionInMeters;
                            secondLabel.StyleId = styleId;
                            secondLabel.DowngradeOpen();

                            //editor.WriteMessage($"\nAnchorDimensionValue: {firstLabel.DimensionAnchorValue}.");
                        }

                    }
                    #endregion 
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("setlabelslength")]
        public void setlabelslength()
        {

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                HashSet<ProfileProjectionLabel> labels = localDb.HashSetOfType<ProfileProjectionLabel>(tx);
                editor.WriteMessage($"\nNumber of labels: {labels.Count}.");

                #region Get length
                PromptDoubleResult result = editor.GetDouble("\nEnter length: ");
                if (((PromptResult)result).Status != PromptStatus.OK) return;
                double length = result.Value;
                #endregion

                foreach (ProfileProjectionLabel label in labels)
                {
                    label.CheckOrOpenForWrite();

                    label.DimensionAnchorValue = length / 250 / 4;
                }

                tx.Commit();
            }
        }

        [CommandMethod("importlabelstyles")]
        public void importlabelstyles()
        {
            try
            {
                DocumentCollection docCol = Application.DocumentManager;
                Database localDb = docCol.MdiActiveDocument.Database;
                Editor editor = docCol.MdiActiveDocument.Editor;
                Document doc = docCol.MdiActiveDocument;
                CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

                #region Set C-ANNO-MTCH-HATCH to frozen
                //using (Transaction tx = localDb.TransactionManager.StartTransaction())
                //{
                //    try
                //    {
                //        // Open the Layer table for read
                //        LayerTable acLyrTbl;
                //        acLyrTbl = tx.GetObject(localDb.LayerTableId,
                //                                           OpenMode.ForRead) as LayerTable;
                //        string sLayerName = "C-ANNO-MTCH-HATCH";
                //        LayerTableRecord acLyrTblRec;
                //        if (acLyrTbl.Has(sLayerName))
                //        {
                //            acLyrTblRec = tx.GetObject(acLyrTbl[sLayerName], OpenMode.ForWrite) as LayerTableRecord;
                //            // Freeze the layer
                //            acLyrTblRec.IsFrozen = true;
                //        }
                //    }
                //    catch (System.Exception ex)
                //    {
                //        editor.WriteMessage("\n" + ex.ToString());
                //        tx.Abort();
                //        return;
                //    }
                //    tx.Commit();
                //}
                #endregion

                #region Setup styles and clone blocks
                string pathToStyles = @"X:\AutoCAD DRI - 01 Civil 3D\Projection_styles.dwg";

                using (Database stylesDB = new Database(false, true))
                {
                    stylesDB.ReadDwgFile(pathToStyles, FileOpenMode.OpenForReadAndWriteNoShare, false, "");

                    using (Transaction localTx = localDb.TransactionManager.StartTransaction())
                    using (Transaction stylesTx = stylesDB.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            CivilDocument stylesDoc = CivilDocument.GetCivilDocument(stylesDB);

                            ObjectIdCollection objIds = new ObjectIdCollection();

                            //Projection Label Styles
                            LabelStyleCollection stc = stylesDoc.Styles.LabelStyles
                                .ProjectionLabelStyles.ProfileViewProjectionLabelStyles;

                            objIds.Add(stc["PROFILE PROJECTION RIGHT"]);
                            objIds.Add(stc["PROFILE PROJECTION LEFT"]);

                            //Profile View Style
                            ProfileViewStyleCollection pvsc = stylesDoc.Styles.ProfileViewStyles;
                            objIds.Add(pvsc["PROFILE VIEW L TO R 1:250:100"]);
                            objIds.Add(pvsc["PROFILE VIEW L TO R NO SCALE"]);

                            //Alignment styles
                            var ass = stylesDoc.Styles.AlignmentStyles;
                            objIds.Add(ass["FJV TRACÉ SHOW"]);
                            objIds.Add(ass["FJV TRACE NO SHOW"]);

                            //Alignment label styles
                            var als = stylesDoc.Styles.LabelStyles.AlignmentLabelStyles;
                            objIds.Add(als.MajorStationLabelStyles["Perpendicular with Line"]);
                            objIds.Add(als.MinorStationLabelStyles["Tick"]);
                            objIds.Add(stylesDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles["STD 20-5"]);

                            //Profile Style
                            var psc = stylesDoc.Styles.ProfileStyles;
                            objIds.Add(psc["PROFIL STYLE MGO KANT"]);
                            objIds.Add(psc["PROFIL STYLE MGO MIDT"]);
                            objIds.Add(psc["Terræn"]);

                            //Profile label styles
                            var plss = stylesDoc.Styles.LabelStyles.ProfileLabelStyles.CurveLabelStyles;
                            objIds.Add(plss["Radius Crest"]);
                            objIds.Add(plss["Radius Sag"]);

                            //Band set style
                            objIds.Add(stylesDoc.Styles.BandStyles.ProfileViewProfileDataBandStyles["Elevations and Stations"]);
                            objIds.Add(stylesDoc.Styles.BandStyles.ProfileViewProfileDataBandStyles["TitleBuffer"]);
                            objIds.Add(stylesDoc.Styles.ProfileViewBandSetStyles["EG-FG Elevations and Stations"]);

                            //Matchline styles
                            objIds.Add(stylesDoc.Styles.MatchLineStyles["Basic"]);

                            //Point styles
                            objIds.Add(stylesDoc.Styles.PointStyles["PIL"]);

                            //Point label styles
                            objIds.Add(stylesDoc.Styles.LabelStyles.PointLabelStyles.LabelStyles["_No labels"]);

                            //Default projection label style
                            objIds.Add(stylesDoc.Styles.LabelStyles.ProjectionLabelStyles
                                .ProfileViewProjectionLabelStyles["PROFILE PROJEKTION MGO"]);

                            int i = 0;
                            foreach (Oid oid in objIds)
                            {
                                prdDbg($"{i}: {oid.ToString()}");
                                i++;

                            }

                            prdDbg("Stylebase.ExportTo() doesn't work!");
                            //Autodesk.Civil.DatabaseServices.Styles.StyleBase.ExportTo(objIds, localDb, Autodesk.Civil.StyleConflictResolverType.Override);
                        }
                        catch (System.Exception)
                        {
                            stylesTx.Abort();
                            stylesDB.Dispose();
                            localTx.Abort();
                            throw;
                        }

                        try
                        {
                            Oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(stylesDB);
                            Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);

                            ObjectIdCollection idsToClone = new ObjectIdCollection();

                            BlockTable sourceBt = stylesTx.GetObject(stylesDB.BlockTableId, OpenMode.ForRead) as BlockTable;
                            idsToClone.Add(sourceBt["EL 10kV"]);
                            idsToClone.Add(sourceBt["EL 0.4kV"]);
                            idsToClone.Add(sourceBt["VerticeArc"]);
                            idsToClone.Add(sourceBt["VerticeLine"]);

                            IdMapping mapping = new IdMapping();
                            stylesDB.WblockCloneObjects(idsToClone, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);
                        }
                        catch (System.Exception e)
                        {
                            prdDbg(e.Message);
                            stylesTx.Abort();
                            localTx.Abort();
                            throw;
                        }
                        stylesTx.Commit();
                        localTx.Commit();
                    }
                }
                #endregion
            }
            catch (System.Exception ex)
            {
                Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
                ed.WriteMessage($"\n{ex.Message}");
                return;
            }
        }

        [CommandMethod("FIXMIDTPROFILESTYLE")]
        public void fixmidtprofilestyle()
        {
            try
            {
                DocumentCollection docCol = Application.DocumentManager;
                Database localDb = docCol.MdiActiveDocument.Database;
                Editor editor = docCol.MdiActiveDocument.Editor;
                Document doc = docCol.MdiActiveDocument;
                CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

                #region Setup styles and clone blocks

                using (Transaction tx = localDb.TransactionManager.StartTransaction())
                {
                    try
                    {
                        //Profile Style
                        var psc = civilDoc.Styles.ProfileStyles;
                        ProfileStyle ps = psc["PROFIL STYLE MGO MIDT"].Go<ProfileStyle>(tx);
                        ps.CheckOrOpenForWrite();

                        DisplayStyle ds;
                        ds = ps.GetDisplayStyleProfile(ProfileDisplayStyleProfileType.Line);
                        ds.LinetypeScale = 10;

                        ds = ps.GetDisplayStyleProfile(ProfileDisplayStyleProfileType.Curve);
                        ds.LinetypeScale = 10;

                        ds = ps.GetDisplayStyleProfile(ProfileDisplayStyleProfileType.SymmetricalParabola);
                        ds.LinetypeScale = 10;

                    }
                    catch (System.Exception)
                    {
                        tx.Abort();
                        throw;
                    }
                    tx.Commit();
                }

                #endregion
            }
            catch (System.Exception ex)
            {
                prdDbg(ex.ToString());
            }
        }

        [CommandMethod("finalizesheets")]
        public void finalizesheets()
        {

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor ed = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            //Create crossing points first
            createlerdatapss();
            //Populateprofileviews with crossing data
            populateprofiles();

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForWrite) as BlockTable;

                    #region Stylize Profile Views
                    HashSet<ProfileView> pvs = localDb.HashSetOfType<ProfileView>(tx);

                    Oid pvStyleId = Oid.Null;
                    try
                    {
                        pvStyleId = civilDoc.Styles.ProfileViewStyles["PROFILE VIEW L TO R 1:250:100"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nProfile view style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    HashSet<Alignment> alss = localDb.HashSetOfType<Alignment>(tx);
                    foreach (Alignment al in alss)
                    {
                        al.CheckOrOpenForWrite();
                        al.StyleId = civilDoc.Styles.AlignmentStyles["FJV TRACÉ SHOW"];
                        al.ImportLabelSet("STD 20-5");
                        al.DowngradeOpen();
                    }

                    foreach (ProfileView pv in pvs)
                    {
                        pv.CheckOrOpenForWrite();
                        pv.StyleId = pvStyleId;

                        Oid alId = pv.AlignmentId;
                        Alignment al = alId.Go<Alignment>(tx);

                        ObjectIdCollection psIds = al.GetProfileIds();
                        HashSet<Profile> ps = new HashSet<Profile>();
                        foreach (Oid oid in psIds) ps.Add(oid.Go<Profile>(tx));

                        Profile surfaceProfile = ps.Where(x => x.Name.Contains("surface")).FirstOrDefault();
                        Oid surfaceProfileId = Oid.Null;
                        if (surfaceProfile != null) surfaceProfileId = surfaceProfile.ObjectId;
                        else ed.WriteMessage("\nSurface profile not found!");

                        Profile topProfile = ps.Where(x => x.Name.Contains("TOP")).FirstOrDefault();
                        Oid topProfileId = Oid.Null;
                        if (topProfile != null) topProfileId = topProfile.ObjectId;
                        else ed.WriteMessage("\nTop profile not found!");

                        //this doesn't quite work
                        Oid pvbsId = civilDoc.Styles.ProfileViewBandSetStyles["EG-FG Elevations and Stations"];
                        ProfileViewBandSet pvbs = pv.Bands;
                        pvbs.ImportBandSetStyle(pvbsId);

                        //try this
                        Oid pvBSId1 = civilDoc.Styles.BandStyles.ProfileViewProfileDataBandStyles["Elevations and Stations"];
                        Oid pvBSId2 = civilDoc.Styles.BandStyles.ProfileViewProfileDataBandStyles["TitleBuffer"];
                        ProfileViewBandItemCollection pvic = new ProfileViewBandItemCollection(pv.Id, BandLocationType.Bottom);
                        pvic.Add(pvBSId1);
                        pvic.Add(pvBSId2);
                        pvbs.SetBottomBandItems(pvic);

                        ProfileViewBandItemCollection pbic = pvbs.GetBottomBandItems();
                        for (int i = 0; i < pbic.Count; i++)
                        {
                            ProfileViewBandItem pvbi = pbic[i];
                            if (i == 0) pvbi.Gap = 0;
                            else if (i == 1) pvbi.Gap = 0.016;
                            if (surfaceProfileId != Oid.Null) pvbi.Profile1Id = surfaceProfileId;
                            if (topProfileId != Oid.Null) pvbi.Profile2Id = topProfileId;
                            pvbi.LabelAtStartStation = true;
                            pvbi.LabelAtEndStation = true;
                        }
                        pvbs.SetBottomBandItems(pbic);

                        #region Scale LER block
                        if (bt.Has(pv.Name))
                        {
                            BlockTableRecord btr = tx.GetObject(bt[pv.Name], OpenMode.ForRead)
                                as BlockTableRecord;
                            ObjectIdCollection brefIds = btr.GetBlockReferenceIds(false, true);

                            foreach (Oid oid in brefIds)
                            {
                                BlockReference bref = oid.Go<BlockReference>(tx, OpenMode.ForWrite);
                                bref.ScaleFactors = new Scale3d(1, 2.5, 1);
                            }

                        }
                        #endregion
                    }
                    #endregion

                    #region ProfileStyles
                    Oid pPipeStyleKantId = Oid.Null;
                    try
                    {
                        pPipeStyleKantId = civilDoc.Styles.ProfileStyles["PROFIL STYLE MGO KANT"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nPROFIL STYLE MGO KANT style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    Oid pPipeStyleMidtId = Oid.Null;
                    try
                    {
                        pPipeStyleMidtId = civilDoc.Styles.ProfileStyles["PROFIL STYLE MGO MIDT"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nPROFIL STYLE MGO MIDT style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    Oid pTerStyleId = Oid.Null;
                    try
                    {
                        pTerStyleId = civilDoc.Styles.ProfileStyles["Terræn"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nTerræn style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    Oid alStyleId = Oid.Null;
                    try
                    {
                        alStyleId = civilDoc.Styles.AlignmentStyles["FJV TRACÉ SHOW"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nFJV TRACÈ SHOW style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    Oid alLabelSetStyleId = Oid.Null;
                    try
                    {
                        alLabelSetStyleId = civilDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles["STD 20-5"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nSTD 20-5 style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    Oid crestCurveLabelId = Oid.Null;
                    try
                    {
                        crestCurveLabelId = civilDoc.Styles.LabelStyles.ProfileLabelStyles.CurveLabelStyles["Radius Crest"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nRADIUS CREST style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    Oid sagCurveLabelId = Oid.Null;
                    try
                    {
                        sagCurveLabelId = civilDoc.Styles.LabelStyles.ProfileLabelStyles.CurveLabelStyles["Radius Sag"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nRADIUS SAG style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);
                    foreach (Alignment al in als)
                    {
                        al.CheckOrOpenForWrite();
                        al.StyleId = alStyleId;
                        al.ImportLabelSet(alLabelSetStyleId);

                        ObjectIdCollection pIds = al.GetProfileIds();
                        foreach (Oid oid in pIds)
                        {
                            Profile p = oid.Go<Profile>(tx);
                            if (p.Name == $"{al.Name}_surface_P")
                            {
                                p.CheckOrOpenForWrite();
                                p.StyleId = pTerStyleId;
                            }
                            else
                            {
                                p.CheckOrOpenForWrite();
                                p.StyleId = pPipeStyleKantId;

                                if (p.Name.Contains("MIDT"))
                                {
                                    p.StyleId = pPipeStyleMidtId;

                                    foreach (ProfileView pv in pvs)
                                    {
                                        pv.CheckOrOpenForWrite();
                                        ProfileCrestCurveLabelGroup.Create(pv.ObjectId, p.ObjectId, crestCurveLabelId);
                                        ProfileSagCurveLabelGroup.Create(pv.ObjectId, p.ObjectId, sagCurveLabelId);
                                    }
                                }
                            }
                        }
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    ed.WriteMessage("\n" + ex.ToString());
                    return;
                }
                tx.Commit();
            }

            //Create detailing blocks on top of exaggerated views
            createdetailingmethod();
            //Auto stagger all labels to right
            staggerlabelsall();
            //Draw rectangles representing viewports around longitudinal profiles
            //Can be used to check if labels are inside
            drawviewportrectangles();
            //Colorize layer as per krydsninger tabler
            colorizealllerlayers();
        }

        /// <summary>
        /// Creates detailing based on SURFACE profile.
        /// </summary>
        [CommandMethod("CREATEDETAILINGPRELIMINARY")]
        public void createdetailingpreliminary()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                #region Open fremtidig db
                DataReferencesOptions dro = new DataReferencesOptions();
                string projectName = dro.ProjectName;
                string etapeName = dro.EtapeName;

                // open the xref database
                Database fremDb = new Database(false, true);
                fremDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Fremtid"),
                    System.IO.FileShare.Read, false, string.Empty);
                Transaction fremTx = fremDb.TransactionManager.StartTransaction();
                HashSet<Curve> allCurves = fremDb.HashSetOfType<Curve>(fremTx);
                HashSet<BlockReference> allBrs = fremDb.HashSetOfType<BlockReference>(fremTx);
                #endregion

                //////////////////////////////////////
                string komponentBlockName = "DRISizeChangeAnno";
                string bueBlockName = "DRIPipeArcAnno";
                //////////////////////////////////////

                try
                {
                    #region Common variables
                    BlockTableRecord modelSpace = localDb.GetModelspaceForWrite();
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    Plane plane = new Plane(); //For intersecting
                    HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);
                    #endregion

                    #region Import blocks if missing
                    if (!bt.Has(komponentBlockName) ||
                        !bt.Has(bueBlockName))
                    {
                        prdDbg("Some of the blocks for detailing are missing! Importing...");
                        Database blockDb = new Database(false, true);
                        blockDb.ReadDwgFile("X:\\AutoCAD DRI - 01 Civil 3D\\DynBlokke\\Symboler.dwg",
                            System.IO.FileShare.Read, false, string.Empty);
                        Transaction blockTx = blockDb.TransactionManager.StartTransaction();

                        Oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(blockDb);
                        //Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);
                        Oid destDbMsId = localDb.BlockTableId;

                        BlockTable sourceBt = blockTx.GetObject(blockDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                        ObjectIdCollection idsToClone = new ObjectIdCollection();
                        if (!bt.Has(komponentBlockName)) idsToClone.Add(sourceBt[komponentBlockName]);
                        if (!bt.Has(bueBlockName)) idsToClone.Add(sourceBt[bueBlockName]);

                        IdMapping mapping = new IdMapping();
                        blockDb.WblockCloneObjects(idsToClone, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);
                        blockTx.Commit();
                        blockTx.Dispose();
                        blockDb.Dispose();
                    }
                    #endregion

                    #region Delete previous blocks
                    //Delete previous blocks
                    var existingBlocks = localDb.GetBlockReferenceByName(komponentBlockName);
                    existingBlocks.UnionWith(localDb.GetBlockReferenceByName(bueBlockName));

                    foreach (BlockReference br in existingBlocks)
                    {
                        br.CheckOrOpenForWrite();
                        br.Erase(true);
                    }
                    #endregion

                    foreach (Alignment al in als)
                    {
                        prdDbg($"\nProcessing: {al.Name}...");
                        #region If exist get surface profile and profile view
                        ObjectIdCollection profileIds = al.GetProfileIds();
                        ObjectIdCollection profileViewIds = al.GetProfileViewIds();
                        ProfileViewCollection pvs = new ProfileViewCollection(profileViewIds);

                        #region Fetch surface profile
                        Profile surfaceProfile = null;
                        foreach (Oid oid in profileIds)
                        {
                            Profile pTemp = oid.Go<Profile>(tx);
                            if (pTemp.Name == $"{al.Name}_surface_P") surfaceProfile = pTemp;
                        }
                        if (surfaceProfile == null)
                        {
                            prdDbg($"No surface profile found for alignment: {al.Name}, skipping current alignment.");
                            continue;
                        }
                        #endregion
                        #endregion

                        #region GetCurvesAndBRs from fremtidig
                        HashSet<Curve> curves = allCurves
                            .Where(x => x.XrecFilter("Alignment", new[] { al.Name }))
                            .ToHashSet();
                        HashSet<BlockReference> brs = allBrs
                            .Where(x => x.XrecFilter("Alignment", new[] { al.Name }))
                            .ToHashSet();
                        prdDbg($"Curves: {curves.Count}, Components: {brs.Count}");
                        #endregion

                        prdDbg("Blocks:");
                        PipelineSizeArray sizeArray = new PipelineSizeArray(al, curves, brs);
                        prdDbg(sizeArray.ToString());

                        foreach (ProfileView pv in pvs)
                        {
                            prdDbg($"Processing PV {pv.Name}.");

                            #region Variables and settings
                            Point3d pvOrigin = pv.Location;
                            double originX = pvOrigin.X;
                            double originY = pvOrigin.Y;

                            double pvStStart = pv.StationStart;
                            double pvStEnd = pv.StationEnd;
                            double pvElBottom = pv.ElevationMin;
                            double pvElTop = pv.ElevationMax;
                            double pvLength = pvStEnd - pvStStart;
                            #endregion

                            #region Determine what sizes appear in current PV
                            var pvSizeArray = sizeArray.GetPartialSizeArrayForPV(pv);
                            prdDbg(pvSizeArray.ToString());
                            #endregion

                            #region Prepare exaggeration handling
                            ProfileViewStyle profileViewStyle = tx
                                .GetObject(((Autodesk.Aec.DatabaseServices.Entity)pv)
                                .StyleId, OpenMode.ForRead) as ProfileViewStyle;
                            #endregion

                            double curStationBL = 0;
                            double sampledMidtElevation = 0;
                            double curX = 0, curY = 0;

                            #region Place size change blocks
                            for (int i = 0; i < pvSizeArray.Length; i++)
                            {   //Although look ahead is used, normal iteration is required
                                //Or cases where sizearray is only 1 size will not run at all
                                //In more general case the last iteration must be aborted
                                if (pvSizeArray.Length != 1 && i != pvSizeArray.Length - 1)
                                {
                                    //General case
                                    curStationBL = pvSizeArray[i].EndStation;
                                    sampledMidtElevation = SampleProfile(surfaceProfile, curStationBL);
                                    curX = originX + pvSizeArray[i].EndStation - pvStStart;
                                    curY = originY + (sampledMidtElevation - pvElBottom) *
                                        profileViewStyle.GraphStyle.VerticalExaggeration;
                                    double deltaY = (sampledMidtElevation - pvElBottom) *
                                        profileViewStyle.GraphStyle.VerticalExaggeration;
                                    //prdDbg($"{originY} + ({sampledMidtElevation} - {pvElBottom}) * " +
                                    //    $"{profileViewStyle.GraphStyle.VerticalExaggeration} = {deltaY}");
                                    BlockReference brInt =
                                        localDb.CreateBlockWithAttributes(komponentBlockName, new Point3d(curX, curY, 0));
                                    brInt.SetAttributeStringValue("LEFTSIZE", $"DN {pvSizeArray[i].DN}");
                                    brInt.SetAttributeStringValue("RIGHTSIZE", $"DN {pvSizeArray[i + 1].DN}");
                                }
                                //Special cases
                                if (i == 0)
                                {//First iteration
                                    curStationBL = pvStStart;
                                    sampledMidtElevation = SampleProfile(surfaceProfile, curStationBL);
                                    curX = originX;
                                    curY = originY + (sampledMidtElevation - pvElBottom) *
                                        profileViewStyle.GraphStyle.VerticalExaggeration;
                                    BlockReference brAt0 =
                                        localDb.CreateBlockWithAttributes(komponentBlockName, new Point3d(curX, curY, 0));
                                    brAt0.SetAttributeStringValue("LEFTSIZE", "");
                                    brAt0.SetAttributeStringValue("RIGHTSIZE", $"DN {pvSizeArray[0].DN}");

                                    if (pvSizeArray.Length == 1)
                                    {//If only one size in the array also place block at end
                                        curStationBL = pvStEnd;
                                        sampledMidtElevation = SampleProfile(surfaceProfile, curStationBL - .1);
                                        curX = originX + curStationBL - pvStStart;
                                        curY = originY + (sampledMidtElevation - pvElBottom) *
                                            profileViewStyle.GraphStyle.VerticalExaggeration;
                                        BlockReference brAtEnd =
                                            localDb.CreateBlockWithAttributes(komponentBlockName, new Point3d(curX, curY, 0));
                                        brAtEnd.SetAttributeStringValue("LEFTSIZE", $"DN {pvSizeArray[0].DN}");
                                        brAtEnd.SetAttributeStringValue("RIGHTSIZE", "");
                                    }
                                }
                                if (i == pvSizeArray.Length - 2)
                                {//End of the iteration
                                    curStationBL = pvStEnd;
                                    sampledMidtElevation = SampleProfile(surfaceProfile, curStationBL - .1);
                                    curX = originX + curStationBL - pvStStart;
                                    curY = originY + (sampledMidtElevation - pvElBottom) *
                                        profileViewStyle.GraphStyle.VerticalExaggeration;
                                    BlockReference brAtEnd =
                                        localDb.CreateBlockWithAttributes(komponentBlockName, new Point3d(curX, curY, 0));
                                    brAtEnd.SetAttributeStringValue("LEFTSIZE", $"DN {pvSizeArray[i + 1].DN}");
                                    brAtEnd.SetAttributeStringValue("RIGHTSIZE", "");
                                }
                            }
                            #endregion

                            #region Local method to sample profiles
                            //Local method to sample profiles
                            double SampleProfile(Profile profile, double station)
                            {
                                double sampledElevation = 0;
                                try { sampledElevation = profile.ElevationAt(station); }
                                catch (System.Exception)
                                {
                                    prdDbg($"Station {station} threw an exception when placing size change blocks! Skipping...");
                                    return 0;
                                }
                                return sampledElevation;
                            }
                            #endregion

                            #region Place component blocks
                            System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(
                                @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");
                            foreach (BlockReference br in brs)
                            {
                                string type = ReadStringParameterFromDataTable(br.RealName(), fjvKomponenter, "Type", 0);
                                if (type == "Reduktion" || type == "Svejsning") continue;
                                Point3d brLocation = al.GetClosestPointTo(br.Position, false);

                                double station;
                                try
                                {
                                    station = al.GetDistAtPoint(brLocation);
                                }
                                catch (System.Exception)
                                {
                                    prdDbg(br.Position.ToString());
                                    prdDbg(brLocation.ToString());
                                    throw;
                                }

                                //Determine if blockref is within current PV
                                //If within -> place block, else go to next iteration
                                if (!(station > pvStStart && station < pvStEnd)) continue;

                                sampledMidtElevation = SampleProfile(surfaceProfile, station);
                                double X = originX + station - pvStStart;
                                double Y = originY + (sampledMidtElevation - pvElBottom) *
                                        profileViewStyle.GraphStyle.VerticalExaggeration;
                                BlockReference brSign = localDb.CreateBlockWithAttributes(komponentBlockName, new Point3d(X, Y, 0));
                                brSign.SetAttributeStringValue("LEFTSIZE", type);
                                if ((new[] { "Parallelafgrening", "Lige afgrening", "Afgrening med spring", "Påsvejsning" }).Contains(type))
                                    brSign.SetAttributeStringValue("RIGHTSIZE", br.XrecReadStringAtIndex("Alignment", 1));
                                else brSign.SetAttributeStringValue("RIGHTSIZE", "");
                            }
                            #endregion

                            #region Find curves and annotate
                            foreach (Curve curve in curves)
                            {
                                if (curve is Polyline pline)
                                {
                                    //Detect arcs and determine if it is a buerør or not
                                    for (int i = 0; i < pline.NumberOfVertices; i++)
                                    {
                                        TypeOfSegment tos;
                                        double bulge = pline.GetBulgeAt(i);
                                        if (bulge == 0) tos = TypeOfSegment.Straight;
                                        else
                                        {
                                            //Determine if centre of arc is within view
                                            CircularArc2d arcSegment2dAt = pline.GetArcSegment2dAt(i);
                                            Point2d samplePoint = ((Curve2d)arcSegment2dAt).GetSamplePoints(11)[5];
                                            double centreStation =
                                                al.GetDistAtPoint(
                                                    al.GetClosestPointTo(
                                                        new Point3d(samplePoint.X, samplePoint.Y, 0), false));
                                            //If centre of arc is not within PV -> continue
                                            if (!(centreStation > pvStStart && centreStation < pvStEnd)) continue;

                                            //Calculate radius
                                            double u = pline.GetPoint2dAt(i).GetDistanceTo(pline.GetPoint2dAt(i + 1));
                                            double radius = u * ((1 + bulge.Pow(2)) / (4 * Math.Abs(bulge)));
                                            double minRadius = GetPipeMinElasticRadius(pline);

                                            if (radius < minRadius) tos = TypeOfSegment.CurvedPipe;
                                            else tos = TypeOfSegment.ElasticArc;

                                            //Acquire start and end stations
                                            double curveStartStation = al.GetDistAtPoint(al.GetClosestPointTo(pline.GetPoint3dAt(i), false));
                                            double curveEndStation = al.GetDistAtPoint(al.GetClosestPointTo(pline.GetPoint3dAt(i + 1), false));
                                            double length = curveEndStation - curveStartStation;
                                            //double midStation = curveStartStation + length / 2;

                                            sampledMidtElevation = SampleProfile(surfaceProfile, centreStation);
                                            curX = originX + centreStation - pvStStart;
                                            curY = originY + (sampledMidtElevation - pvElBottom) *
                                                    profileViewStyle.GraphStyle.VerticalExaggeration;
                                            Point3d curvePt = new Point3d(curX, curY, 0);
                                            BlockReference brCurve =
                                                localDb.CreateBlockWithAttributes(bueBlockName, curvePt);

                                            DynamicBlockReferencePropertyCollection dbrpc = brCurve.DynamicBlockReferencePropertyCollection;
                                            foreach (DynamicBlockReferenceProperty dbrp in dbrpc)
                                            {
                                                if (dbrp.PropertyName == "Length")
                                                {
                                                    //prdDbg(length.ToString());
                                                    dbrp.Value = Math.Abs(length);
                                                }
                                            }

                                            //Set length text
                                            brCurve.SetAttributeStringValue("LGD", Math.Abs(length).ToString("0.0") + " m");

                                            switch (tos)
                                            {
                                                case TypeOfSegment.ElasticArc:
                                                    brCurve.SetAttributeStringValue("TEXT", $"Elastisk bue {radius.ToString("0.0")} m");
                                                    break;
                                                case TypeOfSegment.CurvedPipe:
                                                    brCurve.SetAttributeStringValue("TEXT", $"Buerør {radius.ToString("0.0")} m");
                                                    break;
                                                default:
                                                    break;
                                            }
                                        }
                                    }
                                }
                            }
                            #endregion 
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    fremTx.Abort();
                    fremTx.Dispose();
                    fremDb.Dispose();
                    tx.Abort();
                    prdDbg(ex.ExceptionInfo());
                    prdDbg(ex.ToString());
                    return;
                }
                fremTx.Abort();
                fremTx.Dispose();
                fremDb.Dispose();
                tx.Commit();
            }
        }

        /// <summary>
        /// Creates detailing based on an existing MIDT profile
        /// </summary>
        [CommandMethod("CREATEDETAILING")]
        public void createdetailing()
        {
            createdetailingmethod();
        }
        public void createdetailingmethod(DataReferencesOptions dataReferencesOptions = default, Database database = default)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database dB = database ?? docCol.MdiActiveDocument.Database;
            Editor ed = docCol.MdiActiveDocument.Editor;

            using (Transaction tx = dB.TransactionManager.StartTransaction())
            {
                #region Open fremtidig db
                DataReferencesOptions dro = dataReferencesOptions ?? new DataReferencesOptions();
                string projectName = dro.ProjectName;
                string etapeName = dro.EtapeName;

                // open the xref database
                Database fremDb = new Database(false, true);
                fremDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Fremtid"),
                    System.IO.FileShare.Read, false, string.Empty);
                Transaction fremTx = fremDb.TransactionManager.StartTransaction();
                HashSet<Curve> allCurves = fremDb.HashSetOfType<Curve>(fremTx);
                HashSet<BlockReference> allBrs = fremDb.HashSetOfType<BlockReference>(fremTx);
                #endregion

                //////////////////////////////////////
                string komponentBlockName = "DRISizeChangeAnno";
                string bueBlockName = "DRIPipeArcAnno";
                string weldBlockName = "DRIWeldAnno";
                string weldNumberBlockName = "DRIWeldAnnoText";
                //////////////////////////////////////

                try
                {
                    #region Common variables
                    BlockTableRecord modelSpace = dB.GetModelspaceForWrite();
                    BlockTable bt = tx.GetObject(dB.BlockTableId, OpenMode.ForRead) as BlockTable;
                    Plane plane = new Plane(); //For intersecting
                    HashSet<Alignment> als = dB.HashSetOfType<Alignment>(tx);
                    #endregion

                    #region Initialize PS for source object reference
                    PropertySetManager psmSource = new PropertySetManager(
                        dB, PropertySetManager.DefinedSets.DriSourceReference);
                    string sourceEntityHandleProperty = "SourceEntityHandle";
                    #endregion

                    #region Initialize PS for Alignment
                    PropertySetManager.DefinedSets propertySetName =
                        PropertySetManager.DefinedSets.DriPipelineData;
                    string belongsToAlignmentProperty = "BelongsToAlignment";
                    string branchesOffToAlignmentProperty = "BranchesOffToAlignment";
                    PropertySetManager psmBelongs = new PropertySetManager(
                        fremDb,
                        propertySetName);
                    #endregion

                    #region Import blocks if missing
                    if (!bt.Has(komponentBlockName) ||
                        !bt.Has(bueBlockName) ||
                        !bt.Has(weldBlockName) ||
                        !bt.Has(weldNumberBlockName))
                    {
                        prdDbg("Some of the blocks for detailing are missing! Importing...");
                        Database blockDb = new Database(false, true);
                        blockDb.ReadDwgFile("X:\\AutoCAD DRI - 01 Civil 3D\\DynBlokke\\Symboler.dwg",
                            System.IO.FileShare.Read, false, string.Empty);
                        Transaction blockTx = blockDb.TransactionManager.StartTransaction();

                        Oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(blockDb);
                        //Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);
                        Oid destDbMsId = dB.BlockTableId;

                        BlockTable sourceBt = blockTx.GetObject(blockDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                        ObjectIdCollection idsToClone = new ObjectIdCollection();
                        if (!bt.Has(komponentBlockName)) idsToClone.Add(sourceBt[komponentBlockName]);
                        if (!bt.Has(bueBlockName)) idsToClone.Add(sourceBt[bueBlockName]);
                        if (!bt.Has(weldBlockName)) idsToClone.Add(sourceBt[weldBlockName]);
                        if (!bt.Has(weldNumberBlockName)) idsToClone.Add(sourceBt[weldNumberBlockName]);

                        IdMapping mapping = new IdMapping();
                        blockDb.WblockCloneObjects(idsToClone, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);
                        blockTx.Commit();
                        blockTx.Dispose();
                        blockDb.Dispose();
                    }
                    #endregion

                    #region Delete previous blocks
                    //Delete previous blocks
                    var existingBlocks = dB.GetBlockReferenceByName(komponentBlockName);
                    existingBlocks.UnionWith(dB.GetBlockReferenceByName(bueBlockName));
                    existingBlocks.UnionWith(dB.GetBlockReferenceByName(weldBlockName));
                    existingBlocks.UnionWith(dB.GetBlockReferenceByName(weldNumberBlockName));
                    foreach (BlockReference br in existingBlocks)
                    {
                        br.CheckOrOpenForWrite();
                        br.Erase(true);
                    }
                    #endregion

                    foreach (Alignment al in als)
                    {
                        prdDbg($"\nProcessing: {al.Name}...");
                        #region If exist get surface profile and profile view
                        ObjectIdCollection profileIds = al.GetProfileIds();
                        ObjectIdCollection profileViewIds = al.GetProfileViewIds();
                        ProfileViewCollection pvs = new ProfileViewCollection(profileViewIds);

                        #region Fetch surface profile
                        Profile surfaceProfile = null;
                        foreach (Oid oid in profileIds)
                        {
                            Profile pTemp = oid.Go<Profile>(tx);
                            if (pTemp.Name == $"{al.Name}_surface_P") surfaceProfile = pTemp;
                        }
                        if (surfaceProfile == null)
                        {
                            prdDbg($"No surface profile found for alignment: {al.Name}, skipping current alignment.");
                            continue;
                        }
                        #endregion
                        #region Fetch midt profile
                        Profile midtProfile = null;
                        foreach (Oid oid in profileIds)
                        {
                            Profile pTemp = oid.Go<Profile>(tx);
                            if (pTemp.Name.Contains("MIDT")) midtProfile = pTemp;
                        }
                        if (midtProfile == null)
                        {
                            prdDbg($"No surface profile found for alignment: {al.Name}, skipping current alignment.");
                            continue;
                        }
                        #endregion
                        #endregion

                        #region GetCurvesAndBRs from fremtidig
                        HashSet<Curve> curves = allCurves
                            .Where(x => psmBelongs.FilterPropetyString(x, belongsToAlignmentProperty, al.Name))
                            .ToHashSet();

                        HashSet<BlockReference> brs = allBrs
                            .Where(x => psmBelongs.FilterPropetyString(x, belongsToAlignmentProperty, al.Name))
                            .ToHashSet();

                        HashSet<BlockReference> brsBranchesOffTo = allBrs
                            .Where(x => psmBelongs.FilterPropetyString(x, branchesOffToAlignmentProperty, al.Name))
                            .ToHashSet();

                        prdDbg($"Curves: {curves.Count}, Components: {brs.Count}");
                        #endregion

                        //PipelineSizeArray sizeArray = new PipelineSizeArray(al, curves);
                        //prdDbg("Curves:");
                        //prdDbg(sizeArray.ToString());

                        prdDbg("Blocks:");
                        PipelineSizeArray sizeArray = new PipelineSizeArray(al, curves, brs);
                        prdDbg(sizeArray.ToString());

                        #region Explode midt profile for later sampling
                        DBObjectCollection objs = new DBObjectCollection();
                        //First explode
                        midtProfile.Explode(objs);
                        //Explodes to 1 block
                        prdDbg($"Profile exploded to number of items: {objs.Count}.");
                        Entity firstExplode = (Entity)objs[0];

                        //Second explode
                        objs = new DBObjectCollection();
                        firstExplode.Explode(objs);
                        prdDbg($"Subsequent object exploded to number of items: {objs.Count}.");

                        HashSet<Line> lines = new HashSet<Line>();
                        foreach (DBObject obj in objs) lines.Add((Line)obj);

                        Extents3d extentsPv = default;
                        var isInsideQuery = lines
                            .Where(line =>
                            extentsPv.IsPointInsideXY(line.StartPoint) &&
                            extentsPv.IsPointInsideXY(line.EndPoint));

                        HashSet<Polyline> polylinesToGetDerivative = new HashSet<Polyline>();

                        //Join the resulting lines
                        foreach (ProfileView pv in pvs)
                        {
                            Extents3d te = pv.GeometricExtents;
                            extentsPv = new Extents3d(
                                new Point3d(te.MinPoint.X - 1, te.MinPoint.Y - 1, 0),
                                new Point3d(te.MaxPoint.X + 1, te.MaxPoint.Y + 1, 0));

                            var linesInside = isInsideQuery.ToList();

                            Line seedLine = linesInside[0];
                            linesInside.RemoveAt(0);

                            Polyline pline = new Polyline();
                            pline.AddVertexAt(0, new Point2d(seedLine.StartPoint.X, seedLine.StartPoint.Y), 0, 0, 0);
                            pline.AddVertexAt(1, new Point2d(seedLine.EndPoint.X, seedLine.EndPoint.Y), 0, 0, 0);

                            try
                            {
                                if (linesInside.Count != 0)
                                    pline.JoinEntities(linesInside.Cast<Entity>().ToArray());
                            }
                            catch (System.Exception)
                            {
                                prdDbg($"Midt i {pv.Name} could not be joined!");
                                throw;
                            }
                            polylinesToGetDerivative.Add(pline);

                            //pline.AddEntityToDbModelSpace(localDb);
                        }

                        #endregion

                        #region Create a pline from alignment for working around distatpoint problems

                        #endregion

                        foreach (ProfileView pv in pvs)
                        {
                            prdDbg($"Processing PV {pv.Name}.");

                            #region Variables and settings
                            Point3d pvOrigin = pv.Location;
                            double originX = pvOrigin.X;
                            double originY = pvOrigin.Y;

                            double pvStStart = pv.StationStart;
                            double pvStEnd = pv.StationEnd;
                            double pvElBottom = pv.ElevationMin;
                            double pvElTop = pv.ElevationMax;
                            double pvLength = pvStEnd - pvStStart;
                            #endregion

                            #region Determine what sizes appear in current PV
                            var pvSizeArray = sizeArray.GetPartialSizeArrayForPV(pv);
                            prdDbg(pvSizeArray.ToString());
                            #endregion

                            #region Prepare exaggeration handling
                            ProfileViewStyle profileViewStyle = tx
                                .GetObject(((Autodesk.Aec.DatabaseServices.Entity)pv)
                                .StyleId, OpenMode.ForRead) as ProfileViewStyle;
                            #endregion

                            double curStationBL = 0;
                            double sampledMidtElevation = 0;
                            double curX = 0, curY = 0;

                            #region Place size change blocks
                            for (int i = 0; i < pvSizeArray.Length; i++)
                            {   //Although look ahead is used, normal iteration is required
                                //Or cases where sizearray is only 1 size will not run at all
                                //In more general case the last iteration must be aborted
                                if (pvSizeArray.Length != 1 && i != pvSizeArray.Length - 1)
                                {
                                    //General case
                                    curStationBL = pvSizeArray[i].EndStation;
                                    sampledMidtElevation = SampleProfile(midtProfile, curStationBL);
                                    curX = originX + pvSizeArray[i].EndStation - pvStStart;
                                    curY = originY + (sampledMidtElevation - pvElBottom) *
                                        profileViewStyle.GraphStyle.VerticalExaggeration;
                                    double deltaY = (sampledMidtElevation - pvElBottom) *
                                        profileViewStyle.GraphStyle.VerticalExaggeration;
                                    //prdDbg($"{originY} + ({sampledMidtElevation} - {pvElBottom}) * " +
                                    //    $"{profileViewStyle.GraphStyle.VerticalExaggeration} = {deltaY}");
                                    BlockReference brInt =
                                        dB.CreateBlockWithAttributes(komponentBlockName, new Point3d(curX, curY, 0));
                                    brInt.SetAttributeStringValue("LEFTSIZE", $"DN {pvSizeArray[i].DN}");
                                    brInt.SetAttributeStringValue("RIGHTSIZE", $"DN {pvSizeArray[i + 1].DN}");
                                }
                                //Special cases
                                if (i == 0)
                                {//First iteration
                                    curStationBL = pvStStart;
                                    sampledMidtElevation = SampleProfile(midtProfile, curStationBL);
                                    curX = originX;
                                    curY = originY + (sampledMidtElevation - pvElBottom) *
                                        profileViewStyle.GraphStyle.VerticalExaggeration;
                                    BlockReference brAt0 =
                                        dB.CreateBlockWithAttributes(komponentBlockName, new Point3d(curX, curY, 0));
                                    brAt0.SetAttributeStringValue("LEFTSIZE", "");
                                    brAt0.SetAttributeStringValue("RIGHTSIZE", $"DN {pvSizeArray[0].DN}");

                                    if (pvSizeArray.Length == 1)
                                    {//If only one size in the array also place block at end
                                        curStationBL = pvStEnd;
                                        sampledMidtElevation = SampleProfile(midtProfile, curStationBL - .1);
                                        curX = originX + curStationBL - pvStStart;
                                        curY = originY + (sampledMidtElevation - pvElBottom) *
                                            profileViewStyle.GraphStyle.VerticalExaggeration;
                                        BlockReference brAtEnd =
                                            dB.CreateBlockWithAttributes(komponentBlockName, new Point3d(curX, curY, 0));
                                        brAtEnd.SetAttributeStringValue("LEFTSIZE", $"DN {pvSizeArray[0].DN}");
                                        brAtEnd.SetAttributeStringValue("RIGHTSIZE", "");
                                    }
                                }
                                if (i == pvSizeArray.Length - 2)
                                {//End of the iteration
                                    curStationBL = pvStEnd;
                                    sampledMidtElevation = SampleProfile(midtProfile, curStationBL - .1);
                                    curX = originX + curStationBL - pvStStart;
                                    curY = originY + (sampledMidtElevation - pvElBottom) *
                                        profileViewStyle.GraphStyle.VerticalExaggeration;
                                    BlockReference brAtEnd =
                                        dB.CreateBlockWithAttributes(komponentBlockName, new Point3d(curX, curY, 0));
                                    brAtEnd.SetAttributeStringValue("LEFTSIZE", $"DN {pvSizeArray[i + 1].DN}");
                                    brAtEnd.SetAttributeStringValue("RIGHTSIZE", "");
                                }
                            }
                            #endregion

                            #region Local method to sample profiles
                            //Local method to sample profiles
                            double SampleProfile(Profile profile, double station)
                            {
                                double sampledElevation = 0;
                                try { sampledElevation = profile.ElevationAt(station); }
                                catch (System.Exception)
                                {
                                    prdDbg($"Station {station} threw an exception when placing size change blocks! Skipping...");
                                    return 0;
                                }
                                return sampledElevation;
                            }
                            #endregion

                            #region Place component blocks
                            System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(
                                @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");
                            foreach (BlockReference br in brs)
                            {
                                string type = ReadStringParameterFromDataTable(br.RealName(), fjvKomponenter, "Type", 0);
                                if (type == "Reduktion" || type == "Svejsning") continue;
                                //Point3d firstIteration = al.GetClosestPointTo(br.Position, false);
                                //Point3d brLocation = al.GetClosestPointTo(firstIteration, false);
                                Point3d brLocation = al.GetClosestPointTo(br.Position, false);

                                double station = 0;
                                double offset = 0;
                                try
                                {
                                    al.StationOffset(brLocation.X, brLocation.Y, ref station, ref offset);
                                }
                                catch (System.Exception)
                                {
                                    prdDbg(br.RealName());
                                    prdDbg(br.Handle.ToString());
                                    prdDbg(br.Position.ToString());
                                    prdDbg(brLocation.ToString());
                                    throw;
                                }

                                //Determine if blockref is within current PV
                                //If within -> place block, else go to next iteration
                                if (!(station >= pvStStart && station <= pvStEnd)) continue;

                                sampledMidtElevation = SampleProfile(midtProfile, station);
                                double X = originX + station - pvStStart;
                                double Y = originY + (sampledMidtElevation - pvElBottom) *
                                        profileViewStyle.GraphStyle.VerticalExaggeration;
                                BlockReference brSign = dB.CreateBlockWithAttributes(komponentBlockName, new Point3d(X, Y, 0));
                                brSign.SetAttributeStringValue("LEFTSIZE", type);

                                psmBelongs.GetOrAttachPropertySet(br);
                                if (psmBelongs.ReadPropertyString(branchesOffToAlignmentProperty).IsNotNoE())
                                    brSign.SetAttributeStringValue("RIGHTSIZE",
                                        psmBelongs.ReadPropertyString(branchesOffToAlignmentProperty));
                                else brSign.SetAttributeStringValue("RIGHTSIZE", "");

                                psmSource.GetOrAttachPropertySet(brSign);
                                psmSource.WritePropertyString(sourceEntityHandleProperty, br.Handle.ToString());
                            }
                            #endregion

                            #region Place component blocks for branches belonging to other alignments
                            foreach (BlockReference br in brsBranchesOffTo)
                            {
                                string type = ReadStringParameterFromDataTable(br.RealName(), fjvKomponenter, "Type", 0);
                                Point3d brLocation = al.GetClosestPointTo(br.Position, false);

                                double station = 0;
                                try
                                {
                                    station = al.GetDistAtPoint(brLocation);
                                }
                                catch (System.Exception)
                                {
                                    prdDbg("GetDistAtPoint failed! Performing evasive maneouvres.");
                                    //GetDistAtPoint failed again!!!!! perform evasive maneouvres
                                    try
                                    {
                                        double offset = 0;
                                        al.StationOffset(brLocation.X, brLocation.Y, ref station, ref offset);
                                        //station = plineForSamplingDistance.GetDistAtPoint(brLocation);
                                    }
                                    catch (System.Exception ex)
                                    {
                                        prdDbg(br.RealName());
                                        prdDbg(br.Handle.ToString());
                                        prdDbg(br.Position.ToString());
                                        prdDbg(brLocation.ToString());
                                        throw;
                                    }
                                }

                                //Determine if blockref is within current PV
                                //If within -> place block, else go to next iteration
                                if (!(station >= pvStStart && station <= pvStEnd)) continue;

                                sampledMidtElevation = SampleProfile(midtProfile, station);
                                double X = originX + station - pvStStart;
                                double Y = originY + (sampledMidtElevation - pvElBottom) *
                                        profileViewStyle.GraphStyle.VerticalExaggeration;
                                BlockReference brSign = dB.CreateBlockWithAttributes(komponentBlockName, new Point3d(X, Y, 0));
                                brSign.SetAttributeStringValue("LEFTSIZE", type);

                                psmBelongs.GetOrAttachPropertySet(br);
                                brSign.SetAttributeStringValue("RIGHTSIZE",
                                        psmBelongs.ReadPropertyString(belongsToAlignmentProperty));

                                psmSource.GetOrAttachPropertySet(brSign);
                                psmSource.WritePropertyString(sourceEntityHandleProperty, br.Handle.ToString());
                            }
                            #endregion

                            #region Place weld blocks
                            HashSet<BlockReference> newWeldNumberBlocks = new HashSet<BlockReference>();

                            foreach (BlockReference br in brs)
                            {
                                #region Determine placement
                                string type = ReadStringParameterFromDataTable(br.RealName(), fjvKomponenter, "Type", 0);
                                if (type != "Svejsning") continue;

                                Point3d brLocation = al.GetClosestPointTo(br.Position, false);

                                double station = 0;
                                double offset = 0;
                                al.StationOffset(brLocation.X, brLocation.Y, ref station, ref offset);

                                //Determine if blockref is within current PV
                                //If within -> place block, else go to next iteration
                                if (!(station >= pvStStart && station <= pvStEnd)) continue;

                                sampledMidtElevation = SampleProfile(midtProfile, station);
                                double X = originX + station - pvStStart;
                                double Y = originY + (sampledMidtElevation - pvElBottom) *
                                        profileViewStyle.GraphStyle.VerticalExaggeration;

                                Point3d wPt = new Point3d(X, Y, 0);

                                BlockReference brWeld =
                                    dB.CreateBlockWithAttributes(weldBlockName, wPt);

                                BlockReference brWeldNumber =
                                    dB.CreateBlockWithAttributes(weldNumberBlockName, wPt);

                                //Gather new weld numebrs in a collection to be able to find overlaps
                                newWeldNumberBlocks.Add(brWeldNumber);

                                //Set attributes
                                string nummer = br.GetAttributeStringValue("NUMMER");

                                brWeldNumber.SetAttributeStringValue("NUMMER", nummer);

                                psmSource.GetOrAttachPropertySet(brWeld);
                                psmSource.WritePropertyString(sourceEntityHandleProperty, br.Handle.ToString());
                                #endregion

                                #region Determine rotation
                                //Get the nearest exploded profile polyline and sample first derivative
                                HashSet<(Polyline pline, double dist)> ps = new HashSet<(Polyline pline, double dist)>();
                                foreach (Polyline pline in polylinesToGetDerivative)
                                {
                                    Point3d distPt = pline.GetClosestPointTo(wPt, false);
                                    ps.Add((pline, distPt.DistanceHorizontalTo(wPt)));
                                }
                                Polyline nearest = ps.MinBy(x => x.dist).FirstOrDefault().pline;

                                Vector3d deriv = nearest.GetFirstDerivative(
                                    nearest.GetClosestPointTo(wPt, false));

                                double rotation = Math.Atan2(deriv.Y, deriv.X);
                                brWeld.Rotation = rotation;
                                #endregion

                                #region Scale block to fit kappe
                                SizeEntry curSize = sizeArray.GetSizeAtStation(station);
                                brWeld.ScaleFactors = new Scale3d(1, curSize.Kod / 1000 *
                                    profileViewStyle.GraphStyle.VerticalExaggeration, 1);
                                #endregion
                            }

                            #region Find overlapping weld labels and find a solution
                            var clusters = newWeldNumberBlocks.GroupByCluster((x, y) => Overlaps(x, y), 0.0001);
                            double Overlaps(BlockReference i, BlockReference j)
                            {
                                Extents3d extI = i.GeometricExtents;
                                Extents3d extJ = j.GeometricExtents;

                                double wI = extI.MaxPoint.X - extI.MinPoint.X;
                                double wJ = extJ.MaxPoint.X - extJ.MinPoint.X;

                                double threshold = wI / 2 + wJ / 2;

                                double centreIX = extI.MinPoint.X + wI / 2;
                                double centreJX = extJ.MinPoint.X + wJ / 2;

                                double dist = Math.Abs(centreIX - centreJX);
                                double result = dist - threshold;

                                return result < 0 ? 0 : result;
                            }
                            foreach (IGrouping<BlockReference, BlockReference> cluster in clusters)
                            {
                                if (cluster.Count() < 2) continue;

                                List<string> numbers = new List<string>();
                                string prefix = "";
                                foreach (BlockReference item in cluster)
                                {
                                    string number = item.GetAttributeStringValue("NUMMER");
                                    var splits = number.Split('.');
                                    prefix = splits[0];
                                    numbers.Add(splits[1]);
                                }

                                List<int> convertedNumbers = new List<int>();
                                foreach (string number in numbers)
                                {
                                    int result;
                                    if (int.TryParse(number, out result)) convertedNumbers.Add(result);
                                }

                                convertedNumbers.Sort();

                                string finalNumber = $"{prefix}.{convertedNumbers.First().ToString("000")}" +
                                    $" - {convertedNumbers.Last().ToString("000")}";

                                int i = 0;
                                foreach (BlockReference item in cluster)
                                {
                                    if (i == 0) item.SetAttributeStringValue("NUMMER", finalNumber);
                                    else { item.Erase(true); }
                                    i++;
                                }
                            }
                            #endregion
                            #endregion

                            #region Find curves and annotate
                            foreach (Curve curve in curves)
                            {
                                if (curve is Polyline pline)
                                {
                                    //Detect arcs and determine if it is a buerør or not
                                    for (int i = 0; i < pline.NumberOfVertices; i++)
                                    {
                                        TypeOfSegment tos;
                                        double bulge = pline.GetBulgeAt(i);
                                        if (bulge == 0) tos = TypeOfSegment.Straight;
                                        else
                                        {
                                            //Determine if centre of arc is within view
                                            CircularArc2d arcSegment2dAt = pline.GetArcSegment2dAt(i);
                                            Point2d samplePoint = ((Curve2d)arcSegment2dAt).GetSamplePoints(11)[5];
                                            Point3d location = al.GetClosestPointTo(
                                                        new Point3d(samplePoint.X, samplePoint.Y, 0), false);
                                            double centreStation = 0;
                                            double centreOffset = 0;
                                            al.StationOffset(location.X, location.Y, ref centreStation, ref centreOffset);

                                            //If centre of arc is not within PV -> continue
                                            if (!(centreStation > pvStStart && centreStation < pvStEnd)) continue;

                                            //Calculate radius
                                            double u = pline.GetPoint2dAt(i).GetDistanceTo(pline.GetPoint2dAt(i + 1));
                                            double radius = u * ((1 + bulge.Pow(2)) / (4 * Math.Abs(bulge)));
                                            double minRadius = GetPipeMinElasticRadius(pline);

                                            if (radius < minRadius) tos = TypeOfSegment.CurvedPipe;
                                            else tos = TypeOfSegment.ElasticArc;

                                            //Acquire start and end stations
                                            location = al.GetClosestPointTo(pline.GetPoint3dAt(i), false);
                                            double curveStartStation = 0;
                                            double offset = 0;
                                            al.StationOffset(location.X, location.Y, ref curveStartStation, ref offset);

                                            location = al.GetClosestPointTo(pline.GetPoint3dAt(i + 1), false);
                                            double curveEndStation = 0;
                                            al.StationOffset(location.X, location.Y, ref curveEndStation, ref offset);

                                            double length = curveEndStation - curveStartStation;
                                            //double midStation = curveStartStation + length / 2;

                                            sampledMidtElevation = SampleProfile(midtProfile, centreStation);
                                            curX = originX + centreStation - pvStStart;
                                            curY = originY + (sampledMidtElevation - pvElBottom) *
                                                    profileViewStyle.GraphStyle.VerticalExaggeration;
                                            Point3d curvePt = new Point3d(curX, curY, 0);
                                            BlockReference brCurve =
                                                dB.CreateBlockWithAttributes(bueBlockName, curvePt);

                                            DynamicBlockReferencePropertyCollection dbrpc = brCurve.DynamicBlockReferencePropertyCollection;
                                            foreach (DynamicBlockReferenceProperty dbrp in dbrpc)
                                            {
                                                if (dbrp.PropertyName == "Length")
                                                {
                                                    //prdDbg(length.ToString());
                                                    dbrp.Value = Math.Abs(length);
                                                }
                                            }

                                            //Set length text
                                            brCurve.SetAttributeStringValue("LGD", Math.Abs(length).ToString("0.0") + " m");

                                            switch (tos)
                                            {
                                                case TypeOfSegment.ElasticArc:
                                                    brCurve.SetAttributeStringValue("TEXT", $"Elastisk bue {radius.ToString("0.0")} m");
                                                    break;
                                                case TypeOfSegment.CurvedPipe:
                                                    brCurve.SetAttributeStringValue("TEXT", $"Buerør {radius.ToString("0.0")} m");
                                                    break;
                                                default:
                                                    break;
                                            }

                                            #region Determine rotation
                                            //Get the nearest exploded profile polyline and sample first derivative
                                            HashSet<(Polyline pline, double dist)> ps = new HashSet<(Polyline pline, double dist)>();
                                            foreach (Polyline pline2 in polylinesToGetDerivative)
                                            {
                                                Point3d distPt = pline2.GetClosestPointTo(curvePt, false);
                                                ps.Add((pline2, distPt.DistanceHorizontalTo(curvePt)));
                                            }
                                            Polyline nearest = ps.MinBy(x => x.dist).FirstOrDefault().pline;

                                            Vector3d deriv = nearest.GetFirstDerivative(
                                                nearest.GetClosestPointTo(curvePt, false));

                                            double rotation = Math.Atan2(deriv.Y, deriv.X);
                                            brCurve.Rotation = rotation;
                                            #endregion
                                        }
                                    }
                                }
                            }
                            #endregion 
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    fremTx.Abort();
                    fremTx.Dispose();
                    fremDb.Dispose();
                    tx.Abort();
                    prdDbg(ex.ExceptionInfo());
                    prdDbg(ex.ToString());
                    return;
                }
                fremTx.Abort();
                fremTx.Dispose();
                fremDb.Dispose();
                tx.Commit();
            }
        }

        [CommandMethod("resetprofileviews")]
        public void resetprofileviews()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor ed = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    //#region Delete cogo points
                    //CogoPointCollection cogoPoints = civilDoc.CogoPoints;
                    //ObjectIdCollection cpIds = new ObjectIdCollection();
                    //foreach (oid oid in cogoPoints) cpIds.Add(oid);
                    //foreach (oid oid in cpIds) cogoPoints.Remove(oid);
                    //#endregion

                    #region Stylize Profile Views
                    HashSet<ProfileView> pvs = localDb.HashSetOfType<ProfileView>(tx);

                    Oid pvStyleId = Oid.Null;
                    try
                    {
                        pvStyleId = civilDoc.Styles.ProfileViewStyles["PROFILE VIEW L TO R NO SCALE"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nProfile view style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    foreach (ProfileView pv in pvs)
                    {
                        pv.CheckOrOpenForWrite();
                        pv.StyleId = pvStyleId;

                        var brs = localDb.HashSetOfType<BlockReference>(tx);
                        foreach (BlockReference br in brs)
                        {
                            if (br.Name == pv.Name)
                            {
                                br.CheckOrOpenForWrite();
                                br.Erase(true);
                            }
                        }
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage("\n" + ex.Message);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("destroyalltables")]
        public void destroyalltables()
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
                    #region Ask if continue with operation
                    const string kwd1 = "No";
                    const string kwd2 = "Yes";
                    const string kwd3 = "ALL";

                    PromptKeywordOptions pKeyOpts = new PromptKeywordOptions("");
                    pKeyOpts.Message = "\nThis will destroy all OD Tables in drawing!!! Do you want to continue? ";
                    pKeyOpts.Keywords.Add(kwd1);
                    pKeyOpts.Keywords.Add(kwd2);
                    pKeyOpts.Keywords.Add(kwd3);
                    pKeyOpts.AllowNone = true;
                    pKeyOpts.Keywords.Default = kwd1;
                    PromptResult pKeyRes = editor.GetKeywords(pKeyOpts);
                    #endregion

                    #region Try destroying all tables
                    switch (pKeyRes.StringResult)
                    {
                        case kwd1:
                            tx.Abort();
                            return;
                        case kwd2:
                        case kwd3:
                            Tables odTables = HostMapApplicationServices.Application.ActiveProject.ODTables;
                            StringCollection allDbTables = odTables.GetTableNames();

                            foreach (string name in allDbTables)
                            {
                                //If kwd is Yes, skip CrossingData and IdRecord
                                //Else delete them also
                                if (pKeyRes.StringResult == kwd2)
                                {
                                    if (name == "CrossingData" ||
                                        name == "IdRecord")
                                    {
                                        editor.WriteMessage($"\nSkipping table {name}!");
                                        continue;
                                    }
                                }
                                editor.WriteMessage($"\nDestroying table {name} -> ");
                                if (DoesTableExist(odTables, name))
                                {
                                    if (RemoveTable(odTables, name))
                                    {
                                        editor.WriteMessage($"Done!");
                                    }
                                    else editor.WriteMessage($"Fail!");
                                }
                                else
                                {
                                    editor.WriteMessage($"\nTable {name} does not exist!");
                                }
                            }
                            break;
                        default:
                            tx.Abort();
                            return;
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

        [CommandMethod("revealalignments")]
        [CommandMethod("ral")]
        public void revealalignments()
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
                    Oid alStyle = civilDoc.Styles.AlignmentStyles["FJV TRACÉ SHOW"];
                    Oid labelSetStyle = civilDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles["STD 20-5"];
                    HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);

                    foreach (Alignment al in als)
                    {
                        al.CheckOrOpenForWrite();
                        al.StyleId = alStyle;
                        al.ImportLabelSet(labelSetStyle);
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

        [CommandMethod("hidealignments")]
        [CommandMethod("hal")]
        public void hidealignments()
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
                    Oid alStyle = civilDoc.Styles.AlignmentStyles["FJV TRACE NO SHOW"];
                    Oid labelSetStyle = civilDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles["_No Labels"];
                    HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);

                    foreach (Alignment al in als)
                    {
                        al.CheckOrOpenForWrite();
                        al.StyleId = alStyle;
                        al.ImportLabelSet(labelSetStyle);
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

        [CommandMethod("FINALIZEVIEWFRAMES")]
        [CommandMethod("FVF")]
        public void finalizeviewframes()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            try
            {
                #region Operation

                string path = string.Empty;
                OpenFileDialog dialog = new OpenFileDialog()
                {
                    Title = "Choose txt file:",
                    DefaultExt = "txt",
                    Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*",
                    FilterIndex = 0
                };
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    path = dialog.FileName;
                }
                else return;

                List<string> fileList;
                fileList = File.ReadAllLines(path).ToList();
                path = Path.GetDirectoryName(path) + "\\";

                foreach (string name in fileList)
                {
                    prdDbg(name);
                    string fileName = path + name;

                    using (Database extDb = new Database(false, true))
                    {
                        extDb.ReadDwgFile(fileName, System.IO.FileShare.ReadWrite, false, "");

                        using (Transaction extTx = extDb.TransactionManager.StartTransaction())
                        {
                            #region Change Alignment style
                            CivilDocument extCDoc = CivilDocument.GetCivilDocument(extDb);

                            HashSet<Alignment> als = extDb.HashSetOfType<Alignment>(extTx);

                            foreach (Alignment al in als)
                            {
                                al.CheckOrOpenForWrite();
                                al.StyleId = extCDoc.Styles.AlignmentStyles["FJV TRACE NO SHOW"];
                                Oid labelSetOid = extCDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles["_No Labels"];
                                al.ImportLabelSet(labelSetOid);
                            }
                            #endregion

                            extTx.Commit();
                        }
                        extDb.SaveAs(extDb.Filename, true, DwgVersion.Current, null);
                    }
                    System.Windows.Forms.Application.DoEvents();
                }
                #endregion
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage("\n" + ex.Message);
                return;
            }
        }

        [CommandMethod("DETACHATTACHDWG")]
        public void detachattachdwg()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            try
            {
                #region Operation

                //************************************
                string xrefName = "Fremtidig fjernvarme";
                string xrefPath = @"X:\037-1178 - Gladsaxe udbygning - Dokumenter\01 Intern\02 Tegninger\" +
                                  @"01 Autocad - xxx\Etape 1.2\Fremtidig fjernvarme.dwg";
                //************************************

                string path = string.Empty;
                OpenFileDialog dialog = new OpenFileDialog()
                {
                    Title = "Choose txt file:",
                    DefaultExt = "txt",
                    Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*",
                    FilterIndex = 0
                };
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    path = dialog.FileName;
                }
                else return;

                List<string> fileList;
                fileList = File.ReadAllLines(path).ToList();
                path = Path.GetDirectoryName(path) + "\\";

                foreach (string name in fileList)
                {
                    prdDbg(name);
                    string fileName = path + name;

                    using (Database extDb = new Database(false, true))
                    {
                        extDb.ReadDwgFile(fileName, System.IO.FileShare.ReadWrite, false, "");

                        #region Detach Fremtidig fjernvarme
                        using (Transaction extTx = extDb.TransactionManager.StartTransaction())
                        {
                            try
                            {
                                BlockTable bt = extTx.GetObject(extDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                                foreach (Oid oid in bt)
                                {
                                    BlockTableRecord btr = extTx.GetObject(oid, OpenMode.ForWrite) as BlockTableRecord;
                                    //if (btr.Name.Contains("_alignment"))
                                    if (btr.Name == xrefName && btr.IsFromExternalReference)
                                    {
                                        extDb.DetachXref(btr.ObjectId);
                                    }
                                }
                            }
                            catch (System.Exception ex)
                            {
                                prdDbg(ex.ToString());
                                extTx.Abort();
                                throw;
                            }

                            extTx.Commit();
                        }
                        #endregion

                        #region Attach Fremtidig fjernvarme and change draw order
                        using (Transaction extTx = extDb.TransactionManager.StartTransaction())
                        {
                            try
                            {
                                BlockTable bt = extTx.GetObject(extDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                                Oid xrefId = extDb.AttachXref(xrefPath, xrefName);
                                if (xrefId == Oid.Null) throw new System.Exception("Creating xref failed!");

                                Point3d insPt = new Point3d(0, 0, 0);
                                using (BlockReference br = new BlockReference(insPt, xrefId))
                                {
                                    BlockTableRecord modelSpace = extDb.GetModelspaceForWrite();
                                    modelSpace.AppendEntity(br);
                                    extTx.AddNewlyCreatedDBObject(br, true);

                                    br.Layer = "XREF-FJV_FREMTID";

                                    DrawOrderTable dot = modelSpace.DrawOrderTableId.Go<DrawOrderTable>(extTx);
                                    dot.CheckOrOpenForWrite();

                                    Alignment al = extDb.ListOfType<Alignment>(extTx).FirstOrDefault();
                                    if (al == null) throw new System.Exception("No alignments found in drawing!");

                                    ObjectIdCollection idCol = new ObjectIdCollection(new Oid[1] { br.Id });

                                    dot.MoveBelow(idCol, al.Id);
                                }
                            }
                            catch (System.Exception ex)
                            {
                                prdDbg(ex.ToString());
                                extTx.Abort();
                                throw;
                            }

                            extTx.Commit();
                        }
                        #endregion

                        extDb.SaveAs(extDb.Filename, true, DwgVersion.Current, null);
                    }
                    System.Windows.Forms.Application.DoEvents();
                }
                #endregion
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage("\n" + ex.Message);
                return;
            }
        }

        [CommandMethod("APPLYCOLORSTODWGS")]
        public void applycolorstodwgs()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            try
            {
                #region Operation

                string path = string.Empty;
                OpenFileDialog dialog = new OpenFileDialog()
                {
                    Title = "Choose txt file:",
                    DefaultExt = "txt",
                    Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*",
                    FilterIndex = 0
                };
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    path = dialog.FileName;
                }
                else return;

                List<string> fileList;
                fileList = File.ReadAllLines(path).ToList();
                path = Path.GetDirectoryName(path) + "\\";

                foreach (string name in fileList)
                {
                    prdDbg(name);
                    string fileName = path + name;

                    using (Database extDb = new Database(false, true))
                    {
                        extDb.ReadDwgFile(fileName, System.IO.FileShare.ReadWrite, false, "");

                        using (Transaction extTx = extDb.TransactionManager.StartTransaction())
                        {
                            colorizealllerlayers(extDb);

                            extTx.Commit();
                        }
                        extDb.SaveAs(extDb.Filename, true, DwgVersion.Current, null);
                    }
                    System.Windows.Forms.Application.DoEvents();
                }
                #endregion
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage("\n" + ex.ToString());
                return;
            }
        }
        /// <summary>
        /// Helper method to create empty OD tables if missing on entity
        /// </summary>
        [CommandMethod("createobjectdataentryforentity")]
        public void createobjectdataentryforentity()
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
                    #region Select target pline3d
                    PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                        "\nSelect polyline3d where to create entries:");
                    promptEntityOptions1.SetRejectMessage("\n Not a polyline3d!");
                    promptEntityOptions1.AddAllowedClass(typeof(Polyline3d), true);
                    PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    if (((PromptResult)entity1).Status != PromptStatus.OK) { tx.Abort(); return; }
                    Autodesk.AutoCAD.DatabaseServices.ObjectId targetPline3dId = entity1.ObjectId;
                    #endregion

                    #region Choose table
                    Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;
                    Autodesk.Gis.Map.ObjectData.Table table = tables["PLAN_A_AV_LEDNING_TRACE_H"];

                    if (!AddEmptyODRecord(table, targetPline3dId))
                    {
                        editor.WriteMessage("Something went wrong!");
                    }

                    #endregion


                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("COPYODFROMENTTOENT")]
        public void copyodfromenttoent()
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
                    #region Select entities
                    PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                        "\nSelect entity FROM where to copy OD:");
                    promptEntityOptions1.SetRejectMessage("\n Not an entity!");
                    promptEntityOptions1.AddAllowedClass(typeof(Entity), true);
                    PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    if (((PromptResult)entity1).Status != PromptStatus.OK) { tx.Abort(); return; }
                    Autodesk.AutoCAD.DatabaseServices.ObjectId sourceId = entity1.ObjectId;

                    PromptEntityOptions promptEntityOptions2 = new PromptEntityOptions(
                        "\nSelect entity where to copy OD TO:");
                    promptEntityOptions2.SetRejectMessage("\n Not an entity!");
                    promptEntityOptions2.AddAllowedClass(typeof(Entity), true);
                    PromptEntityResult entity2 = editor.GetEntity(promptEntityOptions2);
                    if (((PromptResult)entity2).Status != PromptStatus.OK) { tx.Abort(); return; }
                    Autodesk.AutoCAD.DatabaseServices.ObjectId targetId = entity2.ObjectId;
                    #endregion


                    #region Choose table

                    CopyAllOD(HostMapApplicationServices.Application.ActiveProject.ODTables,
                        sourceId, targetId);

                    #endregion


                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("colorviewframes")]
        public void colorviewframes()
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
                    #region XrefNodeMethod
                    //XrefGraph graph = localDb.GetHostDwgXrefGraph(false);

                    ////skip node zero, hence i=1
                    //for (int i = 1; i < graph.NumNodes; i++)
                    //{
                    //    XrefGraphNode node = graph.GetXrefNode(i);
                    //    if (node.Name.Contains("alignment"))
                    //    {
                    //        editor.WriteMessage($"\nXref: {node.Name}.");
                    //        node.
                    //    }
                    //} 
                    #endregion

                    var bt = (BlockTable)tx.GetObject(localDb.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                    foreach (Oid id in ms)
                    {
                        var br = tx.GetObject(id, OpenMode.ForRead) as BlockReference;
                        if (br != null)
                        {
                            var bd = (BlockTableRecord)tx.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                            if (bd.IsFromExternalReference)
                            {
                                var xdb = bd.GetXrefDatabase(false);
                                if (xdb != null)
                                {
                                    string fileName = xdb.Filename;
                                    if (fileName.Contains("_alignment"))
                                    {
                                        editor.WriteMessage($"\n{xdb.Filename}.");
                                        if (IsFileLockedOrReadOnly(new FileInfo(fileName)))
                                        {
                                            editor.WriteMessage("\nUnable to modify the external reference. " +
                                                                  "It may be open in the editor or read-only.");
                                        }
                                        else
                                        {
                                            using (var xf = XrefFileLock.LockFile(xdb.XrefBlockId))
                                            {
                                                //Make sure the original symbols are loaded
                                                xdb.RestoreOriginalXrefSymbols();
                                                // Depending on the operation you're performing,
                                                // you may need to set the WorkingDatabase to
                                                // be that of the Xref
                                                //HostApplicationServices.WorkingDatabase = xdb;

                                                using (Transaction xTx = xdb.TransactionManager.StartTransaction())
                                                {
                                                    try
                                                    {
                                                        CivilDocument stylesDoc = CivilDocument.GetCivilDocument(xdb);

                                                        //View Frame Styles edit
                                                        Oid vfsId = stylesDoc.Styles.ViewFrameStyles["Basic"];
                                                        ViewFrameStyle vfs = xTx.GetObject(vfsId, OpenMode.ForWrite) as ViewFrameStyle;
                                                        DisplayStyle planStyle = vfs.GetViewFrameBoundaryDisplayStylePlan();
                                                        planStyle.Color = Color.FromColorIndex(ColorMethod.ByLayer, 256);

                                                        string layName = "C-ANNO-VFRM";

                                                        LayerTable lt = xTx.GetObject(xdb.LayerTableId, OpenMode.ForRead)
                                                            as LayerTable;
                                                        if (lt.Has(layName))
                                                        {
                                                            LayerTableRecord ltr = xTx.GetObject(lt[layName], OpenMode.ForWrite)
                                                                as LayerTableRecord;
                                                            ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 7);
                                                        }
                                                    }
                                                    catch (System.Exception)
                                                    {
                                                        xTx.Abort();
                                                        tx.Abort();
                                                        return;
                                                        //throw;
                                                    }

                                                    xTx.Commit();
                                                }
                                                // And then set things back, afterwards
                                                //HostApplicationServices.WorkingDatabase = db;
                                                xdb.RestoreForwardingXrefSymbols();
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    string viewFrameLayerName = "C-ANNO-VFRM";

                    LayerTable localLt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead)
                        as LayerTable;

                    short[] sequenceInit = new short[] { 1, 3, 4, 5, 6, 40 };
                    LinkedList<short> colorSequence = new LinkedList<short>(sequenceInit);
                    LinkedListNode<short> curNode;
                    curNode = colorSequence.First;

                    foreach (Oid id in localLt)
                    {
                        LayerTableRecord ltr = (LayerTableRecord)tx.GetObject(id, OpenMode.ForRead);

                        if (ltr.Name.Contains("_alignment") &&
                            ltr.Name.Contains(viewFrameLayerName) &&
                            !ltr.Name.Contains("TEXT"))
                        {
                            editor.WriteMessage($"\n{ltr.Name}");
                            ltr.UpgradeOpen();
                            ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 0);
                            //ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, curNode.Value);

                            //if (curNode.Next == null) curNode = colorSequence.First;
                            //else curNode = curNode.Next;

                        }
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("SETGLOBALWIDTH")]
        public void setglobalwidth()
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
                    #region BlockTables
                    // Open the Block table for read
                    BlockTable bt = tx.GetObject(localDb.BlockTableId,
                                                       OpenMode.ForRead) as BlockTable;
                    // Open the Block table record Model space for write
                    BlockTableRecord modelSpace = tx.GetObject(bt[BlockTableRecord.ModelSpace],
                                                          OpenMode.ForWrite) as BlockTableRecord;
                    #endregion

                    #region Read Csv Data for Layers
                    //Establish the pathnames to files
                    //Files should be placed in a specific folder on desktop
                    string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";

                    System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
                    #endregion

                    //Reference datatables
                    Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

                    #region Convert splines
                    //Enclose in a scope to prevent splines collection from being used later on
                    {
                        List<Spline> splines = localDb.ListOfType<Spline>(tx);
                        editor.WriteMessage($"\nNr. of splines: {splines.Count}. Converting to polylines...");

                        foreach (Spline spline in splines)
                        {
                            Curve curve = spline.ToPolylineWithPrecision(10);
                            modelSpace.AppendEntity(curve);
                            tx.AddNewlyCreatedDBObject(curve, true);
                            curve.CheckOrOpenForWrite();
                            curve.Layer = spline.Layer;
                            CopyAllOD(tables, spline, curve);
                            curve.DowngradeOpen();
                            spline.CheckOrOpenForWrite();
                            spline.Erase(true);
                        }
                    }
                    #endregion

                    #region Convert lines
                    //Enclose in a scope to prevent lines collection from being used later on
                    {
                        List<Line> lines = localDb.ListOfType<Line>(tx);
                        editor.WriteMessage($"\nNr. of lines: {lines.Count}. Converting to polylines...");

                        foreach (Line line in lines)
                        {
                            Polyline pline = new Polyline(2);
                            pline.CheckOrOpenForWrite();
                            pline.AddVertexAt(0, new Point2d(line.StartPoint.X, line.StartPoint.Y), 0, 0, 0);
                            pline.AddVertexAt(1, new Point2d(line.EndPoint.X, line.EndPoint.Y), 0, 0, 0);
                            modelSpace.AppendEntity(pline);
                            tx.AddNewlyCreatedDBObject(pline, true);
                            pline.Layer = line.Layer;
                            CopyAllOD(tables, line, pline);
                            pline.DowngradeOpen();
                            line.CheckOrOpenForWrite();
                            line.Erase(true);
                        }
                    }
                    #endregion

                    #region Load linework for analysis
                    prdDbg("\nLoading linework for analyzing...");

                    HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx);
                    editor.WriteMessage($"\nNr. of polylines: {plines.Count}");
                    #endregion

                    #region Read diameter and set width
                    int layerNameNotDefined = 0;
                    int layerNameIgnored = 0;
                    int layerDiameterDefMissing = 0;
                    int findDescriptionPartsFailed = 0;
                    foreach (Polyline pline in plines)
                    {
                        //Set color to by layer
                        pline.CheckOrOpenForWrite();
                        pline.ColorIndex = 256;

                        //Check if pline's layer exists in krydsninger
                        string nameInFile = ReadStringParameterFromDataTable(pline.Layer, dtKrydsninger, "Navn", 0);
                        if (nameInFile.IsNoE())
                        {
                            layerNameNotDefined++;
                            continue;
                        }

                        //Check if pline's layer is IGNOREd
                        string typeInFile = ReadStringParameterFromDataTable(pline.Layer, dtKrydsninger, "Type", 0);
                        if (typeInFile == "IGNORE")
                        {
                            layerNameIgnored++;
                            continue;
                        }

                        //Check if diameter information exists
                        string diameterDef = ReadStringParameterFromDataTable(pline.Layer,
                                dtKrydsninger, "Diameter", 0);
                        if (diameterDef.IsNoE())
                        {
                            layerDiameterDefMissing++;
                            continue;
                        }

                        var list = FindDescriptionParts(diameterDef);
                        if (list.Count < 1)
                        {
                            findDescriptionPartsFailed++;
                            continue;
                        }
                        string[] parts = list[0].Item2.Split(':');

                        int diaOriginal = ReadIntPropertyValue(tables, pline.Id, parts[0], parts[1]);
                        prdDbg(pline.Handle.ToString() + ": " + diaOriginal.ToString());

                        double dia = Convert.ToDouble(diaOriginal) / 1000;

                        if (dia == 0) dia = 0.09;

                        pline.ConstantWidth = dia;
                    }
                    #endregion

                    #region Reporting

                    prdDbg($"Layer name not defined in Krydsninger.csv for {layerNameNotDefined} polyline(s).");
                    prdDbg($"Layer name is set to IGNORE in Krydsninger.csv for {layerNameIgnored} polyline(s).");
                    prdDbg($"Diameter definition is not defined in Krydsninger.csv for {layerDiameterDefMissing} polyline(s).");
                    prdDbg($"Getting diameter definition parts failed for {findDescriptionPartsFailed} polyline(s).");
                    #endregion

                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("CONVERTZEROLINESTOPOINTS")]
        public void convertzerolinestopoints()
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
                    BlockTable acBlkTbl;
                    acBlkTbl = tx.GetObject(localDb.BlockTableId,
                                                 OpenMode.ForRead) as BlockTable;

                    // Open the Block table record Model space for write
                    BlockTableRecord acBlkTblRec;
                    acBlkTblRec = tx.GetObject(acBlkTbl[BlockTableRecord.ModelSpace],
                                                    OpenMode.ForWrite) as BlockTableRecord;

                    HashSet<Line> lines = localDb.HashSetOfType<Line>(tx);

                    foreach (Line line in lines)
                    {
                        if (line.Length < 0.000001)
                        {
                            Point3d point = line.StartPoint;

                            DBPoint acPoint = new DBPoint(point);
                            acPoint.SetDatabaseDefaults();
                            acPoint.Layer = line.Layer;

                            // Add the new object to the block table record and the transaction
                            acBlkTblRec.AppendEntity(acPoint);
                            tx.AddNewlyCreatedDBObject(acPoint, true);

                            line.UpgradeOpen();
                            line.Erase(true);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("FIXGASLAYERSLINES")]
        public void fixgaslayerslines()
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
                    BlockTable acBlkTbl;
                    acBlkTbl = tx.GetObject(localDb.BlockTableId,
                                                 OpenMode.ForRead) as BlockTable;

                    // Open the Block table record Model space for write
                    BlockTableRecord acBlkTblRec;
                    acBlkTblRec = tx.GetObject(acBlkTbl[BlockTableRecord.ModelSpace],
                                                    OpenMode.ForWrite) as BlockTableRecord;

                    HashSet<Line> lines = localDb.HashSetOfType<Line>(tx);
                    HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx);
                    HashSet<Polyline3d> plines3d = localDb.HashSetOfType<Polyline3d>(tx);
                    HashSet<Spline> splines = localDb.HashSetOfType<Spline>(tx);

                    HashSet<Entity> ents = new HashSet<Entity>();
                    ents.UnionWith(lines);
                    ents.UnionWith(plines);
                    ents.UnionWith(plines3d);
                    ents.UnionWith(splines);

                    foreach (Entity ent in ents)
                        fix(ent);

                    void fix(Entity ent)
                    {
                        ent.CheckOrOpenForWrite();
                        ent.ColorIndex = 256;
                        ent.DowngradeOpen();
                    }

                    List<string> layerNames = new List<string>()
                    {
                        "GAS-Distributionsrør",
                        "GAS-Distributionsrør-2D",
                        "GAS-Fordelingsrør",
                        "GAS-Fordelingsrør-2D",
                        "GAS-Stikrør",
                        "GAS-Stikrør-2D"
                    };

                    LayerTable lt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;

                    foreach (string name in layerNames)
                    {
                        if (!lt.Has(name)) continue;
                        LayerTableRecord ltr = lt.GetLayerByName(name);
                        ltr.CheckOrOpenForWrite();
                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 30);
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("GASFINDLABELS")]
        public void gasfindlabels()
        {

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Create layers
                    List<string> layerNames = new List<string>()
                    {   "GAS-Stikrør",
                        "GAS-Stikrør-2D",
                        "GAS-Fordelingsrør",
                        "GAS-Fordelingsrør-2D",
                        "GAS-Distributionsrør",
                        "GAS-Distributionsrør-2D",
                        "GAS-ude af drift",
                        "GAS-ude af drift-2D" };
                    LayerTable lt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;
                    foreach (string name in layerNames)
                    {
                        if (!lt.Has(name))
                        {
                            LayerTableRecord ltr = new LayerTableRecord();
                            ltr.Name = name;
                            if (name == "GAS-ude af drift-2D" ||
                                name == "GAS-ude af drift") ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 221);
                            else ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 30);

                            //Make layertable writable
                            lt.CheckOrOpenForWrite();

                            //Add the new layer to layer table
                            Oid ltId = lt.Add(ltr);
                            tx.AddNewlyCreatedDBObject(ltr, true);
                        }
                    }
                    #endregion

                    #region Prepare OdTable for gas
                    CheckOrCreateTable(
                        tables,
                        OdTables.Gas.GetTableName(),
                        OdTables.Gas.GetTableDescription(),
                        OdTables.Gas.GetColumnNames(),
                        OdTables.Gas.GetColumnDescriptions(),
                        OdTables.Gas.GetDataTypes());
                    #endregion

                    #region GatherObjects
                    HashSet<Entity> allEnts = localDb.HashSetOfType<Entity>(tx);

                    HashSet<Entity> entsPIPE = allEnts.Where(x => x.Layer == "PIPE").ToHashSet();
                    HashSet<Entity> entsLABEL = allEnts.Where(x => x.Layer == "LABEL").ToHashSet();
                    #endregion

                    #region Cache labels in memory
                    //Prepare label objects so we don't access OD all the time
                    //Accessing OD is very slow
                    HashSet<(int G3eFid, string Label)> allLabels = new HashSet<(int G3eFid, string Label)>();

                    foreach (Entity ent in entsLABEL)
                    {
                        string label = ReadStringPropertyValue(
                            tables, ent.Id, "LABEL", "LABEL");

                        ////Filter out unwanted values
                        if (DataQa.Gas.ForbiddenValues.Contains(label.ToUpper())) continue;

                        //Modify labels with excess data
                        if (DataQa.Gas.ReplaceLabelParts.ContainsKey(label.ToUpper()))
                            label = DataQa.Gas.ReplaceLabelParts[label.ToUpper()];

                        allLabels.Add((
                            Convert.ToInt32(ReadDoublePropertyValue(tables, ent.Id, "LABEL", "G3E_FID")),
                            label));
                    }
                    #endregion

                    #region Find and write found labels if any
                    //Iterate pipe objects
                    foreach (Entity PIPE in entsPIPE)
                    {
                        #region Move pipe to correct layer
                        //Move pipe to correct layer
                        int FNO = ReadIntPropertyValue(
                            tables,
                            PIPE.Id,
                            "PIPE",
                            "G3E_FNO");

                        PIPE.CheckOrOpenForWrite();
                        if (FNO == 113) PIPE.Layer = "GAS-Stikrør";
                        else if (FNO == 112) PIPE.Layer = "GAS-Distributionsrør";
                        else if (FNO == 111) PIPE.Layer = "GAS-Fordelingsrør";
                        #endregion

                        //Try to find corresponding label entity
                        int G3EFID = Convert.ToInt32(
                            ReadDoublePropertyValue(
                            tables,
                            PIPE.Id,
                            "PIPE",
                            "G3E_FID"));

                        var matches = allLabels.Where(x => x.G3eFid == G3EFID);

                        //If no matches proceed to next element
                        if (matches.Count() < 1) continue;

                        var match = matches.FirstOrDefault();
                        if (match == default) continue;

                        int parsedInt = 0;
                        string parsedMat = string.Empty;
                        if (match.Label.Contains(" "))
                        {
                            //Gas specific handling
                            string[] output = match.Label.Split((char[])null); //Splits by whitespace

                            int.TryParse(output[0], out parsedInt);
                            //Material
                            parsedMat = output[1];
                        }
                        else
                        {
                            string[] output = match.Label.Split('/');
                            string a = ""; //For number
                            string b = ""; //For material

                            for (int i = 0; i < output[0].Length; i++)
                            {
                                if (Char.IsDigit(output[0][i])) a += output[0][i];
                                else b += output[0][i];
                            }

                            int.TryParse(a, out parsedInt);
                            parsedMat = b;
                        }

                        //prdDbg(parsedInt.ToString() + " - " + parsedMat);

                        //Aggregate
                        MapValue[] values = new MapValue[3];
                        values[0] = new MapValue(parsedInt);
                        values[1] = new MapValue(parsedMat);
                        values[2] = new MapValue("");
                        //if (ledningIbrug) values[2] = new MapValue("");
                        //else values[2] = new MapValue("Ikke i brug");

                        //Length - 1 because we don't need to update the last record
                        for (int i = 0; i < OdTables.Gas.GetColumnNames().Length - 1; i++)
                        {
                            bool success = CheckAddUpdateRecordValue(
                                tables,
                                PIPE.Id,
                                OdTables.Gas.GetTableName(),
                                OdTables.Gas.GetColumnNames()[i],
                                values[i]);

                            if (success)
                            {
                                //editor.WriteMessage($"\nUpdating color and layer properties!");
                                //Entity ent = pline3dId.Go<Entity>(tx, OpenMode.ForWrite);

                                //if (ledningIbrug) ent.ColorIndex = 1;
                                //else { ent.Layer = "GAS-ude af drift"; ent.ColorIndex = 130; }
                                PIPE.ColorIndex = 1;
                            }
                        }
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("GASQADATA")]
        public void gasqadata()
        {

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region GatherObjects
                    HashSet<Entity> allEnts = localDb.HashSetOfType<Entity>(tx);

                    HashSet<Entity> entsPIPE = allEnts.Where(x => x.Layer == "PIPE").ToHashSet();
                    HashSet<Entity> entsLABEL = allEnts.Where(x => x.Layer == "LABEL").ToHashSet();
                    #endregion

                    #region QA data
                    //Find how many times multiple groups occur
                    var groups = entsLABEL.GroupBy(x => Convert.ToInt32(ReadDoublePropertyValue(
                        tables, x.Id, "LABEL", "G3E_FID")));
                    List<int> counts = groups.Select(x => x.Count()).Distinct().OrderBy(x => x).ToList();

                    foreach (int count in counts)
                    {
                        prdDbg(count.ToString() + " - " + groups.Where(x => x.Count() == count).Count().ToString());
                    }

                    //List values in multiple match groups
                    int[] countsArray = counts.ToArray();
                    for (int i = 0; i < countsArray.Length; i++)
                    {
                        //Skip groups with one match
                        if (i == 0) continue;

                        prdDbg($"\nValues for groups of {countsArray[i]}: ");
                        var isolatedGroups = groups.Where(x => x.Count() == countsArray[i]);
                        foreach (var group in isolatedGroups)
                        {
                            string values = string.Join(" ",
                                group.Select(
                                    x => ReadStringPropertyValue(
                                        tables, x.Id, "LABEL", "LABEL")));
                            prdDbg(values);
                        }
                    }

                    //Insert blank line to separate output
                    prdDbg("");

                    //List all unique LABEL values
                    HashSet<string> allLabels = new HashSet<string>();
                    foreach (Entity ent in entsLABEL)
                    {
                        allLabels.Add(ReadStringPropertyValue(tables, ent.Id, "LABEL", "LABEL"));
                    }
                    var ordered = allLabels.OrderBy(x => x);
                    foreach (string value in ordered)
                    {
                        string label = value.ToUpper();
                        //Check if value is handled by filters
                        if (DataQa.Gas.ForbiddenValues.Contains(label))
                            label += " <------*";
                        if (DataQa.Gas.ReplaceLabelParts.ContainsKey(label))
                            label += " <------*";
                        prdDbg(label);
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        //Used to move 3d polylines of gas to elevation points
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
                            DBPoint match = points.Where(x => x.Position.HorizontalEqualz(
                                vertices[i].Position, 0.05)).FirstOrDefault();
                            if (match != null)
                            {
                                vertices[i].UpgradeOpen();
                                vertices[i].Position = new Point3d(
                                    vertices[i].Position.X, vertices[i].Position.Y, match.Position.Z);
                                vertices[i].DowngradeOpen();
                            }
                        }
                    }

                    #region Second pass looking at end points
                    //But only for vertices still at 0

                    HashSet<Point3d> allEnds = new HashSet<Point3d>(new Point3dHorizontalComparer());

                    foreach (Polyline3d pline3d in localPlines3d)
                    {
                        var vertices = pline3d.GetVertices(tx);
                        int end = vertices.Length - 1;
                        allEnds.Add(new Point3d(
                            vertices[0].Position.X, vertices[0].Position.Y, vertices[0].Position.Z));
                        allEnds.Add(new Point3d(
                            vertices[end].Position.X, vertices[end].Position.Y, vertices[end].Position.Z));
                    }

                    foreach (Polyline3d pline3d in localPlines3d)
                    {
                        var vertices = pline3d.GetVertices(tx);

                        for (int i = 0; i < vertices.Length; i++)
                        {
                            //Only change vertices still at zero
                            if (vertices[i].Position.Z > 0.1) continue;

                            Point3d match = allEnds.Where(x => x.HorizontalEqualz(
                                vertices[i].Position, 0.05)).FirstOrDefault();
                            if (match != null)
                            {
                                vertices[i].UpgradeOpen();
                                vertices[i].Position = new Point3d(
                                    vertices[i].Position.X, vertices[i].Position.Y, match.Z);
                                vertices[i].DowngradeOpen();
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

        [CommandMethod("FIXNOVAFOSLAYERSLINES")]
        public void fixnovafoslayerslines()
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
                    LayerTable lt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;

                    if (lt.Has("AFL_ikke_ibrug"))
                    {
                        LayerTableRecord ltr = lt.GetLayerByName("AFL_ikke_ibrug");
                        ltr.CheckOrOpenForWrite();
                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 92);

                        LinetypeTable ltt = tx.GetObject(localDb.LinetypeTableId, OpenMode.ForRead)
                            as LinetypeTable;
                        Oid lineTypeId = Oid.Null;
                        if (ltt.Has("DASHED2"))
                        {
                            lineTypeId = ltt["DASHED2"];
                            ltr.LinetypeObjectId = lineTypeId;
                        }
                        else prdDbg("\nLine type \"DASHED2\" is missing!");
                    }

                    if (lt.Has("AFL_ledning_draen"))
                    {
                        LayerTableRecord ltr = lt.GetLayerByName("AFL_ledning_draen");
                        ltr.CheckOrOpenForWrite();
                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 92);
                    }

                    if (lt.Has("AFL_ledning_faelles"))
                    {
                        LayerTableRecord ltr = lt.GetLayerByName("AFL_ledning_faelles");
                        ltr.CheckOrOpenForWrite();
                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 140);
                    }

                    if (lt.Has("AFL_ledning_regn"))
                    {
                        LayerTableRecord ltr = lt.GetLayerByName("AFL_ledning_regn");
                        ltr.CheckOrOpenForWrite();
                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 191);
                    }

                    if (lt.Has("AFL_ledning_spild"))
                    {
                        LayerTableRecord ltr = lt.GetLayerByName("AFL_ledning_spild");
                        ltr.CheckOrOpenForWrite();
                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 140);
                    }

                    if (lt.Has("VAND_ledning"))
                    {
                        LayerTableRecord ltr = lt.GetLayerByName("VAND_ledning");
                        ltr.CheckOrOpenForWrite();
                        //ltr.Color = Color.FromRgb(0, 0, 255);
                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 5);
                    }

                    if (lt.Has("VAND_ledning_ikke_i_brug"))
                    {
                        LayerTableRecord ltr = lt.GetLayerByName("VAND_ledning_ikke_i_brug");
                        ltr.CheckOrOpenForWrite();
                        ltr.Color = Color.FromRgb(0, 115, 230);
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("P3DRESETVERTICESEXCEPTENDS")]
        public void p3dresetverticesexceptends()
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
                    #region Select pline3d
                    PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                        "\nSelect polyline3d to reset: ");
                    promptEntityOptions1.SetRejectMessage("\n Not a polyline3d!");
                    promptEntityOptions1.AddAllowedClass(typeof(Polyline3d), true);
                    PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    if (((PromptResult)entity1).Status != PromptStatus.OK) { tx.Abort(); return; }
                    Autodesk.AutoCAD.DatabaseServices.ObjectId pline3dId = entity1.ObjectId;
                    #endregion

                    #region Process vertices
                    Polyline3d p3d = pline3dId.Go<Polyline3d>(tx, OpenMode.ForWrite);
                    PolylineVertex3d[] vertices = p3d.GetVertices(tx);

                    //i=1 and Length-1 to skip first and last
                    for (int i = 1; i < vertices.Length - 1; i++)
                    {
                        vertices[i].CheckOrOpenForWrite();
                        vertices[i].Position = new Point3d(
                            vertices[i].Position.X, vertices[i].Position.Y, 0);
                    }

                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("P3DINTERPOLATEBETWEENISLANDS")]
        public void p3dinterpolatebetweenislands()
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
                    #region Select pline3d
                    PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                        "\nSelect polyline3d to interpolate: ");
                    promptEntityOptions1.SetRejectMessage("\n Not a polyline3d!");
                    promptEntityOptions1.AddAllowedClass(typeof(Polyline3d), true);
                    PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    if (((PromptResult)entity1).Status != PromptStatus.OK) { tx.Abort(); return; }
                    Autodesk.AutoCAD.DatabaseServices.ObjectId pline3dId = entity1.ObjectId;
                    #endregion

                    //Currently assumes that start and end vertices are at elevation

                    #region Process vertices
                    Polyline3d p3d = pline3dId.Go<Polyline3d>(tx, OpenMode.ForWrite);
                    PolylineVertex3d[] vertices = p3d.GetVertices(tx);

                    List<int> islands = new List<int>();
                    for (int i = 0; i < vertices.Length; i++)
                        if (vertices[i].Position.Z > 0.1)
                            islands.Add(i);

                    //int endIdx = vertices.Length - 1;

                    for (int i = 0; i < islands.Count; i++)
                    {
                        //Stop before last idx to avoid out of bounds
                        if (i == islands.Count - 1) break;
                        int startIdx = islands[i];
                        int endIdx = islands[i + 1];
                        //check if islands are next to each other
                        if (endIdx - startIdx == 1) continue;

                        //Interpolation
                        double startElevation = vertices[startIdx].Position.Z;
                        double endElevation = vertices[endIdx].Position.Z;
                        double AB = p3d.GetHorizontalLengthBetweenIdxs(startIdx, endIdx);
                        prdDbg(AB.ToString());
                        double AAmark = startElevation - endElevation;
                        double PB = 0;
                        for (int j = startIdx; j < endIdx + 1; j++)
                        {
                            //Skip first and last vertici
                            if (j == startIdx || j == endIdx) continue;

                            PB += vertices[j - 1].Position.DistanceHorizontalTo(
                                                 vertices[j].Position);

                            double newElevation = startElevation - PB * (AAmark / AB);
                            vertices[j].CheckOrOpenForWrite();
                            vertices[j].Position = new Point3d(
                                vertices[j].Position.X, vertices[j].Position.Y, newElevation);
                        }
                    }

                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("GASBEHANDLING")]
        public void gasbehandling()
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
                    #region Lines
                    //double lineThreshold = 1;
                    //HashSet<Line> lines = localDb.HashSetOfType<Line>(tx, true);
                    //foreach (Line line in lines)
                    //{
                    //    if (line.StartPoint.Z < lineThreshold)
                    //    {
                    //        Point3d point = line.EndPoint;

                    //        line.UpgradeOpen();

                    //        line.StartPoint = point;
                    //    }
                    //    else if (line.EndPoint.Z < lineThreshold)
                    //    {
                    //        Point3d point = line.StartPoint;

                    //        line.UpgradeOpen();

                    //        line.EndPoint = point;
                    //    }
                    //}
                    #endregion

                    #region Polylines 3d
                    /////////////////////////////////
                    //Must not overlap
                    //correctionThreshold operates on values LESS THAN value
                    //targetThreshold operates on values GREATER THAN value
                    double correctionThreshold = 1;
                    double targetThreshold = 2;
                    ////////////////////////////////
                    HashSet<Polyline3d> plines3d = localDb.HashSetOfType<Polyline3d>(tx, true);
                    foreach (Polyline3d p3d in plines3d)
                    {
                        PolylineVertex3d[] vertices = p3d.GetVertices(tx);
                        int endIdx = vertices.Length - 1;

                        for (int i = 0; i < vertices.Length; i++)
                        {
                            if (i == 0)
                            {
                                //First vertice case
                                if (vertices[i].Position.Z < correctionThreshold)
                                {
                                    bool elevationUnknown = true;
                                    Point3d pos = new Point3d();
                                    int j = 0;
                                    do
                                    {
                                        if (vertices[j].Position.Z > targetThreshold)
                                        {
                                            elevationUnknown = false;
                                            pos = vertices[j].Position;
                                        }
                                        j++;
                                        //Catch out of bounds
                                        if (j > endIdx) break;
                                    } while (elevationUnknown);

                                    vertices[i].CheckOrOpenForWrite();
                                    vertices[i].Position = new Point3d(
                                        vertices[i].Position.X, vertices[i].Position.Y, pos.Z);
                                }
                            }
                            else if (i == endIdx)
                            {
                                //Last vertice case
                                if (vertices[i].Position.Z < correctionThreshold)
                                {
                                    bool elevationUnknown = true;
                                    Point3d pos = new Point3d();
                                    int j = endIdx;
                                    do
                                    {
                                        if (vertices[j].Position.Z > targetThreshold)
                                        {
                                            elevationUnknown = false;
                                            pos = vertices[j].Position;
                                        }
                                        j--;
                                        //Catch out of bounds
                                        if (j < 0) break;
                                    } while (elevationUnknown);

                                    vertices[i].CheckOrOpenForWrite();
                                    vertices[i].Position = new Point3d(
                                        vertices[i].Position.X, vertices[i].Position.Y, pos.Z);
                                }
                            }
                            else
                            {
                                //Intermediary vertex case
                                if (vertices[i].Position.Z < correctionThreshold)
                                {
                                    bool forwardElevationUnknown = true;
                                    bool backwardElevationUnknown = true;

                                    Point3d forwardPos = new Point3d();
                                    Point3d backwardPos = new Point3d();

                                    int forwardIdx = i;
                                    int backwardIdx = i;

                                    do //Forward detection
                                    {
                                        if (vertices[forwardIdx].Position.Z > targetThreshold)
                                        {
                                            forwardElevationUnknown = false;
                                            forwardPos = vertices[forwardIdx].Position;
                                        }
                                        forwardIdx++;
                                        //Catch out of bounds
                                        if (forwardIdx > endIdx) break;
                                    } while (forwardElevationUnknown);

                                    do //Backward detection
                                    {
                                        if (vertices[backwardIdx].Position.Z > targetThreshold)
                                        {
                                            backwardElevationUnknown = false;
                                            backwardPos = vertices[backwardIdx].Position;
                                        }
                                        backwardIdx--;
                                        //Catch out of bounds
                                        if (backwardIdx < 0) break;
                                    } while (backwardElevationUnknown);

                                    if (!backwardElevationUnknown && !forwardElevationUnknown)
                                    {
                                        double calculatedZ = 0;
                                        double delta = Math.Abs(backwardPos.Z - forwardPos.Z) / 2;
                                        if (backwardPos.Z < forwardPos.Z) calculatedZ = backwardPos.Z + delta;
                                        else if (backwardPos.Z > forwardPos.Z) calculatedZ = forwardPos.Z + delta;
                                        else if (backwardPos.Z == forwardPos.Z) calculatedZ = backwardPos.Z;

                                        vertices[i].CheckOrOpenForWrite();
                                        vertices[i].Position = new Point3d(
                                            vertices[i].Position.X, vertices[i].Position.Y,
                                            calculatedZ);
                                    }
                                    else if (!backwardElevationUnknown)
                                    {
                                        vertices[i].CheckOrOpenForWrite();
                                        vertices[i].Position = new Point3d(
                                            vertices[i].Position.X, vertices[i].Position.Y,
                                            backwardPos.Z);
                                    }
                                    else if (!forwardElevationUnknown)
                                    {
                                        vertices[i].CheckOrOpenForWrite();
                                        vertices[i].Position = new Point3d(
                                            vertices[i].Position.X, vertices[i].Position.Y,
                                            forwardPos.Z);
                                    }
                                }
                            }
                        }
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("GASMOVETOELEVATION")]
        public void gasmovetoelevation()
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
                    #region Lines
                    ///////////////////////////
                    double lineThreshold = -1;
                    double targetElevation = 0;
                    ///////////////////////////

                    HashSet<Line> lines = localDb.HashSetOfType<Line>(tx, true);
                    foreach (Line line in lines)
                    {
                        if (line.StartPoint.Z < lineThreshold)
                        {
                            line.CheckOrOpenForWrite();

                            line.StartPoint = new Point3d(
                                line.StartPoint.X, line.StartPoint.Y, targetElevation);
                        }
                        if (line.EndPoint.Z < lineThreshold)
                        {
                            line.CheckOrOpenForWrite();

                            line.EndPoint = new Point3d(
                                line.EndPoint.X, line.EndPoint.Y, targetElevation);
                        }
                    }
                    #endregion

                    #region Polylines2d
                    ////////////////////////
                    double plineThreshold = -1;
                    //Reuse target elevation from above
                    ////////////////////////

                    HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx, true);
                    foreach (Polyline pline in plines)
                    {
                        if (pline.Elevation < plineThreshold)
                        {
                            pline.CheckOrOpenForWrite();
                            pline.Elevation = targetElevation;
                        }
                    }


                    #endregion

                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("GASCHANGELAYERFOR2D")]
        public void gaschangelayerfor2d()
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
                    #region Change layer
                    ///////////////////////////
                    double moveThreshold = 1;
                    ///////////////////////////

                    #region Create layers
                    List<string> layerNames = new List<string>()
                    {   "GAS-Stikrør-2D",
                        "GAS-Fordelingsrør-2D",
                        "GAS-Distributionsrør-2D",
                        "GAS-ude af drift-2D" };
                    LayerTable lt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;
                    foreach (string name in layerNames)
                    {
                        if (!lt.Has(name))
                        {
                            LayerTableRecord ltr = new LayerTableRecord();
                            ltr.Name = name;
                            if (name != "GAS-ude af drift-2D") ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 30);
                            else ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 221);

                            //Make layertable writable
                            lt.CheckOrOpenForWrite();

                            //Add the new layer to layer table
                            Oid ltId = lt.Add(ltr);
                            tx.AddNewlyCreatedDBObject(ltr, true);
                        }
                    }
                    #endregion

                    #region Rename layers
                    List<string> layersToRename = new List<string>()
                    {
                        "Distributionsrør",
                        "Fordelingsrør",
                        "Stikrør"
                    };

                    foreach (string name in layersToRename)
                    {
                        if (lt.Has(name))
                        {
                            LayerTableRecord layer = lt.GetLayerByName(name);
                            layer.CheckOrOpenForWrite();
                            layer.Name = $"GAS-{name}";
                            layer.Color = Color.FromColorIndex(ColorMethod.ByAci, 30);
                            layer.LineWeight = LineWeight.ByLineWeightDefault;
                        }
                    }
                    #endregion

                    HashSet<Polyline3d> lines = localDb.HashSetOfType<Polyline3d>(tx, true);
                    foreach (Polyline3d line in lines)
                    {
                        bool didNotFindAboveThreshold = true;
                        PolylineVertex3d[] vertices = line.GetVertices(tx);
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            if (vertices[i].Position.Z > 1) didNotFindAboveThreshold = false;
                        }

                        if (didNotFindAboveThreshold)
                        {
                            line.CheckOrOpenForWrite();
                            if (line.Layer == "Distributionsrør" ||
                                line.Layer == "GAS-Distributionsrør") line.Layer = "GAS-Distributionsrør-2D";
                            else if (line.Layer == "Stikrør" ||
                                     line.Layer == "GAS-Stikrør") line.Layer = "GAS-Stikrør-2D";
                            else if (line.Layer == "Fordelingsrør" ||
                                     line.Layer == "GAS-Fordelingsrør") line.Layer = "GAS-Fordelingsrør-2D";
                            else if (line.Layer == "GAS-ude af drift") line.Layer = "GAS-ude af drift-2D";
                        }
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("DELETEALLALIGNMENTS")]
        public void deleteallalignments()
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
                    HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);
                    editor.WriteMessage($"\nNr. of alignments: {als.Count}");
                    foreach (Alignment al in als)
                    {
                        al.CheckOrOpenForWrite();
                        al.Erase(true);
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("LABELALLALIGNMENTS")]
        public void labelallalignments()
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
                    HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);
                    editor.WriteMessage($"\nNr. of alignments: {als.Count}");
                    foreach (Alignment al in als)
                    {
                        al.CheckOrOpenForWrite();
                        al.ImportLabelSet("STD 20-5");
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("LABELALLALIGNMENTSNAME")]
        public void labelallalignmentsname()
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
                    HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);
                    editor.WriteMessage($"\nNr. of alignments: {als.Count}");
                    foreach (Alignment al in als)
                    {
                        al.CheckOrOpenForWrite();
                        al.ImportLabelSet("20-5 - Name");
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        //[CommandMethod("EXPLODENESTEDBLOCKS", CommandFlags.UsePickSet)]
        public void ExplodeNestedBlock()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            //// Ask the user to select the block
            //var peo = new PromptEntityOptions("\nSelect block to explode");
            //peo.SetRejectMessage("Must be a block.");
            //peo.AddAllowedClass(typeof(BlockReference), false);
            //var per = ed.GetEntity(peo);
            //if (per.Status != PromptStatus.OK)
            //    return;

            // Get the PickFirst selection set
            PromptSelectionResult acSSPrompt;
            acSSPrompt = ed.SelectImplied();
            SelectionSet acSSet;
            // If the prompt status is OK, objects were selected before
            // the command was started
            if (acSSPrompt.Status == PromptStatus.OK)
            {
                acSSet = acSSPrompt.Value;
                var Ids = acSSet.GetObjectIds();
                foreach (Oid oid in Ids)
                {
                    if (oid.ObjectClass.Name != "AcDbBlockReference") continue;

                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            // Call our explode function recursively, starting
                            // with the top-level block reference
                            // (you can pass false as a 4th parameter if you
                            // don't want originating entities erased)
                            ExplodeBlock(tr, db, oid, true);
                        }
                        catch (System.Exception ex)
                        {
                            tr.Abort();
                            ed.WriteMessage($"\n{ex.Message}");
                            continue;
                        }
                        tr.Commit();
                    }
                }
            }

            void ExplodeBlock(Transaction tr, Database localDb, ObjectId id, bool topLevelCall = false)
            {
                // Open out block reference - only needs to be readable
                // for the explode operation, as it's non-destructive
                var br = (BlockReference)tr.GetObject(id, OpenMode.ForRead);
                if (br.Name.Contains("MuffeIntern") ||
                    br.Name.StartsWith("*U")) return;

                // We'll collect the BlockReferences created in a collection
                var toExplode = new ObjectIdCollection();

                // Define our handler to capture the nested block references
                ObjectEventHandler handler =
                  (s, e) =>
                  { //if (e.DBObject is BlockReference)
                      toExplode.Add(e.DBObject.ObjectId);
                  };

                // Add our handler around the explode call, removing it
                // directly afterwards
                localDb.ObjectAppended += handler;
                br.ExplodeToOwnerSpace();
                localDb.ObjectAppended -= handler;

                // Go through the results and recurse, exploding the
                // contents
                foreach (ObjectId bid in toExplode)
                {
                    if (bid.ObjectClass.Name != "AcDbBlockReference") continue;
                    ExplodeBlock(tr, localDb, bid, false);
                }

                //Clean stuff emitted to ModelSpace by Top Level Call
                if (topLevelCall)
                {
                    foreach (Oid oid in toExplode)
                    {
                        if (oid.ObjectClass.Name == "AcDbBlockReference") continue;
                        Autodesk.AutoCAD.DatabaseServices.DBObject dbObj =
                            tr.GetObject(oid, OpenMode.ForRead) as Autodesk.AutoCAD.DatabaseServices.DBObject;

                        dbObj.Erase(true);
                        dbObj.DowngradeOpen();
                    }
                }

                // We might also just let it drop out of scope
                toExplode.Clear();

                // To replicate the explode command, we're delete the
                // original entity
                if (topLevelCall == false)
                {
                    ed.WriteMessage($"\nExploding block: {br.Name}");
                    br.UpgradeOpen();
                    br.Erase();
                    br.DowngradeOpen();
                }
            }
        }

        [CommandMethod("EXPLODENESTEDBLOCKS", CommandFlags.UsePickSet)]
        public static void explodenestedblocks2()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                //PromptEntityOptions prEntOpt = new PromptEntityOptions("\nSelect an INSERT:");
                //prEntOpt.SetRejectMessage("\nIt is not an INSERT!");
                //prEntOpt.AddAllowedClass(typeof(BlockReference), true);
                //PromptEntityResult selRes = ed.GetEntity(prEntOpt);

                // Get the PickFirst selection set
                PromptSelectionResult acSSPrompt;
                acSSPrompt = ed.SelectImplied();
                SelectionSet acSSet;
                // If the prompt status is OK, objects were selected before
                // the command was started

                if (acSSPrompt.Status == PromptStatus.OK)
                {
                    acSSet = acSSPrompt.Value;
                    var Ids = acSSet.GetObjectIds();
                    foreach (Oid oid in Ids)
                    {
                        if (oid.ObjectClass.Name != "AcDbBlockReference") continue;
                        //prdDbg("1: " + oid.ObjectClass.Name);

                        using (Transaction tx = localDb.TransactionManager.StartTransaction())
                        {

                            try
                            {
                                BlockReference br = oid.Go<BlockReference>(tx, OpenMode.ForWrite);
                                BlockTableRecord btr = tx.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;

                                foreach (Oid bOid in btr)
                                {
                                    if (bOid.ObjectClass.Name != "AcDbBlockReference") continue;
                                    //prdDbg("2: " + bOid.ObjectClass.Name);

                                    ObjectIdCollection ids = Extensions.ExplodeToOwnerSpace3(bOid);
                                    if (ids.Count > 0)
                                        ed.WriteMessage("\n{0} entities were added into database.", ids.Count);
                                }
                            }
                            catch (System.Exception ex)
                            {
                                tx.Abort();
                                prdDbg("3: " + ex.Message);
                                continue;
                            }
                            tx.Commit();
                        }
                    }
                }
                else
                {
                    ed.WriteMessage("\nSelect before running the command!");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage(ex.Message);
            }
        }

        [CommandMethod("LISTALLNESTEDBLOCKS", CommandFlags.UsePickSet)]
        public static void listallnestedblocks()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                //PromptEntityOptions prEntOpt = new PromptEntityOptions("\nSelect an INSERT:");
                //prEntOpt.SetRejectMessage("\nIt is not an INSERT!");
                //prEntOpt.AddAllowedClass(typeof(BlockReference), true);
                //PromptEntityResult selRes = ed.GetEntity(prEntOpt);

                // Get the PickFirst selection set
                PromptSelectionResult acSSPrompt;
                acSSPrompt = ed.SelectImplied();
                SelectionSet acSSet;
                // If the prompt status is OK, objects were selected before
                // the command was started

                if (acSSPrompt.Status == PromptStatus.OK)
                {
                    acSSet = acSSPrompt.Value;
                    var Ids = acSSet.GetObjectIds();
                    foreach (Oid oid in Ids)
                    {
                        if (oid.ObjectClass.Name != "AcDbBlockReference") continue;
                        using (Transaction tx = localDb.TransactionManager.StartTransaction())
                        {
                            try
                            {
                                BlockReference br = oid.Go<BlockReference>(tx, OpenMode.ForWrite);
                                BlockTableRecord btr = tx.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                                prdDbg("Top LEVEL: " + br.Name);

                                foreach (Oid bOid in btr)
                                {
                                    if (bOid.ObjectClass.Name != "AcDbBlockReference") continue;
                                    WriteNestedBlocksName(bOid.Go<BlockReference>(tx));
                                }
                            }
                            catch (System.Exception ex)
                            {
                                tx.Abort();
                                prdDbg("3: " + ex.Message);
                                continue;
                            }
                            tx.Commit();
                        }
                    }
                }
                else
                {
                    ed.WriteMessage("\nSelect before running command!");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage(ex.Message);
            }

            void WriteNestedBlocksName(BlockReference brNested)
            {
                Transaction txTop = Application.DocumentManager.MdiActiveDocument.TransactionManager.TopTransaction;
                if (brNested.Name != "MuffeIntern" &&
                    brNested.Name != "MuffeIntern2" &&
                    brNested.Name != "MuffeIntern3")
                {
                    string effectiveName = brNested.IsDynamicBlock ?
                            brNested.Name + " *-> " + ((BlockTableRecord)txTop.GetObject(
                            brNested.DynamicBlockTableRecord, OpenMode.ForRead)).Name : brNested.Name;
                    Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\n--> {effectiveName}");
                }

                BlockTableRecord btrNested = txTop.GetObject(brNested.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                foreach (Oid OidNested in btrNested)
                {
                    if (OidNested.ObjectClass.Name != "AcDbBlockReference") continue;
                    WriteNestedBlocksName(OidNested.Go<BlockReference>(txTop));
                }
            }
        }

        [CommandMethod("CONVERTTEXTOUTPUT")]
        public void converttextoutput()
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
                    string[] input = File.ReadAllLines(@"X:\0371-1158 - Gentofte Fase 4 - Dokumenter\01 Intern\02 Tegninger" +
                                                       @"\01 Autocad\Autocad\01 Views\4.6\Komponenter\Input.txt");

                    List<SizeManager> list = new List<SizeManager>();
                    foreach (string s in input)
                    {
                        list.Add(new SizeManager(s));
                    }

                    foreach (SizeManager sm in list)
                    {
                        prdDbg(sm.FirstPosition);
                    }

                    prdDbg("---------------------------------------");

                    foreach (SizeManager sm in list)
                    {
                        prdDbg(sm.SecondPosition);
                    }

                    //ClrFile(@"X:\0371-1158 - Gentofte Fase 4 - Dokumenter\01 Intern\02 Tegninger\" +
                    //        @"01 Autocad\Autocad\01 Views\4.5\Komponenter\Komponenter 4.5.csv");

                    //OutputWriter(@"X:\0371-1158 - Gentofte Fase 4 - Dokumenter\01 Intern\02 Tegninger\" +
                    //             @"01 Autocad\Autocad\01 Views\4.5\Komponenter\Komponenter 4.5.csv", sb.ToString());


                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("COLORIZEALLLERLAYERS")]
        public void colorizealllerlayers(Database extDb = null)
        {

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            Database selectedDB = extDb ?? localDb;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";
                    System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");

                    LayerTable lt = selectedDB.LayerTableId.Go<LayerTable>(selectedDB.TransactionManager.TopTransaction);

                    Regex regex = new Regex(@"^(?<R>\d+)\*(?<G>\d+)\*(?<B>\d+)");

                    HashSet<string> layerNames = dtKrydsninger.AsEnumerable().Select(x => x["Layer"].ToString()).ToHashSet();

                    foreach (string name in layerNames.Where(x => x.IsNotNoE()).OrderBy(x => x))
                    {
                        if (lt.Has(name))
                        {
                            string colorString = ReadStringParameterFromDataTable(name, dtKrydsninger, "Farve", 1);
                            if (colorString.IsNotNoE() && regex.IsMatch(colorString))
                            {
                                Match match = regex.Match(colorString);
                                byte R = Convert.ToByte(int.Parse(match.Groups["R"].Value));
                                byte G = Convert.ToByte(int.Parse(match.Groups["G"].Value));
                                byte B = Convert.ToByte(int.Parse(match.Groups["B"].Value));
                                prdDbg($"Set layer {name} to color: R: {R.ToString()}, G: {G.ToString()}, B: {B.ToString()}");
                                LayerTableRecord ltr = lt[name].Go<LayerTableRecord>(selectedDB.TransactionManager.TopTransaction, OpenMode.ForWrite);
                                ltr.Color = Color.FromRgb(R, G, B);
                            }
                            else prdDbg("No match!");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("UPDATEALLBLOCKS")]
        //Does not update dynamic blocks
        public static void updateallblocks()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                using (Transaction tx = localDb.TransactionManager.StartTransaction())
                using (Database symbolerDB = new Database(false, true))
                {
                    try
                    {
                        System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(
                                            @"X:\AutoCAD DRI - 01 Civil 3D\FJV Komponenter.csv", "FjvKomponenter");

                        symbolerDB.ReadDwgFile(@"X:\0371-1158 - Gentofte Fase 4 - Dokumenter\01 Intern\" +
                                               @"02 Tegninger\01 Autocad\Autocad\01 Views\0.0 Fælles\Symboler.dwg",
                                               System.IO.FileShare.Read, true, "");

                        ObjectIdCollection ids = new ObjectIdCollection();

                        BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                        using (Transaction symbTx = symbolerDB.TransactionManager.StartTransaction())
                        {
                            BlockTable symbBt = symbTx.GetObject(symbolerDB.BlockTableId, OpenMode.ForRead) as BlockTable;

                            foreach (Oid oid in bt)
                            {
                                BlockTableRecord btr = tx.GetObject(oid, OpenMode.ForWrite) as BlockTableRecord;

                                if (ReadStringParameterFromDataTable(btr.Name, fjvKomponenter, "Navn", 0) != null &&
                                    bt.Has(btr.Name))
                                {
                                    ids.Add(symbBt[btr.Name]);
                                }
                            }
                            symbTx.Commit();
                        }

                        if (ids.Count != 0)
                        {
                            IdMapping iMap = new IdMapping();
                            localDb.WblockCloneObjects(ids, localDb.BlockTableId, iMap, DuplicateRecordCloning.Replace, false);
                        }

                        foreach (Oid oid in bt)
                        {
                            BlockTableRecord btr = tx.GetObject(oid, OpenMode.ForWrite) as BlockTableRecord;

                            if (ReadStringParameterFromDataTable(btr.Name, fjvKomponenter, "Navn", 0) != null)
                            {
                                //        prdDbg("2");
                                //        localDb.Insert(btr.Name, symbolerDB, true);
                                //        prdDbg("3");
                                foreach (Oid bRefId in btr.GetBlockReferenceIds(false, true))
                                {
                                    //            prdDbg("4");
                                    BlockReference bref = tx.GetObject(bRefId, OpenMode.ForWrite) as BlockReference;
                                    bref.RecordGraphicsModified(true);
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        tx.Abort();
                        ed.WriteMessage(ex.Message);
                        throw;
                    }

                    tx.Commit();
                };
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage(ex.Message);
            }
        }

        [CommandMethod("CREATEGISDATA")]
        public static void creategisdata()
        {
            IntersectUtilities.GisData.creategisdata();
        }

        [CommandMethod("ATTACHAREADATA")]
        //Does not update dynamic blocks
        public static void attachareadata()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;

            try
            {
                try
                {
                    #region Populate area data from string
                    #region OD Table definition
                    string tableNameAreas = "Områder";

                    string[] columnNames = new string[4]
                           {"Vejnavn",
                                "Ejerskab",
                                "Vejklasse",
                                "Belægning"
                           };
                    string[] columnDescrs = new string[4]
                        {"Name of street",
                             "Owner type of street",
                             "Street/road class",
                             "Pavement type"
                        };
                    DataType[] dataTypes = new DataType[4]
                        {DataType.Character,
                             DataType.Character,
                             DataType.Character,
                             DataType.Character
                        };

                    CheckOrCreateTable(tables, tableNameAreas, "Data for områder", columnNames, columnDescrs, dataTypes);
                    #endregion

                    System.Data.DataTable areaDescriptions = CsvReader.ReadCsvToDataTable(
                                        @"X:\0371-1158 - Gentofte Fase 4 - Dokumenter\01 Intern\05 Udbudsmateriale\" +
                                        @"01 Paradigme\04 TBL\Mængder\4.7\FJV-Fremtid 4.7.csv",
                                        "Areas");

                    //Datatable to list of strings
                    List<string> areaNames = (from System.Data.DataRow dr in areaDescriptions.Rows select (string)dr[1]).ToList();

                    foreach (string name in areaNames)
                    {
                        prdDbg(name);
                        #region Select pline
                        PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                            "\nSelect polyline to add data to:");
                        promptEntityOptions1.SetRejectMessage("\nNot a polyline!");
                        promptEntityOptions1.AddAllowedClass(typeof(Polyline), true);
                        PromptEntityResult entity1 = ed.GetEntity(promptEntityOptions1);
                        if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                        Oid plineId = entity1.ObjectId;
                        #endregion

                        string[] split1 = name.Split(new[] { ", " }, StringSplitOptions.None);

                        string ownership = "O";
                        //Handle the ownership dilemma
                        if (split1[0].Contains("(P)"))
                        {
                            split1[0] = split1[0].Split(new[] { " (" }, StringSplitOptions.None)[0];
                            ownership = "P";
                        }

                        string[] data = new string[4] { split1[0], ownership, split1[1], split1[2] };

                        //Test change
                        //if (ownership == "P") prdDbg(data[0] + " " + data[1] + " " + data[2] + " " + data[3]);

                        for (int i = 0; i < data.Length; i++)
                        {
                            if (DoesRecordExist(tables, plineId, tableNameAreas, columnNames[i]))
                            {
                                UpdateODRecord(tables, tableNameAreas, columnNames[i],
                                    plineId, new MapValue(data[i]));
                            }
                            else AddODRecord(tables, tableNameAreas, columnNames[0],
                                    plineId, new MapValue(data[i]));
                        }

                        using (Transaction tx = localDb.TransactionManager.StartTransaction())
                        {
                            Polyline pline = plineId.Go<Polyline>(tx, OpenMode.ForWrite);
                            pline.Layer = "0-OMRÅDER-OK";
                            tx.Commit();
                        }
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage(ex.Message + " \nCheck if OK layer exists!");
                    throw;
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage(ex.Message);
            }
        }

        [CommandMethod("PROCESSALLSHEETS")]
        public void processallsheets()
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
                    #region Dialog box for file list selection and path determination
                    string path = string.Empty;
                    OpenFileDialog dialog = new OpenFileDialog()
                    {
                        Title = "Choose txt file:",
                        DefaultExt = "txt",
                        Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*",
                        FilterIndex = 0
                    };
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        path = dialog.FileName;
                    }
                    else return;

                    List<string> fileList;
                    fileList = File.ReadAllLines(path).ToList();
                    path = Path.GetDirectoryName(path) + "\\";
                    #endregion

                    //!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                    //Project and etape selection object
                    //Comment out if not needed
                    DataReferencesOptions dro = new DataReferencesOptions();

                    foreach (string name in fileList)
                    {
                        prdDbg(name);
                        string fileName = path + name;
                        using (Database extDb = new Database(false, true))
                        {
                            extDb.ReadDwgFile(fileName, System.IO.FileShare.ReadWrite, false, "");
                            using (Transaction extTx = extDb.TransactionManager.StartTransaction())
                            {
                                try
                                {
                                    #region CreateDetailing
                                    createdetailingmethod(dro, extDb);
                                    #endregion
                                    #region Change xref layer
                                    //BlockTable bt = extTx.GetObject(extDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                                    //foreach (oid oid in bt)
                                    //{
                                    //    BlockTableRecord btr = extTx.GetObject(oid, OpenMode.ForWrite) as BlockTableRecord;
                                    //    if (btr.Name.Contains("_alignment"))
                                    //    {
                                    //        var ids = btr.GetBlockReferenceIds(true, true);
                                    //        foreach (oid brId in ids)
                                    //        {
                                    //            BlockReference br = brId.Go<BlockReference>(extTx, OpenMode.ForWrite);
                                    //            prdDbg(br.Name);
                                    //            if (br.Layer == "0") { prdDbg("Already in 0! Skipping..."); continue; }
                                    //            prdDbg("Was in: :" + br.Layer);
                                    //            br.Layer = "0";
                                    //            prdDbg("Moved to: " + br.Layer);
                                    //            System.Windows.Forms.Application.DoEvents();
                                    //        }
                                    //    }
                                    //} 
                                    #endregion
                                    #region Change Alignment style
                                    //CivilDocument extCDoc = CivilDocument.GetCivilDocument(extDb);

                                    //HashSet<Alignment> als = extDb.HashSetOfType<Alignment>(extTx);

                                    //foreach (Alignment al in als)
                                    //{
                                    //    al.CheckOrOpenForWrite();
                                    //    al.StyleId = extCDoc.Styles.AlignmentStyles["FJV TRACÉ SHOW"];
                                    //    oid labelSetOid = extCDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles["STD 20-5"];
                                    //    al.ImportLabelSet(labelSetOid);
                                    //} 
                                    #endregion
                                    #region Fix midt profile style
                                    //CivilDocument extDoc = CivilDocument.GetCivilDocument(extDb);
                                    //var psc = extDoc.Styles.ProfileStyles;
                                    //ProfileStyle ps = psc["PROFIL STYLE MGO MIDT"].Go<ProfileStyle>(extTx);
                                    //ps.CheckOrOpenForWrite();

                                    //DisplayStyle ds;
                                    //ds = ps.GetDisplayStyleProfile(ProfileDisplayStyleProfileType.Line);
                                    //ds.LinetypeScale = 10;

                                    //ds = ps.GetDisplayStyleProfile(ProfileDisplayStyleProfileType.Curve);
                                    //ds.LinetypeScale = 10;

                                    //ds = ps.GetDisplayStyleProfile(ProfileDisplayStyleProfileType.SymmetricalParabola);
                                    //ds.LinetypeScale = 10;
                                    #endregion
                                }
                                catch (System.Exception ex)
                                {
                                    prdDbg(ex.ToString());
                                    extTx.Abort();
                                    extDb.Dispose();
                                    throw;
                                }

                                extTx.Commit();
                            }
                            extDb.SaveAs(extDb.Filename, true, DwgVersion.Newest, extDb.SecurityParameters);
                        }
                        System.Windows.Forms.Application.DoEvents();
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("PROCESSALLSHEETSINEDITOR", CommandFlags.Session)]
        public void processallsheetsineditor()
        {
            DocumentCollection docCol = Application.DocumentManager;
            //Application.DocumentManager.DocumentActivationEnabled = true;

            try
            {
                #region Dialog box for file list selection and path determination
                string path = string.Empty;
                OpenFileDialog dialog = new OpenFileDialog()
                {
                    Title = "Choose txt file:",
                    DefaultExt = "txt",
                    Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*",
                    FilterIndex = 0
                };
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    path = dialog.FileName;
                }
                else return;

                List<string> fileList;
                fileList = File.ReadAllLines(path).ToList();
                path = Path.GetDirectoryName(path) + "\\";
                #endregion

                foreach (string name in fileList)
                {
                    prdDbg(name);
                    string fileName = path + name;

                    #region Open drawings in editor
                    Document doc = DocumentCollectionExtension.Open(docCol, fileName, false);
                    docCol.MdiActiveDocument = doc;
                    docCol.MdiActiveDocument.Editor.Command("_SYNCHRONIZEREFERENCES");
                    docCol.MdiActiveDocument.Editor.Command("_qsave");
                    docCol.MdiActiveDocument.Editor.Command("_close");
                    #endregion

                    System.Windows.Forms.Application.DoEvents();
                }
            }
            catch (System.Exception ex)
            {
                Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n" + ex.ToString());
                return;
            }

        }

        [CommandMethod("COUNTVFNUMBERS")]
        public void countvfnumbers()
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
                    #region CountVFNumbers

                    string path = string.Empty;
                    OpenFileDialog dialog = new OpenFileDialog()
                    {
                        Title = "Choose txt file:",
                        DefaultExt = "txt",
                        Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*",
                        FilterIndex = 0
                    };
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        path = dialog.FileName;
                    }
                    else return;

                    List<string> fileList;
                    fileList = File.ReadAllLines(path).ToList();
                    path = Path.GetDirectoryName(path) + "\\";
                    prdDbg(path + "\n");

                    foreach (string name in fileList)
                    {
                        prdDbg(name);
                        string fileName = path + name;
                        //prdDbg(fileName);

                        using (Database extDb = new Database(false, true))
                        {
                            extDb.ReadDwgFile(fileName, System.IO.FileShare.ReadWrite, false, "");

                            using (Transaction extTx = extDb.TransactionManager.StartTransaction())
                            {
                                #region Count viewframes
                                var vfSet = extDb.HashSetOfType<ViewFrame>(extTx);

                                foreach (ViewFrame vf in vfSet)
                                {
                                    prdDbg(vf.Name);
                                }
                                #endregion

                                extTx.Commit();
                            }
                            //extDb.SaveAs(extDb.Filename, DwgVersion.Current);
                        }
                        System.Windows.Forms.Application.DoEvents();
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("SETMODELSPACESCALEFORALL")]
        public void setmodelspacescaleforall()
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
                    #region ChangeLayerOfXref

                    string path = string.Empty;
                    OpenFileDialog dialog = new OpenFileDialog()
                    {
                        Title = "Choose txt file:",
                        DefaultExt = "txt",
                        Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*",
                        FilterIndex = 0
                    };
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        path = dialog.FileName;
                    }
                    else return;

                    List<string> fileList;
                    fileList = File.ReadAllLines(path).ToList();
                    path = Path.GetDirectoryName(path) + "\\";
                    prdDbg(path + "\n");

                    foreach (string name in fileList)
                    {
                        if (name.IsNoE()) continue;
                        //prdDbg(name);
                        string fileName = path + name;
                        //prdDbg(fileName);
                        bool needsSaving = false;
                        editor.WriteMessage("-");

                        using (Database extDb = new Database(false, true))
                        {
                            extDb.ReadDwgFile(fileName, System.IO.FileShare.ReadWrite, false, "");

                            using (Transaction extTx = extDb.TransactionManager.StartTransaction())
                            {
                                try
                                {
                                    //prdDbg("Values for db.Cannoscale:");
                                    if (extDb.Cannoscale.Name == "1:1000")
                                    {
                                        prdDbg(name);
                                        var ocm = extDb.ObjectContextManager;
                                        var occ = ocm.GetContextCollection("ACDB_ANNOTATIONSCALES");
                                        AnnotationScale aScale = occ.GetContext("1:250") as AnnotationScale;
                                        if (aScale == null)
                                            throw new System.Exception("Annotation scale not found!");
                                        extDb.Cannoscale = aScale;
                                        needsSaving = true;
                                    }
                                }
                                catch (System.Exception)
                                {
                                    extTx.Abort();
                                    throw;
                                }
                                extTx.Commit();
                            }
                            if (needsSaving)
                                extDb.SaveAs(extDb.Filename, DwgVersion.Current);
                        }
                        System.Windows.Forms.Application.DoEvents();
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("TURNONREVISION")]
        public void turnonrevision()
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
                    #region Turn on revision layers
                    string path = string.Empty;
                    OpenFileDialog dialog = new OpenFileDialog()
                    {
                        Title = "Choose a file:",
                        DefaultExt = "dwg",
                        Filter = "dwg files (*.dwg)|*.dwg|All files (*.*)|*.*",
                        FilterIndex = 0
                    };
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        path = dialog.FileName;
                    }
                    else return;

                    //string path = @"X:\0371-1158 - Gentofte Fase 4 - Dokumenter\01 Intern\02 Tegninger\01 Autocad\Autocad\02 Sheets\4.4\";
                    //string path = @"X:\0371-1158 - Gentofte Fase 4 - Dokumenter\01 Intern\02 Tegninger\01 Autocad\Autocad\01 Views\4.5\Alignment\";
                    //var fileList = File.ReadAllLines(path + "fileList.txt").ToList();

                    prdDbg(path);
                    //string fileName = path + name;

                    using (Database extDb = new Database(false, true))
                    {
                        extDb.ReadDwgFile(path, System.IO.FileShare.ReadWrite, false, "");

                        using (Transaction extTx = extDb.TransactionManager.StartTransaction())
                        {
                            #region Change xref layer
                            //BlockTable bt = extTx.GetObject(extDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                            //foreach (oid oid in bt)
                            //{
                            //    BlockTableRecord btr = extTx.GetObject(oid, OpenMode.ForWrite) as BlockTableRecord;
                            //    if (btr.Name.Contains("_alignment"))
                            //    {
                            //        var ids = btr.GetBlockReferenceIds(true, true);
                            //        foreach (oid brId in ids)
                            //        {
                            //            BlockReference br = brId.Go<BlockReference>(extTx, OpenMode.ForWrite);
                            //            prdDbg(br.Name);
                            //            if (br.Layer == "0") { prdDbg("Already in 0! Skipping..."); continue; }
                            //            prdDbg("Was in: :" + br.Layer);
                            //            br.Layer = "0";
                            //            prdDbg("Moved to: " + br.Layer);
                            //            System.Windows.Forms.Application.DoEvents();
                            //        }
                            //    }
                            //} 
                            #endregion
                            #region Change Alignment style
                            //CivilDocument extCDoc = CivilDocument.GetCivilDocument(extDb);

                            //HashSet<Alignment> als = extDb.HashSetOfType<Alignment>(extTx);

                            //foreach (Alignment al in als)
                            //{
                            //    al.CheckOrOpenForWrite();
                            //    al.StyleId = extCDoc.Styles.AlignmentStyles["FJV TRACÉ SHOW"];
                            //    oid labelSetOid = extCDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles["STD 20-5"];
                            //    al.ImportLabelSet(labelSetOid);
                            //} 
                            #endregion

                            try
                            {
                                string revAlayerName = "REV.A";
                                Oid revAlayerId = Oid.Null;
                                string overskriftLayerName = "Revisionsoverskrifter";
                                Oid overskriftLayerId = Oid.Null;

                                LayerTable lt = extTx.GetObject(extDb.LayerTableId, OpenMode.ForRead) as LayerTable;

                                LayerTableRecord revAlayer = lt.GetLayerByName(revAlayerName);
                                prdDbg($"{revAlayer.Name} is hidden: {revAlayer.IsHidden}");
                                prdDbg($"{revAlayer.Name} is frozen: {revAlayer.IsFrozen}");
                                prdDbg($"{revAlayer.Name} is off: {revAlayer.IsOff}");
                                prdDbg("Turning revision on...\n");
                                revAlayer.CheckOrOpenForWrite();
                                revAlayer.IsFrozen = false;
                                revAlayer.IsHidden = false;
                                revAlayer.IsOff = false;

                                var overskriftsLayer = lt.GetLayerByName(overskriftLayerName);
                                overskriftsLayer.CheckOrOpenForWrite();
                                overskriftsLayer.IsFrozen = false;
                                overskriftsLayer.IsHidden = false;
                                overskriftsLayer.IsOff = false;
                            }
                            catch (System.Exception)
                            {
                                extTx.Abort();
                                throw;
                            }

                            extTx.Commit();
                        }
                        extDb.SaveAs(extDb.Filename, DwgVersion.Current);
                    }
                    System.Windows.Forms.Application.DoEvents();

                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("BRINGALLBLOCKSTOFRONT")]
        [CommandMethod("BF")]
        //Does not update dynamic blocks
        public static void bringallblockstofront()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                SortedList<long, Oid> drawOrder = new SortedList<long, Oid>();

                using (Transaction tx = localDb.TransactionManager.StartTransaction())
                {
                    try
                    {
                        BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                        BlockTableRecord btrModelSpace = tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                        DrawOrderTable dot = tx.GetObject(btrModelSpace.DrawOrderTableId, OpenMode.ForWrite) as DrawOrderTable;

                        ObjectIdCollection ids = new ObjectIdCollection();
                        foreach (Oid oid in bt)
                        {
                            BlockTableRecord btr = tx.GetObject(oid, OpenMode.ForWrite) as BlockTableRecord;

                            foreach (Oid bRefId in btr.GetBlockReferenceIds(true, true))
                            {
                                BlockReference bref = tx.GetObject(bRefId, OpenMode.ForWrite) as BlockReference;
                                if (bref.Name.StartsWith("*")) ids.Add(bRefId);
                            }
                        }

                        dot.MoveToTop(ids);
                    }
                    catch (System.Exception ex)
                    {
                        tx.Abort();
                        ed.WriteMessage(ex.Message);
                        throw;
                    }

                    tx.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage(ex.Message);
            }
        }

        [CommandMethod("LISTPLINESLAYERS")]
        public void listplineslayers()
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
                    HashSet<Polyline> pls = localDb.HashSetOfType<Polyline>(tx);

                    foreach (Polyline pl in pls)
                    {
                        prdDbg($"{pl.Handle.ToString()} -> {pl.Layer}");
                    }

                    HashSet<Line> ls = localDb.HashSetOfType<Line>(tx);

                    foreach (Line l in ls)
                    {
                        prdDbg($"{l.Handle.ToString()} -> {l.Layer}");
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("XRECTEST")]
        public static void XrecordTest()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var per = ed.GetEntity("\nSelect object: ");
            if (per.Status == PromptStatus.OK)
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var ent = (Entity)tr.GetObject(per.ObjectId, OpenMode.ForRead);
                    var data = ent.GetXDictionaryXrecordData("TestXrec");
                    if (data == null)
                    {
                        ed.WriteMessage("\nThe entity does not have a 'TextXrec' Xrecord, it will be added");
                        ent.SetXDictionaryXrecordData("TestXrec", new TypedValue(1, "foo"), new TypedValue(70, 42));
                    }
                    else
                    {
                        foreach (var tv in data.AsArray())
                        {
                            ed.WriteMessage($"\nTypeCode: {tv.TypeCode}, Value: {tv.Value}");
                        }
                    }
                    tr.Commit();
                }
            }
        }

        [CommandMethod("TESTSTIKCOUNTING")]
        public void teststikcounting()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                BlockTableRecord modelSpace = localDb.GetModelspaceForWrite();

                #region Manage layer to contain connection lines
                LayerTable lt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;

                string conLayName = "0-CONNECTION_LINE";
                if (!lt.Has(conLayName))
                {
                    LayerTableRecord ltr = new LayerTableRecord();
                    ltr.Name = conLayName;
                    ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);

                    //Make layertable writable
                    lt.CheckOrOpenForWrite();

                    //Add the new layer to layer table
                    Oid ltId = lt.Add(ltr);
                    tx.AddNewlyCreatedDBObject(ltr, true);
                }
                #endregion

                try
                {
                    #region 
                    using (Database extDb = new Database(false, true))
                    {
                        extDb.ReadDwgFile(@"X:\0371-1158 - Gentofte Fase 4 - Dokumenter\01 Intern\02 Tegninger\" +
                                          @"01 Autocad\Autocad\01 Views\4.1 og 4.2\FJV-Fremtid 4.2.dwg",
                                          System.IO.FileShare.ReadWrite, false, "");

                        using (Transaction extTx = extDb.TransactionManager.StartTransaction())
                        {
                            try
                            {
                                #region Stik counting
                                var plines = extDb.HashSetOfType<Polyline>(extTx, false)
                                    .Where(x => ODDataReader.Pipes.ReadPipeDimension((Entity)x).Int32Value != 999)
                                    .ToHashSet();
                                var points = localDb.HashSetOfType<DBPoint>(tx);

                                foreach (DBPoint point in points)
                                {
                                    List<(double dist, Oid id, Point3d np)> res = new List<(double dist, Oid id, Point3d np)>();
                                    foreach (Polyline pline in plines)
                                    {
                                        Point3d closestPoint = pline.GetClosestPointTo(point.Position, false);
                                        res.Add((point.Position.DistanceHorizontalTo(closestPoint), pline.Id, closestPoint));
                                    }

                                    var nearest = res.MinBy(x => x.dist).FirstOrDefault();
                                    if (nearest == default) continue;

                                    #region Create line
                                    Line connection = new Line();
                                    connection.SetDatabaseDefaults();
                                    connection.Layer = conLayName;
                                    connection.StartPoint = point.Position;
                                    connection.EndPoint = nearest.np;
                                    modelSpace.AppendEntity(connection);
                                    tx.AddNewlyCreatedDBObject(connection, true);
                                    #endregion
                                }

                                #endregion

                                extTx.Commit();
                            }
                            catch (System.Exception ex)
                            {
                                prdDbg(ex.Message);
                                extTx.Abort();
                                throw;
                            }
                        }
                        //extDb.SaveAs(extDb.Filename, DwgVersion.Current);
                    }
                    System.Windows.Forms.Application.DoEvents();

                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                finally
                {

                }
                tx.Commit();
            }
        }

        [CommandMethod("LABELPIPE")]
        [CommandMethod("LB")]
        public void labelpipe()
        {

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor ed = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                        "\nSelect pipe (polyline) to label: ");
                    promptEntityOptions1.SetRejectMessage("\nNot a polyline!");
                    promptEntityOptions1.AddAllowedClass(typeof(Polyline), false);
                    PromptEntityResult entity1 = ed.GetEntity(promptEntityOptions1);
                    if (((PromptResult)entity1).Status != PromptStatus.OK) { tx.Abort(); return; }
                    Oid plineId = entity1.ObjectId;
                    Entity ent = plineId.Go<Entity>(tx);

                    //Test to see if the polyline resides in the correct layer
                    int DN = GetPipeDN(ent);
                    if (DN == 999)
                    {
                        prdDbg("Kunne ikke finde dimension på valgte rør! Kontroller lag!");
                        tx.Abort();
                        return;
                    }
                    string system = GetPipeSystem(ent);
                    if (system == null)
                    {
                        prdDbg("Kunne ikke finde systemet på valgte rør! Kontroller lag!");
                        tx.Abort();
                        return;
                    }
                    double od = GetPipeOd(ent);
                    if (od < 1.0)
                    {
                        prdDbg("Kunne ikke finde rørdimensionen på valgte rør! Kontroller lag!");
                        tx.Abort();
                        return;
                    }

                    //Build label
                    string labelText = "";
                    double kOd = 0;
                    switch (system)
                    {
                        case "Enkelt":
                            kOd = GetBondedPipeKOd(ent);
                            if (kOd < 1.0)
                            {
                                prdDbg("Kunne ikke finde kappedimensionen på valgte rør! Kontroller lag!");
                                tx.Abort();
                                return;
                            }
                            labelText = $"DN{DN}-ø{od.ToString("N1")}/{kOd.ToString("N0")}";
                            break;
                        case "Twin":
                            kOd = GetTwinPipeKOd(ent);
                            if (kOd < 1.0)
                            {
                                prdDbg("Kunne ikke finde kappedimensionen på valgte rør! Kontroller lag!");
                                tx.Abort();
                                return;
                            }
                            labelText = $"DN{DN}-ø{od.ToString("N1")}+ø{od.ToString("N1")}/{kOd.ToString("N0")}";
                            break;
                        default:
                            break;
                    }

                    PromptPointOptions pPtOpts = new PromptPointOptions("\nChoose location of label: ");
                    PromptPointResult pPtRes = ed.GetPoint(pPtOpts);
                    Point3d selectedPoint = pPtRes.Value;
                    if (pPtRes.Status != PromptStatus.OK) { tx.Abort(); return; }

                    //Create new text
                    string layerName = "FJV-DIM";
                    LayerTable lt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;
                    if (!lt.Has(layerName))
                    {
                        LayerTableRecord ltr = new LayerTableRecord();
                        ltr.Name = layerName;
                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 6);

                        lt.CheckOrOpenForWrite();
                        lt.Add(ltr);
                        tx.AddNewlyCreatedDBObject(ltr, true);
                    }

                    DBText label = new DBText();
                    label.Layer = layerName;
                    label.TextString = labelText;
                    label.Height = 1.2;
                    label.HorizontalMode = TextHorizontalMode.TextMid;
                    label.VerticalMode = TextVerticalMode.TextVerticalMid;
                    label.Position = new Point3d(selectedPoint.X, selectedPoint.Y, 0);
                    label.AlignmentPoint = selectedPoint;

                    //Find rotation
                    Polyline pline = (Polyline)ent;
                    Point3d closestPoint = pline.GetClosestPointTo(selectedPoint, true);
                    Vector3d derivative = pline.GetFirstDerivative(closestPoint);
                    double rotation = Math.Atan2(derivative.Y, derivative.X);
                    label.Rotation = rotation;

                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord modelSpace =
                        tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                    Oid labelId = modelSpace.AppendEntity(label);
                    tx.AddNewlyCreatedDBObject(label, true);
                    label.Draw();

                    System.Windows.Forms.Application.DoEvents();

                    //Enable flipping of label
                    const string kwd1 = "Yes";
                    const string kwd2 = "No";
                    PromptKeywordOptions pkos = new PromptKeywordOptions("\nFlip label? ");
                    pkos.Keywords.Add(kwd1);
                    pkos.Keywords.Add(kwd2);
                    pkos.AllowNone = true;
                    pkos.Keywords.Default = kwd2;
                    PromptResult pkwdres = ed.GetKeywords(pkos);
                    string result = pkwdres.StringResult;

                    if (result == kwd1) label.Rotation += Math.PI;

                    #region Attach id data
                    Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;
                    CheckOrCreateTable(
                        tables, OdTables.Labels.GetTableName(), OdTables.Labels.GetTableDescription(),
                        OdTables.Labels.GetColumnNames(), OdTables.Labels.GetColumnDescriptions(),
                        OdTables.Labels.GetDataTypes());

                    string handle = ent.Handle.ToString();
                    CheckAddUpdateRecordValue(
                        tables,
                        labelId,
                        OdTables.Labels.GetTableName(),
                        OdTables.Labels.GetColumnNames()[0],
                        new MapValue(handle));

                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    ed.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("REDUCETEXT")]
        public void reducetext()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor ed = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    double tol = 0;

                    PromptDoubleResult result = ed.GetDouble("\nEnter tolerance in meters:");
                    if (((PromptResult)result).Status != PromptStatus.OK) return;
                    tol = result.Value;

                    HashSet<DBText> dBTexts = localDb.HashSetOfType<DBText>(tx);
                    ObjectIdCollection toDelete = new ObjectIdCollection();

                    HashSet<(Oid id, string text, Point3d position)> allTexts =
                        new HashSet<(Oid id, string text, Point3d position)>();

                    //Cache contents of DBText in memory, i think?
                    foreach (DBText dBText in dBTexts)
                        allTexts.Add((dBText.Id, dBText.TextString, dBText.Position));

                    var groupsWithSimilarText = allTexts.GroupBy(x => x.text);

                    foreach (IGrouping<string, (Oid id, string text, Point3d position)>
                        group in groupsWithSimilarText)
                    {
                        Queue<(Oid id, string text, Point3d position)> qu = new Queue<(Oid, string, Point3d)>();

                        //Load the queue
                        foreach ((Oid id, string text, Point3d position) item in group)
                            qu.Enqueue(item);

                        while (qu.Count > 0)
                        {
                            var labelToTest = qu.Dequeue();
                            if (qu.Count == 0) break;

                            for (int i = 0; i < qu.Count; i++)
                            {
                                var curLabel = qu.Dequeue();
                                if (labelToTest.position.DistanceHorizontalTo(curLabel.position) < tol)
                                    toDelete.Add(curLabel.id);
                                else qu.Enqueue(curLabel);
                            }
                        }
                    }

                    //Delete the chosen labels
                    foreach (Oid oid in toDelete)
                    {
                        DBText ent = oid.Go<DBText>(tx, OpenMode.ForWrite);
                        ent.Erase(true);
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    ed.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("CCL")]
        public void createcomplexlinetype()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            Transaction tr = db.TransactionManager.StartTransaction();
            using (tr)
            {
                try
                {
                    // We'll use the textstyle table to access
                    // the "Standard" textstyle for our text segment
                    TextStyleTable tt = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                    // Get the linetype table from the drawing
                    LinetypeTable ltt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForWrite);
                    // Get layer table
                    LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;

                    //**************************************
                    //Change name of line type to create new and text value
                    //**************************************
                    string ltName = "FJV_RETUR";
                    string text = "RETUR";
                    string textStyleName = "FJV_LINE_TXT";
                    prdDbg($"Remember to create text style: {textStyleName}!!!");

                    List<string> layersToChange = new List<string>();

                    if (ltt.Has(ltName))
                    {
                        Oid existingId = ltt[ltName];
                        Oid placeHolderId = ltt["Continuous"];

                        foreach (Oid oid in lt)
                        {
                            LayerTableRecord ltr = oid.Go<LayerTableRecord>(tr);
                            if (ltr.LinetypeObjectId == existingId)
                            {
                                ltr.CheckOrOpenForWrite();
                                ltr.LinetypeObjectId = placeHolderId;
                                layersToChange.Add(ltr.Name);
                            }
                        }

                        LinetypeTableRecord exLtr = existingId.Go<LinetypeTableRecord>(tr, OpenMode.ForWrite);
                        exLtr.Erase(true);
                    }

                    // Create our new linetype table record...
                    LinetypeTableRecord lttr = new LinetypeTableRecord();
                    // ... and set its properties
                    lttr.Name = ltName;
                    lttr.AsciiDescription =
                      $"{text} ---- {text} ---- {text} ----";
                    lttr.PatternLength = 0.9;
                    //IsScaledToFit makes so that there are no gaps at ends if text cannot fit
                    lttr.IsScaledToFit = true;
                    lttr.NumDashes = 4;
                    // Dash #1
                    lttr.SetDashLengthAt(0, 60);
                    // Dash #2
                    lttr.SetDashLengthAt(1, -10.9);
                    lttr.SetShapeStyleAt(1, tt[textStyleName]);
                    lttr.SetShapeNumberAt(1, 0);
                    lttr.SetShapeOffsetAt(1, new Vector2d(-10.9, -1.1));
                    lttr.SetShapeScaleAt(1, 0.9);
                    lttr.SetShapeIsUcsOrientedAt(1, false);
                    lttr.SetShapeRotationAt(1, 0);
                    lttr.SetTextAt(1, text);
                    // Dash #3
                    lttr.SetDashLengthAt(2, 60);
                    // Dash #4
                    lttr.SetDashLengthAt(3, -10.9);
                    lttr.SetShapeStyleAt(3, tt[textStyleName]);
                    lttr.SetShapeNumberAt(3, 0);
                    lttr.SetShapeOffsetAt(3, new Vector2d(0, 1.1));
                    lttr.SetShapeScaleAt(3, 0.9);
                    lttr.SetShapeIsUcsOrientedAt(3, false);
                    lttr.SetShapeRotationAt(3, Math.PI);
                    lttr.SetTextAt(3, text);
                    // Add the new linetype to the linetype table
                    ObjectId ltId = ltt.Add(lttr);
                    tr.AddNewlyCreatedDBObject(lttr, true);

                    foreach (string name in layersToChange)
                    {
                        Oid ltrId = lt[name];
                        LayerTableRecord ltr = ltrId.Go<LayerTableRecord>(tr, OpenMode.ForWrite);
                        ltr.LinetypeObjectId = ltId;
                    }

                    db.ForEach<Polyline>(x => x.Draw(), tr);
                }
                catch (System.Exception ex)
                {
                    tr.Abort();
                    prdDbg(ex.ToString());
                }
                tr.Commit();
            }
        }

        [CommandMethod("SETLINETYPESCALEDTOFIT")]
        public void setlinetypescaledtofit()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            Transaction tr = db.TransactionManager.StartTransaction();
            using (tr)
            {
                try
                {
                    // We'll use the textstyle table to access
                    // the "Standard" textstyle for our text segment
                    TextStyleTable tt = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                    // Get the linetype table from the drawing
                    LinetypeTable ltt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForWrite);
                    // Get layer table
                    LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;

                    //**************************************
                    //Change name of line type to create new and text value
                    //**************************************
                    string[] ltName = new string[3] { "FJV_RETUR", "FJV_FREM", "FJV_TWIN" };

                    for (int i = 0; i < ltName.Length; i++)
                    {
                        if (ltt.Has(ltName[i]))
                        {
                            Oid existingId = ltt[ltName[i]];
                            LinetypeTableRecord exLtr = existingId.Go<LinetypeTableRecord>(tr, OpenMode.ForWrite);
                            exLtr.IsScaledToFit = true;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    tr.Abort();
                    prdDbg(ex.ToString());
                }
                tr.Commit();
            }
        }

        [CommandMethod("TESTGETDISTANCEATPOINT")]
        public void testgetdistanceatpoint()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            Transaction tx = db.TransactionManager.StartTransaction();
            using (tx)
            {
                try
                {
                    List<BlockReference> blocks = db.ListOfType<BlockReference>(tx);
                    Alignment al = db.ListOfType<Alignment>(tx).FirstOrDefault();

                    foreach (BlockReference br in blocks)
                    {
                        Point3d nearest = al.GetClosestPointTo(br.Position, false);
                        double station = al.GetDistAtPoint(nearest);
                        ed.WriteMessage($"\nNearest station: {station}");
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    ed.WriteMessage(ex.ToString());
                }
                tx.Commit();
            }
        }

        [CommandMethod("DRAWVIEWPORTRECTANGLES")]
        public void drawviewportrectangles()
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
                    #region Paperspace to modelspace test
                    DBDictionary layoutDict = localDb.LayoutDictionaryId.Go<DBDictionary>(tx);
                    var enumerator = layoutDict.GetEnumerator();
                    while (enumerator.MoveNext())
                    {
                        DBDictionaryEntry item = enumerator.Current;
                        prdDbg(item.Key);
                        if (item.Key == "Model")
                        {
                            prdDbg("Skipping model...");
                            continue;
                        }

                        Layout layout = item.Value.Go<Layout>(tx);
                        //ObjectIdCollection vpIds = layout.GetViewports();

                        BlockTableRecord layBlock = layout.BlockTableRecordId.Go<BlockTableRecord>(tx);

                        foreach (Oid id in layBlock)
                        {
                            if (id.IsDerivedFrom<Viewport>())
                            {
                                Viewport viewport = id.Go<Viewport>(tx);
                                //Truncate doubles to whole numebers for easier comparison
                                int centerX = (int)viewport.CenterPoint.X;
                                int centerY = (int)viewport.CenterPoint.Y;
                                if (centerX == 422 && centerY == 442)
                                {
                                    prdDbg("Found profile viewport!");
                                    Extents3d ext = viewport.GeometricExtents;
                                    Polyline pl = new Polyline(4);
                                    pl.AddVertexAt(0, new Point2d(ext.MinPoint.X, ext.MinPoint.Y), 0.0, 0.0, 0.0);
                                    pl.AddVertexAt(1, new Point2d(ext.MinPoint.X, ext.MaxPoint.Y), 0.0, 0.0, 0.0);
                                    pl.AddVertexAt(2, new Point2d(ext.MaxPoint.X, ext.MaxPoint.Y), 0.0, 0.0, 0.0);
                                    pl.AddVertexAt(3, new Point2d(ext.MaxPoint.X, ext.MinPoint.Y), 0.0, 0.0, 0.0);
                                    pl.Closed = true;
                                    pl.SetDatabaseDefaults();
                                    pl.PaperToModel(viewport);
                                    pl.Layer = "0-NONPLOT";
                                    pl.AddEntityToDbModelSpace<Polyline>(localDb);
                                }
                            }
                        }
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    prdDbg("DrawViewportRectangles failed!\n" + ex.ToString());
                    tx.Abort();
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
                    #region Test exploding alignment
                    //PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions("\n Select an alignment: ");
                    //promptEntityOptions1.SetRejectMessage("\n Not an alignment!");
                    //promptEntityOptions1.AddAllowedClass(typeof(Alignment), true);
                    //PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    //if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    //Autodesk.AutoCAD.DatabaseServices.ObjectId alId = entity1.ObjectId;
                    //Alignment al = alId.Go<Alignment>(tx);

                    ////DBObjectCollection objs = new DBObjectCollection();
                    //////First explode
                    ////al.Explode(objs);
                    //////Explodes to 1 block
                    ////Entity firstExplode = (Entity)objs[0];
                    ////Second explode
                    ////objs = new DBObjectCollection();
                    ////firstExplode.Explode(objs);
                    ////prdDbg($"Subsequent block exploded to number of items: {objs.Count}.");

                    //List<Oid> explodedObjects = new List<Oid>();

                    //ObjectEventHandler handler = (s, e) =>
                    //{
                    //    explodedObjects.Add(e.DBObject.ObjectId);
                    //};

                    //localDb.ObjectAppended += handler;
                    //editor.Command("_explode", al.ObjectId);
                    //localDb.ObjectAppended -= handler;

                    //prdDbg(explodedObjects.Count.ToString());

                    ////Assume block reference is the only item
                    //Oid bId = explodedObjects.First();

                    //explodedObjects.Clear();

                    //localDb.ObjectAppended += handler;
                    //editor.Command("_explode", bId);
                    //localDb.ObjectAppended -= handler;

                    //prdDbg(explodedObjects.Count.ToString());
                    #endregion

                    #region Test size arrays
                    //Alignment al;

                    //#region Select alignment
                    //PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions("\n Select an alignment: ");
                    //promptEntityOptions1.SetRejectMessage("\n Not an alignment!");
                    //promptEntityOptions1.AddAllowedClass(typeof(Alignment), true);
                    //PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    //if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    //Autodesk.AutoCAD.DatabaseServices.ObjectId profileId = entity1.ObjectId;
                    //al = profileId.Go<Alignment>(tx);
                    //#endregion

                    //#region Open fremtidig db
                    //DataReferencesOptions dro = new DataReferencesOptions();
                    //string projectName = dro.ProjectName;
                    //string etapeName = dro.EtapeName;

                    //// open the xref database
                    //Database fremDb = new Database(false, true);
                    //fremDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Fremtid"),
                    //    System.IO.FileShare.Read, false, string.Empty);
                    //Transaction fremTx = fremDb.TransactionManager.StartTransaction();
                    //HashSet<Curve> allCurves = fremDb.HashSetOfType<Curve>(fremTx);
                    //HashSet<BlockReference> allBrs = fremDb.HashSetOfType<BlockReference>(fremTx);
                    //#endregion

                    //try
                    //{
                    //    #region GetCurvesAndBRs from fremtidig
                    //    HashSet<Curve> curves = allCurves
                    //        .Where(x => x.XrecFilter("Alignment", new[] { al.Name }))
                    //        .ToHashSet();
                    //    HashSet<BlockReference> brs = allBrs
                    //        .Where(x => x.XrecFilter("Alignment", new[] { al.Name }))
                    //        .ToHashSet();
                    //    prdDbg($"Curves: {curves.Count}, Components: {brs.Count}");
                    //    #endregion

                    //    //PipelineSizeArray sizeArray = new PipelineSizeArray(al, curves);
                    //    //prdDbg("Curves:");
                    //    //prdDbg(sizeArray.ToString());

                    //    prdDbg("Blocks:");
                    //    PipelineSizeArray sizeArray = new PipelineSizeArray(al, curves, brs);
                    //    prdDbg(sizeArray.ToString());

                    //    //Determine direction
                    //    HashSet<(Curve curve, double dist)> curveDistTuples =
                    //            new HashSet<(Curve curve, double dist)>();
                    //    prdDbg($"{al.Name}");
                    //    Point3d samplePoint = al.GetPointAtDist(0);

                    //    foreach (Curve curve in curves)
                    //    {
                    //        if (curve.GetDistanceAtParameter(curve.EndParam) < 1.0) continue;
                    //        Point3d closestPoint = curve.GetClosestPointTo(samplePoint, false);
                    //        if (closestPoint != default)
                    //            curveDistTuples.Add(
                    //                (curve, samplePoint.DistanceHorizontalTo(closestPoint)));
                    //        prdDbg($"Dist: {samplePoint.DistanceHorizontalTo(closestPoint)}");
                    //    }

                    //    Curve closestCurve = curveDistTuples.MinBy(x => x.dist).FirstOrDefault().curve;

                    //    int startingDn = PipeSchedule.GetPipeDN(closestCurve);
                    //    prdDbg($"startingDn: {startingDn}");

                    //    //if (sizeArray[0].DN != startingDn) sizeArray.Reverse();
                    //}
                    //catch (System.Exception ex)
                    //{
                    //    fremTx.Abort();
                    //    fremTx.Dispose();
                    //    fremDb.Dispose();
                    //    prdDbg(ex.ToString());
                    //    throw;
                    //}
                    //fremTx.Abort();
                    //fremTx.Dispose();
                    //fremDb.Dispose();
                    #endregion

                    #region RXClass to String test
                    //prdDbg("Line: " + RXClass.GetClass(typeof(Line)).Name);
                    //prdDbg("Spline: " + RXClass.GetClass(typeof(Spline)).Name);
                    //prdDbg("Polyline: " + RXClass.GetClass(typeof(Polyline)).Name);
                    #endregion

                    #region Paperspace to modelspace test
                    ////BlockTable blockTable = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    ////BlockTableRecord paperSpace = tx.GetObject(blockTable[BlockTableRecord.PaperSpace], OpenMode.ForRead)
                    ////    as BlockTableRecord;

                    //DBDictionary layoutDict = localDb.LayoutDictionaryId.Go<DBDictionary>(tx);
                    //var enumerator = layoutDict.GetEnumerator();
                    //while (enumerator.MoveNext())
                    //{
                    //    DBDictionaryEntry item = enumerator.Current;
                    //    prdDbg(item.Key);
                    //    if (item.Key == "Model")
                    //    {
                    //        prdDbg("Skipping model...");
                    //        continue;
                    //    }

                    //    Layout layout = item.Value.Go<Layout>(tx);
                    //    //ObjectIdCollection vpIds = layout.GetViewports();

                    //    BlockTableRecord layBlock = layout.BlockTableRecordId.Go<BlockTableRecord>(tx);

                    //    foreach (Oid id in layBlock)
                    //    {
                    //        if (id.IsDerivedFrom<Viewport>())
                    //        {
                    //            Viewport viewport = id.Go<Viewport>(tx);
                    //            //Truncate doubles to whole numebers for easier comparison
                    //            int centerX = (int)viewport.CenterPoint.X;
                    //            int centerY = (int)viewport.CenterPoint.Y;
                    //            if (centerX == 422 && centerY == 442)
                    //            {
                    //                prdDbg("Found profile viewport!");
                    //                Extents3d ext = viewport.GeometricExtents;
                    //                Polyline pl = new Polyline(4);
                    //                pl.AddVertexAt(0, new Point2d(ext.MinPoint.X, ext.MinPoint.Y), 0.0, 0.0, 0.0);
                    //                pl.AddVertexAt(1, new Point2d(ext.MinPoint.X, ext.MaxPoint.Y), 0.0, 0.0, 0.0);
                    //                pl.AddVertexAt(2, new Point2d(ext.MaxPoint.X, ext.MaxPoint.Y), 0.0, 0.0, 0.0);
                    //                pl.AddVertexAt(3, new Point2d(ext.MaxPoint.X, ext.MinPoint.Y), 0.0, 0.0, 0.0);
                    //                pl.Closed = true;
                    //                pl.SetDatabaseDefaults();
                    //                pl.PaperToModel(viewport);
                    //                pl.Layer = "0-NONPLOT";
                    //                pl.AddEntityToDbModelSpace<Polyline>(localDb);
                    //            }
                    //        }
                    //    }
                    //}
                    #endregion

                    #region ProfileProjectionLabel testing
                    //HashSet<ProfileProjectionLabel> labels = localDb.HashSetOfType<ProfileProjectionLabel>(tx);
                    //foreach (var label in labels)
                    //{
                    //    DBPoint testPoint = new DBPoint(label.LabelLocation);
                    //    testPoint.AddEntityToDbModelSpace<DBPoint>(localDb);
                    //}

                    #endregion

                    #region PropertySets testing 1
                    ////IntersectUtilities.ODDataConverter.ODDataConverter.testing();
                    //PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                    //    "\nSelect entity to list rxobject:");
                    //promptEntityOptions1.SetRejectMessage("\n Not a p3d!");
                    //promptEntityOptions1.AddAllowedClass(typeof(Polyline3d), true);
                    //PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    //if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    //Autodesk.AutoCAD.DatabaseServices.ObjectId entId = entity1.ObjectId;
                    //prdDbg(CogoPoint.GetClass(typeof(CogoPoint)).Name);
                    #endregion

                    #region Print all values of all ODTable's fields
                    ////PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                    ////    "\nSelect entity to list OdTable:");
                    ////promptEntityOptions1.SetRejectMessage("\n Not a p3d!");
                    ////promptEntityOptions1.AddAllowedClass(typeof(Polyline3d), true);
                    ////PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    ////if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    ////Autodesk.AutoCAD.DatabaseServices.ObjectId entId = entity1.ObjectId;

                    //HashSet<Polyline3d> p3ds = localDb.HashSetOfType<Polyline3d>(tx, true)
                    //    .Where(x => x.Layer == "AFL_ledning_faelles").ToHashSet();

                    //foreach (Polyline3d item in p3ds)
                    //{
                    //    Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

                    //    using (Records records
                    //           = tables.GetObjectRecords(0, item.Id, Autodesk.Gis.Map.Constants.OpenMode.OpenForRead, false))
                    //    {
                    //        int count = records.Count;
                    //        prdDbg($"Tables total: {count.ToString()}");
                    //        for (int i = 0; i < count; i++)
                    //        {
                    //            Record record = records[i];
                    //            int recordCount = record.Count;
                    //            prdDbg($"Table {record.TableName} has {recordCount} fields.");
                    //            for (int j = 0; j < recordCount; j++)
                    //            {
                    //                MapValue value = record[j];
                    //                prdDbg($"R:{i + 1};V:{j + 1} : {value.StrValue}");
                    //            }
                    //        }
                    //    } 
                    //}
                    #endregion

                    #region Test removing colinear vertices
                    //PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                    //    "\nSelect polyline to list parameters:");
                    //promptEntityOptions1.SetRejectMessage("\n Not a polyline!");
                    //promptEntityOptions1.AddAllowedClass(typeof(Polyline), true);
                    //PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    //if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    //Autodesk.AutoCAD.DatabaseServices.ObjectId plineId = entity1.ObjectId;

                    //Polyline pline = plineId.Go<Polyline>(tx);

                    //RemoveColinearVerticesPolyline(pline);
                    #endregion

                    #region Test polyline parameter and vertices
                    //PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                    //    "\nSelect polyline to list parameters:");
                    //promptEntityOptions1.SetRejectMessage("\n Not a polyline!");
                    //promptEntityOptions1.AddAllowedClass(typeof(Polyline), true);
                    //PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    //if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    //Autodesk.AutoCAD.DatabaseServices.ObjectId plineId = entity1.ObjectId;

                    //Polyline pline = plineId.Go<Polyline>(tx);

                    //for (int i = 0; i < pline.NumberOfVertices; i++)
                    //{
                    //    Point3d p3d = pline.GetPoint3dAt(i);
                    //    prdDbg($"Vertex: {i}, Parameter: {pline.GetParameterAtPoint(p3d)}");
                    //}
                    #endregion

                    #region List all gas stik materialer
                    //Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;
                    //HashSet<Polyline3d> p3ds = localDb.HashSetOfType<Polyline3d>(tx)
                    //                                  .Where(x => x.Layer == "GAS-Stikrør" ||
                    //                                              x.Layer == "GAS-Stikrør-2D")
                    //                                  .ToHashSet();
                    //HashSet<string> materials = new HashSet<string>();
                    //foreach (Polyline3d p3d in p3ds)
                    //{
                    //    materials.Add(ReadPropertyToStringValue(tables, p3d.Id, "GasDimOgMat", "Material"));
                    //}

                    //var ordered = materials.OrderBy(x => x);
                    //foreach (string s in ordered) prdDbg(s);
                    #endregion

                    #region ODTables troubles

                    //try
                    //{
                    //    Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;
                    //    StringCollection names = tables.GetTableNames();
                    //    foreach (string name in names)
                    //    {
                    //        prdDbg(name);
                    //        Autodesk.Gis.Map.ObjectData.Table table = null;
                    //        try
                    //        {
                    //            table = tables[name];
                    //            FieldDefinitions defs = table.FieldDefinitions;
                    //            for (int i = 0; i < defs.Count; i++)
                    //            {
                    //                if (defs[i].Name.Contains("DIA") ||
                    //                    defs[i].Name.Contains("Dia") ||
                    //                    defs[i].Name.Contains("dia")) prdDbg(defs[i].Name);
                    //            }
                    //        }
                    //        catch (Autodesk.Gis.Map.MapException e)
                    //        {
                    //            var errCode = (Autodesk.Gis.Map.Constants.ErrorCode)(e.ErrorCode);
                    //            prdDbg(errCode.ToString());

                    //            MapApplication app = HostMapApplicationServices.Application;
                    //            FieldDefinitions tabDefs = app.ActiveProject.MapUtility.NewODFieldDefinitions();
                    //            tabDefs.AddColumn(
                    //                FieldDefinition.Create("Diameter", "Diameter of crossing pipe", DataType.Character), 0);
                    //            tabDefs.AddColumn(
                    //                FieldDefinition.Create("Alignment", "Alignment name", DataType.Character), 1);
                    //            tables.RemoveTable("CrossingData");
                    //            tables.Add("CrossingData", tabDefs, "Table holding relevant crossing data", true);
                    //            //tables.UpdateTable("CrossingData", tabDefs);
                    //        }
                    //    }
                    //}
                    //catch (Autodesk.Gis.Map.MapException e)
                    //{
                    //    var errCode = (Autodesk.Gis.Map.Constants.ErrorCode)(e.ErrorCode);
                    //    prdDbg(errCode.ToString());
                    //}




                    #endregion

                    #region ChangeLayerOfXref

                    //string path = @"X:\0371-1158 - Gentofte Fase 4 - Dokumenter\01 Intern\02 Tegninger\01 Autocad\Autocad\02 Sheets\5.5\";

                    //var fileList = File.ReadAllLines(path + "fileList.txt").ToList();

                    //foreach (string name in fileList)
                    //{
                    //    prdDbg(name);
                    //}

                    //foreach (string name in fileList)
                    //{
                    //    prdDbg(name);
                    //    string fileName = path + name;
                    //    prdDbg(fileName);

                    //    using (Database extDb = new Database(false, true))
                    //    {
                    //        extDb.ReadDwgFile(fileName, System.IO.FileShare.ReadWrite, false, "");

                    //        using (Transaction extTx = extDb.TransactionManager.StartTransaction())
                    //        {
                    //            BlockTable bt = extTx.GetObject(extDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                    //            foreach (oid oid in bt)
                    //            {
                    //                BlockTableRecord btr = extTx.GetObject(oid, OpenMode.ForWrite) as BlockTableRecord;
                    //                if (btr.Name.Contains("_alignment"))
                    //                {
                    //                    var ids = btr.GetBlockReferenceIds(true, true);
                    //                    foreach (oid brId in ids)
                    //                    {
                    //                        BlockReference br = brId.Go<BlockReference>(extTx, OpenMode.ForWrite);
                    //                        prdDbg(br.Name);
                    //                        if (br.Layer == "0") { prdDbg("Already in 0! Skipping..."); continue; }
                    //                        prdDbg("Was in: :" + br.Layer);
                    //                        br.Layer = "0";
                    //                        prdDbg("Moved to: " + br.Layer);
                    //                        System.Windows.Forms.Application.DoEvents();
                    //                    }
                    //                }
                    //            }
                    //            extTx.Commit();
                    //        }
                    //        extDb.SaveAs(extDb.Filename, DwgVersion.Current);

                    //    }
                    //}
                    #endregion

                    #region List blocks scale
                    //HashSet<BlockReference> brs = localDb.HashSetOfType<BlockReference>(tx, true);
                    //foreach (BlockReference br in brs)
                    //{
                    //    prdDbg(br.ScaleFactors.ToString());
                    //}
                    #endregion

                    #region Gather alignment names
                    //HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);

                    //foreach (Alignment al in als.OrderBy(x => x.Name))
                    //{
                    //    editor.WriteMessage($"\n{al.Name}");
                    //}

                    #endregion

                    #region Test ODTables from external database
                    //Tables odTables = HostMapApplicationServices.Application.ActiveProject.ODTables;
                    //StringCollection curDbTables = new StringCollection();
                    //Database curDb = HostApplicationServices.WorkingDatabase;
                    //StringCollection allDbTables = odTables.GetTableNames();
                    //Autodesk.Gis.Map.Project.AttachedDrawings attachedDwgs =
                    //    HostMapApplicationServices.Application.ActiveProject.DrawingSet.AllAttachedDrawings;

                    //int directDWGCount = HostMapApplicationServices.Application.ActiveProject.DrawingSet.DirectDrawingsCount;

                    //foreach (String name in allDbTables)
                    //{
                    //    Autodesk.Gis.Map.ObjectData.Table table = odTables[name];

                    //    bool bTableExistsInCurDb = true;

                    //    for (int i = 0; i < directDWGCount; ++i)
                    //    {
                    //        Autodesk.Gis.Map.Project.AttachedDrawing attDwg = attachedDwgs[i];

                    //        StringCollection attachedTables = attDwg.GetTableList(Autodesk.Gis.Map.Constants.TableType.ObjectDataTable);
                    //    }
                    //    if (bTableExistsInCurDb)

                    //        curDbTables.Add(name);

                    //}

                    //editor.WriteMessage("Current Drawing Object Data Tables Names :\r\n");

                    //foreach (String name in curDbTables)
                    //{

                    //    editor.WriteMessage(name + "\r\n");

                    //}

                    #endregion

                    #region Test description field population
                    //PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                    //"\nSelect test subject:");
                    //promptEntityOptions1.SetRejectMessage("\n Not a polyline3d!");
                    //promptEntityOptions1.AddAllowedClass(typeof(Polyline3d), true);
                    //PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    //if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    //Autodesk.AutoCAD.DatabaseServices.ObjectId lineId = entity1.ObjectId;
                    //Entity ent = lineId.Go<Entity>(tx);

                    //Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

                    //#region Read Csv Data for Layers and Depth

                    ////Establish the pathnames to files
                    ////Files should be placed in a specific folder on desktop
                    //string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";
                    //string pathDybde = "X:\\AutoCAD DRI - 01 Civil 3D\\Dybde.csv";

                    //System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
                    //System.Data.DataTable dtDybde = CsvReader.ReadCsvToDataTable(pathDybde, "Dybde");

                    //#endregion

                    ////Populate description field
                    ////1. Read size record if it exists
                    //MapValue sizeRecord = Utils.ReadRecordData(
                    //    tables, lineId, "SizeTable", "Size");
                    //int SizeTableSize = 0;
                    //string sizeDescrPart = "";
                    //if (sizeRecord != null)
                    //{
                    //    SizeTableSize = sizeRecord.Int32Value;
                    //    sizeDescrPart = $"ø{SizeTableSize}";
                    //}

                    ////2. Read description from Krydsninger
                    //string descrFromKrydsninger = ReadStringParameterFromDataTable(
                    //    ent.Layer, dtKrydsninger, "Description", 0);

                    ////2.1 Read the formatting in the description field
                    //List<(string ToReplace, string Data)> descrFormatList = null;
                    //if (descrFromKrydsninger.IsNotNoE())
                    //    descrFormatList = FindDescriptionParts(descrFromKrydsninger);

                    ////Finally: Compose description field
                    //List<string> descrParts = new List<string>();
                    ////1. Add custom size
                    //if (SizeTableSize != 0) descrParts.Add(sizeDescrPart);
                    ////2. Process and add parts from format bits in OD
                    //if (descrFromKrydsninger.IsNotNoE())
                    //{
                    //    //Interpolate description from Krydsninger with format setting, if they exist
                    //    if (descrFormatList != null && descrFormatList.Count > 0)
                    //    {
                    //        for (int i = 0; i < descrFormatList.Count; i++)
                    //        {
                    //            var tuple = descrFormatList[i];
                    //            string result = ReadDescriptionPartsFromOD(tables, ent, tuple.Data, dtKrydsninger);
                    //            descrFromKrydsninger = descrFromKrydsninger.Replace(tuple.ToReplace, result);
                    //        }
                    //    }

                    //    //Add the description field to parts
                    //    descrParts.Add(descrFromKrydsninger);
                    //}

                    //string description = "";
                    //if (descrParts.Count == 1) description = descrParts[0];
                    //else if (descrParts.Count > 1)
                    //    description = string.Join("; ", descrParts);

                    //editor.WriteMessage($"\n{description}");
                    #endregion

                    #region GetDistance
                    //PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                    //"\nSelect line:");
                    //promptEntityOptions1.SetRejectMessage("\n Not a line!");
                    //promptEntityOptions1.AddAllowedClass(typeof(Line), true);
                    //PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    //if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    //Autodesk.AutoCAD.DatabaseServices.ObjectId lineId = entity1.ObjectId;

                    //PromptEntityOptions promptEntityOptions2 = new PromptEntityOptions(
                    //"\nSelect p3dpoly:");
                    //promptEntityOptions2.SetRejectMessage("\n Not a p3dpoly!");
                    //promptEntityOptions2.AddAllowedClass(typeof(Polyline3d), true);
                    //PromptEntityResult entity2 = editor.GetEntity(promptEntityOptions2);
                    //if (((PromptResult)entity2).Status != PromptStatus.OK) return;
                    //Autodesk.AutoCAD.DatabaseServices.ObjectId poly3dId = entity2.ObjectId;

                    //Line line = lineId.Go<Line>(tx);
                    //Polyline3d p3d = poly3dId.Go<Polyline3d>(tx);

                    //double distance = line.GetGeCurve().GetDistanceTo(
                    //    p3d.GetGeCurve());

                    //editor.WriteMessage($"\nDistance: {distance}.");
                    //editor.WriteMessage($"\nIs less than 0.1: {distance < 0.1}.");

                    //if (distance < 0.1)
                    //{
                    //    PointOnCurve3d[] intPoints = line.GetGeCurve().GetClosestPointTo(
                    //                                 p3d.GetGeCurve());

                    //    //Assume one intersection
                    //    Point3d result = intPoints.First().Point;
                    //    editor.WriteMessage($"\nDetected elevation: {result.Z}.");
                    //}

                    #endregion

                    #region CleanMtexts
                    //HashSet<MText> mtexts = localDb.HashSetOfType<MText>(tx);

                    //foreach (MText mText in mtexts)
                    //{
                    //    string contents = mText.Contents;

                    //    contents = contents.Replace(@"\H3.17507;", "");

                    //    mText.CheckOrOpenForWrite();

                    //    mText.Contents = contents;
                    //} 
                    #endregion

                    #region Test PV start and end station

                    //HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);
                    //foreach (Alignment al in als)
                    //{
                    //    ObjectIdCollection pIds = al.GetProfileIds();
                    //    Profile p = null;
                    //    foreach (oid oid in pIds)
                    //    {
                    //        Profile pt = oid.Go<Profile>(tx);
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
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("CREATEPROPERTYSETSFROMODTABLES")]
        public void createpropertysetsfromodtables()
        {
            IntersectUtilities.ODDataConverter.ODDataConverter.oddatacreatepropertysetsdefs();
        }

        [CommandMethod("ATTACHODTABLEPROPERTYSETS")]
        public void attachodtablepropertysets()
        {
            IntersectUtilities.ODDataConverter.ODDataConverter.attachpropertysetstoobjects();
        }

        [CommandMethod("POPULATEPROPERTYSETSWITHODDATA")]
        public void populatepropertysetswithoddata()
        {
            IntersectUtilities.ODDataConverter.ODDataConverter.populatepropertysetswithoddata();
        }
    }
}