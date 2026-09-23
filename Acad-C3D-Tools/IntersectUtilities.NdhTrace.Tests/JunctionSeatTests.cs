using System;
using System.Linq;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon.Enums;

using Xunit;

namespace IntersectUtilities.NdhTrace.Tests;

/// <summary>
/// A junction's seat is the stretch NDH says the connection takes out of the
/// main, laid about the branch point - and nothing of it is this file's own.
/// </summary>
public sealed class JunctionSeatTests
{
    private static string[] Notes(NdhRoute route, string about) =>
        route.Adjustments.Where(a => a.Contains(about, StringComparison.Ordinal)).ToArray();

    /// <summary>
    /// THE SEAT IS THE ANSWER. A connection that takes a metre either way
    /// seats itself on 49-51 m - not on a legacy block's ports, and not on
    /// those ports widened by a guess.
    /// </summary>
    [Fact]
    public void TheSeatIsTheStretchTheConnectionSaysItTakes()
    {
        using Polyline cl = Legacy.Centreline((0, 0), (50, 0), (100, 0.436));
        RecordingStraight straight = RecordingStraight.Reaching(1.0, bendSetback: 0.2);

        NdhRoute route = Legacy.Route(
            cl, new[] { Legacy.Span(0.0, 100.0, PipeTypeEnum.Twin, 65) }, straight,
            junctions: new[] { new NdhJunctionSeat(new Point2d(50, 0), new JunctionStraight(1.0, 1.0)) });

        Assert.Single(Notes(route, "branch junction at 49"));
        Assert.Matches(@"branch junction at 49[.,]00-51[.,]00 m", Notes(route, "branch junction")[0]);
    }

    /// <summary>
    /// A BEND MOVED OUT OF A SEAT GOES AS FAR AS ITS OWN ARC NEEDS. Its arc must
    /// end where the seat begins, so it stands past the edge by the tangent
    /// setback of the tightest bend that pipe allows - asked of the pipe, never
    /// a half metre picked for every bend alike.
    /// </summary>
    [Fact]
    public void ABendMovedOutOfASeatStandsPastItsEdgeByItsOwnSetback()
    {
        using Polyline cl = Legacy.Centreline((0, 0), (50, 0), (100, 0.436));
        RecordingStraight straight = RecordingStraight.Reaching(1.0, bendSetback: 0.2);

        NdhRoute route = Legacy.Route(
            cl, new[] { Legacy.Span(0.0, 100.0, PipeTypeEnum.Twin, 65) }, straight,
            junctions: new[] { new NdhJunctionSeat(new Point2d(50, 0), new JunctionStraight(1.0, 1.0)) });

        //One metre to the seat's edge, and 0.2 m of arc beyond it.
        Assert.Matches(@"moved 1[.,]20 m out of the branch junction", Notes(route, "moved")[0]);
    }
}
