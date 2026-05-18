using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.MPE.PipePlan;

internal sealed class PipePlanState : IDisposable
{
    private const double PointMatchTolerance = 1e-4;
    private const double DistanceTolerance = 1e-6;

    private readonly PipePlanSolver _solver = new();
    private readonly PipePlanPreviewManager _previewManager = new();

    private PipePlanPalette? _palette;
    private PipePlanAnalysis? _latestAnalysis;
    private PipePlanCandidateResult? _latestInteractiveCandidate;
    private PipePlanActiveContext? _activeContext;
    private double? _manualRadius;
    private ObjectId _continuedPolylineId = ObjectId.Null;

    public PipePlanState()
    {
        StraightSnapToleranceText = "5";
    }

    public List<Point3d> DraftPoints { get; } = [];

    public List<double> DraftBendRadii { get; } = [];

    public string StraightSnapToleranceText { get; set; }

    public PipePlanActiveContext? ActiveContext => _activeContext;

    public double EffectiveRadius => _manualRadius ?? _activeContext?.Radius ?? 0.0;

    public bool HasManualRadiusOverride => _manualRadius.HasValue;

    public Point3d? LastEditDragPoint { get; set; }

    public void SetManualRadius(double radius)
    {
        if (radius <= 0.0)
        {
            return;
        }

        _manualRadius = radius;
        RefreshCurrentPreview();
    }

    public void ClearManualRadius()
    {
        if (!_manualRadius.HasValue)
        {
            return;
        }

        _manualRadius = null;
        RefreshCurrentPreview();
    }

    private void RefreshCurrentPreview()
    {
        if (_latestInteractiveCandidate is not null)
        {
            PreviewCandidate(_latestInteractiveCandidate.RawPoint, _latestInteractiveCandidate.StraightSnapActive);
            return;
        }

        RefreshDraftPreview();
    }

    public void EnsurePalette()
    {
        _palette ??= new PipePlanPalette(this);
        _palette.Show();
        RefreshDraftPreview();
    }

    public void Dispose()
    {
        _previewManager.Dispose();
        _palette?.Dispose();
    }

    public bool InitializeForCurrentLayer(Database db, out string error)
    {
        error = string.Empty;
        if (!PipePlanLayerResolver.TryResolve(db, out PipePlanActiveContext? context, out error) || context is null)
        {
            _activeContext = null;
            return false;
        }

        _activeContext = context;
        return true;
    }

    public bool TryGetStraightSnapTolerance(out double tolerance)
    {
        return PipePlanParsing.TryParsePositiveDouble(StraightSnapToleranceText, out tolerance) &&
               tolerance > DistanceTolerance;
    }

    public void AddDraftPoint(Point3d point)
    {
        _latestInteractiveCandidate = null;
        AppendDraftPoint(point);
        RefreshDraftPreview();
    }

    public void AddCommittedCandidate(PipePlanCandidateResult candidate)
    {
        _latestInteractiveCandidate = null;
        AppendDraftPoint(candidate.FinalPoint);
        RefreshDraftPreview();
    }

    private void AppendDraftPoint(Point3d point)
    {
        DraftPoints.Add(point);
        DraftBendRadii.Add(0.0);
        int total = DraftPoints.Count;
        if (total >= 3)
        {
            // The former endpoint (now at total - 2) is interior; assign current effective radius.
            DraftBendRadii[total - 2] = EffectiveRadius;
        }
    }

    public void ResetDraft(bool clearStatus = true)
    {
        DraftPoints.Clear();
        DraftBendRadii.Clear();
        _latestAnalysis = null;
        _latestInteractiveCandidate = null;
        _continuedPolylineId = ObjectId.Null;
        _manualRadius = null;
        _previewManager.Clear();
        if (clearStatus)
        {
            SetStatus("Draft cleared.", PipePlanStatusKind.Info);
        }
    }

    public PipePlanAnalysis AnalyzeCurrentDraft()
    {
        return AnalyzePoints(DraftPoints, DraftBendRadii);
    }

    public PipePlanAnalysis AnalyzeWithCandidate(Point3d candidate)
    {
        List<Point3d> points = [.. DraftPoints, candidate];
        List<double> radii = new(points.Count);
        radii.AddRange(DraftBendRadii);
        radii.Add(0.0);
        if (points.Count >= 3)
        {
            radii[points.Count - 2] = EffectiveRadius;
        }

        return AnalyzePoints(points, radii);
    }

    public void SetLatestAnalysis(PipePlanAnalysis analysis)
    {
        _latestAnalysis = analysis;
    }

    public PipePlanCandidateResult PreviewCandidate(Point3d rawCandidate, bool allowStraightSnap)
    {
        PipePlanCandidateResult candidate = BuildCandidateResult(rawCandidate, allowStraightSnap);
        _latestInteractiveCandidate = candidate;
        ShowPreview(candidate.Analysis);
        SetStatus(GetCandidateStatusMessage(candidate), GetCandidateStatusKind(candidate));
        return candidate;
    }

    public PipePlanCandidateResult ResolveCommittedCandidate(Point3d rawCandidate, bool allowStraightSnap)
    {
        if (_latestInteractiveCandidate is not null &&
            _latestInteractiveCandidate.RawPoint.DistanceTo(rawCandidate) <= PointMatchTolerance)
        {
            return _latestInteractiveCandidate;
        }

        return BuildCandidateResult(rawCandidate, allowStraightSnap);
    }

    public void RefreshDraftPreview()
    {
        _latestInteractiveCandidate = null;

        PipePlanAnalysis analysis = AnalyzeCurrentDraft();
        _latestAnalysis = analysis;
        ShowPreview(analysis);

        if (DraftPoints.Count < 2)
        {
            SetStatus("Pick at least two points.", PipePlanStatusKind.Info);
            return;
        }

        SetStatus(
            analysis.IsFeasible ? "Current draft is feasible." : analysis.Message,
            analysis.IsFeasible ? PipePlanStatusKind.Ok : PipePlanStatusKind.Error);
    }

    public void ShowPreview(PipePlanAnalysis analysis)
    {
        double globalWidth = _activeContext is { LayerName: var layerName }
            ? PipePlanWidthCalculator.ResolveDrawingWidth(layerName)
            : 0.0;
        _previewManager.Show(analysis, globalWidth);
    }

    public void ClearPreview()
    {
        _previewManager.Clear();
    }

    public void SetStatus(string message, PipePlanStatusKind kind)
    {
        _palette?.SetStatus(message, kind);
    }

    public void BeginDraftFromExisting(ObjectId polylineId, PipePlanStoredData data, bool reverse)
    {
        ApplyStoredContext(data);

        _continuedPolylineId = polylineId;
        DraftPoints.Clear();
        DraftBendRadii.Clear();
        _latestAnalysis = null;
        _latestInteractiveCandidate = null;

        if (reverse)
        {
            DraftPoints.AddRange(data.ControlPoints.AsEnumerable().Reverse());
            DraftBendRadii.AddRange(data.BendRadii.AsEnumerable().Reverse());
        }
        else
        {
            DraftPoints.AddRange(data.ControlPoints);
            DraftBendRadii.AddRange(data.BendRadii);
        }

        RefreshDraftPreview();
    }

    public void BakeDraft()
    {
        Document? document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        if (!TryPrepareBake(out PipePlanActiveContext? context, out PipePlanAnalysis? analysis) || context is null || analysis is null)
        {
            return;
        }

        using DocumentLock documentLock = document.LockDocument();
        using Transaction transaction = document.Database.TransactionManager.StartTransaction();

        try
        {
            string layerName = EnsureBakeLayer(document.Database, context, transaction);
            EnsurePipeTagApp(document.Database, transaction);
            WriteBakedGeometry(document.Database, transaction, context, analysis, layerName);
            transaction.Commit();
        }
        catch
        {
            transaction.Abort();
            throw;
        }

        ResetDraft(clearStatus: false);
        string successMessage = $"Baked {context.System} {context.Type} DN{context.Dn} polyline to layer {context.LayerName}.";
        SetStatus(successMessage, PipePlanStatusKind.Ok);
        document.Editor.WriteMessage($"\n{successMessage}");
    }

    private PipePlanAnalysis AnalyzePoints(IReadOnlyList<Point3d> points, IReadOnlyList<double> radii)
    {
        if (_activeContext is null)
        {
            return PipePlanAnalysis.Invalid(points, "No active pipe context. Activate an FJV layer in NSPalette and re-run PPDRAW.");
        }

        if (EffectiveRadius <= 0.0)
        {
            return PipePlanAnalysis.Invalid(points, "No valid bending radius. Set a manual radius (R) or configure one in PPSETTINGS.");
        }

        return _solver.Analyze(points, radii);
    }

    public void ApplyStoredContext(PipePlanStoredData data)
    {
        StraightSnapToleranceText = data.StraightSnapToleranceText;

        string layerName = BuildLayerName(data.System, data.Type, data.Dn);
        double defaultRadius = ResolveDefaultRadiusFromData(data);
        _activeContext = new PipePlanActiveContext(data.System, data.Type, data.Dn, defaultRadius, layerName);
    }

    private static double ResolveDefaultRadiusFromData(PipePlanStoredData data)
    {
        Document? doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
        if (doc is not null &&
            PipePlanRadiusStore.TryGet(doc.Database, data.System, data.Type, data.Dn, out double storeValue))
        {
            return storeValue;
        }

        for (int i = 1; i < data.BendRadii.Count - 1; i++)
        {
            if (data.BendRadii[i] > 0.0) return data.BendRadii[i];
        }

        return 0.0;
    }

    private static string BuildLayerName(PipeSystemEnum system, PipeTypeEnum type, int dn)
    {
        string systemString = PipeScheduleV2.PipeScheduleV2.GetSystemString(system);
        return $"FJV-{type.ToString().ToUpperInvariant()}-{systemString.ToUpperInvariant()}{dn}";
    }

    private bool TryPrepareBake(out PipePlanActiveContext? context, out PipePlanAnalysis? analysis)
    {
        context = null;
        analysis = null;

        if (DraftPoints.Count < 2)
        {
            SetStatus("Pick at least two points before baking.", PipePlanStatusKind.Warning);
            return false;
        }

        context = _activeContext;
        if (context is null)
        {
            SetStatus("No active pipe context. Activate an FJV layer in NSPalette first.", PipePlanStatusKind.Warning);
            return false;
        }

        analysis = AnalyzeCurrentDraft();
        if (!analysis.IsFeasible)
        {
            ShowPreview(analysis);
            SetStatus(analysis.Message, PipePlanStatusKind.Error);
            return false;
        }

        return true;
    }

    private static string EnsureBakeLayer(Database database, PipePlanActiveContext context, Transaction transaction)
    {
        LayerTable layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        string layerName = context.LayerName;
        if (!layerTable.Has(layerName))
        {
            layerTable.UpgradeOpen();
            LayerTableRecord layer = new()
            {
                Name = layerName
            };
            layerTable.Add(layer);
            transaction.AddNewlyCreatedDBObject(layer, add: true);
        }

        return layerName;
    }

    private static void EnsurePipeTagApp(Database database, Transaction transaction)
    {
        RegAppTable regAppTable = (RegAppTable)transaction.GetObject(database.RegAppTableId, OpenMode.ForRead);
        if (regAppTable.Has(PipePlanMetadata.PipeTagAppName))
        {
            return;
        }

        regAppTable.UpgradeOpen();
        RegAppTableRecord regApp = new()
        {
            Name = PipePlanMetadata.PipeTagAppName
        };
        regAppTable.Add(regApp);
        transaction.AddNewlyCreatedDBObject(regApp, add: true);
    }

    private void WriteBakedGeometry(
        Database database,
        Transaction transaction,
        PipePlanActiveContext context,
        PipePlanAnalysis analysis,
        string layerName)
    {
        BlockTableRecord modelSpace = GetModelSpace(database, transaction);
        PipePlanStoredData storedData = CreateStoredData(context);

        if (_continuedPolylineId != ObjectId.Null)
        {
            WriteReplacementPolyline(transaction, analysis, context, layerName, modelSpace, storedData);
        }
        else
        {
            WriteNewPolyline(transaction, analysis, context, layerName, modelSpace, storedData);
        }
    }

    private static BlockTableRecord GetModelSpace(Database database, Transaction transaction)
    {
        BlockTable blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
        return (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
    }

    private PipePlanStoredData CreateStoredData(PipePlanActiveContext context)
    {
        return new PipePlanStoredData(
            context.System,
            context.Type,
            context.Dn,
            DraftBendRadii,
            StraightSnapToleranceText,
            DraftPoints);
    }

    private void WriteReplacementPolyline(
        Transaction transaction,
        PipePlanAnalysis analysis,
        PipePlanActiveContext context,
        string layerName,
        BlockTableRecord modelSpace,
        PipePlanStoredData storedData)
    {
        Polyline sourcePolyline = (Polyline)transaction.GetObject(_continuedPolylineId, OpenMode.ForWrite);
        Polyline replacement = ReplaceExistingPolyline(sourcePolyline, analysis, context, layerName, modelSpace, transaction);
        PipePlanMetadata.Write(replacement, storedData, transaction);
    }

    private static void WriteNewPolyline(
        Transaction transaction,
        PipePlanAnalysis analysis,
        PipePlanActiveContext context,
        string layerName,
        BlockTableRecord modelSpace,
        PipePlanStoredData storedData)
    {
        using Polyline polyline = analysis.CreatePolyline();
        polyline.Layer = layerName;
        polyline.ConstantWidth = PipePlanWidthCalculator.ResolveDrawingWidth(context.LayerName);

        modelSpace.AppendEntity(polyline);
        transaction.AddNewlyCreatedDBObject(polyline, add: true);
        PipePlanMetadata.Write(polyline, storedData, transaction);
    }

    private static Polyline ReplaceExistingPolyline(
        Polyline sourcePolyline,
        PipePlanAnalysis analysis,
        PipePlanActiveContext context,
        string layerName,
        BlockTableRecord owner,
        Transaction transaction)
    {
        Polyline replacement = analysis.CreatePolyline();
        replacement.SetDatabaseDefaults(sourcePolyline.Database);
        replacement.SetPropertiesFrom(sourcePolyline);
        replacement.Layer = layerName;
        replacement.LayerId = sourcePolyline.LayerId;
        replacement.LinetypeId = sourcePolyline.LinetypeId;
        replacement.LineWeight = sourcePolyline.LineWeight;
        replacement.LinetypeScale = sourcePolyline.LinetypeScale;
        replacement.Transparency = sourcePolyline.Transparency;
        replacement.Normal = sourcePolyline.Normal;
        replacement.Elevation = sourcePolyline.Elevation;
        replacement.Thickness = sourcePolyline.Thickness;
        replacement.ConstantWidth = PipePlanWidthCalculator.ResolveDrawingWidth(context.LayerName);
        replacement.Closed = false;

        owner.AppendEntity(replacement);
        transaction.AddNewlyCreatedDBObject(replacement, add: true);

        if (!sourcePolyline.IsErased)
        {
            sourcePolyline.Erase();
        }

        return replacement;
    }

    private PipePlanCandidateResult BuildCandidateResult(Point3d rawCandidate, bool allowStraightSnap)
    {
        CandidateResolution resolution = ResolveCandidatePoint(rawCandidate, allowStraightSnap);
        PipePlanAnalysis analysis = AnalyzeWithCandidate(resolution.FinalPoint);
        if (resolution.StraightSnapActive)
        {
            analysis = analysis.WithPreviewKind(PipePlanPreviewKind.StraightSnap);
        }

        return new PipePlanCandidateResult(
            rawCandidate,
            resolution.FinalPoint,
            analysis,
            resolution.StraightSnapActive);
    }

    private CandidateResolution ResolveCandidatePoint(Point3d rawCandidate, bool allowStraightSnap)
    {
        if (allowStraightSnap &&
            TryGetStraightSnapTolerance(out double tolerance) &&
            TryApplyStraightSnap(rawCandidate, tolerance, out Point3d straightSnappedPoint))
        {
            return new CandidateResolution(straightSnappedPoint, true);
        }

        return new CandidateResolution(rawCandidate, false);
    }

    private bool TryApplyStraightSnap(Point3d rawCandidate, double tolerance, out Point3d snappedCandidate)
    {
        snappedCandidate = rawCandidate;
        if (DraftPoints.Count < 2)
        {
            return false;
        }

        Point3d previousPoint = DraftPoints[^2];
        Point3d anchorPoint = DraftPoints[^1];
        Vector2d segmentDirection = new(anchorPoint.X - previousPoint.X, anchorPoint.Y - previousPoint.Y);
        double segmentLength = segmentDirection.Length;
        if (segmentLength <= DistanceTolerance)
        {
            return false;
        }

        Vector2d unitDirection = segmentDirection / segmentLength;
        Vector2d offset = new(rawCandidate.X - anchorPoint.X, rawCandidate.Y - anchorPoint.Y);
        double alongDistance = offset.DotProduct(unitDirection);
        if (alongDistance <= DistanceTolerance)
        {
            return false;
        }

        Point2d projectedPoint = new(
            anchorPoint.X + (unitDirection.X * alongDistance),
            anchorPoint.Y + (unitDirection.Y * alongDistance));

        double perpendicularDistance = new Point2d(rawCandidate.X, rawCandidate.Y).GetDistanceTo(projectedPoint);
        if (perpendicularDistance > tolerance)
        {
            return false;
        }

        snappedCandidate = new Point3d(projectedPoint.X, projectedPoint.Y, rawCandidate.Z);
        return true;
    }

    private static string GetCandidateStatusMessage(PipePlanCandidateResult candidate)
    {
        if (!candidate.Analysis.IsFeasible)
        {
            return candidate.Analysis.Message;
        }

        return candidate.StraightSnapActive
            ? "Straight snap active. Release Ctrl to disable."
            : "Current draft is feasible. Hold Ctrl to snap straight.";
    }

    private static PipePlanStatusKind GetCandidateStatusKind(PipePlanCandidateResult candidate)
    {
        if (!candidate.Analysis.IsFeasible)
        {
            return PipePlanStatusKind.Error;
        }

        return candidate.StraightSnapActive
            ? PipePlanStatusKind.Snap
            : PipePlanStatusKind.Ok;
    }

    private sealed record CandidateResolution(
        Point3d FinalPoint,
        bool StraightSnapActive);
}
