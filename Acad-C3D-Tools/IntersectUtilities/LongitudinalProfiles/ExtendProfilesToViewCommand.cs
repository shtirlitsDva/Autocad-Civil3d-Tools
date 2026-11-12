using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using IntersectUtilities.LongitudinalProfiles;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Unit = IntersectUtilities.LongitudinalProfiles.Unit;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        /// <command>EXTENDPROFILESTOVIEW</command>
        /// <summary>
        /// Extends TOP, MIDT and BUND to profile-view limits along their end slopes, within 1 m.
        /// </summary>
        /// <category>Longitudinal Profiles</category>
        /// <execution>
        /// Run EXTENDPROFILESTOVIEW in the active drawing; no selection is required. TOP, MIDT and BUND ends missing at most 1 m are extended to the corresponding profile-view boundary along their end slopes. Larger gaps are left for manual review and reported in F2. The two ends are treated independently. Drawing units must be metres; one UNDO reverses the operation.
        /// </execution>
        [CommandMethod("EXTENDPROFILESTOVIEW", CommandFlags.Modal)]
        public void ExtendProfilesToView() => LongitudinalProfiles.ExtendProfilesToViewCommand.Run();
    }
}

namespace IntersectUtilities.LongitudinalProfiles
{
    internal static class ExtendProfilesToViewCommand
    {
        internal sealed record ViewTarget(ObjectId ProfileId, string ViewName, string ViewHandle,
            ProfileStationRange Range);
        internal sealed record ExtensionRequest(ViewTarget View, ProfileEndExtension Extension);
        private sealed record GeometrySample(double Station, double Elevation, double Grade);
        private sealed record CurveSnapshot(uint Id, string Type, double Start, double End);
        private sealed record GeometrySnapshot(GeometrySample[] Samples, CurveSnapshot[] Curves,
            (double Station, double Elevation)[] Pvis);
        internal sealed record ExtensionSummary(int Views, int Profiles, int ExtendedEnds, int CoveredEnds,
            int SkippedEnds, IReadOnlyList<string> Messages);

        internal static void Run()
        {
            var document = AcApplication.DocumentManager.MdiActiveDocument;
            var editor = document.Editor;
            try
            {
                // Station coordinates use the Civil drawing's linear unit.
                if (CivilApplication.ActiveDocument.Settings.DrawingSettings.UnitZoneSettings.DrawingUnits
                    != Autodesk.Civil.Settings.DrawingUnitType.Meters)
                {
                    editor.WriteMessage("\nEXTENDPROFILESTOVIEW requires Civil drawing units in metres. No profiles were changed.");
                    return;
                }
                Execute(document.Database, message => editor.WriteMessage("\n" + message)).Match(summary =>
                {
                    foreach (string message in summary.Messages) editor.WriteMessage("\n" + message);
                    editor.WriteMessage($"\nEXTENDPROFILESTOVIEW: {summary.Views} views, {summary.Profiles} TOP/MIDT/BUND profiles; "
                        + $"{summary.ExtendedEnds} unique ends extended, {summary.CoveredEnds} profile/view ends already covered, "
                        + $"{summary.SkippedEnds} profile/view ends skipped. Tolerance: 1.000 m. Use UNDO to revert this command.");
                    return Unit.Value;
                }, error =>
                {
                    editor.WriteMessage($"\nEXTENDPROFILESTOVIEW failed; all extensions were rolled back. {error}");
                    return Unit.Value;
                });
            }
            catch (System.Exception error)
            {
                // Last-resort command boundary; Execute's transaction has already been disposed.
                editor.WriteMessage($"\nEXTENDPROFILESTOVIEW stopped: {error.Message}");
            }
        }

        internal static Result<ExtensionSummary> Execute(Database database, Action<string> progress)
        {
            try
            {
                using var transaction = database.TransactionManager.StartTransaction();
                var messages = new List<string>();
                var targets = ReadTargets(database, transaction, messages, out int viewCount);
                int extended = 0, covered = 0, skipped = 0;
                foreach (var group in targets.GroupBy(target => target.ProfileId))
                {
                    var profile = (Profile)transaction.GetObject(group.Key, OpenMode.ForRead);
                    var original = new ProfileStationRange(profile.StartingStation, profile.EndingStation);
                    var requests = new List<ExtensionRequest>();
                    foreach (var target in group)
                    foreach (bool atStart in new[] { true, false })
                    {
                        string label = Label(profile.Name, target, atStart);
                        ProfileExtensionRules.Plan(original, target.Range, atStart).Match(decision => decision.Match(extension =>
                        {
                            requests.Add(new(target, extension));
                            return Unit.Value;
                        }, () => { covered++; return Unit.Value; }), reason =>
                        {
                            skipped++;
                            messages.Add($"SKIPPED {label}: {reason} Profile {original.Start:F6} to {original.End:F6}; "
                                + $"view {target.Range.Start:F6} to {target.Range.End:F6}.");
                            return Unit.Value;
                        });
                    }
                    foreach (var extension in ProfileExtensionRules.Merge(requests.Select(request => request.Extension)))
                    {
                        var affected = requests.Where(request => request.Extension.AtStart == extension.AtStart).ToArray();
                        progress($"Extending {profile.Name} [{profile.Handle}] {extension.EndName}: "
                            + $"{extension.OriginalStation:F6} -> {extension.TargetStation:F6} ({extension.Gap:F6} m)...");
                        ExtendEnd(database, group.Key, extension).Match(elevation =>
                        {
                            extended++;
                            messages.Add($"EXTENDED {profile.Name} [{profile.Handle}] {extension.EndName}: "
                                + $"{extension.OriginalStation:F6} -> {extension.TargetStation:F6} "
                                + $"({extension.Gap:F6} m), elevation {elevation:F6}; views: "
                                + string.Join(", ", affected.Select(request => request.View.ViewName).Distinct()));
                            return Unit.Value;
                        }, reason =>
                        {
                            skipped += affected.Length;
                            foreach (var request in affected)
                                messages.Add($"SKIPPED {Label(profile.Name, request.View, extension.AtStart)}: "
                                    + $"gap {request.Extension.Gap:F6} m. {reason}");
                            return Unit.Value;
                        });
                    }
                }
                transaction.Commit();
                return Result<ExtensionSummary>.Success(new(viewCount, targets.Select(target => target.ProfileId).Distinct().Count(),
                    extended, covered, skipped, messages));
            }
            catch (Autodesk.AutoCAD.Runtime.Exception error) { return Result<ExtensionSummary>.Failure(error.Message); }
            catch (ArgumentException error) { return Result<ExtensionSummary>.Failure(error.Message); }
            catch (InvalidOperationException error) { return Result<ExtensionSummary>.Failure(error.Message); }
        }

        private static ViewTarget[] ReadTargets(Database database, Transaction transaction, List<string> messages, out int viewCount)
        {
            var targets = new List<ViewTarget>();
            viewCount = 0;
            var blocks = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            foreach (ObjectId blockId in blocks)
            {
                var block = (BlockTableRecord)transaction.GetObject(blockId, OpenMode.ForRead);
                if (!block.IsLayout) continue;
                foreach (ObjectId id in block)
                {
                    if (!id.ObjectClass.IsDerivedFrom(RXObject.GetClass(typeof(ProfileView)))) continue;
                    var view = (ProfileView)transaction.GetObject(id, OpenMode.ForRead);
                    viewCount++;
                    int count = 0;
                    foreach (ProfileOverride item in view.GraphOverrides)
                    {
                        if (!ProfileExtensionRules.IsPipeProfile(item.ProfileName)) continue;
                        targets.Add(new(item.ProfileId, view.Name, view.Handle.ToString(), new(view.StationStart, view.StationEnd)));
                        count++;
                    }
                    if (count == 0) messages.Add($"SKIPPED view {view.Name} [{view.Handle}]: no associated TOP/MIDT/BUND profiles.");
                }
            }
            return targets.DistinctBy(target => (target.ProfileId, target.ViewHandle)).ToArray();
        }

        // Each native edit gets a nested transaction: a failure at one end does not discard a valid opposite end.
        private static Result<double> ExtendEnd(Database database, ObjectId profileId, ProfileEndExtension extension)
        {
            try
            {
                using var transaction = database.TransactionManager.StartTransaction();
                var profile = (Profile)transaction.GetObject(profileId, OpenMode.ForRead);
                // Feature.IsEditable access-violates in Civil 3D 2025 on these profiles.
                // Check the supported profile kind here; UpgradeOpen is the editability boundary.
                if (profile.IsReferenceObject || profile.ProfileType != ProfileType.FG)
                    return Result<double>.Failure("The profile is referenced or is not a layout profile.");
                var alignment = (Alignment)transaction.GetObject(profile.AlignmentId, OpenMode.ForRead);
                if (extension.TargetStation < alignment.StartingStation || extension.TargetStation > alignment.EndingStation)
                    return Result<double>.Failure("The view boundary lies outside the parent alignment.");

                double current = extension.AtStart ? profile.StartingStation : profile.EndingStation;
                if (Math.Abs(current - extension.OriginalStation) > ProfileExtensionRules.StationTolerance)
                    return Result<double>.Failure("The profile endpoint changed after extension planning.");
                double grade = profile.GradeAt(current);
                double elevation = profile.ElevationAt(current);
                return ProfileExtensionRules.ProjectElevation(current, elevation, grade, extension.TargetStation).Bind(projected =>
                {
                    var before = Snapshot(profile);
                    profile.UpgradeOpen();
                    profile.PVIs.AddPVI(extension.TargetStation, projected);
                    return Validate(profile, before, extension, projected, grade).Map(_ =>
                    {
                        transaction.Commit();
                        return projected;
                    });
                });
            }
            catch (Autodesk.AutoCAD.Runtime.Exception error) { return Result<double>.Failure(error.Message); }
            catch (ArgumentException error) { return Result<double>.Failure(error.Message); }
            catch (InvalidOperationException error) { return Result<double>.Failure(error.Message); }
        }

        private static GeometrySnapshot Snapshot(Profile profile)
        {
            var entities = profile.Entities.Cast<ProfileEntity>().ToArray();
            var stations = entities.SelectMany(entity => Enumerable.Range(0, 5)
                .Select(index => entity.StartStation + (entity.EndStation - entity.StartStation) * index / 4.0))
                .Distinct().OrderBy(station => station).ToArray();
            var samples = stations.Select(station => new GeometrySample(station, profile.ElevationAt(station), profile.GradeAt(station))).ToArray();
            var curves = entities.Where(entity => entity is not ProfileTangent)
                .Select(entity => new CurveSnapshot(entity.EntityId, entity.GetType().Name,
                    entity.StartStation, entity.EndStation)).ToArray();
            var pvis = profile.PVIs.Cast<ProfilePVI>().Select(pvi => (pvi.RawStation, pvi.Elevation)).ToArray();
            return new(samples, curves, pvis);
        }

        private static Result<Unit> Validate(Profile profile, GeometrySnapshot before, ProfileEndExtension extension, double elevation, double grade)
        {
            const double elevationTolerance = 1e-6;
            const double gradeTolerance = 1e-8;
            bool Near(double first, double second, double tolerance) =>
                double.IsFinite(first) && double.IsFinite(second) && Math.Abs(first - second) <= tolerance;
            double actual = extension.AtStart ? profile.StartingStation : profile.EndingStation;
            if (!Near(actual, extension.TargetStation, ProfileExtensionRules.StationTolerance))
                return Result<Unit>.Failure("Civil 3D did not extend the endpoint to the requested station; this end was rolled back.");
            for (int index = 0; index <= 4; index++)
            {
                double station = extension.OriginalStation + (extension.TargetStation - extension.OriginalStation) * index / 4.0;
                double expected = elevation + grade * (station - extension.TargetStation);
                if (!Near(profile.ElevationAt(station), expected, elevationTolerance)
                    || !Near(profile.GradeAt(station), grade, gradeTolerance))
                    return Result<Unit>.Failure("Civil 3D did not produce a straight extension along the existing slope; this end was rolled back.");
            }
            foreach (var sample in before.Samples)
                if (!Near(profile.ElevationAt(sample.Station), sample.Elevation, elevationTolerance)
                    || !Near(profile.GradeAt(sample.Station), sample.Grade, gradeTolerance))
                    return Result<Unit>.Failure("Civil 3D changed existing profile geometry; this end was rolled back.");
            var afterPvis = profile.PVIs.Cast<ProfilePVI>().ToArray();
            foreach (var pvi in before.Pvis)
                if (!afterPvis.Any(after => Near(after.RawStation, pvi.Station, ProfileExtensionRules.StationTolerance)
                    && Near(after.Elevation, pvi.Elevation, elevationTolerance)))
                    return Result<Unit>.Failure("Civil 3D moved an existing PVI; this end was rolled back.");
            foreach (var curve in before.Curves)
            {
                var after = profile.Entities.EntityAtId(curve.Id);
                if (after.GetType().Name != curve.Type
                    || !Near(after.StartStation, curve.Start, ProfileExtensionRules.StationTolerance)
                    || !Near(after.EndStation, curve.End, ProfileExtensionRules.StationTolerance))
                    return Result<Unit>.Failure("Civil 3D changed an existing vertical curve; this end was rolled back.");
            }
            return Result<Unit>.Success(Unit.Value);
        }

        private static string Label(string profile, ViewTarget view, bool atStart) =>
            $"view {view.ViewName} [{view.ViewHandle}], profile {profile}, {(atStart ? "START" : "END")}";
    }
}
