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

    private void AddRadiusLabels(PipePlanAnalysis analysis)
    {
        double textHeight = GetTextHeight();
        foreach (PipePlanRadiusAnnotation annotation in analysis.RadiusAnnotations)
        {
            AddTransient(CreateRadiusLabel(annotation, analysis, textHeight));
        }
    }

    private static MText CreateRadiusLabel(PipePlanRadiusAnnotation annotation, PipePlanAnalysis analysis, double textHeight)
    {
        MText label = new();
        label.SetDatabaseDefaults();
        label.Color = analysis.GetPreviewColor();
        label.Contents = $"R={annotation.Radius.ToString("0.###", CultureInfo.CurrentCulture)}";
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
