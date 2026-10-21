using System;
using System.Collections.Generic;
using System.Linq;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.PlanProduction;

// Bookkeeping for FINALIZESHEETSAUTO / FINALIZESHEETSAHK: which step was running when finalization
// stopped, and which steps never ran. It only observes the steps and never changes what they do.
// Most steps print their own errors and return silently, so "ran" means the step did not stop
// finalization, not that it found nothing wrong.

internal sealed record FinalizeStep(string Name)
{
    public static readonly FinalizeStep ResetProfileViews = new("Reset profile views");
    public static readonly FinalizeStep ImportCivilStyles = new("Import Civil styles");
    public static readonly FinalizeStep PrepareProfileViewStyle = new("Set NO SCALE profile view style");
    public static readonly FinalizeStep LoadSurface = new("Load surface");
    public static readonly FinalizeStep CreateLerData = new("Create LER points and project them");
    public static readonly FinalizeStep PopulateProfiles = new("Populate profiles with LER");
    public static readonly FinalizeStep StyleProfileViews = new("Style profile views, bands, MLeaders, profiles");
    public static readonly FinalizeStep CreateDetailing = new("Create detailing");
    public static readonly FinalizeStep StaggerLabels = new("Stagger projection labels");
    public static readonly FinalizeStep ViewportRectangles = new("Draw viewport rectangles");
    public static readonly FinalizeStep ColorizeLerLayers = new("Colorize LER layers");
    public static readonly FinalizeStep GermanProjectionLabels = new("Replace projection labels with NPPLs (DE)");

    public static IReadOnlyList<FinalizeStep> Sequence(bool german)
    {
        List<FinalizeStep> steps =
        [
            ResetProfileViews,
            ImportCivilStyles,
            PrepareProfileViewStyle,
            LoadSurface,
            CreateLerData,
            PopulateProfiles,
            StyleProfileViews,
            CreateDetailing,
            StaggerLabels,
            ViewportRectangles,
            ColorizeLerLayers,
        ];
        if (german) steps.Add(GermanProjectionLabels);
        return steps;
    }
}

/// <summary>
/// What happened to one step. Closed: the cases are nested, nothing outside can add one, and every
/// case answers Match itself, so a new case breaks the build wherever it is not handled yet.
/// </summary>
internal abstract record StepOutcome
{
    private StepOutcome() { }

    public abstract TOut Match<TOut>(
        Func<TOut> notRun, Func<TOut> ran, Func<TOut> stopped, Func<string, TOut> crashed);

    public static readonly StepOutcome NotRunYet = new NotRun();
    public static readonly StepOutcome RanThrough = new Ran();
    public static readonly StepOutcome StoppedFinalization = new Stopped();
    public static StepOutcome CrashedWith(string message) => new Crashed(message);

    internal sealed record NotRun : StepOutcome
    {
        public override TOut Match<TOut>(
            Func<TOut> notRun, Func<TOut> ran, Func<TOut> stopped, Func<string, TOut> crashed) => notRun();
    }

    internal sealed record Ran : StepOutcome
    {
        public override TOut Match<TOut>(
            Func<TOut> notRun, Func<TOut> ran, Func<TOut> stopped, Func<string, TOut> crashed) => ran();
    }

    /// <summary>The step made finalization return false; the reason was printed by the step itself.</summary>
    internal sealed record Stopped : StepOutcome
    {
        public override TOut Match<TOut>(
            Func<TOut> notRun, Func<TOut> ran, Func<TOut> stopped, Func<string, TOut> crashed) => stopped();
    }

    internal sealed record Crashed(string Message) : StepOutcome
    {
        public override TOut Match<TOut>(
            Func<TOut> notRun, Func<TOut> ran, Func<TOut> stopped, Func<string, TOut> crashed) => crashed(Message);
    }
}

internal sealed class FinalizeSheetsReport
{
    private readonly IReadOnlyList<FinalizeStep> steps;
    private readonly StepOutcome[] outcomes;
    private readonly string[] notes;
    private int current = -1;

    public FinalizeSheetsReport(IReadOnlyList<FinalizeStep> steps)
    {
        this.steps = steps;
        outcomes = Enumerable.Repeat(StepOutcome.NotRunYet, steps.Count).ToArray();
        notes = Enumerable.Repeat(string.Empty, steps.Count).ToArray();
    }

    /// <summary>Marks the step that is running now; the previous step ran through.</summary>
    public void Begin(FinalizeStep step, string note = "")
    {
        int index = IndexOf(step);
        Mark(StepOutcome.RanThrough);
        current = index;
        notes[index] = note;
    }

    public void Finished() => Mark(StepOutcome.RanThrough);

    public void Stopped() => Mark(StepOutcome.StoppedFinalization);

    public void Crashed(System.Exception ex) => Mark(StepOutcome.CrashedWith($"{ex.GetType().Name}: {ex.Message}"));

    public void Print()
    {
        prdDbg("");
        prdDbg("========== FINALIZE SHEETS: STEP REPORT ==========");
        for (int i = 0; i < steps.Count; i++)
        {
            string note = notes[i].Length > 0 ? $" ({notes[i]})" : string.Empty;
            string line = outcomes[i].Match(
                notRun: () => $"[ --  ] {i + 1,2}. {steps[i].Name}{note} - not run",
                ran: () => $"[ ran ] {i + 1,2}. {steps[i].Name}{note}",
                stopped: () => $"[STOP ] {i + 1,2}. {steps[i].Name}{note} - stopped finalization, see the message above",
                crashed: message => $"[CRASH] {i + 1,2}. {steps[i].Name}{note} - {message}");
            prdDbg(line);
        }

        int halted = Array.FindIndex(outcomes, IsHalt);
        if (halted >= 0)
            prdDbg($"Finalization stopped at step {halted + 1} ({steps[halted].Name}). The steps after it did not run.");
        else
            prdDbg("All steps ran.");

        bool crashed = outcomes.Any(o => o.Match(() => false, () => false, () => false, _ => true));
        if (crashed)
            prdDbg("The crash text is what escaped the step. An error the step printed above this report "
                + "may be the original cause.");

        prdDbg("[ ran ] only means the step did not stop finalization. Steps print their own errors above "
            + "this report and can skip work without stopping.");
        prdDbg("==================================================");
    }

    private static bool IsHalt(StepOutcome outcome) =>
        outcome.Match(notRun: () => false, ran: () => false, stopped: () => true, crashed: _ => true);

    private void Mark(StepOutcome outcome)
    {
        if (current >= 0) outcomes[current] = outcome;
    }

    private int IndexOf(FinalizeStep step)
    {
        for (int i = 0; i < steps.Count; i++)
            if (ReferenceEquals(steps[i], step)) return i;
        throw new InvalidOperationException(
            $"Finalize step \"{step.Name}\" is not in this run's step sequence.");
    }
}
