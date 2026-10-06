using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

using IntersectUtilities.MPE.AutoAmkB;
using IntersectUtilities.UtilsCommon.DataManager;
using IntersectUtilities.UtilsCommon.DataManager.CsvData;

using static IntersectUtilities.UtilsCommon.Utils;

using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using FormsDialogResult = System.Windows.Forms.DialogResult;
using FormsOpenFileDialog = System.Windows.Forms.OpenFileDialog;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        private const string AutoAmkBCommandName = "AUTOAMKB";

        /// <command>AUTOAMKB</command>
        /// <summary>
        /// Finds the work-environment risks the AMK logbook and journal ask about along every alignment of the
        /// active Fremtid drawing, and writes them to an Excel file next to the drawing, named
        /// AUTOAMKB_[project]_[etape]_[date].xlsx. Logbook: crossings of cables of 10 kV and more and of gas and
        /// water mains of Ø100 and more (in service, known size only), gas mains running alongside the trench,
        /// excavation deeper than 2.5 m, roads with under 3 m free width beside the trench and its barriers, and
        /// trench footprint over soil contamination V1/V2. Journal: three rows per valve placement. Slopes are not
        /// evaluated yet and the run info says so. The sheets put rows where the AMK templates expect them, so
        /// B14:F... pastes straight into a template as values; source data sits from column Q. Thresholds and texts
        /// come from AutoAmkB.csv in the Conf folder, or built-in defaults when that file is missing. The project
        /// and etape are found through Stier.csv (subst drives are matched to their X: paths); the base map is
        /// taken from the drawing's xrefs and DKjord from the base map's folder, with a picker when that fails,
        /// remembered in AutoAmkBSettings.json next to the drawing. The drawing itself is only read, never changed.
        /// Requires an active CSV configuration (DKv1/DKv2/DEv1).
        /// </summary>
        /// <category>MPE</category>
        [CommandMethod(AutoAmkBCommandName, CommandFlags.Modal)]
        public void autoamkb()
        {
            DocumentCollection docCol = AcadApp.DocumentManager;
            Document doc = docCol.MdiActiveDocument;
            Database localDb = doc.Database;
            Editor editor = doc.Editor;

            using Transaction tx = localDb.TransactionManager.StartTransaction();
            try
            {
                AutoAmkBCommand.Run(doc, tx, Option<string>.Nothing, message => editor.WriteMessage($"\n{message}")).Switch(
                    summary => editor.WriteMessage(summary),
                    fault => editor.WriteMessage($"\n{AutoAmkBCommandName} stoppede: {fault}"));
                tx.Commit();
            }
            catch (System.Exception ex)
            {
                tx.Abort();
                prdDbg(ex);
                editor.WriteMessage($"\n{AutoAmkBCommandName} fejlede. Se debug-output for detaljer.");
                return;
            }
        }
    }
}

namespace IntersectUtilities.MPE.AutoAmkB
{
    internal static class AutoAmkBCommand
    {
        /// <summary>
        /// Runs AUTOAMKB on a document that need not be the active one and writes to the given file instead of
        /// next to the drawing. For scripted runs; returns the summary, or the reason it stopped.
        /// </summary>
        internal static string RunToFile(Document document, string outputPath)
        {
            using DocumentLock documentLock = document.LockDocument();
            using Transaction tx = document.Database.TransactionManager.StartTransaction();
            string message = Run(document, tx, Option<string>.Of(outputPath), prdDbg)
                .Match(summary => summary, fault => $"AUTOAMKB stoppede: {fault}");
            tx.Commit();
            return message;
        }

        public static Result<string> Run(Document document, Transaction tx, Option<string> outputPath, Action<string> progress)
        {
            Database db = document.Database;

            return AmkPreconditions.Check(db).Bind(preconditions =>
                AmkProject.Resolve(preconditions.DrawingPath).Bind(project =>
                    AmkRulesLoader.Load().Bind(rules =>
                    {
                        AmkSourcePaths sources = AmkSources.Resolve(db, preconditions.DrawingPath);
                        AmkRunInput input = new AmkRunInput(
                            db,
                            tx,
                            project.Options,
                            rules,
                            sources with { Warnings = project.Warnings.Concat(sources.Warnings).ToList() },
                            preconditions.Configuration);

                        return AutoAmkBEngine.Analyze(input, progress).Bind(report =>
                            AutoAmkBExport.Write(report, outputPath.OrElse(AmkOutput.PathFor(preconditions.DrawingPath, project.Options)))
                                .Map(path => AmkOutput.Summary(report, path)));
                    })));
        }
    }

    internal sealed record AmkPreconditions(string DrawingPath, string Configuration)
    {
        public static Result<AmkPreconditions> Check(Database db)
        {
            string drawingPath = db.Filename;
            if (string.IsNullOrWhiteSpace(drawingPath) || !Boundary.TryOption(() => File.Exists(drawingPath)).OrElse(false))
            {
                return Result<AmkPreconditions>.Failure("Gem tegningen først. AUTOAMKB finder projektet ud fra tegningens placering.");
            }

            // Krydsninger.csv, which the LER reader uses to skip ignored layers, needs an active configuration.
            return Boundary.TryOption(() => ConfigurationManager.ActiveConfiguration)
                .Bind(value => AmkNullable.Of(value))
                .Match(
                    configuration => Result<AmkPreconditions>.Success(new AmkPreconditions(drawingPath, configuration)),
                    () => Result<AmkPreconditions>.Failure("Ingen CSV-konfiguration er valgt (DKv1, DKv2 eller DEv1). Vælg en og kør igen."));
        }
    }

    internal sealed record AmkProject(DataReferencesOptions Options, IReadOnlyList<string> Warnings)
    {
        /// <summary>
        /// Finds the project and etape whose Stier.csv row lists this drawing. The drawing may be opened through
        /// a subst drive (C:\1\Norsyn is X:), so every alias of its path is tried before asking the user.
        /// </summary>
        public static Result<AmkProject> Resolve(string drawingPath)
        {
            IReadOnlyList<string> aliases = PathAliases.For(drawingPath);

            return Boundary.Try(
                    () => aliases
                        .SelectMany(StierManager.DetectProjectAndEtape)
                        .Distinct()
                        .OrderBy(key => key.ProjectId)
                        .ThenBy(key => key.EtapeId)
                        .ToList(),
                    "Stier.csv kunne ikke læses")
                .Bind(matches => matches.Count switch
                {
                    1 => Result<AmkProject>.Success(new AmkProject(
                        new DataReferencesOptions(matches[0].ProjectId, matches[0].EtapeId), Array.Empty<string>())),
                    0 => AskForProject(),
                    _ => ChooseAmong(matches),
                })
                .Map(project => project with { Warnings = project.Warnings.Concat(CheckFremtidPath(project.Options, aliases)).ToList() });
        }

        private static Result<AmkProject> ChooseAmong(IReadOnlyList<(string ProjectId, string EtapeId)> matches)
        {
            Dictionary<string, (string ProjectId, string EtapeId)> byLabel =
                matches.ToDictionary(match => $"{match.ProjectId}:{match.EtapeId}", match => match);

            return Pick(byLabel.Keys, "Tegningen findes i flere etaper i Stier.csv. Vælg én:")
                .Bind(label => byLabel.Lookup(label))
                .Match(
                    match => Result<AmkProject>.Success(new AmkProject(
                        new DataReferencesOptions(match.ProjectId, match.EtapeId), Array.Empty<string>())),
                    () => Result<AmkProject>.Failure("Annulleret."));
        }

        private static Result<AmkProject> AskForProject() =>
            Boundary.TryOption(() => DataReferencesOptions.Create(useAuto: false)).Bind(value => AmkNullable.Of(value))
                .Match(
                    options => Result<AmkProject>.Success(new AmkProject(
                        options,
                        new[] { "Tegningen findes ikke i Stier.csv; projekt og etape blev valgt manuelt." })),
                    () => Result<AmkProject>.Failure("Annulleret."));

        // The valves and pipes are read from the open drawing, the rest from the etape's files in Stier.csv;
        // if those are not the same etape the results mix, so say so.
        private static IEnumerable<string> CheckFremtidPath(DataReferencesOptions options, IReadOnlyList<string> aliases) =>
            Boundary.TryOption(() => new DataManager(options).PathToFremtid()).Match(
                fremtid => aliases.Any(alias => string.Equals(alias, fremtid, StringComparison.OrdinalIgnoreCase))
                    ? Array.Empty<string>()
                    : new[] { $"Den åbne tegning er ikke etapens Fremtid-tegning i Stier.csv ({fremtid}). Ventiler og rør er læst fra den åbne tegning." },
                () => Array.Empty<string>());

        private static Option<string> Pick(IEnumerable<string> choices, string title) =>
            Boundary.TryOption(() => StringGridFormCaller.Call(choices, title)).Bind(value => AmkNullable.Of(value));
    }

    internal static class AmkSources
    {
        private const string SettingsFileName = "AutoAmkBSettings.json";

        private sealed record StoredPaths(string Grundkort, string DkJord);

        /// <summary>
        /// The base map comes from the drawing's xrefs (one whose name contains "Grundkort"), DKjord from the base
        /// map's folder (DKjord*.dwg). When either needs a picker, the choice is saved next to the drawing.
        /// </summary>
        public static AmkSourcePaths Resolve(Database db, string drawingPath)
        {
            List<string> warnings = new List<string>();
            string settingsPath = Path.Combine(Path.GetDirectoryName(drawingPath) ?? "", SettingsFileName);
            Option<StoredPaths> stored = ReadStored(settingsPath);
            bool asked = false;

            Option<string> grundkort = stored.Map(paths => paths.Grundkort).Where(Exists).Match(
                Option<string>.Of,
                () =>
                {
                    IReadOnlyDictionary<string, string> xrefs = XrefFiles(db);
                    List<string> named = xrefs.Where(xref => xref.Key.Contains("grundkort", StringComparison.OrdinalIgnoreCase))
                        .Select(xref => xref.Value)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    if (named.Count == 1) return Option<string>.Of(named[0]);

                    asked = true;
                    return Pick(xrefs.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase), "VÆLG GRUNDKORT-XREF (vejkanter):")
                        .Bind(name => xrefs.Lookup(name));
                });

            Option<string> dkJord = stored.Map(paths => paths.DkJord).Where(Exists).Match(
                Option<string>.Of,
                () =>
                {
                    List<string> found = grundkort
                        .Map(path => Boundary.TryOption(() => Directory.GetFiles(Path.GetDirectoryName(path) ?? "", "DKjord*.dwg"))
                            .OrElse(Array.Empty<string>()))
                        .OrElse(Array.Empty<string>())
                        .ToList();
                    if (found.Count == 1) return Option<string>.Of(found[0]);

                    asked = true;
                    return AskForFile("Vælg DKjord-tegningen (jordforurening V1/V2)", grundkort.Map(path => Path.GetDirectoryName(path) ?? "").OrElse(""));
                });

            if (asked)
            {
                Boundary.Try(
                        () =>
                        {
                            File.WriteAllText(settingsPath, JsonSerializer.Serialize(
                                new StoredPaths(grundkort.OrElse(""), dkJord.OrElse("")),
                                new JsonSerializerOptions { WriteIndented = true }));
                            return true;
                        },
                        $"Valget af grundkort og DKjord kunne ikke gemmes i {settingsPath}")
                    .FaultMessage()
                    .Switch(warnings.Add, () => { });
            }

            return new AmkSourcePaths(grundkort, dkJord, warnings);
        }

        private static bool Exists(string path) => path.Length > 0 && Boundary.TryOption(() => File.Exists(path)).OrElse(false);

        private static Option<StoredPaths> ReadStored(string settingsPath) =>
            Boundary.TryOption(() => File.Exists(settingsPath))
                .Where(exists => exists)
                .Bind(_ => Boundary.TryOption(() => JsonSerializer.Deserialize<StoredPaths>(File.ReadAllText(settingsPath))))
                .Bind(paths => AmkNullable.Of(paths));

        private static IReadOnlyDictionary<string, string> XrefFiles(Database db) =>
            Boundary.TryOption(() =>
                {
                    Dictionary<string, string> files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    XrefGraph graph = db.GetHostDwgXrefGraph(true);
                    Gather(graph.RootNode, files, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                    return (IReadOnlyDictionary<string, string>)files;
                })
                .OrElse(new Dictionary<string, string>());

        private static void Gather(GraphNode node, Dictionary<string, string> files, HashSet<string> visited)
        {
            for (int i = 0; i < node.NumOut; i++)
            {
                if (node.Out(i) is not XrefGraphNode child || child.XrefStatus != XrefStatus.Resolved) continue;
                string file = child.Database.Filename;
                if (!visited.Add(file)) continue;
                files[Path.GetFileNameWithoutExtension(file)] = file;
                Gather(child, files, visited);
            }
        }

        private static Option<string> Pick(IEnumerable<string> choices, string title) =>
            Boundary.TryOption(() => StringGridFormCaller.Call(choices, title)).Bind(value => AmkNullable.Of(value));

        private static Option<string> AskForFile(string title, string initialDirectory) =>
            Boundary.TryOption(() =>
                {
                    using FormsOpenFileDialog dialog = new FormsOpenFileDialog
                    {
                        Title = title,
                        Filter = "Tegninger (*.dwg)|*.dwg",
                        CheckFileExists = true,
                        InitialDirectory = initialDirectory,
                    };
                    return dialog.ShowDialog() == FormsDialogResult.OK ? dialog.FileName : "";
                })
                .Where(path => path.Length > 0);
    }

    internal static class AmkOutput
    {
        public static string PathFor(string drawingPath, DataReferencesOptions project) =>
            Path.Combine(
                Path.GetDirectoryName(drawingPath) ?? "",
                $"AUTOAMKB_{Safe(project.ProjectName)}_{Safe(project.EtapeName)}_{DateTime.Now:yyyy-MM-dd}.xlsx");

        private static string Safe(string text) =>
            string.Concat(text.Trim().Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

        public static string Summary(AmkReport report, string path)
        {
            StringBuilder summary = new StringBuilder();
            summary.Append($"\nAUTOAMKB færdig: {report.Hits.Count} fund til logbogen, {report.Valves.Count} ventilplaceringer til journalen.");
            foreach (HitKind kind in HitKind.All)
            {
                int count = report.Hits.Count(hit => hit.Kind == kind);
                if (count > 0) summary.Append($"\n  {kind.Name}: {count}");
            }
            foreach (string item in report.NotEvaluated) summary.Append($"\n  {item}");
            if (report.Warnings.Count > 0) summary.Append($"\n{report.Warnings.Distinct().Count()} advarsler, se fanen Kørselsinfo.");
            summary.Append($"\nGemt: {path}");
            return summary.ToString();
        }
    }

    /// <summary>
    /// Every path that names the same file through a subst drive. Stier.csv stores X:\... while the drawing may
    /// be opened as C:\1\Norsyn\..., so detecting the project has to try both spellings.
    /// </summary>
    internal static class PathAliases
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint QueryDosDevice(string deviceName, StringBuilder targetPath, int maxLength);

        public static IReadOnlyList<string> For(string path)
        {
            List<string> aliases = new List<string> { path };
            foreach ((string drive, string target) in Boundary.TryOption(() => SubstDrives().ToList()).OrElse(new List<(string, string)>()))
            {
                if (path.StartsWith(target + "\\", StringComparison.OrdinalIgnoreCase)) aliases.Add(drive + path.Substring(target.Length));
                if (path.StartsWith(drive + "\\", StringComparison.OrdinalIgnoreCase)) aliases.Add(target + path.Substring(drive.Length));
            }
            return aliases.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        // A subst drive resolves to "\??\C:\some\folder"; network and physical drives resolve to device paths.
        private static IEnumerable<(string Drive, string Target)> SubstDrives()
        {
            for (char letter = 'A'; letter <= 'Z'; letter++)
            {
                string drive = $"{letter}:";
                StringBuilder buffer = new StringBuilder(1024);
                if (QueryDosDevice(drive, buffer, buffer.Capacity) == 0) continue;
                string target = buffer.ToString();
                if (target.StartsWith(@"\??\", StringComparison.Ordinal)) yield return (drive, target.Substring(4).TrimEnd('\\'));
            }
        }
    }

    /// <summary>Where third-party code answers "nothing" with null, this turns the answer into an option.</summary>
    internal static class AmkNullable
    {
        public static Option<T> Of<T>(T? value) where T : class =>
            value is null ? Option<T>.Nothing : Option<T>.Of(value);
    }
}
