using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using IntersectUtilities.LongitudinalProfiles;
using System;
using System.Collections.Generic;
using System.Linq;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Unit = IntersectUtilities.LongitudinalProfiles.Unit;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        /// <command>PROFILEBASELINE</command>
        /// <summary>Replaces a guide with desired/minimum-radius crest/sag geometry at or below it; failed sections may be left as gaps.</summary>
        /// <category>Longitudinal Profiles</category>
        [CommandMethod("PROFILEBASELINE", CommandFlags.Modal)]
        public void ProfileBaseline() => LongitudinalProfiles.Baseline.ProfileBaselineCommand.Run();

        /// <command>PROFILEGUIDE</command>
        /// <summary>Copies a polyline in place to 0-Hjælpelinje for manual preparation before PROFILEBASELINE consumes it.</summary>
        /// <category>Longitudinal Profiles</category>
        [CommandMethod("PROFILEGUIDE", CommandFlags.Modal)]
        public void ProfileGuide() => LongitudinalProfiles.Baseline.ProfileBaselineCommand.CopyGuide();
    }
}

namespace IntersectUtilities.LongitudinalProfiles.Baseline
{
    internal static partial class ProfileBaselineCommand
    {
        private const string OutputLayer = "0-Hjælpelinje";
        private sealed record GuideInput(ObjectId SourceId, ObjectId OwnerId, double Elevation, IReadOnlyList<BaselinePoint> Points);

        internal static void Run() => AtCommandBoundary(document =>
        {
            Editor editor = document.Editor;
            var pick = SelectPolyline(editor, "\nSelect the guide polyline to replace: ");
            if (pick.Status != PromptStatus.OK) return;
            ReadGuide(document.Database, pick.ObjectId).Match(input =>
            {
                var options = new PromptDoubleOptions("\nDesired arc radius in metres: ") { AllowNegative = false, AllowZero = false, AllowNone = false };
                var radius = editor.GetDouble(options);
                if (radius.Status != PromptStatus.OK) return Unit.Value;
                if (!double.IsFinite(radius.Value) || radius.Value <= ProfileBaselineGeometry.LinearTolerance)
                {
                    editor.WriteMessage("\nDesired radius must be a finite positive number. Guide retained.");
                    return Unit.Value;
                }
                ProfileBaselineGeometry.Analyze(input.Points, radius.Value).Match(plan =>
                {
                    if (plan.Problems.Count == 0) WriteResult(document, input, plan.Parts);
                    else ReportAndOfferPartial(document, input, plan, radius.Value);
                    return Unit.Value;
                }, message => { editor.WriteMessage($"\nPROFILEBASELINE: {message} Guide retained."); return Unit.Value; });
                return Unit.Value;
            }, message => { editor.WriteMessage($"\nPROFILEBASELINE: {message}"); return Unit.Value; });
        });

        internal static void CopyGuide() => AtCommandBoundary(document =>
        {
            var pick = SelectPolyline(document.Editor, "\nSelect a polyline to copy to 0-Hjælpelinje: ");
            if (pick.Status != PromptStatus.OK) return;
            using var transaction = document.Database.TransactionManager.StartTransaction();
            var source = (Polyline)transaction.GetObject(pick.ObjectId, OpenMode.ForRead);
            EnsureLayer(document.Database, transaction).Match(layerId =>
            {
                using var copy = (Polyline)source.Clone();
                SetHelperProperties(copy, layerId);
                var space = (BlockTableRecord)transaction.GetObject(source.OwnerId, OpenMode.ForWrite);
                ObjectId id = space.AppendEntity(copy);
                transaction.AddNewlyCreatedDBObject(copy, true);
                transaction.Commit();
                document.Editor.WriteMessage("\nCreated helper-line copy. Original retained. Edit the copy, then run PROFILEBASELINE to replace it.");
                document.Editor.SetImpliedSelection(new[] { id });
                return Unit.Value;
            }, message => { document.Editor.WriteMessage($"\nPROFILEGUIDE: {message}"); return Unit.Value; });
        });

        private static void AtCommandBoundary(Action<Document> action)
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            try { action(document); }
            catch (Autodesk.AutoCAD.Runtime.Exception exception)
            { document.Editor.WriteMessage($"\nProfile tool: AutoCAD error: {exception.Message}"); }
            catch (System.Exception exception)
            { document.Editor.WriteMessage($"\nProfile tool: unexpected error: {exception}"); }
        }

        private static Option<double> PromptMinimumRadius(Editor editor, double desiredRadius)
        {
            while (true)
            {
                var options = new PromptDoubleOptions("\nMinimum fallback radius in metres: ")
                {
                    AllowNegative = false, AllowZero = false, AllowNone = false
                };
                PromptDoubleResult result = editor.GetDouble(options);
                if (result.Status != PromptStatus.OK) return Option<double>.Nothing;
                if (double.IsFinite(result.Value) && result.Value > ProfileBaselineGeometry.LinearTolerance && result.Value <= desiredRadius)
                    return Option<double>.Of(result.Value);
                editor.WriteMessage("\nMinimum radius must be positive and no greater than the desired radius.");
            }
        }

        private static PromptEntityResult SelectPolyline(Editor editor, string message)
        {
            var options = new PromptEntityOptions(message);
            options.AllowObjectOnLockedLayer = true;
            options.SetRejectMessage("\nSelect a lightweight 2D polyline.");
            options.AddAllowedClass(typeof(Polyline), true);
            return editor.GetEntity(options);
        }

        private static Result<GuideInput> ReadGuide(Database database, ObjectId id)
        {
            using var transaction = database.TransactionManager.StartTransaction();
            var source = (Polyline)transaction.GetObject(id, OpenMode.ForRead);
            if (source.Closed || source.NumberOfVertices < 2 || !IsUp(source.Normal) || !double.IsFinite(source.Elevation))
                return Result<GuideInput>.Failure("The guide must be open, have normal +Z and contain at least two vertices in the XY plane.");
            var layer = (LayerTableRecord)transaction.GetObject(source.LayerId, OpenMode.ForRead);
            if (layer.IsLocked || layer.IsDependent)
                return Result<GuideInput>.Failure("The selected guide must be editable because it will be replaced. Use PROFILEGUIDE to copy a protected original first.");
            var points = new List<BaselinePoint>();
            for (int i = 0; i < source.NumberOfVertices; i++)
            {
                if (i < source.NumberOfVertices - 1 && source.GetBulgeAt(i) != 0)
                    return Result<GuideInput>.Failure($"Guide edge {i + 1} is an arc. Prepare a guide containing straight edges only.");
                Point3d point = source.GetPoint3dAt(i);
                points.Add(new(point.X, point.Y));
            }
            return Result<GuideInput>.Success(new(id, source.OwnerId, source.Elevation, points.AsReadOnly()));
        }

        private static void ReportAndOfferPartial(Document document, GuideInput input, BaselinePlan plan, double desiredRadius)
        {
            ResolveStationView(document, input).Match(view =>
            {
                OfferFailureChoice(document, input, plan, desiredRadius, view, true);
                return Unit.Value;
            }, () => { document.Editor.WriteMessage("\nAborted. Guide retained."); return Unit.Value; });
        }

        private static void OfferFailureChoice(Document document, GuideInput input, BaselinePlan plan,
            double desiredRadius, StationView view, bool allowMinimum)
        {
            Editor editor = document.Editor;
            if (!ReportProblems(document, view, plan.Problems))
            { editor.WriteMessage("\nUnable to resolve all failed stations. Guide retained."); return; }
            editor.WriteMessage(plan.Parts.Count > 0
                ? $"\n{plan.Parts.Count} valid pieces can be created, with gaps at the failed sections."
                : "\nNo valid geometry remains at the attempted radius.");
            var options = new PromptKeywordOptions("\nBaseline failed") { AllowNone = true };
            if (allowMinimum) options.Keywords.Add("Minimum");
            options.Keywords.Add("ANyway"); options.Keywords.Add("ABort"); options.Keywords.Default = "ABort";
            PromptResult response = editor.GetKeywords(options);
            if (allowMinimum && response.Status == PromptStatus.OK && response.StringResult == "Minimum")
            {
                PromptMinimumRadius(editor, desiredRadius).Match(minimum =>
                {
                    ProfileBaselineGeometry.RetryWithMinimum(input.Points, desiredRadius, minimum, plan).Match(retry =>
                    {
                        if (retry.MinimumVertices.Count > 0)
                            editor.WriteMessage($"\nRetried {retry.MinimumVertices.Count} corner(s) at minimum R={minimum:g} m; other corners keep desired R={desiredRadius:g} m.");
                        if (retry.Problems.Count == 0) WriteResult(document, input, retry.Parts);
                        else OfferFailureChoice(document, input, retry, desiredRadius, view, false);
                        return Unit.Value;
                    }, message => { editor.WriteMessage($"\nPROFILEBASELINE: {message} Guide retained."); return Unit.Value; });
                    return Unit.Value;
                }, () => { editor.WriteMessage("\nAborted. Guide retained."); return Unit.Value; });
            }
            else if (response.Status == PromptStatus.OK && string.Equals(response.StringResult, "Anyway", StringComparison.OrdinalIgnoreCase))
            {
                if (plan.Parts.Count > 0) WriteResult(document, input, plan.Parts);
                else editor.WriteMessage("\nNo valid geometry to create. Guide retained.");
            }
            else editor.WriteMessage("\nAborted. Guide retained.");
        }

        private static void WriteResult(Document document, GuideInput input, IReadOnlyList<BaselineSolution> parts)
            => Commit(document.Database, input, parts).Match(ids =>
            {
                document.Editor.WriteMessage($"\nCreated {ids.Count} baseline polyline(s) on {OutputLayer}; selected guide replaced. UNDO restores it.");
                return Unit.Value;
            }, message => { document.Editor.WriteMessage($"\nPROFILEBASELINE: {message} Guide retained."); return Unit.Value; });

        private static Result<IReadOnlyList<ObjectId>> Commit(Database database, GuideInput input, IReadOnlyList<BaselineSolution> parts)
        {
            if (parts.Count == 0) return Result<IReadOnlyList<ObjectId>>.Failure("There are no valid pieces to create.");
            using var transaction = database.TransactionManager.StartTransaction();
            var source = (Polyline)transaction.GetObject(input.SourceId, OpenMode.ForRead);
            var sourceLayer = (LayerTableRecord)transaction.GetObject(source.LayerId, OpenMode.ForRead);
            if (sourceLayer.IsLocked || sourceLayer.IsDependent || !SameGuide(source, input))
                return Result<IReadOnlyList<ObjectId>>.Failure("The guide changed or became locked during the command.");
            return EnsureLayer(database, transaction).Match(layerId =>
            {
                var ids = new List<ObjectId>();
                var space = (BlockTableRecord)transaction.GetObject(input.OwnerId, OpenMode.ForWrite);
                foreach (var part in parts)
                {
                    using var polyline = new Polyline(part.Vertices.Count);
                    polyline.SetDatabaseDefaults(database);
                    polyline.Normal = Vector3d.ZAxis; polyline.Elevation = input.Elevation; polyline.ConstantWidth = 0;
                    SetHelperProperties(polyline, layerId);
                    foreach (var vertex in part.Vertices)
                        polyline.AddVertexAt(polyline.NumberOfVertices, new Point2d(vertex.Point.X, vertex.Point.Y), vertex.Bulge, 0, 0);
                    ids.Add(space.AppendEntity(polyline)); transaction.AddNewlyCreatedDBObject(polyline, true);
                }
                // Erasing the guide and writing every output piece share one transaction.
                source.UpgradeOpen(); source.Erase(); transaction.Commit();
                return Result<IReadOnlyList<ObjectId>>.Success(ids.AsReadOnly());
            }, Result<IReadOnlyList<ObjectId>>.Failure);
        }

        private static bool SameGuide(Polyline source, GuideInput input)
        {
            if (source.Closed || source.OwnerId != input.OwnerId || source.NumberOfVertices != input.Points.Count || !IsUp(source.Normal) || !double.IsFinite(source.Elevation) ||
                Math.Abs(source.Elevation - input.Elevation) > ProfileBaselineGeometry.LinearTolerance) return false;
            for (int i = 0; i < source.NumberOfVertices; i++)
            {
                Point3d p = source.GetPoint3dAt(i);
                var point = new BaselinePoint(p.X, p.Y);
                if (!point.IsFinite || (point - input.Points[i]).Length > ProfileBaselineGeometry.LinearTolerance ||
                    i < source.NumberOfVertices - 1 && source.GetBulgeAt(i) != 0) return false;
            }
            return true;
        }

        private static bool IsUp(Vector3d normal)
            => Math.Abs(normal.X) <= 1e-12 && Math.Abs(normal.Y) <= 1e-12 && Math.Abs(normal.Z - 1) <= 1e-12;

        private static void SetHelperProperties(Polyline entity, ObjectId layerId)
        { entity.LayerId = layerId; entity.ColorIndex = 256; entity.Linetype = "ByLayer"; entity.LineWeight = LineWeight.ByLayer; }

        private static Result<ObjectId> EnsureLayer(Database database, Transaction transaction)
        {
            var layers = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
            if (layers.Has(OutputLayer))
            {
                var layer = (LayerTableRecord)transaction.GetObject(layers[OutputLayer], OpenMode.ForRead);
                return layer.IsLocked || layer.IsOff || layer.IsFrozen || layer.IsDependent
                    ? Result<ObjectId>.Failure($"Layer {OutputLayer} must be local, unlocked, on and thawed.")
                    : Result<ObjectId>.Success(layer.ObjectId);
            }
            layers.UpgradeOpen();
            using var created = new LayerTableRecord { Name = OutputLayer, Color = Color.FromColorIndex(ColorMethod.ByAci, 2), LineWeight = LineWeight.LineWeight050 };
            ObjectId id = layers.Add(created); transaction.AddNewlyCreatedDBObject(created, true);
            return Result<ObjectId>.Success(id);
        }
    }
}
