using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;

using CSharpFunctionalExtensions;

using IntersectUtilities.MPE.PlaceLongitudinalBlock;

using Norsyn.AecPropertySets;

using static IntersectUtilities.UtilsCommon.Utils;

using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities;

public partial class Intersect
{
    /// <command>PLACELONGITUDINALBLOCKFV</command>
    /// <summary>
    /// The FV_Fremtid half of PLACELONGITUDINALBLOCK, which starts it: AutoCAD runs it inside FV_Fremtid once
    /// PLACELONGITUDINALBLOCK has ended, so FV_Fremtid is the active document, locked by this command, and everything
    /// written is one UNDO step there. It places and sizes the blocks in one transaction and commits it, runs REGEN
    /// as a nested command so AutoCAD brings the sized dynamic blocks up to date, then trims the pipe at each sized
    /// block's ports in a second transaction (see PLACELONGITUDINALBLOCK for the trim rules); if AutoCAD refuses the
    /// REGEN, no pipe is trimmed and each report line says why. It
    /// prints the report, and when it has ended AutoCAD switches back to the længdeprofil and repeats the report
    /// there; a failure is reported the same way, and one UNDO in FV_Fremtid removes whatever it had written. Typed
    /// by hand with no placements waiting, it prints "intet at placere" and does nothing; placements waiting for
    /// another FV_Fremtid file are left waiting; placements whose længdeprofil has been closed are dropped with a
    /// message. Not added to the repeat-last-command list.
    /// </summary>
    /// <category>MPE</category>
    [CommandMethod("PLACELONGITUDINALBLOCKFV", CommandFlags.Modal | CommandFlags.NoHistory)]
    public void PlaceLongitudinalBlockFv()
    {
        Document document = AcadApp.DocumentManager.MdiActiveDocument;
        Maybe<FremtidJob> taken = Maybe<FremtidJob>.None;
        string report;

        try
        {
            Result<FremtidJob, AecError> job = FremtidHandoff.Take(document);
            taken = job.Match(found => Maybe.From(found), _ => Maybe<FremtidJob>.None);
            report = job.Match(found => FremtidBlockWriter.Write(document, found), error => error.Message);
        }
        catch (System.Exception exception)
        {
            // Last-resort guard of this command: a defect. The report and the return below still run.
            prdDbg(exception);
            report =
                $"{FremtidHandoff.CommandName} fejlede: {exception.Message}\n" +
                "Det, der nåede at blive skrevet i FV_Fremtid, fjernes med én UNDO i FV_Fremtid.";
        }

        document.Editor.WriteMessage($"\n{report}");
        taken.Tap(job => FremtidHandoff.ReturnToProfile(job, report));
    }
}
