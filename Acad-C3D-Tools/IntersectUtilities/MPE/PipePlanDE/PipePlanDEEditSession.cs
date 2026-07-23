using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.MPE.PipePlan;

namespace IntersectUtilities.MPE.PipePlanDE;

/// <summary>One resolved edit draft for a PDDRAW run: the working control points + per-corner
/// inner-pipe radii, and whether they re-solve to a feasible filleted run.</summary>
internal sealed record PipePlanDEEditCandidate(
    IReadOnlyList<Point3d> ControlPoints,
    IReadOnlyList<double> RMinRadii,
    bool Feasible,
    string Message);

/// <summary>
/// PDEDIT session — the German-pipe analogue of <c>PipePlanEditSession</c>. It owns the run's
/// three polyline ids + shared token, the authoring control points + per-corner radii, and
/// re-solves every operation through <c>PipePlanDEGeometryBuilder</c>. Because a PDDRAW run is
/// three polylines (not one), Commit erases the run and re-bakes it from the edited data,
/// preserving the token so the run keeps its identity.
/// </summary>
internal sealed class PipePlanDEEditSession : IDisposable
{
    private readonly Document _document;
    private readonly string _token;
    private readonly int _dn;
    private readonly PipePlanDETrenchDepth _depth;
    private readonly bool _flip;
    private readonly PipePlanDEParameters _parameters;
    private readonly List<Point3d> _controlPoints;
    private readonly List<double> _rMinRadii;
    private readonly PipePlanDEHandleMarkerManager _markers = new();
    private readonly PipePlanDEPreviewManager _preview = new();

    private ObjectId _centreId;
    private ObjectId _fremId;
    private ObjectId _returId;
    private int? _pendingRadiusVertex;
    private double _pendingRadiusValue;

    private PipePlanDEEditSession(
        Document document, ObjectId centreId, ObjectId fremId, ObjectId returId,
        string token, int dn, PipePlanDETrenchDepth depth, bool flip,
        PipePlanDEParameters parameters, IReadOnlyList<Point3d> controlPoints, IReadOnlyList<double> rMinRadii)
    {
        _document = document;
        _centreId = centreId;
        _fremId = fremId;
        _returId = returId;
        _token = token;
        _dn = dn;
        _depth = depth;
        _flip = flip;
        _parameters = parameters;
        _controlPoints = [.. controlPoints];
        _rMinRadii = [.. rMinRadii];
    }

    public void Dispose()
    {
        _markers.Dispose();
        _preview.Dispose();
    }

    public int Dn => _dn;
    public double Half => _parameters.PipeSpacing / 2.0;
    public IReadOnlyList<Point3d> ControlPoints => _controlPoints;
    public IReadOnlyList<double> RMinRadii => _rMinRadii;
    public string SizeLabel => $"DN {_dn}";

    public static bool TryCreateFrom(Document document, ObjectId pickedId, out PipePlanDEEditSession? session, out string error)
    {
        session = null;
        error = string.Empty;

        int dn;
        PipePlanDETrenchDepth depth;
        string token;
        PipePlanDEAuthoring authoring;
        ObjectId centreId, fremId, returId;

        using (Transaction tx = document.Database.TransactionManager.StartTransaction())
        {
            if (!PipePlanDERunLocator.TryFindRun(document.Database, tx, pickedId,
                    out centreId, out fremId, out returId, out PipePlanDEStoredData? centreData, out error) || centreData?.Authoring is null)
            {
                tx.Commit();
                return false;
            }

            dn = centreData.Dn;
            depth = centreData.Depth;
            token = centreData.Token!;
            authoring = centreData.Authoring;
            tx.Commit();
        }

        PipePlanDEParameters? parameters = PipePlanDEParameterStore.GetEffective(document.Database, dn);
        if (parameters is null)
        {
            error = $"Ingen parametre for DN {dn}.";
            return false;
        }

        session = new PipePlanDEEditSession(document, centreId, fremId, returId, token, dn, depth, authoring.Flip,
            parameters, authoring.ControlPoints, authoring.RMinRadii);
        return true;
    }

    public void ShowHandles()
    {
        _preview.Clear();
        IReadOnlyList<PipePlanRadiusAnnotation> annotations = [];
        if (PipePlanDEGeometryBuilder.TryBuild(_controlPoints, _rMinRadii, _parameters, _flip, straight: false,
                out _, out _, out _, out PipePlanAnalysis? analysis, out _) && analysis is not null)
        {
            annotations = analysis.RadiusAnnotations;
        }

        _markers.Show(_document, _controlPoints, annotations, Half);
    }

    public void ClearVisuals()
    {
        _markers.Clear();
        _preview.Clear();
    }

    public double GetPickTolerance() => _markers.GetPickTolerance(_document);

    // --- Handle resolution -------------------------------------------------

    public bool TryResolveHandle(Point3d pick, out PipePlanEditHandle? handle, out string message)
    {
        handle = null;
        message = string.Empty;

        int bestVertex = -1;
        double bestVertexDist = double.MaxValue;
        for (int i = 0; i < _controlPoints.Count; i++)
        {
            double d = Dist(pick, _controlPoints[i]);
            if (d < bestVertexDist) { bestVertexDist = d; bestVertex = i; }
        }

        int bestSegment = -1;
        double bestSegmentDist = double.MaxValue;
        for (int i = 0; i < _controlPoints.Count - 1; i++)
        {
            double d = Dist(pick, Midpoint(i));
            if (d < bestSegmentDist) { bestSegmentDist = d; bestSegment = i; }
        }

        double tolerance = GetPickTolerance();
        if (bestVertexDist <= bestSegmentDist)
        {
            if (bestVertexDist > tolerance) { message = "Vælg et synligt håndtag."; return false; }
            handle = new PipePlanEditHandle(PipePlanEditHandleKind.Vertex, bestVertex, _controlPoints[bestVertex]);
            return true;
        }

        if (bestSegmentDist > tolerance) { message = "Vælg et synligt håndtag."; return false; }
        handle = new PipePlanEditHandle(PipePlanEditHandleKind.Segment, bestSegment, Midpoint(bestSegment));
        return true;
    }

    public bool TryGetNearestVertexIndex(Point3d pick, double tolerance, out int index)
    {
        index = -1;
        double best = double.MaxValue;
        for (int i = 0; i < _controlPoints.Count; i++)
        {
            double d = Dist(pick, _controlPoints[i]);
            if (d < best) { best = d; index = i; }
        }

        return best <= tolerance && index >= 0;
    }

    // --- Candidate builders (re-solve, do not mutate) ----------------------

    public PipePlanDEEditCandidate BuildCandidate(PipePlanEditHandle handle, Point3d dragPoint)
    {
        List<Point3d> cps = [.. _controlPoints];
        List<double> radii = [.. _rMinRadii];

        if (handle.Kind == PipePlanEditHandleKind.Vertex)
        {
            cps[handle.Index] = new Point3d(dragPoint.X, dragPoint.Y, cps[handle.Index].Z);
            if (_pendingRadiusVertex == handle.Index && handle.Index > 0 && handle.Index < cps.Count - 1)
            {
                radii[handle.Index] = _pendingRadiusValue;
            }
        }
        else
        {
            // Segment handle: slide the whole segment by the grip→cursor delta.
            Vector3d delta = dragPoint - handle.GripPoint;
            cps[handle.Index] = cps[handle.Index] + delta;
            cps[handle.Index + 1] = cps[handle.Index + 1] + delta;
        }

        return Analyze(cps, radii);
    }

    public PipePlanDEEditCandidate BuildNearestInsertCandidate(Point3d cursor, double radius, out int segmentIndex)
    {
        segmentIndex = FindNearestControlSegment(cursor);
        List<Point3d> cps = [.. _controlPoints];
        List<double> radii = [.. _rMinRadii];

        Point3d insert = ProjectOntoSegment(cursor, cps[segmentIndex], cps[segmentIndex + 1]);
        cps.Insert(segmentIndex + 1, new Point3d(insert.X, insert.Y, cps[segmentIndex].Z));
        radii.Insert(segmentIndex + 1, radius);
        return Analyze(cps, radii);
    }

    public bool TryBuildRemoveVertexCandidate(int vertexIndex, out PipePlanDEEditCandidate? candidate, out string error)
    {
        candidate = null;
        error = string.Empty;
        if (vertexIndex <= 0 || vertexIndex >= _controlPoints.Count - 1)
        {
            error = "Endepunkter kan ikke slettes.";
            return false;
        }

        List<Point3d> cps = [.. _controlPoints];
        List<double> radii = [.. _rMinRadii];
        cps.RemoveAt(vertexIndex);
        radii.RemoveAt(vertexIndex);
        if (cps.Count < 2)
        {
            error = "Mindst to punkter kræves.";
            return false;
        }

        radii[0] = 0.0;
        radii[^1] = 0.0;
        candidate = Analyze(cps, radii);
        return true;
    }

    private PipePlanDEEditCandidate Analyze(List<Point3d> cps, List<double> radii)
    {
        bool ok = PipePlanDEGeometryBuilder.TryBuild(cps, radii, _parameters, _flip, straight: false,
            out _, out _, out _, out _, out string error);
        return new PipePlanDEEditCandidate(cps, radii, ok, ok ? "OK" : error);
    }

    /// <summary>Builds a candidate from an explicit control-point / radii pair (used by
    /// Continue, which grows the run from an endpoint). The lists are copied.</summary>
    public PipePlanDEEditCandidate BuildRawCandidate(IReadOnlyList<Point3d> controlPoints, IReadOnlyList<double> rMinRadii)
        => Analyze([.. controlPoints], [.. rMinRadii]);

    // --- Previews & pending radius -----------------------------------------

    public void ShowCandidatePreview(PipePlanDEEditCandidate candidate)
    {
        _markers.Clear();
        _preview.Show(candidate.ControlPoints, candidate.RMinRadii, _parameters, _flip, straight: false);
    }

    public void ClearPreview() => _preview.Clear();

    public void SetPendingRadius(int vertexIndex, double value)
    {
        _pendingRadiusVertex = vertexIndex;
        _pendingRadiusValue = value;
    }

    public void ClearPendingRadius() => _pendingRadiusVertex = null;

    public bool TryGetPendingRadius(out int vertexIndex, out double value)
    {
        vertexIndex = _pendingRadiusVertex ?? -1;
        value = _pendingRadiusValue;
        return _pendingRadiusVertex.HasValue;
    }

    /// <summary>Default insert radius = this DN's table R_min (already the floor).</summary>
    public bool TryGetInsertRadius(out double radius, out string error)
    {
        error = string.Empty;
        if (PipePlanDEBendRadiusTable.TryGet(_dn, out radius))
        {
            return true;
        }

        error = $"Ingen bukkeradius for DN {_dn}.";
        return false;
    }

    // --- Commit: erase the run and re-bake from the edited data ------------

    public bool Commit(PipePlanDEEditCandidate candidate, out string error)
    {
        error = string.Empty;
        if (!candidate.Feasible)
        {
            error = candidate.Message;
            return false;
        }

        using DocumentLock documentLock = _document.LockDocument();
        using Transaction tx = _document.Database.TransactionManager.StartTransaction();
        try
        {
            PipePlanDERunLocator.EraseRun(tx, _centreId, _fremId, _returId);
            if (!PipePlanDEPolylineWriter.TryWrite(_document.Database, tx, candidate.ControlPoints, candidate.RMinRadii,
                    _dn, _parameters, _flip, straight: false, _depth, _token, out _, out ObjectId[] ids, out error))
            {
                tx.Abort();
                return false;
            }

            tx.Commit();

            _centreId = ids[0];
            _fremId = ids[1];
            _returId = ids[2];
            _controlPoints.Clear();
            _controlPoints.AddRange(candidate.ControlPoints);
            _rMinRadii.Clear();
            _rMinRadii.AddRange(candidate.RMinRadii);
            _pendingRadiusVertex = null;
            return true;
        }
        catch
        {
            tx.Abort();
            throw;
        }
    }

    // --- Geometry helpers --------------------------------------------------

    private Point3d Midpoint(int segmentIndex) => new(
        (_controlPoints[segmentIndex].X + _controlPoints[segmentIndex + 1].X) / 2.0,
        (_controlPoints[segmentIndex].Y + _controlPoints[segmentIndex + 1].Y) / 2.0,
        (_controlPoints[segmentIndex].Z + _controlPoints[segmentIndex + 1].Z) / 2.0);

    private int FindNearestControlSegment(Point3d cursor)
    {
        int best = 0;
        double bestDist = double.MaxValue;
        for (int i = 0; i < _controlPoints.Count - 1; i++)
        {
            double d = Dist(cursor, ProjectOntoSegment(cursor, _controlPoints[i], _controlPoints[i + 1]));
            if (d < bestDist) { bestDist = d; best = i; }
        }

        return best;
    }

    private static Point3d ProjectOntoSegment(Point3d p, Point3d a, Point3d b)
    {
        Vector3d ab = b - a;
        double len2 = ab.LengthSqrd;
        if (len2 < 1e-12)
        {
            return a;
        }

        double t = Math.Clamp(((p - a).DotProduct(ab)) / len2, 0.0, 1.0);
        return a + (ab * t);
    }

    private static double Dist(Point3d a, Point3d b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));
}
