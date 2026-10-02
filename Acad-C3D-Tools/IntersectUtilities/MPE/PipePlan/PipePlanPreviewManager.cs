using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Geometry;
using System.Globalization;

namespace IntersectUtilities.MPE.PipePlan;

internal sealed class PipePlanPreviewManager : IDisposable
{
    // Arc segments are overlaid in a darker green than the straight-segment green so the
    // bends are visually distinct from the straights while drawing/editing.
    private static readonly Autodesk.AutoCAD.Colors.Color ArcPreviewColor =
        Autodesk.AutoCAD.Colors.Color.FromRgb(0, 100, 45);

    private readonly IntegerCollection _transientViewportNumbers = [];
    private readonly List<Entity> _previewEntities = [];
    private readonly Document _owner;

    public PipePlanPreviewManager(Document owner)
    {
        _owner = owner;
    }

    public void Dispose()
    {
        Clear();
    }

    public void Clear()
    {
        foreach (Entity entity in _previewEntities)
        {
            try
            {
                TransientManager.CurrentTransientManager.EraseTransient(entity, _transientViewportNumbers);
            }
            catch
            {
                // Best effort cleanup for transient preview entities.
            }

            entity.Dispose();
        }

        _previewEntities.Clear();
    }

    public void Show(PipePlanAnalysis analysis, double globalWidth)
    {
        Clear();
        if (analysis.Vertices.Count < 2)
        {
            return;
        }

        Autodesk.AutoCAD.DatabaseServices.Polyline previewPolyline = CreatePreviewPolyline(analysis, globalWidth);
        AddTransient(previewPolyline);

        // Only distinguish arcs in the plain feasible (green) preview; leave the red
        // "infeasible" and blue/cyan snap states as one solid colour so they read clearly.
        if (analysis.IsFeasible && analysis.PreviewKind == PipePlanPreviewKind.Standard)
        {
            AddArcOverlays(analysis, globalWidth);
        }

        AddRadiusLabels(analysis);
        AddFilletEndpointMarkers(analysis, globalWidth);
    }

    /// <summary>
    /// Bonded pair preview. The status lives on the centreline (green / dark-green arcs /
    /// blue snap / red), with the R labels showing the INNER-pipe radius; the sides live on
    /// the pipes: frem red, retur blue, full jacket width, about 55 % transparent. When the
    /// run is infeasible everything turns red and the pipes are mitered sharp.
    /// </summary>
    public void ShowPair(PipePlanAnalysis centre, PipePlanPairSpacing spacing, bool flip)
    {
        Clear();
        if (centre.Vertices.Count < 2)
        {
            return;
        }

        double elevation = centre.ControlPoints.Count > 0 ? centre.ControlPoints[0].Z : 0.0;
        Autodesk.AutoCAD.Colors.Color statusColor = centre.GetPreviewColor();
        (IReadOnlyList<PolylineVertexData> frem, IReadOnlyList<PolylineVertexData> retur) = PipePlanPairGeometry
            .Solve(centre, spacing.Half, flip)
            .Match<(IReadOnlyList<PolylineVertexData>, IReadOnlyList<PolylineVertexData>)>(
                solution => (solution.Frem, solution.Retur),
                _ => PipePlanPairGeometry.Miter(centre.ControlPoints, spacing.Half, flip)
                    .Match<(IReadOnlyList<PolylineVertexData>, IReadOnlyList<PolylineVertexData>)>(
                        mitered => (mitered.Frem, mitered.Retur),
                        _ => ([], [])));

        // Pipes first, so the centreline and its labels draw on top.
        AddPairPipe(frem, centre.IsFeasible ? FremColor : statusColor, spacing.PipeWidth, elevation);
        AddPairPipe(retur, centre.IsFeasible ? ReturColor : statusColor, spacing.PipeWidth, elevation);

        Autodesk.AutoCAD.DatabaseServices.Polyline centreline = CreatePreviewPolyline(centre, 0.0);
        centreline.Elevation = elevation;
        AddTransient(centreline);

        if (centre.IsFeasible && centre.PreviewKind == PipePlanPreviewKind.Standard)
        {
            AddArcOverlays(centre, 0.0);
        }

        AddRadiusLabels(centre, spacing.Half);
        AddFilletEndpointMarkers(centre, spacing.PipeWidth);
    }

    private static readonly Autodesk.AutoCAD.Colors.Color FremColor =
        Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 1);

    private static readonly Autodesk.AutoCAD.Colors.Color ReturColor =
        Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 5);

    // ~55 % transparent, so frem/retur read as a pair and the centreline shows through.
    private const byte PairPipeAlpha = 115;

    private void AddPairPipe(IReadOnlyList<PolylineVertexData> vertices, Autodesk.AutoCAD.Colors.Color color, double width, double elevation)
    {
        if (vertices.Count < 2)
        {
            return;
        }

        Autodesk.AutoCAD.DatabaseServices.Polyline pipe = new();
        for (int i = 0; i < vertices.Count; i++)
        {
            pipe.AddVertexAt(i, vertices[i].Point, vertices[i].Bulge, 0.0, 0.0);
        }

        pipe.Elevation = elevation;
        pipe.ConstantWidth = width;
        pipe.Color = color;
        pipe.Transparency = new Autodesk.AutoCAD.Colors.Transparency(PairPipeAlpha);
        AddTransient(pipe);
    }

    // Overlays each arc segment with a dark-green copy on top of the green base polyline,
    // so straights stay green and arcs (non-zero bulge) read dark green.
    private void AddArcOverlays(PipePlanAnalysis analysis, double globalWidth)
    {
        IReadOnlyList<PolylineVertexData> vertices = analysis.Vertices;
        for (int i = 0; i < vertices.Count - 1; i++)
        {
            double bulge = vertices[i].Bulge;
            if (!PipePlanArcGeometry.IsArcBulge(bulge))
            {
                continue;
            }

            Autodesk.AutoCAD.DatabaseServices.Polyline arc = new();
            arc.AddVertexAt(0, vertices[i].Point, bulge, 0.0, 0.0);
            arc.AddVertexAt(1, vertices[i + 1].Point, 0.0, 0.0, 0.0);
            arc.Color = ArcPreviewColor;
            arc.ConstantWidth = globalWidth;
            arc.LineWeight = LineWeight.LineWeight050;
            AddTransient(arc);
        }
    }

    private Autodesk.AutoCAD.DatabaseServices.Polyline CreatePreviewPolyline(PipePlanAnalysis analysis, double globalWidth)
    {
        Autodesk.AutoCAD.DatabaseServices.Polyline polyline = analysis.CreatePolyline();
        polyline.Color = analysis.GetPreviewColor();
        polyline.ConstantWidth = globalWidth;
        polyline.LineWeight = LineWeight.LineWeight050;
        return polyline;
    }

    // radiusOffset: a pair's annotations are on the centreline, whose radius is the inner
    // pipe's plus c/2; the label shows the inner pipe's.
    private void AddRadiusLabels(PipePlanAnalysis analysis, double radiusOffset = 0.0)
    {
        double textHeight = GetTextHeight();
        foreach (PipePlanRadiusAnnotation annotation in analysis.RadiusAnnotations)
        {
            AddTransient(CreateRadiusLabel(annotation, analysis, textHeight, radiusOffset));
        }
    }

    private static MText CreateRadiusLabel(PipePlanRadiusAnnotation annotation, PipePlanAnalysis analysis, double textHeight, double radiusOffset)
    {
        MText label = new();
        label.SetDatabaseDefaults();
        label.Color = analysis.GetPreviewColor();
        label.Contents = $"R={(annotation.Radius - radiusOffset).ToString("0.###", CultureInfo.CurrentCulture)}";
        label.TextHeight = textHeight;
        label.Attachment = AttachmentPoint.MiddleCenter;
        label.Location = GetLabelLocation(annotation, textHeight);
        return label;
    }

    private void AddFilletEndpointMarkers(PipePlanAnalysis analysis, double globalWidth)
    {
        double markerRadius = GetFilletMarkerRadius(globalWidth);
        foreach (PipePlanFilletEndpointMarker marker in analysis.FilletEndpointMarkers)
        {
            AddTransient(CreateFilletEndpointMarker(marker.TangentIn, analysis, markerRadius));
            AddTransient(CreateFilletEndpointMarker(marker.TangentOut, analysis, markerRadius));
        }
    }

    private void AddTransient(Entity entity)
    {
        _previewEntities.Add(entity);
        TransientManager.CurrentTransientManager.AddTransient(
            entity,
            TransientDrawingMode.DirectShortTerm,
            128,
            _transientViewportNumbers);
    }

    private static Point3d GetLabelLocation(PipePlanRadiusAnnotation annotation, double textHeight)
    {
        Vector3d direction = annotation.ArcMidPoint - annotation.Center;
        if (direction.Length <= 1e-6)
        {
            return annotation.ArcMidPoint;
        }

        Vector3d offset = direction.GetNormal() * Math.Max(textHeight * 0.8, annotation.Radius * 0.05);
        return annotation.ArcMidPoint + offset;
    }

    private static Circle CreateFilletEndpointMarker(Point3d point, PipePlanAnalysis analysis, double markerRadius)
    {
        Circle marker = new(point, Vector3d.ZAxis, markerRadius);
        marker.Color = analysis.GetPreviewColor();
        marker.LineWeight = LineWeight.LineWeight050;
        return marker;
    }

    private double GetFilletMarkerRadius(double globalWidth)
    {
        return Math.Max(GetViewBasedMarkerRadius(), globalWidth * 0.75);
    }

    private double GetViewBasedMarkerRadius()
    {
        try
        {
            using ViewTableRecord view = _owner.Editor.GetCurrentView();
            return Math.Clamp(view.Height / 140.0, 0.08, 2.5);
        }
        catch
        {
            // Fall back to a conservative marker radius.
            return 0.15;
        }
    }

    private double GetTextHeight()
    {
        try
        {
            using ViewTableRecord view = _owner.Editor.GetCurrentView();
            return Math.Clamp(view.Height / 40.0, 0.3, 8.0);
        }
        catch
        {
            // Fall through to TEXTSIZE-based fallback.
        }

        try
        {
            object? value = Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("TEXTSIZE");
            if (value is double textHeight && textHeight > 0.0)
            {
                return textHeight;
            }
        }
        catch
        {
            // Fall through to the default preview text height.
        }

        return 1.0;
    }
}
