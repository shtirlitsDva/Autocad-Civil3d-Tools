using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;
using static IntersectUtilities.UtilsCommon.Utils;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        private const string Ler2ProjectCommandName = "Ler2Project";

        /// <command>Ler2Project</command>
        /// <summary>
        /// Prompts for one or more source 3D polylines, previews how the nearest endpoint of each selected source polyline
        /// will project onto the target 3D polyline, and then rebuilds each source polyline with its projected endpoint
        /// when the target polyline is selected. Every source polyline must contain at least two vertices, and the target
        /// polyline cannot also be part of the source selection.
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
                PromptSelectionResult sourceResult = PromptForSourcePolylines(editor, "\nSelect polyline 1: ");
                if (sourceResult.Status != PromptStatus.OK || sourceResult.Value is null)
                {
                    return;
                }

                List<Ler2ProjectSourcePolyline> sourcePolylines = new List<Ler2ProjectSourcePolyline>();
                HashSet<ObjectId> sourceIds = new HashSet<ObjectId>();
                using (Transaction sourceTx = localDb.TransactionManager.StartTransaction())
                {
                    try
                    {
                        foreach (ObjectId sourceId in sourceResult.Value.GetObjectIds())
                        {
                            if (sourceTx.GetObject(sourceId, OpenMode.ForRead, false) is not Polyline3d source)
                            {
                                editor.WriteMessage("\nSelection must contain only 3D polylines.");
                                return;
                            }

                            List<Point3d> sourcePoints = GetVertices(source, sourceTx);
                            if (sourcePoints.Count < 2)
                            {
                                editor.WriteMessage("\nEach polyline 1 must contain at least two vertices.");
                                return;
                            }

                            sourcePolylines.Add(new Ler2ProjectSourcePolyline(sourceId, sourcePoints));
                            sourceIds.Add(sourceId);
                        }

                        if (sourcePolylines.Count == 0)
                        {
                            editor.WriteMessage("\nNo 3D polylines were selected.");
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

                using var preview = new Ler2ProjectPreview(localDb, editor, sourcePolylines);

                PromptEntityResult targetResult = PromptForPolyline3d(editor, "\nSelect polyline 2: ");
                if (targetResult.Status != PromptStatus.OK)
                {
                    return;
                }

                if (sourceIds.Contains(targetResult.ObjectId))
                {
                    editor.WriteMessage("\nPolyline 2 cannot also be part of the polyline 1 selection.");
                    return;
                }

                using Transaction tx = localDb.TransactionManager.StartTransaction();
                try
                {
                    Polyline3d target = (Polyline3d)tx.GetObject(targetResult.ObjectId, OpenMode.ForRead);
                    int rebuiltCount = 0;

                    foreach (Ler2ProjectSourcePolyline sourceData in sourcePolylines)
                    {
                        Polyline3d source = (Polyline3d)tx.GetObject(sourceData.SourceId, OpenMode.ForWrite);
                        List<Point3d> sourcePoints = GetVertices(source, tx);
                        if (sourcePoints.Count < 2)
                        {
                            editor.WriteMessage("\nEach polyline 1 must contain at least two vertices.");
                            return;
                        }

                        Ler2ProjectProjectionResult projection = ProjectEndpoint(sourcePoints, target);
                        sourcePoints[projection.EndpointIndex] = projection.ProjectedPoint;

                        BlockTableRecord owner = (BlockTableRecord)tx.GetObject(source.OwnerId, OpenMode.ForWrite);
                        var rebuiltPolyline = new Polyline3d(
                            source.PolyType,
                            new Point3dCollection(sourcePoints.ToArray()),
                            source.Closed);
                        rebuiltPolyline.SetPropertiesFrom(source);

                        owner.AppendEntity(rebuiltPolyline);
                        tx.AddNewlyCreatedDBObject(rebuiltPolyline, true);

                        source.Erase();
                        rebuiltCount++;
                    }

                    tx.Commit();

                    editor.WriteMessage(
                        $"\nProjected {rebuiltCount} polyline 1 object(s) onto polyline 2.");
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

        private static PromptSelectionResult PromptForSourcePolylines(Editor editor, string message)
        {
            PromptSelectionOptions options = new PromptSelectionOptions
            {
                MessageForAdding = message,
                MessageForRemoval = "\nRemove polyline 1: "
            };
            SelectionFilter filter = new SelectionFilter(
                new[]
                {
                    new TypedValue((int)DxfCode.Start, "POLYLINE")
                });

            return editor.GetSelection(options, filter);
        }

        private static PromptEntityResult PromptForPolyline3d(Editor editor, string message)
        {
            PromptEntityOptions options = new PromptEntityOptions(message);
            options.SetRejectMessage("\nSelect a 3D polyline.");
            options.AddAllowedClass(typeof(Polyline3d), false);
            return editor.GetEntity(options);
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

        private static Ler2ProjectProjectionResult ProjectEndpoint(IReadOnlyList<Point3d> sourcePoints, Polyline3d target)
        {
            Point3d startPoint = sourcePoints[0];
            Point3d endPoint = sourcePoints[sourcePoints.Count - 1];

            Point3d startProjection = target.GetClosestPointTo(startPoint, false);
            Point3d endProjection = target.GetClosestPointTo(endPoint, false);

            double startDistance = startPoint.DistanceTo(startProjection);
            double endDistance = endPoint.DistanceTo(endProjection);

            return startDistance <= endDistance
                ? new Ler2ProjectProjectionResult(0, startProjection)
                : new Ler2ProjectProjectionResult(sourcePoints.Count - 1, endProjection);
        }

        private readonly record struct Ler2ProjectProjectionResult(int EndpointIndex, Point3d ProjectedPoint);
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

            public Ler2ProjectPreview(Database database, Editor editor, List<Ler2ProjectSourcePolyline> sourcePolylines)
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

                    ShowPreview(BuildPreviewEntities(target));
                    lastTargetId = targetId;
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    ClearPreview();
                }
            }

            private List<Entity> BuildPreviewEntities(Polyline3d target)
            {
                List<Entity> previewEntities = new List<Entity>();
                foreach (Ler2ProjectSourcePolyline sourcePolyline in sourcePolylines)
                {
                    List<Point3d> previewPoints = new List<Point3d>(sourcePolyline.SourcePoints);
                    Ler2ProjectProjectionResult projection = ProjectEndpoint(previewPoints, target);
                    previewPoints[projection.EndpointIndex] = projection.ProjectedPoint;

                    Polyline3d preview = new Polyline3d(
                        Poly3dType.SimplePoly,
                        new Point3dCollection(previewPoints.ToArray()),
                        false)
                    {
                        ColorIndex = 8,
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
