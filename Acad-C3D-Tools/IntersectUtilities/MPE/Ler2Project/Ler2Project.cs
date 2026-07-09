using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;
using IntersectUtilities.MPE.Ler3DNetwork;
using IntersectUtilities.MPE.Ler3DNetwork.LerConnectNetwork;
using static IntersectUtilities.UtilsCommon.Utils;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        private const string Ler2ProjectCommandName = "Ler2Project";

        // Remembered across invocations in the same AutoCAD session, so each run
        // pre-fills the last used values. Seeded with LERConnectNetwork's palette
        // defaults (20 per-mille slope, 0.1 m check distance).
        private static double _ler2ProjectSlopePermille = 20.0;
        private static double _ler2ProjectDistance = 0.1;

        /// <command>Ler2Project</command>
        /// <summary>
        /// A single-target, non-palette form of LERCONNECTNETWORK. Window/crossing multi-selects the source 3D polylines,
        /// then previews how each source connects onto a hovered target 3D polyline and rebuilds each source when the target is
        /// picked. The target prompt carries an inline "Settings" keyword to change the minimum slope in per-mille and/or the
        /// check distance (both persist as the defaults for later runs, and the hover preview reflects changes live); Settings
        /// lives here rather than on the source prompt because GetSelection ignores inline keywords on this AutoCAD build. The
        /// connection reuses LERCONNECTNETWORK's projection unchanged:
        /// both source and target are flattened to XY, the source's nearest end is extended along its tangent to intersect
        /// the target in XY (or pivots at an existing crossing), that point is lifted to the target's real elevation, and the
        /// source is rebuilt sloping upward away from the pivot at the given per-mille. Because the target is picked
        /// explicitly, the connection is never rejected by distance (unlike LERCONNECTNETWORK); the check distance only drives
        /// the "too long / misses target" flag. Each source is rewritten in place, keeping its ObjectId, layer, XData and
        /// property sets. Every source polyline must contain at least two vertices, and the target cannot also be a source.
        /// </summary>
        /// <category>MPE</category>
        [CommandMethod(Ler2ProjectCommandName, CommandFlags.Modal)]
        public void Ler2Project()
        {
            DocumentCollection docCol = AcadApp.DocumentManager;
            Document doc = docCol.MdiActiveDocument;
            Database localDb = doc.Database;
            Editor editor = doc.Editor;

            try
            {
                // Window/crossing multi-select of the source polylines. Settings is NOT
                // offered here: GetSelection silently ignores inline keywords on this AutoCAD
                // build (verified), so it lives on the target prompt (GetEntity) instead.
                List<Ler2ProjectSourcePolyline> sourcePolylines = new List<Ler2ProjectSourcePolyline>();
                HashSet<ObjectId> sourceIds = new HashSet<ObjectId>();
                using (Transaction sourceTx = localDb.TransactionManager.StartTransaction())
                {
                    try
                    {
                        PromptSelectionOptions pso = new PromptSelectionOptions
                        {
                            MessageForAdding = "\nSelect source polylines:"
                        };
                        SelectionFilter filter = new SelectionFilter(
                            new[] { new TypedValue((int)DxfCode.Start, "POLYLINE") });

                        PromptSelectionResult sourceResult = editor.GetSelection(pso, filter);
                        if (sourceResult.Status != PromptStatus.OK || sourceResult.Value is null)
                        {
                            sourceTx.Abort();
                            return;
                        }

                        foreach (ObjectId sourceId in sourceResult.Value.GetObjectIds())
                        {
                            if (!sourceIds.Add(sourceId))
                            {
                                continue;
                            }

                            if (sourceTx.GetObject(sourceId, OpenMode.ForRead, false) is not Polyline3d source)
                            {
                                editor.WriteMessage("\nSelection must contain only 3D polylines.");
                                sourceIds.Remove(sourceId);
                                continue;
                            }

                            List<Point3d> sourcePoints = GetVertices(source, sourceTx);
                            if (sourcePoints.Count < 2)
                            {
                                editor.WriteMessage("\nEach source polyline must contain at least two vertices.");
                                sourceIds.Remove(sourceId);
                                continue;
                            }

                            sourcePolylines.Add(new Ler2ProjectSourcePolyline(sourceId, sourcePoints));
                        }

                        if (sourcePolylines.Count == 0)
                        {
                            editor.WriteMessage("\nNo valid source polylines selected.");
                            sourceTx.Abort();
                            return;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        prdDbg(ex);
                        sourceTx.Abort();
                        editor.WriteMessage($"\n{Ler2ProjectCommandName} failed. See debug output for details.");
                        return;
                    }

                    sourceTx.Commit();
                }

                // The preview reads the current slope/distance live (from the static fields),
                // so changing them via Settings on the target prompt refreshes the preview.
                using var preview = new Ler2ProjectPreview(localDb, editor, sourcePolylines);

                // Target prompt carries the inline [Settings] keyword (GetEntity honors
                // keywords, unlike GetSelection). Choosing Settings changes slope/distance and
                // re-prompts; picking a polyline proceeds to the projection.
                ObjectId targetId;
                while (true)
                {
                    PromptEntityOptions peo = new PromptEntityOptions("\nSelect target polyline or");
                    peo.SetRejectMessage("\nSelect a 3D polyline.");
                    peo.AddAllowedClass(typeof(Polyline3d), false);
                    peo.Keywords.Add("Settings");

                    PromptEntityResult targetResult = editor.GetEntity(peo);
                    if (targetResult.Status == PromptStatus.Keyword)
                    {
                        PromptForSettings(editor);
                        continue;
                    }
                    if (targetResult.Status != PromptStatus.OK)
                    {
                        return;
                    }
                    if (sourceIds.Contains(targetResult.ObjectId))
                    {
                        editor.WriteMessage("\nTarget polyline cannot also be part of the source selection.");
                        continue;
                    }
                    targetId = targetResult.ObjectId;
                    break;
                }

                // Current (possibly Settings-adjusted) projection parameters.
                double permille = _ler2ProjectSlopePermille;
                double distance = _ler2ProjectDistance;

                using Transaction tx = localDb.TransactionManager.StartTransaction();
                try
                {
                    Polyline3d target = (Polyline3d)tx.GetObject(targetId, OpenMode.ForRead);
                    List<Point3d> targetPoints = GetVertices(target, tx);
                    if (targetPoints.Count < 2)
                    {
                        editor.WriteMessage("\nTarget polyline must contain at least two vertices.");
                        tx.Abort();
                        return;
                    }

                    LERNetwork[] networks = { BuildTargetNetwork(targetId, targetPoints) };

                    int rebuiltCount = 0;
                    int flaggedCount = 0;
                    int failedCount = 0;

                    foreach (Ler2ProjectSourcePolyline sourceData in sourcePolylines)
                    {
                        LERConnectionResult? result = Connect(sourceData, networks[0], permille, distance);
                        if (result is null || result.Status != LERConnectionStatus.Connected || result.NewPoints is null)
                        {
                            failedCount++;
                            continue;
                        }

                        if (result.Error != LERConnectionError.None)
                        {
                            flaggedCount++;
                        }

                        // Rewrite in place — keeps ObjectId, layer, XData and property sets.
                        if (LerRebuild.ReplacePolyline3d(tx, sourceData.SourceId, result.NewPoints))
                        {
                            rebuiltCount++;
                        }
                        else
                        {
                            failedCount++;
                        }
                    }

                    tx.Commit();

                    string message = $"\nProjected {rebuiltCount} source polyline(s) onto the target.";
                    if (flaggedCount > 0)
                    {
                        message += $" {flaggedCount} flagged (too long / misses target).";
                    }
                    if (failedCount > 0)
                    {
                        message += $" {failedCount} could not connect.";
                    }
                    editor.WriteMessage(message);
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    tx.Abort();
                    editor.WriteMessage($"\n{Ler2ProjectCommandName} failed. See debug output for details.");
                    return;
                }
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                editor.WriteMessage($"\n{Ler2ProjectCommandName} failed. See debug output for details.");
            }
        }

        // Sub-menu reached via the "Settings" keyword. Lets the operator change the
        // slope and/or the check distance; both persist in the session-static fields
        // and become the defaults for subsequent runs. Enter/Exit returns to selection.
        // The exit keyword is "Exit" (not "Done") so its shortcut "E" doesn't collide
        // with "Distance" on the letter "D".
        private static void PromptForSettings(Editor editor)
        {
            while (true)
            {
                PromptKeywordOptions options = new PromptKeywordOptions(
                    $"\nSettings: slope={_ler2ProjectSlopePermille:0.###} per-mille, "
                    + $"distance={_ler2ProjectDistance:0.###} m - change");
                options.Keywords.Add("Slope");
                options.Keywords.Add("Distance");
                options.Keywords.Add("Exit");
                options.Keywords.Default = "Exit";
                options.AllowNone = true;

                PromptResult result = editor.GetKeywords(options);
                if (result.Status != PromptStatus.OK)
                {
                    return;
                }

                switch (result.StringResult)
                {
                    case "Slope":
                        if (PromptForPositiveDouble(
                                editor, "\nMinimum slope (per-mille): ", _ler2ProjectSlopePermille, out double slope))
                        {
                            _ler2ProjectSlopePermille = slope;
                        }
                        break;

                    case "Distance":
                        if (PromptForPositiveDouble(
                                editor, "\nCheck distance (m): ", _ler2ProjectDistance, out double distance))
                        {
                            _ler2ProjectDistance = distance;
                        }
                        break;

                    default:
                        return;
                }
            }
        }

        private static bool PromptForPositiveDouble(Editor editor, string message, double defaultValue, out double value)
        {
            PromptDoubleOptions options = new PromptDoubleOptions(message)
            {
                DefaultValue = defaultValue,
                UseDefaultValue = true,
                AllowNegative = false,
                AllowZero = false,
                AllowNone = false
            };
            PromptDoubleResult result = editor.GetDouble(options);
            value = result.Value;
            return result.Status == PromptStatus.OK;
        }

        private static List<Point3d> GetVertices(Polyline3d polyline, Transaction transaction)
        {
            List<Point3d> points = new List<Point3d>();
            foreach (ObjectId vertexId in polyline)
            {
                PolylineVertex3d vertex = (PolylineVertex3d)transaction.GetObject(vertexId, OpenMode.ForRead);
                points.Add(vertex.Position);
            }

            return points;
        }

        // Wraps the single picked target polyline as a one-member LERNetwork so the
        // LERConnectNetwork analyzer can be reused verbatim.
        private static LERNetwork BuildTargetNetwork(ObjectId targetId, IReadOnlyList<Point3d> targetPoints)
        {
            LERNetwork network = new LERNetwork(0, LERConnectNetworkAnalyzer.ColorForIndex(0));
            network.MemberIds.Add(targetId);
            network.MemberPoints.Add(targetPoints);
            return network;
        }

        // Runs the full LERConnectNetwork projection for one source against the
        // single target. AssignMain is called with an unbounded distance so the
        // explicitly-picked target is never rejected — it only decides which of the
        // source's two ends connects. Solve then does the XY tangent-extension /
        // crossing projection and the per-mille slope lift; the prompted distance
        // drives Solve's "too long / misses target" flag only.
        private static LERConnectionResult? Connect(
            Ler2ProjectSourcePolyline source, LERNetwork target, double permille, double distance)
        {
            LERNetwork[] networks = { target };
            LERMainAssignment? assignment =
                LERConnectNetworkAnalyzer.AssignMain(source.SourcePoints, networks, double.MaxValue);
            if (assignment is null)
            {
                return null;
            }

            return LERConnectNetworkAnalyzer.Solve(
                source.SourcePoints, source.SourceId, target, assignment.ConnectAtEnd, permille, distance, networks);
        }

        private readonly record struct Ler2ProjectSourcePolyline(ObjectId SourceId, List<Point3d> SourcePoints);

        private sealed class Ler2ProjectPreview : IDisposable
        {
            private readonly Database database;
            private readonly Editor editor;
            private readonly List<Ler2ProjectSourcePolyline> sourcePolylines;
            private readonly HashSet<ObjectId> sourceIds;
            private readonly IntegerCollection transientViewports = new IntegerCollection();
            private readonly List<Entity> transientEntities = new List<Entity>();
            private ObjectId lastTargetId = ObjectId.Null;

            public Ler2ProjectPreview(
                Database database,
                Editor editor,
                List<Ler2ProjectSourcePolyline> sourcePolylines)
            {
                this.database = database;
                this.editor = editor;
                this.sourcePolylines = sourcePolylines;
                sourceIds = new HashSet<ObjectId>(sourcePolylines.ConvertAll(x => x.SourceId));
                editor.PointMonitor += OnPointMonitor;
            }

            public void Dispose()
            {
                editor.PointMonitor -= OnPointMonitor;
                ClearPreview();
            }

            private void OnPointMonitor(object sender, PointMonitorEventArgs e)
            {
                try
                {
                    FullSubentityPath[] pickedEntities = e.Context.GetPickedEntities();
                    if (pickedEntities.Length == 0)
                    {
                        ClearPreview();
                        return;
                    }

                    ObjectId[] containerIds = pickedEntities[0].GetObjectIds();
                    if (containerIds.Length == 0)
                    {
                        ClearPreview();
                        return;
                    }

                    ObjectId targetId = containerIds[containerIds.Length - 1];
                    if (sourceIds.Contains(targetId))
                    {
                        ClearPreview();
                        return;
                    }

                    if (targetId == lastTargetId)
                    {
                        return;
                    }

                    using Transaction tx = database.TransactionManager.StartOpenCloseTransaction();
                    if (tx.GetObject(targetId, OpenMode.ForRead, false) is not Polyline3d target)
                    {
                        ClearPreview();
                        return;
                    }

                    List<Point3d> targetPoints = GetVertices(target, tx);
                    if (targetPoints.Count < 2)
                    {
                        ClearPreview();
                        return;
                    }

                    ShowPreview(BuildPreviewEntities(targetId, targetPoints));
                    lastTargetId = targetId;
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    ClearPreview();
                }
            }

            private List<Entity> BuildPreviewEntities(ObjectId targetId, IReadOnlyList<Point3d> targetPoints)
            {
                List<Entity> previewEntities = new List<Entity>();
                LERNetwork network = BuildTargetNetwork(targetId, targetPoints);

                // Read the current settings live so a Settings change on the target prompt
                // is reflected on the next hover.
                double permille = _ler2ProjectSlopePermille;
                double distance = _ler2ProjectDistance;

                foreach (Ler2ProjectSourcePolyline sourcePolyline in sourcePolylines)
                {
                    LERConnectionResult? result = Connect(sourcePolyline, network, permille, distance);
                    if (result is null || result.Status != LERConnectionStatus.Connected || result.NewPoints is null)
                    {
                        continue;
                    }

                    Polyline3d preview = new Polyline3d(
                        Poly3dType.SimplePoly,
                        new Point3dCollection(result.NewPoints.ToArray()),
                        false)
                    {
                        // Grey for a clean connection, magenta when flagged.
                        ColorIndex = result.Error == LERConnectionError.None ? (short)8 : (short)6,
                        Transparency = new Transparency(150)
                    };

                    previewEntities.Add(preview);
                }

                return previewEntities;
            }

            private void ShowPreview(List<Entity> previewEntities)
            {
                ClearPreview();

                foreach (Entity previewEntity in previewEntities)
                {
                    transientEntities.Add(previewEntity);
                    TransientManager.CurrentTransientManager.AddTransient(
                        previewEntity,
                        TransientDrawingMode.DirectShortTerm,
                        128,
                        transientViewports);
                }
            }

            private void ClearPreview()
            {
                lastTargetId = ObjectId.Null;

                if (transientEntities.Count == 0)
                {
                    return;
                }

                foreach (Entity transientEntity in transientEntities)
                {
                    try
                    {
                        TransientManager.CurrentTransientManager.EraseTransient(transientEntity, transientViewports);
                    }
                    catch (System.Exception ex)
                    {
                        prdDbg(ex);
                    }

                    transientEntity.Dispose();
                }

                transientEntities.Clear();
            }
        }
    }
}
