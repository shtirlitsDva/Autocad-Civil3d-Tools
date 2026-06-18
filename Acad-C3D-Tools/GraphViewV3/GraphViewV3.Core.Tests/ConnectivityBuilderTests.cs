using GraphViewV3.Core;
using Xunit;

namespace GraphViewV3.Core.Tests;

public class ConnectivityBuilderTests
{
    private static PipeDto Pipe(string h, Pt a, Pt b, string size = "DN125", string sys = "TWIN")
        => new(h, $"FJV-{sys}-{size}", sys, size, a, b, a.DistanceTo(b), new[] { a, b });

    [Fact]
    public void TwoPipesSharingAnEndpoint_ConnectButAreFlaggedIllegal()
    {
        var snap = new NetworkSnapshot(
            new[] { Pipe("A", new Pt(0, 0), new Pt(10, 0)), Pipe("B", new Pt(10, 0), new Pt(20, 0)) },
            Array.Empty<ComponentDto>());

        var g = new ConnectivityBuilder { Tolerance = 0.5 }.Build(snap);

        Assert.Equal(2, g.Nodes.Count);
        Assert.Single(g.Edges);
        Assert.True(g.Edges[0].IsError); // pipe-to-pipe is illegal per the FJV ruleset
        Assert.Single(g.Components);
    }

    [Fact]
    public void WeldedStud_ConnectsToPipeMidSpan_NotAtVertex()
    {
        var pipe = Pipe("P", new Pt(0, 0), new Pt(100, 0)); // long straight run
        var stud = new ComponentDto("S", "AFGRSTUDS", new Pt(50, 0),
            Ports: new[] { new Pt(50, 0) }, Weldable: true, WeldPort: new Pt(50, 0)); // mid-span weld

        var g = new ConnectivityBuilder { Tolerance = 0.5 }.Build(
            new NetworkSnapshot(new[] { pipe }, new[] { stud }));

        Assert.Single(g.Edges);
        Assert.False(g.Edges[0].IsError); // legitimate welded connection
        Assert.Single(g.Components);
    }

    [Fact]
    public void PipesJoinedThroughComponent_AreNotFlaggedIllegal()
    {
        var a = Pipe("A", new Pt(0, 0), new Pt(10, 0));
        var b = Pipe("B", new Pt(10, 0), new Pt(20, 0));
        var comp = new ComponentDto("C", "PRTFLX-REDUKTION", new Pt(10, 0), new[] { new Pt(10, 0) });

        var g = new ConnectivityBuilder { Tolerance = 0.5 }.Build(
            new NetworkSnapshot(new[] { a, b }, new[] { comp }));

        Assert.DoesNotContain(g.Edges, e => e.IsError); // mediated by the component -> legal
        Assert.Single(g.Components);
    }

    [Fact]
    public void PipesFartherThanTolerance_Float_AsSeparateComponents()
    {
        var snap = new NetworkSnapshot(
            new[] { Pipe("A", new Pt(0, 0), new Pt(10, 0)), Pipe("B", new Pt(50, 0), new Pt(60, 0)) },
            Array.Empty<ComponentDto>());

        var g = new ConnectivityBuilder { Tolerance = 0.5 }.Build(snap);

        Assert.Empty(g.Edges);
        Assert.Equal(2, g.Components.Count); // both float independently
    }

    [Fact]
    public void ComponentMuffePort_ConnectsToPipeEndpoint()
    {
        var pipe = Pipe("P", new Pt(0, 0), new Pt(10, 0));
        var comp = new ComponentDto("C", "PRTFLX-REDUKTION", new Pt(10, 0),
            new[] { new Pt(10.0, 0.0) }); // muffe sits on the pipe end

        var g = new ConnectivityBuilder { Tolerance = 0.5 }.Build(
            new NetworkSnapshot(new[] { pipe }, new[] { comp }));

        Assert.Single(g.Edges);
        Assert.Single(g.Components);
        Assert.Equal(2, g.Components[0].Count);
    }

    [Fact]
    public void SingleUnconnectedComponent_Floats_AsOwnComponent()
    {
        var comp = new ComponentDto("C", "VENTIL-TWIN-GLD", new Pt(5, 5), new[] { new Pt(5, 5) });
        var g = new ConnectivityBuilder().Build(new NetworkSnapshot(Array.Empty<PipeDto>(), new[] { comp }));

        Assert.Single(g.Nodes);
        Assert.Empty(g.Edges);
        Assert.Single(g.Components);
    }

    [Fact]
    public void Layout_IsDeterministic_AcrossRebuilds()
    {
        var snap = new NetworkSnapshot(
            new[]
            {
                Pipe("A", new Pt(0, 0), new Pt(10, 0)),
                Pipe("B", new Pt(10, 0), new Pt(20, 0)),
                Pipe("C", new Pt(20, 0), new Pt(20, 10)),
            },
            Array.Empty<ComponentDto>());

        var r1 = new NetworkGraphService().Build(snap);
        var r2 = new NetworkGraphService().Build(snap);

        for (int i = 0; i < r1.Graph.Nodes.Count; i++)
        {
            Assert.Equal(r1.Graph.Nodes[i].X, r2.Graph.Nodes[i].X, 6);
            Assert.Equal(r1.Graph.Nodes[i].Y, r2.Graph.Nodes[i].Y, 6);
        }
        Assert.Equal(r1.ContentHash, r2.ContentHash);
    }
}
