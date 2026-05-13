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
        /// Prompts for two 3D polylines, previews how the nearest endpoint of the first polyline will project onto the
        /// second polyline, and then rebuilds the first polyline with that projected endpoint when the second polyline is
        /// selected. The command requires the source polyline to contain at least two vertices and does not allow the same
        /// 3D polyline to be selected for both roles.
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
                PromptEntityResult sourceResult = PromptForPolyline3d(editor, "\nSelect polyline 1: ");
                if (sourceResult.Status != PromptStatus.OK)
                {
                    return;
                }

                List<Point3d> sourcePoints;
                using (Transaction sourceTx = localDb.TransactionManager.StartTransaction())
                {
                    try
                    {
                        Polyline3d source = (Polyline3d)sourceTx.GetObject(sourceResult.ObjectId, OpenMode.ForRead);
                        sourcePoints = GetVertices(source, sourceTx);
                        if (sourcePoints.Count < 2)
                        {
                            editor.WriteMessage("\nPolyline 1 must contain at least two vertices.");
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

                using var preview = new Ler2ProjectPreview(localDb, editor, sourceResult.ObjectId, sourcePoints);

                PromptEntityResult targetResult = PromptForPolyline3d(editor, "\nSelect polyline 2: ");
                if (targetResult.Status != PromptStatus.OK)
                {
                    return;
                }

                if (sourceResult.ObjectId == targetResult.ObjectId)
                {
                    editor.WriteMessage("\nSelect two different 3D polylines.");
                    return;
                }

                using Transaction tx = localDb.TransactionManager.StartTransaction();
                try
                {
                    Polyline3d source = (Polyline3d)tx.GetObject(sourceResult.ObjectId, OpenMode.ForWrite);
                    Polyline3d target = (Polyline3d)tx.GetObject(targetResult.ObjectId, OpenMode.ForRead);

                    sourcePoints = GetVertices(source, tx);
                    if (sourcePoints.Count < 2)
                    {
                        editor.WriteMessage("\nPolyline 1 must contain at least two vertices.");
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
                    tx.Commit();

                    editor.WriteMessage(
                        $"\nPolyline 1 rebuilt. Endpoint {(projection.EndpointIndex == 0 ? "start" : "end")} projected onto polyline 2.");
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

        private sealed class Ler2ProjectPreview : IDisposable
        {
            private readonly Database database;
            private readonly Editor editor;
            private readonly ObjectId sourceId;
            private readonly List<Point3d> sourcePoints;
            private readonly IntegerCollection transientViewports = new IntegerCollection();
            private Entity? transientEntity;
            private ObjectId lastTargetId = ObjectId.Null;

            public Ler2ProjectPreview(Database database, Editor editor, ObjectId sourceId, List<Point3d> sourcePoints)
            {
                this.database = database;
                this.editor = editor;
                this.sourceId = sourceId;
                this.sourcePoints = new List<Point3d>(sourcePoints);
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
                    if (targetId == sourceId)
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

                    ShowPreview(BuildPreviewEntity(target));
                    lastTargetId = targetId;
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    ClearPreview();
                }
            }

            private Entity BuildPreviewEntity(Polyline3d target)
            {
                List<Point3d> previewPoints = new List<Point3d>(sourcePoints);
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

                return preview;
            }

            private void ShowPreview(Entity previewEntity)
            {
                ClearPreview();

                transientEntity = previewEntity;
                TransientManager.CurrentTransientManager.AddTransient(
                    transientEntity,
                    TransientDrawingMode.DirectShortTerm,
                    128,
                    transientViewports);
            }

            private void ClearPreview()
            {
                lastTargetId = ObjectId.Null;

                if (transientEntity is null)
                {
                    return;
                }

                try
                {
                    TransientManager.CurrentTransientManager.EraseTransient(transientEntity, transientViewports);
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                }

                transientEntity.Dispose();
                transientEntity = null;
            }
        }
    }
}
