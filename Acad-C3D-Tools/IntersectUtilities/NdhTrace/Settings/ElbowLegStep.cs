using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon.Enums;

using System.Collections.Generic;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// THE LEGS COME WITH THE BEND. A legacy variable-leg bend states two leg
/// lengths and they are the drawing's own fact about that one elbow - no
/// Produkt publishes them, and NDH's catalogue reach is not what the drafter
/// drew. They are written as the elbow's custom-leg override, which is exactly
/// what an override is for (owner 2026-09-21).
///
/// WHICH LEG IS WHICH IS DECIDED HERE, not read off the block. The block
/// carries each leg's world DIRECTION; the route says which way the lower- and
/// higher-station neighbours lie; the two together name the sides. A block
/// whose sides could not be established is reported and left on the catalogue,
/// because a swapped pair is silent and would be wrong on exactly half the
/// drawing.
/// </summary>
internal static class ElbowLegStep
{
    public static void Run(
        IReadOnlyDictionary<string, BuiltPipeline> built,
        INdhPipelineModifier modifier, NdhImportReport report)
    {
        int written = 0;
        List<string> refused = new List<string>();
        foreach ((string name, BuiltPipeline pipeline) in built)
        {
            Giving giving = new Giving(pipeline, refused);
            for (int i = 0; i < pipeline.Route.VertexLegs.Count; i++) giving.Vertex(i);
            if (giving.Legs.Count == 0) continue;

            NdhModifyOutcome outcome = modifier.SetElbowLegs(pipeline.Handle, giving.Legs);
            if (!outcome.Success)
            {
                report.FittingReport.Add(
                    $"{name}: bentlængderne på {giving.Legs.Count} bøjning(er) kunne IKKE " +
                    $"skrives - {outcome.Status}: {outcome.Detail}");
                continue;
            }
            written += giving.Legs.Count;
        }

        if (written > 0)
            report.FittingReport.Add($"{written} bøjning(er) fik den gamle tegnings egne bentlængder.");
        foreach (string line in refused) report.FittingReport.Add(line);
    }

    /// <summary>
    /// ONE PIPELINE, VERTEX BY VERTEX. It is the audience the corner's legs
    /// talk to, so "states none" and "states two nobody can place" arrive as
    /// two different calls and cannot be confused for each other.
    /// </summary>
    private sealed class Giving : ILegAudience
    {
        //A leg direction has to point at its neighbour, not merely away from
        //the other leg. Half a degree of drawing slop is generous; a route
        //whose corner turns less than that is not a corner at all.
        private const double MinAlignment = 0.01;

        private readonly BuiltPipeline pipeline;
        private readonly List<string> refused;
        private int vertex;

        public Giving(BuiltPipeline pipeline, List<string> refused)
        {
            this.pipeline = pipeline;
            this.refused = refused;
        }

        public List<NdhElbowLegs> Legs { get; } = new List<NdhElbowLegs>();

        public void Vertex(int index)
        {
            vertex = index;
            pipeline.Route.VertexLegs[index].Tell(
                pipeline.Route.VertexCauseHandles[index], this);
        }

        public void None() { }

        public void Unplaceable(string handle, string why) =>
            refused.Add($"Bøjning {handle}: {why} - bentlængderne er ikke skrevet, " +
                        "bøjningen står på katalogets egen rækkevidde.");

        public void Drawn(LegacyLeg a, LegacyLeg b)
        {
            ulong id = pipeline.CauseAt(vertex);
            if (id == 0 || vertex == 0 || vertex + 1 >= pipeline.Route.Vertices.Count)
            {
                refused.Add($"Bøjning {pipeline.Route.VertexCauseHandles[vertex]}: ligger ikke " +
                            "på et hjørne NDH har navngivet - bentlængderne er ikke skrevet.");
                return;
            }

            Vector2d toLow = Toward(vertex - 1);
            Vector2d toHigh = Toward(vertex + 1);
            double straight = a.Direction.DotProduct(toLow) + b.Direction.DotProduct(toHigh);
            double crossed = a.Direction.DotProduct(toHigh) + b.Direction.DotProduct(toLow);
            if (System.Math.Abs(straight - crossed) < MinAlignment)
            {
                refused.Add($"Bøjning {pipeline.Route.VertexCauseHandles[vertex]}: blokkens ben " +
                            "peger ikke entydigt mod hver sin nabo - bentlængderne er ikke skrevet.");
                return;
            }

            LegacyLeg lo = straight > crossed ? a : b;
            LegacyLeg hi = straight > crossed ? b : a;
            foreach (NdhRun run in NsDhModule.RunsOf(ConstructionAt(vertex)))
                Legs.Add(new NdhElbowLegs(
                    new NdhComponent(id, NdhCause.Elbow, run), lo.LengthM * 1000.0, hi.LengthM * 1000.0));
        }

        /// <summary>The unit direction from this vertex toward a neighbouring one.</summary>
        private Vector2d Toward(int other)
        {
            NdhRouteVertex here = pipeline.Route.Vertices[vertex];
            NdhRouteVertex there = pipeline.Route.Vertices[other];
            Vector2d v = new Vector2d(there.X - here.X, there.Y - here.Y);
            return v.Length > 0.0 ? v / v.Length : v;
        }

        private PipeTypeEnum ConstructionAt(int index)
        {
            PipeTypeEnum type = pipeline.Route.Boundaries[0].Type;
            foreach (NdhIdentityBoundary b in pipeline.Route.Boundaries)
                if (b.VertexIndex <= index) type = b.Type;
            return type;
        }
    }
}
