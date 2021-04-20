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
using MoreLinq;
using System.Text;
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

                    oid fId = label.FeatureId;
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
                    pKeyOpts.Message = "\nChoose elevation input method: ";
                    pKeyOpts.Keywords.Add(kwd1);
                    pKeyOpts.Keywords.Add(kwd2);
                    pKeyOpts.AllowNone = true;
                    pKeyOpts.Keywords.Default = kwd1;
                    PromptResult pKeyRes = editor.GetKeywords(pKeyOpts);
                    string workingDrawing = pKeyRes.StringResult;
                    #endregion

                    string etapeName = GetEtapeName(editor);

                    HashSet<Entity> allLinework = new HashSet<Entity>();
                    HashSet<Alignment> alignments = new HashSet<Alignment>();

                    if (workingDrawing == kwd2)
                    {
                        #region Load linework from LER Xref
                        editor.WriteMessage("\n" + GetPathToDataFiles(etapeName, "Ler"));

                        // open the LER dwg database
                        Database xRefLerDB = new Database(false, true);

                        xRefLerDB.ReadDwgFile(GetPathToDataFiles(etapeName, "Ler"),
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
                        editor.WriteMessage("\n" + GetPathToDataFiles(etapeName, "Alignments"));

                        // open the LER dwg database
                        Database xRefAlsDB = new Database(false, true);

                        xRefAlsDB.ReadDwgFile(GetPathToDataFiles(etapeName, "Alignments"),
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

                        foreach (Alignment al in alignments)
                        {
                            editor.WriteMessage($"\n++++++++ Indlæser alignment {al.Name}. ++++++++");

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
                string etapeName = GetEtapeName(editor);

                editor.WriteMessage("\n" + GetPathToDataFiles(etapeName, "Alignments"));

                #region Load alignments from drawing
                HashSet<Alignment> alignments = null;
                //To be able to check local or external alignments following hack is implemented
                alignments = localDb.HashSetOfType<Alignment>(tx);

                // open the LER dwg database
                Database xRefAlsDB = new Database(false, true);

                xRefAlsDB.ReadDwgFile(GetPathToDataFiles(etapeName, "Alignments"),
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

                    oid prStId = stc["PROFILE PROJEKTION MGO"];

                    foreach (Entity ent in allEnts)
                    {
                        if (ent is Label label)
                        {
                            label.CheckOrOpenForWrite();
                            label.StyleId = prStId;

                            oid fId = label.FeatureId;
                            Entity fEnt = fId.Go<Entity>(tx);

                            int diaOriginal = ReadIntPropertyValue(tables, fEnt.Id, "CrossingData", "Diameter");
                            prdDbg(fEnt.Handle.ToString() + ": " + diaOriginal.ToString());

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

                        if (DoesRecordExist(tables, ent.ObjectId, "Id"))
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

            while (true)
            {
                using (Transaction tx = localDb.TransactionManager.StartTransaction())
                {
                    try
                    {
                        LayerTable lt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;

                        #region Select pline3d
                        PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                            "\nSelect polyline3d to modify:");
                        promptEntityOptions1.SetRejectMessage("\n Not a polyline3d!");
                        promptEntityOptions1.AddAllowedClass(typeof(Polyline3d), true);
                        PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                        if (((PromptResult)entity1).Status != PromptStatus.OK) { tx.Abort(); return; }
                        Autodesk.AutoCAD.DatabaseServices.ObjectId pline3dId = entity1.ObjectId;
                        #endregion

                        #region Select text
                        PromptEntityOptions promptEntityOptions2 = new PromptEntityOptions(
                            "\nSelect text to copy:");
                        promptEntityOptions2.SetRejectMessage("\n Not a text!");
                        promptEntityOptions2.AddAllowedClass(typeof(DBText), true);
                        PromptEntityResult entity2 = editor.GetEntity(promptEntityOptions2);
                        if (((PromptResult)entity2).Status != PromptStatus.OK) { tx.Abort(); return; }
                        Autodesk.AutoCAD.DatabaseServices.ObjectId textId = entity2.ObjectId;
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

                        string m_tableName = "GasDimOgMat";

                        string[] columnNames = new string[3] { "Dimension", "Material", "Bemærk" };
                        string[] columnDescriptions = new string[3] { "Pipe diameter", "Pipe material", "Bemærkning til ledning" };
                        Autodesk.Gis.Map.Constants.DataType[] dataTypes = new Autodesk.Gis.Map.Constants.DataType[3]
                        {
                            DataType.Integer,
                            DataType.Character,
                            DataType.Character
                        };
                        MapValue[] values = new MapValue[3];

                        Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

                        if (DoesTableExist(tables, m_tableName))
                        {
                            editor.WriteMessage("\nTable already exists!");

                            if (!DoAllColumnsExist(tables, m_tableName, columnNames))
                            {
                                CreateMissingColumns(tables, m_tableName, columnNames, columnDescriptions, dataTypes);
                            }
                        }
                        else
                        {
                            if (CreateTable(
                                tables, m_tableName, "Gas data", columnNames, columnDescriptions, dataTypes))
                            {
                                editor.WriteMessage($"\nCreated table {m_tableName}.");
                            }
                            else
                            {
                                editor.WriteMessage("\nFailed to create the ObjectData table.");
                                return;
                            }
                        }

                        string value = textId.Go<DBText>(tx).TextString.Trim();

                        int parsedInt = 0;
                        string parsedMat = string.Empty;
                        if (value.Contains(" "))
                        {
                            //Gas specific handling
                            string[] output = value.Split((char[])null); //Splits by whitespace

                            int.TryParse(output[0], out parsedInt);
                            //Material
                            parsedMat = output[1];
                        }
                        else
                        {
                            string[] output = value.Split('/');
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
                            bool success = false;

                            if (DoesRecordExist(tables, pline3dId, columnNames[i]))
                            {
                                editor.WriteMessage($"\nRecord {columnNames[i]} already exists, updating...");

                                if (UpdateODRecord(tables, m_tableName, columnNames[i], pline3dId, values[i]))
                                {
                                    editor.WriteMessage($"\nUpdating record {columnNames[i]} succeded!");
                                    success = true;
                                }
                                else editor.WriteMessage($"\nUpdating record {columnNames[i]} failed!");
                            }
                            else if (AddODRecord(tables, m_tableName, columnNames[i], pline3dId, values[i]))
                            {
                                editor.WriteMessage($"\nAdding record {columnNames[i]} succeded!");
                                success = true;
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
                                    oid ltId = lt.Add(ltr);
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
                            "\nSelect polyline3d to modify:");
                        promptEntityOptions1.SetRejectMessage("\n Not a polyline3d!");
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

                        if (DoesRecordExist(tables, pline3dId, columnName))
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
                                oid ltId = lt.Add(ltr);
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

                    if (DoesRecordExist(tables, Entity.ObjectId, columnNames[0]))
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
                #region Get alignments
                string etapeName = GetEtapeName(editor);

                editor.WriteMessage("\n" + GetPathToDataFiles(etapeName, "Alignments"));

                // open the LER dwg database
                Database xRefAlDB = new Database(false, true);

                xRefAlDB.ReadDwgFile(GetPathToDataFiles(etapeName, "Alignments"),
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
                    string etapeName = GetEtapeName(editor);

                    editor.WriteMessage("\n" + GetPathToDataFiles(etapeName, "Fremtid"));

                    // open the LER dwg database
                    xRefFjvDB = new Database(false, true);

                    xRefFjvDB.ReadDwgFile(GetPathToDataFiles(etapeName, "Fremtid"),
                        System.IO.FileShare.Read, false, string.Empty);
                    xRefFjvTx = xRefFjvDB.TransactionManager.StartTransaction();

                    HashSet<Line> lines = xRefFjvDB.HashSetOfType<Line>(xRefFjvTx);
                    //HashSet<Spline> splines = xRefFjvDB.HashSetOfType<Spline>(xRefLerTx);
                    HashSet<Polyline> plines = xRefFjvDB.HashSetOfType<Polyline>(xRefFjvTx);
                    //HashSet<Polyline3d> plines3d = xRefFjvDB.HashSetOfType<Polyline3d>(xRefLerTx);
                    HashSet<Arc> arcs = xRefFjvDB.HashSetOfType<Arc>(xRefFjvTx);
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

                    // open the LER dwg database
                    using (Database xRefLerDB = new Database(false, true))
                    {
                        xRefLerDB.ReadDwgFile(GetPathToDataFiles(etapeName, "Ler"),
                                                            System.IO.FileShare.Read, false, string.Empty);

                        using (Transaction xRefLerTx = xRefLerDB.TransactionManager.StartTransaction())
                        {
                            try
                            {
                                HashSet<Polyline3d> allLinework = xRefLerDB.HashSetOfType<Polyline3d>(xRefLerTx)
                                                        .Where(x => ReadStringParameterFromDataTable(x.Layer, dtKrydsninger, "Type", 0) != "IGNORE")
                                                        .ToHashSet();

                                PointGroupCollection pgs = civilDoc.PointGroups;

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
                                    oid pvId = oid.Null;

                                    editor.WriteMessage($"\n_-*-_ | Processing alignment {alignment.Name}. | _-*-_");
                                    System.Windows.Forms.Application.DoEvents();

                                    #region Delete existing points


                                    for (int i = 0; i < pgs.Count; i++)
                                    {
                                        PointGroup pg = tx.GetObject(pgs[i], OpenMode.ForRead) as PointGroup;
                                        if (alignment.Name == pg.Name)
                                        {
                                            pg.CheckOrOpenForWrite();
                                            pg.Update();
                                            uint[] numbers = pg.GetPointNumbers();

                                            CogoPointCollection cpc = civilDoc.CogoPoints;

                                            //for (int j = 0; j < numbers.Length; j++)
                                            //{
                                            //    uint number = numbers[j];

                                            //    if (cpc.Contains(number))
                                            //    {
                                            //        cpc.Remove(number);
                                            //    }
                                            //}

                                            StandardPointGroupQuery spgqEmpty = new StandardPointGroupQuery();
                                            spgqEmpty.IncludeNumbers = "";
                                            pg.SetQuery(spgqEmpty);

                                            pg.Update();
                                        }
                                    }
                                    #endregion

                                    HashSet<Polyline3d> filteredLinework = FilterForCrossingEntities(allLinework, alignment);

                                    #region Create profile view
                                    #region Calculate point
                                    Point3d insertionPoint = new Point3d(selectedPoint.X, selectedPoint.Y + index * -120, 0);
                                    #endregion

                                    //If ProfileView already exists -> continue
                                    if (pvSetExisting.Any(x => x.Name == $"{alignment.Name}_PV"))
                                    {
                                        var existingPv = pvSetExisting.Where(x => x.Name == $"{alignment.Name}_PV").FirstOrDefault();
                                        if (existingPv == null) throw new System.Exception("Selection of existing PV failed!");
                                        pvId = existingPv.Id;
                                    }
                                    else
                                    {
                                        pvId = ProfileView.Create(alignment.ObjectId, insertionPoint,
                                            $"{alignment.Name}_PV", profileViewBandSetStyleId, profileViewStyleId);
                                    }
                                    index++;
                                    #endregion

                                    #region Create ler data
                                    using (Transaction loopTx = db.TransactionManager.StartTransaction())
                                    {
                                        try
                                        {
                                            HashSet<oid> oidsToErase = new HashSet<oid>();
                                            createlerdataloopwithdeepclone(filteredLinework, alignment, surface, pvId.Go<ProfileView>(loopTx),
                                                                         dtKrydsninger, dtDybde, xRefLerDB, oidsToErase, loopTx, ref pNames);
                                            foreach (oid OID in oidsToErase)
                                            {
                                                Polyline3d p3d = OID.Go<Polyline3d>(loopTx, OpenMode.ForWrite);
                                                p3d.Erase(true);
                                            }
                                        }
                                        catch (System.Exception e)
                                        {
                                            loopTx.Abort();
                                            xRefLerTx.Abort();
                                            xRefLerDB.Dispose();
                                            xRefSurfaceTx.Abort();
                                            xRefSurfaceDB.Dispose();
                                            editor.WriteMessage($"\n{e.Message}");
                                            return;
                                        }
                                        loopTx.Commit();
                                    }
                                    #endregion
                                }
                            }
                            catch (System.Exception e)
                            {
                                xRefLerTx.Abort();
                                xRefLerDB.Dispose();
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

        public void createlerdataloopwithdeepclone(HashSet<Polyline3d> remoteLinework, Alignment alignment,
                                      CivSurface surface, ProfileView pv,
                                      System.Data.DataTable dtKrydsninger, System.Data.DataTable dtDybde,
                                      Database xRefLerDb, HashSet<oid> oidsToErase, Transaction tx,
                                      ref HashSet<string> pNames)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            try
            {
                #region ModelSpaces
                oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(xRefLerDb);
                oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);

                ObjectIdCollection p3dIds = new ObjectIdCollection();
                foreach (Polyline3d p3d in remoteLinework) p3dIds.Add(p3d.ObjectId);

                IdMapping mapping = new IdMapping();
                xRefLerDb.WblockCloneObjects(p3dIds, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);

                HashSet<Polyline3d> allLinework = localDb.HashSetOfType<Polyline3d>(tx);
                foreach (Polyline3d p3d in allLinework)
                {
                    oidsToErase.Add(p3d.ObjectId);
                }

                #endregion

                #region Prepare variables
                BlockTable acBlkTbl = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord acBlkTblRec =
                    tx.GetObject(acBlkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                Plane plane = new Plane();

                editor.WriteMessage($"\nTotal {allLinework.Count} intersections detected.");

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

                    //prdDbg(localLayerName);

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

                            #region Check, create or update Crossing Data
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

                            #region Create Diameter OD in "CrossingData"
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

                            #region Make sure diameter contains at least zero
                            if (DoesTableExist(tables, "CrossingData"))
                            {
                                if (DoesRecordExist(tables, cogoPoint.ObjectId, "Diameter"))
                                {
                                    UpdateODRecord(tables, "CrossingData", "Diameter",
                                        cogoPoint.ObjectId, new MapValue(0));
                                }
                                else
                                {
                                    AddODRecord(tables, "CrossingData", "Diameter",
                                        cogoPoint.ObjectId, new MapValue(0));
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

                                        AddODRecord(tables, "CrossingData", "Diameter",
                                            cogoPoint.ObjectId, originalValue);

                                    }
                                }
                            }
                            #endregion

                            #region Create alignment "CrossingData"
                            if (DoesTableExist(tables, "CrossingData"))
                            {
                                if (DoesRecordExist(tables, cogoPoint.ObjectId, "Diameter"))
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
                    elMin = tryGetMin - 1;
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
                string etapeName = GetEtapeName(editor);

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

                try
                {
                    List<Alignment> allAlignments = db.ListOfType<Alignment>(tx).OrderBy(x => x.Name).ToList();

                    #region Create surface profiles

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
                                                profileName, alignment.ObjectId, surface.ObjectId,
                                                terrainLayerId, profileStyleId, profileLabelSetStyleId);
                            editor.WriteMessage($"\nSurface profile created for {alignment.Name}.");
                        }
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
                string etapeName = GetEtapeName(editor);

                editor.WriteMessage("\n" + GetPathToDataFiles(etapeName, "Ler"));
                editor.WriteMessage("\n" + GetPathToDataFiles(etapeName, "Fremtid"));

                #region Load linework from LER Xref
                // open the LER dwg database
                Database xRefLerDB = new Database(false, true);

                xRefLerDB.ReadDwgFile(GetPathToDataFiles(etapeName, "Ler"),
                    System.IO.FileShare.Read, false, string.Empty);
                Transaction xRefLerTx = xRefLerDB.TransactionManager.StartTransaction();
                #endregion

                #region Load linework from Fremtid Xref
                // open the Fremtid dwg database
                Database xRefFremtidDB = new Database(false, true);

                xRefFremtidDB.ReadDwgFile(GetPathToDataFiles(etapeName, "Fremtid"),
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
                    //foreach (oid Oid in btr)
                    //{
                    //    if (Oid.ObjectClass.Name == "AcDbBlockReference")
                    //    {
                    //        BlockReference br = Oid.Go<BlockReference>(tx);
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

                    foreach (oid Oid in btr)
                    {
                        if (Oid.ObjectClass.Name == "AcDbBlockReference")
                        {
                            BlockReference br = Oid.Go<BlockReference>(tx);

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
                    foreach (oid Oid in Ids)
                    {
                        if (Oid.ObjectClass.Name != "AcDbBlockReference") continue;
                        using (Transaction tx = localDb.TransactionManager.StartTransaction())
                        {
                            try
                            {
                                BlockReference br = Oid.Go<BlockReference>(tx, OpenMode.ForWrite);
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

                    foreach (oid Oid in btr)
                    {
                        if (Oid.ObjectClass.Name == "AcDbBlockReference")
                        {
                            BlockReference br = Oid.Go<BlockReference>(tx);
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

        [CommandMethod("listnonstandardblocknames")]
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

                    foreach (oid Oid in btr)
                    {
                        if (Oid.ObjectClass.Name == "AcDbBlockReference")
                        {
                            BlockReference br = Oid.Go<BlockReference>(tx);
                            //if (!dbNames.Contains(br.Name))
                            //{
                            //    allNamesNotInDb.Add(br.Name);
                            //}

                            string effectiveName = br.IsDynamicBlock ?
                                                                "*-> " + ((BlockTableRecord)tx.GetObject(
                                                                    br.DynamicBlockTableRecord, OpenMode.ForRead)).Name : br.Name;

                            if (ReadStringParameterFromDataTable(br.Name, fjvKomponenter, "Navn", 0) == null)
                            {
                                allNamesNotInDb.Add(effectiveName);
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

        [CommandMethod("listanonblocks")]
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

                    foreach (oid Oid in btr)
                    {
                        if (Oid.ObjectClass.Name == "AcDbBlockReference")
                        {
                            BlockReference br = Oid.Go<BlockReference>(tx);

                            if (br.Name.StartsWith("*"))
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
                        foreach (oid Oid in Ids)
                        {
                            if (Oid.ObjectClass.Name == "AcDbBlockReference")
                            {
                                BlockReference br = Oid.Go<BlockReference>(tx);

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

        [CommandMethod("LISTALLFJVBLOCKS")]
        public static void listallfjvblocks()
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

                            foreach (oid Oid in bt)
                            {
                                BlockTableRecord btr = tx.GetObject(Oid, OpenMode.ForWrite) as BlockTableRecord;

                                if (ReadStringParameterFromDataTable(btr.Name, fjvKomponenter, "Navn", 0) != null &&
                                    ReadStringParameterFromDataTable(btr.Name, fjvKomponenter, "Type", 0) == "Reduktion" &&
                                    bt.Has(btr.Name))
                                {
                                    prdDbg(btr.Name);
                                }
                            }
                            symbTx.Commit();
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

        [CommandMethod("assignblockstoalignments")]
        public void assignblockstoalignments()
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
                        if (lt.Has(al.Name)) ;
                        else
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
                                oid ltId = lt.Add(ltr);
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

                    foreach (oid Oid in btr)
                    {
                        if (Oid.ObjectClass.Name == "AcDbBlockReference")
                        {
                            BlockReference br = Oid.Go<BlockReference>(tx);
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
                                    foreach (oid attOid in atts)
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

                    foreach (oid Oid in btrMs)
                    {
                        if (Oid.ObjectClass.Name == "AcDbBlockReference")
                        {
                            BlockReference br = Oid.Go<BlockReference>(tx, OpenMode.ForRead);
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

                                    foreach (oid attOid in aCol)
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

                        foreach (oid brOid in brefIds)
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

                    foreach (oid Oid in btrMs)
                    {
                        if (Oid.ObjectClass.Name == "AcDbBlockReference")
                        {
                            BlockReference br = Oid.Go<BlockReference>(tx, OpenMode.ForRead);
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

                    foreach (oid Oid in btrMs)
                    {
                        if (Oid.ObjectClass.Name == "AcDbBlockReference")
                        {
                            BlockReference blkRef = Oid.Go<BlockReference>(tx, OpenMode.ForRead);
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
                        foreach (oid Oid in Ids)
                        {
                            Entity ent = Oid.Go<Entity>(tx, OpenMode.ForWrite);
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

                    oid profileProjection_RIGHT_Style = oid.Null;
                    oid profileProjection_LEFT_Style = oid.Null;

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

                        oid styleId = dirRight ? profileProjection_RIGHT_Style : profileProjection_LEFT_Style;

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

                #region Clone blocks
                string pathToBlockFile = @"X:\AutoCAD DRI - 01 Civil 3D\Projection_styles.dwg";

                #endregion

                #region Set C-ANNO-MTCH-HATCH to frozen
                using (Transaction tx = localDb.TransactionManager.StartTransaction())
                {
                    try
                    {
                        // Open the Layer table for read
                        LayerTable acLyrTbl;
                        acLyrTbl = tx.GetObject(localDb.LayerTableId,
                                                           OpenMode.ForRead) as LayerTable;
                        string sLayerName = "C-ANNO-MTCH-HATCH";
                        LayerTableRecord acLyrTblRec;
                        if (acLyrTbl.Has(sLayerName))
                        {
                            acLyrTblRec = tx.GetObject(acLyrTbl[sLayerName], OpenMode.ForWrite) as LayerTableRecord;
                            // Freeze the layer
                            acLyrTblRec.IsFrozen = true;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        editor.WriteMessage("\n" + ex.Message);
                        tx.Abort();
                        return;
                    }
                    tx.Commit();
                }
                #endregion

                #region Setup styles
                string pathToStyles = @"X:\AutoCAD DRI - 01 Civil 3D\Projection_styles.dwg";

                using (Database stylesDB = new Database(false, true))
                {
                    stylesDB.ReadDwgFile(pathToStyles, FileOpenMode.OpenForReadAndWriteNoShare, false, "");

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
                            objIds.Add(psc["PROFIL STYLE MGO"]);

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

                            Autodesk.Civil.DatabaseServices.Styles.StyleBase.ExportTo(objIds, localDb, Autodesk.Civil.StyleConflictResolverType.Override);
                        }
                        catch (System.Exception)
                        {
                            stylesTx.Abort();
                            throw;
                        }
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

        [CommandMethod("finalizesheets")]
        public void finalizesheets()
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
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForWrite) as BlockTable;

                    #region Stylize Profile Views
                    HashSet<ProfileView> pvs = localDb.HashSetOfType<ProfileView>(tx);

                    oid pvStyleId = oid.Null;
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

                        oid alId = pv.AlignmentId;
                        Alignment al = alId.Go<Alignment>(tx);

                        ObjectIdCollection psIds = al.GetProfileIds();
                        HashSet<Profile> ps = new HashSet<Profile>();
                        foreach (oid Oid in psIds) ps.Add(Oid.Go<Profile>(tx));

                        Profile surfaceProfile = ps.Where(x => x.Name.Contains("surface")).FirstOrDefault();
                        oid surfaceProfileId = oid.Null;
                        if (surfaceProfile != null) surfaceProfileId = surfaceProfile.ObjectId;
                        else ed.WriteMessage("\nSurface profile not found!");

                        Profile topProfile = ps.Where(x => x.Name.Contains("TOP")).FirstOrDefault();
                        oid topProfileId = oid.Null;
                        if (topProfile != null) topProfileId = topProfile.ObjectId;
                        else ed.WriteMessage("\nTop profile not found!");

                        //this doesn't quite work
                        oid pvbsId = civilDoc.Styles.ProfileViewBandSetStyles["EG-FG Elevations and Stations"];
                        ProfileViewBandSet pvbs = pv.Bands;
                        pvbs.ImportBandSetStyle(pvbsId);

                        //try this
                        oid pvBSId1 = civilDoc.Styles.BandStyles.ProfileViewProfileDataBandStyles["Elevations and Stations"];
                        oid pvBSId2 = civilDoc.Styles.BandStyles.ProfileViewProfileDataBandStyles["TitleBuffer"];
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
                            if (surfaceProfileId != oid.Null) pvbi.Profile1Id = surfaceProfileId;
                            if (topProfileId != oid.Null) pvbi.Profile2Id = topProfileId;
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

                            foreach (oid Oid in brefIds)
                            {
                                BlockReference bref = Oid.Go<BlockReference>(tx, OpenMode.ForWrite);
                                bref.ScaleFactors = new Scale3d(1, 2.5, 1);
                            }

                        }
                        #endregion
                    }
                    #endregion

                    #region ProfileStyles
                    oid pPipeStyleId = oid.Null;
                    try
                    {
                        pPipeStyleId = civilDoc.Styles.ProfileStyles["PROFIL STYLE MGO"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nPROFIL STYLE MGO style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    oid pTerStyleId = oid.Null;
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

                    oid alStyleId = oid.Null;
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

                    oid crestCurveLabelId = oid.Null;
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

                    oid sagCurveLabelId = oid.Null;
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

                        ObjectIdCollection pIds = al.GetProfileIds();
                        foreach (oid Oid in pIds)
                        {
                            Profile p = Oid.Go<Profile>(tx);
                            if (p.Name == $"{al.Name}_surface_P")
                            {
                                p.CheckOrOpenForWrite();
                                p.StyleId = pTerStyleId;
                            }
                            else
                            {
                                p.CheckOrOpenForWrite();
                                p.StyleId = pPipeStyleId;

                                if (p.Name.Contains("TOP"))
                                {
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

                    #region Delete unwanted objects -- NOT NEEDED ANYMORE
                    //HashSet<Circle> cs = localDb.HashSetOfType<Circle>(tx);
                    //foreach (Circle c in cs)
                    //{
                    //    c.CheckOrOpenForWrite();
                    //    c.Erase(true);
                    //}

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
                    //foreach (oid Oid in cogoPoints) cpIds.Add(Oid);
                    //foreach (oid Oid in cpIds) cogoPoints.Remove(Oid);
                    //#endregion

                    #region Stylize Profile Views
                    HashSet<ProfileView> pvs = localDb.HashSetOfType<ProfileView>(tx);

                    oid pvStyleId = oid.Null;
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

                    PromptKeywordOptions pKeyOpts = new PromptKeywordOptions("");
                    pKeyOpts.Message = "\nThis will destroy all OD Tables in drawing!!! Do you want to continue? ";
                    pKeyOpts.Keywords.Add(kwd1);
                    pKeyOpts.Keywords.Add(kwd2);
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

                            Tables odTables = HostMapApplicationServices.Application.ActiveProject.ODTables;
                            StringCollection allDbTables = odTables.GetTableNames();

                            foreach (string name in allDbTables)
                            {
                                if (name == "CrossingData" ||
                                    name == "IdRecord")
                                {
                                    editor.WriteMessage($"\nSkipping table {name}!");
                                    continue;
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
                    #region ChangeLayerOfXref

                    var fileList = File.ReadAllLines(@"X:\0371-1158 - Gentofte Fase 4 - Dokumenter\01 Intern\02 Tegninger\01 Autocad\Autocad\02 Sheets\4.3\fileList.txt").ToList();

                    foreach (string name in fileList)
                    {
                        prdDbg(name);
                    }
                    
                    foreach (string name in fileList)
                    {
                        prdDbg(name);
                        string fileName = $"X:\\0371-1158 - Gentofte Fase 4 - Dokumenter\\01 Intern\\02 Tegninger\\01 Autocad\\Autocad\\02 Sheets\\4.3\\{name}";
                        prdDbg(fileName);

                        using (Database extDb = new Database(false, true))
                        {
                            extDb.ReadDwgFile(fileName, System.IO.FileShare.ReadWrite, false, "");

                            using (Transaction extTx = extDb.TransactionManager.StartTransaction())
                            {
                                BlockTable bt = extTx.GetObject(extDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                                foreach (oid Oid in bt)
                                {
                                    BlockTableRecord btr = extTx.GetObject(Oid, OpenMode.ForWrite) as BlockTableRecord;
                                    if (btr.Name.Contains("_alignment"))
                                    {
                                        var ids = btr.GetBlockReferenceIds(true, true);
                                        foreach (oid brId in ids)
                                        {
                                            BlockReference br = brId.Go<BlockReference>(extTx, OpenMode.ForWrite);
                                            prdDbg(br.Name);
                                            prdDbg(br.Layer);
                                            br.Layer = "0";
                                            prdDbg(br.Layer);
                                            System.Windows.Forms.Application.DoEvents();
                                        }
                                    }
                                }
                                extTx.Commit();
                            }
                            extDb.SaveAs(extDb.Filename, DwgVersion.Current);

                        } 
                    }
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
                    tx.Abort();
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
                    oid alStyle = civilDoc.Styles.AlignmentStyles["FJV TRACÉ SHOW"];
                    HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);

                    foreach (Alignment al in als)
                    {
                        al.CheckOrOpenForWrite();
                        al.StyleId = alStyle;
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
                    oid alStyle = civilDoc.Styles.AlignmentStyles["FJV TRACE NO SHOW"];
                    HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);

                    foreach (Alignment al in als)
                    {
                        al.CheckOrOpenForWrite();
                        al.StyleId = alStyle;
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

                    foreach (oid id in ms)
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
                                                        oid vfsId = stylesDoc.Styles.ViewFrameStyles["Basic"];
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

                    foreach (oid id in localLt)
                    {
                        LayerTableRecord ltr = (LayerTableRecord)tx.GetObject(id, OpenMode.ForRead);

                        if (ltr.Name.Contains("_alignment") &&
                            ltr.Name.Contains(viewFrameLayerName) &&
                            !ltr.Name.Contains("TEXT"))
                        {
                            editor.WriteMessage($"\n{ltr.Name}");
                            ltr.UpgradeOpen();
                            ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, curNode.Value);

                            if (curNode.Next == null) curNode = colorSequence.First;
                            else curNode = curNode.Next;

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

        [CommandMethod("setglobalwidth")]
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
                    #region Read Csv Data for Layers
                    //Establish the pathnames to files
                    //Files should be placed in a specific folder on desktop
                    string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";

                    System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
                    #endregion

                    HashSet<Spline> splines = localDb.HashSetOfType<Spline>(tx);
                    HashSet<Line> lines = localDb.HashSetOfType<Line>(tx);
                    HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx);
                    editor.WriteMessage($"\nNr. of splines: {splines.Count}");
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
                    { "GAS-Stikrør-2D", "GAS-Fordelingsrør-2D", "GAS-Distributionsrør-2D", "GAS-ude af drift-2D" };
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
                            oid ltId = lt.Add(ltr);
                            tx.AddNewlyCreatedDBObject(ltr, true);
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
                foreach (oid Oid in Ids)
                {
                    if (Oid.ObjectClass.Name != "AcDbBlockReference") continue;

                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            // Call our explode function recursively, starting
                            // with the top-level block reference
                            // (you can pass false as a 4th parameter if you
                            // don't want originating entities erased)
                            ExplodeBlock(tr, db, Oid, true);
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
                if (br.Name == "MuffeIntern" ||
                    br.Name == "MuffeIntern2" ||
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
                    foreach (oid Oid in toExplode)
                    {
                        if (Oid.ObjectClass.Name == "AcDbBlockReference") continue;
                        Autodesk.AutoCAD.DatabaseServices.DBObject dbObj =
                            tr.GetObject(Oid, OpenMode.ForRead) as Autodesk.AutoCAD.DatabaseServices.DBObject;

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
                    foreach (oid Oid in Ids)
                    {
                        if (Oid.ObjectClass.Name != "AcDbBlockReference") continue;
                        //prdDbg("1: " + Oid.ObjectClass.Name);

                        using (Transaction tx = localDb.TransactionManager.StartTransaction())
                        {

                            try
                            {
                                BlockReference br = Oid.Go<BlockReference>(tx, OpenMode.ForWrite);
                                BlockTableRecord btr = tx.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;

                                foreach (oid bOid in btr)
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
                    foreach (oid Oid in Ids)
                    {
                        if (Oid.ObjectClass.Name != "AcDbBlockReference") continue;
                        using (Transaction tx = localDb.TransactionManager.StartTransaction())
                        {
                            try
                            {
                                BlockReference br = Oid.Go<BlockReference>(tx, OpenMode.ForWrite);
                                BlockTableRecord btr = tx.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                                prdDbg("Top LEVEL: " + br.Name);

                                foreach (oid bOid in btr)
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
                foreach (oid OidNested in btrNested)
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

                            foreach (oid Oid in bt)
                            {
                                BlockTableRecord btr = tx.GetObject(Oid, OpenMode.ForWrite) as BlockTableRecord;

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

                        foreach (oid Oid in bt)
                        {
                            BlockTableRecord btr = tx.GetObject(Oid, OpenMode.ForWrite) as BlockTableRecord;

                            if (ReadStringParameterFromDataTable(btr.Name, fjvKomponenter, "Navn", 0) != null)
                            {
                                //        prdDbg("2");
                                //        localDb.Insert(btr.Name, symbolerDB, true);
                                //        prdDbg("3");
                                foreach (oid bRefId in btr.GetBlockReferenceIds(false, true))
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
        //Does not update dynamic blocks
        public static void creategisdata()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\nRemember to freeze unneeded linework!");
            try
            {
                using (Transaction tx = localDb.TransactionManager.StartTransaction())
                {
                    try
                    {
                        System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(
                                            @"X:\AutoCAD DRI - 01 Civil 3D\FJV Komponenter.csv", "FjvKomponenter");

                        #region Populate Block GIS data
                        #region OD Table definition
                        string tableNameKomponenter = "Components";

                        string[] columnNames = new string[12]
                               {"BlockName",
                                "Type",
                                "Rotation",
                                "System",
                                "DN1",
                                "DN2",
                                "Serie",
                                "Width",
                                "Height",
                                "OffsetX",
                                "OffsetY",
                                "Flip"
                               };
                        string[] columnDescrs = new string[12]
                            {"Name of source block",
                             "Type of the component",
                             "Rotation of the symbol",
                             "Twin or single",
                             "Main run dimension",
                             "Secondary run dimension",
                             "Insulation series of pipes",
                             "Width of symbol",
                             "Height of symbol",
                             "X offset from Origo to CL",
                             "Y offset from Origo to CL",
                             "Describes block's mirror state"
                            };
                        DataType[] dataTypes = new DataType[12]
                            {DataType.Character,
                             DataType.Character,
                             DataType.Real,
                             DataType.Character,
                             DataType.Integer,
                             DataType.Integer,
                             DataType.Character,
                             DataType.Real,
                             DataType.Real,
                             DataType.Real,
                             DataType.Real,
                             DataType.Character
                            };
                        Func<BlockReference, System.Data.DataTable, MapValue>[] populateKomponentData =
                            new Func<BlockReference, System.Data.DataTable, MapValue>[12]
                        {
                            ODDataReader.Komponenter.ReadBlockName,
                            ODDataReader.Komponenter.ReadComponentType,
                            ODDataReader.Komponenter.ReadBlockRotation,
                            ODDataReader.Komponenter.ReadComponentSystem,
                            ODDataReader.Komponenter.ReadComponentDN1,
                            ODDataReader.Komponenter.ReadComponentDN2,
                            ODDataReader.Komponenter.ReadComponentSeries,
                            ODDataReader.Komponenter.ReadComponentWidth,
                            ODDataReader.Komponenter.ReadComponentHeight,
                            ODDataReader.Komponenter.ReadComponentOffsetX,
                            ODDataReader.Komponenter.ReadComponentOffsetY,
                            ODDataReader.Komponenter.ReadComponentFlipState
                        };

                        CheckOrCreateTable(tables, tableNameKomponenter, "Komponentdata", columnNames, columnDescrs, dataTypes);

                        #endregion

                        BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                        foreach (oid Oid in bt)
                        {
                            BlockTableRecord btr = tx.GetObject(Oid, OpenMode.ForWrite) as BlockTableRecord;
                            if (btr.GetBlockReferenceIds(true, true).Count == 0) continue;

                            if (ReadStringParameterFromDataTable(btr.Name, fjvKomponenter, "Navn", 0) != null)
                            {
                                ObjectIdCollection blkIds = btr.GetBlockReferenceIds(true, true);

                                foreach (oid brOid in blkIds)
                                {
                                    BlockReference br = brOid.Go<BlockReference>(tx, OpenMode.ForWrite);

                                    for (int i = 0; i < columnNames.Length; i++)
                                    {
                                        if (DoesRecordExist(tables, br.ObjectId, columnNames[i]))
                                        {
                                            UpdateODRecord(tables, tableNameKomponenter, columnNames[i],
                                                br.ObjectId, populateKomponentData[i].Invoke(br, fjvKomponenter));
                                        }
                                        else AddODRecord(tables, tableNameKomponenter, columnNames[i],
                                                br.ObjectId, populateKomponentData[i].Invoke(br, fjvKomponenter));
                                    }
                                }
                            }
                        }
                        #endregion
                        #region Populate (p)lines and arc GIS data
                        #region OD Table definition
                        string tableNamePipes = "Pipes";

                        string[] columnNamesPipes = new string[4]
                            {"DN", "Length", "System", "Serie"};
                        string[] columnDescrsPipes = new string[4]
                            {"Dimension of pipe", "Length of pipe","System of pipe", "Series of the pipe" };
                        DataType[] dataTypesPipes = new DataType[4]
                            {DataType.Integer, DataType.Real, DataType.Character, DataType.Character };
                        Func<Entity, MapValue>[] pipeData =
                            new Func<Entity, MapValue>[4]
                        {
                            ODDataReader.Pipes.ReadPipeDimension,
                            ODDataReader.Pipes.ReadPipeLength,
                            ODDataReader.Pipes.ReadPipeSystem,
                            ODDataReader.Pipes.ReadPipeSeries
                        };

                        CheckOrCreateTable(tables, tableNamePipes, "Rørdata", columnNamesPipes, columnDescrsPipes, dataTypesPipes);

                        #endregion

                        HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx, true);
                        HashSet<Line> lines = localDb.HashSetOfType<Line>(tx, true);
                        HashSet<Arc> arcs = localDb.HashSetOfType<Arc>(tx, true);
                        HashSet<Entity> ents = new HashSet<Entity>(plines.Count + lines.Count + arcs.Count);
                        ents.UnionWith(plines.Cast<Entity>());
                        ents.UnionWith(lines.Cast<Entity>());
                        ents.UnionWith(arcs.Cast<Entity>());

                        foreach (Entity ent in ents)
                        {
                            for (int i = 0; i < columnNamesPipes.Length; i++)
                            {
                                if (DoesRecordExist(tables, ent.ObjectId, columnNamesPipes[i]))
                                {
                                    UpdateODRecord(tables, tableNamePipes, columnNamesPipes[i],
                                        ent.ObjectId, pipeData[i].Invoke(ent));
                                }
                                else AddODRecord(tables, tableNamePipes, columnNamesPipes[i],
                                        ent.ObjectId, pipeData[i].Invoke(ent));
                            }
                        }
                        #endregion
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
    }
}