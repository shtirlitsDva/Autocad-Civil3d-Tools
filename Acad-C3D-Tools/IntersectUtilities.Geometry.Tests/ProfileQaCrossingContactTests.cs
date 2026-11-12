using IntersectUtilities.LongitudinalProfiles.Quality;

public sealed class ProfileQaCrossingContactTests
{
    [Fact]
    public void Cogo782ContactAtArcLineJoinIsNotAnIntrusion()
    {
        // Exact parsed geometry from 003_PV at station 389.1356897, including survey-coordinate roundoff.
        var outer = new QaRegion(new[] { QaEdge.Arc(389.13568970398046, 44.356532510370016,
            0.3800000000046566, 0.3799999998882413, 0, 2 * Math.PI) });
        var arc = QaSegment.Ellipse(388.9145521087339, 389.1339296333026,
            390.5651457090862, 352.973222034052, 308.99999999477205, 308.99999999477205, -1, 22);
        const double x0 = 724920.912725267, y0 = 6190849.211451011;
        var line = QaSegment.Line(new(725310.0466549003 - x0, 31 + 6190862.187987598 - y0),
            new(725414.0343563844 - x0, 31 + 6190861.706335829 - y0), 23);
        Assert.Empty(QaGeometry.Intrusions(arc, outer, arc.Start, arc.End));
        Assert.Empty(QaGeometry.Intrusions(line, outer, line.Start, line.End));
    }

    [Theory]
    [InlineData(0.055, 0.055)]
    [InlineData(0.38, 0.38)]
    [InlineData(2.5, 0.5)]
    [InlineData(0.5, 2.5)]
    public void ContactToleranceUsesMetresForDifferentCircleAndEllipseSizes(double rx, double ry)
    {
        var outer = new QaRegion(new[] { QaEdge.Arc(0, 0, rx, ry, 0, 2 * Math.PI) });
        double tolerance = QaGeometry.CrossingContactTolerance;
        Assert.False(outer.Contains(new(0, -ry + tolerance / 2)));
        Assert.True(outer.Contains(new(0, -ry + tolerance * 2)));
        var contact = QaSegment.Line(new(-2 * rx, -ry + tolerance / 2), new(2 * rx, -ry + tolerance / 2), 1);
        var intrusion = QaSegment.Line(new(-2 * rx, -ry + tolerance * 2), new(2 * rx, -ry + tolerance * 2), 1);
        Assert.Empty(QaGeometry.Intrusions(contact, outer, contact.Start, contact.End));
        Assert.Single(QaGeometry.Intrusions(intrusion, outer, intrusion.Start, intrusion.End));
    }

    [Fact]
    public void RectangleContactUsesTheSameDistanceTolerance()
    {
        var outer = new QaRegion(new[] { QaEdge.Line(new(-1, 0), new(1, 0)), QaEdge.Line(new(1, 0), new(1, 1)),
            QaEdge.Line(new(1, 1), new(-1, 1)), QaEdge.Line(new(-1, 1), new(-1, 0)) });
        Assert.False(outer.Contains(new(0, QaGeometry.CrossingContactTolerance / 2)));
        Assert.True(outer.Contains(new(0, QaGeometry.CrossingContactTolerance * 2)));
    }
}
