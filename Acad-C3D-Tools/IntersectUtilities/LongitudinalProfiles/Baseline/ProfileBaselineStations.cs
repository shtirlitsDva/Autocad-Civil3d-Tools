using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using IntersectUtilities.LongitudinalProfiles;
using System;
using System.Collections.Generic;
using System.Linq;
using Unit = IntersectUtilities.LongitudinalProfiles.Unit;

namespace IntersectUtilities.LongitudinalProfiles.Baseline;

internal static partial class ProfileBaselineCommand
{
    private sealed record StationView(ObjectId Id, double Left, double Right, double Bottom, double Top);

    private static Option<StationView> ResolveStationView(Document document, GuideInput input)
    {
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var space = (BlockTableRecord)transaction.GetObject(input.OwnerId, OpenMode.ForRead);
        var grids = new List<ProfileViewGrid<StationView>>();
        double minX = input.Points.Min(p => p.X), maxX = input.Points.Max(p => p.X);
        double guideY = (input.Points.Min(p => p.Y) + input.Points.Max(p => p.Y)) / 2;
        foreach (ObjectId id in space)
        {
            if (!id.ObjectClass.IsDerivedFrom(RXObject.GetClass(typeof(ProfileView)))) continue;
            var view = (ProfileView)transaction.GetObject(id, OpenMode.ForRead);
            ReadView(view).Match(grid =>
            {
                if (grid.Left <= minX + ProfileBaselineGeometry.LinearTolerance && grid.Right >= maxX - ProfileBaselineGeometry.LinearTolerance &&
                    grid.Bottom <= guideY && grid.Top >= guideY)
                    grids.Add(new(grid, grid.Left, grid.Bottom, grid.Right, grid.Top));
                return Unit.Value;
            }, () => Unit.Value);
        }
        var nearest = ProfileViewProximity.FindClosest(grids, (minX + maxX) / 2,
            guideY, ProfileBaselineGeometry.LinearTolerance);
        if (nearest.Count == 1) return Option<StationView>.Of(nearest[0]);
        var options = new PromptEntityOptions("\nSelect the profile view for failed station labels (Escape aborts): ");
        options.SetRejectMessage("\nSelect a profile view."); options.AddAllowedClass(typeof(ProfileView), true);
        PromptEntityResult pick = document.Editor.GetEntity(options);
        if (pick.Status != PromptStatus.OK) return Option<StationView>.Nothing;
        var selected = (ProfileView)transaction.GetObject(pick.ObjectId, OpenMode.ForRead);
        if (selected.OwnerId != input.OwnerId) return Option<StationView>.Nothing;
        return ReadView(selected);
    }

    private static Option<StationView> ReadView(ProfileView view)
    {
        if (view.SplitProfileView) return Option<StationView>.Nothing;
        double minX = double.PositiveInfinity, maxX = double.NegativeInfinity;
        double minY = double.PositiveInfinity, maxY = double.NegativeInfinity;
        foreach (double station in new[] { view.StationStart, view.StationEnd })
        foreach (double elevation in new[] { view.ElevationMin, view.ElevationMax })
        {
            double x = 0, y = 0;
            if (!view.FindXYAtStationAndElevation(station, elevation, ref x, ref y) || !double.IsFinite(x) || !double.IsFinite(y))
                return Option<StationView>.Nothing;
            minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
        }
        return maxX > minX && maxY > minY ? Option<StationView>.Of(new(view.ObjectId, minX, maxX, minY, maxY)) : Option<StationView>.Nothing;
    }

    private static bool ReportProblems(Document document, StationView grid, IReadOnlyList<BaselineProblem> problems)
    {
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var view = (ProfileView)transaction.GetObject(grid.Id, OpenMode.ForRead);
        var alignment = (Alignment)transaction.GetObject(view.AlignmentId, OpenMode.ForRead);
        bool success = true;
        foreach (var problem in problems)
        {
            double firstStation = 0, lastStation = 0, elevation = 0;
            double middleY = (grid.Bottom + grid.Top) / 2;
            if (!view.FindStationAndElevationAtXY(problem.From.X, middleY, ref firstStation, ref elevation) ||
                !view.FindStationAndElevationAtXY(problem.To.X, middleY, ref lastStation, ref elevation))
            {
                document.Editor.WriteMessage($"\nFailed section at drawing X {problem.From.X:g12} to {problem.To.X:g12} is outside the selected view; station is unavailable.");
                success = false;
                continue;
            }
            string label = alignment.GetStationStringWithEquations(firstStation);
            if (Math.Abs(firstStation - lastStation) > ProfileBaselineGeometry.LinearTolerance)
                label += " to " + alignment.GetStationStringWithEquations(lastStation);
            document.Editor.WriteMessage($"\nCannot create baseline at station {label}: {problem.Reason}");
        }
        return success;
    }
}
