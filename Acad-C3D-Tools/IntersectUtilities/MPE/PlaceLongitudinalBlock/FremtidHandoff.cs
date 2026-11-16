using Autodesk.AutoCAD.ApplicationServices;

using CSharpFunctionalExtensions;

using Norsyn.AecPropertySets;

using static IntersectUtilities.UtilsCommon.Utils;

using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities.MPE.PlaceLongitudinalBlock;

/// <summary>
/// The placements PLACELONGITUDINALBLOCK collected in a længdeprofil, waiting for PLACELONGITUDINALBLOCKFV to write
/// them inside FV_Fremtid. Profile is the længdeprofil document: its alignments size the blocks, and AutoCAD returns
/// to it.
/// </summary>
internal sealed record FremtidJob(
    Guid Id,
    string FremtidPath,
    Document Profile,
    IReadOnlyList<LongitudinalBlockPick> Picks,
    string BlockName);

/// <summary>
/// Moves a job from the længdeprofil into FV_Fremtid and back, as a multi-document command: the længdeprofil's
/// command stores the job and ends; when AutoCAD is idle, FV_Fremtid is activated and PLACELONGITUDINALBLOCKFV queued
/// to it, so the write runs as FV_Fremtid's own command (its lock, its one UNDO step, its screen); when that
/// command has ended, AutoCAD returns to the længdeprofil. Document switches happen only from Application.Idle,
/// never inside a command, which a switch would cancel.
/// </summary>
internal static class FremtidHandoff
{
    internal const string CommandName = "PLACELONGITUDINALBLOCKFV";

    // One job at a time. Static because NSLOAD creates a new Intersect for every command call.
    private static Maybe<FremtidJob> pending = Maybe<FremtidJob>.None;

    /// <summary>
    /// FV_Fremtid, open in this session for writing and not busy with a command; otherwise why the command cannot
    /// write in it.
    /// </summary>
    internal static Result<Document, AecError> OpenForWriting(string fremtidPath) =>
        FindOpen(fremtidPath)
            .ToResult(AecError.Drawing(
                $"FV_Fremtid er ikke åben i denne Civil 3D. Åbn den til skrivning og kør kommandoen igen:\n{fremtidPath}"))
            .Bind(document => document.IsReadOnly
                ? Result.Failure<Document, AecError>(AecError.Drawing(
                    $"FV_Fremtid er åbnet skrivebeskyttet. Åbn den til skrivning og kør kommandoen igen:\n{fremtidPath}"))
                : Result.Success<Document, AecError>(document))
            .Bind(document => Maybe.From(document.CommandInProgress)
                .Where(command => command.Length > 0)
                .Match(
                    command => Result.Failure<Document, AecError>(AecError.Drawing(
                        $"FV_Fremtid er optaget af kommandoen {command}. Afslut den og kør kommandoen igen:\n{fremtidPath}")),
                    () => Result.Success<Document, AecError>(document)));

    /// <summary>
    /// Stores the job and arranges for FV_Fremtid to run it once the calling command has ended. Returns the line the
    /// længdeprofil shows; nothing is stored when FV_Fremtid cannot take the job.
    /// </summary>
    internal static string Submit(FremtidJob job) =>
        OpenForWriting(job.FremtidPath).Match(
            _ =>
            {
                bool replaced = pending.HasValue;
                pending = Maybe.From(job);
                WhenIdle(() => Start(job.Id), exception => Abandon(job, exception));
                return
                    $"{job.Picks.Count} placering(er) af {job.BlockName} sendes til FV_Fremtid. AutoCAD skifter til " +
                    "FV_Fremtid, placerer og trimmer dér og vender så tilbage hertil." +
                    (replaced ? "\nEt tidligere job, der ikke var nået frem til FV_Fremtid, er erstattet." : string.Empty);
            },
            error => $"Intet skrevet i FV_Fremtid. {error.Message}");

    /// <summary>
    /// The waiting job, taken for <paramref name="document"/>. A failure when none waits; when it is for another
    /// drawing, which leaves it waiting; or when its længdeprofil has been closed, which drops it, as its alignments
    /// can no longer be read.
    /// </summary>
    internal static Result<FremtidJob, AecError> Take(Document document) =>
        pending
            .ToResult(AecError.Drawing("intet at placere"))
            .Bind(job => IsFile(document, job.FremtidPath)
                ? Result.Success<FremtidJob, AecError>(job)
                : Result.Failure<FremtidJob, AecError>(AecError.Drawing(
                    $"Placeringerne venter på {job.FremtidPath}, ikke på denne tegning.")))
            .Tap(_ => pending = Maybe<FremtidJob>.None)
            .Bind(job => ProfileOf(job).Match(
                _ => Result.Success<FremtidJob, AecError>(job),
                () => Result.Failure<FremtidJob, AecError>(AecError.Drawing(
                    "Længdeprofilen, placeringerne blev valgt i, er lukket. Intet skrevet i FV_Fremtid."))));

    /// <summary>
    /// Once the running command has ended: activates the længdeprofil again and repeats the report there. A
    /// længdeprofil closed meanwhile gets nothing; the report is in FV_Fremtid already.
    /// </summary>
    internal static void ReturnToProfile(FremtidJob job, string report) =>
        WhenIdle(
            () => ProfileOf(job).Tap(profile =>
            {
                UnitResult<AecError> activated = AutoCadCall.Do(
                    "skift tilbage til længdeprofilen", () => AcadApp.DocumentManager.MdiActiveDocument = profile);
                profile.Editor.WriteMessage($"\n{report}");
                activated.TapError(error => profile.Editor.WriteMessage($"\n{error.Message}"));
                profile.Editor.PostCommandPrompt();
            }),
            // The report is in FV_Fremtid already; the guard's debug print is all a defect here adds.
            _ => { });

    /// <summary>
    /// A defect while starting the job: drops it, so no later run of the FV command finds it, and says so in the
    /// længdeprofil.
    /// </summary>
    private static void Abandon(FremtidJob job, System.Exception exception)
    {
        pending = pending.Where(waiting => waiting.Id != job.Id);
        ReturnToProfile(job, $"Intet skrevet i FV_Fremtid. {CommandName} kunne ikke startes: {exception.Message}");
    }

    /// <summary>
    /// Activates FV_Fremtid, then queues the FV command to it. The job is out of the slot while this runs and goes
    /// back only once FV_Fremtid is active, so a failed switch leaves nothing for a later run of the FV command to
    /// find; the failure is reported in the længdeprofil. A job replaced in the meantime is left to its own start.
    /// </summary>
    private static void Start(Guid jobId) =>
        pending
            .Where(job => job.Id == jobId)
            .Tap(job =>
            {
                pending = Maybe<FremtidJob>.None;
                OpenForWriting(job.FremtidPath)
                    .Bind(Activate)
                    .Tap(_ => pending = Maybe.From(job))
                    .Bind(fremtid => AutoCadCall.Do(
                        $"send {CommandName} til FV_Fremtid",
                        () => fremtid.SendStringToExecute($"_{CommandName} ", false, false, false)))
                    .TapError(error =>
                    {
                        pending = Maybe<FremtidJob>.None;
                        ReturnToProfile(job, $"Intet skrevet i FV_Fremtid. {error.Message}");
                    });
            });

    /// <summary>FV_Fremtid made the active document; a failure when AutoCAD refuses or ignores the switch.</summary>
    private static Result<Document, AecError> Activate(Document fremtid) =>
        AcadApp.DocumentManager.DocumentActivationEnabled
            ? AutoCadCall
                .Do("skift til FV_Fremtid", () => AcadApp.DocumentManager.MdiActiveDocument = fremtid)
                .Bind(() => ReferenceEquals(AcadApp.DocumentManager.MdiActiveDocument, fremtid)
                    ? Result.Success<Document, AecError>(fremtid)
                    : Result.Failure<Document, AecError>(AecError.Drawing("AutoCAD skiftede ikke til FV_Fremtid.")))
            : Result.Failure<Document, AecError>(AecError.Drawing("AutoCAD tillader ikke at skifte tegning lige nu."));

    /// <summary>The job's længdeprofil, while it is still open.</summary>
    private static Maybe<Document> ProfileOf(FremtidJob job) =>
        OpenDocuments().TryFirst(document => ReferenceEquals(document, job.Profile));

    /// <summary>
    /// Runs <paramref name="action"/> once, in the application context, on the first Idle with no command, LISP or
    /// ARX command active. A defect it throws goes to the debug print and then to <paramref name="onDefect"/>.
    /// </summary>
    private static void WhenIdle(Action action, Action<System.Exception> onDefect)
    {
        void OnIdle(object? sender, EventArgs e)
        {
            if (!AcadApp.IsQuiescent)
            {
                return;
            }

            AcadApp.Idle -= OnIdle;
            try
            {
                action();
            }
            catch (System.Exception exception)
            {
                // Last-resort guard of this Idle entry point.
                prdDbg(exception);
                onDefect(exception);
            }
        }

        AcadApp.Idle += OnIdle;
    }

    private static Maybe<Document> FindOpen(string fremtidPath) =>
        OpenDocuments().TryFirst(document => IsFile(document, fremtidPath));

    private static bool IsFile(Document document, string path) =>
        Maybe.From(document.Database)
            .Map(database => database.Filename)
            .Where(filename => filename.Length > 0)
            .Match(filename => SameFile.Matches(filename, path), () => false);

    private static IEnumerable<Document> OpenDocuments() =>
        AcadApp.DocumentManager.Cast<Document>();
}
