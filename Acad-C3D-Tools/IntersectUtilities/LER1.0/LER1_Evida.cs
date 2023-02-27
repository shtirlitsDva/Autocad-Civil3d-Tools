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
using static IntersectUtilities.UtilsCommon.Utils;
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
using System.Windows.Media.Media3D;

namespace IntersectUtilities
{
    public partial class Intersect
    {
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
                    var forbiddenValues = DataQa.Gas.ForbiddenValues();
                    var replaceValues = DataQa.Gas.ReplaceValues();

                    #region GatherObjects
                    HashSet<Entity> allEnts = localDb.HashSetOfType<Entity>(tx);

                    HashSet<Entity> entsPIPE = allEnts.Where(x => x.Layer == "PIPE").ToHashSet();
                    HashSet<Entity> entsLABEL = allEnts.Where(x => x.Layer == "LABEL").ToHashSet();
                    #endregion

                    #region QA data
                    //Find how many times multiple groups occur
                    var groups = entsLABEL.GroupBy(x => Convert.ToInt32(
                        PropertySetManager.ReadNonDefinedPropertySetDouble(
                        x, "LABEL", "G3E_FID")));
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
                                    x => PropertySetManager.ReadNonDefinedPropertySetString(
                                        x, "LABEL", "LABEL")));
                            prdDbg(values);
                        }
                    }

                    //Insert blank line to separate output
                    prdDbg("");
                    prdDbg("Handle values not marked by OK.");

                    //List all unique LABEL values
                    HashSet<string> allLabels = new HashSet<string>();
                    foreach (Entity ent in entsLABEL)
                    {
                        allLabels.Add(PropertySetManager.ReadNonDefinedPropertySetString(
                            ent, "LABEL", "LABEL"));
                    }
                    var ordered = allLabels.OrderBy(x => x);
                    foreach (string value in ordered)
                    {
                        string label = value.ToUpper();
                        //Check if value is handled by filters
                        if (forbiddenValues.Contains(label))
                            label += " <--- OK - Forbidden";
                        if (replaceValues.ContainsKey(label))
                            label += $" <--- OK - Replaced by {replaceValues[label]}";
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

        [CommandMethod("GASLISTALLPSDATA")]
        public void gaslistallpsdata()
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
                    HashSet<Polyline> allPlines = localDb.HashSetOfType<Polyline>(tx);
                    #endregion

                    #region Property Set Manager
                    PropertySetManager gasPsm = new PropertySetManager(
                        localDb, PSetDefs.DefinedSets.DriGasDimOgMat);
                    PSetDefs.DriGasDimOgMat gasDef = new PSetDefs.DriGasDimOgMat();
                    #endregion

                    #region QA data
                    HashSet<string> allLabels = new HashSet<string>();

                    foreach (Polyline polyline in allPlines)
                    {
                        int dim = gasPsm.ReadPropertyInt(polyline, gasDef.Dimension);
                        string mat = gasPsm.ReadPropertyString(polyline, gasDef.Material);

                        allLabels.Add(dim.ToString() + " " + mat);
                    }

                    allLabels.OrderBy(x => x).ToList().ForEach(x => prdDbg(x));

                    var query = allPlines.Where(x => gasPsm.FilterPropetyString(x, gasDef.Material, "relinet"));

                    foreach (var item in query) prdDbg(item.Handle.ToString());
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
                    var forbiddenValues = DataQa.Gas.ForbiddenValues();
                    var replaceValues = DataQa.Gas.ReplaceValues();

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

                    #region Property Set Manager
                    PropertySetManager psmGas = new PropertySetManager(
                        localDb, PSetDefs.DefinedSets.DriGasDimOgMat);
                    PSetDefs.DriGasDimOgMat driGasDimOgMat = new PSetDefs.DriGasDimOgMat();
                    #endregion

                    #region GatherObjects
                    HashSet<Entity> allEnts = localDb.HashSetOfType<Entity>(tx);

                    HashSet<Entity> entsPIPE = allEnts.Where(x => x.Layer == "PIPE").ToHashSet();
                    HashSet<Entity> entsLABEL = allEnts.Where(x => x.Layer == "LABEL").ToHashSet();
                    #endregion

                    #region Cache labels in memory
                    HashSet<(int G3eFid, string Label)> allLabels = new HashSet<(int G3eFid, string Label)>();

                    foreach (Entity ent in entsLABEL)
                    {
                        //string label = ReadStringPropertyValue(
                        //    tables, ent.Id, "LABEL", "LABEL");

                        string label = PropertySetManager.ReadNonDefinedPropertySetString(ent, "LABEL", "LABEL");

                        ////Filter out unwanted values
                        if (forbiddenValues.Contains(label.ToUpper())) continue;

                        //Modify labels with excess data
                        if (replaceValues.ContainsKey(label.ToUpper()))
                            label = replaceValues[label.ToUpper()];

                        allLabels.Add((
                            Convert.ToInt32(
                                PropertySetManager.ReadNonDefinedPropertySetDouble(ent, "LABEL", "G3E_FID")),
                            label));
                    }
                    #endregion

                    #region Find and write found labels if any
                    //Iterate pipe objects
                    foreach (Entity PIPE in entsPIPE)
                    {
                        #region Move pipe to correct layer
                        //Move pipe to correct layer
                        int FNO = PropertySetManager.ReadNonDefinedPropertySetInt(
                            PIPE,
                            "PIPE",
                            "G3E_FNO");

                        PIPE.CheckOrOpenForWrite();
                        if (FNO == 113) PIPE.Layer = "GAS-Stikrør";
                        else if (FNO == 112) PIPE.Layer = "GAS-Distributionsrør";
                        else if (FNO == 111) PIPE.Layer = "GAS-Fordelingsrør";
                        #endregion

                        //Try to find corresponding label entity
                        int G3EFID = Convert.ToInt32(
                            PropertySetManager.ReadNonDefinedPropertySetDouble(
                            PIPE,
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
                        PIPE.CheckOrOpenForWrite();

                        psmGas.GetOrAttachPropertySet(PIPE);
                        psmGas.WritePropertyObject(driGasDimOgMat.Dimension, parsedInt);
                        psmGas.WritePropertyString(driGasDimOgMat.Material, parsedMat);

                        PIPE.ColorIndex = 1;
                    }
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
                        Autodesk.AutoCAD.DatabaseServices.ObjectId entId = entity1.ObjectId;
                        Entity ent = entId.Go<Entity>(tx, OpenMode.ForWrite);
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

                        #region Use property sets to store data
                        PropertySetManager psmGas = new PropertySetManager(
                            localDb, PSetDefs.DefinedSets.DriGasDimOgMat);

                        PSetDefs.DriGasDimOgMat driGasDimOgMat = new PSetDefs.DriGasDimOgMat();
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

                        //Write properties
                        psmGas.GetOrAttachPropertySet(ent);
                        psmGas.WritePropertyObject(driGasDimOgMat.Dimension, parsedInt);
                        psmGas.WritePropertyObject(driGasDimOgMat.Material, parsedMat);
                        if (!ledningIbrug) psmGas.WritePropertyObject(driGasDimOgMat.Bemærk, "Ikke i brug");

                        editor.WriteMessage($"\nUpdating color and layer properties!");

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
                        Autodesk.AutoCAD.DatabaseServices.ObjectId entId = entity1.ObjectId;
                        Entity ent = entId.Go<Entity>(tx, OpenMode.ForWrite);
                        #endregion

                        #region Property set manager
                        PropertySetManager psm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriGasDimOgMat);
                        PSetDefs.DriGasDimOgMat driGasDimOgMat = new PSetDefs.DriGasDimOgMat();

                        psm.GetOrAttachPropertySet(ent);

                        psm.WritePropertyString(driGasDimOgMat.Bemærk, "Ikke i brug");

                        editor.WriteMessage($"\nUpdating color and layer properties!");

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

        [CommandMethod("gasibrug")]
        [CommandMethod("cf")]
        public void gasibrug()
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
                        Autodesk.AutoCAD.DatabaseServices.ObjectId entId = entity1.ObjectId;
                        Entity ent = entId.Go<Entity>(tx, OpenMode.ForWrite);
                        #endregion

                        #region Property set manager
                        PropertySetManager psm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriGasDimOgMat);
                        PSetDefs.DriGasDimOgMat driGasDimOgMat = new PSetDefs.DriGasDimOgMat();

                        psm.GetOrAttachPropertySet(ent);
                        psm.WritePropertyString(driGasDimOgMat.Bemærk, "");

                        editor.WriteMessage($"\nUpdating color and layer properties!");

                        Dictionary<string, int> layerNames = new Dictionary<string, int>()
                        {
                            {"GAS-Stikrør", 30 },
                            {"GAS-Stikrør-2D",30},
                            {"GAS-Fordelingsrør",30},
                            {"GAS-Fordelingsrør-2D",30},
                            {"GAS-Distributionsrør",30},
                            {"GAS-Distributionsrør-2D",30},
                            {"GAS-ude af drift",221},
                            {"GAS-ude af drift-2D",221}
                        };

                        foreach (KeyValuePair<string, int> entry in layerNames)
                        {
                            localDb.CheckOrCreateLayer(entry.Key, (short)entry.Value);
                        }

                        int FNO = PropertySetManager.ReadNonDefinedPropertySetInt(
                            ent,
                            "PIPE",
                            "G3E_FNO");

                        if (FNO == 113) ent.Layer = "GAS-Stikrør";
                        else if (FNO == 112) ent.Layer = "GAS-Distributionsrør";
                        else if (FNO == 111) ent.Layer = "GAS-Fordelingsrør";

                        if (FNO == 0) ent.ColorIndex = 2;
                        else ent.ColorIndex = 1;
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
                    Entity sourceEnt = sourceId.Go<Entity>(tx);

                    PromptEntityOptions promptEntityOptions2 = new PromptEntityOptions(
                        "\nSelect entity where to copy OD TO:");
                    promptEntityOptions1.SetRejectMessage("\n Not an entity!");
                    promptEntityOptions1.AddAllowedClass(typeof(Entity), false);
                    PromptEntityResult entity2 = editor.GetEntity(promptEntityOptions2);
                    if (((PromptResult)entity2).Status != PromptStatus.OK) { tx.Abort(); return; }
                    Autodesk.AutoCAD.DatabaseServices.ObjectId targetId = entity2.ObjectId;
                    Entity targetEnt = targetId.Go<Entity>(tx, OpenMode.ForWrite);
                    #endregion

                    #region Property sets
                    PropertySetManager.CopyAllProperties(sourceEnt, targetEnt);
                    #endregion

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
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("GASCREATELABELDATA")]
        [CommandMethod("CS")]
        public void gascreatelabeldata()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            List<string> gasDims = new List<string>()
            {
                "18",
                "20",
                "25",
                "40",
                "43",
                "63",
                "90",
                "100",
                "125",
                "160",
                "200",
                "250"
            };
            List<string> gasMats = new List<string>()
            {
                "CU",
                "PC",
                "PM",
                "ST",
                "GG",
                "XX"
            };
            List<string> gasStatus = new List<string>()
            {
                "Ibrug",
                "Uad"
            };

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    PropertySetManager gasPsm = new PropertySetManager(
                        localDb, PSetDefs.DefinedSets.DriGasDimOgMat);
                    PSetDefs.DriGasDimOgMat gasDef = new PSetDefs.DriGasDimOgMat();


                    string dimString = Interaction.GetKeywords("Select dimension: ", gasDims.ToArray());
                    if (dimString.IsNoE()) { AbortGracefully(tx, "User abort!"); return; }
                    string material = Interaction.GetKeywords("Select material: ", gasMats.ToArray());
                    if (material.IsNoE()) { AbortGracefully(tx, "User abort!"); return; }
                    string status = Interaction.GetKeywords("Select status: ", gasStatus.ToArray());
                    if (status.IsNoE()) { AbortGracefully(tx, "User abort!"); return; }

                    int dim = Convert.ToInt32(dimString);

                    Oid id = Interaction.GetEntity("Select gas pipe: ", typeof(Polyline));
                    if (id == Oid.Null) { AbortGracefully(tx, "Pipe selection error!"); return; }

                    Polyline pline = id.Go<Polyline>(tx);
                    pline.CheckOrOpenForWrite();
                    gasPsm.WritePropertyObject(pline, gasDef.Dimension, dim);
                    gasPsm.WritePropertyString(pline, gasDef.Material, material);

                    if (status == "Ibrug")
                    {
                        pline.Color = ColorByName("red");
                    }
                    else
                    {
                        pline.Color = ColorByName("cyan");
                        gasPsm.WritePropertyString(pline, gasDef.Bemærk, "Ikke i brug");
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
                    HashSet<Polyline3d> localPlines3d = localDb.HashSetOfType<Polyline3d>(tx);
                    editor.WriteMessage($"\nNr. of local 3D polies: {localPlines3d.Count}");
                    #endregion

                    //Points to intersect
                    HashSet<DBPoint> points = new HashSet<DBPoint>(localDb.ListOfType<DBPoint>(tx)
                                                  .Where(x => 
                                                  x.Position.Z > -98.0 &&
                                                  !x.Position.Z.IsZero(0.0001)),
                                                  new PointDBHorizontalComparer());
                    editor.WriteMessage($"\nNr. of local points: {points.Count}");
                    editor.WriteMessage($"\nTotal number of combinations: " +
                        $"{points.Count * (localPlines3d.Count)}");

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

        [CommandMethod("MOVEP3DATZEROTO99")]
        public void movep3datzeroto99()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var plines = localDb.HashSetOfType<Polyline3d>(tx);
                    var plinesAtZero = new List<Polyline3d>();

                    foreach (var item in plines)
                    {
                        var verts = item.GetVertices(tx);
                        prdDbg(verts.Length);
                        bool nonZero = false;

                        foreach (var vert in verts)
                        {
                            prdDbg(vert.Position.Z);
                            if (vert.Position.Z < 0.0001 && vert.Position.Z > -0.0001) continue;
                            else nonZero = true;
                        }

                        if (!nonZero) plinesAtZero.Add(item);
                    }

                    prdDbg(plinesAtZero.Count);
                    foreach (var item in plinesAtZero)
                    {
                        foreach (var vert in item.GetVertices(tx))
                        {
                            vert.CheckOrOpenForWrite();
                            vert.Position = new Point3d(vert.Position.X, vert.Position.Y, -99.0);
                        }
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

        /// <summary>
        /// Simple method to get elevation from neighbouring vertices
        /// </summary>
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
                    #region Polylines 3d
                    /////////////////////////////////
                    //Must not overlap
                    //correctionThreshold operates on values LESS THAN value
                    //targetThreshold operates on values GREATER THAN value
                    double correctionThreshold = -15;
                    double targetThreshold = -10;
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
        /// <summary>
        /// More advanced method to get elevation from neighbouring vertices
        /// by interpolation
        /// </summary>
        [CommandMethod("GASBEHANDLINGV2")]
        public void gasbehandlingv2()
        {

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Polylines 3d
                    /////////////////////////////////
                    bool atZero(double value) => value > -0.0001 && value < 0.0001;
                    ////////////////////////////////
                    HashSet<Polyline3d> plines3d = localDb.HashSetOfType<Polyline3d>(tx, true);
                    foreach (Polyline3d p3d in plines3d)
                    {
                        PolylineVertex3d[] vertices = p3d.GetVertices(tx);
                        int endIdx = vertices.Length - 1;

                        #region Skip completely zeroed p3ds
                        //Test if p3d is completely at zero and skip if it is
                        bool nonZero = false;
                        foreach (var vert in vertices)
                        {
                            if (vert.Position.Z < 0.0001 && vert.Position.Z > -0.0001) continue;
                            else nonZero = true;
                        }
                        if (!nonZero) continue;
                        #endregion

                        //Take care of first and last vertices first
                        #region First vertice
                        {
                            PolylineVertex3d vert = vertices[0];

                            //If vertice is already non-zero then skip
                            if (atZero(vert.Position.Z))
                            {
                                bool elevationUnknown = true;
                                Point3d pos = new Point3d();
                                int j = 1; //Start at second vertice
                                do
                                {
                                    if (!atZero(vertices[j].Position.Z))
                                    {
                                        elevationUnknown = false;
                                        pos = vertices[j].Position;
                                    }
                                    j++;
                                    //Catch out of bounds
                                    if (j > endIdx) break;
                                } while (elevationUnknown);

                                vert.CheckOrOpenForWrite();
                                vert.Position = new Point3d(
                                    vert.Position.X, vert.Position.Y, pos.Z);
                            }
                        }
                        #endregion
                        #region Last vertice
                        {
                            PolylineVertex3d vert = vertices[endIdx];

                            //If vertice is already non-zero then skip
                            if (atZero(vert.Position.Z))
                            {
                                bool elevationUnknown = true;
                                Point3d pos = new Point3d();
                                int j = endIdx - 1;
                                do
                                {
                                    if (!atZero(vertices[j].Position.Z))
                                    {
                                        elevationUnknown = false;
                                        pos = vertices[j].Position;
                                    }
                                    j--;
                                    //Catch out of bounds
                                    if (j < 0) break;
                                } while (elevationUnknown);

                                vert.CheckOrOpenForWrite();
                                vert.Position = new Point3d(
                                    vert.Position.X, vert.Position.Y, pos.Z);
                            }
                        }
                        #endregion

                        //Start at second vertice and end at next to last
                        for (int i = 1; i < endIdx; i++)
                        {
                            var vert = vertices[i];

                            //Intermediary vertex case
                            //Activate only if vertex at zero
                            if (atZero(vert.Position.Z))
                            {
                                Point3d forwardPos = new Point3d();
                                Point3d backwardPos = new Point3d();

                                int forwardIdx = i + 1;
                                int backwardIdx = i - 1;

                                bool forwardElevationUnknown = true;
                                bool backwardElevationUnknown = true;

                                #region Back and forward detection
                                while (true) //Forward detection
                                {
                                    if (!atZero(vertices[forwardIdx].Position.Z))
                                    {
                                        forwardElevationUnknown = false;
                                        forwardPos = vertices[forwardIdx].Position;
                                        break; //Break here to avoid increasing forwardIdx
                                    }
                                    forwardIdx++;
                                    //Catch out of bounds
                                    if (forwardIdx > endIdx) break;
                                }

                                while (true) //Backward detection
                                {
                                    if (!atZero(vertices[backwardIdx].Position.Z))
                                    {
                                        backwardElevationUnknown = false;
                                        backwardPos = vertices[backwardIdx].Position;
                                        break; //Break here to avoid increasing backwardIdx
                                    }
                                    backwardIdx--;
                                    //Catch out of bounds
                                    if (backwardIdx < 0) break;
                                }
                                #endregion

                                if (!backwardElevationUnknown && !forwardElevationUnknown)
                                {
                                    //Interpolate between vertices
                                    double startElevation = vertices[backwardIdx].Position.Z;
                                    double endElevation = vertices[forwardIdx].Position.Z;
                                    double AB = p3d.GetHorizontalLengthBetweenIdxs(backwardIdx, forwardIdx);
                                    double PB = p3d.GetHorizontalLengthBetweenIdxs(backwardIdx, i);

                                    double m = (endElevation - startElevation) / AB;
                                    double b = endElevation - m * AB;
                                    double newElevation = m * PB + b;

                                    //Change the elevation
                                    vert.CheckOrOpenForWrite();
                                    vert.Position = new Point3d(
                                        vert.Position.X, vert.Position.Y, newElevation);
                                }
                                else if (!backwardElevationUnknown)
                                {
                                    prdDbg("Warning! Should not execute!");
                                    //vertices[i].CheckOrOpenForWrite();
                                    //vertices[i].Position = new Point3d(
                                    //    vertices[i].Position.X, vertices[i].Position.Y,
                                    //    backwardPos.Z);
                                }
                                else if (!forwardElevationUnknown)
                                {
                                    prdDbg("Warning! Should not execute!");
                                    //vertices[i].CheckOrOpenForWrite();
                                    //vertices[i].Position = new Point3d(
                                    //    vertices[i].Position.X, vertices[i].Position.Y,
                                    //    forwardPos.Z);
                                }
                            }
                        }
                    }
                    #endregion
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
                            if (vertices[i].Position.Z > 1.0) didNotFindAboveThreshold = false;
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
        
        [CommandMethod("GASCHANGELAYERFOR2DV2")]
        public void gaschangelayerfor2dv2()
        {

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Change layer
                    ///////////////////////////
                    bool atZero(double value) => value > -0.0001 && value < 0.0001;
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
                        bool isNotZeroed = false;
                        PolylineVertex3d[] vertices = line.GetVertices(tx);
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            if (!atZero(vertices[i].Position.Z)) isNotZeroed = true;
                        }

                        if (!isNotZeroed)
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
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("GASCOLORKNOWN")]
        public void gascolorknown()
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
                    #region Property Set Manager
                    PropertySetManager psmGas = new PropertySetManager(
                        localDb, PSetDefs.DefinedSets.DriGasDimOgMat);
                    PSetDefs.DriGasDimOgMat driGasDimOgMat = new PSetDefs.DriGasDimOgMat();
                    #endregion

                    #region GatherObjects
                    HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx);
                    #endregion

                    #region Color pipes with dimensions
                    foreach (Polyline pline in plines)
                    {
                        int dim = psmGas.ReadPropertyInt(pline, driGasDimOgMat.Dimension);
                        if (dim == 0) continue;
                        else
                        {
                            pline.CheckOrOpenForWrite();
                            pline.Color = ColorByName("red");
                        }
                    }
                    #endregion
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

        [CommandMethod("GASSELECTUNKNOWN")]
        public void gasselectunknown()
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
                    PropertySetManager psmGas = new PropertySetManager(
                        localDb, PSetDefs.DefinedSets.DriGasDimOgMat);
                    PSetDefs.DriGasDimOgMat driGasDimOgMat = new PSetDefs.DriGasDimOgMat();

                    HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx);

                    List<Oid> oids = new List<Oid>();
                    foreach (Polyline pline in plines)
                    {
                        int dim = psmGas.ReadPropertyInt(pline, driGasDimOgMat.Dimension);
                        if (dim == 0) oids.Add(pline.Id);
                    }
                    editor.SetImpliedSelection(oids.ToArray());
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
    }
}
