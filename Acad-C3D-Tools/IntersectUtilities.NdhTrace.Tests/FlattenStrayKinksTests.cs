using System;
using System.Linq;

using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon.Enums;

using Xunit;

namespace IntersectUtilities.NdhTrace.Tests;

/// <summary>
/// A kink nothing can round is taken out before anything is built on it - and
/// nothing can round a kink standing against a part, because the part's leg
/// claims the stretch up to it.
/// </summary>
public sealed class FlattenStrayKinksTests
{
    private static string[] Flattenings(NdhRoute route) =>
        route.Adjustments.Where(a => a.StartsWith("kink at", StringComparison.Ordinal)).ToArray();

    /// <summary>
    /// THE DEFECT ITSELF, as pipeline 008 draws it: a 90 degree elbow, and a
    /// stone's throw further on a corner the drawing barely turns. The elbow
    /// reserves its leg, the kink gets none of it, and a corner nothing can
    /// round gets no radius - which is how this route says ELBOW, on a corner
    /// no elbow is made for. So the kink goes.
    /// </summary>
    [Fact]
    public void AKinkWithNoRoomToBendIsTakenOutAndTheRouteRunsStraightThrough()
    {
        using Polyline cl = Legacy.Centreline((0, 0), (0, 40), (50, 40.03), (150, 40));
        RecordingStraight straight = RecordingStraight.Reaching(1.0, 0.01);

        NdhRoute route = Legacy.Route(
            cl, new[] { Legacy.Span(0.0, 190.0, PipeTypeEnum.Twin, 65) }, straight,
            corners: new[] { Legacy.Elbow((0, 40)) });

        //Four drawn vertices, the kink taken out.
        Assert.Equal(3, route.Vertices.Count);
        Assert.Single(Flattenings(route));
        //THE SEPARATOR IS THE MACHINE'S, not this file's - the note is written
        //for a drafter and formats in their culture.
        Assert.Matches(@"0[.,]030 m from the trace", Flattenings(route)[0]);
    }

    /// <summary>
    /// A KINK OUT ON OPEN PIPE IS ROUNDED, not straightened. Rounding is the
    /// faithful answer and the import moves no geometry it does not have to -
    /// pipeline 008 wobbles at every vertex for 300 m and every one of those
    /// kinks is rounded without complaint.
    /// </summary>
    [Fact]
    public void AKinkWithRoomToBendIsLeftForTheArcFitter()
    {
        using Polyline cl = Legacy.Centreline(
            (0, 0), (50, 0.03), (100, 0), (150, 0.03), (200, 0));
        RecordingStraight straight = RecordingStraight.Reaching(1.0, 0.01);

        NdhRoute route = Legacy.Route(
            cl, new[] { Legacy.Span(0.0, 200.0, PipeTypeEnum.Twin, 65) }, straight);

        Assert.Empty(Flattenings(route));
    }

    /// <summary>
    /// AGAINST A PART IS NOT ENOUGH. A corner can turn far too little for any
    /// fitting and still stand well away from the line between its neighbours -
    /// over long legs a fraction of a degree is metres. Straightening that is
    /// not repairing the drawing, it is redrawing it.
    /// </summary>
    [Fact]
    public void AKinkTheRouteCannotRunStraightThroughIsLeftWhereItWasDrawn()
    {
        using Polyline cl = Legacy.Centreline((0, 0), (0, 40), (50, 40.5), (150, 40));
        RecordingStraight straight = RecordingStraight.Reaching(1.0, 0.01);

        NdhRoute route = Legacy.Route(
            cl, new[] { Legacy.Span(0.0, 190.0, PipeTypeEnum.Twin, 65) }, straight,
            corners: new[] { Legacy.Elbow((0, 40)) });

        Assert.Empty(Flattenings(route));
        Assert.Equal(4, route.Vertices.Count);
    }

    /// <summary>
    /// A CORNER THE DRAWING MEANT stays, however small it looks beside a long
    /// run. Five degrees is where this file already draws that line - under it
    /// no block is even looked for.
    /// </summary>
    [Fact]
    public void ACornerDeepEnoughToCarryAFittingIsNeverFlattened()
    {
        using Polyline cl = Legacy.Centreline((0, 0), (0, 40), (50, 40), (100, 60));
        RecordingStraight straight = RecordingStraight.Reaching(1.0, 0.01);

        NdhRoute route = Legacy.Route(
            cl, new[] { Legacy.Span(0.0, 143.9, PipeTypeEnum.Twin, 65) }, straight,
            corners: new[] { Legacy.Elbow((0, 40)) });

        Assert.Empty(Flattenings(route));
        Assert.Equal(4, route.Vertices.Count);
    }

}
