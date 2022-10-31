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
using Dreambuild.AutoCAD;

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
using System.Windows.Documents;
using System.Windows.Controls;
using System.Security.Cryptography;

namespace IntersectUtilities
{
    /// <summary>
    /// Class for intersection tools.
    /// </summary>
    public partial class Intersect : IExtensionApplication
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

                    xRefDB.ReadDwgFile(curPathName, FileOpenMode.OpenForReadAndAllShare, false, null);

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

                    xRefDB.ReadDwgFile(curPathName, FileOpenMode.OpenForReadAndAllShare, false, null);

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
                            FileOpenMode.OpenForReadAndAllShare, false, null);
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
                            editor.WriteMessage($"\n{e.ToString()}");
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
                editor.WriteMessage("\n" + GetPathToDataFiles(projectName, etapeName, "Surface"));

                #region Read surface from file
                // open the xref database
                Database xRefSurfaceDB = new Database(false, true);
                xRefSurfaceDB.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Surface"),
                    FileOpenMode.OpenForReadAndAllShare, false, null);
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
                    xRefSurfaceDB.Dispose();
                    prdDbg("No surface found in file! Aborting...");
                    throw;
                }

                if (surface == null)
                {
                    editor.WriteMessage("\nSurface could not be loaded from the xref!");
                    xRefSurfaceTx.Commit();
                    xRefSurfaceDB.Dispose();
                    throw new System.Exception("Surface is null!");
                }
                #endregion

                #region Load alignments from drawing
                HashSet<Alignment> alignments = null;
                //To be able to check local or external alignments following hack is implemented
                alignments = localDb.HashSetOfType<Alignment>(tx);

                // open the LER dwg database
                Database xRefAlsDB = new Database(false, true);

                xRefAlsDB.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Alignments"),
                    FileOpenMode.OpenForReadAndAllShare, false, null);
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
                        foreach (var ent in filteredLinework)
                        {
                            #region Create points
                            using (Point3dCollection p3dcol = new Point3dCollection())
                            {
                                al.IntersectWith(ent, 0, plane, p3dcol, new IntPtr(0), new IntPtr(0));

                                foreach (Point3d p3d in p3dcol)
                                {
                                    #region Assign elevation based on 3D conditions
                                    Point3d p3dInt = ent.GetClosestPointTo(p3d, new Vector3d(0.0, 0.0, 1.0), false);

                                    count++;
                                    if (p3dInt.Z <= 0)
                                    {
                                        editor.WriteMessage($"\nEntity {ent.Handle} returned {p3dInt.Z}" +
                                            $" elevation for a 3D layer.");
                                    }

                                    double surfaceElevation = surface.FindElevationAtXY(p3dInt.X, p3dInt.Y);
                                    if (p3dInt.Z >= surfaceElevation)
                                    {
                                        prdDbg($"Entity {ent.Handle} return intersection point above surface!\n" +
                                               $"Location: {p3dInt}, Surface E: {surfaceElevation}.");
                                    }

                                    prdDbg(
                                        $"Ler elev: {p3dInt.Z.ToString("0.##")}, " +
                                        $"Surface elev: {surfaceElevation.ToString("0.##")}");
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
                    editor.WriteMessage($"\n{e.ToString()}");
                    return;
                }

                xRefAlsTx.Abort();
                xRefAlsTx.Dispose();
                xRefAlsDB.Dispose();
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
                FileOpenMode.OpenForReadAndAllShare, false, null);
            Transaction fremTx = fremDb.TransactionManager.StartTransaction();
            HashSet<Curve> allCurves = fremDb.HashSetOfType<Curve>(fremTx);
            HashSet<BlockReference> allBrs = fremDb.HashSetOfType<BlockReference>(fremTx);
            // open the LER database
            Database lerDb = new Database(false, true);
            lerDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Ler"),
                FileOpenMode.OpenForReadAndAllShare, false, null);
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

                    #region Instantiate property set manager
                    PSetDefs.DriCrossingData driCrossingData = new PSetDefs.DriCrossingData();
                    PropertySetManager psmDriCrossingData =
                        new PropertySetManager(localDb, driCrossingData.SetName);

                    PropertySetManager psmPipeLineData = new PropertySetManager(
                        fremDb,
                        PSetDefs.DefinedSets.DriPipelineData);
                    PSetDefs.DriPipelineData driPipelineData =
                        new PSetDefs.DriPipelineData();
                    #endregion

                    HashSet<ProfileView> pvs = localDb.HashSetOfType<ProfileView>(tx);
                    foreach (ProfileView pv in pvs)
                    {
                        #region Variables declaration
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
                        #endregion

                        #region GetCurvesAndBRs from fremtidig
                        HashSet<Curve> curves = allCurves
                            .Where(x => psmPipeLineData
                            .FilterPropetyString(x, driPipelineData.BelongsToAlignment, al.Name))
                            .ToHashSet();

                        HashSet<BlockReference> brs = allBrs
                            .Where(x => psmPipeLineData
                            .FilterPropetyString(x, driPipelineData.BelongsToAlignment, al.Name))
                            .ToHashSet();
                        prdDbg($"Curves: {curves.Count}, Components: {brs.Count}");
                        #endregion

                        #region Build size array
                        PipelineSizeArray sizeArray = new PipelineSizeArray(al, curves, brs);
                        prdDbg(sizeArray.ToString());
                        #endregion

                        #region Blockrefs and labels
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
                        #endregion

                        foreach (ProfileProjectionLabel ppl in ppls)
                        {
                            Oid pId = ppl.FeatureId;
                            if (!pId.IsDerivedFrom<CogoPoint>()) continue;
                            CogoPoint cp = pId.Go<CogoPoint>(tx);
                            //if (ReadStringPropertyValue(tables, pId, "CrossingData", "Alignment")
                            //!= al.Name) continue;
                            psmDriCrossingData.GetOrAttachPropertySet(cp);
                            if (psmDriCrossingData.ReadPropertyString(driCrossingData.Alignment) != al.Name) continue;

                            //Get original object from LER dwg
                            //string handle = ReadStringPropertyValue(tables, pId, "IdRecord", "Handle");
                            string handle = psmDriCrossingData.ReadPropertyString(driCrossingData.SourceEntityHandle);
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
                            int originalDia = psmDriCrossingData.ReadPropertyInt(driCrossingData.Diameter);
                            //int originalDia = ReadIntPropertyValue(tables, pId, "CrossingData", "Diameter");
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
                                if (station <= sizeArray[i].EndStation) { kappeOd = sizeArray[i].Kod / 1000; break; }
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
                        ObjectIdCollection brsToUpdate = btr.GetBlockReferenceIds(true, true);
                        foreach (Oid oid in brsToUpdate)
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

        [CommandMethod("selectbyhandle")]
        [CommandMethod("SBH")]
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
                    editor.WriteMessage("\n" + ex.ToString());
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
                    FileOpenMode.OpenForReadAndAllShare, false, null);
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

        [CommandMethod("editelevations")]
        public void editelevations()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            bool cont = true;

            while (cont)
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
                        cont = false;
                        continue;
                }
                #endregion

                Point3d selectedPoint;

                #region Get elevation depending on method
                double elevation = 0;
                switch (eim)
                {
                    case ElevationInputMethod.None:
                        cont = false;
                        continue;
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
                                    tx.Abort();
                                    editor.WriteMessage("\n" + ex.ToString());
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
                        tx.Abort();
                        editor.WriteMessage("\n" + ex.ToString());
                        return;
                    }
                    tx.Commit();
                }
                #endregion
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
                    stylesDB.ReadDwgFile(pathToStyles, FileOpenMode.OpenForReadAndAllShare, false, null);

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

                            //int i = 0;
                            //foreach (Oid oid in objIds)
                            //{
                            //    prdDbg($"{i}: {oid.ToString()}");
                            //    i++;

                            //}

                            //prdDbg("Stylebase.ExportTo() doesn't work!");
                            Autodesk.Civil.DatabaseServices.Styles.StyleBase.ExportTo(objIds, localDb, Autodesk.Civil.StyleConflictResolverType.Override);
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

        [CommandMethod("SETPROFILESVIEW")]
        public void setprofilesview()
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
                    #region Stylize Profile Views
                    HashSet<ProfileView> pvs = localDb.HashSetOfType<ProfileView>(tx);

                    foreach (ProfileView pv in pvs)
                    {
                        pv.CheckOrOpenForWrite();

                        Oid alId = pv.AlignmentId;
                        Alignment al = alId.Go<Alignment>(tx);

                        ObjectIdCollection psIds = al.GetProfileIds();
                        HashSet<Profile> ps = new HashSet<Profile>();
                        foreach (Oid oid in psIds) ps.Add(oid.Go<Profile>(tx));

                        Profile surfaceProfile = ps.Where(x => x.Name.Contains("surface")).FirstOrDefault();
                        Oid surfaceProfileId = Oid.Null;
                        if (surfaceProfile != null) surfaceProfileId = surfaceProfile.ObjectId;
                        else
                        {
                            ed.WriteMessage("\nSurface profile not found!");
                            continue;
                        }

                        Profile topProfile = ps.Where(x => x.Name.Contains("TOP")).FirstOrDefault();
                        Oid topProfileId = Oid.Null;
                        if (topProfile != null) topProfileId = topProfile.ObjectId;
                        else
                        {
                            ed.WriteMessage("\nTop profile not found!");
                            continue;
                        }

                        //this doesn't quite work
                        Oid pvbsId = civilDoc.Styles.ProfileViewBandSetStyles["EG-FG Elevations and Stations"];
                        ProfileViewBandSet pvbs = pv.Bands;
                        //pvbs.ImportBandSetStyle(pvbsId);

                        ////try this
                        //Oid pvBSId1 = civilDoc.Styles.BandStyles.ProfileViewProfileDataBandStyles["Elevations and Stations"];
                        //Oid pvBSId2 = civilDoc.Styles.BandStyles.ProfileViewProfileDataBandStyles["TitleBuffer"];
                        //ProfileViewBandItemCollection pvic = new ProfileViewBandItemCollection(pv.Id, BandLocationType.Bottom);
                        //pvic.Add(pvBSId1);
                        //pvic.Add(pvBSId2);
                        //pvbs.SetBottomBandItems(pvic);

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
        }

        [CommandMethod("MOVECOMPONENTINPROFILEVIEW")]
        public void movecomponentinprofileview()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                #region Select profile view
                PromptEntityOptions promptEntityOptions2 = new PromptEntityOptions("\n Select a ProfileView where to move component: ");
                promptEntityOptions2.SetRejectMessage("\n Not a ProfileView");
                promptEntityOptions2.AddAllowedClass(typeof(ProfileView), true);
                PromptEntityResult entity2 = editor.GetEntity(promptEntityOptions2);
                if (((PromptResult)entity2).Status != PromptStatus.OK)
                {
                    tx.Abort();
                    return;
                }
                ProfileView profileView = tx.GetObject(entity2.ObjectId, OpenMode.ForRead) as ProfileView;
                #endregion

                #region Select component block
                PromptEntityOptions promptEntityOptions = new PromptEntityOptions("\n Select component to move: ");
                promptEntityOptions.SetRejectMessage("\n Not a block reference!");
                promptEntityOptions.AddAllowedClass(typeof(BlockReference), true);
                PromptEntityResult entity = editor.GetEntity(promptEntityOptions);
                if (((PromptResult)entity).Status != PromptStatus.OK)
                {
                    tx.Abort();
                    return;
                }
                Autodesk.AutoCAD.DatabaseServices.ObjectId Id = entity.ObjectId;
                BlockReference detailingBr = tx.GetObject(Id, OpenMode.ForWrite, false) as BlockReference;
                #endregion

                #region Select point where to move
                PromptPointOptions pPtOpts = new PromptPointOptions("");
                // Prompt for the start point
                pPtOpts.Message = "\nEnter location where to move the component:";
                PromptPointResult pPtRes = editor.GetPoint(pPtOpts);
                Point3d selectedPointOnPV = pPtRes.Value;
                // Exit if the user presses ESC or cancels the command
                if (pPtRes.Status != PromptStatus.OK)
                {
                    tx.Abort();
                    return;
                }
                #endregion

                PropertySetManager psmHandle = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriSourceReference);
                PSetDefs.DriSourceReference driSourceReference = new PSetDefs.DriSourceReference();

                #region Open fremtidig db
                DataReferencesOptions dro = new DataReferencesOptions();
                string projectName = dro.ProjectName;
                string etapeName = dro.EtapeName;

                //open the xref database
                Database fremDb = new Database(false, true);
                fremDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Fremtid"),
                    FileOpenMode.OpenForReadAndAllShare, false, null);
                Transaction fremTx = fremDb.TransactionManager.StartTransaction();
                //HashSet<Curve> allCurves = fremDb.HashSetOfType<Curve>(fremTx);
                HashSet<BlockReference> allBrs = fremDb.HashSetOfType<BlockReference>(fremTx);
                #endregion

                //Welds cannot be moved at the same time
                //As I cannot determine the new location
                //I think I could do it...
                //By finding the vector offset at the original location and applying
                //The same offset to the new point, buuuuuut....
                //I don't bother... yet...

                //////////////////////////////////////////
                //PropertySetManager.DefinedSets setNameBelongsTo = PropertySetManager.DefinedSets.DriPipelineData;
                //string propertyNameBelongsTo = "BelongsToAlignment";
                //////////////////////////////////////////

                //PropertySetManager psmBelongsTo = new PropertySetManager(fremDb, setNameBelongsTo);

                try
                {
                    #region Get the original source component block
                    string originalHandleString = psmHandle.ReadPropertyString(
                        detailingBr, driSourceReference.SourceEntityHandle);
                    prdDbg(originalHandleString);
                    long ln = Convert.ToInt64(originalHandleString, 16);
                    Handle hn = new Handle(ln);
                    BlockReference originalBr = fremDb.GetObjectId(false, hn, 0)
                                                      .Go<BlockReference>(fremTx);
                    //Original location
                    Point3d originalLocation = originalBr.Position;
                    #endregion

                    #region Determine the new location
                    double station = 0;
                    double elevation = 0;
                    profileView.FindStationAndElevationAtXY(selectedPointOnPV.X, selectedPointOnPV.Y,
                        ref station, ref elevation);

                    double newX = 0;
                    double newY = 0;

                    Alignment al = profileView.AlignmentId.Go<Alignment>(tx);
                    al.PointLocation(station, 0, ref newX, ref newY);
                    Point3d newLocation = new Point3d(newX, newY, 0);
                    #endregion

                    #region Find block's welds
                    ////Filter allBrs to only contain belonging to the alignment in question
                    //HashSet<BlockReference> alBrs = allBrs.Where(x =>
                    //   psmBelongsTo.FilterPropetyString(x, propertyNameBelongsTo, al.Name)).ToHashSet();

                    //HashSet<BlockReference> welds = new HashSet<BlockReference>();

                    //BlockTableRecord originalBtr = originalBr.BlockTableRecord.Go<BlockTableRecord>(fremTx);
                    //foreach (Oid oid in originalBtr)
                    //{
                    //    if (!oid.IsDerivedFrom<BlockReference>()) continue;
                    //    BlockReference nestedBr = oid.Go<BlockReference>(fremTx);
                    //    if (!nestedBr.Name.Contains("MuffeIntern")) continue;
                    //    Point3d wPt = nestedBr.Position;
                    //    wPt = wPt.TransformBy(originalBr.BlockTransform);
                    //    BlockReference weld = alBrs.Where(x =>
                    //            x.Position.HorizontalEqualz(wPt) &&
                    //            x.RealName() == "Svejsepunkt")
                    //             .FirstOrDefault();
                    //    if (weld != default) welds.Add(weld);
                    //}
                    #endregion

                    #region Move the original block
                    Vector3d moveVector = originalLocation.GetVectorTo(newLocation);
                    originalBr.CheckOrOpenForWrite();
                    originalBr.TransformBy(Matrix3d.Displacement(moveVector));
                    #endregion

                    #region Move welds
                    //foreach (BlockReference weld in welds)
                    //{
                    //    weld.CheckOrOpenForWrite();
                    //    weld.TransformBy(Matrix3d.Displacement(moveVector));
                    //}
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
                fremTx.Commit();
                fremTx.Dispose();
                fremDb.SaveAs(fremDb.Filename, true, DwgVersion.Current, null);
                fremDb.Dispose();
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

        [CommandMethod("COPYPSFROMENTTOENT")]
        [CommandMethod("CPYPS")]
        public void copypsfromenttoent()
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
                    Entity sourceEnt = sourceId.Go<Entity>(tx);

                    PromptEntityOptions promptEntityOptions2 = new PromptEntityOptions(
                        "\nSelect entity where to copy OD TO:");
                    promptEntityOptions2.SetRejectMessage("\n Not an entity!");
                    promptEntityOptions2.AddAllowedClass(typeof(Entity), false);
                    PromptEntityResult entity2 = editor.GetEntity(promptEntityOptions2);
                    if (((PromptResult)entity2).Status != PromptStatus.OK) { tx.Abort(); return; }
                    Autodesk.AutoCAD.DatabaseServices.ObjectId targetId = entity2.ObjectId;
                    Entity targetEnt = targetId.Go<Entity>(tx, OpenMode.ForWrite);
                    #endregion

                    #region Copy all PSs
                    PropertySetManager.CopyAllProperties(sourceEnt, targetEnt);
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
                                BlockTableRecord btr = br.IsDynamicBlock ?
                                    tx.GetObject(br.DynamicBlockTableRecord, OpenMode.ForRead) as BlockTableRecord :
                                    tx.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                                prdDbg("Top LEVEL: " + br.RealName());

                                foreach (Oid bOid in btr)
                                {
                                    if (bOid.ObjectClass.Name != "AcDbBlockReference") continue;
                                    BlockReference nBr = bOid.Go<BlockReference>(tx);
                                    prdDbg(nBr.Name);
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

        public void colorizealllerlayersmethod(Database extDb = null)
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
                    string lagSti = "X:\\AutoCAD DRI - 01 Civil 3D\\Lag.csv";
                    System.Data.DataTable dtLag = CsvReader.ReadCsvToDataTable(lagSti, "Lag");

                    LayerTable lt = selectedDB.LayerTableId.Go<LayerTable>(selectedDB.TransactionManager.TopTransaction);

                    Regex regex = new Regex(@"^(?<R>\d+)\*(?<G>\d+)\*(?<B>\d+)");

                    HashSet<string> layerNames = dtLag.AsEnumerable().Select(x => x["Layer"].ToString()).ToHashSet();

                    foreach (string name in layerNames.Where(x => x.IsNotNoE()).OrderBy(x => x))
                    {
                        if (lt.Has(name))
                        {
                            string colorString = ReadStringParameterFromDataTable(name, dtLag, "Farve", 0);
                            if (colorString.IsNotNoE())
                            {
                                var color = UtilsCommon.Utils.ParseColorString(colorString);
                                prdDbg($"Set layer {name} to color: {color}");
                                LayerTableRecord ltr = lt[name].Go<LayerTableRecord>(selectedDB.TransactionManager.TopTransaction, OpenMode.ForWrite);
                                ltr.Color = color;
                                ltr.LineWeight = LineWeight.LineWeight013;
                            }
                            else prdDbg("No match!");
                        }
                    }

                    //List<string> localLayers = localDb.ListLayers();

                    //foreach (string name in localLayers.Where(x => x.IsNotNoE()).OrderBy(x => x))
                    //{
                    //    if (lt.Has(name))
                    //    {
                    //        string colorString = ReadStringParameterFromDataTable(name, dtKrydsninger, "Farve", 0);
                    //        if (colorString.IsNotNoE() && regex.IsMatch(colorString))
                    //        {
                    //            Match match = regex.Match(colorString);
                    //            byte R = Convert.ToByte(int.Parse(match.Groups["R"].Value));
                    //            byte G = Convert.ToByte(int.Parse(match.Groups["G"].Value));
                    //            byte B = Convert.ToByte(int.Parse(match.Groups["B"].Value));
                    //            prdDbg($"Set layer {name} to color: R: {R.ToString()}, G: {G.ToString()}, B: {B.ToString()}");
                    //            LayerTableRecord ltr = lt[name].Go<LayerTableRecord>(selectedDB.TransactionManager.TopTransaction, OpenMode.ForWrite);
                    //            ltr.Color = Color.FromRgb(R, G, B);
                    //            ltr.LineWeight = LineWeight.LineWeight013;
                    //        }
                    //        else prdDbg("No match!");
                    //    }
                    //}
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
                                               FileOpenMode.OpenForReadAndAllShare, false, null);

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
        public static void attachareadata()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;

            try
            {
                try
                {
                    #region Dialog box for file list selection and path determination
                    string path = string.Empty;
                    OpenFileDialog dialog = new OpenFileDialog()
                    {
                        Title = "Choose csv file:",
                        DefaultExt = "csv",
                        Filter = "csv files (*.csv)|*.csv|All files (*.*)|*.*",
                        FilterIndex = 0
                    };
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        path = dialog.FileName;
                    }
                    else return;
                    #endregion

                    #region Populate area data from string
                    System.Data.DataTable areaDescriptions = CsvReader.ReadCsvToDataTable(path, "Areas");

                    //Datatable to list of strings
                    //List<string> areaNames = (from System.Data.DataRow dr in areaDescriptions.Rows select (string)dr[1]).ToList();

                    //foreach (string name in areaNames)
                    foreach (DataRow row in areaDescriptions.Rows)
                    {
                        string nummer = row["Nummer"].ToString();
                        string navn = row["Navn"].ToString();
                        string vejkl = row["Vejkl"].ToString();
                        string belægning = row["Belaegning"].ToString();

                        //prdDbg(name);
                        prdDbg($"{navn} {nummer}, Vejkl. {vejkl}, {belægning}");
                        System.Windows.Forms.Application.DoEvents();
                        #region Select pline
                        PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                            "\nSelect polyline to add data to:");
                        promptEntityOptions1.SetRejectMessage("\nNot a polyline!");
                        promptEntityOptions1.AddAllowedClass(typeof(Polyline), true);
                        PromptEntityResult entity1 = ed.GetEntity(promptEntityOptions1);
                        if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                        Oid plineId = entity1.ObjectId;
                        #endregion

                        //string[] split1 = name.Split(new[] { ", " }, StringSplitOptions.None);
                        //split1[1] = split1[1].Replace("Vejkl. ", "");

                        #region Old ownership logic
                        //string ownership = "O";
                        ////Handle the ownership dilemma
                        //if (split1[0].Contains("(P)"))
                        //{
                        //    split1[0] = split1[0].Split(new[] { " (" }, StringSplitOptions.None)[0];
                        //    ownership = "P";
                        //}

                        //string[] data = new string[4] { split1[0], ownership, split1[1], split1[2] }; 
                        #endregion

                        //Test change
                        //if (ownership == "P") prdDbg(data[0] + " " + data[1] + " " + data[2] + " " + data[3]);

                        #region Create layer for OK plines
                        string områderLayer = "0-OMRÅDER-OK";
                        using (Transaction txLag = localDb.TransactionManager.StartTransaction())
                        {
                            LayerTable lt = txLag.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;

                            if (!lt.Has(områderLayer))
                            {
                                LayerTableRecord ltr = new LayerTableRecord();
                                ltr.Name = områderLayer;
                                ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 0);
                                ltr.LineWeight = LineWeight.LineWeight000;
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

                        using (Transaction tx = localDb.TransactionManager.StartTransaction())
                        {
                            #region Property Set Manager
                            PropertySetManager psmOmråder =
                                new PropertySetManager(localDb, PSetDefs.DefinedSets.DriOmråder);
                            PSetDefs.DriOmråder driOmråder = new PSetDefs.DriOmråder();
                            #endregion

                            Polyline pline = plineId.Go<Polyline>(tx, OpenMode.ForWrite);

                            psmOmråder.GetOrAttachPropertySet(pline);
                            //psmOmråder.WritePropertyString(driOmråder.Vejnavn, split1[0]);
                            ////psmOmråder.WritePropertyString(driOmråder.Ejerskab, split1[1]);
                            //psmOmråder.WritePropertyString(driOmråder.Vejklasse, split1[1]);
                            //psmOmråder.WritePropertyString(driOmråder.Belægning, split1[2]);

                            psmOmråder.WritePropertyString(driOmråder.Nummer, nummer);
                            psmOmråder.WritePropertyString(driOmråder.Vejnavn, navn);
                            psmOmråder.WritePropertyString(driOmråder.Vejklasse, vejkl);
                            psmOmråder.WritePropertyString(driOmråder.Belægning, belægning);

                            pline.Layer = områderLayer;
                            pline.Color = Color.FromColorIndex(ColorMethod.ByAci, 256);
                            pline.LineWeight = LineWeight.ByLayer;
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
                    //DataReferencesOptions dro = new DataReferencesOptions();

                    foreach (string fileName in fileList)
                    {
                        prdDbg(fileName);
                        string file = path + fileName;
                        using (Database extDb = new Database(false, true))
                        {
                            extDb.ReadDwgFile(file, System.IO.FileShare.ReadWrite, false, "");
                            using (Transaction extTx = extDb.TransactionManager.StartTransaction())
                            {
                                try
                                {
                                    #region Correct line weight layers
                                    //string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";
                                    //System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");

                                    //LayerTable lt = extDb.LayerTableId.Go<LayerTable>(extDb.TransactionManager.TopTransaction);

                                    //HashSet<string> layerNames = dtKrydsninger.AsEnumerable().Select(x => x["Layer"].ToString()).ToHashSet();

                                    //foreach (string layerName in layerNames.Where(x => x.IsNotNoE()).OrderBy(x => x))
                                    //{
                                    //    if (lt.Has(layerName))
                                    //    {
                                    //        LayerTableRecord ltr = lt[layerName].Go<LayerTableRecord>(
                                    //            extDb.TransactionManager.TopTransaction, OpenMode.ForWrite);
                                    //        ltr.LineWeight = LineWeight.LineWeight013;
                                    //    }
                                    //}
                                    #endregion
                                    #region Stagger labels
                                    //staggerlabelsallmethod(extDb);
                                    #endregion
                                    #region Unhide specific layer in DB
                                    //LayerTable extLt = extDb.LayerTableId.Go<LayerTable>(extTx);
                                    //foreach (Oid oid in extLt)
                                    //{
                                    //    LayerTableRecord ltr = oid.Go<LayerTableRecord>(extTx);
                                    //    if (ltr.Name.Contains("|"))
                                    //    {
                                    //        var split = ltr.Name.Split('|');
                                    //        string xrefName = split[0];
                                    //        string layerName = split[1];
                                    //        if (xrefName == "FJV-Fremtid-E1" &&
                                    //            layerName.Contains("SVEJSEPKT-NR"))
                                    //        {
                                    //            prdDbg(ltr.Name);
                                    //            prdDbg(ltr.IsDependent.ToString());
                                    //            ltr.CheckOrOpenForWrite();
                                    //            prdDbg(ltr.IsOff.ToString());
                                    //            ltr.IsOff = false;
                                    //            prdDbg(ltr.IsOff.ToString());
                                    //        }
                                    //    }
                                    //}
                                    #endregion
                                    #region Set linetypes of xref
                                    //LayerTable extLt = extDb.LayerTableId.Go<LayerTable>(extTx);
                                    //foreach (Oid oid in extLt)
                                    //{
                                    //    LayerTableRecord ltr = oid.Go<LayerTableRecord>(extTx);
                                    //    if (ltr.Name.Contains("|"))
                                    //    {
                                    //        var split = ltr.Name.Split('|');
                                    //        string xrefName = split[0];
                                    //        string layerName = split[1];
                                    //        if (xrefName == "FJV-Fremtid-E1" &&
                                    //            layerName.Contains("TWIN"))
                                    //        {
                                    //            prdDbg(ltr.Name);
                                    //            prdDbg(ltr.IsDependent.ToString());
                                    //            LinetypeTable ltt = extDb.LinetypeTableId.Go<LinetypeTable>(extTx);
                                    //            Oid contId = ltt["Continuous"];
                                    //            ltr.CheckOrOpenForWrite();
                                    //            ltr.LinetypeObjectId = contId;
                                    //        }
                                    //    }
                                    //}
                                    #endregion
                                    #region CreateDetailing
                                    //createdetailingmethod(dro, extDb);
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
                                    //ds.Lineweight = LineWeight.LineWeight000;

                                    //ds = ps.GetDisplayStyleProfile(ProfileDisplayStyleProfileType.Curve);
                                    //ds.LinetypeScale = 10;
                                    //ds.Lineweight = LineWeight.LineWeight000;

                                    //ds = ps.GetDisplayStyleProfile(ProfileDisplayStyleProfileType.SymmetricalParabola);
                                    //ds.LinetypeScale = 10;
                                    //ds.Lineweight = LineWeight.LineWeight000;
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

        [CommandMethod("UPDATEALLDYNAMICBLOCKSOFTYPE")]
        public void updatealldynamicblocksoftype()
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
                    //string path = string.Empty;
                    //OpenFileDialog dialog = new OpenFileDialog()
                    //{
                    //    Title = "Choose txt file:",
                    //    DefaultExt = "txt",
                    //    Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*",
                    //    FilterIndex = 0
                    //};
                    //if (dialog.ShowDialog() == DialogResult.OK)
                    //{
                    //    path = dialog.FileName;
                    //}
                    //else return;

                    //List<string> fileList;
                    //fileList = File.ReadAllLines(path).ToList();
                    //path = Path.GetDirectoryName(path) + "\\";
                    #endregion

                    #region For update "SH LIGE" blocks in Gentofte
                    System.Data.DataTable dt = CsvReader.ReadCsvToDataTable(@"X:\AutoCAD DRI - 01 Civil 3D\Stier.csv", "Stier");
                    List<string> list = dt.AsEnumerable()
                        .Where(x => x["PrjId"].ToString() == "GENTOFTE1158")
                        .Select(x => x["Fremtid"].ToString()).ToList();
                    #endregion

                    //!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                    //Project and etape selection object
                    //Comment out if not needed
                    //DataReferencesOptions dro = new DataReferencesOptions();

                    foreach (string fileName in list)
                    {
                        prdDbg(fileName);
                        using (Database extDb = new Database(false, true))
                        {
                            extDb.ReadDwgFile(fileName, System.IO.FileShare.ReadWrite, false, "");
                            using (Transaction extTx = extDb.TransactionManager.StartTransaction())
                            {
                                try
                                {
                                    #region Update dynamic block "SH LIGE"
                                    string blockName = "SH LIGE";
                                    string blockPath = @"X:\AutoCAD DRI - 01 Civil 3D\DynBlokke\Symboler.dwg";

                                    using (var blockDb = new Database(false, true))
                                    {
                                        // Read the DWG into a side database
                                        blockDb.ReadDwgFile(blockPath, FileOpenMode.OpenForReadAndAllShare, true, "");

                                        Transaction blockTx = blockDb.TransactionManager.StartTransaction();

                                        Oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(blockDb);
                                        Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(extDb);

                                        BlockTable sourceBt = blockTx.GetObject(blockDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                                        ObjectIdCollection idsToClone = new ObjectIdCollection();
                                        idsToClone.Add(sourceBt[blockName]);

                                        IdMapping mapping = new IdMapping();
                                        blockDb.WblockCloneObjects(idsToClone, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);

                                        blockTx.Commit();
                                        blockTx.Dispose();
                                    }

                                    var existingBlocks = extDb.HashSetOfType<BlockReference>(extTx);
                                    foreach (var existingBlock in existingBlocks)
                                    {
                                        if (existingBlock.RealName() == blockName)
                                        {
                                            existingBlock.ResetBlock();
                                            var props = existingBlock.DynamicBlockReferencePropertyCollection;
                                            foreach (DynamicBlockReferenceProperty prop in props)
                                            {
                                                if (prop.PropertyName == "Type") prop.Value = "200x40";
                                            }
                                            existingBlock.RecordGraphicsModified(true);
                                        }
                                    }
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

        [CommandMethod("REPLACEBLOCK")]
        [CommandMethod("RB")]
        public void replaceblock()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            const string kwd1 = "Naturgas";
            const string kwd2 = "Andet";
            const string kwd3 = "UDGÅR";
            const string kwd4 = "Ingen";
            const string kwd5 = "El";
            const string kwd6 = "Olie";
            const string kwd7 = "Varmepumpe";
            const string kwd8 = "Fjernvarme";
            const string kwd9 = "Fast brændsel";

            PromptKeywordOptions pKeyOpts = new PromptKeywordOptions("");
            pKeyOpts.Message = "\nHvilken block skal indsættes i stedet? ";
            pKeyOpts.Keywords.Add(kwd1);
            pKeyOpts.Keywords.Add(kwd2);
            pKeyOpts.Keywords.Add(kwd3);
            pKeyOpts.Keywords.Add(kwd4);
            pKeyOpts.Keywords.Add(kwd5);
            pKeyOpts.Keywords.Add(kwd6);
            pKeyOpts.Keywords.Add(kwd7);
            pKeyOpts.Keywords.Add(kwd8);
            pKeyOpts.Keywords.Add(kwd9);
            pKeyOpts.AllowNone = true;
            pKeyOpts.Keywords.Default = kwd1;
            PromptResult pKeyRes = editor.GetKeywords(pKeyOpts);

            string blockName = pKeyRes.StringResult;

            while (true)
            {
                using (Transaction tx = localDb.TransactionManager.StartTransaction())
                {
                    try
                    {
                        #region Select block
                        PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                            "\nSelect block to replace:");
                        promptEntityOptions1.SetRejectMessage("\n Not a block!");
                        promptEntityOptions1.AddAllowedClass(typeof(BlockReference), true);
                        PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                        if (((PromptResult)entity1).Status != PromptStatus.OK) { tx.Abort(); return; }
                        Autodesk.AutoCAD.DatabaseServices.ObjectId entId = entity1.ObjectId;
                        BlockReference brOld = entId.Go<BlockReference>(tx, OpenMode.ForWrite);
                        #endregion

                        BlockReference brNew = localDb.CreateBlockWithAttributes(blockName, brOld.Position);

                        PropertySetManager.CopyAllProperties(brOld, brNew);
                        PropertySetManager.WriteNonDefinedPropertySetString(brNew, "BBR", "Type", blockName);

                        brOld.Erase(true);
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

                    string labelText = PipeSchedule.GetLabel(ent);

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
                    PropertySetManager psm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriSourceReference);
                    PSetDefs.DriSourceReference driSourceReference = new PSetDefs.DriSourceReference();

                    psm.GetOrAttachPropertySet(label);
                    string handle = ent.Handle.ToString();
                    psm.WritePropertyString(driSourceReference.SourceEntityHandle, handle);
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
                    if (((PromptResult)result).Status != PromptStatus.OK) { tx.Abort(); return; }
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
                    string ltName = "BIPS_TEXT_FJV-PROD";
                    string text = "FJV-PROD";
                    string textStyleName = "Standard";
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
                    lttr.IsScaledToFit = false;
                    lttr.NumDashes = 4;
                    // Dash #1
                    lttr.SetDashLengthAt(0, 15);
                    // Dash #2
                    lttr.SetDashLengthAt(1, -6.4);
                    lttr.SetShapeStyleAt(1, tt[textStyleName]);
                    lttr.SetShapeNumberAt(1, 0);
                    lttr.SetShapeOffsetAt(1, new Vector2d(-6.4, -0.45));
                    lttr.SetShapeScaleAt(1, 0.9);
                    lttr.SetShapeIsUcsOrientedAt(1, false);
                    lttr.SetShapeRotationAt(1, 0);
                    lttr.SetTextAt(1, text);
                    // Dash #3
                    lttr.SetDashLengthAt(2, 15);
                    // Dash #4
                    lttr.SetDashLengthAt(3, -6.4);
                    lttr.SetShapeStyleAt(3, tt[textStyleName]);
                    lttr.SetShapeNumberAt(3, 0);
                    lttr.SetShapeOffsetAt(3, new Vector2d(0.0, 0.45));
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

        [CommandMethod("CREATEALLLINETYPESLAYERS")]
        public void createcalllinetypeslayers()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                try
                {
                    TextStyleTable tt = (TextStyleTable)tx.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                    // Get the linetype table from the drawing
                    LinetypeTable ltt = (LinetypeTable)tx.GetObject(db.LinetypeTableId, OpenMode.ForWrite);
                    // Get layer table
                    LayerTable lt = tx.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;

                    double startX = 0; double Y = 0; double delta = 5;
                    double endX = 100;

                    var dict = new Dictionary<string, Oid>();

                    foreach (Oid lttrOid in ltt)
                    {
                        LinetypeTableRecord lttr = lttrOid.Go<LinetypeTableRecord>(tx);
                        dict.Add(lttr.Name, lttrOid);
                    }

                    foreach (var kvp in dict.OrderBy(x => x.Key))
                    {
                        LinetypeTableRecord lttr = kvp.Value.Go<LinetypeTableRecord>(tx);

                        string layerName = "00LT-" + lttr.Name;

                        if (!lt.Has(layerName))
                        {
                            db.CheckOrCreateLayer(layerName);
                        }

                        Oid ltrId = lt[layerName];

                        LayerTableRecord ltr = ltrId.Go<LayerTableRecord>(tx, OpenMode.ForWrite);

                        ltr.LinetypeObjectId = kvp.Value;

                        Polyline pline = new Polyline(2);

                        pline.AddVertexAt(pline.NumberOfVertices, new Point2d(startX, Y), 0, 0, 0);
                        pline.AddVertexAt(pline.NumberOfVertices, new Point2d(endX, Y), 0, 0, 0);
                        pline.AddEntityToDbModelSpace(db);

                        pline.Layer = layerName;

                        DBText text = new DBText();
                        text.Position = new Point3d(-60, Y, 0);
                        text.TextString = lttr.Name;
                        text.AddEntityToDbModelSpace(db);

                        Y -= delta;
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex.ToString());
                }
                tx.Commit();
            }
        }

        [CommandMethod("PLACEOBJLAYCOLOR")]
        public void placeobjlaycolor()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            Oid oid = Interaction.GetEntity("Select object to write color of: ", typeof(Entity), false);
            if (oid == Oid.Null) return;

            Point3d location = Interaction.GetPoint("Select where to place the text: ");
            if (location == Algorithms.NullPoint3d) return;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    Entity ent = oid.Go<Entity>(tx);
                    LayerTableRecord ltr = ent.LayerId.Go<LayerTableRecord>(tx);
                    LinetypeTableRecord lttr = ltr.LinetypeObjectId.Go<LinetypeTableRecord>(tx);

                    DBText text = new DBText();
                    text.Position = location;
                    text.TextString = $"{ltr.Color} {lttr.Name}";
                    text.Height = 0.5;
                    text.AddEntityToDbModelSpace(localDb);
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex.ToString());
                    return;
                }
                tx.Commit();
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

        [CommandMethod("MARKBUEPIPES")]
        public void markbuepipes()
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
                    //PromptEntityOptions peo = new PromptEntityOptions("Select pline");
                    //PromptEntityResult per = editor.GetEntity(peo);
                    //Polyline pline = per.ObjectId.Go<Polyline>(tx);
                    HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx);

                    foreach (Polyline pline in plines)
                    {
                        for (int j = 0; j < pline.NumberOfVertices - 1; j++)
                        {
                            //Guard against already cut out curves
                            if (j == 0 && pline.NumberOfVertices == 2) { break; }
                            double b = pline.GetBulgeAt(j);
                            if (b == 0) continue;
                            Point2d fP = pline.GetPoint2dAt(j);
                            Point2d sP = pline.GetPoint2dAt(j + 1);
                            double u = fP.GetDistanceTo(sP);
                            double radius = u * ((1 + b.Pow(2)) / (4 * Math.Abs(b)));
                            double minRadius = PipeSchedule.GetPipeMinElasticRadius(pline);

                            //If radius is less than minRadius a buerør is detected
                            //Split the pline in segments delimiting buerør and append
                            if (radius < minRadius)
                            {
                                prdDbg($"Buerør detected {fP.ToString()} and {sP.ToString()}.");
                                prdDbg($"R: {radius}, minR: {minRadius}");

                                CircularArc2d arc = pline.GetArcSegment2dAt(j);
                                Point2d[] samples = arc.GetSamplePoints(3);

                                Line line = new Line(new Point3d(0, 0, 0), new Point3d(samples[1].X, samples[1].Y, 0));
                                line.AddEntityToDbModelSpace(localDb);
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

        [CommandMethod("CREATE3DTRACEFROMBUNDPROFILE")]
        public void create3dtracefrombundprofile()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            string dbFilename = localDb.OriginalFileName;
            string fileName = Path.GetFileNameWithoutExtension(dbFilename);
            string path = Path.GetDirectoryName(dbFilename);
            string poly3dExportFile = path + "\\" + fileName + "_3D_Bundprofil.dwg";

            Database poly3dDb = new Database(true, true);
            using (Transaction poly3dTx = poly3dDb.TransactionManager.StartTransaction())
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    //string layerName = "0-TRACE-3D";
                    //localDb.CheckOrCreateLayer(layerName);

                    HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);
                    foreach (Alignment al in als)
                    {
                        var pIds = al.GetProfileIds();
                        Profile bundProfile = null;
                        foreach (Oid oid in pIds)
                            if (oid.Go<Profile>(tx).Name.Contains("BUND")) bundProfile = oid.Go<Profile>(tx);

                        if (bundProfile == null)
                        {
                            prdDbg($"Bund profile could not be found for alignment {al.Name}!");
                            continue;
                        }

                        double alLength = al.Length;
                        double step = 0.1;
                        int nrOfSteps = (int)(alLength / step);

                        Point3dCollection p3ds = new Point3dCollection();

                        for (int i = 0; i < nrOfSteps; i++)
                        {
                            double curStation = i * step;

                            double X = 0;
                            double Y = 0;

                            al.PointLocation(curStation, 0, ref X, ref Y);

                            double Z = 0;
                            try
                            {
                                Z = bundProfile.ElevationAt(curStation);
                            }
                            catch (System.Exception)
                            {
                                prdDbg($"Elevation sampling failed at alignment {al.Name}, station {curStation}.");
                            }

                            p3ds.Add(new Point3d(X, Y, Z));
                        }

                        Polyline3d pline3d = new Polyline3d(Poly3dType.SimplePoly, p3ds, false);
                        pline3d.AddEntityToDbModelSpace(localDb);

                        HashSet<int> verticesToRemove = new HashSet<int>();
                        PolylineVertex3d[] vertices = pline3d.GetVertices(tx);

                        for (int i = 0; i < vertices.Length - 2; i++)
                        {
                            PolylineVertex3d vertex1 = vertices[i];
                            PolylineVertex3d vertex2 = vertices[i + 1];
                            PolylineVertex3d vertex3 = vertices[i + 2];

                            Vector3d vec1 = vertex1.Position.GetVectorTo(vertex2.Position);
                            Vector3d vec2 = vertex2.Position.GetVectorTo(vertex3.Position);

                            if (vec1.IsCodirectionalTo(vec2, Tolerance.Global)) verticesToRemove.Add(i + 1);
                        }

                        Point3dCollection p3dsClean = new Point3dCollection();

                        for (int i = 0; i < vertices.Length; i++)
                        {
                            if (verticesToRemove.Contains(i)) continue;
                            PolylineVertex3d v = vertices[i];
                            p3dsClean.Add(v.Position);
                        }

                        Polyline3d nyPline = new Polyline3d(Poly3dType.SimplePoly, p3dsClean, false);
                        nyPline.AddEntityToDbModelSpace(poly3dDb);

                        pline3d.CheckOrOpenForWrite();
                        pline3d.Erase(true);
                    }

                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    poly3dTx.Abort();
                    poly3dDb.Dispose();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                tx.Commit();
                poly3dTx.Commit();
                poly3dDb.SaveAs(poly3dExportFile, DwgVersion.Newest);
                poly3dDb.Dispose();
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
                    #region Test sideloaded nested block location
                    Database fremDb = new Database(false, true);
                    fremDb.ReadDwgFile(@"X:\AutoCAD DRI - 01 Civil 3D\Dev\15 DynBlockSideloaded\BlockDwg.dwg",
                        FileOpenMode.OpenForReadAndAllShare, false, null);
                    Transaction fremTx = fremDb.TransactionManager.StartTransaction();
                    HashSet<BlockReference> allBrs = fremDb.HashSetOfType<BlockReference>(fremTx);
                    foreach (var br in allBrs)
                    {
                        BlockTableRecord btr = br.BlockTableRecord.Go<BlockTableRecord>(fremTx);
                        foreach (Oid id in btr)
                        {
                            if (!id.IsDerivedFrom<BlockReference>()) continue;
                            BlockReference nestedBr = id.Go<BlockReference>(fremTx);
                            if (!nestedBr.Name.Contains("MuffeIntern")) continue;
                            Point3d wPt = nestedBr.Position;
                            wPt = wPt.TransformBy(br.BlockTransform);

                            //Line line = new Line(new Point3d(), wPt);
                            //line.AddEntityToDbModelSpace(localDb);
                        }
                    }
                    fremTx.Abort();
                    fremTx.Dispose();
                    fremDb.Dispose();
                    #endregion

                    #region Test nested block location in dynamic blocks
                    //                    var list = localDb.HashSetOfType<BlockReference>(tx);
                    //                    foreach (var br in list)
                    //                    {
                    //                        BlockTableRecord btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);
                    //                        foreach (Oid id in btr)
                    //{
                    //                            if (!id.IsDerivedFrom<BlockReference>()) continue;
                    //                            BlockReference nestedBr = id.Go<BlockReference>(tx);
                    //                            if (!nestedBr.Name.Contains("MuffeIntern")) continue;
                    //                            Point3d wPt = nestedBr.Position;
                    //                            wPt = wPt.TransformBy(br.BlockTransform);

                    //                            Line line = new Line(new Point3d(), wPt);
                    //                            line.AddEntityToDbModelSpace(localDb);
                    //                        }


                    //                        //DBObjectCollection objs = new DBObjectCollection();
                    //                        //br.Explode(objs);
                    //                        //foreach (var item in objs)
                    //                        //{
                    //                        //    if (item is BlockReference nBr)
                    //                        //    {
                    //                        //        Line line = new Line(new Point3d(), nBr.Position);
                    //                        //        line.AddEntityToDbModelSpace(localDb);
                    //                        //    }
                    //                        //}
                    //                    }
                    #endregion

                    #region Test constant attribute, constant attr is attached to BlockTableRecord and not BR
                    //PromptEntityOptions peo = new PromptEntityOptions("Select a BR: ");
                    //PromptEntityResult per = editor.GetEntity(peo);
                    //BlockReference br = per.ObjectId.Go<BlockReference>(tx);

                    //prdDbg(br.GetAttributeStringValue("VERSION"));

                    //foreach (Oid oid in br.AttributeCollection)
                    //{
                    //    AttributeReference ar = oid.Go<AttributeReference>(tx);
                    //    prdDbg($"Name: {ar.Tag}, Text: {ar.TextString}");
                    //}

                    //BlockTableRecord btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);
                    //foreach (Oid oid in btr)
                    //{
                    //    if (oid.IsDerivedFrom<AttributeDefinition>())
                    //    {
                    //        AttributeDefinition attDef = oid.Go<AttributeDefinition>(tx);
                    //        if (attDef.Tag == "VERSION")
                    //        {
                    //            prdDbg($"Constant attribute > Name: {attDef.Tag}, Text: {attDef.TextString}");
                    //        }
                    //    }
                    //}
                    #endregion

                    #region Test enum list
                    //StringBuilder sb = new StringBuilder();
                    //HashSet<int> nums = new HashSet<int>()
                    //{
                    //    1, 2, 3, 4, 5, 6, 7, 8
                    //};

                    //foreach (var num in nums)
                    //{
                    //    string f = ((Graph.EndType)num).ToString();
                    //    foreach (var xum in nums)
                    //    {
                    //        string s = ((Graph.EndType)xum).ToString();

                    //        sb.AppendLine($"{f}-{s}");
                    //    }
                    //}

                    //OutputWriter(@"C:\Temp\EntTypeEnum.txt", sb.ToString(), true);
                    #endregion

                    #region test regex
                    //List<string> list = new List<string>()
                    //{
                    //    "0*123*232",
                    //    "234*12*0",
                    //    "0*115*230",
                    //    "000*115*230",
                    //    "0*0*0",
                    //    "255*255*255",
                    //    "231*0*98"
                    //};

                    //foreach (string s in list)
                    //{
                    //    var color = UtilsCommon.Utils.ParseColorString(s);
                    //    if (color == null) prdDbg($"Parsing of string {s} failed!");
                    //    else prdDbg($"Parsing of string {s} success!");
                    //}
                    #endregion

                    #region Create points at vertices
                    //var meter = new ProgressMeter();

                    //string pointLayer = "0-MARKER-POINT";
                    //localDb.CheckOrCreateLayer(pointLayer);

                    //meter.Start("Gathering elements...");
                    //var ids = QuickSelection.SelectAll("LWPOLYLINE")
                    //    .QWhere(x => x.Layer.Contains("Etape"));
                    //meter.SetLimit(ids.Count());
                    //ids.QForEach(x =>
                    //{
                    //    var pline = x as Polyline;
                    //    var vertNumber = pline.NumberOfVertices;
                    //    for (int i = 0; i < vertNumber; i++)
                    //    {
                    //        Point3d vertLocation = pline.GetPoint3dAt(i);
                    //        DBPoint point = new DBPoint(vertLocation);
                    //        point.AddEntityToDbModelSpace(localDb);
                    //        point.Layer = pointLayer;
                    //    }
                    //});
                    #endregion

                    #region Test clean 3d poly
                    //PromptEntityOptions peo = new PromptEntityOptions("\nSelect pline 3d: ");
                    //peo.SetRejectMessage("\nNot a Polyline3d!");
                    //peo.AddAllowedClass(typeof(Polyline3d), true);
                    //PromptEntityResult per = editor.GetEntity(peo);
                    //Polyline3d pline = per.ObjectId.Go<Polyline3d>(tx);

                    //List<int> verticesToRemove = new List<int>();

                    //PolylineVertex3d[] vertices = pline.GetVertices(tx);

                    //for (int i = 0; i < vertices.Length - 2; i++)
                    //{
                    //    PolylineVertex3d vertex1 = vertices[i];
                    //    PolylineVertex3d vertex2 = vertices[i + 1];
                    //    PolylineVertex3d vertex3 = vertices[i + 2];

                    //    Vector3d vec1 = vertex1.Position.GetVectorTo(vertex2.Position);
                    //    Vector3d vec2 = vertex2.Position.GetVectorTo(vertex3.Position);

                    //    if (vec1.IsCodirectionalTo(vec2, Tolerance.Global)) verticesToRemove.Add(i + 1);
                    //}

                    //Point3dCollection p3ds = new Point3dCollection();

                    //for (int i = 0; i < vertices.Length; i++)
                    //{
                    //    if (verticesToRemove.Contains(i)) continue;
                    //    PolylineVertex3d v = vertices[i];
                    //    p3ds.Add(v.Position);
                    //}

                    //Polyline3d nyPline = new Polyline3d(Poly3dType.SimplePoly, p3ds, false);
                    //nyPline.AddEntityToDbModelSpace(localDb);

                    //nyPline.Layer = pline.Layer;

                    //pline.CheckOrOpenForWrite();
                    //pline.Erase(true);
                    #endregion

                    #region Test redefine
                    //string blockName = "SH LIGE";
                    //string blockPath = @"X:\AutoCAD DRI - 01 Civil 3D\DynBlokke\Symboler.dwg";

                    //using (var blockDb = new Database(false, true))
                    //{

                    //    // Read the DWG into a side database
                    //    blockDb.ReadDwgFile(blockPath, FileOpenMode.OpenForReadAndAllShare, true, "");

                    //    Transaction blockTx = blockDb.TransactionManager.StartTransaction();

                    //    Oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(blockDb);
                    //    Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);

                    //    BlockTable sourceBt = blockTx.GetObject(blockDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    //    ObjectIdCollection idsToClone = new ObjectIdCollection();
                    //    idsToClone.Add(sourceBt[blockName]);

                    //    IdMapping mapping = new IdMapping();
                    //    blockDb.WblockCloneObjects(idsToClone, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);

                    //    blockTx.Commit();
                    //    blockTx.Dispose();
                    //}

                    //var existingBlocks = localDb.HashSetOfType<BlockReference>(tx);
                    //foreach (var existingBlock in existingBlocks)
                    //{
                    //    if (existingBlock.RealName() == blockName)
                    //    {
                    //        existingBlock.ResetBlock();
                    //        var props = existingBlock.DynamicBlockReferencePropertyCollection;
                    //        foreach (DynamicBlockReferenceProperty prop in props)
                    //        {
                    //            if (prop.PropertyName == "Type") prop.Value = "200x40";
                    //        }
                    //        existingBlock.RecordGraphicsModified(true);
                    //    }
                    //}
                    #endregion

                    #region Test dynamic properties
                    //PromptEntityOptions peo = new PromptEntityOptions("Select a BR: ");
                    //PromptEntityResult per = editor.GetEntity(peo);
                    //BlockReference br = per.ObjectId.Go<BlockReference>(tx);

                    //DynamicBlockReferencePropertyCollection props = br.DynamicBlockReferencePropertyCollection;
                    //foreach (DynamicBlockReferenceProperty property in props)
                    //{
                    //    prdDbg($"Name: {property.PropertyName}, Units: {property.UnitsType}, Value: {property.Value}");
                    //    if (property.PropertyName == "Type")
                    //    {
                    //        property.Value = "Type 2";
                    //    }
                    //}

                    ////Construct pattern which matches the parameter definition
                    //Regex variablePattern = new Regex(@"{\$(?<Parameter>[a-zæøåA-ZÆØÅ0-9_:-]*)}");

                    //stringToTry = ConstructStringByRegex(stringToTry);
                    //prdDbg(stringToTry);

                    ////Test if a pattern matches in the input string
                    //string ConstructStringByRegex(string stringToProcess)
                    //{
                    //    if (variablePattern.IsMatch(stringToProcess))
                    //    {
                    //        //Get the first match
                    //        Match match = variablePattern.Match(stringToProcess);
                    //        //Get the first capture
                    //        string capture = match.Captures[0].Value;
                    //        //Get the parameter name from the regex match
                    //        string parameterName = match.Groups["Parameter"].Value;
                    //        //Read the parameter value from BR
                    //        string parameterValue = ReadDynamicPropertyValue(br, parameterName);
                    //        //Replace the captured group in original string with the parameter value
                    //        stringToProcess = stringToProcess.Replace(capture, parameterValue);
                    //        //Recursively call current function
                    //        //It runs on the string until no more captures remain
                    //        //Then it returns
                    //        stringToProcess = ConstructStringByRegex(stringToProcess);
                    //    }

                    //    return stringToProcess;
                    //}

                    //string ReadDynamicPropertyValue(BlockReference block, string propertyName)
                    //{
                    //    DynamicBlockReferencePropertyCollection props = block.DynamicBlockReferencePropertyCollection;
                    //    foreach (DynamicBlockReferenceProperty property in props)
                    //    {
                    //        //prdDbg($"Name: {property.PropertyName}, Units: {property.UnitsType}, Value: {property.Value}");
                    //        if (property.PropertyName == propertyName)
                    //        {
                    //            switch (property.UnitsType)
                    //            {
                    //                case DynamicBlockReferencePropertyUnitsType.NoUnits:
                    //                    return property.Value.ToString();
                    //                case DynamicBlockReferencePropertyUnitsType.Angular:
                    //                    double angular = Convert.ToDouble(property.Value);
                    //                    return angular.ToDegrees().ToString("0.##");
                    //                case DynamicBlockReferencePropertyUnitsType.Distance:
                    //                    double distance = Convert.ToDouble(property.Value);
                    //                    return distance.ToString("0.##");
                    //                case DynamicBlockReferencePropertyUnitsType.Area:
                    //                    double area = Convert.ToDouble(property.Value);
                    //                    return area.ToString("0.00");
                    //                default:
                    //                    return "";
                    //            }
                    //        }
                    //    }
                    //    return "";
                    //}
                    #endregion

                    #region QA pipe lengths
                    //System.Data.DataTable komponenter = CsvReader.ReadCsvToDataTable(
                    //        @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");

                    //HashSet<BlockReference> brs = localDb.HashSetOfType<BlockReference>(tx);
                    //prdDbg($"Block count: {brs.Count}");

                    //double totalLength = 0;
                    //int antal = 0;

                    //foreach (BlockReference br in brs)
                    //{
                    //    if (br.RealName() == "SVEJSEPUNKT" ||
                    //        ReadStringParameterFromDataTable(br.RealName(), komponenter, "Navn", 0) == null) continue;

                    //    DBObjectCollection objs = new DBObjectCollection();
                    //    br.Explode(objs);

                    //    if (br.RealName().Contains("RED KDLR"))
                    //    {
                    //        BlockReference br1 = null;
                    //        BlockReference br2 = null;

                    //        foreach (DBObject obj in objs)
                    //        {
                    //            if (obj is BlockReference muffe1 && br1 == null) br1 = muffe1;
                    //            else if (obj is BlockReference muffe2 && br1 != null) br2 = muffe2;
                    //        }

                    //        double dist = br1.Position.DistanceHorizontalTo(br2.Position);
                    //        totalLength += dist;
                    //        antal++;
                    //    }
                    //    else
                    //    {
                    //        foreach (DBObject obj in objs)
                    //        {
                    //            if (br.RealName() == "PA TWIN S3") prdDbg(obj.GetType().Name);
                    //            if (obj is Line line) totalLength += line.Length;
                    //        }
                    //        antal++;
                    //    }
                    //}

                    //prdDbg($"Samlet længde af {antal} komponenter: {totalLength}");

                    //HashSet<Profile> profiles = localDb.HashSetOfType<Profile>(tx);

                    //double totalProfLength = 0;

                    //foreach (Profile profile in profiles)
                    //{
                    //    if (profile.Name.Contains("MIDT"))
                    //        totalProfLength += profile.Length;
                    //}

                    //prdDbg($"Profiles: {totalProfLength.ToString("0.###")}");

                    //#region Read surface from file
                    //CivSurface surface = null;
                    //try
                    //{
                    //    surface = localDb
                    //        .HashSetOfType<TinSurface>(tx)
                    //        .FirstOrDefault() as CivSurface;
                    //}
                    //catch (System.Exception)
                    //{
                    //    throw;
                    //}

                    //if (surface == null)
                    //{
                    //    editor.WriteMessage("\nSurface could not be loaded!");
                    //    tx.Abort();
                    //    return;
                    //}
                    //#endregion

                    //HashSet<Polyline> plines = localDb.GetFjvPipes(tx).Where(x => GetPipeDN(x) != 999).ToHashSet();
                    //prdDbg(plines.Count.ToString());

                    //double totalPlineLength = 0;
                    //double totalFlLength = 0;

                    //foreach (Polyline pline in plines)
                    //{
                    //    totalPlineLength += pline.Length;

                    //    Oid flOid = FeatureLine.Create(pline.Handle.ToString(), pline.Id);
                    //    FeatureLine fl = flOid.Go<FeatureLine>(tx);
                    //    fl.AssignElevationsFromSurface(surface.Id, true);

                    //    totalFlLength += fl.Length3D;
                    //}

                    //prdDbg($"Pls: {totalPlineLength.ToString("0.###")}, Fls: {totalFlLength.ToString("0.###")}");
                    #endregion

                    #region Test buerør
                    ////PromptEntityOptions peo = new PromptEntityOptions("Select pline");
                    ////PromptEntityResult per = editor.GetEntity(peo);
                    ////Polyline pline = per.ObjectId.Go<Polyline>(tx);
                    //HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx);

                    //foreach (Polyline pline in plines)
                    //{
                    //    for (int j = 0; j < pline.NumberOfVertices - 1; j++)
                    //    {
                    //        //Guard against already cut out curves
                    //        if (j == 0 && pline.NumberOfVertices == 2) { break; }
                    //        double b = pline.GetBulgeAt(j);
                    //        Point2d fP = pline.GetPoint2dAt(j);
                    //        Point2d sP = pline.GetPoint2dAt(j + 1);
                    //        double u = fP.GetDistanceTo(sP);
                    //        double radius = u * ((1 + b.Pow(2)) / (4 * Math.Abs(b)));
                    //        double minRadius = PipeSchedule.GetPipeMinElasticRadius(pline);

                    //        //If radius is less than minRadius a buerør is detected
                    //        //Split the pline in segments delimiting buerør and append
                    //        if (radius < minRadius)
                    //        {
                    //            prdDbg($"Buerør detected {fP.ToString()} and {sP.ToString()}.");
                    //            prdDbg($"R: {radius}, minR: {minRadius}");

                    //            Line line = new Line(new Point3d(0, 0, 0), pline.GetPointAtDist(pline.Length / 2));
                    //            line.AddEntityToDbModelSpace(localDb);
                    //        }
                    //    }
                    //}
                    #endregion

                    #region Test location point of BRs
                    //HashSet<BlockReference> brs = localDb.HashSetOfType<BlockReference>(tx);
                    //BlockReference br = brs.Where(x => x.Handle.ToString() == "4E2A23").FirstOrDefault();
                    //prdDbg($"{br != default}");

                    //Database alsDB = new Database(false, true);
                    //alsDB.ReadDwgFile(@"X:\022-1226 Egedal - Krogholmvej, Etape 1 - Dokumenter\" +
                    //                  @"01 Intern\02 Tegninger\01 Autocad\Alignment\Alignment - Etape 1.dwg",
                    //    System.IO.FileShare.Read, false, string.Empty);
                    //using (Transaction alsTx = alsDB.TransactionManager.StartTransaction())
                    //{
                    //    HashSet<Alignment> als = alsDB.HashSetOfType<Alignment>(alsTx);
                    //    Alignment al = als.Where(x => x.Name == "05 Sigurdsvej").FirstOrDefault();

                    //    if (al != default)
                    //    {
                    //        Point3d brLoc = al.GetClosestPointTo(br.Position, false);

                    //        double station = 0;
                    //        double offset = 0;
                    //        al.StationOffset(brLoc.X, brLoc.Y, ref station, ref offset);
                    //        prdDbg($"S: {station}, O: {offset}");
                    //    }

                    //    alsTx.Abort();
                    //}
                    //alsDB.Dispose();
                    #endregion

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
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("CLEANPLINE")]
        public void cleanpline()
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
                    #region Remove colinear vertices
                    PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                        "\nSelect polyline to clean:");
                    promptEntityOptions1.SetRejectMessage("\n Not a polyline!");
                    promptEntityOptions1.AddAllowedClass(typeof(Polyline), true);
                    PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    Autodesk.AutoCAD.DatabaseServices.ObjectId plineId = entity1.ObjectId;

                    Polyline pline = plineId.Go<Polyline>(tx);

                    RemoveColinearVerticesPolyline(pline);
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

        //[CommandMethod("CREATEPROPERTYSETSFROMODTABLES")]
        public void createpropertysetsfromodtables()
        {
            IntersectUtilities.ODDataConverter.ODDataConverter.oddatacreatepropertysetsdefs();
        }

        //[CommandMethod("ATTACHODTABLEPROPERTYSETS")]
        public void attachodtablepropertysets()
        {
            IntersectUtilities.ODDataConverter.ODDataConverter.attachpropertysetstoobjects();
        }

        //[CommandMethod("POPULATEPROPERTYSETSWITHODDATA")]
        public void populatepropertysetswithoddata()
        {
            IntersectUtilities.ODDataConverter.ODDataConverter.populatepropertysetswithoddata();
        }

        [CommandMethod("CONVERTODTOPSS")]
        public void convertodtopss()
        {
            IntersectUtilities.ODDataConverter.ODDataConverter.oddatacreatepropertysetsdefs();
            IntersectUtilities.ODDataConverter.ODDataConverter.attachpropertysetstoobjects();
            IntersectUtilities.ODDataConverter.ODDataConverter.populatepropertysetswithoddata();
        }

        //[CommandMethod("GRAPHPOPULATE")]
        public void graphpopulate()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    System.Data.DataTable komponenter = CsvReader.ReadCsvToDataTable(
                                        @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");

                    HashSet<Entity> allEnts = localDb.GetFjvEntities(tx, komponenter, true);

                    PropertySetManager psm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriGraph);
                    Graph graph = new Graph(localDb, psm, komponenter);

                    foreach (Entity entity in allEnts) graph.AddEntityToPOIs(entity);

                    //Create clusters of POIs based on a maximum distance
                    //which is based on the smallest block distance between MuffeIntern
                    IEnumerable<IGrouping<Graph.POI, Graph.POI>> clusters
                        = graph.POIs.GroupByCluster((x, y) => x.Point.GetDistanceTo(y.Point), 0.01);

                    //Iterate over clusters
                    foreach (IGrouping<Graph.POI, Graph.POI> cluster in clusters)
                    {
                        //Create unique pairs
                        var pairs = cluster.SelectMany((value, index) => cluster.Skip(index + 1),
                                                       (first, second) => new { first, second });
                        //Create reference to each other for each pair
                        foreach (var pair in pairs)
                        {
                            if (pair.first.Owner.Handle == pair.second.Owner.Handle) continue;
                            pair.first.AddReference(pair.second);
                            pair.second.AddReference(pair.first);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex.ToString());
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("GRAPHWRITE")]
        public void graphwrite()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            graphclear();
            graphpopulate();

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    System.Data.DataTable komponenter = CsvReader.ReadCsvToDataTable(
                                        @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");

                    HashSet<Entity> allEnts = localDb.GetFjvEntities(tx, komponenter, true, true);

                    PropertySetManager psm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriGraph);
                    Graph graph = new Graph(localDb, psm, komponenter);

                    foreach (Entity entity in allEnts)
                    {
                        graph.AddEntityToGraphEntities(entity);
                    }

                    graph.CreateAndWriteGraph();

                    //Start the dot engine to create the graph
                    System.Diagnostics.Process cmd = new System.Diagnostics.Process();
                    cmd.StartInfo.FileName = "cmd.exe";
                    cmd.StartInfo.WorkingDirectory = @"C:\Temp\";
                    cmd.StartInfo.Arguments = @"/c ""dot -Tpdf MyGraph.dot > MyGraph.pdf""";
                    cmd.Start();
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex.ToString());
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }
        }

        //[CommandMethod("GRAPHCLEAR")]
        public void graphclear()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    System.Data.DataTable komponenter = CsvReader.ReadCsvToDataTable(
                                        @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");

                    HashSet<Entity> allEnts = localDb.GetFjvEntities(tx, komponenter, true);

                    PropertySetManager psm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriGraph);
                    PSetDefs.DriGraph driGraph = new PSetDefs.DriGraph();

                    foreach (var item in allEnts)
                    {
                        psm.GetOrAttachPropertySet(item);
                        psm.WritePropertyString(driGraph.ConnectedEntities, "");
                    }

                }
                catch (System.Exception ex)
                {
                    prdDbg(ex.ToString());
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("WRITEISOGENATTRIBUTESTODWG")]
        public void writeisogenattributestodwg()
        {
            IntersectUtilities.IsogenPopulateAttributes.WriteIsogenAttrubutesToDwg();
        }

        [CommandMethod("SELECTBYPS")]
        public void selectbyps()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            PromptStringOptions opts1 = new PromptStringOptions("\nEnter name of Property Set: ");
            opts1.AllowSpaces = true;
            PromptResult pr1 = editor.GetString(opts1);
            string propertySetName = "";
            if (pr1.Status != PromptStatus.OK) return;
            else propertySetName = pr1.StringResult;

            PromptStringOptions opts2 = new PromptStringOptions("\nEnter name of Property: ");
            opts2.AllowSpaces = true;
            PromptResult pr2 = editor.GetString(opts2);
            string propertyName = "";
            if (pr2.Status != PromptStatus.OK) return;
            else propertyName = pr2.StringResult;

            PromptStringOptions opts3 = new PromptStringOptions("\nEnter data to search: ");
            opts3.AllowSpaces = true;
            PromptResult pr3 = editor.GetString(opts3);
            string data = "";
            if (pr3.Status != PromptStatus.OK) return;
            else data = pr3.StringResult;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    prdDbg("Frozen entities are discarded!");
                    var ents = localDb
                        .ListOfType<Entity>(tx, true)
                        .Where(x => PropertySetManager.ReadNonDefinedPropertySetString(
                            x, propertySetName, propertyName) == data);

                    editor.SetImpliedSelection(ents.Select(x => x.Id).ToArray());
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

        [CommandMethod("COLORENTSMISSINGPS")]
        public void colorentsmissingps()
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
                    var ents = localDb.ListOfType<Polyline>(tx);
                    var manglerPs = new List<Entity>();
                    foreach (var item in ents)
                    {
                        ObjectIdCollection propertySetIds = PropertyDataServices.GetPropertySets(item);

                        if (propertySetIds.Count == 0)
                        {
                            manglerPs.Add(item);
                        }
                        else
                        {
                            bool foundPs = false;
                            foreach (Oid oid in propertySetIds)
                            {
                                PropertySet ps = oid.Go<PropertySet>(tx);
                                if (ps.PropertySetDefinitionName == "DriGasDimOgMat") foundPs = true;
                            }

                            if (foundPs == false) manglerPs.Add(item);
                        }
                    }

                    foreach (var item in manglerPs)
                    {
                        item.CheckOrOpenForWrite();
                        item.Color = ColorByName("white");
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

        [CommandMethod("DIVIDEPLINE")]
        public void dividepline()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            Oid pId = Interaction.GetEntity("Select pline to divide: ", typeof(Polyline));
            int nrOfSegments = Interaction.GetInteger("Enter number of segments to create: ");

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    Polyline originalPline = pId.Go<Polyline>(tx);
                    double length = originalPline.Length;

                    double segmentLength = length / nrOfSegments;

                    for (int i = 0; i < nrOfSegments; i++)
                    {
                        double startLength = i * segmentLength;
                        double endLength = (i + 1) * segmentLength;
                        Polyline newPline = new Polyline(2);
                        newPline.AddVertexAt(0,
                            originalPline.GetPointAtDist(startLength).To2D(), 0, 0, 0);
                        newPline.AddVertexAt(1,
                            originalPline.GetPointAtDist(endLength).To2D(), 0, 0, 0);
                        newPline.AddEntityToDbModelSpace(localDb);
                        newPline.Layer = originalPline.Layer;
                        //PropertySetManager.CopyAllProperties(originalPline, newPline);
                    }

                    originalPline.CheckOrOpenForWrite();
                    originalPline.Erase(true);
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

        [CommandMethod("COUNTENTS")]
        public void countents()
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
                    var blockTable = (BlockTable)tx.GetObject(localDb.BlockTableId, OpenMode.ForRead);

                    // Get the model space block table record
                    var modelSpace = (BlockTableRecord)tx.GetObject(
                        blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                    RXClass theClass = RXObject.GetClass(typeof(Entity));

                    int count = 0;

                    // Loop through the entities in model space
                    foreach (Oid objectId in modelSpace)
                    {
                        // Look for entities of the correct type
                        if (objectId.ObjectClass.IsDerivedFrom(theClass))
                        {
                            count++;
                        }
                    }
                    prdDbg($"Total number of Entities in DWG: {count}");
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

        [CommandMethod("PIPELAYERSCOLOURSET")]
        public void pipelayerscolourset()
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
                    LayerTable lt = localDb.LayerTableId.Go<LayerTable>(tx);
                    List<LayerTableRecord> pipeLtrs = new List<LayerTableRecord>();

                    foreach (var ltrid in lt)
                    {
                        LayerTableRecord ltr = ltrid.Go<LayerTableRecord>(tx);
                        if (PipeSchedule.GetPipeDN(ltr.Name) != 999) pipeLtrs.Add(ltr);
                    }
                    pipeLtrs = pipeLtrs.OrderBy(x => x.Name).ToList();

                    prdDbg($"Number of pipe layers in drawing: {pipeLtrs.Count}");

                    List<Color> colors = new List<Color>();
                    #region HSL H stepping method, produces many similar-looking colors
                    //Random random = new Random();
                    //for (int h = 0; h < 360; h += 360 / pipeLtrs.Count)
                    //{
                    //    double hue = h;
                    //    double saturation = (90.0 + random.Next(0, 11)) / 100.0;
                    //    //double saturation = random.NextDouble();
                    //    //double lightness = random.NextDouble();
                    //    double lightness = (25.0 + random.Next(0, 36)) / 100.0;
                    //    prdDbg($"HSL: {hue}, {saturation}, {lightness}");
                    //    System.Drawing.Color color = ColorUtils.SimpleColorTransforms.HsLtoRgb(hue, saturation, lightness);
                    //    prdDbg(color.ToString());
                    //    colors.Add(Color.FromRgb(color.R, color.G, color.B));
                    //} 
                    #endregion

                    if (pipeLtrs.Count > ColorUtils.StaticColorList.StaticColors.Count)
                        throw new System.Exception($"There are {pipeLtrs.Count} layers and only " +
                            $"{ColorUtils.StaticColorList.StaticColors.Count} colours!");

                    for (int j = 0; j < pipeLtrs.Count; j++)
                    {
                        string colorString = ColorUtils.StaticColorList.StaticColors[j];
                        System.Drawing.Color color = System.Drawing.ColorTranslator.FromHtml(colorString);
                        colors.Add(Color.FromRgb(color.R, color.G, color.B));
                    }

                    var zip = pipeLtrs.Zip(colors, (x, y) => new { layer = x, color = y });

                    #region Determine upper right corner of all polylines: Point3d UpperRightCorner
                    //To place legend find bbox of all plines and place legend at the top right corner
                    HashSet<Polyline> allPlines = localDb.HashSetOfType<Polyline>(tx)
                        .Where(x => PipeSchedule.GetPipeDN(x) != 999).ToHashSet();

                    //Determine maximum points
                    HashSet<double> Xs = new HashSet<double>();
                    HashSet<double> Ys = new HashSet<double>();

                    foreach (var pl in allPlines)
                    {
                        Extents3d bbox = pl.GeometricExtents;
                        Xs.Add(bbox.MaxPoint.X);
                        Xs.Add(bbox.MinPoint.X);
                        Ys.Add(bbox.MaxPoint.Y);
                        Ys.Add(bbox.MinPoint.Y);
                    }

                    double maxX = Xs.Max();
                    double maxY = Ys.Max();

                    Point3d upperRightCorner = new Point3d(maxX, maxY, 0);
                    #endregion

                    //Assign colors and create legend
                    int i = 0;
                    foreach (var pair in zip.OrderByAlphaNumeric(x => x.layer.Name))
                    {
                        Color color = pair.color;
                        pair.layer.CheckOrOpenForWrite();
                        pair.layer.Color = color;

                        //Create legend
                        double vDist = 10;
                        Point3d p1 = new Point3d(upperRightCorner.X, upperRightCorner.Y + vDist * i, 0.0);
                        Point3d p2 = new Point3d(upperRightCorner.X + 50.0, upperRightCorner.Y + vDist * i, 0.0);
                        Line line = new Line(p1, p2);
                        line.AddEntityToDbModelSpace(localDb);
                        line.SetDatabaseDefaults();
                        line.Layer = "0";
                        line.Color = color;

                        DBText text = new DBText();
                        text.AddEntityToDbModelSpace(localDb);
                        text.SetDatabaseDefaults();
                        text.Position = new Point3d(p2.X + 10.0, p2.Y, 0.0);
                        text.Height = 5;
                        text.Color = color;
                        text.TextString =
                            $"DN{PipeSchedule.GetPipeDN(pair.layer.Name)} " +
                            $"{PipeSchedule.GetPipeType(pair.layer.Name)}";

                        i--;
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

        [CommandMethod("PIPELAYERSCOLOURRESET")]
        public void pipelayerscolourreset()
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
                    LayerTable lt = localDb.LayerTableId.Go<LayerTable>(tx);
                    List<LayerTableRecord> pipeLtrs = new List<LayerTableRecord>();

                    foreach (var ltrid in lt)
                    {
                        LayerTableRecord ltr = ltrid.Go<LayerTableRecord>(tx);
                        if (PipeSchedule.GetPipeDN(ltr.Name) != 999) pipeLtrs.Add(ltr);
                    }
                    pipeLtrs = pipeLtrs.OrderBy(x => x.Name).ToList();

                    prdDbg($"Number of pipe layers in drawing: {pipeLtrs.Count}");

                    //Assign colors and create legend
                    foreach (var ltr in pipeLtrs)
                    {
                        ltr.CheckOrOpenForWrite();
                        PipeTypeEnum type = PipeSchedule.GetPipeType(ltr.Name);

                        switch (type)
                        {
                            case PipeTypeEnum.Ukendt:
                                throw new System.Exception($"Unexpected type {type}!");
                            case PipeTypeEnum.Twin:
                                ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 6);
                                break;
                            case PipeTypeEnum.Frem:
                            case PipeTypeEnum.Retur:
                                ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                break;
                            default:
                                throw new System.Exception($"Unexpected type {type}!");
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

        //[CommandMethod("CLEANLAGFILE")]
        public void cleanlagfile()
        {
            System.Data.DataTable dupes = CsvReader.ReadCsvToDataTable(@"X:\AutoCAD DRI - 01 Civil 3D\Lag-duplicates.csv", "LagDupes");

            var list = new HashSet<string>();

            //hashset will not allow duplicate strings to be added thus effectively making the list distinct
            foreach (DataRow row in dupes.Rows) list.Add($"{row[0]};{row[1]};{row[2]}");

            OutputWriter(@"X:\AutoCAD DRI - 01 Civil 3D\Lag-clean.csv", string.Join("\n", list.ToArray()), true);
        }

        [CommandMethod("FIXLERLAYERS")]
        public void fixlerlayers()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            fixlerlayersmethod(localDb);
        }
        public void fixlerlayersmethod(Database localDb)
        {
            string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";
            string pathLag = "X:\\AutoCAD DRI - 01 Civil 3D\\Lag.csv";

            System.Data.DataTable dtK = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
            System.Data.DataTable dtLag = CsvReader.ReadCsvToDataTable(pathLag, "Lag");

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    LayerTable lt = localDb.LayerTableId.Go<LayerTable>(localDb.TransactionManager.TopTransaction);

                    //Cache layers in memory
                    HashSet<LayerTableRecord> layers = new HashSet<LayerTableRecord>();
                    foreach (Oid lid in lt) layers.Add(lid.Go<LayerTableRecord>(tx));

                    #region Prepare linetypes
                    LinetypeTable ltt = (LinetypeTable)localDb.TransactionManager.TopTransaction
                            .GetObject(localDb.LinetypeTableId, OpenMode.ForWrite);

                    //Lookup linetype specification and link to layer name
                    Dictionary<string, string> layerLineTypeMap = new Dictionary<string, string>();
                    foreach (var layer in layers)
                    {
                        string targetLayerName = ReadStringParameterFromDataTable(layer.Name, dtK, "Layer", 0);
                        string lineTypeName = ReadStringParameterFromDataTable(targetLayerName, dtLag, "LineType", 0);
                        if (lineTypeName.IsNoE()) prdDbg($"LineTypeName is missing for {layer.Name}!");
                        layerLineTypeMap.Add(layer.Name, lineTypeName);
                    }

                    //Check if all line types are present
                    HashSet<string> missingLineTypes = new HashSet<string>();
                    foreach (var layer in layers)
                    {
                        string lineTypeName = layerLineTypeMap[layer.Name];
                        if (lineTypeName.IsNoE()) continue;
                        if (!ltt.Has(lineTypeName)) missingLineTypes.Add(lineTypeName);
                    }

                    if (missingLineTypes.Count > 0)
                    {
                        Database ltDb = new Database(false, true);
                        ltDb.ReadDwgFile("X:\\AutoCAD DRI - 01 Civil 3D\\Projection_styles.dwg",
                            FileOpenMode.OpenForReadAndAllShare, false, null);
                        Transaction ltTx = ltDb.TransactionManager.StartTransaction();

                        Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);

                        LinetypeTable sourceLtt = (LinetypeTable)ltDb.TransactionManager.TopTransaction
                            .GetObject(ltDb.LinetypeTableId, OpenMode.ForRead);
                        ObjectIdCollection idsToClone = new ObjectIdCollection();

                        foreach (string missingName in missingLineTypes) idsToClone.Add(sourceLtt[missingName]);

                        IdMapping mapping = new IdMapping();
                        ltDb.WblockCloneObjects(idsToClone, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);
                        ltTx.Commit();
                        ltTx.Dispose();
                        ltDb.Dispose();
                    }
                    #endregion

                    foreach (LayerTableRecord ltr in layers)
                    {
                        string layerName = ltr.Name;
                        if (!dtK.AsEnumerable().Any(row => row[0].ToString() == layerName))
                        {
                            prdDbg($"UNKNOWN LAYER: {ltr.Name}");
                            continue;
                        }

                        string type = ReadStringParameterFromDataTable(layerName, dtK, "Type", 0);
                        if (type == "IGNORE") { prdDbg($"Layer {layerName} IGNORED!"); continue; }
                        string lerLayerName = ReadStringParameterFromDataTable(layerName, dtK, "Layer", 0);

                        if (!dtLag.AsEnumerable().Any(row => row[0].ToString() == lerLayerName))
                        {
                            prdDbg($"Ler layer {lerLayerName} not found in Lag.csv! Tilføj laget i Lag.csv.");
                            continue;
                        }

                        string farveString = ReadStringParameterFromDataTable(lerLayerName, dtLag, "Farve", 0);

                        Color color = UtilsCommon.Utils.ParseColorString(farveString);
                        if (color == null)
                        {
                            prdDbg($"Failed to read color for layer name {lerLayerName} with colorstring {farveString}. Skipping!");
                            continue;
                        }

                        ltr.CheckOrOpenForWrite();
                        ltr.Color = color;

                        #region Read and assign layer's linetype
                        Oid lineTypeId;

                        string lineTypeName = layerLineTypeMap[layerName];
                        if (lineTypeName.IsNoE())
                        {
                            prdDbg($"WARNING! Layer name {layerName} does not have a line type specified!.");
                            //If linetype string is NoE -> CONTINUOUS linetype must be used
                            lineTypeId = ltt["Continuous"];
                        }
                        else
                        {
                            //the presence of the linetype is assured in previous section.
                            lineTypeId = ltt[lineTypeName];
                        }

                        ltr.LinetypeObjectId = lineTypeId;
                        #endregion
                    }

                    #region Set objects to byLayer
                    var list = localDb.ListOfType<Entity>(tx);
                    foreach (Entity item in list)
                    {
                        if (item is Polyline || item is Polyline3d)
                        {
                            item.CheckOrOpenForWrite();
                            item.Color = Color.FromColorIndex(ColorMethod.ByAci, 256);
                            item.Linetype = "ByLayer";
                        }
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex.ToString());
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("LISTDESCRIPTIONS")]
        public void listdescriptions()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    HashSet<Polyline3d> p3ds = localDb.HashSetOfType<Polyline3d>(tx);
                    string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";
                    System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
                    HashSet<string> layerNames = new HashSet<string>();

                    foreach (var p3d in p3ds)
                    {
                        layerNames.Add(p3d.Layer);
                    }

                    var sorted = layerNames.OrderBy(x => x);

                    foreach (var layerName in sorted)
                    {
                        string description = ReadStringParameterFromDataTable(
                            layerName, dtKrydsninger, "Description", 0);

                        if (description.IsNoE()) prdDbg($"{layerName};{description}");
                    }

                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex.ToString());
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("OVERLAPCOMPARISON")]
        public void overlapcomparison()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Document doc = docCol.CurrentDocument;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor ed = docCol.CurrentDocument.Editor;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Select xref and open database
                    PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions("\n Select XREF to compare: ");
                    promptEntityOptions1.SetRejectMessage("\n Not a XREF");
                    promptEntityOptions1.AddAllowedClass(typeof(Autodesk.AutoCAD.DatabaseServices.BlockReference), true);
                    PromptEntityResult entity1 = ed.GetEntity(promptEntityOptions1);
                    if (((PromptResult)entity1).Status != PromptStatus.OK) AbortGracefully(tx, "No input!");
                    Oid blkObjId = entity1.ObjectId;
                    BlockReference blkRef = tx.GetObject(blkObjId, OpenMode.ForRead, false) as BlockReference;

                    BlockTableRecord blockDef = tx.GetObject(blkRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                    if (!blockDef.IsFromExternalReference) AbortGracefully(tx, "Selected object is not an XREF!");

                    // open the xref database
                    Database xrefDb = new Database(false, true);
                    prdDbg($"\nPathName of the blockDef -> {blockDef.PathName}");

                    //Relative path handling
                    string curPathName = blockDef.PathName;
                    bool isFullPath = IsFullPath(curPathName);
                    if (isFullPath == false)
                    {
                        string sourcePath = Path.GetDirectoryName(doc.Name);
                        prdDbg($"\nSourcePath -> {sourcePath}");
                        curPathName = GetAbsolutePath(sourcePath, blockDef.PathName);
                        prdDbg($"\nTargetPath -> {curPathName}");
                    }

                    xrefDb.ReadDwgFile(curPathName, FileOpenMode.OpenForReadAndWriteNoShare, false, string.Empty);
                    #endregion

                    //Transaction from Database of the Xref
                    using (Transaction xrefTx = xrefDb.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            var remotePlines = xrefDb.HashSetOfType<Polyline>(xrefTx);
                            prdDbg($"Number of polylines in remote database: {remotePlines.Count}");
                            var localPlines = localDb.HashSetOfType<Polyline>(tx);
                            prdDbg($"Number of polylines in local database: {localPlines.Count}");
                            var remotePoints = xrefDb.HashSetOfType<DBPoint>(xrefTx);
                            prdDbg($"Number of points in remote database: {remotePoints.Count}");
                            var localPoints = localDb.HashSetOfType<DBPoint>(tx);
                            prdDbg($"Number of points in local database: {localPoints.Count}");

                            int remotePartial = 0;
                            int localPartial = 0;
                            int remoteFull = 0;
                            int localFull = 0;
                            int duplicatePoints = 0;

                            foreach (var remotePline in remotePlines)
                            {
                                foreach (var localPline in localPlines)
                                {
                                    var overlap = GetOverlapStatus(remotePline, localPline);

                                    switch (overlap)
                                    {
                                        case OverlapStatusEnum.None:
                                            break;
                                        case OverlapStatusEnum.Partial:
                                            remotePline.CheckOrOpenForWrite();
                                            remotePline.Color = ColorByName("yellow");
                                            remotePartial++;
                                            break;
                                        case OverlapStatusEnum.Full:
                                            remotePline.CheckOrOpenForWrite();
                                            remotePline.Color = ColorByName("red");
                                            remoteFull++;
                                            break;
                                        default:
                                            break;
                                    }
                                }
                            }

                            foreach (var localPline in localPlines)
                            {
                                foreach (var remotePline in remotePlines)
                                {
                                    var overlap = GetOverlapStatus(localPline, remotePline);

                                    switch (overlap)
                                    {
                                        case OverlapStatusEnum.None:
                                            break;
                                        case OverlapStatusEnum.Partial:
                                            localPline.CheckOrOpenForWrite();
                                            localPline.Color = ColorByName("yellow");
                                            localPartial++;
                                            break;
                                        case OverlapStatusEnum.Full:
                                            localPline.CheckOrOpenForWrite();
                                            localPline.Color = ColorByName("red");
                                            localFull++;
                                            break;
                                        default:
                                            break;
                                    }
                                }
                            }

                            foreach (var localPoint in localPoints)
                            {
                                foreach (var remotePoint in remotePoints)
                                {
                                    if (localPoint.Position.IsEqualTo(remotePoint.Position, Tolerance.Global))
                                    {
                                        localPoint.CheckOrOpenForWrite();
                                        localPoint.Color = ColorByName("magenta");
                                        remotePoint.CheckOrOpenForWrite();
                                        remotePoint.Color = ColorByName("magenta");
                                        duplicatePoints++;
                                    }
                                }
                            }

                            prdDbg(
                                $"Remote -> Partial overlaps {remotePartial}, Full overlaps {remoteFull}\n" +
                                $"Local -> Partial overlaps {localPartial}, Full overlaps {localFull}\n" +
                                $"Points -> Duplicates {duplicatePoints}");
                        }
                        catch (System.Exception ex)
                        {
                            xrefTx.Abort();
                            xrefDb.Dispose();
                            throw;
                        }
                        xrefTx.Commit();
                    }

                    xrefDb.SaveAs(xrefDb.Filename, true, DwgVersion.Current, null);
                    xrefDb.Dispose();
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex.ToString());
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("OVERLAPRESET")]
        public void overlapreset()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Document doc = docCol.CurrentDocument;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor ed = docCol.CurrentDocument.Editor;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Select xref and open database
                    PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions("\n Select XREF to reset: ");
                    promptEntityOptions1.SetRejectMessage("\n Not a XREF");
                    promptEntityOptions1.AddAllowedClass(typeof(Autodesk.AutoCAD.DatabaseServices.BlockReference), true);
                    PromptEntityResult entity1 = ed.GetEntity(promptEntityOptions1);
                    if (((PromptResult)entity1).Status != PromptStatus.OK) AbortGracefully(tx, "No input!");
                    Oid blkObjId = entity1.ObjectId;
                    BlockReference blkRef = tx.GetObject(blkObjId, OpenMode.ForRead, false) as BlockReference;

                    BlockTableRecord blockDef = tx.GetObject(blkRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                    if (!blockDef.IsFromExternalReference) AbortGracefully(tx, "Selected object is not an XREF!");

                    // open the xref database
                    Database xrefDb = new Database(false, true);
                    prdDbg($"\nPathName of the blockDef -> {blockDef.PathName}");

                    //Relative path handling
                    string curPathName = blockDef.PathName;
                    bool isFullPath = IsFullPath(curPathName);
                    if (isFullPath == false)
                    {
                        string sourcePath = Path.GetDirectoryName(doc.Name);
                        prdDbg($"\nSourcePath -> {sourcePath}");
                        curPathName = GetAbsolutePath(sourcePath, blockDef.PathName);
                        prdDbg($"\nTargetPath -> {curPathName}");
                    }

                    xrefDb.ReadDwgFile(curPathName, FileOpenMode.OpenForReadAndWriteNoShare, false, string.Empty);
                    #endregion

                    //Transaction from Database of the Xref
                    using (Transaction xrefTx = xrefDb.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            var remotePlines = xrefDb.HashSetOfType<Polyline>(xrefTx);
                            prdDbg($"Number of polylines in remote database: {remotePlines.Count}");
                            var localPlines = localDb.HashSetOfType<Polyline>(tx);
                            prdDbg($"Number of polylines in local database: {localPlines.Count}");
                            var remotePoints = xrefDb.HashSetOfType<DBPoint>(xrefTx);
                            prdDbg($"Number of points in remote database: {remotePoints.Count}");
                            var localPoints = localDb.HashSetOfType<DBPoint>(tx);
                            prdDbg($"Number of points in local database: {localPoints.Count}");

                            foreach (var remotePline in remotePlines)
                            {
                                remotePline.CheckOrOpenForWrite();
                                remotePline.Color = ColorByName("bylayer");
                            }

                            foreach (var localPline in localPlines)
                            {
                                localPline.CheckOrOpenForWrite();
                                localPline.Color = ColorByName("bylayer");
                            }

                            foreach (var localPoint in localPoints)
                            {
                                localPoint.CheckOrOpenForWrite();
                                localPoint.Color = ColorByName("bylayer");
                            }

                            foreach (var remotePoint in remotePoints)
                            {
                                remotePoint.CheckOrOpenForWrite();
                                remotePoint.Color = ColorByName("bylayer");
                            }

                        }
                        catch (System.Exception ex)
                        {
                            xrefTx.Abort();
                            xrefDb.Dispose();
                            throw;
                        }
                        xrefTx.Commit();
                    }

                    xrefDb.SaveAs(xrefDb.Filename, true, DwgVersion.Current, null);
                    xrefDb.Dispose();
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex.ToString());
                    return;
                }
                tx.Commit();
            }
        }

        /// <summary>
        /// Command for GAS points in Køge, where elevations are very low and we cannot discern between zero elevation and none elevation.
        /// So the solution is to move all points that are at precisely 0.000 to -99 and then they can be deleted.
        /// </summary>
        [CommandMethod("MOVEZEROPOINTSTO99")]
        public void movezeropointsto99()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Document doc = docCol.CurrentDocument;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor ed = docCol.CurrentDocument.Editor;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    //Transaction from Database of the Xref

                    var localPoints = localDb.HashSetOfType<DBPoint>(tx);
                    prdDbg($"Number of points in local database: {localPoints.Count}");

                    //List<DBPoint> elevations = new List<DBPoint>();
                    foreach (var localPoint in localPoints)
                    {
                        double elevation = localPoint.Position.Z;
                        //if (elevation > -0.001 && elevation < 0.001) elevations.Add(localPoint);
                        if (localPoint.Layer != "LABEL" && elevation > -0.001 && elevation < 0.001)
                        {
                            localPoint.CheckOrOpenForWrite();
                            localPoint.Position =
                                new Point3d(localPoint.Position.X, localPoint.Position.Y, -99.0);
                        }

                    }

                    //var groups = elevations.GroupBy(x => x);

                    //foreach (var item in groups.OrderBy(x => x.Key))
                    //{
                    //    prdDbg($"Key: {item.Key}, Count: {item.Count()}");
                    //}
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex.ToString());
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("LISTGW")]
        public void listallplineslayers()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Document doc = docCol.CurrentDocument;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var id = Interaction.GetEntity("Select polyline to list GW: ", typeof(Polyline), true);
                    Polyline pline = id.Go<Polyline>(tx);
                    prdDbg(pline.ConstantWidth);
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("EXPORTBLOCKSPSDATATOCSV")]
        public void exportblockspsdatatocsv()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Document doc = docCol.CurrentDocument;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var brs = localDb.HashSetOfType<BlockReference>(tx, true);

                    StringBuilder sb = new StringBuilder();

                    PropertySetManager psMan = new PropertySetManager(localDb, PSetDefs.DefinedSets.BBR);
                    PSetDefs.BBR bbrDef = new PSetDefs.BBR();

                    var propList = bbrDef.ListOfProperties();

                    foreach (var item in propList)
                    {
                        sb.Append(item.Name + ";");
                    }
                    sb.Append("X;Y;");
                    sb.AppendLine();

                    foreach (var br in brs)
                    {
                        foreach (var prop in propList)
                        {
                            sb.Append(psMan.ReadPropertyString(br, prop) + ";");
                        }
                        sb.Append(br.Position.X.ToString()+";");
                        sb.Append(br.Position.Y.ToString()+";");
                        sb.AppendLine();
                    }

                    Utils.OutputWriter(
                        @"X:\108 - 1249 - VEKS projektledelse - Dokumenter\01 Intern\" +
                        @"15 GIS\01 Tranegilde\05 Analyse\BBR.csv", sb.ToString(), true);
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }

        //[CommandMethod("TESTENUMS")]
        public void testenums()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Document doc = docCol.CurrentDocument;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    TestEnum testEnum = TestEnum.None
                        //| TestEnum.TestSecond
                        //| TestEnum.TestThird
                        ;

                    testEnum |= TestEnum.TestFirst;
                    testEnum |= TestEnum.TestSecond;
                    testEnum |= TestEnum.TestThird;
                    //testEnum |= TestEnum.TestFourth;

                    prdDbg((testEnum & TestEnum.TestFourth) == 0);

                    prdDbg(testEnum.ToString());
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }

        [Flags]
        private enum TestEnum
        {
            None = 0,
            TestFirst = 1,
            TestSecond = 2,
            TestThird = 4,
            TestFourth = 8,
            TestFith = 16,
        }

        void AbortGracefully(Transaction tx, string msg)
        {
            tx.Abort();
            prdDbg(msg);
        }

        void AbortGracefully(Transaction[] txs, Database[] dbs, string exMsg)
        {
            foreach (Transaction tx in txs)
            {
                tx.Abort();
                tx.Dispose();
            }

            foreach (var db in dbs)
            {
                db.Dispose();
            }
            prdDbg(exMsg);
        }
    }
}