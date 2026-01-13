using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.PlottingServices;
using Autodesk.AutoCAD.Runtime;

using IntersectUtilities.DataScience.PropertySetBrowser;
using IntersectUtilities.UtilsCommon;

using Microsoft.Win32;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using static IntersectUtilities.UtilsCommon.Utils;

using PsDataType = Autodesk.Aec.PropertyData.DataType;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        private static PropertySetBrowserWindow? _propertySetBrowserWindow;

        /// <command>DSPSBROWSER</command>
        /// <summary>
        /// Opens a modeless window to browse PropertySet data in the drawing.
        /// Allows searching and filtering, and selecting entities in AutoCAD.
        /// </summary>
        /// <category>Data Science</category>
        [CommandMethod("DSPSBROWSER")]
        public void dspsbrowser()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            // If window already exists and is open, bring to front
            if (_propertySetBrowserWindow != null && _propertySetBrowserWindow.IsLoaded)
            {
                _propertySetBrowserWindow.Activate();
                return;
            }

            // Create new window
            _propertySetBrowserWindow = new PropertySetBrowserWindow(localDb);
            _propertySetBrowserWindow.Closed += (s, e) => _propertySetBrowserWindow = null;
            _propertySetBrowserWindow.Show();
        }

        [CommandMethod("DSIMPORTPTSWITHPS")]
        public void dsimportptwithps()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            PropertySetManager.UpdatePropertySetDefinition(localDb, PSetDefs.DefinedSets.BBR);

            #region File dialog for CSV selection
            string csvFilePath = PromptForCsvFile("Choose CSV file:");
            if (string.IsNullOrWhiteSpace(csvFilePath))
            {
                prdDbg("\nNo CSV file selected.");
                return;
            }
            #endregion

            CsvTypedDataTable csvData;
            try
            {
                csvData = new CsvTypedDataTable(csvFilePath);
            }
            catch (System.Exception ex)
            {
                prdDbg($"Error reading CSV file: {ex.Message}");
                return;
            }

            var columnNames = csvData.ColumnNames?
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList() ?? new List<string>();
            if (columnNames.Count == 0)
            {
                prdDbg("No columns found in CSV file.");
                return;
            }

            #region Select X and Y coordinate column names
            string? xColumnName = StringGridFormCaller.Call(
                columnNames.OrderBy(x => x),
                "Select X coordinate column:");
            if (string.IsNullOrWhiteSpace(xColumnName))
            {
                prdDbg("X coordinate column selection cancelled.");
                return;
            }
            string selectedXColumn = xColumnName!;

            string? yColumnName = StringGridFormCaller.Call(
                columnNames.OrderBy(x => x),
                "Select Y coordinate column:");
            if (string.IsNullOrWhiteSpace(yColumnName))
            {
                prdDbg("Y coordinate column selection cancelled.");
                return;
            }
            string selectedYColumn = yColumnName!;
            #endregion

            using Transaction tx = localDb.TransactionManager.StartTransaction();

            try
            {
                #region Create GenericPropertySetImporter
                GenericPropertySetImporter importer;
                try
                {
                    importer = new GenericPropertySetImporter(
                        localDb,
                        csvData,
                        selectedXColumn,
                        selectedYColumn);
                }
                catch (System.Exception ex)
                {
                    prdDbg($"Error creating importer: {ex.Message}");
                    tx.Abort();
                    return;
                }
                #endregion

                var bbrs = localDb.HashSetOfTypeWithPs<BlockReference>(tx,
                    PSetDefs.DefinedSets.BBR).Select(x => new BBR(x)).ToHashSet();

                #region Process rows and create DBPoints
                int pointCount = 0;
                var pm = new ProgressMeter();
                pm.Start("Importing points with PropertySets...");

                // Get model space block table record
                BlockTableRecord space = (BlockTableRecord)tx.GetObject(
                    localDb.CurrentSpaceId, OpenMode.ForWrite);

                string propertySetName = csvData.TableName;

                while (importer.HasMoreRows)
                {
                    (double? x, double? y, Action<Entity> attachAction) = importer.GetNextRow();

                    DBPoint dbPoint = new DBPoint();
                    dbPoint.SetDatabaseDefaults(localDb);
                    // Add to model space
                    space.AppendEntity(dbPoint);
                    tx.AddNewlyCreatedDBObject(dbPoint, true);

                    attachAction(dbPoint);

                    if (!x.HasValue || !y.HasValue)
                    {
                        // Try to find values in BBR
                        var vejnavn = PropertySetManager.ReadNonDefinedPropertySetString(
                            dbPoint, propertySetName, "Adresse");
                        var husnr = PropertySetManager.ReadNonDefinedPropertySetString(
                            dbPoint, propertySetName, "Husnr");
                        var litraer = PropertySetManager.ReadNonDefinedPropertySetString(
                            dbPoint, propertySetName, "Litraer");

                        var searchstring = $"{vejnavn} {husnr}{litraer}";

                        var query = bbrs.Where(x => x.Adresse == searchstring).FirstOrDefault();
                        if (query == default)
                        {
                            pm.Stop();
                            throw new System.Exception(
                                $"{searchstring} cannot find matchin bbr!");
                        }

                        dbPoint.Position = new Point3d(query.X, query.Y, 0);
                    }
                    else
                    {
                        // Convert lat/lon (x, y) to UTM32N easting/northing
                        double[] utmCoords = ToUtm32NFromWGS84(y.Value, x.Value);
                        dbPoint.Position = new Point3d(utmCoords[0], utmCoords[1], 0);
                    }

                    pointCount++;
                    pm.MeterProgress();
                }

                pm.Stop();
                prdDbg($"Successfully imported {pointCount} points with PropertySets.");
                #endregion
            }
            catch (System.Exception ex)
            {
                tx.Abort();
                prdDbg(ex);
                return;
            }

            tx.Commit();
        }

        [CommandMethod("DSMERGEDATA")]
        public void dsmergedata()
        {
            string leftCsvPath = PromptForCsvFile("Select LEFT dataset (consumers)");
            if (string.IsNullOrWhiteSpace(leftCsvPath))
            {
                prdDbg("Left dataset selection cancelled.");
                return;
            }

            string rightCsvPath = PromptForCsvFile("Select RIGHT dataset (geocoding lookup)");
            if (string.IsNullOrWhiteSpace(rightCsvPath))
            {
                prdDbg("Right dataset selection cancelled.");
                return;
            }

            CsvTypedDataTable leftTable;
            CsvTypedDataTable rightTable;
            try
            {
                leftTable = new CsvTypedDataTable(leftCsvPath);
                rightTable = new CsvTypedDataTable(rightCsvPath);
            }
            catch (System.Exception ex)
            {
                prdDbg($"Error loading CSV files: {ex.Message}");
                return;
            }

            if (!EnsureColumns(leftTable, ["Ejendomsnr", "Adresse", "Husnr", "Litraer"], "Left"))
                return;
            if (!EnsureColumns(rightTable, ["Forbrugernr", "Vejnavn", "Husnr", "Breddegrad", "Længdegrad"], "Right"))
                return;

            prdDbg($"Left dataset '{Path.GetFileName(leftCsvPath)}' rows: {leftTable.RowCount}");
            prdDbg($"Right dataset '{Path.GetFileName(rightCsvPath)}' rows: {rightTable.RowCount}");

            var rightByConsumerId = BuildRightIndex(rightTable);
            var leftGroups = BuildGroupMap(leftTable.Rows, BuildLeftKey);
            var rightGroups = BuildGroupMap(rightTable.Rows, BuildRightKey);
            var duplicateQueues = rightGroups
                .Where(kvp => kvp.Value.Count > 1)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => new Queue<int>(kvp.Value));

            HashSet<int> usedRightRows = new HashSet<int>();
            List<MatchResult> matches = new List<MatchResult>();
            List<UnmatchedLeftRow> unmatched = new List<UnmatchedLeftRow>();
            Dictionary<string, int> matchStageCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int sequentialAssignments = 0;
            int duplicateCountMismatch = 0;

            for (int leftIndex = 0; leftIndex < leftTable.RowCount; leftIndex++)
            {
                var leftRow = leftTable.Rows[leftIndex];
                string ejendomsKey = NormalizeValue(ReadString(leftRow, "Ejendomsnr"));

                if (string.IsNullOrEmpty(ejendomsKey))
                {
                    TrackUnmatched(leftIndex, leftRow, "Missing Ejendomsnr");
                    continue;
                }

                if (!rightByConsumerId.TryGetValue(ejendomsKey, out var candidateIndices))
                {
                    TrackUnmatched(leftIndex, leftRow, "No Forbrugernr match");
                    continue;
                }

                var availableCandidates = candidateIndices
                    .Where(idx => !usedRightRows.Contains(idx))
                    .ToList();

                if (availableCandidates.Count == 0)
                {
                    TrackUnmatched(leftIndex, leftRow, "All matching Forbrugernr already used");
                    continue;
                }

                if (availableCandidates.Count == 1)
                {
                    Assign(leftIndex, leftRow, availableCandidates[0], "Ejendomsnr");
                    continue;
                }

                string adresseKey = NormalizeValue(ReadString(leftRow, "Adresse"));
                availableCandidates = availableCandidates
                    .Where(idx => NormalizeValue(ReadString(rightTable.Rows[idx], "Vejnavn")) == adresseKey)
                    .ToList();

                if (availableCandidates.Count == 1)
                {
                    Assign(leftIndex, leftRow, availableCandidates[0], "Ejendomsnr+Adresse");
                    continue;
                }

                if (availableCandidates.Count == 0)
                {
                    TrackUnmatched(leftIndex, leftRow, "Address mismatch");
                    continue;
                }

                string husnrKey = NormalizeValue(ReadString(leftRow, "Husnr"));
                availableCandidates = availableCandidates
                    .Where(idx => NormalizeValue(ReadString(rightTable.Rows[idx], "Husnr")) == husnrKey)
                    .ToList();

                if (availableCandidates.Count == 1)
                {
                    Assign(leftIndex, leftRow, availableCandidates[0], "Ejendomsnr+Adresse+Husnr");
                    continue;
                }

                if (availableCandidates.Count == 0)
                {
                    TrackUnmatched(leftIndex, leftRow, "House number mismatch");
                    continue;
                }

                MergeKey compositeKey = new MergeKey(ejendomsKey, adresseKey, husnrKey);
                bool leftHasDuplicates = leftGroups.TryGetValue(compositeKey, out var leftDuplicateGroup) && leftDuplicateGroup.Count > 1;
                bool rightHasDuplicates = rightGroups.TryGetValue(compositeKey, out var rightDuplicateGroup) && rightDuplicateGroup.Count > 1;

                if (leftHasDuplicates && rightHasDuplicates && leftDuplicateGroup != null && rightDuplicateGroup != null)
                {
                    if (rightDuplicateGroup.Count == leftDuplicateGroup.Count)
                    {
                        if (!duplicateQueues.TryGetValue(compositeKey, out var queue))
                        {
                            queue = new Queue<int>(availableCandidates);
                            duplicateQueues[compositeKey] = queue;
                        }

                        while (queue.Count > 0 && usedRightRows.Contains(queue.Peek()))
                            queue.Dequeue();

                        if (queue.Count == 0)
                        {
                            TrackUnmatched(leftIndex, leftRow, "Duplicate queue exhausted");
                            continue;
                        }

                        sequentialAssignments++;
                        Assign(leftIndex, leftRow, queue.Dequeue(), "DuplicateSequential");
                        continue;
                    }
                    else
                    {
                        duplicateCountMismatch++;
                        TrackUnmatched(leftIndex, leftRow, $"Duplicate count mismatch (Left={leftDuplicateGroup.Count}, Right={rightDuplicateGroup.Count})");
                        continue;
                    }
                }

                // Fall back to the first available candidate if duplicates only exist on the right side.
                Assign(leftIndex, leftRow, availableCandidates[0], "AmbiguousFallback");
            }

            int adjustedCoordinates = ApplyCoordinateOffsets(matches);

            prdDbg($"Matches created: {matches.Count} / {leftTable.RowCount}");
            prdDbg($"Unmatched rows: {unmatched.Count}");
            prdDbg($"Sequential duplicate assignments: {sequentialAssignments}");
            prdDbg($"Duplicate count mismatches: {duplicateCountMismatch}");
            prdDbg($"Adjusted overlapping coordinates: {adjustedCoordinates}");
            prdDbg();
            PrintTable(
                new[] { "Metric", "Value" },
                new List<IEnumerable<object>>
                {
                    new object[] { "Left rows", leftTable.RowCount },
                    new object[] { "Right rows", rightTable.RowCount },
                    new object[] { "Matches", matches.Count },
                    new object[] { "Unmatched", unmatched.Count },
                    new object[] { "Sequential duplicates", sequentialAssignments },
                    new object[] { "Duplicate mismatches", duplicateCountMismatch },
                    new object[] { "Coordinate adjustments", adjustedCoordinates }
                });

            if (matchStageCounts.Count > 0)
            {
                var stageRows = matchStageCounts
                    .OrderByDescending(kvp => kvp.Value)
                    .Select(kvp => new object[] { kvp.Key, kvp.Value });
                prdDbg();
                PrintTable(new[] { "Match stage", "Count" }, stageRows);
            }

            if (unmatched.Count > 0)
            {
                var reasonRows = unmatched
                    .GroupBy(x => x.Reason)
                    .OrderByDescending(g => g.Count())
                    .Select(g => new object[] { g.Key, g.Count() });
                prdDbg();
                PrintTable(new[] { "Unmatched reason", "Count" }, reasonRows);
            }

            #region Save merged data and generate report
            string? leftDirectory = Path.GetDirectoryName(leftCsvPath);
            if (string.IsNullOrWhiteSpace(leftDirectory))
            {
                prdDbg("Cannot determine output directory from left CSV path.");
                return;
            }

            string leftFileName = Path.GetFileNameWithoutExtension(leftCsvPath);
            if (string.IsNullOrWhiteSpace(leftFileName))
            {
                leftFileName = "merged_data";
            }
            string outputPath = Path.Combine(leftDirectory!, $"{leftFileName}_merged.csv");

            SaveMergedDataToCsv(matches, leftTable, rightTable, outputPath);
            prdDbg($"Merged data saved to: {outputPath}");

            // Identify orphaned right rows (right rows that were never matched)
            var orphanedRightIndices = Enumerable.Range(0, rightTable.RowCount)
                .Where(idx => !usedRightRows.Contains(idx))
                .ToList();

            // Generate HTML report
            string reportPath = Path.Combine(leftDirectory!, "DSMERGE_report.html");
            GenerateMergeReport(
                reportPath,
                leftTable,
                rightTable,
                matches,
                unmatched,
                orphanedRightIndices,
                matchStageCounts,
                adjustedCoordinates,
                leftGroups,
                rightGroups);
            prdDbg($"HTML report saved to: {reportPath}");
            #endregion

            #region Local helper functions
            Dictionary<string, List<int>> BuildRightIndex(CsvTypedDataTable table)
            {
                Dictionary<string, List<int>> dict = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < table.RowCount; i++)
                {
                    string key = NormalizeValue(ReadString(table.Rows[i], "Forbrugernr"));
                    if (string.IsNullOrEmpty(key))
                        continue;
                    if (!dict.TryGetValue(key, out var list))
                    {
                        list = new List<int>();
                        dict[key] = list;
                    }
                    list.Add(i);
                }
                return dict;
            }

            Dictionary<MergeKey, List<int>> BuildGroupMap(
                IReadOnlyList<Dictionary<string, object?>> rows,
                Func<Dictionary<string, object?>, MergeKey> keySelector)
            {
                Dictionary<MergeKey, List<int>> map = new Dictionary<MergeKey, List<int>>();
                for (int i = 0; i < rows.Count; i++)
                {
                    MergeKey key = keySelector(rows[i]);
                    if (!map.TryGetValue(key, out var list))
                    {
                        list = new List<int>();
                        map[key] = list;
                    }
                    list.Add(i);
                }
                return map;
            }

            MergeKey BuildLeftKey(Dictionary<string, object?> row) =>
                new MergeKey(
                    NormalizeValue(ReadString(row, "Ejendomsnr")),
                    NormalizeValue(ReadString(row, "Adresse")),
                    NormalizeValue(ReadString(row, "Husnr")));

            MergeKey BuildRightKey(Dictionary<string, object?> row) =>
                new MergeKey(
                    NormalizeValue(ReadString(row, "Forbrugernr")),
                    NormalizeValue(ReadString(row, "Vejnavn")),
                    NormalizeValue(ReadString(row, "Husnr")));

            void Assign(int leftIdx, Dictionary<string, object?> leftRow, int rightIndex, string stage)
            {
                usedRightRows.Add(rightIndex);
                var rightRow = rightTable.Rows[rightIndex];
                var match = new MatchResult(leftIdx, rightIndex, leftRow, rightRow, BuildLeftKey(leftRow), stage)
                {
                    Latitude = ReadNullableDouble(rightRow, "Breddegrad"),
                    Longitude = ReadNullableDouble(rightRow, "Længdegrad")
                };
                matches.Add(match);
                IncrementStage(stage);
            }

            void TrackUnmatched(int leftIdx, Dictionary<string, object?> leftRow, string reason)
            {
                unmatched.Add(new UnmatchedLeftRow(leftIdx, leftRow, reason));
            }

            void IncrementStage(string stage)
            {
                if (matchStageCounts.ContainsKey(stage))
                    matchStageCounts[stage]++;
                else
                    matchStageCounts[stage] = 1;
            }

            string NormalizeValue(string value) =>
                string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

            string ReadString(Dictionary<string, object?> row, string column)
            {
                if (row == null)
                    return string.Empty;
                if (row.TryGetValue(column, out var obj) && obj != null)
                {
                    return Convert.ToString(obj, CultureInfo.InvariantCulture) ?? string.Empty;
                }
                return string.Empty;
            }

            double? ReadNullableDouble(Dictionary<string, object?> row, string column)
            {
                if (row == null)
                    return null;
                if (!row.TryGetValue(column, out var obj) || obj == null)
                    return null;
                if (obj is double d)
                    return d;
                if (obj is float f)
                    return f;
                if (obj is int i)
                    return i;
                if (double.TryParse(obj.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                    return parsed;
                return null;
            }

            string FormatDouble(double? value) =>
                value.HasValue ? value.Value.ToString("F6", CultureInfo.InvariantCulture) : string.Empty;
            #endregion
        }

        #region Merge methods
        private static void SaveMergedDataToCsv(
            List<MatchResult> matches,
            CsvTypedDataTable leftTable,
            CsvTypedDataTable rightTable,
            string outputPath)
        {
            // Build column list: all left columns plus coordinate columns from right
            List<string> outputColumns = new List<string>();
            List<string> outputTypes = new List<string>();

            // Add all left table columns
            foreach (string col in leftTable.ColumnNames)
            {
                outputColumns.Add(col);
                outputTypes.Add(MapPropertySetTypeToCsvType(leftTable.ColumnDataTypes[col]));
            }

            // Add coordinate columns from right dataset
            outputColumns.Add("Breddegrad");
            outputTypes.Add("double");
            outputColumns.Add("Længdegrad");
            outputTypes.Add("double");

            // Write CSV file
            using (StreamWriter writer = new StreamWriter(outputPath, false, Encoding.UTF8))
            {
                // Write header row (column names)
                writer.WriteLine(string.Join(";", outputColumns));

                // Write data type row
                writer.WriteLine(string.Join(";", outputTypes));

                // Write data rows
                foreach (var match in matches)
                {
                    List<string> rowValues = new List<string>();

                    // Add left table values
                    foreach (string col in leftTable.ColumnNames)
                    {
                        rowValues.Add(FormatCsvValue(match.LeftRow, col));
                    }

                    // Add coordinate values from right dataset (use adjusted if available, otherwise original)
                    rowValues.Add(FormatDoubleForCsv(match.AdjustedLatitude ?? match.Latitude));
                    rowValues.Add(FormatDoubleForCsv(match.AdjustedLongitude ?? match.Longitude));

                    writer.WriteLine(string.Join(";", rowValues));
                }
            }
        }

        private static string MapPropertySetTypeToCsvType(PsDataType dataType)
        {
            return dataType switch
            {
                PsDataType.Text => "string",
                PsDataType.Real => "double",
                PsDataType.Integer => "int",
                PsDataType.TrueFalse => "bool",
                _ => "string"
            };
        }
        private static string FormatCsvValue(Dictionary<string, object?> row, string column)
        {
            if (row == null || !row.TryGetValue(column, out var obj) || obj == null)
                return string.Empty;

            if (obj is double d)
                return d.ToString("F6", CultureInfo.InvariantCulture);
            if (obj is float f)
                return f.ToString("F6", CultureInfo.InvariantCulture);
            if (obj is int i)
                return i.ToString(CultureInfo.InvariantCulture);
            if (obj is bool b)
                return b.ToString(CultureInfo.InvariantCulture);

            return Convert.ToString(obj, CultureInfo.InvariantCulture) ?? string.Empty;
        }
        private static string FormatDoubleForCsv(double? value) =>
            value.HasValue ? value.Value.ToString("F6", CultureInfo.InvariantCulture) : string.Empty;
        private static string PromptForCsvFile(string title)
        {
            OpenFileDialog dialog = new OpenFileDialog()
            {
                Title = title,
                DefaultExt = "csv",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FilterIndex = 0
            };

            if (dialog.ShowDialog() == true)
            {
                return dialog.FileName;
            }

            return string.Empty;
        }
        private static bool EnsureColumns(CsvTypedDataTable table, IEnumerable<string> requiredColumns, string label)
        {
            var missing = requiredColumns
                .Where(column => !table.HasColumn(column))
                .ToList();

            if (missing.Count > 0)
            {
                prdDbg($"{label} dataset is missing columns: {string.Join(", ", missing)}");
                return false;
            }

            return true;
        }
        private static int ApplyCoordinateOffsets(List<MatchResult> matches)
        {
            const double metersPerDegreeLat = 1.0 / 111320.0;
            int adjusted = 0;

            foreach (var group in matches
                .GroupBy(match => match.Key)
                .Where(g => g.Count() > 1))
            {
                var valid = group.Where(m => m.Latitude.HasValue && m.Longitude.HasValue).ToList();
                if (valid.Count != group.Count())
                    continue;

                bool sameLatitude = valid.All(m => AreAlmostEqual(m.Latitude!.Value, valid[0].Latitude!.Value));
                bool sameLongitude = valid.All(m => AreAlmostEqual(m.Longitude!.Value, valid[0].Longitude!.Value));

                if (!sameLatitude || !sameLongitude)
                    continue;

                double latRad = DegreesToRadians(valid[0].Latitude!.Value);
                double latDelta = 5.0 * metersPerDegreeLat;
                double cosLat = Math.Cos(latRad);
                double lonDelta = cosLat < 1e-6 ? 0 : (5.0 / (111320.0 * cosLat));

                for (int i = 0; i < valid.Count; i++)
                {
                    valid[i].AdjustedLatitude = valid[i].Latitude!.Value + i * latDelta;
                    valid[i].AdjustedLongitude = valid[i].Longitude!.Value + i * lonDelta;
                }

                adjusted += valid.Count;
            }

            return adjusted;
        }
        private static bool AreAlmostEqual(double a, double b, double tolerance = 1e-8) =>
            Math.Abs(a - b) <= tolerance;
        private static double DegreesToRadians(double value) => value * Math.PI / 180.0;

        private static void GenerateMergeReport(
            string reportPath,
            CsvTypedDataTable leftTable,
            CsvTypedDataTable rightTable,
            List<MatchResult> matches,
            List<UnmatchedLeftRow> unmatched,
            List<int> orphanedRightIndices,
            Dictionary<string, int> matchStageCounts,
            int adjustedCoordinates,
            Dictionary<MergeKey, List<int>> leftGroups,
            Dictionary<MergeKey, List<int>> rightGroups)
        {
            var sb = new StringBuilder();

            // Identify non-1:1 matches (anything not matched by simple Ejendomsnr alone)
            var nonSimpleMatches = matches
                .Where(m => m.Stage != "Ejendomsnr")
                .ToList();

            // Group matches by key to show full picture of duplicates
            var matchesByKey = matches
                .GroupBy(m => m.Key)
                .Where(g => g.Count() > 1 || g.Any(m => m.Stage != "Ejendomsnr"))
                .ToDictionary(g => g.Key, g => g.ToList());

            // HTML Header
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"UTF-8\">");
            sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine("  <title>DSMERGE Report</title>");
            sb.AppendLine("  <style>");
            sb.AppendLine(@"
    :root {
      --bg-primary: #1a1a2e;
      --bg-secondary: #16213e;
      --bg-tertiary: #0f3460;
      --text-primary: #eaeaea;
      --text-secondary: #a0a0a0;
      --accent: #e94560;
      --accent-secondary: #0f9b8e;
      --success: #4caf50;
      --warning: #ff9800;
      --error: #f44336;
      --border: #2a2a4a;
    }
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      font-family: 'Segoe UI', system-ui, sans-serif;
      background: var(--bg-primary);
      color: var(--text-primary);
      line-height: 1.6;
      padding: 20px;
    }
    h1 {
      color: var(--accent);
      border-bottom: 2px solid var(--accent);
      padding-bottom: 10px;
      margin-bottom: 20px;
    }
    h2 {
      color: var(--accent-secondary);
      margin: 20px 0 10px 0;
      font-size: 1.3em;
    }
    .summary-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 15px;
      margin-bottom: 30px;
    }
    .summary-card {
      background: var(--bg-secondary);
      border: 1px solid var(--border);
      border-radius: 8px;
      padding: 15px;
      text-align: center;
    }
    .summary-card .value {
      font-size: 2em;
      font-weight: bold;
      color: var(--accent);
    }
    .summary-card .label {
      color: var(--text-secondary);
      font-size: 0.9em;
    }
    .summary-card.success .value { color: var(--success); }
    .summary-card.warning .value { color: var(--warning); }
    .summary-card.error .value { color: var(--error); }
    .section {
      background: var(--bg-secondary);
      border: 1px solid var(--border);
      border-radius: 8px;
      margin-bottom: 20px;
      overflow: hidden;
    }
    .section-header {
      background: var(--bg-tertiary);
      padding: 12px 15px;
      cursor: pointer;
      display: flex;
      justify-content: space-between;
      align-items: center;
      user-select: none;
    }
    .section-header:hover { background: #1a4a7a; }
    .section-header h3 {
      margin: 0;
      font-size: 1.1em;
      color: var(--text-primary);
    }
    .section-header .badge {
      background: var(--accent);
      color: white;
      padding: 2px 10px;
      border-radius: 12px;
      font-size: 0.85em;
    }
    .section-header .badge.warning { background: var(--warning); }
    .section-header .badge.error { background: var(--error); }
    .section-header .badge.success { background: var(--success); }
    .section-content {
      padding: 15px;
      display: none;
    }
    .section.open .section-content { display: block; }
    .section-header::after {
      content: '▶';
      transition: transform 0.2s;
    }
    .section.open .section-header::after {
      transform: rotate(90deg);
    }
    .data-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.85em;
      margin-top: 10px;
    }
    .data-table th, .data-table td {
      padding: 8px 10px;
      border: 1px solid var(--border);
      text-align: left;
      max-width: 200px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .data-table th {
      background: var(--bg-tertiary);
      color: var(--accent-secondary);
      position: sticky;
      top: 0;
    }
    .data-table tr:nth-child(even) { background: rgba(255,255,255,0.02); }
    .data-table tr:hover { background: rgba(255,255,255,0.05); }
    .data-table td:hover {
      white-space: normal;
      word-break: break-all;
    }
    .group-container {
      margin-bottom: 20px;
      border: 1px solid var(--border);
      border-radius: 6px;
      overflow: hidden;
    }
    .group-header {
      background: var(--bg-tertiary);
      padding: 10px 15px;
      font-weight: bold;
      border-bottom: 1px solid var(--border);
    }
    .group-content { padding: 10px; }
    .sub-table-label {
      color: var(--accent-secondary);
      font-size: 0.9em;
      margin: 10px 0 5px 0;
      font-weight: bold;
    }
    .stage-badge {
      display: inline-block;
      padding: 2px 8px;
      border-radius: 4px;
      font-size: 0.8em;
      font-weight: bold;
    }
    .stage-Ejendomsnr { background: var(--success); color: white; }
    .stage-DuplicateSequential { background: var(--warning); color: black; }
    .stage-AmbiguousFallback { background: var(--error); color: white; }
    .table-scroll {
      max-height: 400px;
      overflow-y: auto;
    }
    .timestamp {
      color: var(--text-secondary);
      font-size: 0.85em;
      margin-bottom: 20px;
    }
    .no-data {
      color: var(--text-secondary);
      font-style: italic;
      padding: 20px;
      text-align: center;
    }
");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            // Title
            sb.AppendLine($"<h1>DSMERGE Report</h1>");
            sb.AppendLine($"<p class=\"timestamp\">Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");

            // Summary Cards
            sb.AppendLine("<div class=\"summary-grid\">");
            AppendSummaryCard(sb, "Left Rows", leftTable.RowCount.ToString(), "");
            AppendSummaryCard(sb, "Right Rows", rightTable.RowCount.ToString(), "");
            AppendSummaryCard(sb, "Matched", matches.Count.ToString(), matches.Count == leftTable.RowCount ? "success" : "warning");
            AppendSummaryCard(sb, "Unmatched Left", unmatched.Count.ToString(), unmatched.Count == 0 ? "success" : "error");
            AppendSummaryCard(sb, "Orphaned Right", orphanedRightIndices.Count.ToString(), orphanedRightIndices.Count == 0 ? "success" : "warning");
            AppendSummaryCard(sb, "Non-Simple Matches", nonSimpleMatches.Count.ToString(), nonSimpleMatches.Count == 0 ? "success" : "warning");
            AppendSummaryCard(sb, "Coord. Adjustments", adjustedCoordinates.ToString(), adjustedCoordinates == 0 ? "success" : "warning");
            sb.AppendLine("</div>");

            // Match Stage Breakdown
            sb.AppendLine("<h2>Match Stage Breakdown</h2>");
            sb.AppendLine("<table class=\"data-table\" style=\"max-width: 400px;\">");
            sb.AppendLine("<tr><th>Stage</th><th>Count</th></tr>");
            foreach (var kvp in matchStageCounts.OrderByDescending(x => x.Value))
            {
                string stageClass = $"stage-{kvp.Key.Replace("+", "")}";
                sb.AppendLine($"<tr><td><span class=\"stage-badge {stageClass}\">{HtmlEncode(kvp.Key)}</span></td><td>{kvp.Value}</td></tr>");
            }
            sb.AppendLine("</table>");

            // Section: Duplicate/Complex Matches (Full Picture)
            var complexKeys = matchesByKey.Keys.ToList();
            AppendCollapsibleSection(sb, "Duplicate & Complex Matches (Full Picture)", complexKeys.Count,
                complexKeys.Count > 0 ? "warning" : "success",
                () =>
                {
                    if (complexKeys.Count == 0)
                    {
                        sb.AppendLine("<p class=\"no-data\">All matches were simple 1:1 by Ejendomsnr. No complex matches to report.</p>");
                        return;
                    }

                    sb.AppendLine("<p>These are matches where either duplicates existed or additional criteria were needed.</p>");

                    foreach (var key in complexKeys)
                    {
                        var groupMatches = matchesByKey[key];
                        sb.AppendLine("<div class=\"group-container\">");
                        sb.AppendLine($"<div class=\"group-header\">Key: {HtmlEncode(key.Id)} | {HtmlEncode(key.Address)} | {HtmlEncode(key.HouseNumber)} ({groupMatches.Count} matches)</div>");
                        sb.AppendLine("<div class=\"group-content\">");

                        // Show all left rows in this group
                        var leftIndicesInGroup = leftGroups.TryGetValue(key, out var lg) ? lg : new List<int>();
                        var rightIndicesInGroup = rightGroups.TryGetValue(key, out var rg) ? rg : new List<int>();

                        sb.AppendLine($"<p><strong>Left rows with this key:</strong> {leftIndicesInGroup.Count} | <strong>Right rows with this key:</strong> {rightIndicesInGroup.Count}</p>");

                        // Show the actual pairings
                        sb.AppendLine("<div class=\"sub-table-label\">Pairings Made:</div>");
                        sb.AppendLine("<div class=\"table-scroll\">");
                        sb.AppendLine("<table class=\"data-table\">");
                        sb.AppendLine("<tr><th>Stage</th><th>Left#</th><th>Right#</th>");
                        foreach (var col in leftTable.ColumnNames.Take(5))
                            sb.AppendLine($"<th>L:{HtmlEncode(col)}</th>");
                        foreach (var col in rightTable.ColumnNames.Take(5))
                            sb.AppendLine($"<th>R:{HtmlEncode(col)}</th>");
                        sb.AppendLine("</tr>");

                        foreach (var m in groupMatches)
                        {
                            string stageClass = $"stage-{m.Stage.Replace("+", "")}";
                            sb.Append($"<tr><td><span class=\"stage-badge {stageClass}\">{HtmlEncode(m.Stage)}</span></td>");
                            sb.Append($"<td>{m.LeftIndex + 1}</td><td>{m.RightIndex + 1}</td>");
                            foreach (var col in leftTable.ColumnNames.Take(5))
                                sb.Append($"<td title=\"{HtmlEncode(FormatValue(m.LeftRow, col))}\">{HtmlEncode(TruncateValue(FormatValue(m.LeftRow, col), 30))}</td>");
                            foreach (var col in rightTable.ColumnNames.Take(5))
                                sb.Append($"<td title=\"{HtmlEncode(FormatValue(m.RightRow, col))}\">{HtmlEncode(TruncateValue(FormatValue(m.RightRow, col), 30))}</td>");
                            sb.AppendLine("</tr>");
                        }
                        sb.AppendLine("</table>");
                        sb.AppendLine("</div>");

                        // Show full left rows data
                        if (leftIndicesInGroup.Count > 0)
                        {
                            sb.AppendLine("<div class=\"sub-table-label\">All Left Rows in Group:</div>");
                            sb.AppendLine("<div class=\"table-scroll\">");
                            AppendFullRowTable(sb, leftTable, leftIndicesInGroup.Select(i => leftTable.Rows[i]).ToList(), leftIndicesInGroup);
                            sb.AppendLine("</div>");
                        }

                        // Show full right rows data
                        if (rightIndicesInGroup.Count > 0)
                        {
                            sb.AppendLine("<div class=\"sub-table-label\">All Right Rows in Group:</div>");
                            sb.AppendLine("<div class=\"table-scroll\">");
                            AppendFullRowTable(sb, rightTable, rightIndicesInGroup.Select(i => rightTable.Rows[i]).ToList(), rightIndicesInGroup);
                            sb.AppendLine("</div>");
                        }

                        sb.AppendLine("</div></div>");
                    }
                });

            // Section: Unmatched Left Rows
            AppendCollapsibleSection(sb, "Unmatched Left Rows", unmatched.Count,
                unmatched.Count == 0 ? "success" : "error",
                () =>
                {
                    if (unmatched.Count == 0)
                    {
                        sb.AppendLine("<p class=\"no-data\">All left rows were successfully matched.</p>");
                        return;
                    }

                    // Group by reason
                    var byReason = unmatched.GroupBy(u => u.Reason).OrderByDescending(g => g.Count());

                    foreach (var reasonGroup in byReason)
                    {
                        sb.AppendLine("<div class=\"group-container\">");
                        sb.AppendLine($"<div class=\"group-header\">Reason: {HtmlEncode(reasonGroup.Key)} ({reasonGroup.Count()} rows)</div>");
                        sb.AppendLine("<div class=\"group-content\">");
                        sb.AppendLine("<div class=\"table-scroll\">");
                        AppendFullRowTable(sb, leftTable, reasonGroup.Select(u => u.Row).ToList(), reasonGroup.Select(u => u.LeftIndex).ToList());
                        sb.AppendLine("</div></div></div>");
                    }
                });

            // Section: Orphaned Right Rows
            AppendCollapsibleSection(sb, "Orphaned Right Rows (Never Matched)", orphanedRightIndices.Count,
                orphanedRightIndices.Count == 0 ? "success" : "warning",
                () =>
                {
                    if (orphanedRightIndices.Count == 0)
                    {
                        sb.AppendLine("<p class=\"no-data\">All right rows were used in matches.</p>");
                        return;
                    }

                    sb.AppendLine("<p>These rows from the right (geocoding) dataset were never matched to any left row.</p>");
                    sb.AppendLine("<div class=\"table-scroll\">");
                    AppendFullRowTable(sb, rightTable, orphanedRightIndices.Select(i => rightTable.Rows[i]).ToList(), orphanedRightIndices);
                    sb.AppendLine("</div>");
                });

            // Section: Coordinate Adjustments
            var adjustedMatches = matches.Where(m => m.AdjustedLatitude.HasValue).ToList();
            AppendCollapsibleSection(sb, "Coordinate Adjustments", adjustedMatches.Count,
                adjustedMatches.Count == 0 ? "success" : "warning",
                () =>
                {
                    if (adjustedMatches.Count == 0)
                    {
                        sb.AppendLine("<p class=\"no-data\">No coordinate adjustments were necessary.</p>");
                        return;
                    }

                    sb.AppendLine("<p>These matches had overlapping coordinates and were offset by 5m increments.</p>");
                    sb.AppendLine("<div class=\"table-scroll\">");
                    sb.AppendLine("<table class=\"data-table\">");
                    sb.AppendLine("<tr><th>Left#</th><th>Key</th><th>Original Lat</th><th>Original Lng</th><th>Adjusted Lat</th><th>Adjusted Lng</th></tr>");
                    foreach (var m in adjustedMatches)
                    {
                        sb.AppendLine($"<tr><td>{m.LeftIndex + 1}</td><td>{HtmlEncode(m.Key.Id)}</td>");
                        sb.AppendLine($"<td>{m.Latitude:F6}</td><td>{m.Longitude:F6}</td>");
                        sb.AppendLine($"<td>{m.AdjustedLatitude:F6}</td><td>{m.AdjustedLongitude:F6}</td></tr>");
                    }
                    sb.AppendLine("</table>");
                    sb.AppendLine("</div>");
                });

            // JavaScript for collapsible sections
            sb.AppendLine("<script>");
            sb.AppendLine(@"
document.querySelectorAll('.section-header').forEach(header => {
    header.addEventListener('click', () => {
        header.parentElement.classList.toggle('open');
    });
});
// Auto-open sections with issues
document.querySelectorAll('.section').forEach(section => {
    const badge = section.querySelector('.badge');
    if (badge && (badge.classList.contains('warning') || badge.classList.contains('error'))) {
        const count = parseInt(badge.textContent);
        if (count > 0) section.classList.add('open');
    }
});
");
            sb.AppendLine("</script>");

            sb.AppendLine("</body></html>");

            File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8);
        }

        private static void AppendSummaryCard(StringBuilder sb, string label, string value, string cssClass)
        {
            string classAttr = string.IsNullOrEmpty(cssClass) ? "" : $" {cssClass}";
            sb.AppendLine($"<div class=\"summary-card{classAttr}\">");
            sb.AppendLine($"  <div class=\"value\">{value}</div>");
            sb.AppendLine($"  <div class=\"label\">{HtmlEncode(label)}</div>");
            sb.AppendLine("</div>");
        }

        private static void AppendCollapsibleSection(StringBuilder sb, string title, int count, string badgeClass, Action contentBuilder)
        {
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine($"<div class=\"section-header\"><h3>{HtmlEncode(title)}</h3><span class=\"badge {badgeClass}\">{count}</span></div>");
            sb.AppendLine("<div class=\"section-content\">");
            contentBuilder();
            sb.AppendLine("</div></div>");
        }

        private static void AppendFullRowTable(StringBuilder sb, CsvTypedDataTable table, List<Dictionary<string, object?>> rows, List<int> indices)
        {
            sb.AppendLine("<table class=\"data-table\">");
            sb.Append("<tr><th>#</th>");
            foreach (var col in table.ColumnNames)
                sb.Append($"<th>{HtmlEncode(col)}</th>");
            sb.AppendLine("</tr>");

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                int rowNum = indices[i] + 1; // 1-based for display
                sb.Append($"<tr><td>{rowNum}</td>");
                foreach (var col in table.ColumnNames)
                {
                    string val = FormatValue(row, col);
                    sb.Append($"<td title=\"{HtmlEncode(val)}\">{HtmlEncode(TruncateValue(val, 25))}</td>");
                }
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</table>");
        }

        private static string FormatValue(Dictionary<string, object?> row, string column)
        {
            if (row == null || !row.TryGetValue(column, out var obj) || obj == null)
                return "";
            if (obj is double d)
                return d.ToString("G", CultureInfo.InvariantCulture);
            return Convert.ToString(obj, CultureInfo.InvariantCulture) ?? "";
        }

        private static string TruncateValue(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value;
            return value.Substring(0, maxLength - 3) + "...";
        }

        private static string HtmlEncode(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return System.Net.WebUtility.HtmlEncode(value);
        }

        private class MatchResult
        {
            public int LeftIndex { get; }
            public int RightIndex { get; }
            public Dictionary<string, object?> LeftRow { get; }
            public Dictionary<string, object?> RightRow { get; }
            public MergeKey Key { get; }
            public string Stage { get; }
            public double? Latitude { get; set; }
            public double? Longitude { get; set; }
            public double? AdjustedLatitude { get; set; }
            public double? AdjustedLongitude { get; set; }

            public MatchResult(
                int leftIndex,
                int rightIndex,
                Dictionary<string, object?> leftRow,
                Dictionary<string, object?> rightRow,
                MergeKey key,
                string stage)
            {
                LeftIndex = leftIndex;
                RightIndex = rightIndex;
                LeftRow = leftRow;
                RightRow = rightRow;
                Key = key;
                Stage = stage;
            }
        }

        private class UnmatchedLeftRow
        {
            public int LeftIndex { get; }
            public Dictionary<string, object?> Row { get; }
            public string Reason { get; }

            public UnmatchedLeftRow(int leftIndex, Dictionary<string, object?> row, string reason)
            {
                LeftIndex = leftIndex;
                Row = row;
                Reason = reason;
            }
        }

        private readonly record struct MergeKey(string Id, string Address, string HouseNumber);
        #endregion

        [CommandMethod("DSAPPLYAFKOLING")]
        public void dsapplyafkoling()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            PropertySetManager.UpdatePropertySetDefinition(localDb, PSetDefs.DefinedSets.BBR);

            using Transaction tx = localDb.TransactionManager.StartTransaction();

            try
            {
                var bbrs = localDb.HashSetOfTypeWithPs<BlockReference>(tx,
                    PSetDefs.DefinedSets.BBR).Select(x => new BBR(x)).ToHashSet();

                var pts = localDb.HashSetOfType<DBPoint>(tx);

                foreach (var bbr in bbrs)
                {
                    var location = new Point3d(bbr.X, bbr.Y, 0);

                    var nearestPts = pts
                        .Where(p => p.Position.DistanceHorizontalTo(location) < 3.0);

                    if (!nearestPts.Any())
                    {
                        prdDbg("No nearby points found for BBR at location:");
                        DebugHelper.CreateDebugLine(location);
                        continue;
                    }

                    var data = nearestPts.Select(p => (
                    MWh: PropertySetManager.ReadNonDefinedPropertySetDouble(
                        p, "forbrugerdata", "EnergiiMWH"),
                    Afkoling: PropertySetManager.ReadNonDefinedPropertySetDouble(
                        p, "forbrugerdata", "Afkøling")
                    ));

                    double totalEnergy = data.Sum(d => d.MWh);
                    double weightedAfkoling = data.Sum(d => d.MWh * d.Afkoling) / totalEnergy;

                    bbr.EstimeretVarmeForbrug = totalEnergy;
                    bbr.TempDeltaVarme = weightedAfkoling;
                }
            }
            catch (System.Exception ex)
            {
                tx.Abort();
                prdDbg(ex);
                return;
            }

            tx.Commit();
        }

        [CommandMethod("DSCOPYAFKOLING")]
        public void dscopyafkoling()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            PropertySetManager.UpdatePropertySetDefinition(localDb, PSetDefs.DefinedSets.BBR);

            using Transaction tx = localDb.TransactionManager.StartTransaction();

            try
            {
                var bbrs = localDb.HashSetOfTypeWithPs<BlockReference>(tx,
                    PSetDefs.DefinedSets.BBR).Select(x => new BBR(x)).ToHashSet();

                var pts = localDb.HashSetOfType<DBPoint>(tx);

                foreach (var bbr in bbrs)
                {
                    var value = PropertySetManager.ReadNonDefinedPropertySetDouble(
                        bbr.Entity, "BBR", "TempDelta");
                    bbr.TempDeltaVarme = value;
                }
            }
            catch (System.Exception ex)
            {
                tx.Abort();
                prdDbg(ex);
                return;
            }

            tx.Commit();
        }
    }
}