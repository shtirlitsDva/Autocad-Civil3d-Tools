using System;

using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.NdhTrace.Tests;

/// <summary>
/// What an elastic bend is given for a radius, and what it is allowed to read
/// off its neighbours to work that out. Law R1 again: a neighbour's setback is
/// READ FROM THAT NEIGHBOUR, and the only thing this file may make up is the
/// even split between two bends that have no radius yet.
/// </summary>
public class SizeBendsTests
{
    private const double Tan10 = 0.17632698070846498;
    //An arc of radius R bulges R(1/cos(turn/2) - 1) inside a 20 degree kink, so
    //the 5 cm the route may stray from the trace caps every radius here at:
    private const double DeviationCap = 3.2412;

    private static (double X, double Y) Step((double X, double Y) p, double headingDeg, double length) =>
        (p.X + length * Math.Cos(headingDeg * Math.PI / 180.0),
         p.Y + length * Math.Sin(headingDeg * Math.PI / 180.0));

    /// <summary>
    /// THE PIPE ON A VERTEX IS READ FROM THE BOUNDARIES AS LAID. Placing a
    /// change MOVES it - here off the corner it was drafted on, 5 m back down
    /// the straight - and nothing writes that new station back onto the span.
    /// So the span's own StartDist is the legacy DRAFT, and a vertex between the
    /// drafted and the laid position stands on a different pipe than the draft
    /// says: the corner here is drafted as DN80 and laid as DN65.
    /// </summary>
    [Fact]
    public void AVertexIsAskedAboutThePipeTheLaidBoundariesPutOnIt()
    {
        using Polyline cl = Legacy.Centreline((0, 0), (100, 0), (100, -40));
        LegacyIdentitySpan dn80 = Legacy.Span(0, 100.5, PipeTypeEnum.Twin, 80);
        //Drafted with its part centred ON the corner, and the pipe itself
        //starting half a metre past it.
        LegacyIdentitySpan dn65 = Legacy.Span(100.5, 140, PipeTypeEnum.Twin, 65, changeDist: 100.0);
        RecordingStraight straight = RecordingStraight.Reaching(partLeg: 5.0);

        NdhRoute route = Legacy.Route(
            cl, new[] { dn80, dn65 }, straight, new[] { Legacy.Elbow((100, 0)) });

        Assert.Contains(route.Adjustments, a => a.Contains("falls on a corner"));
        Assert.Equal(95.0, route.VertexOf(route.BoundaryOf(dn65)).X, 6);

        int corner = route.Vertices
            .Select((v, i) => (v, i))
            .Single(t => t.v.X == 100.0 && t.v.Y == 0.0).i;
        Ask onTheCorner = straight.PerVertex(route.Vertices.Count)[corner];
        Assert.Equal(dn65.Identity, onTheCorner.BeforeId);
        Assert.Equal(dn65.Identity, onTheCorner.AfterId);
    }

    /// <summary>
    /// TWO BENDS WITH NO RADIUS YET SHARE THEIR LEG EVENLY, and the second one
    /// then reads the first one's REAL setback rather than taking the half share
    /// all over again. The sizing walk runs ascending, so by the time the second
    /// bend is reached the first has a settled radius standing in its leg - and
    /// reading the KIND instead of whether the radius is settled throws that
    /// setback away and invents a length beside one already computed.
    /// <para>
    /// The two kinks turn opposite ways, which is a 1 m jog in the route and
    /// also keeps the arc fitter out of them (it has nothing to fit: the
    /// straights either side are parallel).
    /// </para>
    /// </summary>
    [Fact]
    public void TheSecondOfTwoBendsReadsTheFirstsSettledSetbackNotHalfTheLeg()
    {
        var p0 = (0.0, 0.0);
        var p1 = Step(p0, 0, 60);
        var p2 = Step(p1, 20, 1.0);
        var p3 = Step(p2, 0, 60);
        using Polyline cl = Legacy.Centreline(p0, p1, p2, p3);
        RecordingStraight straight =
            RecordingStraight.Reaching(partLeg: 5.0, bendSetback: 0.1, changeReach: 0.1);

        NdhRoute route = Legacy.Route(
            cl, new[] { Legacy.Span(0, 121, PipeTypeEnum.Twin, 80) }, straight);

        Assert.Equal(4, route.Vertices.Count);
        //Neither radius exists yet when the first is sized, so it gets half the
        //1 m leg - and half a metre of setback is well inside the 3.24 m the
        //trace tolerance would allow, so the leg is what decides it.
        Assert.Equal(0.5 / Tan10, route.Vertices[1].BendRadius, 4);
        Assert.True(route.Vertices[1].BendRadius < DeviationCap);
        //The second reads what the first actually took: its 0.5 m setback plus
        //the 0.1 m the bent pipe itself reaches, leaving 0.4 m of the leg.
        Assert.Equal((1.0 - 0.6) / Tan10, route.Vertices[2].BendRadius, 3);
    }

    /// <summary>
    /// A BEND LEFT SHARP IS NOT A BEND WITH A RADIUS. It gets none and never
    /// will - and a radius of nought is how this route says ELBOW - so NDH will
    /// stand a part on it whose legs were never asked for and never reserved.
    /// Marking it settled would hand the next bend the whole leg, right up to
    /// where that unasked-for part is going to stand.
    /// </summary>
    [Fact]
    public void ABendLeftSharpStillSharesItsLegWithTheNextBend()
    {
        var p0 = (0.0, 0.0);
        var p1 = Step(p0, 0, 30);
        var p2 = Step(p1, -90, 5.0);
        var p3 = Step(p2, -70, 1.0);
        var p4 = Step(p3, -90, 60);
        using Polyline cl = Legacy.Centreline(p0, p1, p2, p3, p4);
        //The elbow's legs are exactly the 5 m to the first bend, so that bend
        //has no leg room at all.
        RecordingStraight straight =
            RecordingStraight.Reaching(partLeg: 5.0, bendSetback: 0.1, changeReach: 0.1);

        NdhRoute route = Legacy.Route(
            cl, new[] { Legacy.Span(0, 96, PipeTypeEnum.Twin, 80) }, straight,
            new[] { Legacy.Elbow(p1) });

        Assert.Equal(5, route.Vertices.Count);
        Assert.Contains(route.Adjustments, a => a.Contains("no leg room, left sharp"));
        Assert.Equal(0.0, route.Vertices[2].BendRadius);
        //Still an even share of the 1 m leg, not the 0.9 m a settled neighbour
        //would have yielded (which the trace tolerance would then have capped
        //at 3.24 m).
        Assert.Equal(0.5 / Tan10, route.Vertices[3].BendRadius, 4);
        Assert.True(route.Vertices[3].BendRadius < DeviationCap - 0.1);
    }
}
