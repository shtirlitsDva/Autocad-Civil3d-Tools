using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.Generic;
using System.Linq;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// ONE LEGACY VALVE BLOCK, as plain data. <paramref name="At"/> is the point on
/// the pipeline's centreline nearest the block - where the valve stands along
/// the route, which is all a route can use. <paramref name="Run"/> is the
/// carrier pipe the block sits on; <paramref name="Produkt"/> is what the block
/// register says it becomes.
///
/// A POINT, NOT A STATION. A merged pipeline runs its members end to end and
/// sometimes backwards, so a station taken on one member means nothing on the
/// merged centreline; a point does, exactly as a corner's does.
/// </summary>
internal readonly record struct LegacyValveBlock(Point2d At, NdhRun Run, string Produkt, string Handle);

/// <summary>
/// ONE VALVE AS NDH WILL STAND IT: the legacy blocks that are one valve. A twin
/// valve is one block. A bonded valve is TWO - one per carrier, staggered along
/// the run because their manholes cannot stand side by side - and is still ONE
/// NDH valve, on one vertex, with each carrier's stagger. A bonded valve whose
/// partner was not found is one block and is said so.
/// </summary>
internal sealed record LegacyValve(IReadOnlyList<LegacyValveBlock> Blocks)
{
    /// <summary>The blocks' handles, for the report.</summary>
    public string Handles => string.Join(", ", Blocks.Select(b => b.Handle));
}

/// <summary>
/// A valve the import could not carry across, and why: marked where it stands
/// with <paramref name="Note"/> (English, the drawing's own note layer) and
/// reported with <paramref name="Reason"/> (Danish).
/// </summary>
internal sealed record LostValve(Point2d At, string Note, string Reason);

/// <summary>
/// Reads the valves of ONE legacy pipeline: its blocks that the old drawing
/// calls a valve, each on the carrier it sits on, the two carriers of a bonded
/// valve paired into one.
/// </summary>
internal static class LegacyValveReader
{
    //WHAT THE OLD DRAWING CALLS A VALVE: its own element types, the legacy
    //classification every block of the register carries. This is a fact about
    //the OLD drawing and names no NDH part - which Produkt each block becomes
    //is still the register's answer, asked below.
    private static readonly HashSet<PipelineElementType> ValveTypes = new()
    {
        PipelineElementType.Engangsventil,
        PipelineElementType.PræisoleretVentil,
        PipelineElementType.PræventilMedUdluftning,
    };

    /// <summary>
    /// The pipeline's valves, paired. A valve block the register cannot
    /// translate is not a valve NDH can stand, and is added to
    /// <paramref name="lost"/> - never dropped in silence. Everything read here
    /// is plain data afterwards; the blocks are used only while
    /// <paramref name="tx"/> is open.
    /// </summary>
    public static List<LegacyValve> Read(
        IEnumerable<BlockReference> blocks, IReadOnlyList<Polyline> pipes, Polyline centreline,
        Transaction tx, List<LostValve> lost)
    {
        List<(LegacyValveBlock Block, double Station)> found = new();
        foreach (BlockReference br in blocks)
        {
            if (!FjvLegacyPipelineReader.TryGetType(br, out PipelineElementType type) ||
                !ValveTypes.Contains(type)) continue;

            Point3d onCl = centreline.GetClosestPointTo(br.Position, false);
            string navn = br.RealName();
            LegacyPartRegister.Of(navn).Tell(navn, new Reading(
                new Point2d(onCl.X, onCl.Y), centreline.GetDistAtPoint(onCl),
                CarrierOf(br, pipes, tx), br.Handle.ToString(), found, lost));
        }
        return LegacyValvePairing.Pair(found);
    }

    /// <summary>
    /// THE CARRIER A VALVE SITS ON: the pipe nearest its ports. A valve is
    /// welded into its own carrier, so its ports meet that pipe's ends; the
    /// other carrier of a bonded pair runs past at the pair's spacing (0.48 m
    /// centre to centre at DN65, more above), so the nearest pipe is never in
    /// doubt. A block without ports is measured from its insertion, which is
    /// on its own carrier's axis.
    /// </summary>
    private static NdhRun CarrierOf(BlockReference br, IReadOnlyList<Polyline> pipes, Transaction tx)
    {
        List<Point3d> ports = ComponentPorts.Read(br, tx).Select(p => p.Position).ToList();
        if (ports.Count == 0) ports.Add(br.Position);

        Polyline nearest = pipes
            .OrderBy(pipe => ports.Min(p => pipe.GetClosestPointTo(p, false).DistanceHorizontalTo(p)))
            .First();
        return NsDhModule.RunOf(GetPipeType(nearest));
    }

    /// <summary>
    /// ONE BLOCK, TOLD WHAT IT BECOMES. The register's three answers are three
    /// methods, so there is no verdict to read back: a block that translates
    /// leaves a valve behind, one NDH does not have yet is lost out loud, and
    /// one NDH deliberately does not model is let go in silence.
    /// </summary>
    private sealed class Reading : ILegacyVerdictAudience
    {
        private readonly Point2d at;
        private readonly double station;
        private readonly NdhRun run;
        private readonly string handle;
        private readonly List<(LegacyValveBlock Block, double Station)> found;
        private readonly List<LostValve> lost;

        public Reading(
            Point2d at, double station, NdhRun run, string handle,
            List<(LegacyValveBlock Block, double Station)> found, List<LostValve> lost)
        {
            this.at = at;
            this.station = station;
            this.run = run;
            this.handle = handle;
            this.found = found;
            this.lost = lost;
        }

        public void Translates(string navn, string produkt) =>
            found.Add((new LegacyValveBlock(at, run, produkt, handle), station));

        public void Missing(string navn, string note, string reason) =>
            lost.Add(new LostValve(at,
                $"legacy valve '{navn}' ({handle}) not carried across: {note}.",
                $"ventilen '{navn}' (blok {handle}) er ikke overført: {reason}"));

        public void Skipped(string navn, string why) { }
    }
}

/// <summary>
/// WHICH LEGACY VALVE BLOCKS ARE ONE VALVE. Plain data in, plain data out, so
/// the rule can be tested without a drawing.
/// </summary>
internal static class LegacyValvePairing
{
    //HOW FAR APART, ALONG THE RUN, THE TWO CARRIERS' VALVES OF ONE BONDED VALVE
    //MAY STAND. They are staggered only so far that their two manholes do not
    //collide - a manhole's width or two - while two different valves on one
    //pipeline stand a section apart, tens to hundreds of metres. Five metres
    //holds every stagger a drafter draws and reaches no other valve.
    //
    //NOT A REACH: it answers RECOGNITION - "are these two blocks one valve?" -
    //about a drawing NDH did not make. The stagger itself is read off the
    //blocks and handed to NDH as drawn.
    private const double PairReach = 5.0;

    /// <summary>
    /// The valves the blocks make. A twin block is a valve on its own. A Frem
    /// and a Retur block pair up NEAREST FIRST: every Frem-Retur pair within
    /// <see cref="PairReach"/> is considered, the closest pair is taken, and so
    /// on - so valves on both sides of a tee (four blocks, a few metres apart)
    /// pair across the carriers on each side rather than across the tee. A
    /// block left over is a valve on its own. In station order.
    /// </summary>
    public static List<LegacyValve> Pair(IReadOnlyList<(LegacyValveBlock Block, double Station)> blocks)
    {
        List<(double Station, LegacyValve Valve)> valves = new();
        HashSet<int> taken = new();

        var pairs =
            from f in Enumerable.Range(0, blocks.Count)
            where blocks[f].Block.Run == NdhRun.Frem
            from r in Enumerable.Range(0, blocks.Count)
            where blocks[r].Block.Run == NdhRun.Retur
            let apart = Math.Abs(blocks[f].Station - blocks[r].Station)
            where apart <= PairReach
            orderby apart
            select (f, r);

        foreach ((int f, int r) in pairs)
        {
            if (taken.Contains(f) || taken.Contains(r)) continue;
            taken.Add(f);
            taken.Add(r);
            valves.Add(((blocks[f].Station + blocks[r].Station) / 2.0,
                        new LegacyValve(new[] { blocks[f].Block, blocks[r].Block })));
        }

        for (int i = 0; i < blocks.Count; i++)
            if (!taken.Contains(i))
                valves.Add((blocks[i].Station, new LegacyValve(new[] { blocks[i].Block })));

        return valves.OrderBy(v => v.Station).Select(v => v.Valve).ToList();
    }
}
