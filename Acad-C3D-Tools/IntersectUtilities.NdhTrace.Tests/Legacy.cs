using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.NdhTrace.Tests;

/// <summary>
/// A legacy drawing written out by hand: the centreline, the identity spans
/// along it and the blocks that make its sharp corners - the three things
/// <see cref="NdhRouteBuilder.Build"/> is given.
/// </summary>
internal static class Legacy
{
    /// <summary>A centreline of straight segments through <paramref name="points"/>.</summary>
    public static Polyline Centreline(params (double X, double Y)[] points)
    {
        Polyline pl = new Polyline();
        for (int i = 0; i < points.Length; i++)
            pl.AddVertexAt(i, new Point2d(points[i].X, points[i].Y), 0.0, 0.0, 0.0);
        return pl;
    }

    /// <summary>
    /// One stretch of constant identity. <paramref name="changeDist"/> is where
    /// the change INTO it stands - the centre of the legacy part making it -
    /// and defaults to where the stretch starts, which is the usual case.
    /// </summary>
    public static LegacyIdentitySpan Span(
        double startDist, double endDist, PipeTypeEnum type, int dn,
        double? changeDist = null, PipeSystemEnum system = PipeSystemEnum.Stål,
        bool holdsPipeOrPart = true) =>
        new LegacyIdentitySpan(
            startDist, endDist, system, type, dn, PipeSeriesEnum.S3,
            changeDist ?? startDist, holdsPipeOrPart);

    /// <summary>
    /// A block that puts a sharp corner into the centreline near
    /// <paramref name="at"/>.
    /// <para>
    /// NominalTurn IS NaN ON PURPOSE - "a part made to any angle". A stated
    /// angle would hand the corner to <c>SnapFittingAngles</c>, whose
    /// Gauss-Newton projection moves vertices; these fixtures are about what
    /// happens to a route AFTER its corners are settled, so they are drawn at
    /// the angle they mean and the snapper is kept out of them. What the
    /// snapper itself does is not covered here.
    /// </para>
    /// </summary>
    public static LegacyCorner Corner(
        (double X, double Y) at, LegacyCornerKind kind, string handle) =>
        new LegacyCorner(
            new Point2d(at.X, at.Y), kind, double.NaN, handle, new NoLegsStated());

    public static LegacyCorner Elbow((double X, double Y) at, string handle = "ELBOW") =>
        Corner(at, LegacyCornerKind.Elbow, handle);

    public static LegacyCorner FModel((double X, double Y) at, string handle = "FMODEL") =>
        Corner(at, LegacyCornerKind.FModel, handle);

    public static NdhRoute Route(
        Polyline centreline, IEnumerable<LegacyIdentitySpan> spans, INdhPartStraight straight,
        IEnumerable<LegacyCorner>? corners = null,
        IEnumerable<NdhJunctionSeat>? junctions = null)
    {
        using LegacyPipelineTrace trace = new LegacyPipelineTrace(
            "fixture", centreline, spans.ToList(),
            (corners ?? Enumerable.Empty<LegacyCorner>()).ToList());
        return NdhRouteBuilder.Build(
            trace, straight,
            (junctions ?? Enumerable.Empty<NdhJunctionSeat>()).ToList());
    }

    /// <summary>The route vertex a boundary was laid on.</summary>
    public static NdhRouteVertex VertexOf(this NdhRoute route, NdhIdentityBoundary b) =>
        route.Vertices[b.VertexIndex];

    public static NdhIdentityBoundary BoundaryOf(this NdhRoute route, LegacyIdentitySpan span) =>
        route.Boundaries.Single(b =>
            (b.System, b.Type == PipeTypeEnum.Twin, b.Dn) == span.Identity);

    /// <summary>A readable dump, so a failing fixture says what it actually built.</summary>
    public static string Describe(this NdhRoute route) =>
        string.Join(Environment.NewLine,
            route.Vertices.Select((v, i) =>
                $"  v{i}: ({v.X:F3}, {v.Y:F3}) R={v.BendRadius:F4}")
            .Concat(route.Boundaries.Select(b =>
                $"  boundary v{b.VertexIndex}: {b.System} {b.Type} DN{b.Dn}"))
            .Concat(route.Adjustments.Select(a => "  note: " + a)));
}
