using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.UtilsCommon;

namespace IntersectUtilities.MPE.MatchBBR
{
    internal sealed class CircleExportResult
    {
        public CircleExportResult(int drawn, int erased, int skipped)
        {
            Drawn = drawn;
            Erased = erased;
            Skipped = skipped;
        }

        public int Drawn { get; }

        // Circles from an earlier export that were replaced by this one.
        public int Erased { get; }

        // Rows with no block to draw around — Excel-only rows.
        public int Skipped { get; }
    }

    // Writes the comparison result into the drawing as real circles, one per BBR block, on a
    // layer named after the outcome.
    //
    // This is the permanent counterpart to MatchBbrPreview: the preview answers "where are they
    // right now", this produces geometry that survives the session and can be plotted, xrefed or
    // handed to someone without the tool.
    //
    // Every circle is stamped with XData under MPE_MATCHBBR, and a re-export erases only stamped
    // entities. That matters: the original MATCHBBR identified its own markers *geometrically*
    // (any circle of radius ~5, or any closed 16-vertex polyline, on any layer), so it deleted
    // unrelated geometry — including a boundary polyline that happened to have 16 vertices. A tag
    // cannot misfire that way.
    //
    // The caller owns the document lock and the transaction, so the erase and the redraw land as
    // one AutoCAD undo step.
    internal static class MatchBbrCircleExport
    {
        public const string XDataAppName = "MPE_MATCHBBR";

        public static CircleExportResult Draw(Database database, IEnumerable<ComparisonRow> rows)
        {
            List<ComparisonRow> ordered = rows.ToList();

            EnsureRegApp(database);

            int erased = EraseExisting(database);
            int drawn = 0;
            int skipped = 0;

            // Layers are created up front, once per status actually in play, rather than once per
            // circle — CheckOrCreateLayer opens the layer table for write every time it is asked.
            foreach (MatchStatus status in ordered
                .Where(r => r.BbrRecord is not null)
                .Select(r => r.Status)
                .Distinct())
            {
                database.CheckOrCreateLayer(LayerFor(status), ColorFor(status));
            }

            Transaction tx = database.TransactionManager.TopTransaction;
            BlockTableRecord space = database.GetModelspaceForWrite();

            foreach (ComparisonRow row in ordered)
            {
                if (row.BbrRecord is null)
                {
                    skipped++;
                    continue;
                }

                // The circle is drawn flat at the block's elevation, not at Z=0: a BBR block sits
                // on terrain in these drawings, and a marker at zero would float away from it in
                // any 3D view.
                Circle circle = new Circle(
                    row.BbrRecord.Position,
                    Vector3d.ZAxis,
                    MatchBbrConstants.MarkerRadius)
                {
                    Layer = LayerFor(row.Status),

                    // ByLayer, so freezing or recolouring a status is a layer operation.
                    Color = Color.FromColorIndex(ColorMethod.ByLayer, 256),
                };

                space.AppendEntity(circle);
                tx.AddNewlyCreatedDBObject(circle, true);

                // The status and the block handle travel with the circle, so a drawing that has
                // been passed on still says what each marker meant and which block it belongs to.
                circle.XData = new ResultBuffer(
                    new TypedValue((int)DxfCode.ExtendedDataRegAppName, XDataAppName),
                    new TypedValue((int)DxfCode.ExtendedDataAsciiString, row.Status.ToString()),
                    new TypedValue(
                        (int)DxfCode.ExtendedDataAsciiString,
                        row.BbrRecord.Handle.ToString()));

                drawn++;
            }

            return new CircleExportResult(drawn, erased, skipped);
        }

        // Erases every entity this tool has previously stamped, so exporting twice replaces the
        // markers instead of stacking a second set on top of the first.
        public static int EraseExisting(Database database)
        {
            Transaction tx = database.TransactionManager.TopTransaction;
            BlockTableRecord space = database.GetModelspaceForWrite();

            int erased = 0;

            // Materialized first: erasing while enumerating the block table record is not safe.
            List<ObjectId> ids = space.Cast<ObjectId>().ToList();

            foreach (ObjectId id in ids)
            {
                if (tx.GetObject(id, OpenMode.ForRead) is not Entity entity)
                {
                    continue;
                }

                using ResultBuffer? buffer = entity.GetXDataForApplication(XDataAppName);
                if (buffer is null)
                {
                    continue;
                }

                entity.UpgradeOpen();
                entity.Erase();
                erased++;
            }

            return erased;
        }

        private static void EnsureRegApp(Database database)
        {
            Transaction tx = database.TransactionManager.TopTransaction;
            RegAppTable table = database.RegAppTableId.Go<RegAppTable>(tx)!;

            if (table.Has(XDataAppName))
            {
                return;
            }

            table.CheckOrOpenForWrite();
            RegAppTableRecord record = new RegAppTableRecord { Name = XDataAppName };
            table.Add(record);
            tx.AddNewlyCreatedDBObject(record, true);
        }

        // One layer per outcome, so the result can be read with the layer manager alone: freeze
        // the matches and only the problems are left on screen.
        //
        // KunIExcel has no block and therefore no layer — those rows are counted as skipped.
        private static string LayerFor(MatchStatus status) =>
            status switch
            {
                MatchStatus.Match => "BBR_MATCH",
                MatchStatus.Afvigelse => "BBR_AFVIGELSE",
                MatchStatus.KunITegning => "BBR_KUN_I_TEGNING",
                MatchStatus.Dublet => "BBR_DUBLET",
                _ => "BBR_MARKERING",
            };

        // The same ACI colours the transient preview uses, so turning a preview into geometry
        // does not change what you are looking at.
        private static short ColorFor(MatchStatus status) =>
            status switch
            {
                MatchStatus.Match => 3,
                MatchStatus.Afvigelse => 2,
                MatchStatus.KunITegning => 1,
                MatchStatus.Dublet => 6,
                _ => 7,
            };
    }
}
