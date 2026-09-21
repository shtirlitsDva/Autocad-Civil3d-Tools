using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// Sets the working drawing's producer and series matrix from the legacy
/// drawing's facts (legacy-fjv-import.md settings-not-pins), BEFORE any
/// pipeline is built so no family has to re-derive (contract D7).
///
/// Every question is asked before anything is written: a cancelled dialog
/// leaves the drawing untouched.
///
/// - Producer (contract D8): no producer named - keep the drawing's own and say
///   so; exactly one - set it; two - the drafter chooses.
/// - Fittings: the census's majority part per pipe system and situation becomes
///   ONE unbanded rule row, and the systems it speaks for have their rows
///   REPLACED. A system the old drawing does not draw keeps its seed rows. The
///   components that disagree with the fitted policy are named individually
///   after their pipelines are built, not here.
/// - Series: a size drawn in one series gets it; a size drawn in several is
///   settled by the drafter; a size whose pipes' series cannot be read is left
///   to the drawing and reported. The matrix export replaces a system's whole
///   grid (D6), so the drawing's own cells for sizes the legacy drawing does
///   not draw are sent along unchanged: the legacy drawing only speaks for the
///   sizes it has. A cell the catalogue cannot serve is dropped, reported, and
///   the rest is sent again.
/// </summary>
internal static class DrawingSettingsStep
{
    public static bool Apply(
        LegacySettingsFacts facts, INdhDrawingSettings settings, IImportDialogs dialogs,
        NdhImportReport report, out FittedSheet fitted)
    {
        //THE POLICY THE OLD DRAWING IMPLIES, worked out before any question is
        //asked, so a cancelled dialog leaves it unwritten like everything else.
        //It is handed back whatever happens: the components that disagree with
        //it are named after their pipelines stand, which is not here.
        fitted = facts.Fittings.Fit();

        //1. Decide.
        string? producer = null;
        string producerLine;
        switch (facts.Producers.Count)
        {
            case 0:
                producerLine = $"Den gamle tegning nævner ingen producent (kun GLD/uden navn); " +
                    $"tegningens producent {settings.ReadProducer()} beholdes.";
                break;
            case 1:
                producer = facts.Producers.Keys.Single();
                producerLine = $"{producer} sat (den gamle tegning: " +
                    $"{LegacySettingsFacts.Describe(facts.Producers[producer])}).";
                break;
            default:
                producer = dialogs.ChooseProducer(facts.Producers);
                if (producer == null)
                {
                    report.Cancelled = "Producentvalget blev annulleret; intet er ændret.";
                    return false;
                }
                producerLine = $"{producer} valgt i dialogen (den gamle tegning nævner " +
                    string.Join("; ", facts.Producers.Select(x => $"{x.Key}: {LegacySettingsFacts.Describe(x.Value)}")) + ").";
                break;
        }

        Dictionary<SeriesKey, PipeSeriesEnum> decided = new Dictionary<SeriesKey, PipeSeriesEnum>();
        List<AmbiguousSize> ambiguous = new List<AmbiguousSize>();
        foreach ((SeriesKey key, SortedDictionary<PipeSeriesEnum, SeriesTally> seen) in facts.Series)
        {
            List<PipeSeriesEnum> defined = seen.Keys.Where(s => s != PipeSeriesEnum.Undefined).ToList();
            if (defined.Count == 1) decided[key] = defined[0];
            else if (defined.Count > 1) ambiguous.Add(new AmbiguousSize(key, seen));
        }

        HashSet<SeriesKey> settledInDialog = new HashSet<SeriesKey>();
        if (ambiguous.Count > 0)
        {
            IReadOnlyDictionary<SeriesKey, PipeSeriesEnum>? settled = dialogs.SettleSeries(ambiguous);
            if (settled == null)
            {
                report.Cancelled = "Seriedialogen blev annulleret; intet er ændret.";
                return false;
            }
            foreach ((SeriesKey key, PipeSeriesEnum series) in settled)
            {
                decided[key] = series;
                settledInDialog.Add(key);
            }
        }

        //2. Write.
        //ONTO A COPY, NEVER THE DRAFTER'S OWN. An import states a whole
        //drawing's policy - producer, series, rule sheet - and doing that to
        //the profile the drafter set up would overwrite work nobody asked us
        //to touch. On a copy they switch back and their drawing is as they
        //left it. The register uniquifies the name, so a second import makes a
        //second profile rather than eating the first.
        NdhSettingsOutcome profile = settings.UseSettingsProfileCopy("FJV-import");
        if (!profile.Success)
        {
            report.Cancelled =
                $"Indstillingsprofilen kunne ikke oprettes: {profile.Detail} Intet er ændret.";
            return false;
        }
        report.ProducerReport.Add(
            $"Indstillingerne skrives til profilen '{profile.Detail}'; " +
            "tegningens egen profil står urørt og kan vælges igen.");

        if (producer != null)
            Require(settings.SetProducer(producer), $"Producenten {producer}");
        report.ProducerReport.Add(producerLine);
        foreach ((string navn, int n) in facts.IgnoredProducerParts)
            report.ProducerReport.Add($"Ignoreret (NDH's producent gælder stål): {navn} ×{n}.");

        HashSet<SeriesKey> dropped = WriteSeries(decided, settings, report);
        WriteFittingRules(fitted, settings, report);

        //3. Report every size the legacy drawing draws.
        foreach ((SeriesKey key, SortedDictionary<PipeSeriesEnum, SeriesTally> seen) in facts.Series)
        {
            string legacy = string.Join(", ", seen.Select(x => $"{Name(x.Key)}: {x.Value}"));
            if (!decided.TryGetValue(key, out PipeSeriesEnum series))
                report.SeriesReport.Add($"{key}: serien kunne ikke læses ({legacy}) - tegningens egen bruges.");
            else if (dropped.Contains(key))
                report.SeriesReport.Add($"{key}: {Name(series)} findes ikke i kataloget - udeladt, katalogets standard bruges ({legacy}).");
            else if (settledInDialog.Contains(key))
                report.SeriesReport.Add($"{key}: {Name(series)} valgt i dialogen ({legacy}).");
            else
                report.SeriesReport.Add($"{key}: {Name(series)} ({legacy}).");
        }
        return true;
    }

    /// <summary>
    /// Writes the fitted policy ON TOP OF the rows already there, deleting
    /// nothing. First match wins, so a fitted row beats the seed row it
    /// competes with and the seed rows stay behind it, answering every
    /// situation the old drawing never mentioned.
    ///
    /// This used to REPLACE the rows of every system the old drawing spoke
    /// for. That cost the drawing every seed row for every situation the old
    /// drawing did not happen to contain, and the import filled with rule-miss
    /// complaints about corners the defaults had always covered.
    ///
    /// A REFUSAL IS REPORTED AND NOTHING IS RETRIED. The series matrix drops a
    /// cell the catalogue cannot serve and sends the rest, because a cell is a
    /// fact about one size and the others are still true. A rule sheet is not
    /// like that: the rows are an ORDERED policy, and sending it minus the row
    /// that refused would leave the drawing governed by a policy nobody wrote.
    /// So the drawing keeps the policy it had, and the report says which row
    /// and why.
    /// </summary>
    private static void WriteFittingRules(
        FittedSheet fitted, INdhDrawingSettings settings, NdhImportReport report)
    {
        foreach (string line in fitted.Report) report.FittingReport.Add(line);
        if (fitted.Rows.Count == 0)
        {
            report.FittingReport.Add(
                "Den gamle tegning afgør ingen fittingregler - tegningens egne regler beholdes.");
            return;
        }

        NdhSettingsOutcome outcome = settings.AddFittingRules(fitted.Rows);
        if (outcome.Success)
        {
            report.FittingReport.Add(
                $"{fitted.Rows.Count} fittingregel(ler) tilføjet forrest i reglerne; " +
                "tegningens øvrige regler er beholdt og gælder fortsat de situationer, " +
                "den gamle tegning ikke nævner.");
            return;
        }

        string which = outcome.CellIndex >= 0 && outcome.CellIndex < fitted.Rows.Count
            ? $"regel {outcome.CellIndex} ({fitted.Rows[outcome.CellIndex].SystemToken}/" +
              $"{fitted.Rows[outcome.CellIndex].SituationToken})"
            : "reglerne";
        report.FittingReport.Add(
            $"Fittingreglerne blev IKKE skrevet - {which} blev afvist: {outcome.Detail} " +
            "Tegningens egne regler er urørte.");
    }

    /// <summary>Writes the decided cells over the drawing's own; returns the cells the catalogue refused.</summary>
    private static HashSet<SeriesKey> WriteSeries(
        Dictionary<SeriesKey, PipeSeriesEnum> decided, INdhDrawingSettings settings, NdhImportReport report)
    {
        HashSet<SeriesKey> dropped = new HashSet<SeriesKey>();
        if (decided.Count == 0) return dropped;

        HashSet<string> systems = decided.Keys.Select(k => k.SystemToken).ToHashSet(StringComparer.Ordinal);
        HashSet<(string, string, int)> decidedCells = decided.Keys
            .Select(k => (k.SystemToken, k.TypeToken, k.Dn)).ToHashSet();

        List<NdhSeriesCell> kept = settings.ReadSeriesMatrix()
            .Where(c => systems.Contains(c.SystemToken) && !decidedCells.Contains((c.SystemToken, c.TypeToken, c.Dn)))
            .ToList();
        List<(NdhSeriesCell Cell, SeriesKey? Key)> cells = kept.Select(c => (c, (SeriesKey?)null))
            .Concat(decided.Select(x => (new NdhSeriesCell(x.Key.SystemToken, x.Key.TypeToken, x.Key.Dn, (int)x.Value), (SeriesKey?)x.Key)))
            .ToList();

        //Each round either succeeds or drops one cell, so this ends.
        while (cells.Count > 0)
        {
            NdhSettingsOutcome outcome = settings.SetSeriesMatrix(cells.Select(x => x.Cell).ToList());
            if (outcome.Success) break;
            if (outcome.Status != NdhSettingsStatus.Unservable || outcome.CellIndex < 0 || outcome.CellIndex >= cells.Count)
                throw new InvalidOperationException(
                    $"Seriematricen kunne ikke sættes ({outcome.Status}): {outcome.Detail}");

            (NdhSeriesCell cell, SeriesKey? key) = cells[outcome.CellIndex];
            cells.RemoveAt(outcome.CellIndex);
            if (key is SeriesKey k) dropped.Add(k);
            else report.SeriesReport.Add(
                $"{Display(cell)}: tegningens egen serie S{cell.Series} " +
                $"afvises af kataloget - udeladt ({outcome.Detail}).");
        }

        if (kept.Count > 0)
            report.SeriesReport.Add(
                $"Tegningens egne valg for størrelser den gamle tegning ikke har, er bevaret ({kept.Count}).");
        return dropped;
    }

    private static void Require(NdhSettingsOutcome outcome, string what)
    {
        if (!outcome.Success)
            throw new InvalidOperationException(
                $"{what} kunne ikke sættes ({outcome.Status}): {outcome.Detail}");
    }

    /// <summary>A cell read back from the drawing, named as the drafter reads it (F7).</summary>
    private static string Display(NdhSeriesCell cell) =>
        NsDhModule.TrySystemOfToken(cell.SystemToken, out PipeSystemEnum system)
            ? SeriesKey.Display(system, cell.TypeToken == NsDhModule.TypeToken(true), cell.Dn)
            : $"{cell.SystemToken} {cell.TypeToken} {cell.Dn}";

    private static string Name(PipeSeriesEnum s) => s == PipeSeriesEnum.Undefined ? "ukendt serie" : s.ToString();
}
