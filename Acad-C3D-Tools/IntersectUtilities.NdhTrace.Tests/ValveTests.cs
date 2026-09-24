using System;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.NdhTrace.Tests;

/// <summary>
/// A LEGACY VALVE BECOMES AN AUTHORED VALVE: a vertex of its own on its
/// straight, which no pass moves or drops, and - for a bonded pair - the
/// midpoint of its two blocks with each carrier's remainder as its stagger.
/// A valve with no straight to stand on is not carried across and says why.
/// </summary>
public class ValveTests
{
    /// <summary>
    /// THE VALVE VERTEX SURVIVES EVERY PASS. The route here has a change of
    /// pipe (a straight vertex inserted, and the kind of vertex the passes are
    /// allowed to drop) and an elbow; the valve still stands where the block
    /// does, turns nothing, and is not mistaken for the boundary beside it.
    /// </summary>
    [Fact]
    public void AValveStandsOnAVertexOfItsOwnThatTurnsNothing()
    {
        using Polyline cl = Legacy.Centreline((0, 0), (100, 0), (100, -40));
        LegacyIdentitySpan dn80 = Legacy.Span(0, 50, PipeTypeEnum.Twin, 80);
        LegacyIdentitySpan dn65 = Legacy.Span(50, 140, PipeTypeEnum.Twin, 65);
        RecordingStraight straight = RecordingStraight.Reaching(partLeg: 5.0, changeReach: 0.5);

        NdhRoute route = Legacy.Route(
            cl, new[] { dn80, dn65 }, straight, new[] { Legacy.Elbow((100, 0)) },
            valves: new[] { Legacy.Valve(Legacy.ValveBlock((30, 0))) });

        NdhRouteValve valve = Assert.Single(route.Valves);
        NdhRouteVertex at = route.Vertices[valve.VertexIndex];
        Assert.Equal(30.0, at.X, 9);
        Assert.Equal(0.0, at.Y, 9);
        Assert.Equal(0.0, at.BendRadius);
        //Collinear: both neighbours lie on the same straight.
        Assert.Equal(0.0, route.Vertices[valve.VertexIndex - 1].Y, 9);
        Assert.Equal(0.0, route.Vertices[valve.VertexIndex + 1].Y, 9);
        Assert.DoesNotContain(route.Boundaries, b => b.VertexIndex == valve.VertexIndex);
        Assert.Equal(50.0, route.VertexOf(route.BoundaryOf(dn65)).X, 6);
        //A twin valve has no stagger, and names the one run it stands on.
        Assert.Equal(0.0, valve.FremStaggerM);
        Assert.Equal(0.0, valve.ReturStaggerM);
        Assert.Equal(new[] { new NdhValveName(NdhRun.Twin, "Ventil") }, valve.Names);
        Assert.Empty(route.LostValves);
    }

    /// <summary>
    /// A BONDED PAIR IS ONE VALVE: its vertex stands midway between the two
    /// blocks, measured along the centreline, and each carrier keeps its own
    /// block's station minus that midpoint as its stagger - the split NDH's
    /// own valve drag commits.
    /// </summary>
    [Fact]
    public void ABondedPairStandsOnItsMidpointWithEachCarriersRemainder()
    {
        using Polyline cl = Legacy.Centreline((0, 0), (100, 0));
        RecordingStraight straight = RecordingStraight.Reaching(partLeg: 5.0);

        NdhRoute route = Legacy.Route(
            cl, new[] { Legacy.Span(0, 100, PipeTypeEnum.Enkelt, 100) }, straight,
            valves: new[]
            {
                Legacy.Valve(
                    Legacy.ValveBlock((29.0, 0), NdhRun.Frem, "Engangsventil", "F"),
                    Legacy.ValveBlock((31.5, 0), NdhRun.Retur, "Engangsventil", "R")),
            });

        NdhRouteValve valve = Assert.Single(route.Valves);
        Assert.Equal(30.25, route.Vertices[valve.VertexIndex].X, 9);
        Assert.Equal(-1.25, valve.FremStaggerM, 9);
        Assert.Equal(1.25, valve.ReturStaggerM, 9);
        Assert.Equal(
            new[]
            {
                new NdhValveName(NdhRun.Frem, "Engangsventil"),
                new NdhValveName(NdhRun.Retur, "Engangsventil"),
            },
            valve.Names);
        Assert.Empty(route.ValveNotes);
    }

    /// <summary>
    /// A BONDED BLOCK WITH NO PARTNER is still a valve, with no stagger - NDH
    /// stands one on both carriers - and the drafter is told the old drawing
    /// showed only one.
    /// </summary>
    [Fact]
    public void ALoneBondedBlockIsAValveOnBothCarriersAndSaysSo()
    {
        using Polyline cl = Legacy.Centreline((0, 0), (100, 0));
        RecordingStraight straight = RecordingStraight.Reaching(partLeg: 5.0);

        NdhRoute route = Legacy.Route(
            cl, new[] { Legacy.Span(0, 100, PipeTypeEnum.Enkelt, 100) }, straight,
            valves: new[] { Legacy.Valve(Legacy.ValveBlock((40, 0), NdhRun.Retur)) });

        NdhRouteValve valve = Assert.Single(route.Valves);
        Assert.Equal(0.0, valve.FremStaggerM);
        Assert.Equal(0.0, valve.ReturStaggerM);
        Assert.Equal(new[] { new NdhValveName(NdhRun.Retur, "Ventil") }, valve.Names);
        Assert.Contains(route.ValveNotes, n => n.Contains("begge rør"));
    }

    /// <summary>
    /// A VALVE IN AN ARC IS NOT CARRIED ACROSS. The legacy drawing curves
    /// there, and the import makes no straight for a valve: it is reported,
    /// and the valve on the straight before the arc is carried as usual.
    /// </summary>
    [Fact]
    public void AValveInALegacyArcIsLostWithANote()
    {
        //A quarter circle of R = 20 from (50, 0) to (70, 20), turning left.
        using Polyline cl = new Polyline();
        cl.AddVertexAt(0, new Point2d(0, 0), 0.0, 0.0, 0.0);
        cl.AddVertexAt(1, new Point2d(50, 0), Math.Tan(Math.PI / 8.0), 0.0, 0.0);
        cl.AddVertexAt(2, new Point2d(70, 20), 0.0, 0.0, 0.0);
        cl.AddVertexAt(3, new Point2d(70, 100), 0.0, 0.0, 0.0);
        //Halfway round the arc.
        (double X, double Y) inTheArc =
            (50 + 20 * Math.Sin(Math.PI / 4.0), 20 - 20 * Math.Cos(Math.PI / 4.0));
        RecordingStraight straight = RecordingStraight.Reaching(partLeg: 5.0);

        NdhRoute route = Legacy.Route(
            cl, new[] { Legacy.Span(0, cl.Length, PipeTypeEnum.Twin, 80) }, straight,
            valves: new[]
            {
                Legacy.Valve(Legacy.ValveBlock((20, 0), handle: "ON-STRAIGHT")),
                Legacy.Valve(Legacy.ValveBlock(inTheArc, handle: "IN-ARC")),
            });

        NdhRouteValve valve = Assert.Single(route.Valves);
        Assert.Equal(20.0, route.Vertices[valve.VertexIndex].X, 9);
        LostValve lost = Assert.Single(route.LostValves);
        Assert.Contains("IN-ARC", lost.Reason);
        Assert.Contains("står i en bue", lost.Reason);
        Assert.Equal(inTheArc.X, lost.At.X, 6);
    }

    /// <summary>
    /// AN ELASTIC BEND DOES NOT REACH ACROSS A VALVE. Rounding a 2 degree kink
    /// would set its arc back 5.7 m, over a valve drafted 2 m before it; the
    /// arc is held to the 2 m, and the valve stays on its straight.
    /// </summary>
    [Fact]
    public void AnElasticBendIsHeldOffAValveOnItsStraight()
    {
        double turn = 2.0 * Math.PI / 180.0;
        using Polyline cl = Legacy.Centreline(
            (0, 0), (50, 0), (50 + 50 * Math.Cos(turn), 50 * Math.Sin(turn)));
        RecordingStraight straight = RecordingStraight.Reaching(partLeg: 5.0);

        NdhRoute route = Legacy.Route(
            cl, new[] { Legacy.Span(0, 100, PipeTypeEnum.Twin, 80) }, straight,
            valves: new[] { Legacy.Valve(Legacy.ValveBlock((48, 0))) });

        NdhRouteValve valve = Assert.Single(route.Valves);
        Assert.Equal(48.0, route.Vertices[valve.VertexIndex].X, 9);
        NdhRouteVertex bend = route.Vertices[valve.VertexIndex + 1];
        Assert.True(bend.BendRadius > 0.0, route.Describe());
        Assert.True(bend.BendRadius * Math.Tan(turn / 2.0) <= 2.0, route.Describe());
    }

    /// <summary>
    /// WHICH BLOCKS ARE ONE VALVE: a Frem and a Retur block pair NEAREST FIRST,
    /// so the valves either side of a tee pair across the carriers on each
    /// side, not across the tee. A block with no partner in reach is a valve
    /// on its own, and so is every twin block.
    /// </summary>
    [Fact]
    public void BondedBlocksPairNearestFirstAndTheRestStandAlone()
    {
        LegacyValveBlock f10 = Legacy.ValveBlock((10, 0), NdhRun.Frem, handle: "F10");
        LegacyValveBlock r11 = Legacy.ValveBlock((11, 0), NdhRun.Retur, handle: "R11");
        LegacyValveBlock f13 = Legacy.ValveBlock((13.5, 0), NdhRun.Frem, handle: "F13");
        LegacyValveBlock r14 = Legacy.ValveBlock((14, 0), NdhRun.Retur, handle: "R14");
        LegacyValveBlock f80 = Legacy.ValveBlock((80, 0), NdhRun.Frem, handle: "F80");
        LegacyValveBlock t50 = Legacy.ValveBlock((50, 0), NdhRun.Twin, handle: "T50");

        var valves = LegacyValvePairing.Pair(new[]
        {
            (f10, 10.0), (r11, 11.0), (f13, 13.5), (r14, 14.0), (f80, 80.0), (t50, 50.0),
        });

        Assert.Equal(
            new[] { "F10, R11", "F13, R14", "T50", "F80" },
            valves.ConvertAll(v => v.Handles));
    }

    /// <summary>
    /// THE BRIDGE'S MIRRORS MARSHAL TO THE HEADER'S SIZES. Every mirror is
    /// checked in the bridge's static constructor, the authored valve's 24
    /// bytes among them; a drift throws there, before any call.
    /// </summary>
    [Fact]
    public void TheBridgeMirrorsMatchTheHeader()
    {
        Assert.Null(Record.Exception(() => new NsDhPipelineBridge()));
    }
}
