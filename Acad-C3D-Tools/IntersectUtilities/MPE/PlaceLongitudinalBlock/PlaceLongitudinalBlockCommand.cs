using System.IO;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

using IntersectUtilities.MPE.PlaceLongitudinalBlock;
using IntersectUtilities.UtilsCommon.DataManager;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities;

public partial class Intersect
{
    /// <command>PLACELONGITUDINALBLOCK</command>
    /// <summary>
    /// Places a component block in the FV_Fremtid plan drawing from a click in a længdeprofil: the station is
    /// resolved from the picked point and a component is chosen from FJV Dynamiske Komponenter.csv. Answer Multiple
    /// to pick all locations first and place the same block at each. The block is inserted loose on layer
    /// 0-KOMPONENT - the run polyline is not cut and no welds are generated.
    /// </summary>
    /// <category>MPE</category>
    [CommandMethod("PLACELONGITUDINALBLOCK", CommandFlags.Modal)]
    public void PlaceLongitudinalBlock()
    {
        Document? document = GetActiveDocument();
        if (document is null)
        {
            return;
        }

        try
        {
            ExecutePlaceLongitudinalBlock(document);
        }
        catch (System.Exception exception)
        {
            prdDbg(exception);
            document.Editor.WriteMessage($"\nPLACELONGITUDINALBLOCK failed: {exception.Message}");
        }
    }

    private static void ExecutePlaceLongitudinalBlock(Document document)
    {
        Editor editor = document.Editor;
        Database localDb = document.Database;

        // Preflight everything that can fail cheaply BEFORE the user invests any picking — a missing
        // block library or an unresolvable project should not cost a station and a block choice first.
        if (!TryPreflight(out string fremtidPath, out string preflightError))
        {
            editor.WriteMessage($"\n{preflightError}");
            return;
        }

        if (!ProfileViewStationResolver.HasAnyProfileView(localDb))
        {
            editor.WriteMessage("\nTegningen indeholder ingen længdeprofiler.");
            return;
        }

        // First pick doubles as the mode switch. Multiple is a keyword rather than a separate prompt
        // so the common single placement stays one click.
        bool multiple = false;
        LongitudinalBlockPick? first = null;

        while (first is null)
        {
            StationPickStatus status = ProfileViewStationResolver.Resolve(
                editor, localDb, offerMultiple: !multiple, allowFinish: multiple,
                out first, out string pickError);

            if (status == StationPickStatus.Multiple)
            {
                multiple = true;
                editor.WriteMessage(
                    "\nMultiple mode: udpeg alle placeringer, tryk Enter, og vælg så blokken.");
                continue;
            }

            if (status == StationPickStatus.Finished)
            {
                editor.WriteMessage("\nIngen placeringer valgt.");
                return;
            }

            if (status != StationPickStatus.Ok || first is null)
            {
                editor.WriteMessage($"\n{pickError}");
                return;
            }
        }

        List<LongitudinalBlockPick> picks = [first];
        Report(editor, first);

        // All locations are collected before the block is chosen, so one choice covers the whole set.
        if (multiple)
        {
            while (true)
            {
                StationPickStatus status = ProfileViewStationResolver.Resolve(
                    editor, localDb, offerMultiple: false, allowFinish: true,
                    out LongitudinalBlockPick? next, out _);

                if (status != StationPickStatus.Ok || next is null)
                {
                    // Enter finishes and keeps what was collected; Esc does the same rather than
                    // discarding earlier picks, which would be a nasty surprise mid-run.
                    break;
                }

                picks.Add(next);
                Report(editor, next);
            }

            editor.WriteMessage($"\n{picks.Count} placering(er) valgt. Vælg blok.");
        }

        if (!ComponentCataloguePicker.TryPick(out string blockName, out string catalogueError))
        {
            editor.WriteMessage($"\n{catalogueError}");
            return;
        }

        FremtidBlockWriter.TryPlace(fremtidPath, localDb, picks, blockName, out string writeMessage);
        editor.WriteMessage($"\n{writeMessage}");
    }

    private static void Report(Editor editor, LongitudinalBlockPick pick)
    {
        editor.WriteMessage(
            $"\n{pick.ProfileViewName}: station {pick.Station:0.###}, kote {pick.Elevation:0.###}.");
    }

    /// <summary>
    /// Resolves the project and verifies every external dependency: the block library, the component
    /// catalogue, and that FV_Fremtid exists. Nothing here touches the drawing.
    /// </summary>
    private static bool TryPreflight(out string fremtidPath, out string message)
    {
        fremtidPath = string.Empty;

        if (!File.Exists(FremtidBlockWriter.SymbolerPath))
        {
            message = $"Kan ikke tilgå blokbiblioteket: {FremtidBlockWriter.SymbolerPath}";
            return false;
        }

        if (!ComponentCataloguePicker.TryLoad(out message))
        {
            return false;
        }

        // May itself show a project/etape picker, which is why it belongs before any transaction.
        DataReferencesOptions? dro = DataReferencesOptions.Create();
        if (dro is null)
        {
            message = "PLACELONGITUDINALBLOCK annulleret.";
            return false;
        }

        DataManager manager = new(dro);
        string path;
        try
        {
            path = manager.PathToFremtid();
        }
        catch (System.Exception exception)
        {
            message = $"Kan ikke finde FV_Fremtid for {dro.ProjectName} / {dro.EtapeName}: {exception.Message}";
            return false;
        }

        if (!File.Exists(path))
        {
            message = $"FV_Fremtid findes ikke: {path}";
            return false;
        }

        // FV_Fremtid open in ANOTHER session has to be caught here, before the user invests any
        // picking. AutoCAD's .dwl is advisory, not an OS lock, so a side-save against it SUCCEEDS
        // and is then silently discarded when that session saves — the save failing is not the
        // safety net it looks like. Open in THIS session is fine: the write goes to the live
        // document instead. Re-checked at save time, since it can be opened while picking.
        if (FremtidBlockWriter.TryFindForeignLock(path, out string lockOwner))
        {
            message = FremtidBlockWriter.ForeignLockMessage(path, lockOwner);
            return false;
        }

        fremtidPath = path;
        message = string.Empty;
        return true;
    }
}
