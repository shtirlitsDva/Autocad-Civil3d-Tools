using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using System.Globalization;

namespace IntersectUtilities.MPE.PipePlan;

internal sealed class PipePlanHandleMarkerManager : IDisposable
{
    private readonly Autodesk.AutoCAD.Geometry.IntegerCollection _viewportNumbers = [];
    private readonly List<Entity> _markers = [];

    public void Dispose()
    {
        Clear();
    }

    public void Show(
        Document document,
        IReadOnlyList<Point3d> controlPoints,
        IReadOnlyList<PipePlanSegmentDivider> dividers,
        IReadOnlyList<PipePlanArcDimension> arcDimensions,
        double pipeWidth)
    {
        Clear();
        if (controlPoints.Count < 2)
        {
            return;
        }

        double markerSize = GetMarkerSize(document);
        Autodesk.AutoCAD.Colors.Color vertexColor = Autodesk.AutoCAD.Colors.Color.FromRgb(255, 170, 0);
        Autodesk.AutoCAD.Colors.Color segmentColor = Autodesk.AutoCAD.Colors.Color.FromRgb(255, 210, 60);
        AddSegmentDividers(dividers, markerSize, pipeWidth);
        AddArcDimensions(arcDimensions, markerSize, pipeWidth, document);

        foreach (Point3d point in controlPoints)
        {
            Circle circle = new(point, Vector3d.ZAxis, markerSize * 0.35)
            {
                Color = vertexColor
            };
            _markers.Add(circle);
        }

        for (int index = 0; index < controlPoints.Count - 1; index++)
        {
            Point3d midpoint = new(
                (controlPoints[index].X + controlPoints[index + 1].X) / 2.0,
                (controlPoints[index].Y + controlPoints[index + 1].Y) / 2.0,
                (controlPoints[index].Z + controlPoints[index + 1].Z) / 2.0);

            Autodesk.AutoCAD.DatabaseServices.Polyline square = new();
            square.AddVertexAt(0, new Point2d(midpoint.X - markerSize * 0.25, midpoint.Y - markerSize * 0.25), 0.0, 0.0, 0.0);
            square.AddVertexAt(1, new Point2d(midpoint.X + markerSize * 0.25, midpoint.Y - markerSize * 0.25), 0.0, 0.0, 0.0);
            square.AddVertexAt(2, new Point2d(midpoint.X + markerSize * 0.25, midpoint.Y + markerSize * 0.25), 0.0, 0.0, 0.0);
            square.AddVertexAt(3, new Point2d(midpoint.X - markerSize * 0.25, midpoint.Y + markerSize * 0.25), 0.0, 0.0, 0.0);
            square.Closed = true;
            square.Color = segmentColor;
            _markers.Add(square);
        }

        foreach (Entity marker in _markers)
        {
            TransientManager.CurrentTransientManager.AddTransient(
                marker,
                TransientDrawingMode.DirectShortTerm,
                129,
                _viewportNumbers);
        }
    }

    // Perpendicular white ticks at straight<->arc junctions. Sized to overshoot the pipe
    // width a little so each reads as a divider crossing the pipe; floored to a view-scaled
    // length so it stays visible on thin (or zero-width) pipes.
    private void AddSegmentDividers(IReadOnlyList<PipePlanSegmentDivider> dividers, double markerSize, double pipeWidth)
    {
        if (dividers.Count == 0)
        {
            return;
        }

        Autodesk.AutoCAD.Colors.Color dividerColor = Autodesk.AutoCAD.Colors.Color.FromRgb(255, 255, 255);
        double halfLength = Math.Max(markerSize, pipeWidth * 0.75);

        foreach (PipePlanSegmentDivider divider in dividers)
        {
            if (divider.Tangent.Length < 1e-9)
            {
                continue;
            }

            Vector2d tangent = divider.Tangent.GetNormal();
            Vector2d perpendicular = new(-tangent.Y, tangent.X);
            Point3d end1 = new(
                divider.Center.X + (perpendicular.X * halfLength),
                divider.Center.Y + (perpendicular.Y * halfLength),
                divider.Center.Z);
            Point3d end2 = new(
                divider.Center.X - (perpendicular.X * halfLength),
                divider.Center.Y - (perpendicular.Y * halfLength),
                divider.Center.Z);

            Line tick = new(end1, end2)
            {
                Color = dividerColor,
                LineWeight = LineWeight.LineWeight050
            };
            _markers.Add(tick);
        }
    }

    // DIMARC-style radius annotation per arc: a concentric arc offset a CONSTANT distance
    // out from the pipe arc (independent of the — possibly huge — radius, so it never flies
    // off), radial extension lines joining the two arcs' endpoints, and the "R=" label at
    // the mid, pushed just outside the offset arc.
    private void AddArcDimensions(
        IReadOnlyList<PipePlanArcDimension> dimensions,
        double markerSize,
        double pipeWidth,
        Document document)
    {
        if (dimensions.Count == 0)
        {
            return;
        }

        double textHeight = GetTextHeight(document);
        double offset = Math.Max(markerSize * 1.6, pipeWidth * 1.1);
        Autodesk.AutoCAD.Colors.Color color = Autodesk.AutoCAD.Colors.Color.FromRgb(0, 170, 70);

        foreach (PipePlanArcDimension dimension in dimensions)
        {
            Vector3d toStart = dimension.Start - dimension.Center;
            Vector3d toEnd = dimension.End - dimension.Center;
            if (toStart.Length < 1e-9 || toEnd.Length < 1e-9)
            {
                continue;
            }

            Vector3d unitStart = toStart.GetNormal();
            Vector3d unitEnd = toEnd.GetNormal();
            double dimensionRadius = dimension.Radius + offset;

            // Full (extended) dimension arc as a CCW start->end pair. AutoCAD arcs always sweep
            // CCW, so a CW pipe arc has its endpoints swapped. Both ends overshoot by 0.1 (as
            // arc length) so it reads as a dimension.
            const double extend = 0.1;
            double deltaAngle = extend / dimensionRadius;
            double startAngle = Math.Atan2(unitStart.Y, unitStart.X);
            double endAngle = Math.Atan2(unitEnd.Y, unitEnd.X);
            double arcStart = (dimension.IsCcw ? startAngle : endAngle) - deltaAngle;
            double arcEnd = (dimension.IsCcw ? endAngle : startAngle) + deltaAngle;
            double sweep = NormalizeCcw(arcEnd - arcStart);
            double midAngle = arcStart + (sweep / 2.0);

            string contents = $"R={dimension.Radius.ToString("0.###", CultureInfo.CurrentCulture)}";
            _markers.Add(MakeArc(dimension.Center, dimensionRadius, arcStart, arcEnd, color));

            Point3d extensionStart = dimension.Center + (unitStart * (dimensionRadius + extend));
            Point3d extensionEnd = dimension.Center + (unitEnd * (dimensionRadius + extend));
            _markers.Add(new Line(dimension.Start, extensionStart) { Color = color, LineWeight = LineWeight.LineWeight000 });
            _markers.Add(new Line(dimension.End, extensionEnd) { Color = color, LineWeight = LineWeight.LineWeight000 });

            Vector3d midDirection = new(Math.Cos(midAngle), Math.Sin(midAngle), 0.0);

            // Arrowheads at the measured endpoints, pointing outward along the dimension arc.
            double arrowLength = Math.Max(textHeight * 0.7, markerSize * 0.5);
            Point3d dimensionStart = dimension.Center + (unitStart * dimensionRadius);
            Point3d dimensionEnd = dimension.Center + (unitEnd * dimensionRadius);
            AddArrowhead(dimensionStart, OutwardTangent(unitStart, midDirection), arrowLength, color);
            AddArrowhead(dimensionEnd, OutwardTangent(unitEnd, midDirection), arrowLength, color);

            // "R=" text sitting on top of the arc: its baseline rests on the arc mid, so the
            // text rises above and the (continuous) arc runs under it.
            MText label = new();
            label.SetDatabaseDefaults();
            label.Color = color;
            label.Contents = contents;
            label.TextHeight = textHeight;
            label.Attachment = AttachmentPoint.BottomCenter;
            label.Location = dimension.Center + (midDirection * dimensionRadius);
            _markers.Add(label);
        }
    }

    private static Arc MakeArc(Point3d center, double radius, double startAngle, double endAngle, Autodesk.AutoCAD.Colors.Color color)
    {
        return new Arc(center, radius, startAngle, endAngle)
        {
            Color = color,
            LineWeight = LineWeight.LineWeight000
        };
    }

    private static double NormalizeCcw(double angle)
    {
        double twoPi = 2.0 * Math.PI;
        double result = angle % twoPi;
        return result < 0.0 ? result + twoPi : result;
    }

    private void AddArrowhead(Point3d tip, Vector3d direction, double length, Autodesk.AutoCAD.Colors.Color color)
    {
        if (direction.Length < 1e-9)
        {
            return;
        }

        Vector3d unit = direction.GetNormal();
        Vector3d perpendicular = new(-unit.Y, unit.X, 0.0);
        double halfWidth = length * 0.32;
        Point3d back = tip - (unit * length);
        Point3d corner1 = back + (perpendicular * halfWidth);
        Point3d corner2 = back - (perpendicular * halfWidth);

        // Solid fills c1->c2->tip as a triangle (p4 == p3 collapses the quad).
        Solid arrow = new(corner1, corner2, tip, tip) { Color = color };
        _markers.Add(arrow);
    }

    // Dimension-arc tangent at a radial endpoint, oriented to point away from the mid (i.e.
    // outward toward that end's extension line).
    private static Vector3d OutwardTangent(Vector3d unitRadial, Vector3d midDirection)
    {
        Vector3d tangent = new(-unitRadial.Y, unitRadial.X, 0.0);
        return tangent.DotProduct(midDirection) < 0.0 ? tangent : tangent.Negate();
    }

    private static double GetTextHeight(Document document)
    {
        try
        {
            using ViewTableRecord view = document.Editor.GetCurrentView();
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
            // Fall through to the default label text height.
        }

        return 1.0;
    }

    public void Clear()
    {
        if (_markers.Count == 0)
        {
            return;
        }

        foreach (Entity marker in _markers)
        {
            try
            {
                TransientManager.CurrentTransientManager.EraseTransient(marker, _viewportNumbers);
            }
            catch
            {
                // Best effort cleanup for transient marker entities.
            }

            marker.Dispose();
        }

        _markers.Clear();
    }

    public double GetPickTolerance(Document document)
    {
        return GetMarkerSize(document) * 0.9;
    }

    private static double GetMarkerSize(Document document)
    {
        using ViewTableRecord view = document.Editor.GetCurrentView();
        return Math.Clamp(view.Height / 90.0, 0.2, 4.0);
    }
}
