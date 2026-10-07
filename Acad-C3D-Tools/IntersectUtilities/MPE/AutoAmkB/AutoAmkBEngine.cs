using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.LongitudinalProfiles;
using IntersectUtilities.UtilsCommon.Collections;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager;

using static IntersectUtilities.UtilsCommon.Utils;

using AcEntity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace IntersectUtilities.MPE.AutoAmkB;

/// <summary>The data files that are not in Stier.csv: the base map for road edges and the DKjord drawing.</summary>
internal sealed record AmkSourcePaths(Option<string> Grundkort, Option<string> DkJord, IReadOnlyList<string> Warnings);

internal sealed record AmkRunInput(
    Database FremtidDb,
    Transaction FremtidTx,
    DataReferencesOptions Project,
    LoadedRules Rules,
    AmkSourcePaths Sources,
    string Configuration);

/// <summary>
/// Runs every AUTOAMKB check for one Fremtid drawing and returns the findings. Reads only: the Fremtid
/// drawing is never changed, and temporary geometry made in the side databases is rolled back.
/// </summary>
internal static class AutoAmkBEngine
{
    public static Result<AmkReport> Analyze(AmkRunInput input, Action<AmkProgress> progress)
    {
        DataManager dataManager = new DataManager(input.Project);
        return Boundary.Try(() => dataManager.Alignments(), "Alignment-tegningen fra Stier.csv kunne ikke åbnes")
            .Bind(alignmentDb =>
            {
                using (alignmentDb)
                {
                    return AnalyzeWithAlignments(input, dataManager, alignmentDb, progress);
                }
            });
    }

    private static Result<AmkReport> AnalyzeWithAlignments(
        AmkRunInput input, DataManager dataManager, Database alignmentDb, Action<AmkProgress> progress)
    {
        AmkRules rules = input.Rules.Rules;
        List<string> warnings = new List<string>(input.Sources.Warnings);
        List<string> notEvaluated = new List<string>
        {
            "Skråninger (over 20 % hældning i ubefæstet areal): ikke vurderet, afventer projektleder."
        };
        notEvaluated.AddRange(rules.LerCriteria
            .Where(criterion => !criterion.AlongsideEnabled)
            .Select(criterion => $"{criterion.Alongside.Name}: ikke vurderet, slået fra i {AmkRulesLoader.FileName}."));
        List<DataFileInfo> files = new List<DataFileInfo>
        {
            FileInfoOf("Fremtid (aktiv tegning)", input.FremtidDb.Filename),
            FileInfoOf("Alignments", alignmentDb.Filename),
        };
        List<Hit> hits = new List<Hit>();
        List<AlignmentTrace> traces = new List<AlignmentTrace>();

        using Transaction alignmentTx = alignmentDb.TransactionManager.StartTransaction();
        Dictionary<string, Alignment> alignments = alignmentDb.ListOfType<Alignment>(alignmentTx)
            .GroupBy(alignment => alignment.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        progress(new AmkProgress("Indlæser LER ...", 0.00));
        Option<ILer3dManager> ler = Boundary.Try(() => Ler3dManagerFactory.LoadLer3d(dataManager), "LER kunne ikke indlæses")
            .Match(
                Option<ILer3dManager>.Of,
                fault =>
                {
                    warnings.Add(fault);
                    notEvaluated.Add("Ledninger fra LER (el, gas og vand): ikke vurderet, LER kunne ikke indlæses.");
                    return Option<ILer3dManager>.Nothing;
                });

        progress(new AmkProgress("Indlæser længdeprofiler ...", 0.02));
        Option<DatabaseList> profileDatabases = Boundary.Try(() => dataManager.Længdeprofiler(), "Længdeprofilerne kunne ikke åbnes")
            .Match(
                Option<DatabaseList>.Of,
                fault =>
                {
                    warnings.Add(fault);
                    notEvaluated.Add("Dyb udgravning: ikke vurderet, længdeprofilerne kunne ikke åbnes.");
                    return Option<DatabaseList>.Nothing;
                });
        ProfileSet profiles = profileDatabases.Match(databases => ProfileSet.Open(databases, warnings), () => ProfileSet.Empty);

        try
        {
            ler.Switch(
                manager => files.AddRange(Boundary.TryOption(() => manager.GetDatabases().Select(db => db.Filename).ToList())
                    .OrElse(new List<string>())
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .Select(name => FileInfoOf("LER", name))),
                () => { });
            profileDatabases.Switch(
                databases => files.AddRange(databases.Select(db => FileInfoOf("Længdeprofiler", db.Filename))),
                () => { });

            progress(new AmkProgress("Indlæser grundkort og jordforurening ...", 0.05));
            Option<SegmentIndex<string>> roadEdges = RoadEdges(input.Sources, rules, files, warnings, notEvaluated);
            Option<AreaIndex<string>> soilAreas = SoilAreas(input.Sources, rules, files, warnings, notEvaluated);

            Dictionary<string, List<AcEntity>> pipelineEntities = PipelineEntities(input, warnings);
            Option<AlongsideCheck> alongside = ler.Map(manager => AlongsideCheck.Build(manager, rules, warnings));

            int index = 0;
            foreach (Alignment alignment in alignments.Values.OrderBy(alignment => alignment.Name, StringComparer.OrdinalIgnoreCase))
            {
                index++;
                // The alignments take about nine tenths of a run (7.21.12: 27.6 of 31.5 s), so they fill most of the bar.
                // The text stays the same for all of them: the status bar restarts its meter when the text changes.
                progress(new AmkProgress(
                    $"Gennemgår {alignments.Count} alignments ...",
                    0.08 + 0.87 * (index - 1) / alignments.Count));

                AlignmentSampler.Sample(alignment, rules.SampleStep).Switch(
                    samples =>
                    {
                        TrenchWidthMap widths = TrenchWidths(alignment, pipelineEntities, warnings);
                        double unknownMetres = samples.Count(sample => !widths.At(sample.Station).IsSome()) * rules.SampleStep;
                        if (unknownMetres > 0.5)
                        {
                            warnings.Add(
                                $"Alignment {alignment.Name}: {AmkNumbers.Text(unknownMetres, "0")} m uden kendt rendebredde. "
                                + "Smal vej, jordforurening og 'langs med' er ikke vurderet der.");
                        }

                        ler.Switch(manager => hits.AddRange(LerCrossingCheck.Run(alignment, manager, rules, warnings)), () => { });
                        alongside.Switch(check => hits.AddRange(check.Run(alignment.Name, samples, widths, rules)), () => { });
                        profileDatabases.Switch(
                            _ => hits.AddRange(DepthCheck.Run(alignment.Name, profiles, new SampleLookup(samples), rules, warnings)),
                            () => { });
                        roadEdges.Switch(edges => hits.AddRange(NarrowRoadCheck.Run(alignment.Name, samples, widths, edges, rules)), () => { });
                        soilAreas.Switch(areas => hits.AddRange(SoilCheck.Run(alignment.Name, samples, widths, areas, rules)), () => { });
                        traces.Add(AlignmentTraces.Build(alignment, samples, widths, roadEdges, rules));
                    },
                    warnings.Add);
            }

            progress(new AmkProgress("Finder ventiler ...", 0.95));
            IReadOnlyList<ValveLocation> valves = ValveCollector.Collect(
                input.FremtidDb, input.FremtidTx, alignments, alignmentTx, rules, warnings);

            List<Hit> ordered = hits
                .OrderBy(hit => hit.Alignment, StringComparer.OrdinalIgnoreCase)
                .ThenBy(hit => hit.Extent.From)
                .ThenBy(hit => HitKind.All.ToList().IndexOf(hit.Kind))
                .ToList();

            return Result<AmkReport>.Success(new AmkReport(
                input.Project.ProjectName,
                input.Project.EtapeName,
                input.FremtidDb.Filename,
                DateTime.Now,
                input.Configuration,
                input.Rules,
                ordered,
                valves,
                traces,
                files,
                notEvaluated,
                warnings));
        }
        finally
        {
            profiles.Dispose();
            profileDatabases.Switch(databases => databases.Dispose(), () => { });
            ler.Switch(manager => manager.Dispose(true), () => { });
            // Station lookups make temporary polylines in the alignment drawing; rolling back removes them.
            alignmentTx.Abort();
        }
    }

    private static TrenchWidthMap TrenchWidths(
        Alignment alignment, IReadOnlyDictionary<string, List<AcEntity>> pipelineEntities, ICollection<string> warnings) =>
        pipelineEntities.Lookup(alignment.Name).Match(
            entities => TrenchWidthMap.Build(alignment, entities).Match(
                map => map,
                fault =>
                {
                    warnings.Add(fault);
                    return TrenchWidthMap.Unknown;
                }),
            () => TrenchWidthMap.Unknown);

    private static Dictionary<string, List<AcEntity>> PipelineEntities(AmkRunInput input, ICollection<string> warnings)
    {
        Dictionary<string, List<AcEntity>> byAlignment = new Dictionary<string, List<AcEntity>>(StringComparer.OrdinalIgnoreCase);
        int unassigned = 0;

        Boundary.Try(() => input.FremtidDb.GetFjvEntities(input.FremtidTx), "FJV-objekterne i tegningen kunne ikke læses").Switch(
            entities =>
            {
                foreach (AcEntity entity in entities)
                {
                    PropertySetValues.AlignmentOf(entity).Switch(
                        name =>
                        {
                            if (!byAlignment.TryGetValue(name, out List<AcEntity>? list))
                            {
                                list = new List<AcEntity>();
                                byAlignment[name] = list;
                            }
                            list.Add(entity);
                        },
                        () => unassigned++);
                }
            },
            warnings.Add);

        if (unassigned > 0)
        {
            warnings.Add(
                $"{unassigned} FJV-objekter i tegningen er ikke tilknyttet en alignment (DriPipelineData.BelongsToAlignment), "
                + "så rendebredden kendes ikke ved dem.");
        }
        return byAlignment;
    }

    private static Option<SegmentIndex<string>> RoadEdges(
        AmkSourcePaths sources, AmkRules rules, List<DataFileInfo> files, ICollection<string> warnings, ICollection<string> notEvaluated) =>
        sources.Grundkort.Match(
            path =>
            {
                files.Add(FileInfoOf("Grundkort", path));
                return ReadRoadEdges(path, rules.RoadEdgeLayer).Match(
                    index =>
                    {
                        if (index.Count == 0) warnings.Add($"Grundkortet har ingen linjer på laget '{rules.RoadEdgeLayer}'.");
                        return Option<SegmentIndex<string>>.Of(index);
                    },
                    fault =>
                    {
                        warnings.Add(fault);
                        notEvaluated.Add("Smal vej: ikke vurderet, grundkortet kunne ikke læses.");
                        return Option<SegmentIndex<string>>.Nothing;
                    });
            },
            () =>
            {
                notEvaluated.Add("Smal vej: ikke vurderet, intet grundkort valgt.");
                return Option<SegmentIndex<string>>.Nothing;
            });

    private static Option<AreaIndex<string>> SoilAreas(
        AmkSourcePaths sources, AmkRules rules, List<DataFileInfo> files, ICollection<string> warnings, ICollection<string> notEvaluated) =>
        sources.DkJord.Match(
            path =>
            {
                files.Add(FileInfoOf("DKjord", path));
                return ReadSoilAreas(path, rules).Match(
                    index =>
                    {
                        if (index.Count == 0)
                            warnings.Add($"DKjord-tegningen har ingen skraveringer på lagene '{rules.SoilLayerV1}' og '{rules.SoilLayerV2}'.");
                        return Option<AreaIndex<string>>.Of(index);
                    },
                    fault =>
                    {
                        warnings.Add(fault);
                        notEvaluated.Add("Jordforurening V1/V2: ikke vurderet, DKjord-tegningen kunne ikke læses.");
                        return Option<AreaIndex<string>>.Nothing;
                    });
            },
            () =>
            {
                notEvaluated.Add("Jordforurening V1/V2: ikke vurderet, ingen DKjord-tegning valgt.");
                return Option<AreaIndex<string>>.Nothing;
            });

    private static Result<SegmentIndex<string>> ReadRoadEdges(string path, string layer) =>
        Boundary.Try(
            () =>
            {
                using Database db = new Database(false, true);
                db.ReadDwgFile(path, FileOpenMode.OpenForReadAndAllShare, true, "");
                using Transaction tx = db.TransactionManager.StartOpenCloseTransaction();

                SegmentIndex<string> index = new SegmentIndex<string>();
                foreach (Curve curve in db.ListOfType<Curve>(tx).Where(curve => curve.Layer.Equals(layer, StringComparison.OrdinalIgnoreCase)))
                {
                    CurveCoordinates.Of(curve, tx, 0.5).Switch(coordinates => index.Add(coordinates, curve.Handle.ToString()), () => { });
                }
                return index;
            },
            $"Grundkortet kunne ikke læses: {path}");

    private static Result<AreaIndex<string>> ReadSoilAreas(string path, AmkRules rules) =>
        Boundary.Try(
            () =>
            {
                using Database db = new Database(false, true);
                db.ReadDwgFile(path, FileOpenMode.OpenForReadAndAllShare, true, "");
                using Transaction tx = db.TransactionManager.StartOpenCloseTransaction();

                Dictionary<string, string> classByLayer = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [rules.SoilLayerV1] = "V1",
                    [rules.SoilLayerV2] = "V2",
                };

                AreaIndex<string> index = new AreaIndex<string>();
                foreach (Hatch hatch in db.ListOfType<Hatch>(tx))
                {
                    classByLayer.Lookup(hatch.Layer).Switch(
                        soilClass => HatchGeometry.Of(hatch).Switch(area => index.Add(area, soilClass), () => { }),
                        () => { });
                }
                return index;
            },
            $"DKjord-tegningen kunne ikke læses: {path}");

    private static DataFileInfo FileInfoOf(string role, string path) =>
        new DataFileInfo(
            role,
            path,
            Boundary.TryOption(() => File.Exists(path)).OrElse(false)
                ? Boundary.TryOption(() => File.GetLastWriteTime(path))
                : Option<DateTime>.Nothing);
}
