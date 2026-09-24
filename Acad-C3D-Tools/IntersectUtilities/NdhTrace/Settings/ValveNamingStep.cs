using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// WHICH VALVE EACH VALVE IS. NDH picks a valve's Produkt from the drawing's
/// rule sheet, and the sheet names one valve for a whole system (the shipped
/// sheet: every steel valve is a Ventil). A legacy block that drew something
/// else - a VENTIL E is an Engangsventil, never a Ventil - is named on its own
/// valve through the modify door, exactly as the fitting deviations are
/// (authored-valves.md: the single-use valve is named on the one valve, never
/// by a row).
///
/// ONLY WHERE THE SHEET SAYS OTHERWISE. A valve the sheet already answers with
/// what the block drew is left on the sheet, so a later change of policy still
/// reaches it. "Already answers" is read conservatively: the FIRST row for the
/// valve's system must be the whole situation, unbanded, selecting that very
/// Produkt. A banded row might answer this valve and might not - its bore is
/// NDH's to measure - and naming a valve the sheet would have agreed with costs
/// nothing, where leaving one the sheet disagrees with costs the part.
///
/// IT RUNS AFTER THE PIPELINES STAND, because a valve cannot be named before it
/// exists: the name is the vertex NDH laid, the Valve cause kind, and the run.
/// ONE EDIT PER PIPELINE, as the deviations: a refusal leaves that pipeline's
/// valves on the sheet and says so.
/// </summary>
internal static class ValveNamingStep
{
    public static void Run(
        IReadOnlyDictionary<string, BuiltPipeline> built, INdhDrawingSettings settings,
        INdhPipelineModifier modifier, NdhImportReport report)
    {
        if (built.Values.All(p => p.Route.Valves.Count == 0)) return;
        IReadOnlyList<NdhFittingRule> sheet = settings.ReadFittingRules();

        int named = 0;
        int agreed = 0;
        int unnamed = 0;
        foreach ((string name, BuiltPipeline pipeline) in built)
        {
            List<NdhFittingOverride> choices = new List<NdhFittingOverride>();
            foreach (NdhRouteValve valve in pipeline.Route.Valves)
            {
                ulong id = pipeline.CauseAt(valve.VertexIndex);
                string system = NsDhModule.SystemToken(pipeline.Route.IdentityAt(valve.VertexIndex).System);
                foreach (NdhValveName said in valve.Names)
                {
                    if (Answers(sheet, system, said.Produkt))
                    {
                        agreed++;
                        continue;
                    }
                    if (id == 0)
                    {
                        //NAMED NOTHING RATHER THAN THE WRONG THING: without the
                        //vertex id there is no valve to write to.
                        unnamed++;
                        continue;
                    }
                    choices.Add(new NdhFittingOverride(
                        NdhComponent.Valve(id, said.Run), new NdhFittingProdukt(said.Produkt)));
                }
            }
            if (choices.Count == 0) continue;

            NdhModifyOutcome outcome = modifier.SetFittingChoices(pipeline.Handle, choices);
            if (!outcome.Success)
            {
                report.FittingReport.Add(
                    $"{name}: {choices.Count} ventil(er) kunne IKKE sættes - " +
                    $"{outcome.Status}: {outcome.Detail} Ventilerne står på reglerne.");
                continue;
            }
            named += choices.Count;
        }

        if (named > 0)
            report.FittingReport.Add(
                $"{named} ventil(er) sat enkeltvis, fordi den gamle tegning tegnede en anden " +
                "ventil end reglen.");
        if (agreed > 0)
            report.FittingReport.Add($"{agreed} ventil(er) fulgte allerede reglen.");
        if (unnamed > 0)
            report.FittingReport.Add(
                $"{unnamed} ventil(er) afveg fra reglen, men NDH oplyste intet knudepunkt " +
                "for dem - de står på reglen.");
    }

    /// <summary>
    /// Whether the sheet provably picks <paramref name="produkt"/> for a valve
    /// on <paramref name="system"/>: its first valve row there is unbanded and
    /// selects exactly that. First match wins on the sheet, so a later row is
    /// never the answer while an earlier one stands.
    /// </summary>
    private static bool Answers(IReadOnlyList<NdhFittingRule> sheet, string system, string produkt) =>
        sheet.Where(r => r.SystemToken == system && r.SituationToken == NdhSituation.Valve)
             .Take(1)
             .Any(r => r.Bands.Count == 0 && r.Selects.Produkt == produkt);
}
