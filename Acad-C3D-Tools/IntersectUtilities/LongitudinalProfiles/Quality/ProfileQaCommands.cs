using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager;
using Norsyn.AecPropertySets;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Unit = Norsyn.AecPropertySets.Unit;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        /// <command>CHECKPROFILERADII</command>
        /// <summary>Checks vertical casing-centreline radii in one profile view using the verified Norsyn tables.</summary>
        /// <category>Longitudinal Profiles</category>
        [CommandMethod("CHECKPROFILERADII", CommandFlags.Modal)]
        public void CheckProfileRadii() => LongitudinalProfiles.Quality.ProfileQaCommands.Run(LongitudinalProfiles.Quality.QaCheck.Radii);

        /// <command>CHECKPROFILEOVERLAP</command>
        /// <summary>Lists overlapping FJV horizontal arcs and vertical curves in one profile view.</summary>
        /// <category>Longitudinal Profiles</category>
        [CommandMethod("CHECKPROFILEOVERLAP", CommandFlags.Modal)]
        public void CheckProfileOverlap() => LongitudinalProfiles.Quality.ProfileQaCommands.Run(LongitudinalProfiles.Quality.QaCheck.Overlap);

        /// <command>CHECKPROFILECROSSINGS</command>
        /// <summary>Lists intrusions into the drawn outer crossing envelopes in one selected detailing block.</summary>
        /// <category>Longitudinal Profiles</category>
        [CommandMethod("CHECKPROFILECROSSINGS", CommandFlags.Modal)]
        public void CheckProfileCrossings() => LongitudinalProfiles.Quality.ProfileQaCommands.Run(LongitudinalProfiles.Quality.QaCheck.Crossings);

        /// <command>CHECKPROFILETANGENCY</command>
        /// <summary>Lists non-tangent casing-centreline connections by station in one profile view.</summary>
        /// <category>Longitudinal Profiles</category>
        [CommandMethod("CHECKPROFILETANGENCY", CommandFlags.Modal)]
        public void CheckProfileTangency() => LongitudinalProfiles.Quality.ProfileQaCommands.Run(LongitudinalProfiles.Quality.QaCheck.Tangency);

        /// <command>CHECKPROFILECOVER</command>
        /// <summary>Checks vertical soil cover over the casing for one profile view or selected interval.</summary>
        /// <category>Longitudinal Profiles</category>
        [CommandMethod("CHECKPROFILECOVER", CommandFlags.Modal)]
        public void CheckProfileCover() => LongitudinalProfiles.Quality.ProfileQaCommands.Run(LongitudinalProfiles.Quality.QaCheck.Cover);
    }
}

namespace IntersectUtilities.LongitudinalProfiles.Quality
{
    internal enum QaCheck { Radii, Overlap, Crossings, Tangency, Cover }
    internal sealed record QaInput(ProfileView View, Alignment Alignment, QaViewMap Map, IReadOnlyList<QaSegment> Segments, double Start, double End);
    internal readonly record struct QaHorizontal(double Start, double End, double Radius, string Handle);
    internal sealed record QaPipeData(IReadOnlyList<QaPipeSpan> Sizes, IReadOnlyList<QaHorizontal> Horizontal, string File);
    internal sealed record QaTerrain(string Name, IReadOnlyList<QaSegment> Segments);
    internal sealed record QaCoverAnalysis(IReadOnlyList<QaCoverFailure> Failures, IReadOnlyList<QaInterval> Checked,
        Option<QaCoverSample> Worst, IReadOnlyList<QaCoverSample> Samples, IReadOnlyList<double> UncheckedSamples);

    internal readonly record struct QaViewMap(double Station, double Elevation, double X, double Y, double ScaleX, double ScaleY)
    {
        internal QaPoint FromWorld(Point3d point) => new(Station + (point.X - X) / ScaleX, Elevation + (point.Y - Y) / ScaleY);
        internal static Result<QaViewMap> Read(ProfileView view)
        {
            if (view.SplitProfileView) return Result<QaViewMap>.Failure("Split profile views need separate datum handling and are not supported by this draft.");
            double x = 0, y = 0, sx = 0, sy = 0, ex = 0, ey = 0;
            view.FindXYAtStationAndElevation(view.StationStart, view.ElevationMin, ref x, ref y);
            view.FindXYAtStationAndElevation(view.StationStart + 1, view.ElevationMin, ref sx, ref sy);
            view.FindXYAtStationAndElevation(view.StationStart, view.ElevationMin + 1, ref ex, ref ey);
            double scaleX = sx - x, scaleY = ey - y;
            if (!double.IsFinite(scaleX) || !double.IsFinite(scaleY) || Math.Abs(scaleX) < 1e-10 || scaleY <= 0 ||
                Math.Abs(sy - y) > 1e-7 || Math.Abs(ex - x) > 1e-7)
                return Result<QaViewMap>.Failure("The view does not have a readable axis-aligned station/elevation transform.");
            return Result<QaViewMap>.Success(new(view.StationStart, view.ElevationMin, x, y, scaleX, scaleY));
        }
    }

    internal static partial class ProfileQaCommands
    {
        private static double lastCoverThreshold = 0.60;

        internal static void Run(QaCheck check)
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            try
            {
                using var transaction = document.Database.TransactionManager.StartTransaction();
                PickInput(document.Editor, transaction).Match(input =>
                {
                    Header(document.Editor, input, check);
                    return check switch
                    {
                        QaCheck.Radii => WithPipes(document.Editor, input, pipes => ReportRadii(document.Editor, input, pipes)),
                        QaCheck.Overlap => WithPipes(document.Editor, input, pipes => ReportOverlap(document.Editor, input, pipes)),
                        QaCheck.Crossings => ReportCrossings(document.Editor, transaction, input),
                        QaCheck.Tangency => ReportTangency(document.Editor, input),
                        QaCheck.Cover => WithPipes(document.Editor, input, pipes => ReportCover(document.Editor, transaction, input, pipes))
                    };
                }, message => { document.Editor.WriteMessage($"\nProfile QA: {message}"); return Unit.Value; });
                // Deliberately no commit: metadata helpers in legacy pipe factories can attach defaults.
                // The local drawing and every source database remain unchanged.
            }
            catch (Autodesk.AutoCAD.Runtime.Exception error) { document.Editor.WriteMessage($"\nProfile QA could not complete: {error.Message}"); }
            catch (System.Exception error) { document.Editor.WriteMessage($"\nProfile QA could not complete: {error.Message}\n{error.StackTrace}"); }
        }

        private static Result<QaInput> PickInput(Editor editor, Transaction transaction)
        {
            var pick = ProfileConversionSelection.SelectSource(editor, "casing centreline (polyline or Civil 3D profile)", true,
                typeof(Polyline), typeof(Profile));
            if (pick.Status != PromptStatus.OK) return Result<QaInput>.Failure("Cancelled.");
            var viewPick = ProfileConversionSelection.SelectView(editor, transaction, pick);
            if (!viewPick.Accepted) return Result<QaInput>.Failure("Cancelled.");
            var view = (ProfileView)transaction.GetObject(viewPick.ViewId, OpenMode.ForRead);
            var alignment = (Alignment)transaction.GetObject(view.AlignmentId, OpenMode.ForRead);
            var source = (Entity)transaction.GetObject(pick.ObjectId, OpenMode.ForRead);
            if (source is Profile profile && profile.AlignmentId != view.AlignmentId)
                return Result<QaInput>.Failure("The selected profile belongs to another alignment.");
            return QaViewMap.Read(view).Bind(map => ReadSource(source, map).Bind(segments =>
            {
                if (segments.Count == 0) return Result<QaInput>.Failure("The centreline has no readable segments.");
                double start = Math.Max(view.StationStart, segments.Min(s => s.Start));
                double end = Math.Min(view.StationEnd, segments.Max(s => s.End));
                if (end - start <= QaGeometry.LinearTolerance) return Result<QaInput>.Failure("The centreline does not overlap the selected view's station range.");
                return Result<QaInput>.Success(new(view, alignment, map, segments, start, end));
            }));
        }

        internal static Result<IReadOnlyList<QaSegment>> ReadSource(Entity source, QaViewMap map)
        {
            if (source is Polyline polyline) return ReadPolyline(polyline, map);
            if (source is Profile profile) return ReadProfile(profile);
            return Result<IReadOnlyList<QaSegment>>.Failure("Unsupported centreline entity.");
        }

        internal static Result<IReadOnlyList<QaSegment>> ReadProfile(Profile profile)
        {
            var segments = new List<QaSegment>();
            int index = 0;
            foreach (ProfileEntity entity in profile.Entities.Cast<ProfileEntity>().OrderBy(e => e.StartStation))
            {
                index++;
                double start = entity.StartStation, end = entity.EndStation;
                if (end - start <= QaGeometry.LinearTolerance) return Result<IReadOnlyList<QaSegment>>.Failure($"Profile entity {index} has zero or reversed station length at {start:F6}.");
                QaSegment segment;
                if (entity is ProfileTangent)
                    segment = QaSegment.Line(new(start, entity.StartElevation), new(end, entity.EndElevation), index);
                else if (entity is ProfileCircular circular)
                {
                    double sign = circular.GradeOut >= circular.GradeIn ? 1 : -1;
                    double angle = Math.Atan(circular.GradeIn);
                    double radius = Math.Abs(circular.Radius);
                    double cx = start - sign * radius * Math.Sin(angle), cz = entity.StartElevation + sign * radius * Math.Cos(angle);
                    segment = QaSegment.Ellipse(start, end, cx, cz, radius, radius, -sign, index);
                }
                else if (entity is ProfileParabolaSymmetric parabola)
                    segment = QaSegment.Parabola(start, end, entity.StartElevation, parabola.GradeIn, (parabola.GradeOut - parabola.GradeIn) / (end - start), index);
                else return Result<IReadOnlyList<QaSegment>>.Failure($"Unsupported profile entity {entity.GetType().Name} at {start:F3}-{end:F3}; no approximation was made.");
                if (!double.IsFinite(segment.At(start)) || !double.IsFinite(segment.At(end)) ||
                    Math.Abs(segment.At(start) - entity.StartElevation) > 1e-5 || Math.Abs(segment.At(end) - entity.EndElevation) > 1e-5)
                    return Result<IReadOnlyList<QaSegment>>.Failure($"Profile entity {index} could not be represented accurately.");
                segments.Add(segment);
            }
            return ValidateSegments(segments);
        }

        private static Result<IReadOnlyList<QaSegment>> ReadPolyline(Polyline polyline, QaViewMap map)
        {
            if (polyline.Closed || polyline.NumberOfVertices < 2 || Math.Abs(polyline.Normal.X) > 1e-10 || Math.Abs(polyline.Normal.Y) > 1e-10 || polyline.Normal.Z < 0.999999)
                return Result<IReadOnlyList<QaSegment>>.Failure("Use an open XY-plane polyline with normal +Z and at least two vertices.");
            var segments = new List<QaSegment>();
            for (int i = 0; i < polyline.NumberOfVertices - 1; i++)
            {
                QaPoint p = map.FromWorld(polyline.GetPoint3dAt(i)), q = map.FromWorld(polyline.GetPoint3dAt(i + 1));
                if (Math.Abs(p.S - q.S) <= QaGeometry.LinearTolerance)
                    return Result<IReadOnlyList<QaSegment>>.Failure($"Zero-length or vertical polyline segment {i + 1} at station {p.S:F6}; station/elevation must be single-valued.");
                if (polyline.GetBulgeAt(i) == 0) segments.Add(QaSegment.Line(p, q, i + 1));
                else
                {
                    Point3d worldStart = polyline.GetPoint3dAt(i);
                    Point3d worldEnd = polyline.GetPoint3dAt(i + 1);
                    string issue = QaBulgeArc.FromEndpoints(new(worldStart.X, worldStart.Y), new(worldEnd.X, worldEnd.Y), polyline.GetBulgeAt(i)).Match(arc =>
                    {
                        if (InteriorAngle(arc.Angle, arc.Sweep, 0) || InteriorAngle(arc.Angle, arc.Sweep, Math.PI))
                            return $"Arc segment {i + 1} reverses station direction. Split or repair it before checking.";
                        QaPoint centre = map.FromWorld(new Point3d(arc.Centre.S, arc.Centre.Z, polyline.Elevation));
                        double sign = Math.Sign(Math.Sin(arc.Angle + arc.Sweep / 2));
                        segments.Add(QaSegment.Ellipse(Math.Min(p.S, q.S), Math.Max(p.S, q.S), centre.S, centre.Z,
                            arc.Radius / Math.Abs(map.ScaleX), arc.Radius / map.ScaleY, sign, i + 1));
                        return "";
                    }, message => $"Arc segment {i + 1}: {message}");
                    if (issue.Length > 0) return Result<IReadOnlyList<QaSegment>>.Failure(issue);
                }
            }
            return ValidateSegments(segments);
        }

        private static bool InteriorAngle(double angle, double sweep, double candidate)
        {
            double delta = QaGeometry.PositiveAngle(sweep >= 0 ? candidate - angle : angle - candidate);
            return delta > 1e-9 && delta < Math.Abs(sweep) - 1e-9;
        }

        private static Result<IReadOnlyList<QaSegment>> ValidateSegments(List<QaSegment> segments)
        {
            segments = segments.OrderBy(s => s.Start).ToList();
            for (int i = 1; i < segments.Count; i++)
                if (segments[i].Start < segments[i - 1].End - QaGeometry.StationTolerance)
                    return Result<IReadOnlyList<QaSegment>>.Failure($"The centreline doubles back or overlaps itself near station {segments[i].Start:F6}.");
            return Result<IReadOnlyList<QaSegment>>.Success(segments.AsReadOnly());
        }

        private static Unit WithPipes(Editor editor, QaInput input, Func<QaPipeData, Unit> action) => ReadPipes(input).Match(action,
            message => { editor.WriteMessage($"\nCOULD NOT CHECK: {message}"); return Unit.Value; });

        internal static Result<QaPipeData> ReadPipes(QaInput input) => ReadPipeData(input, true);

        internal static Result<QaPipeData> ReadCrossingPipes(QaInput input) => ReadPipeData(input, false);

        private static Result<QaPipeData> ReadPipeData(QaInput input, bool includeHorizontal)
        {
            // Existing project/phase resolution and size-array factory are the same ones UPDATEPROFILEVIEWS uses.
            if (DataReferencesOptions.Create() is not DataReferencesOptions references)
                return Result<QaPipeData>.Failure("Project/phase selection was cancelled.");
            var manager = new DataManager(references);
            using Database database = manager.Fremtid();
            using var transaction = database.TransactionManager.StartTransaction();
            var entities = database.GetFjvEntities(transaction).Where(e =>
                Text(e, "DriPipelineData", "BelongsToAlignment") == input.Alignment.Name).ToArray();
            if (entities.Length == 0) return Result<QaPipeData>.Failure($"No FJV_fremtid entities link to alignment {input.Alignment.Name}.");
            var pipeline = PipelineV2Factory.Create(entities, input.Alignment);
            var sizes = PipelineSizeArrayFactory.CreateSizeArray(pipeline).Sizes.Where(s => s.EndStation > input.Start && s.StartStation < input.End).Select(s =>
                new QaPipeSpan(s.StartStation, s.EndStation, s.DN, s.System.ToString(), s.Type.ToString(), s.Series.ToString(), s.Kod / 1000)).ToArray();
            if (sizes.Length == 0 || sizes.Any(s => !double.IsFinite(s.Diameter) || s.Diameter <= 0 || s.End <= s.Start))
                return Result<QaPipeData>.Failure("The linked pipe size array has missing or invalid casing dimensions/ranges.");
            if (!includeHorizontal) return Result<QaPipeData>.Success(new(sizes, Array.Empty<QaHorizontal>(), database.Filename));
            var horizontal = new List<QaHorizontal>();
            foreach (var polyline in entities.OfType<Polyline>())
            {
                for (int i = 0; i < polyline.NumberOfVertices - 1; i++)
                {
                    if (polyline.GetBulgeAt(i) == 0) continue;
                    Point3d p = polyline.GetPoint3dAt(i), q = polyline.GetPoint3dAt(i + 1);
                    double start = Station(input.Alignment, p), end = Station(input.Alignment, q);
                    horizontal.Add(new(Math.Min(start, end), Math.Max(start, end), polyline.GetArcSegment2dAt(i).Radius, $"{polyline.Handle}:{i + 1}"));
                }
            }
            return Result<QaPipeData>.Success(new(sizes, horizontal, database.Filename));
        }

        private static double Station(Alignment alignment, Point3d point)
        {
            double station = 0, offset = 0;
            alignment.StationOffset(point.X, point.Y, ref station, ref offset);
            return station;
        }

        private static string Text(Entity entity, string set, string property) =>
            PropertySetManager.TryReadNonDefinedPropertySetObject(entity, set, property, out object value)
                ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? "" : "";

        private static void Header(Editor editor, QaInput input, QaCheck check)
        {
            editor.WriteMessage($"\n\n--- PROFILE QA DRAFT / {check} ---\nView: {input.View.Name}; alignment: {input.Alignment.Name}; stations {input.Start:F3}-{input.End:F3} m");
            ReportGaps(editor, input.Segments, input.Start, input.End, "centreline");
        }

        private static void ReportGaps(Editor editor, IEnumerable<QaSegment> segments, double start, double end, string name)
        {
            double covered = start;
            foreach (var segment in segments.Where(s => s.End > start && s.Start < end).OrderBy(s => s.Start))
            {
                if (segment.Start > covered + QaGeometry.StationTolerance) editor.WriteMessage($"\nUNCHECKED {name} gap: {covered:F3}-{Math.Min(segment.Start, end):F3} m");
                covered = Math.Max(covered, segment.End);
            }
            if (covered < end - QaGeometry.StationTolerance) editor.WriteMessage($"\nUNCHECKED {name} gap: {covered:F3}-{end:F3} m");
        }

        private static IEnumerable<(QaSegment Curve, QaPipeSpan Pipe, double Start, double End)> Pieces(QaInput input, QaPipeData pipes)
        {
            foreach (var curve in input.Segments)
                foreach (var pipe in pipes.Sizes)
                {
                    double start = Math.Max(input.Start, Math.Max(curve.Start, pipe.Start));
                    double end = Math.Min(input.End, Math.Min(curve.End, pipe.End));
                    if (end - start > QaGeometry.LinearTolerance) yield return (curve, pipe, start, end);
                }
        }

        private static Unit ReportRadii(Editor editor, QaInput input, QaPipeData pipes)
        {
            int count = 0, failed = 0, review = 0, unknown = 0;
            if (pipes.Sizes.Any(p => p.System == "Stål"))
                editor.WriteMessage("\nSteel: minimum and target acceptance thresholds are rounded to whole metres; the table shows the original limits.");
            var table = new QaTable(new("From sta", true), new("To sta", true), new("V R (m)", true), new("Min (m)", true),
                new("Target (m)", true), new("Worst sta", true), new("Result"), new("Pipe", MaxWidth: int.MaxValue));
            var notes = new List<string>();
            foreach (var part in Pieces(input, pipes))
            {
                if (part.Curve.Kind == QaCurveKind.Line) continue;
                count++;
                var minimum = part.Curve.MinimumRadius(part.Start, part.End);
                ProfileQaRadii.Get(part.Pipe, true).Match(rule =>
                {
                    string status = ProfileQaRadii.Status(minimum.Radius, rule, part.Pipe);
                    if (status == "FAIL") failed++; if (status == "REVIEW") review++;
                    table.Add(part.Start.ToString("F3"), part.End.ToString("F3"), minimum.Radius.ToString("F1"), rule.Minimum.ToString("F1"),
                        rule.HasTarget ? rule.Target.ToString("F1") : "--", minimum.Station.ToString("F3"), status, QaPipeText.Description(part.Pipe));
                    return Unit.Value;
                }, message =>
                {
                    unknown++;
                    table.Add(part.Start.ToString("F3"), part.End.ToString("F3"), minimum.Radius.ToString("F1"), "?", "?",
                        minimum.Station.ToString("F3"), "UNVERIFIED", QaPipeText.Description(part.Pipe));
                    notes.Add($"{QaPipeText.Description(part.Pipe)}: {message}");
                    return Unit.Value;
                });
            }
            WriteTable(editor, table);
            foreach (string note in notes.Distinct()) editor.WriteMessage($"\n{note}");
            ReportPipeGaps(editor, input, pipes);
            editor.WriteMessage($"\nChecked {count} curved pipe intervals; {failed} FAIL; {review} REVIEW; {unknown} UNVERIFIED. Straight segments have infinite radius.");
            return Unit.Value;
        }

        private static Unit ReportOverlap(Editor editor, QaInput input, QaPipeData pipes)
        {
            int count = 0;
            editor.WriteMessage("\nAll overlaps are listed for manual evaluation. Radii and limits are in metres; utilization = minimum / actual radius.");
            var table = new QaTable(new("From sta", true), new("To sta", true), new("H R (m)", true), new("V R (m)", true),
                new("H min/target", true), new("V min/target", true), new("H %", true), new("V %", true), new("H arc"), new("Pipe", MaxWidth: int.MaxValue));
            foreach (var part in Pieces(input, pipes).Where(p => p.Curve.Kind != QaCurveKind.Line))
            {
                foreach (var horizontal in pipes.Horizontal)
                {
                    double start = Math.Max(part.Start, horizontal.Start), end = Math.Min(part.End, horizontal.End);
                    if (end - start <= QaGeometry.LinearTolerance) continue;
                    count++;
                    double radius = part.Curve.MinimumRadius(start, end).Radius;
                    string hText = "unverified", vText = "unverified", hUse = "--", vUse = "--";
                    ProfileQaRadii.Get(part.Pipe, false).Match(rule => { hText = LimitText(rule); hUse = (rule.Minimum / horizontal.Radius * 100).ToString("F1"); return Unit.Value; }, _ => Unit.Value);
                    ProfileQaRadii.Get(part.Pipe, true).Match(rule => { vText = LimitText(rule); vUse = (rule.Minimum / radius * 100).ToString("F1"); return Unit.Value; }, _ => Unit.Value);
                    table.Add(start.ToString("F3"), end.ToString("F3"), horizontal.Radius.ToString("F1"), radius.ToString("F1"),
                        hText, vText, hUse, vUse, horizontal.Handle, QaPipeText.Description(part.Pipe));
                }
            }
            WriteTable(editor, table);
            ReportPipeGaps(editor, input, pipes);
            editor.WriteMessage($"\n{count} overlapping curve intervals. Combined acceptance is left to the designer.");
            return Unit.Value;
        }

        private static string LimitText(QaRadiusLimit limit) => limit.HasTarget ? $"{limit.Minimum:F1}/{limit.Target:F1}" : $"{limit.Minimum:F1}/--";

        private static Unit ReportTangency(Editor editor, QaInput input)
        {
            int count = 0;
            editor.WriteMessage($"\nAngular tolerance: {QaGeometry.TangencyDegrees:F6} degrees. Every larger deviation is listed; no component exemptions.");
            var table = new QaTable(new("Station", true), new("Segments"), new("Types"), new("Angle (deg)", true),
                new("Sta gap (m)", true), new("Z gap (m)", true));
            for (int i = 1; i < input.Segments.Count; i++)
            {
                var left = input.Segments[i - 1]; var right = input.Segments[i];
                double station = (left.End + right.Start) / 2;
                if (station < input.Start - QaGeometry.StationTolerance || station > input.End + QaGeometry.StationTolerance) continue;
                double gap = right.Start - left.End, dz = right.At(right.Start) - left.At(left.End);
                double angle = Math.Abs(Math.Atan(left.Grade(left.End)) - Math.Atan(right.Grade(right.Start))) * 180 / Math.PI;
                if (gap <= QaGeometry.StationTolerance && Math.Abs(dz) <= QaGeometry.LinearTolerance && angle <= QaGeometry.TangencyDegrees) continue;
                count++;
                table.Add(station.ToString("F1"), $"{left.Index}/{right.Index}", $"{QaGeometry.CurveLabel(left.Kind)}/{QaGeometry.CurveLabel(right.Kind)}",
                    angle.ToString("G3"), gap.ToString("G3"), dz.ToString("G3"));
            }
            WriteTable(editor, table);
            editor.WriteMessage($"\n{count} non-tangent or disconnected connections in the selected view.");
            return Unit.Value;
        }

        private static void ReportPipeGaps(Editor editor, QaInput input, QaPipeData pipes)
        {
            var covered = QaGeometry.Merge(Pieces(input, pipes).Select(p => new QaInterval(p.Start, p.End)));
            foreach (var curve in input.Segments)
            {
                double start = Math.Max(input.Start, curve.Start), end = Math.Min(input.End, curve.End), at = start;
                if (end <= start) continue;
                foreach (var range in covered.Where(r => r.End > start && r.Start < end))
                {
                    if (range.Start > at + QaGeometry.StationTolerance) editor.WriteMessage($"\nUNCHECKED pipe-data gap {at:F3}-{range.Start:F3} m");
                    at = Math.Max(at, range.End);
                }
                if (at < end - QaGeometry.StationTolerance) editor.WriteMessage($"\nUNCHECKED pipe-data gap {at:F3}-{end:F3} m");
            }
        }

        private static void WriteTable(Editor editor, QaTable table) => editor.WriteMessage("\n" + table.Render());

        private static void WriteTable(Editor editor, QaTable table, IReadOnlyList<int> widths) => editor.WriteMessage("\n" + table.Render(widths));

        internal static Result<QaTerrain> ReadTerrain(Transaction transaction, QaInput input)
        {
            var surfaces = input.Alignment.GetProfileIds().Cast<ObjectId>().Select(id => (Profile)transaction.GetObject(id, OpenMode.ForRead))
                .Where(p => p.Name.EndsWith("_surface_P", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (surfaces.Length != 1)
                return Result<QaTerrain>.Failure($"Expected one _surface_P terrain profile for {input.Alignment.Name}; found {surfaces.Length}.");
            return ReadProfile(surfaces[0]).Bind(surface => surface.Any(s => s.Kind != QaCurveKind.Line)
                ? Result<QaTerrain>.Failure("The surface profile contains curved entities; this draft requires its original straight terrain segments.")
                : Result<QaTerrain>.Success(new(surfaces[0].Name, surface)));
        }

        private static Unit ReportCover(Editor editor, Transaction transaction, QaInput input, QaPipeData pipes)
        {
            var thresholdOptions = new PromptDoubleOptions($"\nMinimum vertical cover in metres <{lastCoverThreshold:0.00###############}>: ")
            { AllowNegative = false, AllowZero = true, AllowNone = true, DefaultValue = lastCoverThreshold, UseDefaultValue = true };
            var threshold = editor.GetDouble(thresholdOptions);
            if (threshold.Status != PromptStatus.OK) return Unit.Value;
            if (!double.IsFinite(threshold.Value)) { editor.WriteMessage("\nEnter a finite cover value."); return Unit.Value; }
            lastCoverThreshold = threshold.Value;
            var modeOptions = new PromptKeywordOptions("\nCheck range [Whole/Partial] <Whole>: ") { AllowNone = true };
            modeOptions.Keywords.Add("Whole"); modeOptions.Keywords.Add("Partial"); modeOptions.Keywords.Default = "Whole";
            var mode = editor.GetKeywords(modeOptions);
            if (mode.Status == PromptStatus.Cancel) return Unit.Value;
            double start = input.Start, end = input.End;
            if (mode.Status == PromptStatus.OK && mode.StringResult == "Partial")
            {
                var first = editor.GetPoint("\nPick interval start in the profile view: ");
                if (first.Status != PromptStatus.OK) return Unit.Value;
                var second = editor.GetPoint("\nPick interval end in the profile view: ");
                if (second.Status != PromptStatus.OK) return Unit.Value;
                double a = input.Map.FromWorld(first.Value.TransformBy(editor.CurrentUserCoordinateSystem)).S;
                double b = input.Map.FromWorld(second.Value.TransformBy(editor.CurrentUserCoordinateSystem)).S;
                start = Math.Max(start, Math.Min(a, b)); end = Math.Min(end, Math.Max(a, b));
                if (end - start <= QaGeometry.LinearTolerance) { editor.WriteMessage("\nThe selected interval has no centreline within this view."); return Unit.Value; }
            }
            QaInput selected = input with { Start = start, End = end };
            var samplingOptions = new PromptKeywordOptions("\nCover check [Every5m/Continuous] <Every5m>: ") { AllowNone = true };
            samplingOptions.Keywords.Add("Every5m"); samplingOptions.Keywords.Add("Continuous"); samplingOptions.Keywords.Default = "Every5m";
            var sampling = editor.GetKeywords(samplingOptions);
            if (sampling.Status != PromptStatus.OK && sampling.Status != PromptStatus.None) return Unit.Value;
            bool sampled = sampling.Status == PromptStatus.None || sampling.StringResult == "Every5m";
            return ReadTerrain(transaction, input).Match(terrain =>
            {
                var surface = terrain.Segments;
                var analysis = sampled ? AnalyzeSampledCover(selected, pipes, surface) : AnalyzeCover(selected, pipes, surface, threshold.Value);
                analysis.Worst.Match(worst =>
                {
                    string status = QaGeometry.IsLowCover(worst.Cover, threshold.Value) ? "FAIL" : "OK";
                    editor.WriteMessage($"\n\n{(sampled ? "WORST SAMPLED COVER" : "WORST COVER")}: {worst.Cover:F2} m at station {worst.Station:F3} | Required {threshold.Value:F2} m | {status}");
                    return Unit.Value;
                }, () => { editor.WriteMessage("\nCOULD NOT CHECK: no cover could be evaluated in the selected range."); return Unit.Value; });
                editor.WriteMessage($"\nSurface: {terrain.Name}; selected range {start:F3}-{end:F3} m.");
                if (sampled)
                {
                    var failures = analysis.Samples.Where(s => QaGeometry.IsLowCover(s.Cover, threshold.Value)).ToArray();
                    editor.WriteMessage($"\nSample stations with cover less than {threshold.Value:F2} m (only 5 m station multiples and selected endpoints checked):");
                    var table = new QaTable(new("Station", true), new("Cover (m)", true));
                    foreach (var sample in failures) table.Add(sample.Station.ToString("F3"), sample.Cover.ToString("F2"));
                    WriteTable(editor, table);
                    foreach (double station in analysis.UncheckedSamples) editor.WriteMessage($"\nUNCHECKED station {station:F3}: missing centreline, surface or pipe data.");
                    editor.WriteMessage($"\n{failures.Length} of {analysis.Samples.Count} checked sample stations have cover less than {threshold.Value:F2} m.");
                }
                else
                {
                    var table = new QaTable(new("From sta", true), new("To sta", true), new("Length (m)", true),
                        new("Min cover (m)", true), new("Worst sta", true));
                    foreach (var failure in analysis.Failures)
                        table.Add(failure.Start.ToString("F3"), failure.End.ToString("F3"), (failure.End - failure.Start).ToString("F1"),
                            failure.Minimum.ToString("F2"), failure.Station.ToString("F3"));
                    WriteTable(editor, table);
                    double at = start;
                    foreach (var range in analysis.Checked)
                    {
                        if (range.Start > at + QaGeometry.StationTolerance) editor.WriteMessage($"\nUNCHECKED data/geometry gap {at:F3}-{range.Start:F3} m");
                        at = Math.Max(at, range.End);
                    }
                    if (at < end - QaGeometry.StationTolerance) editor.WriteMessage($"\nUNCHECKED data/geometry gap {at:F3}-{end:F3} m");
                    editor.WriteMessage($"\n{analysis.Failures.Count} continuous intervals with cover less than {threshold.Value:F2} m.");
                }
                return Unit.Value;
            }, message => { editor.WriteMessage($"\nCOULD NOT CHECK terrain: {message}"); return Unit.Value; });
        }

        internal static QaCoverAnalysis AnalyzeCover(QaInput input, QaPipeData pipes, IReadOnlyList<QaSegment> surface, double threshold)
        {
            var failures = new List<QaCoverFailure>();
            var checkedRanges = new List<QaInterval>();
            var minima = new List<QaCoverSample>();
            foreach (var part in Pieces(input, pipes))
                foreach (var terrain in surface)
                {
                    double start = Math.Max(part.Start, terrain.Start), end = Math.Min(part.End, terrain.End);
                    if (end - start <= QaGeometry.LinearTolerance) continue;
                    checkedRanges.Add(new(start, end));
                    minima.Add(QaGeometry.MinimumCover(part.Curve, terrain, part.Pipe.Diameter / 2, start, end));
                    failures.AddRange(QaGeometry.Cover(part.Curve, terrain, part.Pipe.Diameter / 2, threshold, start, end));
                }
            return new(QaGeometry.MergeCover(failures), QaGeometry.Merge(checkedRanges), WorstCover(minima), Array.Empty<QaCoverSample>(), Array.Empty<double>());
        }

        internal static QaCoverAnalysis AnalyzeSampledCover(QaInput input, QaPipeData pipes, IReadOnlyList<QaSegment> surface)
        {
            var samples = new List<QaCoverSample>();
            var missing = new List<double>();
            foreach (double station in QaGeometry.CoverStations(input.Start, input.End))
            {
                var values = (from curve in input.Segments
                              where station >= curve.Start - QaGeometry.StationTolerance && station <= curve.End + QaGeometry.StationTolerance
                              from pipe in pipes.Sizes
                              where station >= pipe.Start - QaGeometry.StationTolerance && station <= pipe.End + QaGeometry.StationTolerance
                              from terrain in surface
                              where station >= terrain.Start - QaGeometry.StationTolerance && station <= terrain.End + QaGeometry.StationTolerance
                              select new QaCoverSample(station, terrain.At(station) - curve.At(station) - pipe.Diameter / 2)).ToArray();
                if (values.Length == 0) missing.Add(station);
                else samples.Add(values.OrderBy(s => s.Cover).First());
            }
            return new(Array.Empty<QaCoverFailure>(), Array.Empty<QaInterval>(), WorstCover(samples), samples, missing);
        }

        private static Option<QaCoverSample> WorstCover(IReadOnlyList<QaCoverSample> values) => values.Count == 0
            ? Option<QaCoverSample>.Nothing : Option<QaCoverSample>.Of(values.OrderBy(s => s.Cover).First());
    }
}
