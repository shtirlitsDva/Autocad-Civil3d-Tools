using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using DimensioneringV2.Forms;
using DimensioneringV2.Geometry;
using DimensioneringV2.GraphFeatures;
using DimensioneringV2.GraphModelRoads;
using DimensioneringV2.Schema;
using DimensioneringV2.Serialization;
using DimensioneringV2.Services;
using DimensioneringV2.Services.GDALClient;
using DimensioneringV2.UI;
using DimensioneringV2.Vejklasser.Models;
using DimensioneringV2.Vejklasser.Views;

using Dreambuild.AutoCAD;

using IntersectUtilities;
using IntersectUtilities.NTS;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager.CsvData;
using IntersectUtilities.UtilsCommon.Serialization;

using Microsoft.Win32;

using NetTopologySuite;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

using QuikGraph;

using Schema.Datafordeler;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;

using static IntersectUtilities.UtilsCommon.Utils;

using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using cv = DimensioneringV2.CommonVariables;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

[assembly: CommandClass(typeof(DimensioneringV2.Commands))]

namespace DimensioneringV2
{
    public class Commands : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\nVelkommen til Dimensionering v2.0!");

            string basePath = @"X:\AutoCAD DRI - 01 Civil 3D\NetloadV2\2025\DimensioneringV2\";
            List<string> dlls = [
                "OxyPlot.dll",
                "OxyPlot.Wpf.dll",
                "SharpVectors.Core.dll",
                "SharpVectors.Model.dll",
                "SharpVectors.Dom.dll",
                "SharpVectors.CSS.dll",
                "SharpVectors.Runtime.Wpf.dll",
                "SharpVectors.Converters.Wpf.dll",
                "SharpVectors.Rendering.Wpf.dll"
                ];

            foreach (string dll in dlls)
            {
                Assembly.LoadFrom(Path.Combine(basePath, dll));
            }

#if DEBUG
            AppDomain.CurrentDomain.AssemblyResolve +=
        new ResolveEventHandler(MissingAssemblyLoaderDimV2.Debug_AssemblyResolveV2);
#endif
        }

        public void Terminate()
        {
            //Add your termination code here
        }
        #endregion

        private static double tol = DimensioneringV2.Tolerance.Default;
        /// <command>DIM2PREPAREDWG</command>
        /// <summary>
        /// Prepares a new dwg for dimensionering.
        /// Creates layers, imports blocks etc.
        /// </summary>
        /// <category>DIMENSIONERING V2</category>
        [CommandMethod("DIM2PREPAREDWG")]
        public void dim2preparedwg() => dim2preparedwgmethod();
        internal static void dim2preparedwgmethod(Database db = null)
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = db ?? docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var lt = localDb.LayerTableId.Go<LayerTable>(tx);

                    localDb.CheckOrCreateLayer(cv.LayerEndPoint);
                    localDb.CheckOrCreateLayer(cv.LayerSupplyPoint);

                    localDb.CheckOrCreateLayer(cv.LayerVejmidteTændt, 1);
                    var ltr = lt[cv.LayerVejmidteTændt].Go<LayerTableRecord>(tx, OpenMode.ForWrite);
                    ltr.LineWeight = LineWeight.LineWeight030;

                    localDb.CheckOrCreateLayer(cv.LayerVejmidteSlukket, 4);
                    ltr = lt[cv.LayerVejmidteSlukket].Go<LayerTableRecord>(tx, OpenMode.ForWrite);
                    ltr.LineWeight = LineWeight.LineWeight030;

                    byte transparencyValue = 50;
                    Byte alpha = (Byte)(255 * (100 - transparencyValue) / 100);
                    Transparency trans = new Transparency(alpha);
                    ltr.Transparency = trans;

                    localDb.CheckOrCreateLayer(cv.LayerConnectionLine, 2);
                    localDb.CheckOrCreateLayer(cv.LayerNoCross, 1);

                    localDb.CheckOrImportBlockRecord(
                        @"X:\AutoCAD DRI - 01 Civil 3D\DynBlokke\Symboler.dwg", cv.BlockEndPointName);
                    localDb.CheckOrImportBlockRecord(
                        @"X:\AutoCAD DRI - 01 Civil 3D\DynBlokke\Symboler.dwg", cv.BlockSupplyPointName);

                    #region Import building blocks if missing
                    BlockTable bt = localDb.BlockTableId.Go<BlockTable>(tx, OpenMode.ForRead);
                    var nonExistingBlocks = cv.AllBlockTypes.Where(x => !bt.Has(x));

                    if (nonExistingBlocks.Count() > 0)
                    {
                        ObjectIdCollection idsToClone = new ObjectIdCollection();

                        using Database blockDb = new Database(false, true);
                        blockDb.ReadDwgFile("X:\\AutoCAD DRI - 01 Civil 3D\\DynBlokke\\Symboler.dwg",
                            FileOpenMode.OpenForReadAndAllShare, false, null);
                        using Transaction blockTx = blockDb.TransactionManager.StartTransaction();

                        Oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(blockDb);
                        Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);

                        BlockTable sourceBt = blockDb.BlockTableId.Go<BlockTable>(blockTx, OpenMode.ForRead);

                        foreach (string blockName in nonExistingBlocks)
                        {
                            prdDbg($"Importing block {blockName}.");
                            idsToClone.Add(sourceBt[blockName]);
                        }

                        IdMapping mapping = new IdMapping();
                        blockDb.WblockCloneObjects(idsToClone, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);
                        blockTx.Commit();
                    }
                    #endregion

                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }

            prdDbg("Finished!");
        }

        /// <command>DIM2IMPORTBBRBLOCKS</command>
        /// <summary>
        /// Imports BBR features from a selected GeoJSON file as block references and writes their
        /// attributes into the BBR property set. Intended to populate the drawing with building points
        /// carrying BBR data for subsequent graph and reporting workflows.
        /// </summary>
        /// <category>DIMENSIONERING V2</category>
        [CommandMethod("DIM2IMPORTBBRBLOCKS")]
        public void dim2importbbrblocks()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            PropertySetManager.UpdatePropertySetDefinition(
                Application.DocumentManager.MdiActiveDocument.Database,
                PSetDefs.DefinedSets.BBR);

            #region Get path
            ChoosePath cp = new ChoosePath();
            cp.ShowDialog();
            cp.Close();
            if (cp.Path.IsNoE()) return;
            #endregion

            #region Read BBR data
            string basePath = @"X:\AutoCAD DRI - QGIS\BBR UDTRÆK";
            string path = basePath + "\\" + cp.Path + "\\";

            string bbrPath = path + "BBR.geojson";
            if (!File.Exists(bbrPath))
            { prdDbg("BBR_enhed.json does not exist! Download med RestHenter!"); return; }            

            BBRFeatureCollection BBR = IntersectUtilities.UtilsCommon.Serialization
                .Json.Deserialize<BBRFeatureCollection>(bbrPath);
            #endregion

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                PropertySetManager bbrPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.BBR);
                PSetDefs.BBR bbrDef = new PSetDefs.BBR();

                try
                {
                    var pm = new ProgressMeter();
                    pm.Start("Importing BBR features...");
                    pm.SetLimit(BBR.features.Count);
                    foreach (Schema.Feature feature in BBR.features)
                    {
                        try
                        {
                            var test = feature.geometry.coordinates as IEnumerable;
                        }
                        catch (System.Exception ex)
                        {
                            prdDbg("Feature " + feature.properties.id_lokalId + " mangler geometry!1");
                            throw;
                        }
                        var source = feature.geometry.coordinates as IEnumerable;
                        double[] coords = new double[2];
                        int i = 0;
                        if (source != null)
                        {
                            foreach (double d in source)
                            {
                                if (i == 0) coords[0] = d;
                                else coords[1] = d;
                                i++;
                            }
                        }

                        Point3d position = new Point3d(coords[0], coords[1], 0);

                        BlockReference bbrBlock = null;
                        try
                        {
                            bbrBlock = localDb.CreateBlockWithAttributes(feature.properties.Type, position);
                        }
                        catch (System.Exception ex)
                        {
                            if (ex.Message == "eKeyNotFound") prdDbg("Tegningen mangler BBR blokke!");
                            else prdDbg("Failed block name: " + feature.properties.Type + "\n" + ex.ToString());
                            tx.Abort();
                            return;
                        }

                        var properties = feature.properties.GetType().GetRuntimeProperties()
                            .Where(p => p.GetMethod != null && p.GetMethod.IsPublic && p.GetMethod.IsStatic == false)
                            .ToList();

                        var dict = bbrDef.ToPropertyDictionary();
                        foreach (PropertyInfo pinfo in properties)
                        {
                            if (dict.ContainsKey(pinfo.Name))
                            {
                                var value = TryGetValue(pinfo, feature.properties);
                                try
                                {
                                    bbrPsm.WritePropertyObject(bbrBlock, dict[pinfo.Name] as PSetDefs.Property, value);
                                }
                                catch (System.Exception)
                                {
                                    prdDbg($"Property not found: {(dict[pinfo.Name] as PSetDefs.Property).Name}");
                                    throw;
                                }
                            }
                        }

                        pm.MeterProgress();
                    }
                    pm.Stop();
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                finally
                {

                }
                tx.Commit();
            }

            dim2analyzeduplicateaddr();

            prdDbg("Finished!");

        }

        /// <command>DIM2ANALYZEDUPLICATEADDR</command>
        /// <summary>
        /// Analyzes buildings with identical addresses, assigns or repairs duplicate numbers in the BBR
        /// property set to ensure unique address labels, and exports a CSV report. Intended to normalize
        /// duplicate addresses before Excel generation and other downstream processes.
        /// </summary>
        /// <category>DIMENSIONERING V2</category>
        [CommandMethod("DIM2ANALYZEDUPLICATEADDR")]
        public void dim2analyzeduplicateaddr()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            PropertySetManager.UpdatePropertySetDefinition(localDb, PSetDefs.DefinedSets.BBR);

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    PropertySetManager bbrPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.BBR);
                    PSetDefs.BBR bbrDef = new PSetDefs.BBR();

                    HashSet<BlockReference> brs =
                        localDb
                        .HashSetOfType<BlockReference>(tx)
                        .Where(x => cv.AllBlockTypes.Contains(
                            bbrPsm.ReadPropertyString(x, bbrDef.Type)))
                        .ToHashSet();

                    var groups = brs.GroupBy(x => bbrPsm.ReadPropertyString(x, bbrDef.Adresse))
                        .Where(x => x.Count() > 1);

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("Adresse;Område;Forbrug;Ny adresse");

                    foreach (var group in groups)
                    {
                        //Handle a case where a building is added with same address after an analyzis have already been carried out
                        if (group.All(x => bbrPsm.ReadPropertyInt(x, bbrDef.AdresseDuplikatNr) == 0))
                        {
                            int count = 0;
                            foreach (var br in group.OrderBy(x => x.Position.X).ThenBy(x => x.Position.Y))
                            {
                                count++;
                                sb.AppendLine(
                                    $"{bbrPsm.ReadPropertyString(br, bbrDef.Adresse)};" +
                                    $"{bbrPsm.ReadPropertyString(br, bbrDef.DistriktetsNavn)};" +
                                    $"{bbrPsm.ReadPropertyDouble(br, bbrDef.EstimeretVarmeForbrug)};" +
                                    $"{bbrPsm.ReadPropertyString(br, bbrDef.Adresse)} {count}");

                                bbrPsm.WritePropertyObject(br, bbrDef.AdresseDuplikatNr, count);
                            }
                        }
                        else if (group.All(x => bbrPsm.ReadPropertyInt(x, bbrDef.AdresseDuplikatNr) != 0))
                        {
                            foreach (var br in group.OrderBy(x => x.Position.X).ThenBy(x => x.Position.Y))
                            {
                                sb.AppendLine(
                                    $"{bbrPsm.ReadPropertyString(br, bbrDef.Adresse)};" +
                                    $"{bbrPsm.ReadPropertyString(br, bbrDef.DistriktetsNavn)};" +
                                    $"{bbrPsm.ReadPropertyDouble(br, bbrDef.EstimeretVarmeForbrug)};" +
                                    $"{bbrPsm.ReadPropertyString(br, bbrDef.Adresse)} {bbrPsm.ReadPropertyInt(br, bbrDef.AdresseDuplikatNr)}");
                            }
                        }
                        else if (group.Any(x => bbrPsm.ReadPropertyInt(x, bbrDef.AdresseDuplikatNr) != 0))
                        {
                            List<int> existingNumbers =
                                group.Select(x => bbrPsm.ReadPropertyInt(x, bbrDef.AdresseDuplikatNr))
                                .OrderBy(x => x).ToList();

                            foreach (var br in group
                                .Where(x => bbrPsm.ReadPropertyInt(x, bbrDef.AdresseDuplikatNr) == 0)
                                .OrderBy(x => x.Position.X).ThenBy(x => x.Position.Y))
                            {
                                int nextNumber = 0;
                                var missing = FindMissing(existingNumbers);
                                if (missing.Count() > 0) nextNumber = missing.Min();
                                else nextNumber = existingNumbers.Max() + 1;
                                existingNumbers.Add(nextNumber);

                                bbrPsm.WritePropertyObject(br, bbrDef.AdresseDuplikatNr, nextNumber);
                            }

                            foreach (var br in group.OrderBy(x => bbrPsm.ReadPropertyInt(x, bbrDef.AdresseDuplikatNr)))
                            {
                                sb.AppendLine(
                                    $"{bbrPsm.ReadPropertyString(br, bbrDef.Adresse)};" +
                                    $"{bbrPsm.ReadPropertyString(br, bbrDef.DistriktetsNavn)};" +
                                    $"{bbrPsm.ReadPropertyDouble(br, bbrDef.EstimeretVarmeForbrug)};" +
                                    $"{bbrPsm.ReadPropertyString(br, bbrDef.Adresse)} {bbrPsm.ReadPropertyInt(br, bbrDef.AdresseDuplikatNr)}");
                            }
                        }
                        sb.AppendLine();
                    }

                    IEnumerable<int> FindMissing(IEnumerable<int> values)
                    {
                        HashSet<int> myRange = new HashSet<int>(Enumerable.Range(values.Min(), values.Max()));
                        myRange.ExceptWith(values);
                        return myRange;
                    }

                    //Build file name
                    string dbFilename = localDb.OriginalFileName;
                    string path = System.IO.Path.GetDirectoryName(dbFilename);
                    string dumpExportFileName = path + "\\duplikater.csv";

                    Utils.OutputWriter(dumpExportFileName, sb.ToString(), true);
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                finally
                {

                }
                tx.Commit();
            }
        }

        private static object TryGetValue(PropertyInfo property, object element)
        {
            object value;
            try
            {
                value = property.GetValue(element);

                if (value == null)
                {
                    switch (property.PropertyType.Name)
                    {
                        case nameof(String):
                            value = "";
                            break;
                        case nameof(Boolean):
                            value = false;
                            break;
                        case nameof(Double):
                            value = 0.0;
                            break;
                        case nameof(Int32):
                            value = 0;
                            break;
                        default:
                            value = "";
                            break;
                    }
                }
            }
            catch (System.Exception ex)
            {
                value = $"{{{ex.Message}}}";
            }

            return value;
        }

        /// <command>DIM2AREACROP</command>
        /// <summary>
        /// Crops BBR blocks and vejmidte polylines to an area delimited by a closed polyline.
        /// Blocks outside the boundary are deleted. Polylines wholly outside are deleted,
        /// wholly inside are kept, and crossing polylines are split at the boundary.
        /// </summary>
        /// <category>DIMENSIONERING V2</category>
        [CommandMethod("DIM2CROPAREA")]
        public void dim2croparea()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;

            PropertySetManager.UpdatePropertySetDefinition(localDb, PSetDefs.DefinedSets.BBR);

            PromptEntityOptions peo = new PromptEntityOptions(
                "\nVælg lukket POLYLINE til beskæring: ");
            peo.SetRejectMessage("\n Ikke en POLYLINE!");
            peo.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult res = editor.GetEntity(peo);
            if (res.Status != PromptStatus.OK) return;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    Polyline boundary = res.ObjectId.Go<Polyline>(tx);
                    if (!boundary.Closed)
                    {
                        prdDbg("Selected polyline is not closed!");
                        return;
                    }

                    Polygon cropPolygon = NTSConversion
                        .ConvertClosedPlineToNTSPolygon(boundary);
                    var geomFactory = cropPolygon.Factory;

                    var brs = localDb.HashSetOfType<BlockReference>(tx, true)
                        .Where(x => cv.AllBlockTypes.Contains(new BBR(x).Type))
                        .ToList();

                    int deletedBlocks = 0;
                    foreach (var br in brs)
                    {
                        var point = geomFactory.CreatePoint(
                            new Coordinate(br.Position.X, br.Position.Y));
                        if (!cropPolygon.Covers(point))
                        {
                            br.CheckOrOpenForWrite();
                            br.Erase(true);
                            deletedBlocks++;
                        }
                    }
                    prdDbg($"Blocks: kept {brs.Count - deletedBlocks}, deleted {deletedBlocks}");

                    var pls = localDb.HashSetOfType<Polyline>(tx)
                        .Where(x =>
                        x.Layer == cv.LayerVejmidteTændt ||
                        x.Layer == cv.LayerVejmidteSlukket ||
                        x.Layer == "Vejmidte")
                        .ToList();

                    int kept = 0, deleted = 0, split = 0;
                    foreach (var pl in pls)
                    {
                        if (pl.Id == boundary.Id) continue;

                        LineString ls = NTSConversion.ConvertPlineToNTSLineString(pl);

                        if (cropPolygon.Covers(ls)) { kept++; continue; }

                        if (!cropPolygon.Intersects(ls))
                        {
                            pl.CheckOrOpenForWrite();
                            pl.Erase(true);
                            deleted++;
                            continue;
                        }

                        NetTopologySuite.Geometries.Geometry clipped =
                            cropPolygon.Intersection(ls);                        

                        for (int i = 0; i < clipped.NumGeometries; i++)
                        {
                            if (clipped.GetGeometryN(i) is not LineString line) continue;
                            if (line.IsEmpty || line.Length < tol) continue;
                            Polyline newPl = NTSConversion.ConvertNTSLineStringToPline(line);
                            newPl.Layer = pl.Layer;
                            newPl.AddEntityToDbModelSpace(localDb);
                        }
                        split++;

                        pl.CheckOrOpenForWrite();
                        pl.Erase(true);
                    }
                    prdDbg($"Polylines: kept {kept}, deleted {deleted}, split {split}");
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }

            prdDbg("Finished!");
        }

        /// <command>DIM2TÆND</command>
        /// <summary>
        /// Interactively selects road centerline polylines and moves each to the "tændt" layer
        /// until the user cancels.
        /// </summary>
        /// <category>DIMENSIONERING V2</category>
        [CommandMethod("DIM2TÆND")]
        public void dim2tænd()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            while (true)
            {
                var oid = Interaction.GetEntity("\nVælg en vejmidte at tænde: ", typeof(Polyline));

                if (oid.IsNull) break;

                Autodesk.AutoCAD.Internal.Utils.SetUndoMark(true);
                oid.QOpenForWrite<Polyline>(x => x.Layer = cv.LayerVejmidteTændt);
                Autodesk.AutoCAD.Internal.Utils.SetUndoMark(false);
            }
            prdDbg("Finished!");
        }

        /// <command>DIM2SLUK</command>
        /// <summary>
        /// Interactively selects road centerline polylines and moves each to the "slukket" layer
        /// until the user cancels.
        /// </summary>
        /// <category>DIMENSIONERING V2</category>
        [CommandMethod("DIM2SLUK")]
        public void dim2sluk()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            while (true)
            {
                var oid = Interaction.GetEntity("\nVælg en vejmidte at slukke: ", typeof(Polyline));

                if (oid.IsNull) break;

                Autodesk.AutoCAD.Internal.Utils.SetUndoMark(true);
                oid.QOpenForWrite<Polyline>(x => x.Layer = cv.LayerVejmidteSlukket);
                Autodesk.AutoCAD.Internal.Utils.SetUndoMark(false);
            }
            prdDbg("Finished!");
        }

        /// <command>DIM2INTERSECTVEJMIDTE</command>
        /// <summary>
        /// Detects intersections among all polylines on "Tændt" layer and inserts a missing
        /// vertex at each true intersection point on both involved polylines.
        /// </summary>
        /// <category>DIMENSIONERING V2</category>
        [CommandMethod("DIM2INTERSECTVEJMIDTE")]
        public void dim2intersectvejmidte()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var pls = localDb.HashSetOfType<Polyline>(tx)
                        .Where(x => x.Layer == cv.LayerVejmidteTændt)
                        .ToArray();

                    for (int i = 0; i < pls.Length; i++)
                    {
                        for (int j = i + 1; j < pls.Length; j++)
                        {
                            var pl1 = pls[i];
                            var pl2 = pls[j];

                            using Point3dCollection pts = new Point3dCollection();
                            pl1.IntersectWith(pl2, Autodesk.AutoCAD.DatabaseServices.Intersect.OnBothOperands, new Plane(),
                                pts, IntPtr.Zero, IntPtr.Zero);

                            foreach (Point3d pt in pts)
                            {
                                //Workaround for AutoCAD bug?
                                //I was getting more points than I should have
                                //Test to see if the point lies on BOTH polylines
                                //IntersectUtilities.UtilsCommon.Utils.DebugHelper.CreateDebugLine(pt, ColorByName("red"));
                                if (!pt.IsOnCurve(pl1, tol) || !pt.IsOnCurve(pl2, tol)) continue;
                                AddVertexIfMissing(pl1, pt);
                                AddVertexIfMissing(pl2, pt);
                            }
                        }
                    }

                    void AddVertexIfMissing(Polyline pl, Point3d pt)
                    {
                        pt = pl.GetClosestPointTo(pt, false);
                        double param = pl.GetParameterAtPoint(pt);
                        int index = (int)param;
                        if ((param - index) > tol)
                        {
                            pl.CheckOrOpenForWrite();
                            pl.AddVertexAt(index + 1, pt.To2d(), 0, 0, 0);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }

            prdDbg("Finished!");
        }

        /// <command>DIM2MARKENDPOINTS</command>
        /// <summary>
        /// Ensures vertices exist at intersections among "tændt" centerlines, removes existing endpoint
        /// blocks, and places new endpoint blocks on start/end points that are not shared with other
        /// polylines. Helpful for detecting false endpoints.
        /// </summary>
        /// <category>DIMENSIONERING V2</category>
        [CommandMethod("DIM2MARKENDPOINTS")]
        public void dim2markendpoints()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var pls = localDb.HashSetOfType<Polyline>(tx)
                        .Where(x => x.Layer == cv.LayerVejmidteTændt)
                        .ToArray();

                    for (int i = 0; i < pls.Length; i++)
                    {
                        for (int j = i + 1; j < pls.Length; j++)
                        {
                            var pl1 = pls[i];
                            var pl2 = pls[j];

                            using Point3dCollection pts = new Point3dCollection();
                            pl1.IntersectWith(pl2, Autodesk.AutoCAD.DatabaseServices.Intersect.OnBothOperands, new Plane(),
                                pts, IntPtr.Zero, IntPtr.Zero);

                            foreach (Point3d pt in pts)
                            {
                                //Workaround for AutoCAD bug?
                                //I was getting more points than I should have
                                //Test to see if the point lies on BOTH polylines
                                //IntersectUtilities.UtilsCommon.Utils.DebugHelper.CreateDebugLine(pt, ColorByName("red"));
                                if (!pt.IsOnCurve(pl1, tol) || !pt.IsOnCurve(pl2, tol)) continue;
                                AddVertexIfMissing(pl1, pt);
                                AddVertexIfMissing(pl2, pt);
                            }
                        }
                    }

                    void AddVertexIfMissing(Polyline pl, Point3d pt)
                    {
                        pt = pl.GetClosestPointTo(pt, false);
                        double param = pl.GetParameterAtPoint(pt);
                        int index = (int)param;
                        if ((param - index) > tol)
                        {
                            pl.CheckOrOpenForWrite();
                            pl.AddVertexAt(index + 1, pt.To2d(), 0, 0, 0);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    foreach (var br in localDb.GetBlockReferenceByName(cv.BlockEndPointName))
                    {
                        br.CheckOrOpenForWrite();
                        br.Erase(true);
                    }

                    var pls = localDb.HashSetOfType<Polyline>(tx)
                        .Where(x => x.Layer == cv.LayerVejmidteTændt)
                        .ToList();

                    localDb.CheckOrCreateLayer(cv.LayerEndPoint);

                    foreach (var pl in pls)
                    {
                        if (!pls.Any(x => pl.Id != x.Id && pl.StartPoint.IsOnCurve(x, tol)))
                        {
                            var br = localDb.CreateBlockWithAttributes(
                                cv.BlockEndPointName, pl.StartPoint);
                            br.CheckOrOpenForWrite();
                            br.Layer = cv.LayerEndPoint;
                        }

                        if (!pls.Any(x => pl.Id != x.Id && pl.EndPoint.IsOnCurve(x, tol)))
                        {
                            var br = localDb.CreateBlockWithAttributes(
                                cv.BlockEndPointName, pl.EndPoint);
                            br.CheckOrOpenForWrite();
                            br.Layer = cv.LayerEndPoint;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }

            prdDbg("Finished!");
        }

        /// <command>DIM2DRAWBUILDINGSEGMENTS</command>
        /// <summary>
        /// Clears existing connection lines, builds a graph from centerlines, supply points, building
        /// blocks and no-cross lines, then draws building connection segments.
        /// </summary>
        /// <category>DIMENSIONERING V2</category>
        [CommandMethod("DIM2DRAWBUILDINGSEGMENTS")]
        public void dim2drawbuildingsegments()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Delete previous connection lines
                    var lines = localDb.HashSetOfType<Line>(tx)
                        .Where(x => x.Layer == cv.LayerConnectionLine);
                    foreach (var line in lines)
                    {
                        line.CheckOrOpenForWrite();
                        line.Erase(true);
                    }
                    #endregion

                    var pls = localDb.HashSetOfType<Polyline>(tx)
                        .Where(x => x.Layer == cv.LayerVejmidteTændt)
                        .ToList();

                    var basePoints = localDb.HashSetOfType<BlockReference>(tx)
                        .Where(x => x.RealName() == cv.BlockSupplyPointName)
                        .Select(x => new Point2D(x.Position.X, x.Position.Y))
                        .ToList();

                    var acceptedTypes = HydraulicSettingsService.Instance.Settings.GetAcceptedBlockTypes();
                    var brs = localDb.HashSetOfType<BlockReference>(tx, true)
                        .Where(x => acceptedTypes.Contains(
                            PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR", "Type")));

                    var noCrossLines = localDb.HashSetOfType<Line>(tx)
                        .Where(x => x.Layer == cv.LayerNoCross);

                    var graph = new DimensioneringV2.GraphModelRoads.Graph();
                    graph.BuildGraph(pls, basePoints, brs, noCrossLines);

                    localDb.CheckOrCreateLayer(cv.LayerConnectionLine, cv.ConnectionLineColor);

                    foreach (var segment in graph.GetBuildingConnectionSegments())
                    {
                        Line line = new Line(segment.StartPoint.To3d(), segment.EndPoint.To3d());
                        line.Layer = cv.LayerConnectionLine;
                        line.AddEntityToDbModelSpace(localDb);
                    }
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }

            prdDbg("Finished!");
        }

        /// <command>DIM2MAP</command>
        /// <summary>
        /// Opens the Dimensionering V2 map palette, creating it if necessary, and makes it visible.
        /// </summary>
        /// <category>DIMENSIONERING V2</category>
        [CommandMethod("DIM2MAP")]
        public static void dim2map()
        {
            PropertySetManager.UpdatePropertySetDefinition(
                Application.DocumentManager.MdiActiveDocument.Database,
                PSetDefs.DefinedSets.BBR);

            AcContext.Current = SynchronizationContext.Current;

            if (Services.PaletteSetCache.paletteSet == null) Services.PaletteSetCache.paletteSet = new CustomPaletteSet();
            Services.PaletteSetCache.paletteSet.Visible = true;
            Services.PaletteSetCache.paletteSet.WasVisible = true;

            //var events = PaletteSetCache.paletteSet.GetType().GetEvents(BindingFlags.Public | BindingFlags.Instance);
            //foreach (var ev in events)
            //{
            //    var eh = new EventHandler((s, e) => 
            //    {
            //        prdDbg($"Event {ev.Name} fired!");
            //    });

            //    ev.AddEventHandler(PaletteSetCache.paletteSet, Delegate.CreateDelegate(
            //        ev.EventHandlerType, eh.Target, eh.Method));
            //}
        }

        /// <command>DIM2MAPCOLLECTFEATURES</command>
        /// <summary>
        /// THIS COMMAND MUST NOT BE RUN MANUALLY! It is called from Dim2Map window.
        /// Builds the road/building graph from drawing data, validates connectivity (drawing convex hulls
        /// around disconnected components), converts it into features, and loads them into the data
        /// service for the map palette.
        /// </summary>
        /// <category>DIMENSIONERING V2</category>
        [CommandMethod("DIM2MAPCOLLECTFEATURES")]
        public void dim2mapcollectfeatures()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            PropertySetManager.UpdatePropertySetDefinition(
                Application.DocumentManager.MdiActiveDocument.Database,
                PSetDefs.DefinedSets.BBR);

            if (PaletteSetCache.paletteSet == null) { prdDbg("This command is run from DIM2MAP"); return; }

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var pls = localDb.HashSetOfType<Polyline>(tx)
                        .Where(x => x.Layer == cv.LayerVejmidteTændt)
                        .ToList();

                    var basePoints = localDb.HashSetOfType<BlockReference>(tx)
                        .Where(x => x.RealName() == cv.BlockSupplyPointName)
                        .Select(x => new Point2D(x.Position.X, x.Position.Y))
                        .ToList();

                    var acceptedTypes = HydraulicSettingsService.Instance.Settings.GetAcceptedBlockTypes();                    
                    var brs = localDb.HashSetOfType<BlockReference>(tx, true)
                        .Where(x => acceptedTypes.Contains(
                            PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR", "Type")));

                    var noCrossLines = localDb.HashSetOfType<Line>(tx)
                        .Where(x => x.Layer == cv.LayerNoCross);

                    var graph = new DimensioneringV2.GraphModelRoads.Graph();
                    graph.BuildGraph(pls, basePoints, brs, noCrossLines);

                    //Validation
                    if (graph.ConnectedComponents.Count != graph.RootNodes.Count)
                    {
                        prdDbg("Graph is not connected!");

                        //Draw convex hull around connected elements to help find disconnects
                        foreach (ConnectedComponent component in graph.ConnectedComponents)
                        {
                            var ntsCoordinates = component.AllPoints()
                                .Select(p => new Coordinate(p.X, p.Y)) // ignore Z for 2D geometry
                                .ToArray();

                            if (ntsCoordinates.Length < 3)
                            {
                                foreach (var p in component.AllPoints())
                                {
                                    DebugHelper.CreateDebugLine(p, ColorByName("yellow"));
                                }
                                continue;
                            }

                            var geometryFactory =
                                NtsGeometryServices.Instance.CreateGeometryFactory(
                                    srid: 25832);
                            var multiPoint = geometryFactory
                                .CreateMultiPointFromCoords(ntsCoordinates);

                            var ch = multiPoint.ConvexHull();
                            var pl = NTSConversion.ConvertNTSPolygonToClosedPolyline((Polygon)ch);
                            pl.Closed = true;
                            pl.Layer = cv.LayerDebugLines;
                            pl.Color = ColorByName("yellow");
                            pl.ConstantWidth = 0.5;
                            pl.AddEntityToDbModelSpace(localDb);
                        }

                        tx.Commit();
                        return;
                    }

                    var features = GraphTranslator.TranslateGraph(graph);

                    if (features != null)
                    {
                        Services.DataService.Instance.LoadData(features);

                        try
                        {
                            prdDbg("Starting BBR layer loading...");
                            Services.BBRLayerService.Instance.LoadBBRFeatures(localDb, tx);
                            prdDbg($"BBR layer loading complete. Active: {Services.BBRLayerService.Instance.ActiveFeatures.Count()}, Inactive: {Services.BBRLayerService.Instance.InactiveFeatures.Count()}");
                        }
                        catch (System.Exception bbrEx)
                        {
                            prdDbg($"BBR layer loading failed: {bbrEx.Message}\n{bbrEx.StackTrace}");
                        }
                    }
                    //else fall through and if any errors where marked with debug lines
                    //they will be shown in the drawing
                }
                catch (System.Exception ex)
                {

                    if (ex.Message.Contains("DBG"))
                    {
                        prdDbg(ex.Message);
                        tx.Commit();
                        return;
                    }

                    prdDbg(ex);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }

            prdDbg("Finished!");
        }

        /// <command>DIM2MAPLOADELEVATIONS</command>
        /// <summary>
        /// THIS COMMAND MUST NOT BE RUN MANUALLY! It is called from Dim2Map window.
        /// Loads GeoTIFF files with surface elevation data.
        /// </summary>
        /// <category>DIMENSIONERING V2</category>
        [CommandMethod("DIM2MAPLOADELEVATIONS")]
        public void dim2maploadelevations()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            if (PaletteSetCache.paletteSet == null) { prdDbg("This command is run from DIM2MAP"); return; }

            using var tx = localDb.TransactionManager.StartTransaction();

            try
            {
                #region Determine extents
                var pls = localDb.ListOfType<Polyline>(tx)
                            .Where(x =>
                            x.Layer == cv.LayerVejmidteTændt ||
                            x.Layer == cv.LayerVejmidteSlukket)
                            .ToList();

                var brs = localDb.ListOfType<BlockReference>(tx, true)
                    .Where(x => cv.AllBlockTypes.Contains(
                        PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR", "Type")));

                var xs = (Polyline pl) => pl.GetPolyPoints().Select(x => x.X);
                var ys = (Polyline pl) => pl.GetPolyPoints().Select(x => x.Y);

                var maxX = (Entity ent) =>
                {
                    if (ent is Polyline pl) return xs(pl).Max();
                    else if (ent is BlockReference br) return br.Position.X;
                    else return 0.0;
                };

                var maxY = (Entity ent) =>
                {
                    if (ent is Polyline pl) return ys(pl).Max();
                    else if (ent is BlockReference br) return br.Position.Y;
                    else return 0.0;
                };

                var minX = (Entity ent) =>
                {
                    if (ent is Polyline pl) return xs(pl).Min();
                    else if (ent is BlockReference br) return br.Position.X;
                    else return 0.0;
                };

                var minY = (Entity ent) =>
                {
                    if (ent is Polyline pl) return ys(pl).Min();
                    else if (ent is BlockReference br) return br.Position.Y;
                    else return 0.0;
                };

                var extents = new Extents3d(
                    new Point3d(
                    Math.Min(pls.Select(x => minX(x)).Min(), brs.Select(x => minX(x)).Min()) - 2,
                    Math.Min(pls.Select(x => minY(x)).Min(), brs.Select(x => minY(x)).Min()) - 2, 0),
                new Point3d(
                    Math.Max(pls.Select(x => maxX(x)).Max(), brs.Select(x => maxX(x)).Max()) + 2,
                    Math.Max(pls.Select(x => maxY(x)).Max(), brs.Select(x => maxY(x)).Max()) + 2, 0));
                #endregion

                GdalRpcTransport.Instance.DownloadElevationData(extents, localDb.Filename);
            }
            catch (System.Exception ex)
            {
                if (ex.Message.Contains("DBG"))
                {
                    prdDbg(ex.Message);
                    tx.Commit();
                    return;
                }

                prdDbg(ex);
                tx.Abort();
                return;
            }
            tx.Commit();


            prdDbg("Finished!");
        }

        /// <command>DIM2COPYBBRFROMDWG</command>
        /// <summary>
        /// Prompts for a DWG file and copies all BBR block references from the
        /// source drawing into the current drawing.
        /// </summary>
        /// <category>DIMENSIONERING V2</category>
        [CommandMethod("DIM2COPYBBRFROMDWG")]
        public void dim2copybbrfromdwg()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "DWG Files (*.dwg)|*.dwg|All Files (*.*)|*.*",
                DefaultExt = "dwg",
                Title = "Select file where to copy BBR from",
                CheckFileExists = true // Ensures the user selects an existing file
            };

            string fileName;
            if (openFileDialog.ShowDialog() == true)
            {
                fileName = openFileDialog.FileName;
            }
            else
            {
                return;
            }

            if (string.IsNullOrEmpty(fileName)) return;

            if (!File.Exists(fileName))
            {
                MessageBox.Show("The file does not exist.", "File not found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using Transaction localTx = localDb.TransactionManager.StartTransaction();

            using Database sourceDb = new Database(false, true);
            sourceDb.ReadDwgFile(fileName, FileOpenMode.OpenForReadAndAllShare, false, null);
            using Transaction sourceTx = sourceDb.TransactionManager.StartTransaction();

            try
            {
                #region Import BBR blocks

                var bbrBlocks = sourceDb.ListOfType<BlockReference>(sourceTx)
                    .Where(x => cv.AllBlockTypes.Contains(x.RealName()))
                    .ToList();

                if (bbrBlocks.Count() > 0)
                {
                    ObjectIdCollection idsToClone = new ObjectIdCollection();
                    foreach (var bbr in bbrBlocks) idsToClone.Add(bbr.Id);

                    Oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(sourceDb);
                    Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);

                    BlockTable sourceBt = sourceDb.BlockTableId.Go<BlockTable>(sourceTx, OpenMode.ForRead);

                    IdMapping mapping = new IdMapping();
                    sourceDb.WblockCloneObjects(idsToClone, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);
                }
                #endregion

            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                sourceTx.Abort();
                localTx.Abort();
                return;
            }

            sourceTx.Commit();
            localTx.Commit();

            prdDbg("Finished!");
        }

        /// <command>DIM2COPYNOCROSSFROMDWG</command>
        /// <summary>
        /// Prompts for a DWG file and copies all no-cross lines from the source drawing into
        /// the current drawing.
        /// </summary>
        /// <category>DIMENSIONERING V2</category>
        [CommandMethod("DIM2COPYNOCROSSFROMDWG")]
        public void dim2copynocrossfromdwg()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "DWG Files (*.dwg)|*.dwg|All Files (*.*)|*.*",
                DefaultExt = "dwg",
                Title = "Select file where to copy nocross lines from",
                CheckFileExists = true // Ensures the user selects an existing file
            };

            string fileName;
            if (openFileDialog.ShowDialog() == true)
            {
                fileName = openFileDialog.FileName;
            }
            else
            {
                return;
            }

            if (string.IsNullOrEmpty(fileName)) return;

            if (!File.Exists(fileName))
            {
                MessageBox.Show("The file does not exist.", "File not found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using Transaction localTx = localDb.TransactionManager.StartTransaction();

            using Database sourceDb = new Database(false, true);
            sourceDb.ReadDwgFile(fileName, FileOpenMode.OpenForReadAndAllShare, false, null);
            using Transaction sourceTx = sourceDb.TransactionManager.StartTransaction();

            try
            {
                #region Import BBR blocks

                var noCrossLines = sourceDb.ListOfType<Line>(sourceTx)
                    .Where(x => x.Layer == cv.LayerNoCross)
                    .ToList();

                if (noCrossLines.Count() > 0)
                {
                    ObjectIdCollection idsToClone = new ObjectIdCollection();
                    foreach (var bbr in noCrossLines) idsToClone.Add(bbr.Id);

                    Oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(sourceDb);
                    Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);

                    BlockTable sourceBt = sourceDb.BlockTableId.Go<BlockTable>(sourceTx, OpenMode.ForRead);

                    IdMapping mapping = new IdMapping();
                    sourceDb.WblockCloneObjects(idsToClone, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);
                }
                #endregion

            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                sourceTx.Abort();
                localTx.Abort();
                return;
            }

            sourceTx.Commit();
            localTx.Commit();

            prdDbg("Finished!");
        }

        /// <command>DIM2VEJKLASSERASSIGNBBR</command>
        /// <summary>
        /// Aggregates BBR blocks by road name, opens a grid view to assign road classes (vejklasser),
        /// and writes the selected class back to each BBR instance.
        /// </summary>
        /// <category>DIMENSIONERING V2</category>
        [CommandMethod("DIM2VEJKLASSERASSIGNBBR")]
        public void dim2vejklasserassignbbr()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            PropertySetManager.UpdatePropertySetDefinition(
                localDb, PSetDefs.DefinedSets.BBR);

            using Transaction tx = localDb.TransactionManager.StartTransaction();
            try
            {
                var brs = localDb.HashSetOfType<BlockReference>(tx, true)
                    .Where(x => cv.AllBlockTypes.Contains(
                        PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR", "Type")));

                var bbrs = brs.Select(x => new BBR(x));

                var bbrsPåVeje = bbrs
                    .GroupBy(x => x.Vejnavn)
                    .Select(x => new VTV_BBR_Model(x.Key, x.ToList()))
                    .OrderBy(x => x.Vejnavn)
                    .ToList();

                var window = new VejklasserGridView(bbrsPåVeje);
                window.ShowDialog();

                var results = window.Results;

                foreach (VTV_BBR_Model result in results)
                {
                    foreach (var bbr in result.BBRs)
                    {
                        bbr.Vejklasse = result.Vejklasse;
                    }
                }
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }
            tx.Commit();

            prdDbg("Finished!");
        }

        /// <command>DIM2VEJKLASSERASSIGND2R</command>
        /// <summary>
        /// Opens a D2R file, groups single-building service segments by road name, lets the user assign
        /// road classes, applies them to the features, and saves a new D2R file with the assignments.
        /// </summary>
        /// <category>DIMENSIONERING V2</category>
        [CommandMethod("DIM2VEJKLASSERASSIGND2R")]
        public void dim2vejklasserassignd2r()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            PropertySetManager.UpdatePropertySetDefinition(
                localDb, PSetDefs.DefinedSets.BBR);

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "D2R Files (*.d2r)|*.d2r|All Files (*.*)|*.*",
                DefaultExt = "d2r",
                Title = "Open D2R File",
                CheckFileExists = true // Ensures the user selects an existing file
            };

            string fileName;
            if (openFileDialog.ShowDialog() == true)
            {
                fileName = openFileDialog.FileName;
            }
            else
            {
                return;
            }

            if (string.IsNullOrEmpty(fileName)) return;

            if (!File.Exists(fileName))
            {
                MessageBox.Show("The file does not exist.", "File not found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var options = new JsonSerializerOptions();
            options.WriteIndented = true;
            options.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals;
            options.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
            options.Converters.Add(new DimJsonConverter());
            options.Converters.Add(new AnalysisFeatureJsonConverter());
            options.Converters.Add(new UndirectedGraphJsonConverter());
            options.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
            UndirectedGraph<NodeJunction, EdgePipeSegment>[]? graphs =
                JsonSerializer.Deserialize<UndirectedGraph<NodeJunction, EdgePipeSegment>[]>(
                    File.ReadAllText(fileName), options);
            if (graphs == null) throw new System.Exception("Deserialization failed.");

            var afsPåVeje = graphs
                .SelectMany(x => x.Edges)
                .Select(x => x.PipeSegment)
                .Where(x => x.NumberOfBuildingsConnected == 1) //Should be stik
                .GroupBy(x => (string)x["Vejnavn"]!)
                .Select(x => new VTV_AF_Model(x.Key, x.ToList()))
                .OrderBy(x => x.Vejnavn)
                .ToList();

            var window = new VejklasserGridView(afsPåVeje);
            window.ShowDialog();

            var results = window.Results;

            foreach (VTV_AF_Model result in results)
            {
                foreach (var af in result.AFs)
                {
                    af["Vejklasse"] = result.Vejklasse;
                }
            }


            var newFileName = Path.Combine(
                Path.GetDirectoryName(fileName)!,
                Path.GetFileNameWithoutExtension(fileName) + "_Vejklasser.d2r");

            if (File.Exists(newFileName))
            {
                MessageBoxResult result = MessageBox.Show(
                    "The file already exists. Do you want to overwrite it?",
                    "File already exists",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;
            }

            options = new JsonSerializerOptions();
            options.WriteIndented = true;
            options.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals;
            options.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
            options.Converters.Add(new AnalysisFeatureJsonConverter());
            options.Converters.Add(new UndirectedGraphJsonConverter());
            options.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

            string json = JsonSerializer.Serialize(graphs, options);
            File.WriteAllText(newFileName, json);

            prdDbg($"Results saved to {newFileName}");

            prdDbg("Finished!");
        }

        /// <command>DIM2ENHEDERANALYZE</command>
        /// <summary>
        /// Analyzes BBR unit data to count residential units per building and writes results back to
        /// the drawing (e.g., AntalEnheder). Intended to quantify demand per building for planning.
        /// </summary>
        /// <category>DIMENSIONERING V2</category>
        [CommandMethod("DIM2ENHEDERANALYZE")]
        public void dimenhederanalyze()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            PropertySetManager.UpdatePropertySetDefinition(localDb, PSetDefs.DefinedSets.BBR);

            #region Get path
            ChoosePath cp = new ChoosePath();
            cp.ShowDialog();
            cp.Close();
            if (cp.Path.IsNoE()) return;
            #endregion

            #region Read data
            string basePath = @"X:\AutoCAD DRI - QGIS\BBR UDTRÆK";
            string path = basePath + "\\" + cp.Path + "\\";

            string enhederPath = path + "BBR_enhed.json";
            if (!File.Exists(enhederPath))
            { prdDbg("BBR_enhed.json does not exist! Download med RestHenter!"); return; }

            var enheder = IntersectUtilities.UtilsCommon.Serialization.Json
                .Deserialize<HashSet<BBREnhed.Enhed>>(enhederPath)
                .Where(x => x.status == "6" || x.status == "7");

            var enhedsDict = enheder
                .Where(x => x.status.IsNotNoE())
                .Where(x => x.enh020EnhedensAnvendelse.IsNotNoE())
                .GroupBy(x => x.bygning)
                .ToDictionary(x => x.Key, x => x.ToHashSet());
            #endregion

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                PropertySetManager bbrPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.BBR);
                PSetDefs.BBR bbrDef = new PSetDefs.BBR();

                var enhKoder = Csv.EnhKoder;
                var enhKoderDict = new Dictionary<string, bool>();
                foreach (var row in enhKoder.Rows)
                {
                    //Column "Nr."
                    string key = EnhKoder.Col(row, EnhKoder.Columns.Nr);
                    //Column "Beboelse"
                    bool result = int.TryParse(EnhKoder.Col(row, EnhKoder.Columns.Beboelse), out int val) && val == 1;
                    if (!enhKoderDict.ContainsKey(key))
                        enhKoderDict.Add(key, result);
                }

                //PrintTable(
                //    new string[] { "Anvendelseskode", "Beboelse" },
                //    enhKoderDict.Select(x => new object[] { x.Key, x.Value } as IEnumerable<object>)
                //    );

                var brs = localDb.HashSetOfType<BlockReference>(tx, true)
                    .Where(x => !x.BlockTableRecord.Go<BlockTableRecord>(tx).IsFromExternalReference);

                var pm = new ProgressMeter();
                pm.Start("Processing...");
                pm.SetLimit(brs.Count());

                try
                {
                    //Test to see if there are multiple bygninger with same id_lokalId
                    var test = brs.GroupBy(x => bbrPsm.ReadPropertyString(x, bbrDef.id_lokalId))
                        .Where(x => x.Count() > 1);

                    if (test.Count() > 0)
                    {
                        prdDbg(
                            $"Buildings with non-unique id_lokalId present!" +
                            $"Analysis cannot continue!");
                        prdDbg(string.Join("\n", test.Select(x => x.Key)));

                        prdDbg("Bygninger med samme bygningsnummer:");
                        var groupByCount = test
                            .Select(g => g.Count())
                            .GroupBy(count => count)
                            .Select(g => new { Antal = g.Key, Forekomst = g.Count() })
                            .OrderBy(x => x.Antal);

                        PrintTable(
                            ["Antal", "Forekomster"],
                            groupByCount.Select(g => new object[] { g.Antal, g.Forekomst } as IEnumerable<object>)
                            );

                        docCol.MdiActiveDocument.Editor.SetImpliedSelection(
                            test.SelectMany(x => x.Select(y => y.Id)).ToArray()
                            );

                        throw new System.Exception(
                            $"Buildings with non-unique id_lokalId present!" +
                            $"Analysis cannot continue!");
                    }

                    var results = new HashSet<(string, int)>();

                    foreach (var br in brs)
                    {
                        string brId = bbrPsm.ReadPropertyString(br, bbrDef.id_lokalId).ToLower();
                        if (brId.IsNoE())
                        {
                            if (br.BlockTableRecord.Go<BlockTableRecord>(tx).IsFromExternalReference)
                                continue;

                            prdDbg($"WARNING! id_lokalId is empty for block {br.Handle}!");
                        }

                        if (enhedsDict.TryGetValue(brId, out HashSet<BBREnhed.Enhed> units))
                        {
                            int count = units.Count(x => enhKoderDict[x.enh020EnhedensAnvendelse]);
                            if (count > 1)
                                bbrPsm.WritePropertyObject(br, bbrDef.AntalEnheder, count);

                            results.Add((brId, count == 0 ? 1 : count));
                        }

                        pm.MeterProgress();
                    }

                    pm.Stop();
                    pm.Dispose();

                    var analysis = results
                        .GroupBy(tuple => tuple.Item2)
                        .Select(group => new { Value = group.Key, Count = group.Select(x => x.Item1).Count() })
                        .OrderByDescending(x => x.Count);

                    PrintTable(
                        ["Antal enheder", "Antal forekomster"],
                        analysis.Select(x => new object[] { x.Value, x.Count } as IEnumerable<object>)
                        );
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    pm.Stop();
                    pm.Dispose();
                    return;
                }
                finally
                {

                }
                tx.Commit();

                //string html = ConvertToHtmlTree(list);
                //string htmlPath = "C:\\Temp\\" + "enheder.html";
                //File.WriteAllText(htmlPath, html);
            }

            prdDbg("Finished!");
            prdDbg("ADVARSEL: ER IKKE TESTET SAMMEN MED DIMCONNECTHUSNR!!!!!!!!!");
        }

        //[CommandMethod("DIM2TESTNAMING")]
        public void dim2testnaming()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var pls = localDb.HashSetOfType<Polyline>(tx)
                        .Where(x => x.Layer == cv.LayerVejmidteTændt)
                        .ToList();

                    var basePoints = localDb.HashSetOfType<BlockReference>(tx)
                        .Where(x => x.RealName() == cv.BlockSupplyPointName)
                        .Select(x => new Point2D(x.Position.X, x.Position.Y))
                        .ToList();

                    var graph = new DimensioneringV2.GraphModelRoads.Graph();
                    graph.BuildGraph(pls, basePoints);

                    localDb.CheckOrCreateLayer(cv.LayerNumbering);

                    foreach (var data in graph.GetSegmentsNumbering())
                    {
                        DBText dBText = new DBText();
                        //dBText.Justify = AttachmentPoint.MiddleCenter;
                        //dBText.VerticalMode = TextVerticalMode.TextVerticalMid;
                        //dBText.HorizontalMode = TextHorizontalMode.TextCenter;
                        dBText.TextString = data.Text;
                        dBText.Position = data.Point;
                        dBText.Layer = cv.LayerNumbering;
                        dBText.AddEntityToDbModelSpace(localDb);
                    }
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }

            prdDbg("Finished!");
        }

        //[CommandMethod("DIM2TESTROOTNODE")]
        public void dim2testrootnode()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var pls = localDb.HashSetOfType<Polyline>(tx)
                        .Where(x => x.Layer == cv.LayerVejmidteTændt)
                        .ToList();

                    var basePoints = localDb.HashSetOfType<BlockReference>(tx)
                        .Where(x => x.RealName() == cv.BlockSupplyPointName)
                        .Select(x => new Point2D(x.Position.X, x.Position.Y))
                        .ToList();

                    var graph = new DimensioneringV2.GraphModelRoads.Graph();
                    graph.BuildGraph(pls, basePoints);

                    foreach (var component in graph.ConnectedComponents)
                    {
                        //Utils.DebugHelper.CreateDebugLine(component.RootNode.GetMidpoint().To3d(), ColorByName("red"));
                    }
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }

            prdDbg("Finished!");
        }

        //[CommandMethod("DIM2TESTFEATURECREATION")]
        public void dim2testfeaturecreation()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var pls = localDb.HashSetOfType<Polyline>(tx)
                        .Where(x => x.Layer == cv.LayerVejmidteTændt)
                        .ToList();

                    var basePoints = localDb.HashSetOfType<BlockReference>(tx)
                        .Where(x => x.RealName() == cv.BlockSupplyPointName)
                        .Select(x => new Point2D(x.Position.X, x.Position.Y))
                        .ToList();

                    var acceptedTypes = HydraulicSettingsService.Instance.Settings.GetAcceptedBlockTypes();
                    var brs = localDb.HashSetOfType<BlockReference>(tx, true)
                        .Where(x => acceptedTypes.Contains(
                            PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR", "Type")));

                    var noCrossLines = localDb.HashSetOfType<Line>(tx)
                        .Where(x => x.Layer == cv.LayerNoCross);

                    var graph = new DimensioneringV2.GraphModelRoads.Graph();
                    graph.BuildGraph(pls, basePoints, brs, noCrossLines);

                    var features = GraphTranslator.TranslateGraph(graph);

                    if (features != null)
                    {
                        FeatureCollection fc = new FeatureCollection(features.SelectMany(x => x));
                        NetTopologySuite.IO.GeoJsonWriter writer = new NetTopologySuite.IO.GeoJsonWriter();
                        string json = writer.Write(fc);
                        using (var sw = new StreamWriter("C:\\Temp\\testfc.geojson"))
                        {
                            sw.Write(json);
                        }


                        //Shapefile.WriteAllFeatures(fc, "C:\\Temp\\testfc");

                        ////Create the projection file
                        //using (var sw = new StreamWriter("C:\\Temp\\testfc.prj"))
                        //{
                        //    //sw.Write(ProjNet.CoordinateSystems.ProjectedCoordinateSystem.WGS84_UTM(32, true));
                        //    sw.Write(@"PROJCS[""ETRS_1989_UTM_Zone_32N"",GEOGCS[""GCS_ETRS_1989"",DATUM[""D_ETRS_1989"",SPHEROID[""GRS_1980"",6378137.0,298.257222101]],PRIMEM[""Greenwich"",0.0],UNIT[""Degree"",0.0174532925199433]],PROJECTION[""Transverse_Mercator""],PARAMETER[""False_Easting"",500000.0],PARAMETER[""False_Northing"",0.0],PARAMETER[""Central_Meridian"",9.0],PARAMETER[""Scale_Factor"",0.9996],PARAMETER[""Latitude_Of_Origin"",0.0],UNIT[""Meter"",1.0]]");
                        //}
                    }

                }
                catch (System.Exception ex)
                {
                    if (ex is ArgumentException argex)
                    {
                        if (argex.Message.Contains("DBG"))
                        {
                            prdDbg(argex.Message);
                            tx.Commit();
                            return;
                        }
                    }

                    prdDbg(ex);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }

            prdDbg("Finished!");
        }
    }
}