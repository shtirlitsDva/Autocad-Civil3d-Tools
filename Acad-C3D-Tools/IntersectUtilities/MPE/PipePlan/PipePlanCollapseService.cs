using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.PipePlan;

internal static class PipePlanCollapseService
{
    private const double DefaultThreshold = 0.01;

    public static bool TryCollapse(Document document, out string message)
    {
        message = string.Empty;
        if (!TryPickPolyline(document, out ObjectId polylineId, out message))
        {
            return false;
        }

        using DocumentLock documentLock = document.LockDocument();
        using PipePlanSharpCornerMarkerManager markers = new();
        using PipePlanPreviewManager preview = new(document);
        using Transaction transaction = document.Database.TransactionManager.StartTransaction();

        try
        {
            Polyline polyline = (Polyline)transaction.GetObject(polylineId, OpenMode.ForWrite);
            if (!PipePlanMetadata.TryRead(polyline, transaction, out PipePlanStoredData? data) || data is null)
            {
                message = "Polylinjen har ingen PipePlan-data. Kør PPCONVERT først.";
                transaction.Commit();
                return false;
            }

            if (!PipePlanGeometryValidator.TryValidateAgainstMetadata(polyline, data, out message))
            {
                transaction.Commit();
                return false;
            }

            string layerName = polyline.Layer;
            double width = PipePlanWidthCalculator.ResolveDrawingWidth(layerName);
            PipePlanSolver solver = new();
            Editor editor = document.Editor;

            double threshold = DefaultThreshold;
            CollapsePlan? confirmed = null;

            while (true)
            {
                bool built = TryBuildCollapse(solver, data, threshold, out CollapsePlan? plan, out string buildError);
                if (built && plan is not null)
                {
                    preview.Show(plan.Analysis, width);
                    markers.Show(document, plan.RemovedPositions);
                    editor.UpdateScreen();
                }
                else
                {
                    preview.Clear();
                    markers.Clear();
                    editor.WriteMessage($"\n{buildError}");
                }

                string countText = plan is not null ? plan.RemovedIndices.Count.ToString() : "?";
                PromptDoubleOptions options = new(
                    $"\n{countText} bøjning(er) ≤ {threshold:0.###} fjernes. Enter for at bekræfte, ny tærskel for at forhåndsvise, eller Esc for at annullere: ")
                {
                    AllowNegative = false,
                    AllowZero = false,
                    AllowNone = true
                };

                PromptDoubleResult result = editor.GetDouble(options);

                if (result.Status == PromptStatus.None)
                {
                    if (plan is null)
                    {
                        editor.WriteMessage("\nKan ikke bekræfte: ugyldigt resultat ved denne tærskel.");
                        continue;
                    }

                    confirmed = plan;
                    break;
                }

                if (result.Status == PromptStatus.OK)
                {
                    threshold = result.Value;
                    continue;
                }

                transaction.Commit();
                message = "PPCOLLAPSE annulleret.";
                return false;
            }

            if (confirmed.RemovedIndices.Count == 0)
            {
                transaction.Commit();
                message = $"Ingen bøjninger ≤ {threshold:0.###} — intet at fjerne.";
                return true;
            }

            PipePlanStoredData newData = new(
                data.System,
                data.Type,
                data.Dn,
                confirmed.Radii,
                data.StraightSnapToleranceText,
                confirmed.ControlPoints,
                data.ObjectToken);
            PipePlanPolylineMutator.ApplyAnalysis(polyline, confirmed.Analysis, newData, layerName, transaction);
            transaction.Commit();

            message = $"{confirmed.RemovedIndices.Count} bøjning(er) fjernet (tærskel {threshold:0.###}).";
            return true;
        }
        catch
        {
            transaction.Abort();
            throw;
        }
    }

    /// <summary>
    /// Flags every interior bend whose sagitta — the distance from the arc midpoint to the
    /// midpoint of the chord between its tangent points, equal to R·(1 − cos(δ/2)) — is at
    /// or below <paramref name="threshold"/>, removes those control vertices, and re-solves.
    /// Returns false (with a reason) when the collapsed path is infeasible or degenerate.
    /// </summary>
    private static bool TryBuildCollapse(PipePlanSolver solver, PipePlanStoredData data, double threshold, out CollapsePlan? plan, out string error)
    {
        plan = null;
        error = string.Empty;

        IReadOnlyList<Point3d> controlPoints = data.ControlPoints;
        IReadOnlyList<double> radii = data.BendRadii;

        List<int> removeIndices = [];
        List<Point3d> removedPositions = [];
        for (int i = 1; i < controlPoints.Count - 1; i++)
        {
            double radius = radii[i];
            if (radius <= 0.0)
            {
                continue;
            }

            if (PipePlanBendCalculator.TryCompute(controlPoints[i - 1], controlPoints[i], controlPoints[i + 1], radius, out PipePlanBendGeometry bend) != PipePlanBendStatus.Bend)
            {
                continue;
            }

            double sagitta = bend.Radius * (1.0 - Math.Cos(bend.Deflection / 2.0));
            if (sagitta <= threshold)
            {
                removeIndices.Add(i);
                removedPositions.Add(controlPoints[i]);
            }
        }

        List<Point3d> newControlPoints = [.. controlPoints];
        List<double> newRadii = [.. radii];
        for (int k = removeIndices.Count - 1; k >= 0; k--)
        {
            newControlPoints.RemoveAt(removeIndices[k]);
            newRadii.RemoveAt(removeIndices[k]);
        }

        if (newControlPoints.Count < 2)
        {
            error = "Collapse ville give færre end to hjørner.";
            return false;
        }

        // Endpoints never carry a bend — if a collapse promoted an interior vertex to a
        // terminus, zero its radius so the solver treats it as a straight start/end.
        newRadii[0] = 0.0;
        newRadii[^1] = 0.0;

        PipePlanAnalysis analysis = solver.Analyze(newControlPoints, newRadii);
        if (!analysis.IsFeasible)
        {
            error = $"Resultat ugyldigt: {analysis.Message}";
            return false;
        }

        plan = new CollapsePlan(newControlPoints, newRadii, removeIndices, removedPositions, analysis);
        return true;
    }

    private static bool TryPickPolyline(Document document, out ObjectId polylineId, out string message)
    {
        polylineId = ObjectId.Null;
        message = string.Empty;

        PromptEntityOptions options = new("\nSelect a PipePlan polyline to collapse: ");
        options.SetRejectMessage("\nOnly PipePlan polylines are supported.");
        options.AddAllowedClass(typeof(Polyline), exactMatch: false);

        PromptEntityResult result = document.Editor.GetEntity(options);
        if (result.Status != PromptStatus.OK)
        {
            message = "PPCOLLAPSE annulleret.";
            return false;
        }

        polylineId = result.ObjectId;
        return true;
    }

    private sealed record CollapsePlan(
        List<Point3d> ControlPoints,
        List<double> Radii,
        List<int> RemovedIndices,
        List<Point3d> RemovedPositions,
        PipePlanAnalysis Analysis);
}
