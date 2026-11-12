using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.AECC.Interop.Land;
using IntersectUtilities.LongitudinalProfiles;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using CivilObject = Autodesk.Civil.DatabaseServices.DBObject;
using Unit = IntersectUtilities.LongitudinalProfiles.Unit;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        /// <command>SETMIDTLABELSET</command>
        /// <summary>
        /// Replaces the label groups of displayed MIDT profiles with one chosen label set.
        /// </summary>
        /// <category>Longitudinal Profiles</category>
        /// <execution>
        /// Run SETMIDTLABELSET in the longitudinal-profile drawing. Select a profile label set by its number in the displayed list. Existing label groups on all displayed MIDT profile/view pairs are replaced. An empty set such as _No Labels removes the groups; profile geometry is preserved.
        /// </execution>
        [CommandMethod("SETMIDTLABELSET", CommandFlags.Modal)]
        public void SetMidtLabelSet() => LongitudinalProfiles.SetMidtLabelSetCommand.Run();
    }
}

namespace IntersectUtilities.LongitudinalProfiles
{
    internal static class SetMidtLabelSetCommand
    {
        internal sealed record LabelSetChoice(ObjectId Id, string Name, int GroupCount);
        internal sealed record Target(ObjectId ViewId, ObjectId ProfileId, string Description);
        internal sealed record Catalog(LabelSetChoice[] Choices, Target[] Targets, int HiddenPairs);
        internal sealed record Applied(int Profiles, int Views, int Pairs, int RemovedGroups, int CreatedGroups);

        public static void Run()
        {
            var document = AcApplication.DocumentManager.MdiActiveDocument;
            var editor = document.Editor;
            try
            {
                ReadCatalog(document.Database, CivilApplication.ActiveDocument).Match(
                    catalog =>
                    {
                        if (catalog.Choices.Length == 0)
                            editor.WriteMessage("\nThe drawing contains no profile label sets.");
                        else if (catalog.Targets.Length == 0)
                            editor.WriteMessage("\nNo displayed MIDT profiles were found in the drawing's profile views.");
                        else
                            ChooseLabelSet(editor, catalog).Match(
                                choice =>
                                {
                                    Apply(document.Database, catalog.Targets, choice).Match(
                                        applied =>
                                        {
                                            editor.WriteMessage(
                                                $"\nApplied '{choice.Name}' to {applied.Profiles} MIDT profiles in " +
                                                $"{applied.Views} profile views ({applied.Pairs} profile/view pairs). " +
                                                $"Replaced {applied.RemovedGroups} groups with {applied.CreatedGroups} groups.");
                                            if (catalog.HiddenPairs > 0)
                                                editor.WriteMessage($"\nSkipped {catalog.HiddenPairs} hidden MIDT profile/view pairs.");
                                            return Unit.Value;
                                        },
                                        error =>
                                        {
                                            editor.WriteMessage($"\nLabel replacement failed; all replacements were rolled back. {error}");
                                            return Unit.Value;
                                        });
                                    return Unit.Value;
                                },
                                () =>
                                {
                                    editor.WriteMessage("\nCancelled. No labels were changed.");
                                    return Unit.Value;
                                });
                        return Unit.Value;
                    },
                    error =>
                    {
                        editor.WriteMessage($"\nCannot read the profile label sets: {error}");
                        return Unit.Value;
                    });
            }
            catch (System.Exception error)
            {
                // Last-resort command boundary. Apply's transaction is disposed before arriving here.
                editor.WriteMessage($"\nSETMIDTLABELSET stopped; no replacements were committed. {error.Message}");
            }
        }

        internal static bool IsMidt(string name) => name
            .Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
            .Contains("MIDT", StringComparer.OrdinalIgnoreCase);

        internal static Result<Catalog> ReadCatalog(Database database, CivilDocument civilDocument)
        {
            try
            {
                using var transaction = database.TransactionManager.StartTransaction();
                var choices = civilDocument.Styles.LabelSetStyles.ProfileLabelSetStyles.Cast<ObjectId>()
                    .Select(id => (ProfileLabelSetStyle)transaction.GetObject(id, OpenMode.ForRead))
                    .Select(style => new LabelSetChoice(style.ObjectId, ((CivilObject)style).Name, style.Count))
                    .OrderBy(choice => choice.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
                var targets = new List<Target>();
                var hiddenPairs = 0;
                foreach (ObjectId alignmentId in civilDocument.GetAlignmentIds())
                {
                    var alignment = (Alignment)transaction.GetObject(alignmentId, OpenMode.ForRead);
                    foreach (ObjectId viewId in alignment.GetProfileViewIds())
                    {
                        var view = (ProfileView)transaction.GetObject(viewId, OpenMode.ForRead);
                        foreach (ProfileOverride profileOverride in view.GraphOverrides)
                        {
                            if (!IsMidt(profileOverride.ProfileName)) continue;
                            if (!profileOverride.Draw)
                            {
                                hiddenPairs++;
                                continue;
                            }
                            targets.Add(new Target(viewId, profileOverride.ProfileId,
                                $"{profileOverride.ProfileName} in {view.Name}"));
                        }
                    }
                }
                return Result<Catalog>.Success(new Catalog(choices,
                    targets.DistinctBy(target => (target.ViewId, target.ProfileId)).ToArray(), hiddenPairs));
            }
            catch (Autodesk.AutoCAD.Runtime.Exception error)
            {
                return Result<Catalog>.Failure(error.Message);
            }
        }

        private static Option<LabelSetChoice> ChooseLabelSet(Editor editor, Catalog catalog)
        {
            editor.WriteMessage($"\nReplace labels for {catalog.Targets.Length} displayed MIDT profile/view pairs.");
            for (var index = 0; index < catalog.Choices.Length; index++)
            {
                var choice = catalog.Choices[index];
                editor.WriteMessage($"\n  {index + 1}: {choice.Name} ({choice.GroupCount} groups)");
            }
            var result = editor.GetInteger(new PromptIntegerOptions("\nChoose profile label set number: ")
            {
                LowerLimit = 1,
                UpperLimit = catalog.Choices.Length,
                AllowNone = false
            });
            return result.Status == PromptStatus.OK
                ? Option<LabelSetChoice>.Of(catalog.Choices[result.Value - 1])
                : Option<LabelSetChoice>.Nothing;
        }

        internal static Result<Applied> Apply(Database database, IReadOnlyList<Target> targets, LabelSetChoice choice)
        {
            var currentTarget = choice.Name;
            try
            {
                using var transaction = database.TransactionManager.StartTransaction();
                var style = (ProfileLabelSetStyle)transaction.GetObject(choice.Id, OpenMode.ForRead);
                if (style.AcadObject is not AeccProfileLabelStyleSet nativeStyle)
                    return Result<Applied>.Failure("Civil 3D did not expose the selected label set to its native importer.");

                Result<(int Removed, int Created)> result = Result<(int, int)>.Success((0, 0));
                foreach (var target in targets)
                {
                    currentTarget = target.Description;
                    result = result.Bind(counts => ReplaceGroups(transaction, target, nativeStyle)
                        .Map(replaced => (counts.Removed + replaced.Removed, counts.Created + replaced.Created)));
                }
                return result.Match(
                    counts =>
                    {
                        transaction.Commit();
                        return Result<Applied>.Success(new Applied(
                            targets.Select(target => target.ProfileId).Distinct().Count(),
                            targets.Select(target => target.ViewId).Distinct().Count(),
                            targets.Count, counts.Removed, counts.Created));
                    },
                    Result<Applied>.Failure);
            }
            catch (System.Runtime.InteropServices.COMException error)
            {
                return Result<Applied>.Failure($"{currentTarget}: {error.Message}");
            }
            catch (Autodesk.AutoCAD.Runtime.Exception error)
            {
                return Result<Applied>.Failure($"{currentTarget}: {error.Message}");
            }
        }

        private static Result<(int Removed, int Created)> ReplaceGroups(
            Transaction transaction, Target target, AeccProfileLabelStyleSet nativeStyle)
        {
            var view = (ProfileView)transaction.GetObject(target.ViewId, OpenMode.ForRead);
            if (view.AcadObject is not IAeccProfileView nativeView)
                return Result<(int, int)>.Failure($"Cannot access the native profile view for {target.Description}.");

            return FindOverride(nativeView, target).Map(profileOverride =>
            {
                var groups = GetGroups(target);
                foreach (ObjectId groupId in groups)
                    transaction.GetObject(groupId, OpenMode.ForWrite).Erase();

                // The managed API has no profile label-set importer. Civil 3D's native importer
                // preserves station increments, geometry selectors, anchors, weeding and staggering.
                profileOverride.ImportLabelSet(nativeStyle);
                return (groups.Count, GetGroups(target).Count);
            });
        }

        private static Result<IAeccProfileOverride> FindOverride(IAeccProfileView view, Target target)
        {
            var overrides = view.Overrides;
            for (var index = 0; index < overrides.Count; index++)
            {
                var profileOverride = overrides.Item(index);
                if (profileOverride.Profile.ObjectID == target.ProfileId.OldIdPtr.ToInt64())
                    return profileOverride.Draw
                        ? Result<IAeccProfileOverride>.Success(profileOverride)
                        : Result<IAeccProfileOverride>.Failure($"{target.Description} is no longer displayed.");
            }
            return Result<IAeccProfileOverride>.Failure($"Cannot find {target.Description} in its profile view.");
        }

        private static ObjectIdCollection GetGroups(Target target) => ProfileLabelGroup.GetAvailableLabelGroupIds(
            RXObject.GetClass(typeof(ProfileLabelGroup)), target.ViewId, target.ProfileId, true);
    }
}
