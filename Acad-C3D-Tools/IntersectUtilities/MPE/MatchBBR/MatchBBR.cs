using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;
using System.Xml.Linq;
using Autodesk.Aec.PropertyData.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;
using Autodesk.Gis.Map.Project;
using static IntersectUtilities.UtilsCommon.Utils;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using CadColor = Autodesk.AutoCAD.Colors.Color;
using FormsDialogResult = System.Windows.Forms.DialogResult;
using FormsOpenFileDialog = System.Windows.Forms.OpenFileDialog;
using FormsSaveFileDialog = System.Windows.Forms.SaveFileDialog;
using OdTable = Autodesk.Gis.Map.ObjectData.Table;
using OdTables = Autodesk.Gis.Map.ObjectData.Tables;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        private const string MatchBbrCommandName = "MATCHBBR";
        private const string CompareBbrCommandName = "COMPAREBBR";
        private const string MatchBbrPropertySetName = "BBR";
        private const string MatchBbrAddressPropertyName = "Adresse";
        private const string MatchBbrTypePropertyName = "Type";
        private const string MatchBbrSkippedTypeValue = "Ingen";
        private const string MatchBbrMissingInformationLayerName = "MISSING_INFORMATION";
        private const short MatchBbrYellowColorIndex = 2;
        private static readonly XNamespace SpreadsheetMlNs =
            "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace PackageRelationshipsNs =
            "http://schemas.openxmlformats.org/package/2006/relationships";
        private static readonly XNamespace OfficeDocumentRelationshipsNs =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        /// <command>MATCHBBR</command>
        /// <summary>
        /// Matches BBR block addresses inside selected closed boundary polylines against rows in an Excel file and draws a
        /// circle at each matching block on a district-named layer. Blocks whose address is missing from the Excel data are
        /// marked on the MISSING_INFORMATION layer. After the circles are created, the user can choose whether to export a
        /// missing-address report and, if so, choose where to save the new Excel file. Blocks with BBR Type set to "Ingen"
        /// are skipped, and boundary polylines with arc segments are rejected.
        /// </summary>
        /// <category>MPE</category>
        [CommandMethod(MatchBbrCommandName, CommandFlags.Modal)]
        public void MatchBBR()
        {
            DocumentCollection docCol = AcadApp.DocumentManager;
            Document doc = docCol.MdiActiveDocument;
            Editor editor = doc.Editor;
            Database localDb = doc.Database;

            ObjectId[] boundaryPolylineIds = PromptForBoundaryPolylines(editor);
            if (boundaryPolylineIds.Length == 0)
            {
                return;
            }

            string? excelPath = PromptForExcelPath(editor);
            if (string.IsNullOrWhiteSpace(excelPath))
            {
                return;
            }

            if (!File.Exists(excelPath))
            {
                editor.WriteMessage("\nExcel file not found.");
                return;
            }

            Dictionary<string, string> districtByAddress;
            try
            {
                districtByAddress = LoadDistrictsFromExcel(excelPath);
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                editor.WriteMessage("\nFailed to read the Excel file.");
                return;
            }

            if (districtByAddress.Count == 0)
            {
                editor.WriteMessage("\nNo address rows were read from the Excel file.");
                return;
            }

            using Transaction tx = localDb.TransactionManager.StartTransaction();
            try
            {
                BlockTable blockTable = (BlockTable)tx.GetObject(localDb.BlockTableId, OpenMode.ForRead);
                BlockTableRecord modelSpace =
                    (BlockTableRecord)tx.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                List<Polyline> boundaryPolylines = LoadBoundaryPolylines(boundaryPolylineIds, tx, editor);
                if (boundaryPolylines.Count == 0)
                {
                    return;
                }

                RemoveExistingMarkers(modelSpace, tx, boundaryPolylines);
                HashSet<string> existingCircleLocations = GetExistingMarkerLocations(modelSpace, tx);

                int createdCount = 0;
                int matchedCount = 0;
                int missingCount = 0;
                var missingAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (ObjectId entityId in modelSpace)
                {
                    if (tx.GetObject(entityId, OpenMode.ForRead) is not BlockReference blockReference)
                    {
                        continue;
                    }

                    if (!IsPointInsideAnyBoundary(blockReference.Position, boundaryPolylines))
                    {
                        continue;
                    }

                    if (HasSkippedBbrType(blockReference, tx))
                    {
                        continue;
                    }

                    string? address = TryGetBbrAddress(blockReference, tx);
                    if (string.IsNullOrWhiteSpace(address))
                    {
                        continue;
                    }

                    string normalizedAddress = NormalizeAddress(address);
                    if (!districtByAddress.TryGetValue(normalizedAddress, out string? district))
                    {
                        EnsureLayerExists(
                            MatchBbrMissingInformationLayerName,
                            tx,
                            localDb,
                            CadColor.FromColorIndex(ColorMethod.ByAci, MatchBbrYellowColorIndex));

                        Polyline missingMarker = CreateMarkerPolyline(blockReference.Position, 5.0, MatchBbrMissingInformationLayerName);

                        if (TryAppendMarkerAtLocation(modelSpace, tx, missingMarker, existingCircleLocations))
                        {
                            missingCount++;
                        }

                        missingAddresses.Add(address.Trim());
                        continue;
                    }

                    matchedCount++;

                    string layerName = SanitizeLayerName(district);
                    EnsureLayerExists(layerName, tx, localDb, null);

                    Polyline marker = CreateMarkerPolyline(blockReference.Position, 5.0, layerName);

                    if (TryAppendMarkerAtLocation(modelSpace, tx, marker, existingCircleLocations))
                    {
                        createdCount++;
                    }
                }

                tx.Commit();

                editor.WriteMessage(
                    $"\n{MatchBbrCommandName} complete. Matched {matchedCount} blocks and created {createdCount} circles. "
                    + $"Marked {missingCount} blocks with missing information on layer {MatchBbrMissingInformationLayerName}.");

                if (missingAddresses.Count == 0)
                {
                    editor.WriteMessage("\nNo missing addresses were found, so no export was needed.");
                    return;
                }

                if (!PromptToExportMissingAddresses(editor))
                {
                    editor.WriteMessage("\nMissing-address export skipped.");
                    return;
                }

                string? outputPath = PromptForMissingAddressesSavePath(excelPath, editor);
                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    editor.WriteMessage("\nMissing-address export cancelled.");
                    return;
                }

                string missingAddressesPath = ExportMissingAddresses(outputPath, missingAddresses);
                editor.WriteMessage($"\nExported {missingAddresses.Count} missing addresses to {missingAddressesPath}.");
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                editor.WriteMessage($"\n{MatchBbrCommandName} failed. See debug output for details.");
                return;
            }
        }

        /// <command>COMPAREBBR</command>
        /// <summary>
        /// Compares address rows in the selected Excel workbook against BBR blocks inside selected closed boundary
        /// polylines and finds rows that exist in Excel but do not exist in the drawing. The comparison uses the same
        /// address construction as MATCHBBR from Excel columns D and E and skips drawing blocks whose BBR Type is
        /// "Ingen". The missing Excel rows can then be exported to a new workbook with the same column structure as
        /// the source worksheet.
        /// </summary>
        /// <category>MPE</category>
        [CommandMethod(CompareBbrCommandName, CommandFlags.Modal)]
        public void CompareBBR()
        {
            DocumentCollection docCol = AcadApp.DocumentManager;
            Document doc = docCol.MdiActiveDocument;
            Editor editor = doc.Editor;
            Database localDb = doc.Database;

            ObjectId[] boundaryPolylineIds = PromptForBoundaryPolylines(editor);
            if (boundaryPolylineIds.Length == 0)
            {
                return;
            }

            string? excelPath = PromptForExcelPath(editor);
            if (string.IsNullOrWhiteSpace(excelPath))
            {
                return;
            }

            if (!File.Exists(excelPath))
            {
                editor.WriteMessage("\nExcel file not found.");
                return;
            }

            WorksheetData worksheetData;
            try
            {
                worksheetData = LoadWorksheetData(excelPath);
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                editor.WriteMessage("\nFailed to read the Excel file.");
                return;
            }

            if (worksheetData.ColumnOrder.Count == 0 || worksheetData.Rows.Count == 0)
            {
                editor.WriteMessage("\nNo data rows were read from the Excel file.");
                return;
            }

            HashSet<string> drawingAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using Transaction tx = localDb.TransactionManager.StartTransaction();
            try
            {
                BlockTable blockTable = (BlockTable)tx.GetObject(localDb.BlockTableId, OpenMode.ForRead);
                BlockTableRecord modelSpace =
                    (BlockTableRecord)tx.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                List<Polyline> boundaryPolylines = LoadBoundaryPolylines(boundaryPolylineIds, tx, editor);
                if (boundaryPolylines.Count == 0)
                {
                    return;
                }

                foreach (ObjectId entityId in modelSpace)
                {
                    if (tx.GetObject(entityId, OpenMode.ForRead) is not BlockReference blockReference)
                    {
                        continue;
                    }

                    if (!IsPointInsideAnyBoundary(blockReference.Position, boundaryPolylines))
                    {
                        continue;
                    }

                    if (HasSkippedBbrType(blockReference, tx))
                    {
                        continue;
                    }

                    string? address = TryGetBbrAddress(blockReference, tx);
                    if (string.IsNullOrWhiteSpace(address))
                    {
                        continue;
                    }

                    drawingAddresses.Add(NormalizeAddress(address));
                }

                tx.Commit();
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                editor.WriteMessage($"\n{CompareBbrCommandName} failed. See debug output for details.");
                return;
            }

            List<WorksheetRowData> missingRows = worksheetData.Rows
                .Where(row => TryGetAddressFromWorksheetRow(row, out string normalizedAddress)
                              && !drawingAddresses.Contains(normalizedAddress))
                .ToList();

            editor.WriteMessage(
                $"\n{CompareBbrCommandName} complete. Compared {worksheetData.Rows.Count} Excel rows against "
                + $"{drawingAddresses.Count} drawing addresses and found {missingRows.Count} missing Excel rows.");

            if (missingRows.Count == 0)
            {
                editor.WriteMessage("\nNo missing Excel rows were found, so no export was needed.");
                return;
            }

            string? outputPath = PromptForCompareBbrSavePath(excelPath, editor);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                editor.WriteMessage("\nCompareBBR export cancelled.");
                return;
            }

            try
            {
                string exportedPath = ExportWorksheetRows(
                    outputPath,
                    worksheetData.ColumnOrder,
                    worksheetData.HeaderByColumn,
                    missingRows,
                    "MissingInDwg");
                editor.WriteMessage($"\nExported {missingRows.Count} missing Excel rows to {exportedPath}.");
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                editor.WriteMessage("\nFailed to export the CompareBBR workbook.");
            }
        }

        private static ObjectId[] PromptForBoundaryPolylines(Editor editor)
        {
            PromptSelectionOptions options = new PromptSelectionOptions
            {
                MessageForAdding = "\nSelect closed boundary polylines: "
            };

            SelectionFilter filter = new SelectionFilter(
                new[]
                {
                    new TypedValue((int)DxfCode.Start, "LWPOLYLINE")
                });

            PromptSelectionResult result = editor.GetSelection(options, filter);
            if (result.Status != PromptStatus.OK || result.Value is null || result.Value.Count == 0)
            {
                editor.WriteMessage("\nNo boundary polylines selected.");
                return Array.Empty<ObjectId>();
            }

            return result.Value.GetObjectIds();
        }

        private static string? PromptForExcelPath(Editor editor)
        {
            using FormsOpenFileDialog dialog = new FormsOpenFileDialog
            {
                Title = "Select Excel File",
                Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false
            };

            FormsDialogResult result = dialog.ShowDialog();
            if (result != FormsDialogResult.OK)
            {
                editor.WriteMessage("\nNo Excel file selected.");
                return null;
            }

            return dialog.FileName;
        }

        private static List<Polyline> LoadBoundaryPolylines(
            IEnumerable<ObjectId> boundaryPolylineIds,
            Transaction transaction,
            Editor editor)
        {
            List<Polyline> polylines = new List<Polyline>();

            foreach (ObjectId boundaryId in boundaryPolylineIds)
            {
                if (transaction.GetObject(boundaryId, OpenMode.ForRead) is not Polyline polyline)
                {
                    continue;
                }

                if (!polyline.Closed)
                {
                    editor.WriteMessage("\nAll selected boundary polylines must be closed.");
                    return new List<Polyline>();
                }

                if (PolylineHasArcs(polyline))
                {
                    editor.WriteMessage("\nBoundary polylines with arc segments are not supported.");
                    return new List<Polyline>();
                }

                polylines.Add(polyline);
            }

            return polylines;
        }

        private static Dictionary<string, string> LoadDistrictsFromExcel(string excelPath)
        {
            WorksheetData worksheetData = LoadWorksheetData(excelPath);
            if (worksheetData.ColumnOrder.Count == 0 || worksheetData.Rows.Count == 0)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            string districtColumnName = worksheetData.HeaderByColumn
                .FirstOrDefault(kvp =>
                    string.Equals(kvp.Value.Trim(), "Varmedistrikt", StringComparison.OrdinalIgnoreCase))
                .Key;
            if (string.IsNullOrWhiteSpace(districtColumnName))
            {
                throw new InvalidOperationException("Column 'Varmedistrikt' was not found in the first row.");
            }

            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (WorksheetRowData row in worksheetData.Rows)
            {
                string streetName = GetRowCellValue(row.CellsByColumn, "D");
                string streetNumber = GetRowCellValue(row.CellsByColumn, "E");
                string district = GetRowCellValue(row.CellsByColumn, districtColumnName);

                if (string.IsNullOrWhiteSpace(streetName)
                    || string.IsNullOrWhiteSpace(streetNumber)
                    || string.IsNullOrWhiteSpace(district))
                {
                    continue;
                }

                string trimmedDistrict = district.Trim();
                AddExcelAddressKey(result, $"{streetName} {streetNumber}", trimmedDistrict);
                AddExcelAddressKey(result, $"{streetNumber} {streetName}", trimmedDistrict);
            }

            return result;
        }

        private static void AddExcelAddressKey(
            IDictionary<string, string> result,
            string rawAddress,
            string district)
        {
            string normalizedAddress = NormalizeAddress(rawAddress);
            if (string.IsNullOrWhiteSpace(normalizedAddress))
            {
                return;
            }

            result[normalizedAddress] = district;
        }

        private static string? TryGetBbrAddress(BlockReference blockReference, Transaction transaction)
        {
            return TryGetBbrValue(blockReference, transaction, MatchBbrAddressPropertyName);
        }

        private static bool HasSkippedBbrType(BlockReference blockReference, Transaction transaction)
        {
            string? typeValue = TryGetBbrValue(blockReference, transaction, MatchBbrTypePropertyName);
            return string.Equals(typeValue?.Trim(), MatchBbrSkippedTypeValue, StringComparison.OrdinalIgnoreCase);
        }

        private static string? TryGetBbrValue(
            BlockReference blockReference,
            Transaction transaction,
            string propertyName)
        {
            return TryGetBbrValueFromPropertySet(blockReference, transaction, propertyName)
                   ?? TryGetBbrValueFromObjectData(blockReference, propertyName);
        }

        private static string? TryGetBbrValueFromPropertySet(
            BlockReference blockReference,
            Transaction transaction,
            string propertyName)
        {
            ObjectIdCollection propertySetIds;
            try
            {
                propertySetIds = PropertyDataServices.GetPropertySets(blockReference);
            }
            catch
            {
                return null;
            }

            foreach (ObjectId propertySetId in propertySetIds)
            {
                if (!propertySetId.IsValid || propertySetId.IsNull)
                {
                    continue;
                }

                if (transaction.GetObject(propertySetId, OpenMode.ForRead) is not PropertySet propertySet)
                {
                    continue;
                }

                if (!string.Equals(
                        propertySet.PropertySetDefinitionName,
                        MatchBbrPropertySetName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    int propertyId = propertySet.PropertyNameToId(propertyName);
                    object? value = propertySet.GetAt(propertyId);
                    return value?.ToString();
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        private static string? TryGetBbrValueFromObjectData(BlockReference blockReference, string propertyName)
        {
            ProjectModel project = HostMapApplicationServices.Application.ActiveProject;
            OdTables tables = project.ODTables;

            OdTable table;
            try
            {
                table = tables[MatchBbrPropertySetName];
            }
            catch
            {
                return null;
            }

            Records records = table.GetObjectTableRecords(
                0,
                blockReference.ObjectId,
                Autodesk.Gis.Map.Constants.OpenMode.OpenForRead,
                false);

            foreach (Record record in records)
            {
                for (int i = 0; i < table.FieldDefinitions.Count; i++)
                {
                    FieldDefinition fieldDefinition = table.FieldDefinitions[i];
                    if (!string.Equals(fieldDefinition.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    dynamic value = record[i];
                    return value?.StrValue?.ToString() ?? value?.ToString();
                }
            }

            return null;
        }

        private static bool PromptToExportMissingAddresses(Editor editor)
        {
            PromptKeywordOptions options =
                new PromptKeywordOptions("\nExport missing adresses? [Yes/No] <No>: ", "Yes No")
                {
                    AllowNone = true
                };

            PromptResult result = editor.GetKeywords(options);
            if (result.Status == PromptStatus.Cancel)
            {
                return false;
            }

            string response = string.IsNullOrWhiteSpace(result.StringResult) ? "No" : result.StringResult;
            return string.Equals(response, "Yes", StringComparison.OrdinalIgnoreCase);
        }

        private static string? PromptForMissingAddressesSavePath(string sourceExcelPath, Editor editor)
        {
            string initialDirectory = Path.GetDirectoryName(sourceExcelPath) ?? Environment.CurrentDirectory;
            string initialFileName = "missing_adresses.xlsx";

            using FormsSaveFileDialog dialog = new FormsSaveFileDialog
            {
                Title = "Save Missing Addresses Excel",
                Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
                InitialDirectory = initialDirectory,
                FileName = initialFileName,
                AddExtension = true,
                DefaultExt = "xlsx",
                OverwritePrompt = true
            };

            FormsDialogResult result = dialog.ShowDialog();
            if (result != FormsDialogResult.OK)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(dialog.FileName))
            {
                editor.WriteMessage("\nNo output file was selected.");
                return null;
            }

            return dialog.FileName;
        }

        private static string? PromptForCompareBbrSavePath(string sourceExcelPath, Editor editor)
        {
            string initialDirectory = Path.GetDirectoryName(sourceExcelPath) ?? Environment.CurrentDirectory;
            string initialFileName = "compare_bbr_missing_in_dwg.xlsx";

            using FormsSaveFileDialog dialog = new FormsSaveFileDialog
            {
                Title = "Save CompareBBR Excel",
                Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
                InitialDirectory = initialDirectory,
                FileName = initialFileName,
                AddExtension = true,
                DefaultExt = "xlsx",
                OverwritePrompt = true
            };

            FormsDialogResult result = dialog.ShowDialog();
            if (result != FormsDialogResult.OK)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(dialog.FileName))
            {
                editor.WriteMessage("\nNo output file was selected.");
                return null;
            }

            return dialog.FileName;
        }

        private static string ExportMissingAddresses(string outputPath, IEnumerable<string> missingAddresses)
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            List<string> orderedAddresses = missingAddresses
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();

            using FileStream fileStream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            using ZipArchive archive = new ZipArchive(fileStream, ZipArchiveMode.Create);

            CreateZipEntry(
                archive,
                "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
                + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
                + "<Override PartName=\"/xl/workbook.xml\" "
                + "ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"
                + "<Override PartName=\"/xl/worksheets/sheet1.xml\" "
                + "ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"
                + "</Types>");

            CreateZipEntry(
                archive,
                "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" "
                + "Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" "
                + "Target=\"xl/workbook.xml\"/>"
                + "</Relationships>");

            CreateZipEntry(
                archive,
                "xl/workbook.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" "
                + "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"
                + "<sheets><sheet name=\"MissingAddresses\" sheetId=\"1\" r:id=\"rId1\"/></sheets>"
                + "</workbook>");

            CreateZipEntry(
                archive,
                "xl/_rels/workbook.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" "
                + "Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" "
                + "Target=\"worksheets/sheet1.xml\"/>"
                + "</Relationships>");

            CreateZipEntry(archive, "xl/worksheets/sheet1.xml", BuildMissingAddressesWorksheetXml(orderedAddresses));

            return outputPath;
        }

        private static string ExportWorksheetRows(
            string outputPath,
            IReadOnlyList<string> columnOrder,
            IReadOnlyDictionary<string, string> headerByColumn,
            IReadOnlyList<WorksheetRowData> rows,
            string sheetName)
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            using FileStream fileStream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            using ZipArchive archive = new ZipArchive(fileStream, ZipArchiveMode.Create);

            CreateZipEntry(
                archive,
                "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
                + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
                + "<Override PartName=\"/xl/workbook.xml\" "
                + "ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"
                + "<Override PartName=\"/xl/worksheets/sheet1.xml\" "
                + "ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"
                + "</Types>");

            CreateZipEntry(
                archive,
                "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" "
                + "Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" "
                + "Target=\"xl/workbook.xml\"/>"
                + "</Relationships>");

            CreateZipEntry(
                archive,
                "xl/workbook.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" "
                + "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"
                + $"<sheets><sheet name=\"{EscapeXml(sheetName)}\" sheetId=\"1\" r:id=\"rId1\"/></sheets>"
                + "</workbook>");

            CreateZipEntry(
                archive,
                "xl/_rels/workbook.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" "
                + "Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" "
                + "Target=\"worksheets/sheet1.xml\"/>"
                + "</Relationships>");

            CreateZipEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheetXml(columnOrder, headerByColumn, rows));

            return outputPath;
        }

        private static bool IsPointInsideAnyBoundary(Point3d point, IEnumerable<Polyline> boundaryPolylines)
        {
            Point2d testPoint = new Point2d(point.X, point.Y);
            foreach (Polyline boundary in boundaryPolylines)
            {
                if (IsPointInsidePolyline(testPoint, boundary))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPointInsidePolyline(Point2d testPoint, Polyline polyline)
        {
            int vertexCount = polyline.NumberOfVertices;
            bool inside = false;

            for (int i = 0, j = vertexCount - 1; i < vertexCount; j = i++)
            {
                Point2d current = polyline.GetPoint2dAt(i);
                Point2d previous = polyline.GetPoint2dAt(j);

                bool intersects = ((current.Y > testPoint.Y) != (previous.Y > testPoint.Y))
                    && (testPoint.X
                        < ((previous.X - current.X) * (testPoint.Y - current.Y) / ((previous.Y - current.Y) + double.Epsilon))
                        + current.X);

                if (intersects)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static bool PolylineHasArcs(Polyline polyline)
        {
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                if (Math.Abs(polyline.GetBulgeAt(i)) > 1e-9)
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureLayerExists(string layerName, Transaction transaction, Database database, CadColor? color)
        {
            LayerTable layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
            if (layerTable.Has(layerName))
            {
                if (color is null)
                {
                    return;
                }

                LayerTableRecord existingLayer =
                    (LayerTableRecord)transaction.GetObject(layerTable[layerName], OpenMode.ForWrite);
                existingLayer.Color = color;
                return;
            }

            layerTable.UpgradeOpen();
            LayerTableRecord layer = new LayerTableRecord
            {
                Name = layerName
            };

            if (color is not null)
            {
                layer.Color = color;
            }

            layerTable.Add(layer);
            transaction.AddNewlyCreatedDBObject(layer, true);
        }

        private static void RemoveExistingMarkers(
            BlockTableRecord modelSpace,
            Transaction transaction,
            IEnumerable<Polyline> boundaryPolylines)
        {
            List<ObjectId> markerIdsToRemove = new List<ObjectId>();

            foreach (ObjectId entityId in modelSpace)
            {
                Entity? entity = transaction.GetObject(entityId, OpenMode.ForRead) as Entity;
                if (entity is null)
                {
                    continue;
                }

                if (!TryGetManagedMarkerCenter(entity, out Point3d center))
                {
                    continue;
                }

                if (!IsPointInsideAnyBoundary(center, boundaryPolylines))
                {
                    continue;
                }

                markerIdsToRemove.Add(entityId);
            }

            foreach (ObjectId markerId in markerIdsToRemove)
            {
                if (transaction.GetObject(markerId, OpenMode.ForWrite) is Entity entity)
                {
                    entity.Erase();
                }
            }
        }

        private static HashSet<string> GetExistingMarkerLocations(BlockTableRecord modelSpace, Transaction transaction)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.Ordinal);

            foreach (ObjectId entityId in modelSpace)
            {
                Entity? entity = transaction.GetObject(entityId, OpenMode.ForRead) as Entity;
                if (entity is null)
                {
                    continue;
                }

                if (TryGetManagedMarkerCenter(entity, out Point3d center))
                {
                    result.Add(CreateLocationKey(center));
                }
            }

            return result;
        }

        private static bool TryAppendMarkerAtLocation(
            BlockTableRecord modelSpace,
            Transaction transaction,
            Polyline marker,
            ISet<string> existingCircleLocations)
        {
            Point3d center = GetMarkerPolylineCenter(marker);
            string locationKey = CreateLocationKey(center);
            if (!existingCircleLocations.Add(locationKey))
            {
                marker.Dispose();
                return false;
            }

            modelSpace.AppendEntity(marker);
            transaction.AddNewlyCreatedDBObject(marker, true);
            return true;
        }

        private static Polyline CreateMarkerPolyline(Point3d center, double radius, string layerName)
        {
            const int vertexCount = 16;
            Polyline polyline = new Polyline(vertexCount)
            {
                Layer = layerName,
                Closed = true
            };

            for (int i = 0; i < vertexCount; i++)
            {
                double angle = (2.0 * Math.PI * i) / vertexCount;
                double x = center.X + (radius * Math.Cos(angle));
                double y = center.Y + (radius * Math.Sin(angle));
                polyline.AddVertexAt(i, new Point2d(x, y), 0.0, 0.0, 0.0);
            }

            return polyline;
        }

        private static Point3d GetMarkerPolylineCenter(Polyline polyline)
        {
            Point3d? center = TryGetMarkerPolylineCenter(polyline);
            if (!center.HasValue)
            {
                throw new InvalidOperationException("Marker polyline center could not be determined.");
            }

            return center.Value;
        }

        private static Point3d? TryGetMarkerPolylineCenter(Polyline polyline)
        {
            if (!polyline.Closed || polyline.NumberOfVertices != 16)
            {
                return null;
            }

            double sumX = 0.0;
            double sumY = 0.0;
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                Point2d point = polyline.GetPoint2dAt(i);
                sumX += point.X;
                sumY += point.Y;
            }

            return new Point3d(sumX / polyline.NumberOfVertices, sumY / polyline.NumberOfVertices, 0.0);
        }

        private static bool TryGetManagedMarkerCenter(Entity entity, out Point3d center)
        {
            center = default;

            if (entity is Circle circle)
            {
                if (Math.Abs(circle.Radius - 5.0) > 1e-6)
                {
                    return false;
                }

                center = circle.Center;
                return true;
            }

            if (entity is not Polyline polyline)
            {
                return false;
            }

            Point3d? polylineCenter = TryGetMarkerPolylineCenter(polyline);
            if (!polylineCenter.HasValue)
            {
                return false;
            }

            center = polylineCenter.Value;
            return true;
        }

        private static string NormalizeAddress(string address)
        {
            string[] parts = address
                .Trim()
                .Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

            return string.Join(" ", parts).ToUpperInvariant();
        }

        private static string SanitizeLayerName(string layerName)
        {
            char[] invalidChars = { '<', '>', '/', '\\', '"', ':', ';', '?', '*', '|', '=', ',' };
            string sanitized = layerName.Trim();
            foreach (char invalidChar in invalidChars)
            {
                sanitized = sanitized.Replace(invalidChar, '_');
            }

            return string.IsNullOrWhiteSpace(sanitized) ? "UNKNOWN_DISTRICT" : sanitized;
        }

        private static string CreateLocationKey(Point3d point)
        {
            return $"{Math.Round(point.X, 6):0.######}|{Math.Round(point.Y, 6):0.######}|{Math.Round(point.Z, 6):0.######}";
        }

        private static string ReadZipEntryText(ZipArchive archive, string entryPath)
        {
            ZipArchiveEntry? entry = archive.GetEntry(entryPath);
            if (entry is null)
            {
                throw new InvalidOperationException($"Expected Excel entry '{entryPath}' was not found.");
            }

            using Stream stream = entry.Open();
            using StreamReader reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private static WorksheetData LoadWorksheetData(string excelPath)
        {
            using ZipArchive archive = ZipFile.OpenRead(excelPath);

            Dictionary<string, string> sharedStrings = LoadSharedStrings(archive);
            string workbookXml = ReadZipEntryText(archive, "xl/workbook.xml");
            XDocument workbookDocument = XDocument.Parse(workbookXml);
            string firstWorksheetPath = ResolveFirstWorksheetPath(archive, workbookDocument);
            string worksheetXml = ReadZipEntryText(archive, firstWorksheetPath);
            XDocument worksheetDocument = XDocument.Parse(worksheetXml);

            Dictionary<string, Dictionary<string, string>> rows = LoadWorksheetRows(worksheetDocument, sharedStrings);
            if (rows.Count == 0)
            {
                return new WorksheetData(new List<string>(), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), new List<WorksheetRowData>());
            }

            KeyValuePair<string, Dictionary<string, string>> headerEntry = rows
                .OrderBy(kvp => GetRowNumber(kvp.Key))
                .First();
            if (headerEntry.Value.Count == 0)
            {
                return new WorksheetData(new List<string>(), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), new List<WorksheetRowData>());
            }

            List<string> orderedColumns = headerEntry.Value.Keys
                .OrderBy(GetColumnIndex)
                .ToList();
            Dictionary<string, string> headerByColumn = orderedColumns
                .ToDictionary(column => column, column => GetRowCellValue(headerEntry.Value, column), StringComparer.OrdinalIgnoreCase);

            List<WorksheetRowData> dataRows = rows
                .OrderBy(kvp => GetRowNumber(kvp.Key))
                .Skip(1)
                .Select(kvp => new WorksheetRowData(kvp.Key, CopyRowValuesForColumns(kvp.Value, orderedColumns)))
                .ToList();

            return new WorksheetData(orderedColumns, headerByColumn, dataRows);
        }

        private static Dictionary<string, string> LoadSharedStrings(ZipArchive archive)
        {
            ZipArchiveEntry? entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry is null)
            {
                return new Dictionary<string, string>();
            }

            XDocument document = XDocument.Parse(ReadZipEntryText(archive, "xl/sharedStrings.xml"));
            Dictionary<string, string> result = new Dictionary<string, string>();
            int index = 0;
            foreach (XElement sharedString in document.Descendants(SpreadsheetMlNs + "si"))
            {
                string value = string.Concat(
                    sharedString
                        .Descendants(SpreadsheetMlNs + "t")
                        .Select(x => x.Value));
                result[index.ToString()] = value;
                index++;
            }

            return result;
        }

        private static string ResolveFirstWorksheetPath(ZipArchive archive, XDocument workbookDocument)
        {
            XElement firstSheet = workbookDocument
                .Descendants(SpreadsheetMlNs + "sheet")
                .FirstOrDefault()
                ?? throw new InvalidOperationException("No worksheets were found in the workbook.");

            XAttribute? relationshipIdAttribute = firstSheet.Attribute(OfficeDocumentRelationshipsNs + "id");
            if (relationshipIdAttribute is null || string.IsNullOrWhiteSpace(relationshipIdAttribute.Value))
            {
                throw new InvalidOperationException("The first worksheet relationship could not be resolved.");
            }

            XDocument workbookRelationships = XDocument.Parse(ReadZipEntryText(archive, "xl/_rels/workbook.xml.rels"));
            XElement relationship = workbookRelationships
                .Descendants(PackageRelationshipsNs + "Relationship")
                .FirstOrDefault(x => string.Equals((string?)x.Attribute("Id"), relationshipIdAttribute.Value, StringComparison.Ordinal))
                ?? throw new InvalidOperationException("The first worksheet target could not be resolved.");

            string target = (string?)relationship.Attribute("Target")
                ?? throw new InvalidOperationException("The worksheet target path was missing.");

            return NormalizeWorkbookRelativePath(target);
        }

        private static string NormalizeWorkbookRelativePath(string target)
        {
            string normalized = target.Replace('\\', '/');
            if (normalized.StartsWith("/", StringComparison.Ordinal))
            {
                return normalized.TrimStart('/');
            }

            return $"xl/{normalized.TrimStart('/')}";
        }

        private static Dictionary<string, Dictionary<string, string>> LoadWorksheetRows(
            XDocument worksheetDocument,
            IReadOnlyDictionary<string, string> sharedStrings)
        {
            Dictionary<string, Dictionary<string, string>> result =
                new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            foreach (XElement rowElement in worksheetDocument.Descendants(SpreadsheetMlNs + "row"))
            {
                string rowKey = (string?)rowElement.Attribute("r") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(rowKey))
                {
                    continue;
                }

                Dictionary<string, string> rowValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (XElement cellElement in rowElement.Elements(SpreadsheetMlNs + "c"))
                {
                    string cellReference = (string?)cellElement.Attribute("r") ?? string.Empty;
                    string columnName = GetColumnName(cellReference);
                    if (string.IsNullOrWhiteSpace(columnName))
                    {
                        continue;
                    }

                    rowValues[columnName] = ReadWorksheetCellValue(cellElement, sharedStrings);
                }

                result[rowKey] = rowValues;
            }

            return result;
        }

        private static string ReadWorksheetCellValue(XElement cellElement, IReadOnlyDictionary<string, string> sharedStrings)
        {
            string? cellType = (string?)cellElement.Attribute("t");
            if (string.Equals(cellType, "inlineStr", StringComparison.OrdinalIgnoreCase))
            {
                return string.Concat(cellElement.Descendants(SpreadsheetMlNs + "t").Select(x => x.Value));
            }

            XElement? valueElement = cellElement.Element(SpreadsheetMlNs + "v");
            if (valueElement is null)
            {
                return string.Empty;
            }

            string rawValue = valueElement.Value;
            if (string.Equals(cellType, "s", StringComparison.OrdinalIgnoreCase)
                && sharedStrings.TryGetValue(rawValue, out string? sharedStringValue))
            {
                return sharedStringValue;
            }

            return rawValue;
        }

        private static string GetRowCellValue(IReadOnlyDictionary<string, string> row, string columnName)
        {
            return row.TryGetValue(columnName, out string? value) ? value : string.Empty;
        }

        private static bool TryGetAddressFromWorksheetRow(WorksheetRowData row, out string normalizedAddress)
        {
            string streetName = GetRowCellValue(row.CellsByColumn, "D");
            string streetNumber = GetRowCellValue(row.CellsByColumn, "E");
            if (string.IsNullOrWhiteSpace(streetName) || string.IsNullOrWhiteSpace(streetNumber))
            {
                normalizedAddress = string.Empty;
                return false;
            }

            normalizedAddress = NormalizeAddress($"{streetName} {streetNumber}");
            return !string.IsNullOrWhiteSpace(normalizedAddress);
        }

        private static string GetColumnName(string? cellReference)
        {
            if (string.IsNullOrWhiteSpace(cellReference))
            {
                return string.Empty;
            }

            return new string(cellReference.TakeWhile(char.IsLetter).ToArray());
        }

        private static int GetColumnIndex(string columnName)
        {
            int result = 0;
            foreach (char ch in columnName.ToUpperInvariant())
            {
                if (ch < 'A' || ch > 'Z')
                {
                    continue;
                }

                result = (result * 26) + (ch - 'A' + 1);
            }

            return result;
        }

        private static int GetRowNumber(string rowReference)
        {
            string digits = new string(rowReference.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out int rowNumber) ? rowNumber : int.MaxValue;
        }

        private static void CreateZipEntry(ZipArchive archive, string path, string contents)
        {
            ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);
            using Stream stream = entry.Open();
            using StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(contents);
        }

        private static string BuildMissingAddressesWorksheetXml(IReadOnlyList<string> addresses)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            builder.Append("<sheetData>");
            builder.Append("<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Adresse</t></is></c></row>");

            for (int i = 0; i < addresses.Count; i++)
            {
                int rowNumber = i + 2;
                string escapedValue = SecurityElement.Escape(addresses[i]) ?? string.Empty;
                builder.Append($"<row r=\"{rowNumber}\"><c r=\"A{rowNumber}\" t=\"inlineStr\"><is><t>{escapedValue}</t></is></c></row>");
            }

            builder.Append("</sheetData>");
            builder.Append("</worksheet>");
            return builder.ToString();
        }

        private static string BuildWorksheetXml(
            IReadOnlyList<string> columnOrder,
            IReadOnlyDictionary<string, string> headerByColumn,
            IReadOnlyList<WorksheetRowData> rows)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            builder.Append("<sheetData>");

            builder.Append("<row r=\"1\">");
            for (int i = 0; i < columnOrder.Count; i++)
            {
                string columnName = columnOrder[i];
                string cellReference = $"{columnName}1";
                builder.Append(BuildInlineStringCellXml(cellReference, GetRowCellValue(headerByColumn, columnName)));
            }
            builder.Append("</row>");

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                int worksheetRowNumber = rowIndex + 2;
                builder.Append($"<row r=\"{worksheetRowNumber}\">");
                foreach (string columnName in columnOrder)
                {
                    string cellReference = $"{columnName}{worksheetRowNumber}";
                    string value = GetRowCellValue(rows[rowIndex].CellsByColumn, columnName);
                    builder.Append(BuildInlineStringCellXml(cellReference, value));
                }
                builder.Append("</row>");
            }

            builder.Append("</sheetData>");
            builder.Append("</worksheet>");
            return builder.ToString();
        }

        private static string BuildInlineStringCellXml(string cellReference, string value)
        {
            return $"<c r=\"{cellReference}\" t=\"inlineStr\"><is><t>{EscapeXml(value)}</t></is></c>";
        }

        private static string EscapeXml(string value)
        {
            return SecurityElement.Escape(value) ?? string.Empty;
        }

        private static Dictionary<string, string> CopyRowValuesForColumns(
            IReadOnlyDictionary<string, string> source,
            IReadOnlyList<string> columns)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string column in columns)
            {
                result[column] = GetRowCellValue(source, column);
            }

            return result;
        }

        private sealed class WorksheetData
        {
            public WorksheetData(
                IReadOnlyList<string> headers,
                IReadOnlyDictionary<string, string> headerByColumn,
                IReadOnlyList<WorksheetRowData> rows)
            {
                ColumnOrder = headers;
                HeaderByColumn = headerByColumn;
                Rows = rows;
            }

            public IReadOnlyList<string> ColumnOrder { get; }

            public IReadOnlyDictionary<string, string> HeaderByColumn { get; }

            public IReadOnlyList<WorksheetRowData> Rows { get; }
        }

        private sealed class WorksheetRowData
        {
            public WorksheetRowData(string rowReference, IReadOnlyDictionary<string, string> cellsByColumn)
            {
                RowReference = rowReference;
                CellsByColumn = cellsByColumn;
            }

            public string RowReference { get; }

            public IReadOnlyDictionary<string, string> CellsByColumn { get; }
        }
    }
}
