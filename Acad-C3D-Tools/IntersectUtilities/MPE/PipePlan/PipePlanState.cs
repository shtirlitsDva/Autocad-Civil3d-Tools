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
    private readonly List<PipePlanFittingProposal> _draftFittingProposals = [];

    private PipePlanPalette? _palette;
    private PipePlanAnalysis? _latestAnalysis;
    private PipePlanCandidateResult? _latestInteractiveCandidate;
    private PipePlanActiveContext? _activeContext;
    private ObjectId _continuedPolylineId = ObjectId.Null;

    public PipePlanState()
    {
        StraightSnapToleranceText = "1";
    }

    public List<Point3d> DraftPoints { get; } = [];

    public string StraightSnapToleranceText { get; set; }

    public PipePlanActiveContext? ActiveContext => _activeContext;

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
        DraftPoints.Add(point);
        RefreshDraftPreview();
    }

    public void AddCommittedCandidate(PipePlanCandidateResult candidate)
    {
        _latestInteractiveCandidate = null;
        DraftPoints.Add(candidate.FinalPoint);
        if (candidate.FittingProposal is not null)
        {
            _draftFittingProposals.Add(candidate.FittingProposal);
        }

        RefreshDraftPreview();
    }

    public void ResetDraft(bool clearStatus = true)
    {
        DraftPoints.Clear();
        _draftFittingProposals.Clear();
        _latestAnalysis = null;
        _latestInteractiveCandidate = null;
        _continuedPolylineId = ObjectId.Null;
        _previewManager.Clear();
        if (clearStatus)
        {
            SetStatus("Draft cleared.", PipePlanStatusKind.Info);
        }
    }

    public PipePlanAnalysis AnalyzeCurrentDraft()
    {
        return AnalyzePoints(DraftPoints);
    }

    public PipePlanAnalysis AnalyzeWithCandidate(Point3d candidate)
    {
        List<Point3d> points = [.. DraftPoints, candidate];
        return AnalyzePoints(points);
    }

    public void SetLatestAnalysis(PipePlanAnalysis analysis)
    {
        _latestAnalysis = analysis;
    }

    public PipePlanCandidateResult PreviewCandidate(Point3d rawCandidate, bool allowStraightSnap)
    {
        PipePlanCandidateResult candidate = BuildCandidateResult(rawCandidate, allowStraightSnap);
        _latestInteractiveCandidate = candidate;
        ShowPreview(candidate.Analysis, candidate.FittingProposal);
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

    public void ShowPreview(PipePlanAnalysis analysis, PipePlanFittingProposal? fittingProposal = null)
    {
        double globalWidth = _activeContext?.Width ?? 0.0;
        _previewManager.Show(analysis, globalWidth, fittingProposal);
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
        _latestAnalysis = null;
        _latestInteractiveCandidate = null;

        IEnumerable<Point3d> orderedPoints = reverse
            ? data.ControlPoints.AsEnumerable().Reverse()
            : data.ControlPoints;

        DraftPoints.AddRange(orderedPoints);
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

    private PipePlanAnalysis AnalyzePoints(IReadOnlyList<Point3d> points)
    {
        if (_activeContext is null || _activeContext.Radius <= 0.0)
        {
            return PipePlanAnalysis.Invalid(points, "No active pipe context. Activate an FJV layer in NSPalette and re-run PPDRAW.");
        }

        return _solver.Analyze(points, _activeContext.Radius);
    }

    public void ApplyStoredContext(PipePlanStoredData data)
    {
        StraightSnapToleranceText = data.StraightSnapToleranceText;

        double width = ResolveWidthFromStored(data);
        string layerName = BuildLayerName(data.System, data.Type, data.Dn);
        _activeContext = new PipePlanActiveContext(data.System, data.Type, data.Dn, width, data.Radius, layerName);
    }

    private static double ResolveWidthFromStored(PipePlanStoredData data)
    {
        PipeSeriesEnum series = NSPaletteAdapter.TryGetCurrentSeries(out PipeSeriesEnum s)
            ? s
            : PipeSeriesEnum.S3;

        try
        {
            double kOd = PipeScheduleV2.PipeScheduleV2.GetPipeKOd(data.System, data.Dn, data.Type, series);
            if (kOd > 0.0) return kOd;
        }
        catch
        {
            // fall through to default
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

        AppendFittingGeometry(_draftFittingProposals, context.Width, layerName, modelSpace, transaction);
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
            context.Radius,
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
        polyline.ConstantWidth = context.Width;

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
        replacement.ConstantWidth = context.Width;
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
            resolution.StraightSnapActive,
            resolution.FittingProposal);
    }

    private CandidateResolution ResolveCandidatePoint(Point3d rawCandidate, bool allowStraightSnap)
    {
        Document? document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
        if (TryApplyFittingSnap(document, rawCandidate, out PipePlanFittingProposal? fittingProposal, out Point3d fittingSnappedPoint))
        {
            return new CandidateResolution(fittingSnappedPoint, false, fittingProposal);
        }

        if (allowStraightSnap &&
            TryGetStraightSnapTolerance(out double tolerance) &&
            TryApplyStraightSnap(rawCandidate, tolerance, out Point3d straightSnappedPoint))
        {
            return new CandidateResolution(straightSnappedPoint, true, null);
        }

        return new CandidateResolution(rawCandidate, false, null);
    }

    private bool TryApplyFittingSnap(
        Document? document,
        Point3d rawCandidate,
        out PipePlanFittingProposal? fittingProposal,
        out Point3d snappedPoint)
    {
        fittingProposal = null;
        snappedPoint = rawCandidate;

        return document is not null &&
               DraftPoints.Count >= 1 &&
               PipePlanFittingSnapService.TryFindBestProposal(
                   document,
                   DraftPoints[^1],
                   rawCandidate,
                   _continuedPolylineId,
                   out fittingProposal,
                   out snappedPoint,
                   out _);
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

        if (candidate.FittingProposal is not null)
        {
            return candidate.FittingProposal.Kind == PipePlanFittingKind.Tee
                ? "T fitting snap active."
                : "X fitting snap active.";
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

        if (candidate.FittingProposal is not null)
        {
            return PipePlanStatusKind.Ok;
        }

        return candidate.StraightSnapActive
            ? PipePlanStatusKind.Snap
            : PipePlanStatusKind.Ok;
    }

    private static void AppendFittingGeometry(
        IReadOnlyList<PipePlanFittingProposal> fittingProposals,
        double globalWidth,
        string layerName,
        BlockTableRecord modelSpace,
        Transaction transaction)
    {
        foreach (PipePlanFittingProposal fittingProposal in fittingProposals)
        {
            foreach (Polyline fittingPolyline in PipePlanFittingGeometry.CreatePolylines(fittingProposal, globalWidth))
            {
                fittingPolyline.Layer = layerName;
                modelSpace.AppendEntity(fittingPolyline);
                transaction.AddNewlyCreatedDBObject(fittingPolyline, add: true);
            }
        }
    }

    private sealed record CandidateResolution(
        Point3d FinalPoint,
        bool StraightSnapActive,
        PipePlanFittingProposal? FittingProposal);
}
