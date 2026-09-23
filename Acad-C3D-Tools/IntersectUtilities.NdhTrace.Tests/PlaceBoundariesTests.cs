using System.Linq;

using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.NdhTrace.Tests;

/// <summary>
/// Where an identity change ends up standing, and what the vertex it stands on
/// then says it carries. The second is not decoration: the vertex's change is
/// the whole of what <see cref="INdhPartStraight"/> is asked about it (Law R1),
/// so a vertex marked with a change nobody lays reserves straight for parts
/// that are not there.
/// </summary>
public class PlaceBoundariesTests
{
    /// <summary>
    /// AN F-RØR CORNER IS THE CORNER, NOT ITS INDEX. Every F corner is found
    /// before any change is placed, and placing a change INSERTS a vertex - so
    /// an index taken up front names a different vertex by the time the F
    /// corner's own change is placed.
    /// <para>
    /// Live run 2026-09-19, pipeline 014: one reducer placed at 300 m shifted
    /// the F corner's index by one, and the Twin-&gt;Enkelt change went to 350.81 m
    /// instead of the corner at 527.72 m - 177 metres of pipe filed as the wrong
    /// identity, one spurious FootprintOverlap and two branches refused.
    /// </para>
    /// </summary>
    [Fact]
    public void AnFCornerChangeLandsOnTheCornerThoughAnEarlierChangeInsertedAVertex()
    {
        using Polyline cl = Legacy.Centreline((0, 0), (600, 0), (600, -200));
        LegacyIdentitySpan twin100 = Legacy.Span(0, 300, PipeTypeEnum.Twin, 100);
        LegacyIdentitySpan twin80 = Legacy.Span(300, 600, PipeTypeEnum.Twin, 80);
        LegacyIdentitySpan enkelt80 = Legacy.Span(600, 800, PipeTypeEnum.Enkelt, 80);
        RecordingStraight straight = RecordingStraight.Reaching(partLeg: 2.0, bendSetback: 0.2);

        NdhRoute route = Legacy.Route(
            cl, new[] { twin100, twin80, enkelt80 }, straight, new[] { Legacy.FModel((600, 0)) });

        //The reducer at 300 m is laid first and puts a new vertex in front of
        //the F corner; the F-rør change must still land on the corner itself.
        Assert.Equal(3, route.Boundaries.Count);
        NdhRouteVertex onFCorner = route.VertexOf(route.BoundaryOf(enkelt80));
        Assert.Equal(600.0, onFCorner.X, 6);
        Assert.Equal(0.0, onFCorner.Y, 6);
        Assert.Equal(300.0, route.VertexOf(route.BoundaryOf(twin80)).X, 6);
        Assert.DoesNotContain(route.Adjustments, a => a.Contains("no length left"));
    }

    /// <summary>
    /// THE MARKING NAMES WHAT SURVIVED. Where a change is pushed back onto the
    /// one before it, the earlier one is dropped and the pipe ARRIVING at the
    /// vertex is the one before THAT - so the vertex must ask for the change
    /// that is actually laid (A-&gt;C), never for the drafted predecessor's
    /// (B-&gt;C), which would reserve straight for a chain of parts nobody lays.
    /// </summary>
    [Fact]
    public void ADroppedPredecessorLeavesTheVertexAskingForTheChangeThatIsLaid()
    {
        using Polyline cl = Legacy.Centreline((0, 0), (105, 0), (105, -100));
        LegacyIdentitySpan a = Legacy.Span(0, 100, PipeTypeEnum.Twin, 100);
        LegacyIdentitySpan b = Legacy.Span(100, 101, PipeTypeEnum.Twin, 80);
        LegacyIdentitySpan c = Legacy.Span(101, 205, PipeTypeEnum.Twin, 65);
        //The elbow's 5 m legs leave the 5 m straight between 100 m and the
        //corner with no room, so C is pushed back onto B and B is dropped.
        RecordingStraight straight = RecordingStraight.Reaching(partLeg: 5.0);

        NdhRoute route = Legacy.Route(
            cl, new[] { a, b, c }, straight, new[] { Legacy.Elbow((105, 0)) });

        Assert.Contains(route.Adjustments, x => x.Contains("DN80") && x.Contains("no length left"));
        NdhIdentityBoundary laid = route.BoundaryOf(c);
        Assert.Equal(100.0, route.VertexOf(laid).X, 6);

        Ask onTheVertex = straight.PerVertex(route.Vertices.Count)[laid.VertexIndex];
        Assert.Equal(a.Identity, onTheVertex.BeforeId);
        Assert.Equal(c.Identity, onTheVertex.AfterId);
    }

    /// <summary>
    /// A CHANGE THAT STARTS THE PIPELINE LAYS NOTHING - the pipeline simply
    /// begins as that identity - so the vertex must come out of the build
    /// carrying no change at all. Left marked, it would have the sizing walk
    /// reserve the transition chain's straight out of the first leg, and the
    /// contact rule hold a minimum pipe, for parts nobody lays.
    /// </summary>
    [Fact]
    public void AChangeAtThePipelineStartLeavesNoChangeOnTheFirstVertex()
    {
        using Polyline cl = Legacy.Centreline((0, 0), (100, 0));
        LegacyIdentitySpan opening = Legacy.Span(0, 0.2, PipeTypeEnum.Twin, 100);
        LegacyIdentitySpan actual = Legacy.Span(0.2, 100, PipeTypeEnum.Twin, 80, changeDist: 0.0005);
        RecordingStraight straight = RecordingStraight.Reaching(partLeg: 2.0);

        NdhRoute route = Legacy.Route(cl, new[] { opening, actual }, straight);

        Assert.Contains(route.Adjustments, x => x.Contains("starts the pipeline"));
        NdhIdentityBoundary only = Assert.Single(route.Boundaries);
        Assert.Equal(0, only.VertexIndex);
        Assert.Equal((actual.System, true, actual.Dn), (only.System, only.Type == PipeTypeEnum.Twin, only.Dn));

        Ask onTheFirstVertex = straight.PerVertex(route.Vertices.Count)[0];
        Assert.True(onTheFirstVertex.IsPipeRunningThrough, onTheFirstVertex.ToString());
        Assert.Equal(actual.Identity, onTheFirstVertex.BeforeId);
        //The only DN100 -> DN80 question ever put is the one asked of the open
        //pipe before the change had a vertex; nothing asks it of a vertex.
        Assert.Equal(1, straight.Count(opening, actual));
    }

    /// <summary>
    /// THE F PRE-MARKING IS UNDONE WHERE THE BOUNDARY DID NOT LAND. An F corner
    /// is marked with its change before any change is placed, so that a straight
    /// measured against it already sees it - but the corner is a real corner of
    /// the route and is NOT removed when its boundary is then dropped. Left
    /// marked, it would keep asking for a transition nobody lays.
    /// <para>
    /// Here the DN65 reducer is pushed back off the corner's leg and takes the
    /// F-rør's own change with it, so what stands at the corner afterwards is
    /// plain pipe running through.
    /// </para>
    /// </summary>
    [Fact]
    public void AnFCornerWhoseBoundaryWasDroppedCarriesNoChange()
    {
        using Polyline cl = Legacy.Centreline((0, 0), (100, 0), (100, -100));
        LegacyIdentitySpan twin80 = Legacy.Span(0, 98, PipeTypeEnum.Twin, 80);
        LegacyIdentitySpan enkelt80 = Legacy.Span(98, 99, PipeTypeEnum.Enkelt, 80, changeDist: 98.5);
        LegacyIdentitySpan enkelt65 = Legacy.Span(99, 200, PipeTypeEnum.Enkelt, 65, changeDist: 99);
        RecordingStraight straight = RecordingStraight.Reaching(partLeg: 5.0, minimumPipe: 0.5);

        NdhRoute route = Legacy.Route(
            cl, new[] { twin80, enkelt80, enkelt65 }, straight, new[] { Legacy.FModel((100, 0)) });

        Assert.Contains(route.Adjustments, x => x.Contains("F-rør corner"));
        Assert.Contains(route.Adjustments, x => x.Contains("DN80") && x.Contains("no length left"));

        int fCorner = route.Vertices
            .Select((v, i) => (v, i))
            .Single(t => t.v.X == 100.0 && t.v.Y == 0.0).i;
        Assert.DoesNotContain(route.Boundaries, b => b.VertexIndex == fCorner);

        Ask onTheCorner = straight.PerVertex(route.Vertices.Count)[fCorner];
        Assert.True(onTheCorner.IsPipeRunningThrough, onTheCorner.ToString());
        Assert.Equal(enkelt65.Identity, onTheCorner.BeforeId);
        Assert.True(onTheCorner.TurnedByAPart);
    }
}
