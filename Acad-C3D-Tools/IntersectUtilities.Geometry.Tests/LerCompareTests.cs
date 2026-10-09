using IntersectUtilities.LerCompare;

namespace IntersectUtilities.LerCompare.Tests;

public sealed class LerCompareTests
{
    private static Pipe P(string handle, double y = 0, string id = "", params double[] xs)
    {
        var pipe = new Pipe { Handle = handle, Layer = "Vandledning_L2", Points = (xs.Length > 0 ? xs : new double[] { 0, 10 }).Select(x => new XY(x, y)).ToList() };
        pipe.Bulges = pipe.Points.Select(_ => 0d).ToList();
        pipe.Properties["Vandledning/GmlId"] = id;
        pipe.Properties["Vandledning/LedningsEjersNavn"] = "Owner";
        pipe.Properties["Vandledning/Driftsstatus"] = "i drift";
        return pipe;
    }

    private static Report Compare(Pipe[] old, Pipe[] current) => Compare(old, current, (_, _) => { });

    private static Report Compare(Pipe[] old, Pipe[] current, Action<Snapshot, Snapshot> configure)
    {
        var a = new Snapshot { Pipes = old.ToList() };
        var b = new Snapshot { Pipes = current.ToList() };
        configure(a, b);
        return Engine.Compare(a, b, new Options());
    }

    private static void Require(bool value) => Assert.True(value);
    private static CoveragePolygon Rectangle(double x1, double y1, double x2, double y2)
        => new() { Rings = new() { new() { new(x1, y1), new(x2, y1), new(x2, y2), new(x1, y2) } } };
    [Fact]
    public void Handlescannotidentifydifferentroutes()
    {
        var r=Compare(new[]{P("A",0)},new[]{P("A",20)});Require(r.Counts["New"]==1&&r.Counts["Missing"]==1);
    }

    [Fact]
    public void Reversedrouteisunchanged()
    {
        var r=Compare(new[]{P("A",0,"id",0,5,10)},new[]{P("B",0,"id",10,5,0)});Require(r.Unchanged==1);
    }

    [Fact]
    public void Movementbelowonecmisunchanged()
    {
        var r=Compare(new[]{P("A",0,"id")},new[]{P("B",.009,"id")});Require(r.Unchanged==1);
    }

    [Fact]
    public void Movementaboveonecmflagsverticesandedges()
    {
        var r=Compare(new[]{P("A",0,"id")},new[]{P("B",.011,"id")}).Results.Single();Require(r.Flags.HasFlag(Change.Geometry)&&r.OldChangedVertices.Count==2&&r.OldChangedEdges.Count==1);
    }

    [Fact]
    public void Movedmiddlevertexflagsconnectededges()
    {
        var a=P("A",0,"id",0,5,10);var b=P("B",0,"id",0,5,10);b.Points[1]=new(5,.1);var r=Compare(new[]{a},new[]{b}).Results.Single();Require(r.OldChangedVertices.SequenceEqual(new[]{1})&&r.OldChangedEdges.SequenceEqual(new[]{0,1}));
    }

    [Fact]
    public void Statusandmaterialcarrymultipleflags()
    {
        var a=P("A");var b=P("B");a.Properties["Vandledning/UdvendigMateriale"]="PVC";b.Properties["Vandledning/UdvendigMateriale"]="PE";b.Properties["Vandledning/Driftsstatus"]="ude af drift";var r=Compare(new[]{a},new[]{b}).Results.Single();Require(r.Flags.HasFlag(Change.Status)&&r.Flags.HasFlag(Change.Material)&&r.Differences.Count==2);
    }

    [Fact]
    public void Addedabsentfielddiffersfromemptyfield()
    {
        var a=P("A");var b=P("B");b.Properties["Custom/Empty"]="";var r=Compare(new[]{a},new[]{b}).Results.Single();Require(!r.Differences.Single().OldPresent&&r.Differences.Single().NewValue=="");
    }

    [Fact]
    public void SamenameindifferentPropertySetsispreserved()
    {
        var a=P("A");var b=P("B");a.Properties["Custom/Driftsstatus"]="x";b.Properties["Custom/Driftsstatus"]="y";var r=Compare(new[]{a},new[]{b}).Results.Single();Require(r.Differences.Single().Property=="Custom/Driftsstatus");
    }

    [Fact]
    public void Deliverymetadataseparatedfrompipeproperties()
    {
        var a=P("A");var b=P("B");a.Properties["Vandledning/LerNummer"]="1";b.Properties["Vandledning/LerNummer"]="2";var r=Compare(new[]{a},new[]{b}).Results.Single();Require(r.Flags==Change.Delivery);
    }

    [Fact]
    public void Materialcasingchangesaretextformatchanges()
    {
        var a=P("A");var b=P("B");a.Properties["Vandledning/UdvendigMateriale"]="pe80";b.Properties["Vandledning/UdvendigMateriale"]="PE80";var r=Compare(new[]{a},new[]{b}).Results.Single();Require(r.Flags==Change.TextFormat&&r.Differences[0].OldValue=="pe80");
    }

    [Fact]
    public void Localizedbooleanandtimestamprepresentationsnormalize()
    {
        var a=P("A");var b=P("B");a.Properties["Vandledning/HarFod"]="Falsk";b.Properties["Vandledning/HarFod"]="False";a.Properties["Vandledning/RegistreringFra"]="08-04-2025 00:00:00";b.Properties["Vandledning/RegistreringFra"]="08/04/2025 00.00.00";Require(Compare(new[]{a},new[]{b}).Unchanged==1);
    }

    [Fact]
    public void Oneroutesplitintotwocompletepieces()
    {
        var r=Compare(new[]{P("A",0,"",0,10)},new[]{P("B",0,"",0,5),P("C",0,"",5,10)}).Results.Single();Require(r.Flags.HasFlag(Change.Split)&&r.New.Count==2&&!r.Flags.HasFlag(Change.New));
    }

    [Fact]
    public void Tworoutesmergedintoonecompleteroute()
    {
        var r=Compare(new[]{P("A",0,"",0,5),P("B",0,"",5,10)},new[]{P("C",0,"",0,10)}).Results.Single();Require(r.Flags.HasFlag(Change.Merged)&&r.Old.Count==2);
    }

    [Fact]
    public void Incompletesplitisnotconfirmed()
    {
        var r=Compare(new[]{P("A",0,"",0,10)},new[]{P("B",0,"",0,4),P("C",0,"",6,10)});Require(r.Counts["Split"]==0);
    }

    [Fact]
    public void Parallelrouteswithequalevidencerequirereview()
    {
        var r=Compare(new[]{P("A",0)},new[]{P("B",.1),P("C",-.1)}).Results.Single();Require(r.Flags.HasFlag(Change.Review)&&r.New.Count==2&&!r.Flags.HasFlag(Change.New));
    }

    [Fact]
    public void SharedIDfarawayisaconflict()
    {
        var r=Compare(new[]{P("A",0,"reused")},new[]{P("B",100,"reused")}).Results.Single();Require(r.Flags.HasFlag(Change.Review)&&r.Match.Contains("conflicting"));
    }

    [Fact]
    public void Differentutilityneversilentlymatchesnearbygeometry()
    {
        var a=P("A");var b=P("B");b.Layer="GAS-Stikledning";var r=Compare(new[]{a},new[]{b});Require(r.Counts["New"]==1&&r.Counts["Missing"]==1);
    }

    [Fact]
    public void Ownerchangesareflaggedforreview()
    {
        var a=P("A");var b=P("B");b.Properties["Vandledning/LedningsEjersNavn"]="Other";var r=Compare(new[]{a},new[]{b}).Results.Single();Require(r.Flags.HasFlag(Change.Review)&&r.Flags.HasFlag(Change.Properties));
    }

    [Fact]
    public void Newrouteoutsideoldcoverageisflaggedincomparable()
    {
        var r=Compare(Array.Empty<Pipe>(),new[]{P("A",0,"",11,12)},(a,b)=>{a.Coverage.Add(Rectangle(-1,-1,10,1));b.Coverage.Add(Rectangle(-1,-1,20,1));}).Results.Single();Require(r.Flags.HasFlag(Change.New)&&r.Flags.HasFlag(Change.Coverage));
    }

    [Fact]
    public void Pipecrossingacoveragegapwithcoveredendpointsisdetected()
    {
        var p=P("A",0,"",0,10);var coverage=new[]{Rectangle(-1,-1,2,1),Rectangle(8,-1,11,1)};Require(Geometry.HasOutside(p,coverage,.01));
    }

    [Fact]
    public void Coverageholesareexcluded()
    {
        var c=Rectangle(-1,-1,11,1);c.Rings.Add(Rectangle(4,-.5,6,.5).Rings[0]);Require(Geometry.HasOutside(P("A"),new[]{c},.01));
    }

    [Fact]
    public void MissinghatchfallbackpreservesNewMissing()
    {
        var r=Compare(Array.Empty<Pipe>(),new[]{P("A")});Require(r.Counts["Coverage"]==0&&r.Counts["New"]==1);
    }

    [Fact]
    public void Arcedgechangewithunchangedverticesisdetected()
    {
        var a=P("A",0,"id");var b=P("B",0,"id");a.Bulges[0]=.1;b.Bulges[0]=.11;var r=Compare(new[]{a},new[]{b}).Results.Single();Require(r.Flags.HasFlag(Change.Geometry)&&r.OldChangedVertices.Count==0&&r.OldChangedEdges.Count==1);
    }

    [Fact]
    public void Closedloopstartindexrotationisunchanged()
    {
        var a=P("A");var b=P("B");a.Points=new(){new(0,0),new(1,0),new(1,1),new(0,1)};b.Points=new(){new(1,1),new(0,1),new(0,0),new(1,0)};a.Bulges=b.Bulges=new(){0,0,0,0};a.Closed=b.Closed=true;Require(Compare(new[]{a},new[]{b}).Unchanged==1);
    }

    [Fact]
    public void Vertexinsertionisageometrychange()
    {
        var r=Compare(new[]{P("A",0,"id",0,10)},new[]{P("B",0,"id",0,5,10)}).Results.Single();Require(r.Flags.HasFlag(Change.Geometry));
    }

    [Fact]
    public void Insertedvertexhighlightsonlytheinsertedvertexandreplacededges()
    {
        var r=Compare(new[]{P("A",0,"id",0,10)},new[]{P("B",0,"id",0,5,10)}).Results.Single();Require(r.OldChangedVertices.Count==0&&r.NewChangedVertices.SequenceEqual(new[]{1})&&r.OldChangedEdges.SequenceEqual(new[]{0})&&r.NewChangedEdges.SequenceEqual(new[]{0,1}));
    }

    [Fact]
    public void Removedvertexkeepsotherverticesunmarked()
    {
        var r=Compare(new[]{P("A",0,"id",0,5,10)},new[]{P("B",0,"id",0,10)}).Results.Single();Require(r.OldChangedVertices.SequenceEqual(new[]{1})&&r.NewChangedVertices.Count==0);
    }

    [Fact]
    public void Identicalrequestcoveragedoesnotmakepipesoutsidebothincomparable()
    {
        var a=P("A",10);var b=P("B",10);b.Properties["Vandledning/Driftsstatus"]="ude af drift";var r=Compare(new[]{a},new[]{b},(x,y)=>{x.Coverage.Add(Rectangle(-1,-1,11,1));y.Coverage.Add(Rectangle(-1,-1,11,1));}).Results.Single();Require(!r.Flags.HasFlag(Change.Coverage));
    }

    [Fact]
    public void DuplicateIDsonidenticalroutescannotselectanarbitraryoldpipe()
    {
        var r=Compare(new[]{P("A",0,"dup"),P("B",0,"dup")},new[]{P("C",0,"dup")}).Results.Single();Require(r.Flags.HasFlag(Change.Review)&&r.Old.Count==2);
    }

    [Fact]
    public void Reversedarcrouteisunchanged()
    {
        var a=P("A",0,"id",0,10);var b=P("B",0,"id",10,0);a.Bulges[0]=.1;b.Bulges[0]=-.1;Require(Compare(new[]{a},new[]{b}).Unchanged==1);
    }

    [Fact]
    public void DefaultOptionRepresentsAbsence()
        => Assert.Equal("none", default(CompareOption<string>).Match(value => value, () => "none"));

    private sealed class FakePalette
    {
        public bool Disposed;
        public int DisposeCalls;
    }

    [Fact]
    public void ShortenedExistingRouteIsNotAnUnmatchedAdditionAndRemoval()
    {
        var report = Compare(new[] { P("OLD", 0, "old-id", 0, 5, 10) }, new[] { P("NEW", 0, "new-id", 0, 5, 8) });
        var row = report.Results.Single();
        Assert.Equal("Partially shared route", row.Match);
        Assert.True(row.Flags.HasFlag(Change.Review) && row.Flags.HasFlag(Change.Geometry));
        Assert.False(row.Flags.HasFlag(Change.New) || row.Flags.HasFlag(Change.Missing));
        Assert.Equal("OLD", row.Old.Single().Handle);
    }

    [Fact]
    public void ExtendedExistingRouteKeepsItsPotentialOldCounterpart()
    {
        var report = Compare(new[] { P("OLD", 0, "old-id", 0, 5, 10) }, new[] { P("NEW", 0, "new-id", 0, 5, 12) });
        var row = report.Results.Single();
        Assert.True(row.Flags.HasFlag(Change.Geometry) && row.Flags.HasFlag(Change.Review));
        Assert.False(row.Flags.HasFlag(Change.New));
        Assert.NotEmpty(row.NewChangedVertices);
        Assert.NotEmpty(row.NewChangedEdges);
    }

    [Fact]
    public void PartialResegmentationDoesNotRequireFullOldRouteCoverage()
    {
        var row = Compare(new[] { P("OLD", 0, "old-id", 0, 20) },
            new[] { P("A", 0, "new-a", 0, 8), P("B", 0, "new-b", 8, 18) }).Results.Single();
        Assert.Equal(2, row.New.Count);
        Assert.True(row.Flags.HasFlag(Change.Review));
        Assert.False(row.Flags.HasFlag(Change.New) || row.Flags.HasFlag(Change.Missing));
    }

    [Fact]
    public void SharedGeometryWithAnAlreadyMatchedRouteRemainsAnExplicitCandidate()
    {
        var old = P("OLD", 0, "stable", 0, 10);
        var matched = P("MATCHED", 0, "stable", 0, 10);
        var extra = P("EXTRA", 0, "regenerated", 0, 8);
        var report = Compare(new[] { old }, new[] { matched, extra });
        Assert.Equal(1, report.Unchanged);
        var row = report.Results.Single();
        Assert.True(row.Flags.HasFlag(Change.Review));
        Assert.False(row.Flags.HasFlag(Change.New));
        Assert.Empty(row.Old);
        Assert.Equal("OLD", row.CandidateOld.Single().Handle);
        Assert.Equal("?OLD", row.OldHandles);
    }

    [Fact]
    public void PerpendicularCrossingIsNotAContinuationOfAnOldPipe()
    {
        var a = P("OLD", 0, "old", -10, 10);
        var b = P("NEW", 0, "new");
        b.Points = new() { new(0, -8), new(0, 8) };
        var report = Compare(new[] { a }, new[] { b });
        Assert.Equal(1, report.Counts["New"]);
        Assert.Equal(1, report.Counts["Missing"]);
    }

    [Fact]
    public void NearbyEndpointDoesNotCountAsSharedRouteLength()
    {
        var report = Compare(new[] { P("OLD", 0, "old", 0, 10) }, new[] { P("NEW", 0, "new", 10.2, 18) });
        Assert.Equal(1, report.Counts["New"]);
        Assert.Equal(1, report.Counts["Missing"]);
    }

    [Fact]
    public void ParallelPartialRouteBeyondTheMatchingRadiusStaysUnmatched()
    {
        var report = Compare(new[] { P("OLD", 0, "old", 0, 10) }, new[] { P("NEW", 1.1, "new", 0, 8) });
        Assert.Equal(1, report.Counts["New"]);
    }

    [Fact]
    public void PartialRouteWithADifferentOwnerIsNotSilentlyMatched()
    {
        var a = P("OLD", 0, "old", 0, 10);
        var b = P("NEW", 0, "new", 0, 8);
        b.Properties["Vandledning/LedningsEjersNavn"] = "Other";
        var report = Compare(new[] { a }, new[] { b });
        Assert.Equal(1, report.Counts["New"]);
    }

    [Fact]
    public void SharedLengthUsesUnionInsteadOfDoubleCountingDuplicateSegments()
    {
        var a = new[] { new XY(0, 0), new XY(10, 0) };
        var b = new[] { new XY(0, 0), new XY(8, 0) };
        Assert.Equal(8, Geometry.SharedLength(a, new[] { b, b }, .01), 6);
        Assert.Equal(8, Geometry.SharedLength(a, new[] { b.Reverse().ToArray() }, .01), 6);
    }

    [Fact]
    public void ShortenedSubMeterPolylineIsAlsoCheckedForRouteOverlap()
    {
        var report = Compare(new[] { P("OLD", 0, "old", 0, .4) }, new[] { P("NEW", 0, "new", 0, .2) });
        Assert.Equal(0, report.Counts["New"]);
        Assert.Equal(0, report.Counts["Missing"]);
        Assert.True(report.Results.Single().Flags.HasFlag(Change.Review));
    }

    [Fact]
    public void ShortNearbyRouteWithAnAlreadyMatchedCounterpartNeedsReview()
    {
        var old = P("OLD", 0, "stable", 0, .4);
        var matched = P("MATCHED", 0, "stable", 0, .4);
        var extra = P("EXTRA", .2, "changed", 0, .4);
        extra.Points = new() { new(.2, -.1), new(.2, .3) };
        var report = Compare(new[] { old }, new[] { matched, extra });
        Assert.Equal(1, report.Unchanged);
        Assert.Equal(0, report.Counts["New"]);
        Assert.True(report.Results.Single().Flags.HasFlag(Change.Review));
        Assert.Single(report.Results.Single().CandidateOld);
    }

    [Fact]
    public void ShortChangedSegmentsWithUnequalLengthsRetainUncertainCounterparts()
    {
        var old = P("OLD", 0, "old-id", 0, .76);
        var current = P("NEW", 0, "new-id", 0, .47);
        current.Points = new() { new(.1, -.2), new(.1, .27) };
        var report = Compare(new[] { old }, new[] { current });
        Assert.Equal(0, report.Counts["New"]);
        Assert.Equal(0, report.Counts["Missing"]);
        Assert.All(report.Results, row => Assert.True(row.Flags.HasFlag(Change.Review)));
        Assert.Equal("OLD", report.Results.Single(row => row.New.Count == 1).CandidateOld.Single().Handle);
    }

    [Fact]
    public void RepeatedShowReusesTheLivePalette()
    {
        var owner = new OwnedResource<FakePalette>(value => value.Disposed, value => value.Disposed = true);
        var first = owner.GetOrCreate(() => new());
        Assert.Same(first, owner.GetOrCreate(() => new()));
    }

    [Fact]
    public void ADisposedPaletteIsReplacedBeforeShowing()
    {
        var owner = new OwnedResource<FakePalette>(value => value.Disposed, value => value.Disposed = true);
        var first = owner.GetOrCreate(() => new());
        first.Disposed = true;
        var second = owner.GetOrCreate(() => new());
        Assert.NotSame(first, second);
        Assert.False(second.Disposed);
    }

    [Fact]
    public void ResetDropsOwnershipBeforeNativeDisposalCallbacks()
    {
        var observedStates = new List<bool>();
        var ownerReference = CompareOption<OwnedResource<FakePalette>>.None;
        var owner = new OwnedResource<FakePalette>(value => value.Disposed, value =>
        {
            observedStates.Add(ownerReference.Match(resource => resource.Match(_ => true, () => false), () => true));
            value.Disposed = true;
        });
        ownerReference = CompareOption<OwnedResource<FakePalette>>.Some(owner);
        owner.GetOrCreate(() => new());
        owner.Reset();
        Assert.Equal(new[] { false }, observedStates);
    }

    [Fact]
    public void RepeatedUnloadDisposesOnlyOnce()
    {
        var owner = new OwnedResource<FakePalette>(value => value.Disposed, value => { value.Disposed = true; value.DisposeCalls++; });
        var first = owner.GetOrCreate(() => new());
        owner.Reset();
        owner.Reset();
        Assert.Equal(1, first.DisposeCalls);
        Assert.NotSame(first, owner.GetOrCreate(() => new()));
    }
}
