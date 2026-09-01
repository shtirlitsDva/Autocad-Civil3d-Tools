using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.MPE.Shared;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.MPE.MatchBBR
{
    internal enum MatchBbrStatusKind
    {
        Info,
        Ok,
        Warning,
        Error,
    }

    // Everything the tool knows about one document: the boundary, the workbook, the rules, and
    // the last comparison. The palette is process-wide and outlives any single drawing, so all of
    // this hangs off the state object and is swapped when the active document changes.
    //
    // Every method that reaches into the drawing takes the document lock itself — the palette runs
    // outside a command context, so nothing is locked for it.
    internal sealed class MatchBbrState : IDisposable
    {
        private readonly List<ObjectId> _boundaryIds = new List<ObjectId>();
        private readonly List<ExcelRowRecord> _excelRows = new List<ExcelRowRecord>();
        private readonly List<CompareRule> _rules = new List<CompareRule>();

        // Owned per document, which is also what the transient manager is bound to.
        private readonly MatchBbrPreview _preview = new MatchBbrPreview();

        // Address key -> why a block carrying that address was left out of the compared set.
        private Dictionary<string, string> _unmatchedHints =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public MatchBbrState(Document owner)
        {
            Owner = owner;
        }

        public Document Owner { get; }

        public Action<string, MatchBbrStatusKind>? StatusSink { get; set; }

        public string? WorkbookPath { get; private set; }

        public string? SheetName { get; private set; }

        public XlsxSheet Sheet { get; private set; } = XlsxSheet.Empty(string.Empty);

        public IReadOnlyList<string> SheetNames { get; private set; } = Array.Empty<string>();

        public IReadOnlyList<ExcelRowRecord> ExcelRows => _excelRows;

        public IReadOnlyList<CompareRule> Rules => _rules;

        public int BoundaryCount { get; private set; }

        public IReadOnlyList<BbrRecord> BbrRecords { get; private set; } = Array.Empty<BbrRecord>();

        public MatchResult Result { get; private set; } = MatchResult.Empty;

        // Which Excel column supplies the district used for the marker layer names. Optional:
        // without it, matched markers fall back to the block's own Distriktets_navn.
        public string? DistrictColumn { get; set; }

        public bool SkipTypeIngen { get; set; } = true;

        public bool IsActive() =>
            Owner is not null
            && !Owner.IsDisposed
            && Application.DocumentManager.MdiActiveDocument == Owner;

        // ---- Workbook ------------------------------------------------------

        public bool LoadWorkbook(string path, string? sheetName)
        {
            if (!File.Exists(path))
            {
                SetStatus("Excel-filen blev ikke fundet.", MatchBbrStatusKind.Error);
                return false;
            }

            try
            {
                SheetNames = XlsxReader.ListSheetNames(path);
                Sheet = XlsxReader.Read(path, sheetName);
                WorkbookPath = path;
                SheetName = Sheet.Name;

                _excelRows.Clear();
                foreach (XlsxRow row in Sheet.Rows)
                {
                    _excelRows.Add(new ExcelRowRecord(row));
                }

                Result = MatchResult.Empty;

                if (Sheet.IsEmpty)
                {
                    SetStatus($"Arket '{Sheet.Name}' er tomt.", MatchBbrStatusKind.Warning);
                    return false;
                }

                SetStatus(
                    $"Indlæste {Sheet.Rows.Count} rækker fra '{Sheet.Name}'.",
                    MatchBbrStatusKind.Ok);
                return true;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                SetStatus("Excel-filen kunne ikke læses. Se debug-output.", MatchBbrStatusKind.Error);
                return false;
            }
        }

        public void ReplaceRules(IEnumerable<CompareRule> rules)
        {
            _rules.Clear();
            _rules.AddRange(rules);
        }

        // The label a rule is shown and exported under. The BBR property name is the least
        // ambiguous choice: Excel headers repeat and are sometimes blank.
        public static string LabelFor(CompareRule rule) =>
            string.IsNullOrWhiteSpace(rule.BbrPropertyName) ? "(uden navn)" : rule.BbrPropertyName;

        // ---- Boundary ------------------------------------------------------

        // Interactive pick from a palette button. GetSelection is called directly, as the other
        // MPE palettes do; AutoCAD routes it to the active document.
        public bool PickBoundary()
        {
            if (!IsActive())
            {
                SetStatus("Tegningen er ikke aktiv.", MatchBbrStatusKind.Warning);
                return false;
            }

            Editor editor = Owner.Editor;
            SelectionFilter filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
            });
            PromptSelectionOptions options = new PromptSelectionOptions
            {
                MessageForAdding = "\nVælg lukkede afgrænsningspolylinjer: ",
            };

            PromptSelectionResult result = editor.GetSelection(options, filter);
            if (result.Status != PromptStatus.OK || result.Value is null || result.Value.Count == 0)
            {
                SetStatus("Valg annulleret eller tomt.", MatchBbrStatusKind.Warning);
                return false;
            }

            _boundaryIds.Clear();
            _boundaryIds.AddRange(result.Value.GetObjectIds());
            BoundaryCount = _boundaryIds.Count;

            SetStatus($"{BoundaryCount} afgrænsning(er) valgt.", MatchBbrStatusKind.Ok);
            return true;
        }

        public bool HasBoundary => _boundaryIds.Count > 0;

        // ---- Drawing side --------------------------------------------------

        public bool ReloadFromDrawing()
        {
            if (!IsActive())
            {
                SetStatus("Tegningen er ikke aktiv.", MatchBbrStatusKind.Warning);
                return false;
            }

            if (!HasBoundary)
            {
                SetStatus("Vælg en afgrænsning først.", MatchBbrStatusKind.Warning);
                return false;
            }

            Database database = Owner.Database;

            try
            {
                HashSet<string> wantedProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (CompareRule rule in _rules.Where(r => r.IsConfigured))
                {
                    wantedProperties.Add(rule.BbrPropertyName);
                }

                using DocumentLock documentLock = Owner.LockDocument();

                // Two constraints pull in opposite directions here, so the order matters:
                // UpdatePropertySetDefinition writes to the drawing, so it needs the document
                // lock, but it also refuses to run while a transaction is open (it starts its
                // own). Hence: lock first, update, then start the transaction.
                PropertySetManager.UpdatePropertySetDefinition(database, PSetDefs.DefinedSets.BBR);

                // Nothing but the read happens inside the transaction. Reporting and status
                // updates run after it is closed, so there is no path on which Abort can follow
                // Commit — the failure mode the tool was rewritten to remove.
                List<string> rejected;
                BbrGatherResult gathered;

                using (Transaction tx = database.TransactionManager.StartTransaction())
                {
                    try
                    {
                        BoundarySnapshot boundary =
                            BoundarySnapshot.Capture(_boundaryIds, tx, out rejected);

                        if (boundary.IsEmpty)
                        {
                            tx.Abort();
                            SetStatus(
                                rejected.Count > 0
                                    ? $"Ingen brugbare afgrænsninger. Afvist: {string.Join(", ", rejected)}"
                                    : "Ingen brugbare afgrænsninger i valget.",
                                MatchBbrStatusKind.Warning);
                            return false;
                        }

                        Boundary = boundary;

                        gathered = MatchBbrGather.Collect(
                            database, tx, boundary, wantedProperties, SkipTypeIngen);

                        tx.Commit();
                    }
                    catch (System.Exception)
                    {
                        tx.Abort();
                        throw;
                    }
                }

                BbrRecords = gathered.Inside;
                _unmatchedHints = BuildUnmatchedHints(gathered);

                string rejectedText = rejected.Count > 0
                    ? $" ({rejected.Count} afgrænsning(er) afvist: {string.Join(", ", rejected)})"
                    : string.Empty;

                // The excluded counts are reported up front: a boundary that misses half the
                // area shows here rather than as a wall of "kun i Excel" rows later.
                string excludedText = string.Empty;
                if (gathered.OutsideBoundary.Count > 0 || gathered.SkippedByType.Count > 0)
                {
                    excludedText =
                        $" Udeladt: {gathered.OutsideBoundary.Count} uden for afgrænsningen, "
                        + $"{gathered.SkippedByType.Count} med Type = \"Ingen\".";
                }

                SetStatus(
                    $"Fandt {BbrRecords.Count} BBR-blokke i {Boundary.Count} afgrænsning(er).{rejectedText}{excludedText}",
                    BbrRecords.Count > 0 ? MatchBbrStatusKind.Ok : MatchBbrStatusKind.Warning);
                return BbrRecords.Count > 0;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                SetStatus("Indlæsning fra tegningen mislykkedes. Se debug-output.", MatchBbrStatusKind.Error);
                return false;
            }
        }

        public BoundarySnapshot Boundary { get; private set; } = BoundarySnapshot.Empty;

        // ---- Comparison ----------------------------------------------------

        public bool Compare()
        {
            if (Sheet.IsEmpty)
            {
                SetStatus("Indlæs en Excel-fil først.", MatchBbrStatusKind.Warning);
                return false;
            }

            if (!_rules.Any(r => r.IsKey && r.IsConfigured))
            {
                SetStatus("Vælg en nøgleregel med både Excel-kolonne og BBR-egenskab.", MatchBbrStatusKind.Warning);
                return false;
            }

            List<ExcelRowRecord> included = _excelRows.Where(r => r.IsIncluded).ToList();
            if (included.Count == 0)
            {
                SetStatus("Ingen Excel-rækker er valgt.", MatchBbrStatusKind.Warning);
                return false;
            }

            Result = MatchBbrMatcher.Run(included, BbrRecords, _rules, LabelFor, _unmatchedHints);
            SetStatus(Result.Summary(), MatchBbrStatusKind.Ok);
            return true;
        }

        // Indexes the excluded blocks by every key their address can be found under, so an
        // unmatched Excel row can say which exclusion caught it. Type = "Ingen" is applied last
        // and wins: those blocks are inside the boundary, so it is the more specific answer.
        private static Dictionary<string, string> BuildUnmatchedHints(BbrGatherResult gathered)
        {
            Dictionary<string, string> hints =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string address in gathered.OutsideBoundary)
            {
                foreach (string key in MatchBbrAddressKey.BuildKeys(address))
                {
                    hints[key] = "Findes i tegningen, men uden for afgrænsningen";
                }
            }

            foreach (string address in gathered.SkippedByType)
            {
                foreach (string key in MatchBbrAddressKey.BuildKeys(address))
                {
                    hints[key] = "Findes i tegningen, men Type = \"Ingen\"";
                }
            }

            return hints;
        }

        // ---- Actions on the drawing ----------------------------------------

        public bool IsPreviewVisible => _preview.IsVisible;

        // Transient graphics only — nothing is written to the drawing, so there is never anything
        // to clean up afterwards.
        public bool ShowPreview(IEnumerable<ComparisonRow> rows)
        {
            if (!IsActive())
            {
                SetStatus("Tegningen er ikke aktiv.", MatchBbrStatusKind.Warning);
                return false;
            }

            if (Result.Rows.Count == 0)
            {
                SetStatus("Kør en sammenligning først.", MatchBbrStatusKind.Warning);
                return false;
            }

            try
            {
                int drawn = _preview.Show(rows);
                SetStatus(
                    drawn == 0
                        ? "Ingen af de viste rækker har en blok at markere."
                        : $"Forhåndsvisning: {drawn} cirkler.",
                    drawn == 0 ? MatchBbrStatusKind.Warning : MatchBbrStatusKind.Ok);
                return drawn > 0;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                SetStatus("Forhåndsvisning mislykkedes. Se debug-output.", MatchBbrStatusKind.Error);
                return false;
            }
        }

        public void ClearPreview()
        {
            _preview.Clear();
        }

        // Turns the preview into real geometry. Unlike ShowPreview this changes the drawing, so it
        // goes through a document lock and a transaction and is undoable as one step.
        public bool ExportCircles(IEnumerable<ComparisonRow> rows)
        {
            if (!IsActive())
            {
                SetStatus("Tegningen er ikke aktiv.", MatchBbrStatusKind.Warning);
                return false;
            }

            if (Result.Rows.Count == 0)
            {
                SetStatus("Kør en sammenligning først.", MatchBbrStatusKind.Warning);
                return false;
            }

            Database database = Owner.Database;

            using DocumentLock documentLock = Owner.LockDocument();

            CircleExportResult result;

            using (Transaction tx = database.TransactionManager.StartTransaction())
            {
                try
                {
                    result = MatchBbrCircleExport.Draw(database, rows);
                    tx.Commit();
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    SetStatus(
                        "Tegning af cirkler mislykkedes. Se debug-output.",
                        MatchBbrStatusKind.Error);
                    return false;
                }
            }

            string replaced = result.Erased > 0
                ? $" Erstattede {result.Erased} fra en tidligere kørsel."
                : string.Empty;
            string skipped = result.Skipped > 0
                ? $" {result.Skipped} række(r) uden blok blev sprunget over."
                : string.Empty;

            SetStatus(
                $"Tegnede {result.Drawn} cirkel/cirkler i tegningen.{replaced}{skipped}",
                result.Drawn > 0 ? MatchBbrStatusKind.Ok : MatchBbrStatusKind.Warning);

            Owner.Editor.Regen();
            return result.Drawn > 0;
        }

        // Removes every circle this tool has drawn, without needing a comparison first.
        public bool EraseCircles()
        {
            if (!IsActive())
            {
                SetStatus("Tegningen er ikke aktiv.", MatchBbrStatusKind.Warning);
                return false;
            }

            Database database = Owner.Database;

            using DocumentLock documentLock = Owner.LockDocument();

            int erased;

            using (Transaction tx = database.TransactionManager.StartTransaction())
            {
                try
                {
                    erased = MatchBbrCircleExport.EraseExisting(database);
                    tx.Commit();
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    SetStatus(
                        "Sletning af cirkler mislykkedes. Se debug-output.",
                        MatchBbrStatusKind.Error);
                    return false;
                }
            }

            SetStatus(
                erased == 0
                    ? "Ingen cirkler fra dette værktøj at slette."
                    : $"Slettede {erased} cirkel/cirkler.",
                erased == 0 ? MatchBbrStatusKind.Info : MatchBbrStatusKind.Ok);

            Owner.Editor.Regen();
            return erased > 0;
        }

        public bool WriteAll()
        {
            return RunWrite(database => MatchBbrWriteBack.WriteAll(
                database,
                Result.Rows.Where(r => r.IsWritable),
                _rules));
        }

        public bool WriteOne(ComparisonRow row, CompareRule rule)
        {
            return RunWrite(database => MatchBbrWriteBack.WriteOne(database, row, rule));
        }

        private bool RunWrite(Func<Database, WriteBackResult> write)
        {
            if (!IsActive())
            {
                SetStatus("Tegningen er ikke aktiv.", MatchBbrStatusKind.Warning);
                return false;
            }

            Database database = Owner.Database;

            using DocumentLock documentLock = Owner.LockDocument();

            WriteBackResult result;

            // Only the write itself sits inside the transaction; reporting happens after it is
            // closed. SetStatus runs WPF code through the status sink, and a throw from there
            // used to land in a catch that called Abort on an already-committed transaction.
            using (Transaction tx = database.TransactionManager.StartTransaction())
            {
                try
                {
                    result = write(database);
                    tx.Commit();
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    SetStatus(
                        "Skrivning til tegningen mislykkedes. Se debug-output.",
                        MatchBbrStatusKind.Error);
                    return false;
                }
            }

            string problems = result.Problems.Count > 0
                ? $" {result.Problems.Count} problem(er): {result.Problems.First()}"
                : string.Empty;

            SetStatus(
                $"Skrev {result.Written} værdi(er), sprang {result.Skipped} over.{problems}",
                result.Problems.Count > 0 ? MatchBbrStatusKind.Warning : MatchBbrStatusKind.Ok);
            return result.Written > 0;
        }

        public void ZoomTo(ComparisonRow row)
        {
            if (!IsActive() || row.BbrRecord is null)
            {
                return;
            }

            Database database = Owner.Database;
            Editor editor = Owner.Editor;

            using DocumentLock documentLock = Owner.LockDocument();

            // The transaction is only used to read the block's extents. Zooming and selecting
            // happen after it closes, so neither can throw into a catch that would Abort a
            // committed transaction.
            Extents3d extents;

            using (Transaction tx = database.TransactionManager.StartTransaction())
            {
                try
                {
                    if (tx.GetObject(row.BbrRecord.ObjectId, OpenMode.ForRead) is not Entity entity)
                    {
                        tx.Abort();
                        return;
                    }

                    extents = entity.GeometricExtents;
                    tx.Commit();
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
            }

            try
            {
                double padding = Math.Max(
                    Math.Max(
                        extents.MaxPoint.X - extents.MinPoint.X,
                        extents.MaxPoint.Y - extents.MinPoint.Y) * 0.2,
                    20.0);

                editor.Zoom(new Extents3d(
                    new Point3d(extents.MinPoint.X - padding, extents.MinPoint.Y - padding, extents.MinPoint.Z),
                    new Point3d(extents.MaxPoint.X + padding, extents.MaxPoint.Y + padding, extents.MaxPoint.Z)));

                editor.SetImpliedSelection(new[] { row.BbrRecord.ObjectId });
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
            }
        }

        // ---- Export --------------------------------------------------------

        // File I/O deliberately happens outside any transaction. The old MATCHBBR exported inside
        // its transaction after committing, so a locked output file threw, the catch called Abort
        // on an already-committed transaction, and that threw out of the catch.
        public bool Export(string outputPath, IEnumerable<ComparisonRow> rows)
        {
            try
            {
                MatchBbrExport.Export(outputPath, Sheet, _rules, LabelFor, rows);
                SetStatus($"Eksporteret til {outputPath}.", MatchBbrStatusKind.Ok);
                return true;
            }
            catch (IOException ex)
            {
                prdDbg(ex);
                SetStatus(
                    "Filen kunne ikke skrives — er den åben i Excel?",
                    MatchBbrStatusKind.Error);
                return false;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                SetStatus("Eksport mislykkedes. Se debug-output.", MatchBbrStatusKind.Error);
                return false;
            }
        }

        public void SetStatus(string message, MatchBbrStatusKind kind)
        {
            StatusSink?.Invoke(message, kind);
        }

        public void Dispose()
        {
            _preview.Dispose();
            _unmatchedHints.Clear();
            _boundaryIds.Clear();
            _excelRows.Clear();
            _rules.Clear();
            BbrRecords = Array.Empty<BbrRecord>();
            Result = MatchResult.Empty;
            Boundary = BoundarySnapshot.Empty;
        }
    }
}
