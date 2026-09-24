using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// Where every connection will stand on its main, and how much of the main it
/// will occupy - worked out BEFORE any main is routed, so the route can keep
/// that stretch straight and clear.
/// <para>
/// The connection itself is made later (<see cref="BranchConnectionStep"/>),
/// once both pipelines exist. What it will occupy does not have to wait for
/// that: the Produkt is the translation's (the import pins it, it never asks
/// the Afgreningsmatrix), and the two pipes it joins are the legacy drawing's.
/// So each branch is translated here once for its seat, and again there for
/// its connection - the translation is pure, and both read the same answer.
/// </para>
/// <para>
/// A branch the translation does not connect puts nothing on its main, so it
/// takes no seat. One whose straight NDH cannot answer is said out loud in the
/// report and takes no seat either; the connection step then meets the same
/// refusal and marks the junction in the drawing.
/// </para>
/// </summary>
internal static class JunctionSeating
{
    public static ILookup<string, NdhJunctionSeat> Seats(
        LegacyDrawing legacy, MergedTraces merged, INdhJunctionStraight straight, NdhImportReport report)
    {
        Dictionary<string, LegacyPipelineTrace> traces =
            merged.Traces.ToDictionary(t => t.Name, StringComparer.Ordinal);
        List<(string Main, NdhJunctionSeat Seat)> seats = new List<(string, NdhJunctionSeat)>();

        foreach (LegacyBranch b in legacy.Branches)
        {
            string mainName = merged.NameOf(b.MainName);
            string branchName = merged.NameOf(b.BranchName);
            //Merged into one pipeline: a pipeline cannot branch off itself, and
            //the connection step reports it.
            if (mainName == branchName) continue;
            if (!traces.TryGetValue(mainName, out LegacyPipelineTrace? main) ||
                !traces.TryGetValue(branchName, out LegacyPipelineTrace? branch)) continue;

            LegacyBranchTranslator.Translate(b).Tell(
                new SeatingAudience(b, main, branch, straight, report, seats));
        }
        return seats.ToLookup(x => x.Main, x => x.Seat, StringComparer.Ordinal);
    }

    /// <summary>
    /// What a translation means for the MAIN: a connection seats itself, and
    /// anything that is not one leaves the main alone.
    /// </summary>
    private sealed class SeatingAudience(
        LegacyBranch b, LegacyPipelineTrace main, LegacyPipelineTrace branch,
        INdhJunctionStraight straight, NdhImportReport report,
        List<(string Main, NdhJunctionSeat Seat)> seats) : IBranchAudience
    {
        public void Connect(string produkt, NdhBranchOutlet outlet)
        {
            bool atStart = NearestEndIsStart(branch.Centreline, b.BranchPort);
            LegacyIdentitySpan branchPipe = atStart ? branch.Spans[0] : branch.Spans[^1];
            try
            {
                JunctionStraight taken = straight.At(PipeAt(main, b.Site), branchPipe, atStart, outlet, produkt);
                seats.Add((main.Name, new NdhJunctionSeat(b.Site, taken)));
            }
            catch (InvalidOperationException ex)
            {
                report.Adjusted.Add($"{main.Name}: afgreningen til {branch.Name} ({produkt}) " +
                    $"har ingen plads på hovedledningen - {ex.Message}");
            }
        }

        //Nothing of NDH's stands on the main, so nothing is seated. The
        //connection step says why, where the drafter will look for it.
        public void CannotConnect(string note, string reason) { }

        public void Ignore(string why) { }
    }

    /// <summary>The span of <paramref name="trace"/> that holds the point nearest <paramref name="p"/>.</summary>
    private static LegacyIdentitySpan PipeAt(LegacyPipelineTrace trace, Point2d p)
    {
        Polyline cl = trace.Centreline;
        double d = cl.GetDistAtPoint(cl.GetClosestPointTo(new Point3d(p.X, p.Y, 0.0), false));
        foreach (LegacyIdentitySpan s in trace.Spans)
            if (d <= s.EndDist) return s;
        return trace.Spans[^1];
    }

    /// <summary>Whether the trace's first vertex, rather than its last, is nearer the legacy branch port.</summary>
    private static bool NearestEndIsStart(Polyline cl, Point2d port)
    {
        Point2d first = cl.GetPoint2dAt(0), last = cl.GetPoint2dAt(cl.NumberOfVertices - 1);
        return first.GetDistanceTo(port) <= last.GetDistanceTo(port);
    }
}
