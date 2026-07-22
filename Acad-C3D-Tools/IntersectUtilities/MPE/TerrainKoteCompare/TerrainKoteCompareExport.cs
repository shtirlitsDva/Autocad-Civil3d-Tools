using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.MPE.Shared;
using AcEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using CadColor = Autodesk.AutoCAD.Colors.Color;

namespace IntersectUtilities.MPE.TerrainKoteCompare
{
    internal static class TerrainKoteCompareLabelBaker
    {
        // Writes "<number>  <±difference>" next to every visible point, on a layer per
        // classification. Existing labels on those layers are erased first so re-running is
        // idempotent — same pattern as LERCompareTerrain's ClearExistingExportedGeometry.
        public static int CreateLabels(
            Document document,
            TerrainKoteCompareResult result,
            ISet<TerrainKoteCompareClassification> visibleClassifications,
            double textHeight,
            double markerSize,
            TerrainKoteCompareValueMode valueMode)
        {
            List<TerrainKoteCompareResultPoint> visiblePoints = result.Points
                .Where(point => visibleClassifications.Contains(point.Classification))
                .OrderBy(point => point.Number)
                .ToList();

            List<string> layerNames = TerrainKoteCompareLayerNames.AllLayerNames().ToList();

            using DocumentLock documentLock = document.LockDocument();
            using Transaction tx = document.Database.TransactionManager.StartTransaction();

            Database database = document.Database;

            foreach (TerrainKoteCompareClassification classification in Enum.GetValues<TerrainKoteCompareClassification>())
            {
                EnsureLayerExists(
                    TerrainKoteCompareLayerNames.GetLayerName(classification),
                    tx,
                    database,
                    TerrainKoteCompareColors.GetCadColor(classification));
            }

            BlockTable blockTable = (BlockTable)tx.GetObject(database.BlockTableId, OpenMode.ForRead);
            BlockTableRecord modelSpace = (BlockTableRecord)tx.GetObject(
                blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            ClearExistingLabels(modelSpace, tx, layerNames);

            double height = textHeight > 0.0 ? textHeight : 0.5;
            double marker = markerSize > 0.0 ? markerSize : 0.5;
            int created = 0;

            // One MText per point (number over value), positioned by the same helper the transient
            // preview uses, so what "Show" draws is exactly what lands here minus the marker circle.
            foreach (TerrainKoteCompareResultPoint point in visiblePoints)
            {
                string layerName = TerrainKoteCompareLayerNames.GetLayerName(point.Classification);

                MText label = new MText
                {
                    Location = TerrainKoteCompareTextLayout.LabelPosition(point.Position, marker),
                    TextHeight = height,
                    Attachment = AttachmentPoint.BottomLeft,
                    Contents = point.FormatLabelContents(valueMode),
                    Layer = layerName,
                    Color = CadColor.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByLayer, 256)
                };

                modelSpace.AppendEntity(label);
                tx.AddNewlyCreatedDBObject(label, true);
                created++;
            }

            tx.Commit();
            return created;
        }

        private static void EnsureLayerExists(string layerName, Transaction transaction, Database database, CadColor color)
        {
            LayerTable layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
            if (layerTable.Has(layerName))
            {
                LayerTableRecord existingLayer = (LayerTableRecord)transaction.GetObject(layerTable[layerName], OpenMode.ForWrite);
                existingLayer.Color = color;
                return;
            }

            layerTable.UpgradeOpen();
            LayerTableRecord layer = new LayerTableRecord
            {
                Name = layerName,
                Color = color
            };

            layerTable.Add(layer);
            transaction.AddNewlyCreatedDBObject(layer, true);
        }

        private static void ClearExistingLabels(
            BlockTableRecord modelSpace,
            Transaction transaction,
            IEnumerable<string> layerNames)
        {
            HashSet<string> targetLayers = new HashSet<string>(layerNames, StringComparer.OrdinalIgnoreCase);
            foreach (ObjectId entityId in modelSpace)
            {
                if (transaction.GetObject(entityId, OpenMode.ForRead, false) is not AcEntity entity)
                {
                    continue;
                }

                if (!targetLayers.Contains(entity.Layer))
                {
                    continue;
                }

                entity.UpgradeOpen();
                entity.Erase();
            }
        }
    }

    internal static class TerrainKoteCompareExcelExport
    {
        private static readonly string[] Headers =
        {
            "Nr",
            "Handle",
            "X",
            "Y",
            "Kote opmålt",
            "Kote terræn",
            "Forskel",
            "Klassifikation",
            "Flade",
            "Fil",
            "Flere flader"
        };

        public static int Export(
            string outputPath,
            TerrainKoteCompareResult result,
            ISet<TerrainKoteCompareClassification> visibleClassifications)
        {
            List<IReadOnlyList<object?>> rows = result.Points
                .Where(point => visibleClassifications.Contains(point.Classification))
                .OrderBy(point => point.Number)
                .Select(BuildRow)
                .ToList();

            XlsxWriter.Write(outputPath, "Terrænkoter", Headers, rows);
            return rows.Count;
        }

        private static IReadOnlyList<object?> BuildRow(TerrainKoteCompareResultPoint point)
        {
            return new object?[]
            {
                point.Number,
                point.Source.Handle,
                point.Position.X,
                point.Position.Y,
                point.Source.SurveyElevation,
                point.TerrainElevation,
                point.Difference,
                DescribeClassification(point.Classification),
                point.SurfaceName,
                point.SourceFile,
                point.CoveringSurfaceCount > 1 ? point.CoveringSurfaceCount.ToString(CultureInfo.InvariantCulture) : string.Empty
            };
        }

        public static string DescribeClassification(TerrainKoteCompareClassification classification)
        {
            return classification switch
            {
                TerrainKoteCompareClassification.Above => "Punkt over terræn",
                TerrainKoteCompareClassification.Below => "Punkt under terræn",
                TerrainKoteCompareClassification.NoHeight => "Ingen kote",
                _ => "Uden for flade"
            };
        }
    }
}
