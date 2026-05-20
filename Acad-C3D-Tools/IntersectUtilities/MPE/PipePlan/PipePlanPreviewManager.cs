using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Geometry;
using System.Globalization;

namespace IntersectUtilities.MPE.PipePlan;

internal sealed class PipePlanPreviewManager : IDisposable
{
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

        AddRadiusLabels(analysis);
        AddFilletEndpointMarkers(analysis, globalWidth);
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
