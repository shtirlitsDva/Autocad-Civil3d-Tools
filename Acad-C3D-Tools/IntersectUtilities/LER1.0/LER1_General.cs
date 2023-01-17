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

namespace IntersectUtilities
{
    public partial class Intersect
    {
        [CommandMethod("CONVERTLINEWORKPSS")]
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
                        PropertySetManager.CopyAllProperties(spline, curve);
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
                        PropertySetManager.CopyAllProperties(pline, polyline3D);
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
                        PropertySetManager.CopyAllProperties(line, polyline3D);
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
        }

        [CommandMethod("CONVERTLINESTOPOLIESPSS")]
        public void convertlinestopoliespss()
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
                    HashSet<Line> lines = localDb.HashSetOfType<Line>(tx);
                    editor.WriteMessage($"\nNr. of lines: {lines.Count}");

                    Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

                    foreach (Line line in lines)
                    {
                        Point3dCollection p3dcol = new Point3dCollection();

                        p3dcol.Add(line.StartPoint);
                        p3dcol.Add(line.EndPoint);

                        Polyline pline = new Polyline(2);

                        pline.AddVertexAt(pline.NumberOfVertices, line.StartPoint.To2D(), 0, 0, 0);
                        pline.AddVertexAt(pline.NumberOfVertices, line.EndPoint.To2D(), 0, 0, 0);
                        pline.AddEntityToDbModelSpace(localDb);

                        pline.Layer = line.Layer;
                        pline.Color = line.Color;

                        PropertySetManager.CopyAllProperties(line, pline);
                    }

                    foreach (Line line in lines)
                    {
                        line.CheckOrOpenForWrite();
                        line.Erase(true);
                    }
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.ToString());
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("CONVERT3DPOLIESTOPOLIESPSS")]
        public void convert3dpoliestopoliespss()
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
                    var p3ds = localDb.HashSetOfType<Polyline3d>(tx);
                    editor.WriteMessage($"\nNr. of polylines3d: {p3ds.Count}");

                    foreach (var p3d in p3ds)
                    {
                        var verts = p3d.GetVertices(tx);

                        Polyline pline = new Polyline(verts.Length);

                        foreach (var vert in verts)
                        {
                            pline.AddVertexAt(pline.NumberOfVertices, vert.Position.To2D(), 0, 0, 0);
                        }

                        pline.AddEntityToDbModelSpace(localDb);

                        pline.Layer = p3d.Layer;
                        pline.Color = p3d.Color;

                        PropertySetManager.CopyAllProperties(p3d, pline);
                    }

                    foreach (var line in p3ds)
                    {
                        line.CheckOrOpenForWrite();
                        line.Erase(true);
                    }
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.ToString());
                    tx.Abort();
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

                        //var list = FindDescriptionParts(diameterDef);
                        var parts = FindPropertySetParts(diameterDef);
                        if (parts.setName == default && parts.propertyName == default)
                        {
                            findDescriptionPartsFailed++;
                            continue;
                        }
                        //int diaOriginal = ReadIntPropertyValue(tables, pline.Id, parts[0], parts[1]);
                        object diaOriginal = PropertySetManager.ReadNonDefinedPropertySetObject(
                            pline, parts.setName, parts.propertyName);

                        double dia = default;

                        switch (diaOriginal)
                        {
                            case null:
                                dia = 90;
                                break;
                            case int integer:
                                dia = Convert.ToDouble(integer);
                                break;
                            case double d:
                                dia = d;
                                break;
                            case string s:
                                if (s.IsNoE()) s = "90";
                                try
                                {
                                    dia = Convert.ToDouble(s);
                                }
                                catch (System.Exception)
                                {
                                    prdDbg($"Fails: {s}");
                                    throw;
                                }
                                break;
                            default:
                                dia = 90;
                                break;
                        }

                        prdDbg(pline.Handle.ToString() + ": " + dia.ToString());

                        dia = dia / 1000;

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
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("CREATELABELSFOR2D")]
        public void createlabelsfor2d()
        {

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            //Settings
            const double labelOffset = 0.375;
            const double labelHeight = 0.75;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Read krydsninger
                    string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";
                    System.Data.DataTable dtK = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
                    if (dtK == null) throw new System.Exception("Failed to read Krydsninger.csv!");
                    #endregion

                    #region Create layer for labels
                    string labelLayer = "0-LABELS";
                    localDb.CheckOrCreateLayer(labelLayer);
                    #endregion

                    LayerTable lt = localDb.LayerTableId.Go<LayerTable>(tx);

                    HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx);
                    var layerGroups = plines.GroupBy(x => x.Layer);

                    foreach (var group in layerGroups)
                    {
                        string layerName = group.Key;

                        string labelRecipe =
                            ReadStringParameterFromDataTable(layerName, dtK, "Label", 0);

                        if (labelRecipe.IsNoE())
                        {
                            prdDbg($"Layer {layerName} does not have a recipe for labels defined! Skipping...");
                            continue;
                        }

                        LayerTableRecord ltr = lt[layerName].Go<LayerTableRecord>(tx);

                        //Create labels
                        foreach (var pline in group)
                        {
                            //Filter plines to avoid overpopulation
                            if (pline.Length < 5.0) continue;

                            //Compose label
                            string label = ConstructStringFromPSByRecipe(pline, labelRecipe);

                            //quick hack
                            if (
                                label == "ø0 - " ||
                                label == "ø - " ||
                                label == "ø0 - Uoplyst") continue;

                            //Create text object
                            DBText textEnt = new DBText();
                            textEnt.Layer = labelLayer;
                            textEnt.Color = ltr.Color;
                            textEnt.TextString = label;
                            textEnt.Height = labelHeight;

                            //Manage position
                            Point3d cen = pline.GetPointAtDist(pline.Length / 2);
                            var deriv = pline.GetFirstDerivative(cen);
                            var perp = deriv.GetPerpendicularVector();
                            Point3d loc = cen + perp * labelOffset;

                            Vector3d normal = new Vector3d(0.0, 0.0, 1.0);
                            double rotation = UtilsCommon.Utils.GetRotation(deriv, normal);

                            textEnt.HorizontalMode = TextHorizontalMode.TextCenter;
                            textEnt.VerticalMode = TextVerticalMode.TextBottom;

                            textEnt.Rotation = rotation;
                            textEnt.AlignmentPoint = loc;

                            textEnt.AddEntityToDbModelSpace(localDb);
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

        [CommandMethod("FLATTENPL3D")]
        public void flattenpl3d()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Polylines 3d

                    Oid id = Interaction.GetEntity("Select Polyline3d to flatten: ", typeof(Polyline3d));
                    Polyline3d p3d = id.Go<Polyline3d>(tx, OpenMode.ForWrite);

                    PolylineVertex3d[] vertices = p3d.GetVertices(tx);

                    for (int i = 0; i < vertices.Length; i++)
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
                    prdDbg(ex.ToString());
                    return;
                }
                tx.Commit();
            }

        }

        [CommandMethod("QAPSDATA")]
        public void qapsdata()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            #region Settings
            string propertySetName = "Stenlose-Ledninger";
            string propertyName = "Dimension";
            #endregion

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var pls = localDb.HashSetOfType<Polyline>(tx);

                    HashSet<string> data = new HashSet<string>();

                    foreach (var pl in pls)
                    {
                        string originalValue =
                            PropertySetManager.ReadNonDefinedPropertySetString(pl, propertySetName, propertyName);

                        data.Add(originalValue);
                    }

                    foreach (var originalValue in data.OrderBy(x => x))
                    {
                        string value = originalValue;
                        if (DataQa.Vand.replaceDict.ContainsKey(originalValue))
                        {
                            string replaced = DataQa.Vand.replaceDict[originalValue];
                            value += " <- replaced";

                            if (DataQa.Vand.imperialToDnDictString.ContainsKey(replaced))
                                value += " <- InchToDn";

                        }
                        else if (DataQa.Vand.imperialToDnDictString.ContainsKey(originalValue))
                            value += " <- InchToDn";

                        prdDbg(value);
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

        [CommandMethod("CLEANPSDATA")]
        public void cleanpsdata()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            #region Settings
            string propertySetName = "Stenlose-Ledninger";
            string propertyName = "Dimension";
            #endregion

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var pls = localDb.HashSetOfType<Polyline>(tx);

                    foreach (var pl in pls)
                    {
                        string originalValue =
                            PropertySetManager.ReadNonDefinedPropertySetString(pl, propertySetName, propertyName);

                        if (originalValue.IsNoE()) continue;

                        string value = default;

                        if (DataQa.Vand.replaceDict.ContainsKey(originalValue))
                        {
                            string replaced = DataQa.Vand.replaceDict[originalValue];

                            if (DataQa.Vand.imperialToDnDictString.ContainsKey(replaced))
                                value = DataQa.Vand.imperialToDnDictString[replaced];
                            else value = replaced;
                        }
                        else if (DataQa.Vand.imperialToDnDictString.ContainsKey(originalValue))
                            value = DataQa.Vand.imperialToDnDictString[originalValue];

                        if (value != default && value != originalValue)
                        {
                            PropertySetManager.WriteNonDefinedPropertySetString(
                                pl, propertySetName, propertyName, value);
                            prdDbg($"{originalValue} -> {value}");
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

        [CommandMethod("LISTUNIQUEPSDATA")]
        public void listuniquepsdata()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    PropertySetManager.ListUniquePsData(localDb);
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

    }
}