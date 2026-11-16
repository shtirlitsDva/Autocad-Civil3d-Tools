using System.IO;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

using CSharpFunctionalExtensions;

using IntersectUtilities.MPE.PlaceLongitudinalBlock;
using IntersectUtilities.UtilsCommon.DataManager;

using Norsyn.AecPropertySets;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities;

public partial class Intersect
{
    /// <command>PLACELONGITUDINALBLOCK</command>
    /// <summary>
    /// Places a component block in the FV_Fremtid plan drawing from a click in a længdeprofil: the station is
    /// resolved from the picked point and a component is chosen from FJV Dynamiske Komponenter.csv. Answer Multiple
    /// to pick all locations first and place the same block at each. FV_Fremtid must be open in the same Civil 3D
    /// session for writing (not read-only) and not busy with a command; otherwise the command names the FV_Fremtid
    /// file it expects and writes nothing. After the block choice the command hands the placements to
    /// PLACELONGITUDINALBLOCKFV and ends: AutoCAD switches to FV_Fremtid, writes there as FV_Fremtid's own command,
    /// then switches back to the længdeprofil and repeats the report. One UNDO in FV_Fremtid removes the whole run.
    /// The block is inserted on layer 0-KOMPONENT and sized from the pipe at the station; after that is committed and
    /// FV_Fremtid regenerated (REGEN), the FJV pipe of its alignment is cut at the SIZED block's ports (its MuffeIntern blocks lying at most 0.05 m
    /// from the pipe): the stretch between the two outermost ports is deleted, and across a joint each polyline is
    /// trimmed back to its own port. The pieces keep the pipe's layer, colour, linetype, width and property sets;
    /// FJV_fremtid Length becomes each piece's own length, and DriGraph ConnectedEntities links the pieces' cut ends
    /// to the block and moves the pipe's other links to the piece that holds them. The report line gives the length
    /// cut out of each polyline ("0,134 m skåret"). With fewer than two ports on the pipe (end cap, plan bend,
    /// bonded pipes, no pipe at the station) the pipe is left alone and the placement's report line ends with
    /// "rør ikke trimmet:" and the reason. A block that could not be sized (its VERSION missing from the catalogue,
    /// no pipe or no size at the station, or a size parameter the block lacks or AutoCAD refuses) stays placed at its
    /// defaults and its pipe is not cut: "rør ikke trimmet: blokken kunne ikke dimensioneres (…)". A block without
    /// size parameters (fixed geometry) is cut as it is. In Multiple mode all blocks are placed first, then each trims the pipe as
    /// the previous one left it. No welds are generated. FV_Fremtid is not saved.
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

        // The write runs as FV_Fremtid's own command once this one has ended.
        editor.WriteMessage($"\n{FremtidHandoff.Submit(new FremtidJob(Guid.NewGuid(), fremtidPath, document, picks, blockName))}");
    }

    private static void Report(Editor editor, LongitudinalBlockPick pick)
    {
        editor.WriteMessage(
            $"\n{pick.ProfileViewName}: station {pick.Station:0.###}, kote {pick.Elevation:0.###}.");
    }

    /// <summary>
    /// Resolves the project and verifies every external dependency: the block library, the component
    /// catalogue, and that FV_Fremtid exists and is open in this session for writing, idle. Nothing
    /// here touches a drawing.
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

        // Before the user invests any picking. Checked again when the job is handed over, since
        // FV_Fremtid can be closed or start a command meanwhile.
        Result<Document, AecError> fremtid = FremtidHandoff.OpenForWriting(path);
        message = fremtid.Match(_ => string.Empty, error => error.Message);
        fremtidPath = fremtid.Match(_ => path, _ => string.Empty);
        return fremtid.IsSuccess;
    }
}
