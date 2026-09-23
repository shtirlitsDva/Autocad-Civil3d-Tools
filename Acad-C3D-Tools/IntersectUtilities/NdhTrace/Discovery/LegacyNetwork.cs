using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Graphs;

using System;
using System.Collections.Generic;
using System.Linq;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// Reads the legacy pipeline network with the legacy utilities: one
/// <see cref="IPipelineV2"/> per traced pipeline, stationed along its traced
/// centreline; <see cref="PipelineGraphBuilder"/> roots each network at its
/// largest DN (the distance from the root names a merged pipeline); and
/// <see cref="IPipelineV2.TryGetConnectionLocationToParent"/> places every pair
/// the legacy connection graph says is connected:
/// - both pipelines END there, and no branch part stands there: an end-to-end
///   join, merged into one NDH pipeline;
/// - one pipeline ends on the other's run with no branch part naming the pair:
///   a materialeskift at that end makes it a branch (Direkte påsvejsning on a
///   steel main), anything else is marked.
///
/// The Try form is used so a pair it cannot place answers false instead of
/// throwing; both forms only read.
/// </summary>
internal static class LegacyNetwork
{
    //The legacy network's own tolerance (PipelineGraphBuilder, IsConnectedTo).
    private const double ConnectionTol = 0.05;
    //A branch part standing this close to a meeting point makes it a junction,
    //not an end-to-end join.
    private const double PartAtJoinTol = 0.1;
    //How far from a pipeline's end a materialeskift's port may be and still
    //be the part at that end.
    private const double PartAtEndTol = 0.1;

    public static void Read(
        IReadOnlyList<LegacyComponent> parts, IReadOnlyDictionary<string, List<Entity>> groups,
        LegacyDrawing drawing)
    {
        List<(IPipelineV2 Pipeline, Polyline Topology)> pipelines = new();
        try
        {
            foreach (LegacyPipelineTrace t in drawing.Traces.Traces)
            {
                if (!groups.TryGetValue(t.Name, out List<Entity>? ents) || ents.Count == 0) continue;
                //The pipeline keeps the polyline it is given; it gets its own copy.
                Polyline topology = (Polyline)t.Centreline.Clone();
                pipelines.Add((PipelineV2Factory.CreateFromTopology(ents, topology), topology));
            }

            ReadDepths(pipelines.Select(x => x.Pipeline).ToList(), drawing);
            ReadMeetings(pipelines.Select(x => x.Pipeline).ToList(), parts, drawing);
        }
        finally
        {
            foreach ((_, Polyline topology) in pipelines) topology.Dispose();
        }
    }

    private static void ReadDepths(List<IPipelineV2> pipelines, LegacyDrawing drawing)
    {
        try
        {
            GraphCollection<IPipelineV2> graphs = new PipelineGraphBuilder().BuildPipelineGraphs(pipelines);
            foreach (Graph<IPipelineV2> graph in graphs)
            {
                Dictionary<Node<IPipelineV2>, int> depth = new() { [graph.Root] = 0 };
                foreach (Node<IPipelineV2> node in graph.Bfs())
                {
                    int d = node.Parent is Node<IPipelineV2> parent && depth.TryGetValue(parent, out int pd)
                        ? pd + 1
                        : 0;
                    depth[node] = d;
                    drawing.Depth[node.Value.Name] = d;
                }
            }
        }
        catch (Exception ex)
        {
            //Loud, but not fatal: without the tree a merged pipeline is named
            //alphabetically and branches connect in name order.
            drawing.NetworkNotes.Add(
                $"Netværket kunne ikke rodfæstes ved største DN ({ex.Message}); " +
                "sammenlagte rørledninger navngives alfabetisk.");
        }
    }

    private static void ReadMeetings(
        List<IPipelineV2> pipelines, IReadOnlyList<LegacyComponent> parts, LegacyDrawing drawing)
    {
        //Pairs a branch part already joins: their meeting is that part's.
        HashSet<(string, string)> joinedByPart = new();
        foreach (LegacyBranch b in drawing.Branches)
        {
            joinedByPart.Add((b.MainName, b.BranchName));
            joinedByPart.Add((b.BranchName, b.MainName));
        }
        foreach (LegacyComponent p in parts.Where(x => x.Role == LegacyPartRole.ServiceConnection))
        {
            joinedByPart.Add((p.BelongsTo, p.BranchesOffTo));
            joinedByPart.Add((p.BranchesOffTo, p.BelongsTo));
        }
        List<LegacyComponent> branchParts = parts
            .Where(x => x.Role is LegacyPartRole.Tee or LegacyPartRole.Stud or LegacyPartRole.ServiceConnection)
            .ToList();

        for (int i = 0; i < pipelines.Count; i++)
        {
            for (int j = i + 1; j < pipelines.Count; j++)
            {
                IPipelineV2 a = pipelines[i], b = pipelines[j];
                if (!a.IsConnectedTo(b, ConnectionTol)) continue;

                string pairName = $"{a.Name} / {b.Name}";
                try
                {
                    //Case 4 looks for THIS pipeline's end on the parent's parts
                    //only, so the pair is asked both ways.
                    if (!a.TryGetConnectionLocationToParent(b, ConnectionTol, out Point3d at3) &&
                        !b.TryGetConnectionLocationToParent(a, ConnectionTol, out at3))
                    {
                        if (!joinedByPart.Contains((a.Name, b.Name)))
                            drawing.NetworkNotes.Add(
                                $"{pairName}: forbundet i den gamle tegning, men forbindelsesstedet kunne ikke findes.");
                        continue;
                    }
                    Point2d at = new Point2d(at3.X, at3.Y);
                    Classify(a, b, at, parts, branchParts, joinedByPart, drawing);
                }
                catch (Exception ex)
                {
                    drawing.NetworkNotes.Add($"{pairName}: forbindelsen kunne ikke læses ({ex.Message}).");
                }
            }
        }
    }

    private static void Classify(
        IPipelineV2 a, IPipelineV2 b, Point2d at,
        IReadOnlyList<LegacyComponent> parts, List<LegacyComponent> branchParts,
        HashSet<(string, string)> joinedByPart, LegacyDrawing drawing)
    {
        (double da, bool aAtStart) = NearestEnd(a, at);
        (double db, bool bAtStart) = NearestEnd(b, at);

        if (da < ConnectionTol && db < ConnectionTol)
        {
            //Both end here. A branch part standing at the meeting makes it a
            //junction the part's own branch describes; only a bare meeting is
            //an end-to-end join.
            if (branchParts.Any(p => p.Touches(at, PartAtJoinTol))) return;
            drawing.Joins.Add(new LegacyJoin(a.Name, aAtStart, b.Name, bAtStart, at));
            return;
        }

        if (joinedByPart.Contains((a.Name, b.Name))) return;

        //The one whose end is at the meeting leaves the other's run.
        (IPipelineV2 branch, IPipelineV2 main, bool branchAtStart) = da <= db ? (a, b, aAtStart) : (b, a, bAtStart);
        Point3d end3 = branchAtStart ? branch.StartPoint : branch.EndPoint;
        Point2d end = new Point2d(end3.X, end3.Y);

        LegacyComponent? skift = parts.FirstOrDefault(p =>
            p.Role == LegacyPartRole.Materialeskift && p.BelongsTo == branch.Name && p.Touches(end, PartAtEndTol));

        LegacyPipelineTrace? mainTrace = drawing.Traces.Traces.FirstOrDefault(t => t.Name == main.Name);
        if (skift != null && mainTrace != null)
        {
            (Point2d site, var system, var type) = LegacyBranchFinder.SiteOn(mainTrace, end);
            drawing.Branches.Add(new LegacyBranch(
                main.Name, branch.Name, skift.Navn, skift.Handle, end, site, system, type,
                "materialeskift ved afgreningens ende"));
            return;
        }

        //A tee or stud at the branch's end that names no branch is already
        //reported by LegacyBranchFinder; one marker per place.
        if (branchParts.Any(p => p.Touches(end, PartAtEndTol))) return;

        drawing.Problems.Add(new LegacyBranchProblem(
            $"{branch.Name} → {main.Name}", at,
            $"NDHFROMFJV: '{branch.Name}' meets '{main.Name}' with no legacy branch part; not connected.",
            "mødes i den gamle tegning uden nogen afgreningsdel"));
    }

    private static (double Distance, bool AtStart) NearestEnd(IPipelineV2 p, Point2d at)
    {
        double ds = new Point2d(p.StartPoint.X, p.StartPoint.Y).GetDistanceTo(at);
        double de = new Point2d(p.EndPoint.X, p.EndPoint.Y).GetDistanceTo(at);
        return ds <= de ? (ds, true) : (de, false);
    }
}
