using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.PlottingServices;
using Autodesk.AutoCAD.Runtime;

using Dreambuild.AutoCAD;

using Fluid;

using IntersectUtilities.DataScience;
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

        #region Fluid Templating Engine
        private static readonly FluidParser FluidParser = new FluidParser();

        private static readonly Lazy<IFluidTemplate> FluidTemplate = new Lazy<IFluidTemplate>(() =>
        {
            if (FluidParser.TryParse(MergeReportTemplate.Template, out var template, out var error))
                return template;
            throw new InvalidOperationException($"Failed to parse Fluid template: {error}");
        });

        private static readonly Lazy<IFluidTemplate> EnergyFluidTemplate = new Lazy<IFluidTemplate>(() =>
        {
            if (FluidParser.TryParse(EnergyReportTemplate.Template, out var template, out var error))
                return template;
            throw new InvalidOperationException($"Failed to parse Energy report Fluid template: {error}");
        });

        private static readonly Lazy<TemplateOptions> FluidOptions = new Lazy<TemplateOptions>(() =>
        {
            var options = new TemplateOptions();
            options.MemberAccessStrategy.Register<MergeReportModel>();
            options.MemberAccessStrategy.Register<SummaryCard>();
            options.MemberAccessStrategy.Register<StageCount>();
            options.MemberAccessStrategy.Register<ReportSection>();
            options.MemberAccessStrategy.Register<SectionGroup>();
            options.MemberAccessStrategy.Register<DataTableModel>();
            options.MemberAccessStrategy.Register<CellValue>();
            return options;
        });

        private static readonly Lazy<string> EmbeddedStyles = new Lazy<string>(() =>
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            // Find the CSS resource by suffix since assembly name can change (NetReload scenario)
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("MergeReportStyles.css"));
            if (resourceName == null)
                throw new InvalidOperationException("Embedded resource 'MergeReportStyles.css' not found.");
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new InvalidOperationException($"Could not load embedded resource '{resourceName}'.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });

        private static string RenderReport(MergeReportModel model)
        {
            var template = FluidTemplate.Value;
            var context = new TemplateContext(model, FluidOptions.Value);
            return template.Render(context);
        }

        private static string RenderEnergyReport(MergeReportModel model)
        {
            var template = EnergyFluidTemplate.Value;
            var context = new TemplateContext(model, FluidOptions.Value);
            return template.Render(context);
        }
        #endregion

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
            var model = BuildReportModel(
                leftTable, rightTable, matches, unmatched,
                orphanedRightIndices, matchStageCounts, adjustedCoordinates,
                leftGroups, rightGroups);

            string html = RenderReport(model);
            File.WriteAllText(reportPath, html, Encoding.UTF8);
        }

        private static MergeReportModel BuildReportModel(
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
            var model = new MergeReportModel
            {
                Styles = EmbeddedStyles.Value
            };

            // Identify non-1:1 matches
            var nonSimpleMatches = matches.Where(m => m.Stage != "Ejendomsnr").ToList();

            // Group matches by key to show full picture of duplicates
            var matchesByKey = matches
                .GroupBy(m => m.Key)
                .Where(g => g.Count() > 1 || g.Any(m => m.Stage != "Ejendomsnr"))
                .ToDictionary(g => g.Key, g => g.ToList());

            // Summary Cards
            model.SummaryCards = new List<SummaryCard>
            {
                new() { Label = "Left Rows", Value = leftTable.RowCount.ToString() },
                new() { Label = "Right Rows", Value = rightTable.RowCount.ToString() },
                new() { Label = "Matched", Value = matches.Count.ToString(),
                        CssClass = matches.Count == leftTable.RowCount ? "success" : "warning" },
                new() { Label = "Unmatched Left", Value = unmatched.Count.ToString(),
                        CssClass = unmatched.Count == 0 ? "success" : "error" },
                new() { Label = "Orphaned Right", Value = orphanedRightIndices.Count.ToString(),
                        CssClass = orphanedRightIndices.Count == 0 ? "success" : "warning" },
                new() { Label = "Non-Simple Matches", Value = nonSimpleMatches.Count.ToString(),
                        CssClass = nonSimpleMatches.Count == 0 ? "success" : "warning" },
                new() { Label = "Coord. Adjustments", Value = adjustedCoordinates.ToString(),
                        CssClass = adjustedCoordinates == 0 ? "success" : "warning" },
            };

            // Match Stages
            model.MatchStages = matchStageCounts
                .OrderByDescending(x => x.Value)
                .Select(x => new StageCount { Stage = x.Key, Count = x.Value })
                .ToList();

            // Build sections
            model.Sections = new List<ReportSection>();

            // Section 1: Duplicate & Complex Matches
            var complexKeys = matchesByKey.Keys.ToList();
            var complexSection = new ReportSection
            {
                Title = "Duplicate & Complex Matches (Full Picture)",
                Count = complexKeys.Count,
                BadgeClass = complexKeys.Count > 0 ? "warning" : "success",
                EmptyMessage = "All matches were simple 1:1 by Ejendomsnr. No complex matches to report."
            };

            foreach (var key in complexKeys)
            {
                var groupMatches = matchesByKey[key];
                var leftIndicesInGroup = leftGroups.TryGetValue(key, out var lg) ? lg : new List<int>();
                var rightIndicesInGroup = rightGroups.TryGetValue(key, out var rg) ? rg : new List<int>();

                var group = new SectionGroup
                {
                    Header = $"Key: {HtmlEncode(key.Id)} | {HtmlEncode(key.Address)} | {HtmlEncode(key.HouseNumber)} ({groupMatches.Count} matches)",
                    Description = $"<strong>Left rows with this key:</strong> {leftIndicesInGroup.Count} | <strong>Right rows with this key:</strong> {rightIndicesInGroup.Count}"
                };

                // Pairings table
                var pairingsTable = new DataTableModel
                {
                    Label = "Pairings Made:",
                    Columns = new List<string> { "Stage", "Left#", "Right#" }
                };
                foreach (var col in leftTable.ColumnNames.Take(5))
                    pairingsTable.Columns.Add($"L:{col}");
                foreach (var col in rightTable.ColumnNames.Take(5))
                    pairingsTable.Columns.Add($"R:{col}");

                foreach (var m in groupMatches)
                {
                    var row = new List<CellValue>
                    {
                        CellValue.Badge(m.Stage, $"stage-{m.Stage.Replace("+", "")}"),
                        CellValue.Simple((m.LeftIndex + 1).ToString()),
                        CellValue.Simple((m.RightIndex + 1).ToString())
                    };
                    foreach (var col in leftTable.ColumnNames.Take(5))
                        row.Add(CellValue.Simple(FormatValue(m.LeftRow, col), 30));
                    foreach (var col in rightTable.ColumnNames.Take(5))
                        row.Add(CellValue.Simple(FormatValue(m.RightRow, col), 30));
                    pairingsTable.Rows.Add(row);
                }
                group.Tables.Add(pairingsTable);

                // Left rows table
                if (leftIndicesInGroup.Count > 0)
                {
                    group.Tables.Add(BuildFullRowTableModel(
                        "All Left Rows in Group:",
                        leftTable,
                        leftIndicesInGroup.Select(i => leftTable.Rows[i]).ToList(),
                        leftIndicesInGroup));
                }

                // Right rows table
                if (rightIndicesInGroup.Count > 0)
                {
                    group.Tables.Add(BuildFullRowTableModel(
                        "All Right Rows in Group:",
                        rightTable,
                        rightIndicesInGroup.Select(i => rightTable.Rows[i]).ToList(),
                        rightIndicesInGroup));
                }

                complexSection.Groups.Add(group);
            }
            model.Sections.Add(complexSection);

            // Section 2: Unmatched Left Rows
            var unmatchedSection = new ReportSection
            {
                Title = "Unmatched Left Rows",
                Count = unmatched.Count,
                BadgeClass = unmatched.Count == 0 ? "success" : "error",
                EmptyMessage = "All left rows were successfully matched."
            };

            var byReason = unmatched.GroupBy(u => u.Reason).OrderByDescending(g => g.Count());
            foreach (var reasonGroup in byReason)
            {
                var group = new SectionGroup
                {
                    Header = $"Reason: {HtmlEncode(reasonGroup.Key)} ({reasonGroup.Count()} rows)"
                };
                group.Tables.Add(BuildFullRowTableModel(
                    "",
                    leftTable,
                    reasonGroup.Select(u => u.Row).ToList(),
                    reasonGroup.Select(u => u.LeftIndex).ToList()));
                unmatchedSection.Groups.Add(group);
            }
            model.Sections.Add(unmatchedSection);

            // Section 3: Orphaned Right Rows
            var orphanedSection = new ReportSection
            {
                Title = "Orphaned Right Rows (Never Matched)",
                Count = orphanedRightIndices.Count,
                BadgeClass = orphanedRightIndices.Count == 0 ? "success" : "warning",
                EmptyMessage = "All right rows were used in matches."
            };

            if (orphanedRightIndices.Count > 0)
            {
                var group = new SectionGroup
                {
                    Description = "These rows from the right (geocoding) dataset were never matched to any left row."
                };
                group.Tables.Add(BuildFullRowTableModel(
                    "",
                    rightTable,
                    orphanedRightIndices.Select(i => rightTable.Rows[i]).ToList(),
                    orphanedRightIndices));
                orphanedSection.Groups.Add(group);
            }
            model.Sections.Add(orphanedSection);

            // Section 4: Coordinate Adjustments
            var adjustedMatches = matches.Where(m => m.AdjustedLatitude.HasValue).ToList();
            var coordSection = new ReportSection
            {
                Title = "Coordinate Adjustments",
                Count = adjustedMatches.Count,
                BadgeClass = adjustedMatches.Count == 0 ? "success" : "warning",
                EmptyMessage = "No coordinate adjustments were necessary."
            };

            if (adjustedMatches.Count > 0)
            {
                var group = new SectionGroup
                {
                    Description = "These matches had overlapping coordinates and were offset by 5m increments."
                };

                var coordTable = new DataTableModel
                {
                    Columns = new List<string> { "Left#", "Key", "Original Lat", "Original Lng", "Adjusted Lat", "Adjusted Lng" }
                };

                foreach (var m in adjustedMatches)
                {
                    coordTable.Rows.Add(new List<CellValue>
                    {
                        CellValue.Simple((m.LeftIndex + 1).ToString()),
                        CellValue.Simple(m.Key.Id),
                        CellValue.Simple(m.Latitude?.ToString("F6") ?? ""),
                        CellValue.Simple(m.Longitude?.ToString("F6") ?? ""),
                        CellValue.Simple(m.AdjustedLatitude?.ToString("F6") ?? ""),
                        CellValue.Simple(m.AdjustedLongitude?.ToString("F6") ?? "")
                    });
                }
                group.Tables.Add(coordTable);
                coordSection.Groups.Add(group);
            }
            model.Sections.Add(coordSection);

            return model;
        }

        private static DataTableModel BuildFullRowTableModel(
            string label,
            CsvTypedDataTable table,
            List<Dictionary<string, object?>> rows,
            List<int> indices)
        {
            var model = new DataTableModel
            {
                Label = label,
                Columns = new List<string> { "#" }
            };
            model.Columns.AddRange(table.ColumnNames);

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                int rowNum = indices[i] + 1;
                var cellRow = new List<CellValue> { CellValue.Simple(rowNum.ToString()) };
                foreach (var col in table.ColumnNames)
                {
                    cellRow.Add(CellValue.Simple(FormatValue(row, col)));
                }
                model.Rows.Add(cellRow);
            }

            return model;
        }

        private static string FormatValue(Dictionary<string, object?> row, string column)
        {
            if (row == null || !row.TryGetValue(column, out var obj) || obj == null)
                return "";
            if (obj is double d)
                return d.ToString("G", CultureInfo.InvariantCulture);
            return Convert.ToString(obj, CultureInfo.InvariantCulture) ?? "";
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

        [CommandMethod("DSFORDELFORBRUG")]
        public void dsfordelforbrug()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            PropertySetManager.UpdatePropertySetDefinition(localDb, PSetDefs.DefinedSets.BBR);

            using Transaction tx = localDb.TransactionManager.StartTransaction();

            try
            {
                // Select blocks using window/crossing/lasso selection
                ObjectId[] blockIds = Interaction.GetSelection(
                    "\nSelect blocks to distribute demand to: ",
                    "INSERT");  // DXF name for BlockReference

                if (blockIds.Length == 0)
                {
                    prdDbg("\nNo blocks selected. Command cancelled.");
                    tx.Abort();
                    return;
                }

                var selectedBlocks = blockIds
                    .Select(id => tx.GetObject(id, OpenMode.ForWrite) as BlockReference)
                    .Where(br => br != null)
                    .ToList()!;

                prdDbg($"\n{selectedBlocks.Count} blocks selected. Now select a DBPoint with demand data:");

                // Select single DBPoint
                var pointId = Interaction.GetEntity(
                    "\nSelect DBPoint with power demand: ",
                    typeof(DBPoint));

                if (pointId == ObjectId.Null)
                {
                    prdDbg("\nNo point selected. Command cancelled.");
                    tx.Abort();
                    return;
                }

                var demandPoint = tx.GetObject(pointId, OpenMode.ForRead) as DBPoint;
                if (demandPoint == null)
                {
                    prdDbg("\nFailed to read point. Command cancelled.");
                    tx.Abort();
                    return;
                }

                var bbrs = selectedBlocks.Select(x => new BBR(x));

                // Read power demand from the point using stub method
                double totalDemand = PropertySetManager.ReadNonDefinedPropertySetDouble(
                    demandPoint, "Forbrugsoverblik 2024_merged", "EnergiMWh");
                prdDbg($"\nTotal power demand from point: {totalDemand:F2}");

                // Calculate total floor space from all selected blocks
                double totalFloorSpace = bbrs.Sum(br => br.SamletBoligareal);

                if (totalFloorSpace <= 0)
                {
                    prdDbg("\nTotal floor space is zero or negative. Cannot distribute demand.");
                    tx.Abort();
                    return;
                }

                prdDbg($"Total floor space: {totalFloorSpace:F2}");

                // Distribute demand to each block based on floor space proportion
                foreach (var bbr in bbrs)
                {
                    double bbrFloorSpace = bbr.SamletBoligareal;
                    double proportion = bbrFloorSpace / totalFloorSpace;
                    double distributedDemand = totalDemand * proportion;

                    bbr.EstimeretVarmeForbrug = distributedDemand;

                    prdDbg($"Block {bbr.Adresse}: FloorSpace={bbrFloorSpace:F2}, " +
                           $"Proportion={proportion:P2}, Demand={distributedDemand:F2}");
                }

                prdDbg($"\nSuccessfully distributed {totalDemand:F2} demand to {selectedBlocks.Count} blocks.");
            }
            catch (System.Exception ex)
            {
                tx.Abort();
                prdDbg(ex);
                return;
            }

            tx.Commit();
        }

        [CommandMethod("DSCLONEBBRFROMPOINT")]
        public void dsclonebbrfrompoint()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            PropertySetManager.UpdatePropertySetDefinition(localDb, PSetDefs.DefinedSets.BBR);

            using Transaction tx = localDb.TransactionManager.StartTransaction();

            try
            {
                // Get model space for adding new blocks
                BlockTable bt = (BlockTable)tx.GetObject(localDb.BlockTableId, OpenMode.ForRead);
                BlockTableRecord space = (BlockTableRecord)tx.GetObject(
                    localDb.CurrentSpaceId, OpenMode.ForWrite);

                // Outer loop: Select BBR template block
                while (true)
                {
                    var templateId = Interaction.GetEntity(
                        "\nSelect BBR block as template (Space to exit): ",
                        typeof(BlockReference));

                    if (templateId == ObjectId.Null)
                    {
                        prdDbg("\nCommand finished.");
                        break;
                    }

                    var templateBlock = tx.GetObject(templateId, OpenMode.ForRead) as BlockReference;
                    if (templateBlock == null)
                    {
                        prdDbg("\nFailed to read block. Try again.");
                        continue;
                    }

                    // Verify it has BBR PropertySet attached
                    if (!PropertySetManager.IsPropertySetAttached(templateBlock, PSetDefs.DefinedSets.BBR))
                    {
                        prdDbg("\nSelected block does not have BBR PropertySet attached. Select another.");
                        continue;
                    }

                    var templateBbr = new BBR(templateBlock);
                    prdDbg($"\nTemplate: {templateBbr.Adresse} (Block: {templateBlock.Name})");

                    int cloneCount = 0;

                    // Inner loop: Select DBPoints to place clones
                    while (true)
                    {
                        var pointId = Interaction.GetEntity(
                            $"\nSelect DBPoint to place clone ({cloneCount} created, Space for new template): ",
                            typeof(DBPoint));

                        if (pointId == ObjectId.Null)
                        {
                            prdDbg($"\n{cloneCount} clones created from this template.");
                            break; // Back to outer loop
                        }

                        var sourcePoint = tx.GetObject(pointId, OpenMode.ForRead) as DBPoint;
                        if (sourcePoint == null)
                        {
                            prdDbg("\nFailed to read point. Try again.");
                            continue;
                        }

                        // Read data from the DBPoint's PropertySet
                        string ptAdresse = PropertySetManager.ReadNonDefinedPropertySetString(
                            sourcePoint, "Forbrugsoverblik 2024_merged", "Adresse");
                        string ptHusnr = PropertySetManager.ReadNonDefinedPropertySetString(
                            sourcePoint, "Forbrugsoverblik 2024_merged", "Husnr");
                        double ptEnergi = PropertySetManager.ReadNonDefinedPropertySetDouble(
                            sourcePoint, "Forbrugsoverblik 2024_merged", "EnergiMWh");

                        // Create new BlockReference at point location
                        BlockReference newBlock = new BlockReference(
                            sourcePoint.Position,
                            templateBlock.BlockTableRecord);
                        newBlock.SetDatabaseDefaults(localDb);
                        newBlock.ScaleFactors = templateBlock.ScaleFactors;
                        newBlock.Rotation = templateBlock.Rotation;
                        newBlock.Layer = templateBlock.Layer;

                        space.AppendEntity(newBlock);
                        tx.AddNewlyCreatedDBObject(newBlock, true);

                        // Create BBR wrapper for new block (this attaches the PropertySet)
                        var newBbr = new BBR(newBlock);

                        // Copy all properties from template
                        newBbr.id_lokalId = Guid.NewGuid().ToString(); // New GUID!
                        newBbr.id_husnummerid = templateBbr.id_husnummerid;
                        newBbr.Name = $"{ptAdresse} {ptHusnr}, 4000 Roskilde";
                        newBbr.Bygningsnummer = templateBbr.Bygningsnummer;
                        newBbr.BygningsAnvendelseNyTekst = templateBbr.BygningsAnvendelseNyTekst;
                        newBbr.BygningsAnvendelseNyKode = templateBbr.BygningsAnvendelseNyKode;
                        newBbr.BygningsAnvendelseGlTekst = templateBbr.BygningsAnvendelseGlTekst;
                        newBbr.BygningsAnvendelseGlKode = templateBbr.BygningsAnvendelseGlKode;
                        newBbr.Opførelsesår = templateBbr.Opførelsesår;
                        newBbr.SamletBygningsareal = templateBbr.SamletBygningsareal;
                        newBbr.SamletBoligareal = templateBbr.SamletBoligareal;
                        newBbr.SamletErhvervsareal = templateBbr.SamletErhvervsareal;
                        newBbr.BebyggetAreal = templateBbr.BebyggetAreal;
                        newBbr.KælderAreal = templateBbr.KælderAreal;
                        newBbr.VarmeInstallation = templateBbr.VarmeInstallation;
                        newBbr.OpvarmningsMiddel = templateBbr.OpvarmningsMiddel;
                        newBbr.Status = templateBbr.Status;
                        newBbr.Vejklasse = templateBbr.Vejklasse;
                        newBbr.Postnr = templateBbr.Postnr;
                        newBbr.By = templateBbr.By;
                        newBbr.SpecifikVarmeForbrug = templateBbr.SpecifikVarmeForbrug;
                        newBbr.DistriktetsNavn = templateBbr.DistriktetsNavn;
                        newBbr.Type = templateBbr.Type;
                        newBbr.TempDeltaVarme = templateBbr.TempDeltaVarme;
                        newBbr.TempDeltaBV = templateBbr.TempDeltaBV;

                        // Set address from point data
                        newBbr.Vejnavn = ptAdresse;
                        newBbr.Husnummer = ptHusnr;
                        newBbr.Adresse = $"{ptAdresse} {ptHusnr}";
                        newBbr.EstimeretVarmeForbrug = ptEnergi;

                        cloneCount++;
                        prdDbg($"Created: {newBbr.Adresse} (Energy: {ptEnergi:F2} MWh)");
                    }
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

        [CommandMethod("DSSUMENERGITOBR")]
        public void dssumenergitobr()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            PropertySetManager.UpdatePropertySetDefinition(localDb, PSetDefs.DefinedSets.BBR);

            using Transaction tx = localDb.TransactionManager.StartTransaction();

            try
            {
                // Collect all BBR blocks
                var bbrs = localDb.HashSetOfTypeWithPs<BlockReference>(tx, PSetDefs.DefinedSets.BBR)
                    .Select(x => new BBR(x))
                    .ToList();

                // Collect all DBPoints
                var pts = localDb.HashSetOfType<DBPoint>(tx).ToList();

                prdDbg($"\nFound {bbrs.Count} BBR blocks and {pts.Count} DBPoints.");

                // Data tracking for report
                var matchedBbrs = new List<(string Address, double PreviousEnergy, double NewEnergy)>();
                var unmatchedBbrs = new List<(string Address, double ExistingEnergy)>();
                var usedPointIds = new HashSet<ObjectId>();

                double totalEnergyAssigned = 0;

                var pm = new ProgressMeter();
                try
                {
                    pm.Start($"Processing {bbrs.Count} BBR blocks...");
                    pm.SetLimit(bbrs.Count);

                    foreach (var bbr in bbrs)
                    {
                        var location = new Point3d(bbr.X, bbr.Y, 0);

                        // Find all DBPoints within 1 unit of the BBR location
                        var nearbyPts = pts
                            .Where(p => p.Position.DistanceHorizontalTo(location) < 1.0)
                            .ToList();

                        if (nearbyPts.Count == 0)
                        {
                            unmatchedBbrs.Add((bbr.Adresse, bbr.EstimeretVarmeForbrug));
                            pm.MeterProgress();
                            continue;
                        }

                        // Track used points
                        foreach (var pt in nearbyPts)
                            usedPointIds.Add(pt.ObjectId);

                        // Sum EnergiMWh from all nearby points
                        double newEnergy = nearbyPts.Sum(p =>
                            PropertySetManager.ReadNonDefinedPropertySetDouble(
                                p, "Forbrugsoverblik 2024_merged", "EnergiMWh"));

                        // Capture previous value before overwriting
                        double previousEnergy = bbr.EstimeretVarmeForbrug;
                        bbr.EstimeretVarmeForbrug = newEnergy;

                        matchedBbrs.Add((bbr.Adresse, previousEnergy, newEnergy));
                        totalEnergyAssigned += newEnergy;
                        pm.MeterProgress();
                    }
                }
                finally
                {
                    pm.Stop();
                }

                // Find unmatched DBPoints
                var unmatchedPoints = pts
                    .Where(p => !usedPointIds.Contains(p.ObjectId))
                    .Select(p => (
                        Address: PropertySetManager.ReadNonDefinedPropertySetString(
                            p, "Forbrugsoverblik 2024_merged", "Adresse") + " " +
                            PropertySetManager.ReadNonDefinedPropertySetString(
                            p, "Forbrugsoverblik 2024_merged", "Husnr"),
                        Energy: PropertySetManager.ReadNonDefinedPropertySetDouble(
                            p, "Forbrugsoverblik 2024_merged", "EnergiMWh")
                    ))
                    .ToList();

                prdDbg($"\nResults:");
                prdDbg($"  Updated: {matchedBbrs.Count} BBR blocks");
                prdDbg($"  Skipped (no nearby points): {unmatchedBbrs.Count} BBR blocks");
                prdDbg($"  Unmatched DBPoints: {unmatchedPoints.Count}");
                prdDbg($"  Total energy assigned: {totalEnergyAssigned:F2} MWh");

                // Generate HTML report
                string reportPath = Path.Combine(
                    Path.GetDirectoryName(localDb.Filename) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "DSSUMENERGY_report.html");

                GenerateEnergyReport(reportPath, matchedBbrs, unmatchedBbrs, unmatchedPoints, totalEnergyAssigned);
                prdDbg($"\nHTML report saved to: {reportPath}");
            }
            catch (System.Exception ex)
            {
                tx.Abort();
                prdDbg(ex);
                return;
            }

            tx.Commit();
        }

        private static void GenerateEnergyReport(
            string reportPath,
            List<(string Address, double PreviousEnergy, double NewEnergy)> matchedBbrs,
            List<(string Address, double ExistingEnergy)> unmatchedBbrs,
            List<(string Address, double Energy)> unmatchedPoints,
            double totalEnergyAssigned)
        {
            var model = new MergeReportModel
            {
                Styles = EmbeddedStyles.Value
            };

            // Summary Cards
            model.SummaryCards = new List<SummaryCard>
            {
                new() { Label = "Matched BBRs", Value = matchedBbrs.Count.ToString(),
                        CssClass = matchedBbrs.Count > 0 ? "success" : "warning" },
                new() { Label = "Unmatched BBRs", Value = unmatchedBbrs.Count.ToString(),
                        CssClass = unmatchedBbrs.Count == 0 ? "success" : "warning" },
                new() { Label = "Unmatched Points", Value = unmatchedPoints.Count.ToString(),
                        CssClass = unmatchedPoints.Count == 0 ? "success" : "error" },
                new() { Label = "Total Energy", Value = $"{totalEnergyAssigned:F2} MWh",
                        CssClass = "success" }
            };

            model.Sections = new List<ReportSection>();

            // Section 1: Matched BBRs (sorted ascending by new energy)
            var matchedSection = new ReportSection
            {
                Title = "Matched BBR Blocks",
                Count = matchedBbrs.Count,
                BadgeClass = matchedBbrs.Count > 0 ? "success" : "warning",
                EmptyMessage = "No BBR blocks were matched with DBPoints."
            };

            if (matchedBbrs.Count > 0)
            {
                var sortedMatched = matchedBbrs.OrderBy(x => x.NewEnergy).ToList();
                var matchedTable = new DataTableModel
                {
                    Label = $"Total assigned: {totalEnergyAssigned:F2} MWh",
                    Columns = new List<string> { "#", "Address", "Previous (MWh)", "New (MWh)" }
                };

                int idx = 1;
                foreach (var (address, previousEnergy, newEnergy) in sortedMatched)
                {
                    matchedTable.Rows.Add(new List<CellValue>
                    {
                        CellValue.Simple(idx.ToString()),
                        CellValue.Simple(address, 50),
                        CellValue.Simple(previousEnergy.ToString("F2")),
                        CellValue.Simple(newEnergy.ToString("F2"))
                    });
                    idx++;
                }

                var group = new SectionGroup();
                group.Tables.Add(matchedTable);
                matchedSection.Groups.Add(group);
            }
            model.Sections.Add(matchedSection);

            // Section 2: Unmatched BBRs
            var unmatchedBbrSection = new ReportSection
            {
                Title = "Unmatched BBR Blocks (No Nearby Points)",
                Count = unmatchedBbrs.Count,
                BadgeClass = unmatchedBbrs.Count == 0 ? "success" : "warning",
                EmptyMessage = "All BBR blocks were matched with DBPoints."
            };

            if (unmatchedBbrs.Count > 0)
            {
                var unmatchedBbrTable = new DataTableModel
                {
                    Columns = new List<string> { "#", "Address", "Existing Energy (MWh)" }
                };

                int idx = 1;
                foreach (var (address, existingEnergy) in unmatchedBbrs.OrderBy(x => x.Address))
                {
                    unmatchedBbrTable.Rows.Add(new List<CellValue>
                    {
                        CellValue.Simple(idx.ToString()),
                        CellValue.Simple(address, 50),
                        CellValue.Simple(existingEnergy.ToString("F2"))
                    });
                    idx++;
                }

                var group = new SectionGroup
                {
                    Description = "These BBR blocks had no DBPoints within 1 unit distance."
                };
                group.Tables.Add(unmatchedBbrTable);
                unmatchedBbrSection.Groups.Add(group);
            }
            model.Sections.Add(unmatchedBbrSection);

            // Section 3: Unmatched DBPoints
            var unmatchedPointsSection = new ReportSection
            {
                Title = "Unmatched DBPoints (No Nearby BBR)",
                Count = unmatchedPoints.Count,
                BadgeClass = unmatchedPoints.Count == 0 ? "success" : "error",
                EmptyMessage = "All DBPoints were matched with BBR blocks."
            };

            if (unmatchedPoints.Count > 0)
            {
                double unmatchedPointsTotal = unmatchedPoints.Sum(x => x.Energy);
                var unmatchedPointsTable = new DataTableModel
                {
                    Label = $"Total unassigned: {unmatchedPointsTotal:F2} MWh",
                    Columns = new List<string> { "#", "Address", "Energy (MWh)" }
                };

                int idx = 1;
                foreach (var (address, energy) in unmatchedPoints.OrderBy(x => x.Address))
                {
                    unmatchedPointsTable.Rows.Add(new List<CellValue>
                    {
                        CellValue.Simple(idx.ToString()),
                        CellValue.Simple(address, 50),
                        CellValue.Simple(energy.ToString("F2"))
                    });
                    idx++;
                }

                var group = new SectionGroup
                {
                    Description = "These DBPoints had no BBR block within 1 unit distance. Their energy was not assigned."
                };
                group.Tables.Add(unmatchedPointsTable);
                unmatchedPointsSection.Groups.Add(group);
            }
            model.Sections.Add(unmatchedPointsSection);

            string html = RenderEnergyReport(model);
            File.WriteAllText(reportPath, html, Encoding.UTF8);
        }
    }
}