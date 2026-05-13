using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace PipePlan.Plugin;

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
    private string _activeSizeName;
    private ObjectId _continuedPolylineId = ObjectId.Null;

    public PipePlanState()
    {
        Sizes =
        [
            new PipeSizeOption("DN 50") { RadiusText = "36" },
            new PipeSizeOption("DN 100") { RadiusText = "68" },
            new PipeSizeOption("DN 150") { RadiusText = "101" },
            new PipeSizeOption("DN 200") { RadiusText = "132" },
            new PipeSizeOption("DN 250") { RadiusText = "164" }
        ];

        _activeSizeName = Sizes[0].Name;
        StraightSnapToleranceText = "1";
    }

    public List<PipeSizeOption> Sizes { get; }

    public List<Point3d> DraftPoints { get; } = [];

    public string StraightSnapToleranceText { get; set; }

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

    public PipeSizeOption? GetSelectedSize()
    {
        return Sizes.FirstOrDefault(size => string.Equals(size.Name, _activeSizeName, StringComparison.OrdinalIgnoreCase))
               ?? Sizes.FirstOrDefault();
    }

    public void SetSelectedSize(string? sizeName)
    {
        PipeSizeOption? selected = Sizes.FirstOrDefault(size => string.Equals(size.Name, sizeName, StringComparison.OrdinalIgnoreCase));
        if (selected is null)
        {
            return;
        }

        _activeSizeName = selected.Name;
        _palette?.UpdateFromState();
    }

    public bool TryGetSelectedRadius(out double radius)
    {
        PipeSizeOption? selected = GetSelectedSize();
        radius = 0.0;
        return selected is not null && selected.TryGetRadius(out radius);
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
        PipeSizeOption? size = GetSelectedSize();
        double globalWidth = size?.GetGlobalWidth() ?? 0.0;
        _previewManager.Show(analysis, globalWidth, fittingProposal);
        _palette?.UpdateFromState();
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
        ApplyStoredSettings(data);

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

        if (!TryPrepareBake(out PipeSizeOption? size, out PipePlanAnalysis? analysis))
        {
            return;
        }

        using DocumentLock documentLock = document.LockDocument();
        using Transaction transaction = document.Database.TransactionManager.StartTransaction();

        try
        {
            string layerName = EnsureBakeLayer(document.Database, size!, transaction);
            EnsurePipeTagApp(document.Database, transaction);
            WriteBakedGeometry(document.Database, transaction, size!, analysis!, layerName);
            transaction.Commit();
        }
        catch
        {
            transaction.Abort();
            throw;
        }

        ResetDraft(clearStatus: false);
        string successMessage = $"Baked {size!.Name} polyline to layer {size.GetLayerName()}.";
        SetStatus(successMessage, PipePlanStatusKind.Ok);
        document.Editor.WriteMessage($"\n{successMessage}");
    }

    private PipePlanAnalysis AnalyzePoints(IReadOnlyList<Point3d> points)
    {
        if (!TryGetSelectedRadius(out double radius))
        {
            return PipePlanAnalysis.Invalid(points, "Enter a valid radius for the selected size.");
        }

        return _solver.Analyze(points, radius);
    }

    private void ApplyStoredSettings(PipePlanStoredData data)
    {
        PipeSizeOption? selected = Sizes.FirstOrDefault(size => string.Equals(size.Name, data.SizeName, StringComparison.OrdinalIgnoreCase));
        if (selected is not null)
        {
            selected.RadiusText = data.RadiusText;
            _activeSizeName = selected.Name;
        }

        StraightSnapToleranceText = data.StraightSnapToleranceText;
    }

    private bool TryPrepareBake(out PipeSizeOption? size, out PipePlanAnalysis? analysis)
    {
        size = null;
        analysis = null;

        if (DraftPoints.Count < 2)
        {
            SetStatus("Pick at least two points before baking.", PipePlanStatusKind.Warning);
            return false;
        }

        size = GetSelectedSize();
        if (size is null)
        {
            SetStatus("Select a pipe size first.", PipePlanStatusKind.Warning);
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

    private static string EnsureBakeLayer(Database database, PipeSizeOption size, Transaction transaction)
    {
        LayerTable layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        string layerName = size.GetLayerName();
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
        PipeSizeOption size,
        PipePlanAnalysis analysis,
        string layerName)
    {
        BlockTableRecord modelSpace = GetModelSpace(database, transaction);
        PipePlanStoredData storedData = CreateStoredData(size);

        if (_continuedPolylineId != ObjectId.Null)
        {
            WriteReplacementPolyline(transaction, analysis, size, layerName, modelSpace, storedData);
        }
        else
        {
            WriteNewPolyline(transaction, analysis, size, layerName, modelSpace, storedData);
        }

        AppendFittingGeometry(_draftFittingProposals, size.GetGlobalWidth(), layerName, modelSpace, transaction);
    }

    private static BlockTableRecord GetModelSpace(Database database, Transaction transaction)
    {
        BlockTable blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
        return (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
    }

    private PipePlanStoredData CreateStoredData(PipeSizeOption size)
    {
        return new PipePlanStoredData(size.Name, size.RadiusText, StraightSnapToleranceText, DraftPoints);
    }

    private void WriteReplacementPolyline(
        Transaction transaction,
        PipePlanAnalysis analysis,
        PipeSizeOption size,
        string layerName,
        BlockTableRecord modelSpace,
        PipePlanStoredData storedData)
    {
        Polyline sourcePolyline = (Polyline)transaction.GetObject(_continuedPolylineId, OpenMode.ForWrite);
        Polyline replacement = ReplaceExistingPolyline(sourcePolyline, analysis, size, layerName, modelSpace, transaction);
        PipePlanMetadata.Write(replacement, storedData, transaction);
    }

    private static void WriteNewPolyline(
        Transaction transaction,
        PipePlanAnalysis analysis,
        PipeSizeOption size,
        string layerName,
        BlockTableRecord modelSpace,
        PipePlanStoredData storedData)
    {
        using Polyline polyline = analysis.CreatePolyline();
        polyline.Layer = layerName;
        polyline.ConstantWidth = size.GetGlobalWidth();

        modelSpace.AppendEntity(polyline);
        transaction.AddNewlyCreatedDBObject(polyline, add: true);
        PipePlanMetadata.Write(polyline, storedData, transaction);
    }

    private static Polyline ReplaceExistingPolyline(
        Polyline sourcePolyline,
        PipePlanAnalysis analysis,
        PipeSizeOption size,
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
        replacement.ConstantWidth = size.GetGlobalWidth();
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
