using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.UtilsCommon;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

namespace IntersectUtilities.MPE.PlaceLongitudinalBlock;

/// <summary>How a station prompt ended.</summary>
internal enum StationPickStatus
{
    /// <summary>A point was picked and resolved; <c>pick</c> is set.</summary>
    Ok,

    /// <summary>The user asked for Multiple mode; nothing was picked yet.</summary>
    Multiple,

    /// <summary>Enter — the user is done placing.</summary>
    Finished,

    /// <summary>Escape, or a precondition failed.</summary>
    Cancelled,
}

/// <summary>
/// Turns a click in a længdeprofil into an alignment station and the corresponding location in the
/// plan drawing. The profile view is derived from the picked point rather than selected separately,
/// so the point and the view can never disagree.
/// </summary>
internal static class ProfileViewStationResolver
{
    /// <summary>Look-ahead/behind used to read the alignment tangent, matching PLACEALIGNMENTMARKER.</summary>
    private const double TangentDelta = 0.5;

    /// <summary>Civil 3D's RXClass name for a ProfileView.</summary>
    private const string ProfileViewClassName = "AeccDbGraphProfile";

    internal static bool HasAnyProfileView(Database db)
    {
        using Transaction tx = db.TransactionManager.StartTransaction();
        BlockTableRecord modelSpace = (BlockTableRecord)tx.GetObject(
            SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);

        foreach (Oid id in modelSpace)
        {
            if (id.ObjectClass.Name == ProfileViewClassName)
            {
                tx.Commit();
                return true;
            }
        }

        tx.Commit();
        return false;
    }

    /// <summary>
    /// Prompts for a point and resolves it, re-asking when the click misses every profile view.
    /// </summary>
    /// <param name="offerMultiple">Adds the <c>Multiple</c> keyword to the prompt.</param>
    /// <param name="allowFinish">Lets Enter end the loop instead of being rejected.</param>
    internal static StationPickStatus Resolve(
        Editor editor,
        Database localDb,
        bool offerMultiple,
        bool allowFinish,
        out LongitudinalBlockPick? pick,
        out string message)
    {
        pick = null;
        message = string.Empty;

        while (true)
        {
            // Prompt outside any transaction; a miss below re-enters this loop only after the
            // transaction has closed, so the editor never prompts under an open transaction.
            PromptPointOptions options = new(BuildPrompt(offerMultiple, allowFinish))
            {
                AllowNone = allowFinish,
            };
            if (offerMultiple)
            {
                options.Keywords.Add("Multiple");
            }

            PromptPointResult result = editor.GetPoint(options);

            if (result.Status == PromptStatus.Keyword &&
                string.Equals(result.StringResult, "Multiple", StringComparison.OrdinalIgnoreCase))
            {
                return StationPickStatus.Multiple;
            }

            if (result.Status == PromptStatus.None)
            {
                return StationPickStatus.Finished;
            }

            if (result.Status != PromptStatus.OK)
            {
                message = "PLACELONGITUDINALBLOCK annulleret.";
                return StationPickStatus.Cancelled;
            }

            // GetPoint returns the point in the CURRENT UCS, but IsPointInsideXY and
            // FindStationAndElevationAtXY below both work in WCS. Under a rotated or shifted UCS —
            // routine on profile sheets — an untransformed point misses every profile view, or
            // resolves a station in the wrong one.
            Point3d picked = result.Value.TransformBy(editor.CurrentUserCoordinateSystem);

            if (TryResolvePoint(localDb, picked, out pick, out string resolveError))
            {
                return StationPickStatus.Ok;
            }

            editor.WriteMessage($"\n{resolveError}");
        }
    }

    private static string BuildPrompt(bool offerMultiple, bool allowFinish)
    {
        if (offerMultiple)
        {
            return "\nUdpeg placering i længdeprofil [Multiple]: ";
        }

        return allowFinish
            ? "\nUdpeg næste placering, eller Enter for at afslutte: "
            : "\nUdpeg placering i længdeprofil: ";
    }

    private static bool TryResolvePoint(
        Database db,
        Point3d picked,
        out LongitudinalBlockPick? pick,
        out string message)
    {
        pick = null;
        message = string.Empty;

        using Transaction tx = db.TransactionManager.StartTransaction();
        try
        {
            List<(ProfileView View, double Station, double Elevation)> hits = [];

            BlockTableRecord modelSpace = (BlockTableRecord)tx.GetObject(
                SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);

            foreach (Oid id in modelSpace)
            {
                if (id.ObjectClass.Name != ProfileViewClassName)
                {
                    continue;
                }

                ProfileView view = (ProfileView)tx.GetObject(id, OpenMode.ForRead);

                // Buffer 0: the click must land inside the view, not merely near it.
                Extents3d extents;
                try
                {
                    extents = ((Entity)view).GetBufferedXYGeometricExtents(0.0);
                }
                catch
                {
                    continue;
                }

                if (!extents.IsPointInsideXY(picked))
                {
                    continue;
                }

                double station = 0.0;
                double elevation = 0.0;
                bool inside;
                try
                {
                    inside = view.FindStationAndElevationAtXY(picked.X, picked.Y, ref station, ref elevation);
                }
                catch
                {
                    continue;
                }

                // The bool matters: outside the graph area the call happily extrapolates a station.
                if (!inside || station < view.StationStart || station > view.StationEnd)
                {
                    continue;
                }

                hits.Add((view, station, elevation));
            }

            if (hits.Count == 0)
            {
                message = "Punktet ligger ikke i et længdeprofil. Prøv igen.";
                tx.Commit();
                return false;
            }

            (ProfileView View, double Station, double Elevation) hit;
            if (hits.Count == 1)
            {
                hit = hits[0];
            }
            else if (!TryDisambiguate(hits, out hit))
            {
                message = "Annulleret.";
                tx.Commit();
                return false;
            }

            Alignment alignment = hit.View.AlignmentId.Go<Alignment>(tx);

            pick = new LongitudinalBlockPick(
                alignment.Name,
                alignment.Id,
                hit.View.Name,
                hit.Station,
                hit.Elevation,
                PointAtStation(alignment, hit.Station),
                TangentRotation(alignment, hit.Station));

            tx.Commit();
            return true;
        }
        catch
        {
            tx.Abort();
            throw;
        }
    }

    /// <summary>
    /// Overlapping profile views can both contain the click. Ask which one rather than guessing.
    /// </summary>
    private static bool TryDisambiguate(
        List<(ProfileView View, double Station, double Elevation)> hits,
        out (ProfileView View, double Station, double Elevation) hit)
    {
        hit = default;

        List<string> names = hits.Select(x => x.View.Name).OrderBy(x => x).ToList();
        string? selected = StringGridFormCaller.Call(names, "Flere længdeprofiler matcher – vælg:");
        if (selected is null)
        {
            return false;
        }

        int index = hits.FindIndex(x => x.View.Name == selected);
        if (index < 0)
        {
            return false;
        }

        hit = hits[index];
        return true;
    }

    private static Point3d PointAtStation(Alignment alignment, double station)
    {
        double x = 0.0;
        double y = 0.0;
        alignment.PointLocation(station, 0.0, ref x, ref y);
        return new Point3d(x, y, 0.0);
    }

    /// <summary>
    /// Tangent direction at the station, read from a short chord clamped to the alignment's ends.
    /// Returns the direction of increasing station; Symboler component blocks are drawn with their
    /// run along +X, so this orients them along the pipe.
    /// </summary>
    private static double TangentRotation(Alignment alignment, double station)
    {
        double behind = Math.Max(alignment.StartingStation, station - TangentDelta);
        double ahead = Math.Min(alignment.EndingStation, station + TangentDelta);
        if (ahead - behind <= 1e-9)
        {
            return 0.0;
        }

        Vector3d direction = PointAtStation(alignment, ahead) - PointAtStation(alignment, behind);
        return direction.Length <= 1e-9 ? 0.0 : Math.Atan2(direction.Y, direction.X);
    }
}
