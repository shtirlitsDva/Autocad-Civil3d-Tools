using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using IntersectUtilities.LER2.AttachStik;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        private static double attachStikSlope = 20.0;
        private static double attachStikMaximumDistance = 1.0;

        /// <command>LER2ATTACHSTIK</command>
        /// <summary>
        /// Extends or trims selected branch endpoints along their terminal XY segments onto unselected 3D polyline edges,
        /// interpolates the connection elevation and applies a signed per-mille slope.
        /// With two unknown endpoints, uses the connection requiring the least endpoint movement. Positive
        /// slope rises away from the main. Both prompts remember their session values.
        /// </summary>
        /// <category>LER2</category>
        [CommandMethod("LER2ATTACHSTIK", CommandFlags.Modal | CommandFlags.UsePickSet | CommandFlags.NoPaperSpace)]
        public void Ler2AttachStik()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document is null) return; // AutoCAD boundary: no active document.
            Editor editor = document.Editor;
            try
            {
                PromptSelectionResult selection = editor.SelectImplied();
                if (selection.Status != PromptStatus.OK)
                    selection = editor.GetSelection(
                        new PromptSelectionOptions
                        {
                            MessageForAdding = "\nSelect branch 3D polylines to attach: ",
                            RejectObjectsOnLockedLayers = true,
                            RejectObjectsFromNonCurrentSpace = true
                        },
                        new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "POLYLINE") }));
                if (selection.Status != PromptStatus.OK) return;

                PromptDoubleResult slopeInput = editor.GetDouble(new PromptDoubleOptions(
                    "\nSlope in promille (positive rises away from main)")
                {
                    AllowNegative = true, AllowZero = true, AllowNone = true,
                    UseDefaultValue = true, DefaultValue = attachStikSlope
                });
                if (slopeInput.Status != PromptStatus.OK && slopeInput.Status != PromptStatus.None) return;
                double slopeValue = slopeInput.Status == PromptStatus.None ? attachStikSlope : slopeInput.Value;
                PromptDoubleResult distanceInput = editor.GetDouble(new PromptDoubleOptions(
                    "\nMaximum attachment distance (drawing units)")
                {
                    AllowNegative = false, AllowZero = false, AllowNone = true,
                    UseDefaultValue = true, DefaultValue = attachStikMaximumDistance
                });
                if (distanceInput.Status != PromptStatus.OK && distanceInput.Status != PromptStatus.None) return;
                double distanceValue = distanceInput.Status == PromptStatus.None ? attachStikMaximumDistance : distanceInput.Value;
                if (!double.IsFinite(slopeValue) || !double.IsFinite(distanceValue) || distanceValue <= 0)
                {
                    editor.WriteMessage("\nEnter a finite slope and a positive, finite distance. Nothing changed.");
                    return;
                }

                attachStikSlope = slopeValue;
                attachStikMaximumDistance = distanceValue;
                StikAttachmentSummary result = StikAttachmentDrawing.Apply(
                    document.Database, selection.Value.GetObjectIds(), slopeValue, distanceValue);
                foreach (string message in result.Messages) editor.WriteMessage("\n" + message);
                editor.WriteMessage($"\nLER2ATTACHSTIK: attached {result.Attached}; skipped {result.Skipped}. " +
                    $"Slope {slopeValue:0.###}‰; maximum distance {distanceValue:0.###} drawing units.");
            }
            catch (System.Exception exception)
            {
                // AutoCAD API boundary. Apply owns one transaction, which rolls back
                // all branch edits if the API rejects any write.
                editor.WriteMessage("\nLER2ATTACHSTIK failed: " + exception.Message);
            }
        }
    }
}

namespace IntersectUtilities.LER2.AttachStik
{
    internal sealed record StikAttachmentSummary(int Attached, int Skipped, IReadOnlyList<string> Messages);

    internal static class StikAttachmentDrawing
    {
        public static StikAttachmentSummary Apply(
            Database database, IReadOnlyCollection<ObjectId> selectedIds, double slope, double maximumDistance)
        {
            using Transaction transaction = database.TransactionManager.StartTransaction();
            HashSet<ObjectId> selected = selectedIds.ToHashSet();
            List<string> messages = new();
            List<Branch> branches = new();

            foreach (ObjectId id in selected)
            {
                DBObject value = transaction.GetObject(id, OpenMode.ForRead);
                if (value is not Polyline3d branch)
                {
                    messages.Add($"Skipped {value.Handle}: not a 3D polyline.");
                    continue;
                }
                if (branch.OwnerId != database.CurrentSpaceId || branch.Closed || branch.PolyType != Poly3dType.SimplePoly)
                {
                    messages.Add($"Skipped {branch.Handle}: requires an open, simple 3D polyline in the current space.");
                    continue;
                }
                LayerTableRecord layer = (LayerTableRecord)transaction.GetObject(branch.LayerId, OpenMode.ForRead);
                if (layer.IsLocked)
                {
                    messages.Add($"Skipped {branch.Handle}: layer is locked.");
                    continue;
                }
                PolylineVertex3d[] vertices = ReadVertices(branch, transaction);
                branches.Add(new Branch(branch, vertices, vertices.Select(v => FromPoint(v.Position)).ToArray()));
            }

            // Snapshot unselected mains before changing any branch. Repaired branches
            // cannot become targets part-way through the command; order is irrelevant.
            STRtree<StikEdge> index = new();
            BlockTableRecord space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForRead);
            foreach (ObjectId id in space)
            {
                if (selected.Contains(id)) continue;
                if (transaction.GetObject(id, OpenMode.ForRead) is not Polyline3d main || main.PolyType != Poly3dType.SimplePoly)
                    continue;
                StikPoint[] points = ReadVertices(main, transaction).Select(v => FromPoint(v.Position)).ToArray();
                int edgeCount = main.Closed ? points.Length : points.Length - 1;
                for (int i = 0; i < edgeCount; i++)
                {
                    StikEdge edge = new(points[i], points[(i + 1) % points.Length]);
                    if (!StikAttachmentGeometry.IsUsableEdge(edge)) continue;
                    index.Insert(new Envelope(edge.Start.X, edge.End.X, edge.Start.Y, edge.End.Y), edge);
                }
            }
            index.Build();

            int attached = 0;
            foreach (Branch branch in branches)
            {
                HashSet<StikEdge> nearby = new();
                if (branch.Points.Length >= 2)
                {
                    foreach (StikPoint end in new[] { branch.Points[0], branch.Points[^1] })
                    {
                        if (!end.IsFinite) continue;
                        double radius = maximumDistance + StikAttachmentGeometry.PositionTolerance;
                        foreach (StikEdge edge in index.Query(new Envelope(
                            end.X - radius, end.X + radius, end.Y - radius, end.Y + radius))) nearby.Add(edge);
                    }
                }
                StikAttachmentGeometry.Solve(branch.Points, nearby, slope, maximumDistance).Match(
                    solution =>
                    {
                        // Mutate vertices in place: preserve ObjectId, handle, layer,
                        // property sets, XData, vertex order and the main geometry.
                        branch.Polyline.UpgradeOpen();
                        for (int i = 0; i < branch.Vertices.Length; i++)
                        {
                            branch.Vertices[i].UpgradeOpen();
                            StikPoint point = solution.Points[i];
                            branch.Vertices[i].Position = new Point3d(point.X, point.Y, point.Z);
                        }
                        attached++;
                        return true;
                    },
                    reason =>
                    {
                        messages.Add($"Skipped {branch.Polyline.Handle}: {reason}");
                        return false;
                    });
            }
            transaction.Commit();
            return new StikAttachmentSummary(attached, selected.Count - attached, messages);
        }

        private static PolylineVertex3d[] ReadVertices(Polyline3d polyline, Transaction transaction)
        {
            List<PolylineVertex3d> vertices = new();
            foreach (ObjectId id in polyline)
                vertices.Add((PolylineVertex3d)transaction.GetObject(id, OpenMode.ForRead));
            return vertices.ToArray();
        }

        private static StikPoint FromPoint(Point3d point) => new(point.X, point.Y, point.Z);
        private sealed record Branch(Polyline3d Polyline, PolylineVertex3d[] Vertices, StikPoint[] Points);
    }
}
