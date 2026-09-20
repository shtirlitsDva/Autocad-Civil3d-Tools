using IntersectUtilities.UtilsCommon.Enums;

using System.Collections.Generic;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// THE OTHER HALF OF THE FITTED SHEET. The census fits the drawing's rule sheet
/// to what the old drawing drew MOST OFTEN; every block that drew something
/// else is named here, on the component it became. Both halves, or neither: a
/// fitted policy without its deviations is not a translation of the old
/// drawing, it is a summary of it, and the import is a translation
/// (legacy-fjv-import.md, the-fitting-sheet-is-fitted-too).
///
/// IT RUNS AFTER THE PIPELINES STAND, because a component cannot be named
/// before it exists: the name is the vertex NDH laid, the kind of component the
/// vertex caused, and the run it stands on. The vertex comes back from the
/// build, the kind rides with the census's count, and the run comes from the
/// construction the route carries at that vertex.
///
/// ONE EDIT PER PIPELINE. A refusal therefore leaves that pipeline entirely
/// unnamed rather than half-named, and says so; the other pipelines are
/// unaffected, because each is its own edit.
/// </summary>
internal static class FittingDeviationStep
{
    public static void Run(
        FittedSheet fitted, IReadOnlyDictionary<string, BuiltPipeline> built,
        INdhPipelineModifier modifier, NdhImportReport report)
    {
        int named = 0;
        int agreed = 0;
        int unnamed = 0;
        foreach ((string name, BuiltPipeline pipeline) in built)
        {
            Naming naming = new Naming(pipeline);
            for (int i = 0; i < pipeline.Route.VertexCauseHandles.Count; i++)
                naming.Vertex(i, fitted);

            agreed += naming.Agreed;
            unnamed += naming.Unnamed;
            if (naming.Choices.Count == 0) continue;

            NdhModifyOutcome outcome = modifier.SetFittingChoices(pipeline.Handle, naming.Choices);
            if (!outcome.Success)
            {
                report.FittingReport.Add(
                    $"{name}: {naming.Choices.Count} komponent(er) kunne IKKE sættes - " +
                    $"{outcome.Status}: {outcome.Detail} Hele rørledningen står på reglerne.");
                continue;
            }
            named += naming.Choices.Count;
        }

        if (named > 0)
            report.FittingReport.Add(
                $"{named} komponent(er) sat enkeltvis, fordi den gamle tegning tegnede noget " +
                "andet end reglen.");
        if (agreed > 0)
            report.FittingReport.Add($"{agreed} komponent(er) fulgte allerede reglen.");
        if (unnamed > 0)
            report.FittingReport.Add(
                $"{unnamed} komponent(er) afveg fra reglen, men NDH oplyste intet knudepunkt " +
                "for dem - de står på reglen.");
    }

    /// <summary>
    /// ONE PIPELINE, VERTEX BY VERTEX. It is the audience the sheet talks to,
    /// so the three answers are three methods and there is no verdict value to
    /// read back and misread.
    /// </summary>
    private sealed class Naming : IDeviationAudience
    {
        private readonly BuiltPipeline pipeline;
        private int vertex;

        public Naming(BuiltPipeline pipeline) => this.pipeline = pipeline;

        public List<NdhFittingOverride> Choices { get; } = new List<NdhFittingOverride>();
        public int Agreed { get; private set; }
        public int Unnamed { get; private set; }

        public void Vertex(int index, FittedSheet fitted)
        {
            vertex = index;
            fitted.TellDeviation(pipeline.Route.VertexCauseHandles[index], this);
        }

        //A vertex no counted block made - an end, a bend the centreline turns
        //itself, a vertex a boundary asked for. The census has nothing to say
        //about it and neither has this.
        public void NotCounted() { }

        public void Agrees() => Agreed++;

        public void Overrides(NdhCause cause, string produkt)
        {
            ulong id = pipeline.CauseAt(vertex);
            if (id == 0 || pipeline.Route.Boundaries.Count == 0)
            {
                //NAMED NOTHING RATHER THAN THE WRONG THING. Without the vertex
                //id there is no component to write to, and a guess would file
                //the drafter's part under a name that resolves to somebody
                //else's component.
                Unnamed++;
                return;
            }

            foreach (NdhRun run in NsDhModule.RunsOf(ConstructionAt(vertex)))
                Choices.Add(new NdhFittingOverride(new NdhComponent(id, cause, run),
                                                   new NdhFittingProdukt(produkt)));
        }

        /// <summary>
        /// What the pipe IS at this vertex: the last boundary at or before it.
        /// The route states its identity in boundaries rather than per vertex,
        /// so this is the reading, not an inference.
        /// </summary>
        private PipeTypeEnum ConstructionAt(int index)
        {
            PipeTypeEnum type = pipeline.Route.Boundaries[0].Type;
            foreach (NdhIdentityBoundary b in pipeline.Route.Boundaries)
                if (b.VertexIndex <= index) type = b.Type;
            return type;
        }
    }
}
