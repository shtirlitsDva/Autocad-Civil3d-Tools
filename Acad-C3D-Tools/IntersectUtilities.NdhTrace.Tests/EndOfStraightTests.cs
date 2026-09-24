using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.NdhTrace.Tests;

/// <summary>
/// THE CONTACT RULE MUST AGREE WITH THE REACH. The drawing's shortest pipe is a
/// distance to a PART: a change drafted nearer than that to something standing
/// at a vertex is put in contact with it instead of leaving a sliver nobody can
/// weld. NDH answers that minimum at every vertex - it is a setting, not a fact
/// about the vertex - so the route builder decides for itself where it APPLIES,
/// and a reach is not that test.
/// <para>
/// All three fixtures here are the same drawing with one thing changed, so the
/// station a change is laid at is the whole answer.
/// </para>
/// </summary>
public class EndOfStraightTests
{
    private static readonly LegacyIdentitySpan Twin80 = Legacy.Span(0, 93.5, PipeTypeEnum.Twin, 80);
    private static readonly LegacyIdentitySpan Twin65 = Legacy.Span(93.5, 200, PipeTypeEnum.Twin, 65);

    /// <summary>A part turns the corner, so the reducer 1.5 m short of its 5 m leg is put in contact with it.</summary>
    [Fact]
    public void AChangeTooCloseToAPartThatTurnsTheVertexIsPutInContactWithIt()
    {
        using Polyline cl = Legacy.Centreline((0, 0), (100, 0), (100, -100));
        RecordingStraight straight =
            RecordingStraight.Reaching(partLeg: 5.0, bendSetback: 5.0, minimumPipe: 2.0);

        NdhRoute route = Legacy.Route(
            cl, new[] { Twin80, Twin65 }, straight, new[] { Legacy.Elbow((100, 0)) });

        Assert.Contains(route.Adjustments, a => a.Contains("put in contact with it"));
        Assert.Equal(95.0, route.VertexOf(route.BoundaryOf(Twin65)).X, 6);
    }

    /// <summary>
    /// THE SAME DRAWING WITHOUT THE ELBOW BLOCK. The corner is then an
    /// elastically bent pipe: it REACHES exactly as far - the fake answers the
    /// same 5 m setback - but nothing STANDS there, so there is no weld to leave
    /// a spool of pipe against and the change stays where the drafter drew it.
    /// A reach is not the contact test, and this pair is the only place the
    /// difference shows.
    /// </summary>
    [Fact]
    public void AChangeIsNotPutInContactWithAnElasticallyBentPipe()
    {
        using Polyline cl = Legacy.Centreline((0, 0), (100, 0), (100, -100));
        RecordingStraight straight =
            RecordingStraight.Reaching(partLeg: 5.0, bendSetback: 5.0, minimumPipe: 2.0);

        NdhRoute route = Legacy.Route(cl, new[] { Twin80, Twin65 }, straight);

        Assert.DoesNotContain(route.Adjustments, a => a.Contains("put in contact with it"));
        Assert.Equal(93.5, route.VertexOf(route.BoundaryOf(Twin65)).X, 6);
    }

    /// <summary>
    /// A CHANGE FROM AN IDENTITY TO ITSELF IS NOT ONE. While boundaries are
    /// being placed a vertex can hold A-&gt;A for a moment - here DN65 is dropped
    /// between two stretches of DN80, and the dedup pass only clears that at the
    /// end of the call - and a later change measured against that vertex must
    /// see nothing standing on it. NDH answers nought for such a vertex, so the
    /// contact rule has to agree with the reach or the two read the same vertex
    /// differently.
    /// </summary>
    [Fact]
    public void AChangeIsNotPutInContactWithAVertexWhoseChangeIsToItsOwnIdentity()
    {
        using Polyline cl = Legacy.Centreline((0, 0), (200, 0), (200, -100));
        LegacyIdentitySpan first80 = Legacy.Span(0, 100, PipeTypeEnum.Twin, 80);
        LegacyIdentitySpan dn65 = Legacy.Span(100, 100.5, PipeTypeEnum.Twin, 65);
        LegacyIdentitySpan again80 = Legacy.Span(100.5, 101, PipeTypeEnum.Twin, 80);
        LegacyIdentitySpan dn50 = Legacy.Span(101, 300, PipeTypeEnum.Twin, 50);
        RecordingStraight straight = RecordingStraight.Reaching(partLeg: 5.0, minimumPipe: 2.0);

        NdhRoute route = Legacy.Route(
            cl, new[] { first80, dn65, again80, dn50 }, straight, new[] { Legacy.Elbow((200, 0)) });

        //DN65 is pushed onto the vertex at 100 m and dropped, which leaves that
        //vertex momentarily changing DN80 into DN80.
        Assert.Contains(route.Adjustments, a => a.Contains("DN65") && a.Contains("no length left"));
        //DN50, drafted 1 m on, therefore stays at 101 m: had the minimum pipe
        //been held at that vertex it would have been dragged back onto it.
        Assert.Equal(101.0, route.VertexOf(route.BoundaryOf(dn50)).X, 6);
    }
}
