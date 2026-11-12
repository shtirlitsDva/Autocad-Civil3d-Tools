using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

using System;
using System.Collections.Generic;
using System.Linq;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

namespace IntersectUtilities.LongitudinalProfiles;

// An unaccepted result carries no usable view ID; cancellation never reaches the builder.
internal readonly record struct ProfileViewPickResult(bool Accepted, Oid ViewId)
{
    internal static ProfileViewPickResult Cancelled => default;
    internal static ProfileViewPickResult Selected(Oid viewId) => new(true, viewId);
}

internal static class ProfileConversionSelection
{
    // Shared by profile conversion and QA commands for the lifetime of the loaded plugin.
    private static bool automatic = true;

    internal static PromptEntityResult SelectSource(Editor editor, Type type, string name) =>
        SelectSource(editor, name, false, type);

    internal static PromptEntityResult SelectSource(Editor editor, string name, bool allowObjectOnLockedLayer, params Type[] types)
    {
        while (true)
        {
            string mode = automatic ? "Auto" : "Manual";
            var options = new PromptEntityOptions(
                $"\nSelect a {name} (profile view: {mode}) or") { AllowObjectOnLockedLayer = allowObjectOnLockedLayer };
            options.SetRejectMessage($"\nNot a {name}.");
            foreach (Type type in types) options.AddAllowedClass(type, true);
            options.Keywords.Add("Auto");
            options.Keywords.Add("Manual");

            PromptEntityResult result = editor.GetEntity(options);
            if (result.Status != PromptStatus.Keyword)
                return result;

            automatic = string.Equals(result.StringResult, "Auto", StringComparison.OrdinalIgnoreCase);
            editor.WriteMessage($"\nAutomatic profile view selection: {(automatic ? "On" : "Off")}.");
        }
    }

    internal static ProfileViewPickResult SelectView(
        Editor editor, Transaction transaction, PromptEntityResult sourcePick)
    {
        if (automatic)
        {
            // GetEntity's pick is in the current UCS; profile view grid coordinates are in WCS.
            Point3d picked = sourcePick.PickedPoint.TransformBy(editor.CurrentUserCoordinateSystem);
            var source = (Entity)transaction.GetObject(sourcePick.ObjectId, OpenMode.ForRead);
            var grids = new List<ProfileViewGrid<Oid>>();
            string failure = string.Empty;

            // Search the source's space so a layout cannot supply a view from another space.
            var space = (BlockTableRecord)transaction.GetObject(source.OwnerId, OpenMode.ForRead);
            foreach (Oid id in space)
            {
                if (!id.ObjectClass.IsDerivedFrom(Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(ProfileView))))
                    continue;

                try
                {
                    var view = (ProfileView)transaction.GetObject(id, OpenMode.ForRead);
                    if (source is Profile profile && view.AlignmentId != profile.AlignmentId)
                        continue;

                    // A split grid has multiple elevation datums, so a single rectangle cannot rank it.
                    if (view.SplitProfileView)
                    {
                        failure = "A split profile view requires manual selection.";
                        break;
                    }

                    double start = view.StationStart;
                    double end = view.StationEnd;
                    double bottom = view.ElevationMin;
                    double top = view.ElevationMax;
                    var corners = new List<Point2d>(4);
                    foreach (var (station, elevation) in new[]
                    {
                        (start, bottom), (start, top), (end, bottom), (end, top)
                    })
                    {
                        double x = 0, y = 0;
                        view.FindXYAtStationAndElevation(station, elevation, ref x, ref y);
                        corners.Add(new Point2d(x, y));
                    }

                    var grid = new ProfileViewGrid<Oid>(id,
                        corners.Min(point => point.X), corners.Min(point => point.Y),
                        corners.Max(point => point.X), corners.Max(point => point.Y));
                    if (!grid.IsValid)
                    {
                        failure = "A profile view grid could not be read.";
                        break;
                    }
                    grids.Add(grid);
                }
                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                {
                    failure = $"A profile view grid could not be read: {ex.Message}";
                    break;
                }
            }

            if (failure.Length == 0)
            {
                IReadOnlyList<Oid> nearest = ProfileViewProximity.FindClosest(
                    grids, picked.X, picked.Y, Tolerance.Global.EqualPoint);
                if (nearest.Count == 1)
                {
                    var view = (ProfileView)transaction.GetObject(nearest[0], OpenMode.ForRead);
                    editor.WriteMessage($"\nAutomatically selected profile view: {view.Name}.");
                    return ProfileViewPickResult.Selected(nearest[0]);
                }
                failure = nearest.Count == 0
                    ? "No suitable profile view was found."
                    : "Several profile views are equally close to the pick point.";
            }
            editor.WriteMessage($"\n{failure} Select the profile view manually.");
        }

        var options = new PromptEntityOptions("\nSelect a ProfileView: ");
        options.SetRejectMessage("\nNot a ProfileView.");
        options.AddAllowedClass(typeof(ProfileView), true);
        PromptEntityResult result = editor.GetEntity(options);
        return result.Status == PromptStatus.OK
            ? ProfileViewPickResult.Selected(result.ObjectId)
            : ProfileViewPickResult.Cancelled;
    }
}
