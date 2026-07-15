using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.MPE.PipePlan;

internal sealed class PipePlanEditSession : IDisposable
{
    private readonly Document _document;
    private readonly PipePlanState _state;
    private readonly PipePlanSolver _solver = new();
    private readonly PipePlanHandleMarkerManager _markerManager = new();

    private ObjectId _polylineId;
    private PipePlanStoredData _data;
    private int? _pendingRadiusVertex;
    private double _pendingRadiusValue;

    public PipePlanEditSession(Document document, PipePlanState state, ObjectId polylineId, PipePlanStoredData data)
    {
        _document = document;
        _state = state;
        _polylineId = polylineId;
        _data = data;
    }

    public string SizeLabel => _data.SizeDisplay;

    public string RadiusDisplay => _data.RadiusDisplay;

    public void Dispose()
    {
        ClearVisuals();
    }

    /// <summary>
    /// Prompts for the polyline to edit. Split from <see cref="TryCreateFrom"/> so the
    /// command can attempt an auto-convert on the already-picked entity (without
    /// re-prompting) when it turns out to carry no valid PipePlan metadata.
    /// </summary>
    public static bool TryPickPolyline(Document document, out ObjectId polylineId, out string errorMessage)
    {
        return TryPickEditablePolyline(document, out polylineId, out errorMessage);
    }

    /// <summary>
    /// Builds an edit session from an already-picked polyline. Returns false (with a
    /// convert-suggesting <paramref name="errorMessage"/>) when the polyline has no
    /// valid PipePlan metadata — a state <c>TryConvertExisting</c> can repair before
    /// the caller retries.
    /// </summary>
    public static bool TryCreateFrom(Document document, PipePlanState state, ObjectId polylineId, out PipePlanEditSession? session, out string errorMessage)
    {
        session = null;

        if (!TryLoadSessionData(document, polylineId, out PipePlanStoredData? data, out errorMessage) || data is null)
        {
            return false;
        }

        state.ApplyStoredContext(data);
        session = new PipePlanEditSession(document, state, polylineId, data);
        return true;
    }

    public void ShowHandles()
    {
        _state.ClearPreview();

        // Solve the baked geometry so the handles can be accompanied by perpendicular
        // divider ticks at every straight<->arc junction (visible as soon as the pipe is
        // picked, before any drag).
        IReadOnlyList<PipePlanSegmentDivider> dividers = [];
        IReadOnlyList<PipePlanArcDimension> arcDimensions = [];
        PipePlanAnalysis analysis = _solver.Analyze(_data.ControlPoints, _data.BendRadii);
        if (analysis.IsFeasible)
        {
            dividers = ComputeSegmentDividers(analysis);
            arcDimensions = ComputeArcDimensions(analysis);
        }

        double pipeWidth = _state.ActiveContext is PipePlanActiveContext context
            ? PipePlanWidthCalculator.ResolveDrawingWidth(context.LayerName)
            : 0.0;

        _markerManager.Show(_document, _data.ControlPoints, dividers, arcDimensions, pipeWidth);
    }

    // Divider ticks sit at every segment junction that involves an arc: straight<->arc
    // tangent points and arc<->arc joins inside a merged chain. (Pure straight-straight
    // joins carry no divider.) The tangent at such a vertex is G1-continuous, so the
    // incoming segment's end-tangent gives the perpendicular basis for the tick.
    private IReadOnlyList<PipePlanSegmentDivider> ComputeSegmentDividers(PipePlanAnalysis analysis)
    {
        IReadOnlyList<PolylineVertexData> vertices = analysis.Vertices;
        if (vertices.Count < 3)
        {
            return [];
        }

        double z = _data.ControlPoints.Count > 0 ? _data.ControlPoints[0].Z : 0.0;
        List<PipePlanSegmentDivider> dividers = [];
        for (int k = 1; k < vertices.Count - 1; k++)
        {
            bool arcBefore = PipePlanArcGeometry.IsArcBulge(vertices[k - 1].Bulge);
            bool arcAfter = PipePlanArcGeometry.IsArcBulge(vertices[k].Bulge);
            if (!arcBefore && !arcAfter)
            {
                continue; // straight-straight join — nothing to separate
            }

            Vector2d tangent = PipePlanArcGeometry.TangentAtEnd(
                vertices[k - 1].Point, vertices[k].Point, vertices[k - 1].Bulge);
            if (tangent.Length < 1e-9)
            {
                continue;
            }

            Point2d point = vertices[k].Point;
            dividers.Add(new PipePlanSegmentDivider(new Point3d(point.X, point.Y, z), tangent));
        }

        return dividers;
    }

    // One DIMARC-style annotation per arc segment (fillets and every arc in a merged chain).
    private IReadOnlyList<PipePlanArcDimension> ComputeArcDimensions(PipePlanAnalysis analysis)
    {
        IReadOnlyList<PolylineVertexData> vertices = analysis.Vertices;
        double z = _data.ControlPoints.Count > 0 ? _data.ControlPoints[0].Z : 0.0;
        List<PipePlanArcDimension> dimensions = [];
        for (int i = 0; i < vertices.Count - 1; i++)
        {
            double bulge = vertices[i].Bulge;
            if (!PipePlanArcGeometry.IsArcBulge(bulge))
            {
                continue;
            }

            Point2d start = vertices[i].Point;
            Point2d end = vertices[i + 1].Point;
            Point2d center = PipePlanArcGeometry.ArcCenter(start, end, bulge);
            double radius = PipePlanArcGeometry.ArcRadius(start, end, bulge);
            dimensions.Add(new PipePlanArcDimension(
                new Point3d(start.X, start.Y, z),
                new Point3d(end.X, end.Y, z),
                new Point3d(center.X, center.Y, z),
                radius,
                bulge > 0.0));
        }

        return dimensions;
    }

    public void ClearVisuals()
    {
        _markerManager.Clear();
        _state.ClearPreview();
    }

    public bool TryResolveHandle(Point3d pickedPoint, out PipePlanEditHandle? handle, out string message)
    {
        handle = null;
        message = string.Empty;

        double tolerance = _markerManager.GetPickTolerance(_document);
        PipePlanEditHandle? bestVertexHandle = FindClosestVertexHandle(pickedPoint);
        PipePlanEditHandle? bestSegmentHandle = FindClosestSegmentHandle(pickedPoint);

        handle = ChooseClosestHandle(pickedPoint, bestVertexHandle, bestSegmentHandle);
        if (handle is null || PipePlanGeometryUtil.Distance2D(pickedPoint, handle.GripPoint) > tolerance)
        {
            message = "Vælg et synligt PipePlan-håndtag.";
            return false;
        }

        return true;
    }

    public PipePlanEditCandidate BuildCandidate(PipePlanEditHandle handle, Point3d dragPoint)
    {
        PipePlanEditDraft draft = BuildCandidateDraft(handle, dragPoint);
        return new PipePlanEditCandidate(draft, Analyze(draft));
    }

    public void Commit(PipePlanEditCandidate candidate)
    {
        using DocumentLock documentLock = _document.LockDocument();
        using Transaction transaction = _document.Database.TransactionManager.StartTransaction();

        try
        {
            Polyline polyline = (Polyline)transaction.GetObject(_polylineId, OpenMode.ForWrite);
            _data = new PipePlanStoredData(
                _data.System,
                _data.Type,
                _data.Dn,
                candidate.Draft.BendRadii,
                _data.StraightSnapToleranceText,
                candidate.Draft.ControlPoints,
                _data.ObjectToken);
            PipePlanPolylineMutator.ApplyAnalysis(polyline, candidate.Analysis, _data, polyline.Layer, transaction);
            // _polylineId stays the same across edits because the entity is mutated
            // in place — Handle, ObjectId, ExtensionDictionary and any third-party
            // attached data survive the edit.

            transaction.Commit();
        }
        catch
        {
            transaction.Abort();
            throw;
        }

        ClearPendingRadius();
    }

    public void SetPendingRadius(int vertexIndex, double radius)
    {
        if (vertexIndex <= 0 || vertexIndex >= _data.ControlPoints.Count - 1) return;
        if (radius <= 0.0) return;
        _pendingRadiusVertex = vertexIndex;
        _pendingRadiusValue = radius;
    }

    public void ClearPendingRadius()
    {
        _pendingRadiusVertex = null;
        _pendingRadiusValue = 0.0;
    }

    public bool TryGetPendingRadius(out int vertexIndex, out double radius)
    {
        if (_pendingRadiusVertex is int idx)
        {
            vertexIndex = idx;
            radius = _pendingRadiusValue;
            return true;
        }
        vertexIndex = -1;
        radius = 0.0;
        return false;
    }

    public bool TryAnalyzeVertexState(int vertexIndex, Point3d newPosition, double radius, out PipePlanAnalysis analysis, out string error)
    {
        error = string.Empty;
        analysis = PipePlanAnalysis.Invalid(_data.ControlPoints, string.Empty);

        if (vertexIndex <= 0 || vertexIndex >= _data.ControlPoints.Count - 1)
        {
            error = "Endepunkter har ikke bukkeradius.";
            return false;
        }

        if (radius <= 0.0)
        {
            error = "Radius skal være > 0.";
            return false;
        }

        PipePlanEditDraft draft = BuildVertexRadiusDraft(vertexIndex, newPosition, radius);
        analysis = Analyze(draft);
        if (!analysis.IsFeasible)
        {
            error = analysis.Message;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Builds a candidate that removes the control point at <paramref name="vertexIndex"/>
    /// (and its aligned bend radius). Endpoints are allowed — removing one shortens the
    /// path and the promoted neighbour loses its bend (its radius is forced to 0, since
    /// endpoints never bend). The merged tangent only relaxes the radius-fit constraint,
    /// so this is feasible in all but pathological cases; the analysis is still checked.
    /// </summary>
    public bool TryBuildRemoveVertexCandidate(int vertexIndex, out PipePlanEditCandidate? candidate, out string error)
    {
        candidate = null;
        error = string.Empty;

        if (_data.ControlPoints.Count <= 2)
        {
            error = "En PipePlan skal have mindst to hjørner.";
            return false;
        }

        if (vertexIndex < 0 || vertexIndex >= _data.ControlPoints.Count)
        {
            error = "Ugyldigt hjørne.";
            return false;
        }

        List<Point3d> controlPoints = [.. _data.ControlPoints];
        List<double> radii = [.. _data.BendRadii];
        controlPoints.RemoveAt(vertexIndex);
        radii.RemoveAt(vertexIndex);

        // Endpoints never carry a bend. If the removal promoted an interior vertex to a
        // start/end, zero its radius so the solver treats it as a straight terminus.
        radii[0] = 0.0;
        radii[^1] = 0.0;

        PipePlanEditDraft draft = new(controlPoints, radii);
        PipePlanAnalysis analysis = Analyze(draft);
        if (!analysis.IsFeasible)
        {
            error = analysis.Message;
            return false;
        }

        candidate = new PipePlanEditCandidate(draft, analysis);
        return true;
    }

    /// <summary>
    /// Builds a candidate that inserts a new interior control point on the segment between
    /// <paramref name="segmentIndex"/> and the next control point. <paramref name="position"/>
    /// is expected to lie on (or near) that segment; placing it collinearly yields a
    /// geometrically identical polyline that the caller can then drag into a real corner.
    /// The new vertex carries <paramref name="radius"/> as its bend radius.
    /// </summary>
    public PipePlanEditCandidate BuildInsertCandidate(int segmentIndex, Point3d position, double radius)
    {
        List<Point3d> controlPoints = [.. _data.ControlPoints];
        List<double> radii = [.. _data.BendRadii];

        double z = controlPoints[segmentIndex].Z;
        controlPoints.Insert(segmentIndex + 1, new Point3d(position.X, position.Y, z));
        radii.Insert(segmentIndex + 1, radius);

        PipePlanEditDraft draft = new(controlPoints, radii);
        return new PipePlanEditCandidate(draft, Analyze(draft));
    }

    /// <summary>
    /// Resolves the default bend radius offered when inserting a vertex: the project
    /// per-DN ProjekteringsRadius when available, otherwise any existing interior radius
    /// on this object. The command layer prompts the user with this value, letting them
    /// accept it or type a custom radius instead.
    /// </summary>
    public bool TryGetInsertRadius(out double radius, out string error)
    {
        error = string.Empty;

        if (PipePlanRadiusStore.TryGet(_document.Database, _data.System, _data.Type, _data.Dn, out radius) && radius > 0.0)
        {
            return true;
        }

        radius = _data.BendRadii.Where(r => r > 0.0).DefaultIfEmpty(0.0).First();
        if (radius > 0.0)
        {
            return true;
        }

        error = $"Ingen standard-radius for {_data.SizeDisplay}. Sæt den i PPSETTINGS.";
        return false;
    }

    /// <summary>Pick tolerance (drawing units) for resolving a clicked/hovered handle, scaled to the current zoom.</summary>
    public double GetPickTolerance() => _markerManager.GetPickTolerance(_document);

    /// <summary>
    /// Finds the control vertex nearest to <paramref name="point"/> within
    /// <paramref name="tolerance"/>. Used by delete mode to resolve both the hovered
    /// preview target and the clicked deletion target.
    /// </summary>
    public bool TryGetNearestVertexIndex(Point3d point, double tolerance, out int index)
    {
        index = -1;
        double best = double.MaxValue;
        for (int i = 0; i < _data.ControlPoints.Count; i++)
        {
            double distance = PipePlanGeometryUtil.Distance2D(point, _data.ControlPoints[i]);
            if (distance < best)
            {
                best = distance;
                index = i;
            }
        }

        if (index < 0 || best > tolerance)
        {
            index = -1;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Builds an insert candidate for the control segment nearest to <paramref name="cursor"/>,
    /// placing the new vertex at the cursor. Used by add mode for both the live hover preview
    /// and the committed click — the cursor position drives both which segment receives the
    /// vertex and where the resulting corner sits.
    /// </summary>
    public PipePlanEditCandidate BuildNearestInsertCandidate(Point3d cursor, double radius, out int segmentIndex)
    {
        segmentIndex = FindNearestControlSegment(cursor);
        return BuildInsertCandidate(segmentIndex, cursor, radius);
    }

    private int FindNearestControlSegment(Point3d cursor)
    {
        int best = 0;
        double bestDistance = double.MaxValue;
        for (int i = 0; i < _data.ControlPoints.Count - 1; i++)
        {
            double distance = DistancePointToSegment2D(cursor, _data.ControlPoints[i], _data.ControlPoints[i + 1]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    private static double DistancePointToSegment2D(Point3d p, Point3d a, Point3d b)
    {
        double abx = b.X - a.X;
        double aby = b.Y - a.Y;
        double lengthSquared = (abx * abx) + (aby * aby);
        double t = lengthSquared <= 1e-12
            ? 0.0
            : Math.Clamp((((p.X - a.X) * abx) + ((p.Y - a.Y) * aby)) / lengthSquared, 0.0, 1.0);
        double closestX = a.X + (t * abx);
        double closestY = a.Y + (t * aby);
        double dx = p.X - closestX;
        double dy = p.Y - closestY;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static bool TryPickEditablePolyline(Document document, out ObjectId polylineId, out string errorMessage)
    {
        polylineId = ObjectId.Null;
        errorMessage = string.Empty;

        PromptEntityOptions options = new("\nSelect a PipePlan polyline to edit: ");
        options.SetRejectMessage("\nOnly PipePlan polylines are supported.");
        options.AddAllowedClass(typeof(Polyline), exactMatch: false);

        PromptEntityResult result = document.Editor.GetEntity(options);
        if (result.Status != PromptStatus.OK)
        {
            errorMessage = "PPEDIT annulleret.";
            return false;
        }

        polylineId = result.ObjectId;
        return true;
    }

    private static bool TryLoadSessionData(Document document, ObjectId polylineId, out PipePlanStoredData? data, out string errorMessage)
    {
        data = null;
        errorMessage = string.Empty;

        using Transaction transaction = document.Database.TransactionManager.StartTransaction();
        try
        {
            Polyline polyline = (Polyline)transaction.GetObject(polylineId, OpenMode.ForRead);
            if (!PipePlanMetadata.TryRead(polyline, transaction, out data) || data is null)
            {
                errorMessage = "Polylinjen er fra en ældre PipePlan-version. Kør PPCONVERT først.";
                return false;
            }

            if (!PipePlanGeometryValidator.TryValidateAgainstMetadata(polyline, data, out errorMessage))
            {
                return false;
            }

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Abort();
            throw;
        }
    }

    private PipePlanEditHandle? FindClosestVertexHandle(Point3d pickedPoint)
    {
        double bestDistance = double.MaxValue;
        PipePlanEditHandle? bestHandle = null;

        for (int index = 0; index < _data.ControlPoints.Count; index++)
        {
            Point3d controlPoint = _data.ControlPoints[index];
            double distance = PipePlanGeometryUtil.Distance2D(pickedPoint, controlPoint);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestHandle = new PipePlanEditHandle(PipePlanEditHandleKind.Vertex, index, controlPoint);
        }

        return bestHandle;
    }

    private PipePlanEditHandle? FindClosestSegmentHandle(Point3d pickedPoint)
    {
        double bestDistance = double.MaxValue;
        PipePlanEditHandle? bestHandle = null;

        for (int index = 0; index < _data.ControlPoints.Count - 1; index++)
        {
            Point3d midpoint = Midpoint(_data.ControlPoints[index], _data.ControlPoints[index + 1]);
            double distance = PipePlanGeometryUtil.Distance2D(pickedPoint, midpoint);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestHandle = new PipePlanEditHandle(PipePlanEditHandleKind.Segment, index, midpoint);
        }

        return bestHandle;
    }

    private static PipePlanEditHandle? ChooseClosestHandle(
        Point3d pickedPoint,
        PipePlanEditHandle? vertexHandle,
        PipePlanEditHandle? segmentHandle)
    {
        if (vertexHandle is null)
        {
            return segmentHandle;
        }

        if (segmentHandle is null)
        {
            return vertexHandle;
        }

        double vertexDistance = PipePlanGeometryUtil.Distance2D(pickedPoint, vertexHandle.GripPoint);
        double segmentDistance = PipePlanGeometryUtil.Distance2D(pickedPoint, segmentHandle.GripPoint);
        return vertexDistance <= segmentDistance ? vertexHandle : segmentHandle;
    }

    private PipePlanEditDraft BuildCandidateDraft(PipePlanEditHandle handle, Point3d dragPoint)
    {
        List<Point3d> controlPoints = [.. _data.ControlPoints];
        if (handle.Kind == PipePlanEditHandleKind.Vertex)
        {
            Point3d original = controlPoints[handle.Index];
            controlPoints[handle.Index] = new Point3d(dragPoint.X, dragPoint.Y, original.Z);
        }
        else
        {
            Vector3d delta = new(dragPoint.X - handle.GripPoint.X, dragPoint.Y - handle.GripPoint.Y, 0.0);
            controlPoints[handle.Index] = controlPoints[handle.Index].Add(delta);
            controlPoints[handle.Index + 1] = controlPoints[handle.Index + 1].Add(delta);
        }

        List<double> radii = [.. _data.BendRadii];
        if (_pendingRadiusVertex is int idx && idx >= 0 && idx < radii.Count)
        {
            radii[idx] = _pendingRadiusValue;
        }

        return new PipePlanEditDraft(controlPoints, radii);
    }

    private PipePlanEditDraft BuildVertexRadiusDraft(int vertexIndex, Point3d newPosition, double radius)
    {
        List<Point3d> controlPoints = [.. _data.ControlPoints];
        Point3d original = controlPoints[vertexIndex];
        controlPoints[vertexIndex] = new Point3d(newPosition.X, newPosition.Y, original.Z);

        List<double> radii = [.. _data.BendRadii];
        radii[vertexIndex] = radius;

        return new PipePlanEditDraft(controlPoints, radii);
    }

    private PipePlanAnalysis Analyze(PipePlanEditDraft draft)
    {
        if (draft.ControlPoints.Count != draft.BendRadii.Count)
        {
            return PipePlanAnalysis.Invalid(draft.ControlPoints, "Data inkonsistent. Kør PPCONVERT.");
        }

        return _solver.Analyze(draft.ControlPoints, draft.BendRadii);
    }

    public IReadOnlyList<double> CurrentBendRadii => _data.BendRadii;

    public IReadOnlyList<Point3d> ControlPoints => _data.ControlPoints;

    private static Point3d Midpoint(Point3d a, Point3d b)
    {
        return new Point3d((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0, (a.Z + b.Z) / 2.0);
    }
}
