using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Autodesk.AutoCAD.Runtime;

using IntersectUtilities.MPE.SplitStik;

using static IntersectUtilities.UtilsCommon.Utils;

using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        private const string SplitStikStikSuffix = " - Stik";
        private const string SplitStikHovedSuffix = " - Hovedledning";

        /// <command>SPLITSTIK</command>
        /// <summary>
        /// Imports a DimensioneringV2 result file (.d2r) and writes the calculated network out as
        /// two standalone drawings next to it: one holding only the stikledninger and one holding
        /// only the hovedledningerne. Both are named after the result's own Id, e.g.
        /// "Calc 012 - Stik.dwg" and "Calc 012 - Hovedledning.dwg", and are overwritten on re-run.
        /// Each stikledning becomes its own polyline. Hovedledninger are merged into continuous
        /// runs that are broken only where the dimension changes — a run therefore passes straight
        /// through a stik branch, and splits at every genuine network junction. Polylines get the
        /// usual FJV layer, constant width, linetype and Plinegen, plus NORSYN_NHS_PIPE XData
        /// carrying the pipe family, the FL/SL role and the DN, because the layer name alone cannot
        /// distinguish a fordelingsledning from a stikledning. Segments that DimensioneringV2 never
        /// sized are skipped and reported. The active drawing is not modified.
        /// </summary>
        /// <category>Dimensionering</category>
        [CommandMethod("SPLITSTIK")]
        public void splitstik()
        {
            OpenFileDialog dialog = new()
            {
                Title = "Choose the DimensioneringV2 result file to import: ",
                DefaultExt = "d2r",
                Filter = "D2R Files (*.d2r)|*.d2r|All Files (*.*)|*.*",
                CheckFileExists = true,
                FilterIndex = 0,
            };

            if (dialog.ShowDialog() != true) return;
            string d2rPath = dialog.FileName;

            try
            {
                D2rNetwork network = D2rReader.Read(d2rPath);

                prdDbg(
                    $"Read \"{network.Id ?? Path.GetFileNameWithoutExtension(d2rPath)}\" "
                        + $"(FormatVersion {network.FormatVersion}, calculated "
                        + $"{network.CalculatedAt ?? "at an unknown time"}).");

                // A newer file is read anyway — the reader ignores properties it does not know
                // and the anomaly counters below catch a key that moved. Say so, so an odd
                // result is traceable to the version rather than to the drawing.
                if (network.FormatVersion > D2rReader.VerifiedFormatVersion)
                    prdDbg(
                        $"NOTE: FormatVersion {network.FormatVersion} is newer than the "
                            + $"{D2rReader.VerifiedFormatVersion} this command was verified "
                            + "against. Reading it anyway — check the counts below.");

                SplitStikStats stats = new();
                List<PipeRun> runs = new();
                int vertexCount = 0;

                foreach (D2rGraph graph in network.Graphs)
                {
                    vertexCount += graph.Vertices.Count;
                    runs.AddRange(SplitStikRunBuilder.Build(graph, stats));
                }

                prdDbg(
                    $"{network.Graphs.Count} graph(s), {vertexCount} vertices, "
                        + $"{stats.TotalEdges} edges.");

                if (runs.Count == 0)
                {
                    prdDbg("Nothing to draw — no sized segments found. Aborting.");
                    return;
                }

                List<PipeRun> stik = runs.Where(x => x.IsServiceLine).ToList();
                List<PipeRun> hoved = runs.Where(x => !x.IsServiceLine).ToList();

                string folder =
                    Path.GetDirectoryName(d2rPath)
                    ?? throw new InvalidOperationException(
                        $"Cannot determine the folder of \"{d2rPath}\".");

                string baseName = SanitizeFileName(
                    string.IsNullOrWhiteSpace(network.Id)
                        ? Path.GetFileNameWithoutExtension(d2rPath)
                        : network.Id!);

                string stikPath = Path.Combine(folder, baseName + SplitStikStikSuffix + ".dwg");
                string hovedPath = Path.Combine(folder, baseName + SplitStikHovedSuffix + ".dwg");

                // The two drawings are written one after the other, so a failure on the second
                // leaves the first updated and the second stale from an earlier run — with nothing
                // on disk to say so. Track what actually landed and spell it out on the way out.
                List<string> saved = new();
                try
                {
                    int stikWritten = SplitStikWriter.Write(stikPath, stik, stats);
                    saved.Add(stikPath);
                    prdDbg($"{stikWritten} stikledninger -> {stikPath}");

                    int hovedWritten = SplitStikWriter.Write(hovedPath, hoved, stats);
                    saved.Add(hovedPath);
                    prdDbg($"{hovedWritten} hovedledninger -> {hovedPath}");
                }
                catch (System.Exception)
                {
                    ReportPartialWrite(saved, new[] { stikPath, hovedPath });
                    throw;
                }

                ReportStats(stats, hoved);
                prdDbg("Finished!");
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
            }
        }

        /// <summary>
        /// Names every output file and whether THIS run wrote it. Without this the user cannot tell
        /// a freshly written drawing from one left behind by an earlier run, because both files are
        /// always overwritten in place and carry no marker.
        /// </summary>
        private static void ReportPartialWrite(
            IReadOnlyList<string> saved,
            IReadOnlyList<string> all)
        {
            prdDbg("--- SPLITSTIK stopped part-way ---");

            foreach (string path in all)
                prdDbg(
                    saved.Contains(path)
                        ? $"WRITTEN by this run: {path}"
                        : $"NOT written — any file at this path is left over from an earlier "
                            + $"run: {path}");

            if (saved.Count > 0)
                prdDbg(
                    "The two drawings therefore do NOT belong together. Fix the error below and "
                        + "run SPLITSTIK again before using either of them.");
        }

        private static void ReportStats(SplitStikStats stats, IReadOnlyList<PipeRun> hoved)
        {
            if (hoved.Count > 0)
            {
                int merged = stats.MainEdgesUsed - hoved.Count;
                double totalLength = hoved.Sum(x => x.SourceLength);
                prdDbg(
                    $"Hovedledning: {stats.MainEdgesUsed} edges merged into {hoved.Count} runs "
                        + $"({merged} joins), total length "
                        + (double.IsFinite(totalLength)
                            ? $"{totalLength:F3} m."
                            : "UNKNOWN — the .d2r carries non-finite segment lengths."));
            }

            if (stats.SeriesFallbacks.Count > 0)
                prdDbg(
                    "Pipe series fallback — the schedule has no S3 row for these, so the next "
                        + "series down supplied the width: "
                        + $"{string.Join(", ", stats.SeriesFallbacks)}.");

            if (stats.EdgesPerSubGraph.Count > 1)
                prdDbg(
                    "Subgraphs (all imported): "
                        + string.Join(
                            ", ",
                            stats.EdgesPerSubGraph.Select(x => $"{x.Key}={x.Value} edges")));

            if (!stats.HasAnomalies) return;

            prdDbg("--- Skipped / anomalies ---");

            if (stats.SkippedNoDim > 0)
                prdDbg(
                    $"{stats.SkippedNoDim} segment(s) had no dimension and were skipped "
                        + "(they supply nothing).");

            if (stats.SkippedDegenerateGeometry > 0)
                prdDbg(
                    $"{stats.SkippedDegenerateGeometry} segment(s) had fewer than 2 vertices "
                        + "and were skipped.");

            if (stats.SkippedNonFiniteGeometry > 0)
                prdDbg(
                    $"{stats.SkippedNonFiniteGeometry} segment(s) had NaN or Infinity coordinates "
                        + "in the .d2r and were skipped — they would have produced a corrupt "
                        + "polyline. This means DimensioneringV2 wrote an unsized/failed geometry.");

            if (stats.RunsWithoutWidth > 0)
                prdDbg(
                    $"{stats.RunsWithoutWidth} run(s) were drawn with ZERO width because "
                        + "PipeScheduleV2 has no row in ANY series for: "
                        + $"{string.Join(", ", stats.MissingWidths)}. "
                        + "Geometry, layer and XData are correct; add the size to the pipe "
                        + "schedule to get the right width.");

            if (stats.SkippedUnknownType > 0)
                prdDbg(
                    $"{stats.SkippedUnknownType} segment(s) had an unknown type tag "
                        + $"({string.Join(", ", stats.UnknownTypeTags)}) and were skipped.");

            if (stats.SkippedUnknownFamily > 0)
                prdDbg(
                    $"{stats.SkippedUnknownFamily} run(s) had an unknown pipe family "
                        + $"({string.Join(", ", stats.UnknownFamilies)}) and were skipped. "
                        + "Add a case to SplitStikWriter.TranslateFamilyNameToSystem.");

            if (stats.SeamMismatches > 0)
                prdDbg(
                    $"{stats.SeamMismatches} chain(s) were split early because the geometry did "
                        + "not meet at the shared node.");
        }

        private static string SanitizeFileName(string name) =>
            string.Join("_", name.Split(Path.GetInvalidFileNameChars())).Trim();
    }
}
