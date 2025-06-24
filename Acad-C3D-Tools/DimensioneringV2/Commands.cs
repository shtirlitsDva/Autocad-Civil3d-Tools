using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using dbg = IntersectUtilities.UtilsCommon.Utils.DebugHelper;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using cv = DimensioneringV2.CommonVariables;
using Autodesk.AutoCAD.Geometry;
using DimensioneringV2.GraphModelRoads;
using DimensioneringV2.Geometry;
using IntersectUtilities;
using Autodesk.AutoCAD.Colors;
using DimensioneringV2.GraphFeatures;
using NetTopologySuite.Features;
using NetTopologySuite.IO.Esri;
using System.IO;
using DimensioneringV2.UI;
using Dreambuild.AutoCAD;
using DimensioneringV2.Services;
using Microsoft.Win32;
using System.Windows;
using DimensioneringV2.AutoCAD;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using DimensioneringV2.Vejklasser.Models;
using DimensioneringV2.Vejklasser.Views;
using Autodesk.Internal.Windows;

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

            Assembly.LoadFrom(@"X:\AutoCAD DRI - 01 Civil 3D\NetloadV2\2025\DimensioneringV2\OxyPlot.dll");
            Assembly.LoadFrom(@"X:\AutoCAD DRI - 01 Civil 3D\NetloadV2\2025\DimensioneringV2\OxyPlot.Wpf.dll");

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

                    var brs = localDb.HashSetOfType<BlockReference>(tx, true)
                        .Where(x => cv.AcceptedBlockTypes.Contains(
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

        [CommandMethod("DIM2MAP")]
        public static void dim2map()
        {
            AcContext.Current = SynchronizationContext.Current;

            PropertySetManager.UpdatePropertySetDefinition(
                Application.DocumentManager.MdiActiveDocument.Database,
                PSetDefs.DefinedSets.BBR);

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

        [CommandMethod("DIM2MAPCOLLECTFEATURES")]
        public void dim2mapcollectfeatures()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

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

                    var brs = localDb.HashSetOfType<BlockReference>(tx, true)
                        .Where(x => cv.AcceptedBlockTypes.Contains(
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
                            var hull = Algorithms.GetConvexHull(component.AllPoints());
                            if (hull != null && hull.Count > 0)
                            {
                                Polyline pl = new Polyline(hull.Count);
                                for (int i = 0; i < hull.Count; i++)
                                {
                                    pl.AddVertexAt(i, hull[i].To2d(), 0, 0, 0);
                                }
                                pl.Closed = true;
                                pl.Layer = cv.LayerDebugLines;
                                pl.Color = ColorByName("yellow");
                                pl.ConstantWidth = 0.5;
                                pl.AddEntityToDbModelSpace(localDb);
                            }
                        }

                        tx.Commit();
                        return;
                    }

                    var features = GraphTranslator.TranslateGraph(graph);

                    if (features != null)
                    {
                        Services.DataService.Instance.LoadData(features);
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

        [CommandMethod("DIM2VEJKLASSERASSIGN")]
        public void dim2vejklasserassign()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            PropertySetManager.UpdatePropertySetDefinition(
                localDb, PSetDefs.DefinedSets.BBR);

            using Transaction tx = localDb.TransactionManager.StartTransaction();
            try
            {
                var brs = localDb.HashSetOfType<BlockReference>(tx, true)
                    .Where(x => cv.AcceptedBlockTypes.Contains(
                        PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR", "Type")));

                var bbrs = brs.Select(x => new BBR(x));

                var bbrsPåVeje = bbrs
                    .GroupBy(x => x.Vejnavn)
                    .Select(x => new VejnavnTilVejklasseModel(x.Key, x.ToList()))
                    .OrderBy(x => x.Vejnavn)
                    .ToList();

                var window = new VejklasserGridView(bbrsPåVeje);
                window.ShowDialog();

                var results = window.Results;

                foreach (var result in results)
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

                    var brs = localDb.HashSetOfType<BlockReference>(tx, true)
                        .Where(x => cv.AcceptedBlockTypes.Contains(
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