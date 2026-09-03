using System;
using System.Collections.Generic;
using System.IO;

using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using PSv2 = IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

namespace IntersectUtilities.MPE.SplitStik
{
    /// <summary>
    /// Writes a set of <see cref="PipeRun"/> to a standalone .dwg. Mirrors
    /// DimensioneringV2's AutoCadDrawingSink.WriteDrafterCore so the two exports produce
    /// identical layers, widths, linetypes and XData — only the polyline count differs,
    /// because SPLITSTIK merges same-dimension edges into runs and the drafter does not.
    /// </summary>
    internal static class SplitStikWriter
    {
        /// <summary>
        /// RegApp read by NorsynDistrictZones\Acad\PipeReader.cs (PipeReader.PipeIdentityApp).
        /// The name and the value order below are a contract with that reader — keep verbatim.
        /// </summary>
        private const string NhsPipeXDataApp = "NORSYN_NHS_PIPE";

        private const string StikledningTag = "Stikledning";
        private const string FordelingsledningTag = "Fordelingsledning";

        internal static int Write(string path, IReadOnlyList<PipeRun> runs, SplitStikStats stats)
        {
            using Database db = new(true, true);
            db.Insunits = UnitsValue.Meters;
            db.Measurement = MeasurementValue.Metric;

            SetStandardTextStyle(db);
            EnsureLineTypes(db, runs);

            int written;
            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                try
                {
                    written = WriteRuns(db, tx, runs, stats);
                }
                catch (System.Exception)
                {
                    tx.Abort();
                    throw;
                }
                tx.Commit();
            }

            // SaveAs overwrites an existing file silently, which is what we want. Do NOT guess at
            // the cause when it fails: report what actually went wrong, then offer the likely cause
            // as a hint. SaveAs writes straight onto the target, so an existing file may also have
            // been left partly overwritten — the user has to be told that before they open it.
            try
            {
                db.SaveAs(path, DwgVersion.Current);
            }
            catch (System.Exception ex)
            {
                bool existed = File.Exists(path);
                throw new InvalidOperationException(
                    $"Could not save \"{path}\".\n"
                        + $"{ex.GetType().Name}: {ex.Message}\n"
                        + (existed
                            ? "A file exists at that path and may now be INCOMPLETE — check it "
                                + "before using it. If it is open in AutoCAD or is read-only, "
                                + "close it and run SPLITSTIK again."
                            : "No file was created at that path."),
                    ex);
            }

            return written;
        }

        private static void SetStandardTextStyle(Database db)
        {
            using Transaction tx = db.TransactionManager.StartTransaction();
            TextStyleTable? tst = db.TextStyleTableId.Go<TextStyleTable>(tx);
            if (tst is not null && tst.Has("Standard"))
            {
                TextStyleTableRecord? tsr =
                    tst["Standard"].Go<TextStyleTableRecord>(tx, OpenMode.ForWrite);
                if (tsr is not null) tsr.FileName = "arial.ttf";
            }
            tx.Commit();
        }

        /// <summary>
        /// Created up front, in its own transactions, because createltmethod starts a
        /// transaction of its own and opens the LinetypeTable for write — doing that while
        /// the main write transaction already holds the table open invites a lock conflict.
        /// </summary>
        private static void EnsureLineTypes(Database db, IReadOnlyList<PipeRun> runs)
        {
            HashSet<string> existing = new(StringComparer.OrdinalIgnoreCase);
            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                LinetypeTable? ltt = db.LinetypeTableId.Go<LinetypeTable>(tx);
                if (ltt is not null)
                    foreach (Oid id in ltt)
                        existing.Add(id.Go<LinetypeTableRecord>(tx)!.Name);
                tx.Commit();
            }

            foreach (PipeRun run in runs)
            {
                PipeSystemEnum ps = TranslateFamilyNameToSystem(run.Dim.FamilyName);
                if (ps == PipeSystemEnum.Ukendt) continue; // reported in WriteRuns

                string text = PSv2.GetLineTypeLayerPrefix(ps) + run.Dim.NominalDiameter;
                string name = "LT-" + text;
                if (!existing.Add(name)) continue;

                PlanDetailing.LineTypes.LineTypes.createltmethod(name, text, "Standard", db);
            }
        }

        private static int WriteRuns(
            Database db,
            Transaction tx,
            IReadOnlyList<PipeRun> runs,
            SplitStikStats stats)
        {
            LinetypeTable ltt = db.LinetypeTableId.Go<LinetypeTable>(tx)!;
            EnsureRegApp(db, tx, NhsPipeXDataApp);

            int written = 0;

            foreach (PipeRun run in runs)
            {
                PipeSystemEnum ps = TranslateFamilyNameToSystem(run.Dim.FamilyName);
                if (ps == PipeSystemEnum.Ukendt)
                {
                    stats.SkippedUnknownFamily++;
                    stats.UnknownFamilies.Add(run.Dim.FamilyName);
                    continue;
                }

                int dn = run.Dim.NominalDiameter;
                PipeTypeEnum pt = PSv2.GetPipeTypeByAvailability(ps, dn);
                string layerName = PSv2.GetLayerName(dn, ps, pt);
                CheckOrCreateLayerForPipe(db, tx, layerName, ps, pt);

                double kOd = ResolveKOd(ps, dn, pt, stats);

                Polyline pline = new(run.Vertices.Length);
                pline.SetDatabaseDefaults(db);
                for (int i = 0; i < run.Vertices.Length; i++)
                    pline.AddVertexAt(i, run.Vertices[i], 0, 0, 0);

                pline.AddEntityToDbModelSpace(db);

                pline.Layer = layerName;
                pline.ConstantWidth = kOd / 1000;
                pline.Plinegen = true;

                string lineTypeName = "LT-" + PSv2.GetLineTypeLayerPrefix(ps) + dn;
                if (ltt.Has(lineTypeName)) pline.LinetypeId = ltt[lineTypeName];

                // The exported layer name cannot distinguish FL from SL — AluPEXFL and
                // AluPEXSL both collapse to PipeSystemEnum.AluPex — so the FL/SL role is
                // carried here instead. See docs\PSv2-NHS-translation-research.md.
                pline.XData = new ResultBuffer(
                    new TypedValue((int)DxfCode.ExtendedDataRegAppName, NhsPipeXDataApp),
                    new TypedValue(
                        (int)DxfCode.ExtendedDataAsciiString, run.Dim.FamilyName),
                    new TypedValue(
                        (int)DxfCode.ExtendedDataAsciiString,
                        run.IsServiceLine ? StikledningTag : FordelingsledningTag),
                    new TypedValue((int)DxfCode.ExtendedDataInteger32, dn));

                written++;
            }

            return written;
        }

        /// <summary>
        /// Series to try for the jacket diameter, thickest insulation first.
        ///
        /// S3 is what this export wants, but most systems simply do not stock it: only DN,
        /// FIBREFLEX and PRTPIPE carry Twin/S3 rows. Asking for S3 alone therefore produced a
        /// width of ZERO — GetPipeKOd reports a miss as 0 rather than throwing — for every size
        /// of ALUPEX, AQTHRM11, CU, PE and PRTFLEXL, i.e. five of the eight systems drew as
        /// hairlines. Descending S3 -> S2 -> S1 resolves every size in every system, and leaves
        /// the three that already had S3 rows picking S3 exactly as before.
        /// </summary>
        private static readonly PipeSeriesEnum[] SeriesPreference =
            new[] { PipeSeriesEnum.S3, PipeSeriesEnum.S2, PipeSeriesEnum.S1 };

        /// <summary>
        /// The jacket diameter for this pipe, falling back down the series when the schedule holds
        /// no row for the preferred one. Records which series actually supplied the width whenever
        /// it was not the preferred one, so a width that did not come from S3 is visible in the
        /// report rather than silently assumed.
        /// </summary>
        private static double ResolveKOd(
            PipeSystemEnum ps,
            int dn,
            PipeTypeEnum pt,
            SplitStikStats stats)
        {
            foreach (PipeSeriesEnum series in SeriesPreference)
            {
                double kOd = PSv2.GetPipeKOd(ps, dn, pt, series);
                if (kOd <= 0) continue;

                if (series != SeriesPreference[0])
                    stats.SeriesFallbacks.Add(
                        $"{ps} DN{dn} {pt}: {SeriesPreference[0]} -> {series}");

                return kOd;
            }

            // No series has this pipe at all — a genuine gap between what DimensioneringV2 sized
            // and what the pipe schedule carries.
            stats.RunsWithoutWidth++;
            stats.MissingWidths.Add($"{ps} DN{dn} {pt}");
            return 0;
        }

        private static void EnsureRegApp(Database db, Transaction tx, string appName)
        {
            RegAppTable rat = db.RegAppTableId.Go<RegAppTable>(tx)!;
            if (rat.Has(appName)) return;

            rat.CheckOrOpenForWrite();
            RegAppTableRecord rar = new() { Name = appName };
            rat.Add(rar);
            tx.AddNewlyCreatedDBObject(rar, true);
        }

        private static void CheckOrCreateLayerForPipe(
            Database db,
            Transaction tx,
            string layerName,
            PipeSystemEnum system,
            PipeTypeEnum type)
        {
            LayerTable lt = db.LayerTableId.Go<LayerTable>(tx)!;
            if (lt.Has(layerName)) return;

            LinetypeTable ltt = db.LinetypeTableId.Go<LinetypeTable>(tx)!;

            LayerTableRecord ltr = new()
            {
                Name = layerName,
                Color = Color.FromColorIndex(
                    ColorMethod.ByAci, PSv2.GetLayerColor(system, type)),
                LinetypeObjectId = ltt["Continuous"],
                LineWeight = LineWeight.ByLineWeightDefault,
            };

            lt.CheckOrOpenForWrite();
            lt.Add(ltr);
            tx.AddNewlyCreatedDBObject(ltr, true);
        }

        /// <summary>
        /// NorsynHydraulicShared PipeType (as a string — the .d2r stores PipeType.ToString()
        /// in Dim.Family.Name) to PipeScheduleV2 PipeSystemEnum.
        ///
        /// This is a third copy of a mapping that also lives in DimensioneringV2's
        /// AutoCadDrawingSink.TranslatePipeTypeToSystem and is tabulated in
        /// docs\PSv2-NHS-translation-research.md, which flags it as a consolidation
        /// candidate. It is duplicated rather than shared because IntersectUtilities has no
        /// reference to NorsynHydraulicCalc and so cannot parse the PipeType enum. Keep the
        /// three in step; an unrecognised name means DimensioneringV2 gained a pipe type.
        ///
        /// The mapping deliberately collapses FL/SL — that distinction survives only in the
        /// NORSYN_NHS_PIPE XData, never in the layer name.
        /// </summary>
        internal static PipeSystemEnum TranslateFamilyNameToSystem(string familyName) =>
            familyName switch
            {
                "Stål" => PipeSystemEnum.Stål,
                "PertFlextraFL" or "PertFlextraSL" => PipeSystemEnum.PertFlextra,
                "AluPEXFL" or "AluPEXSL" => PipeSystemEnum.AluPex,
                "Kobber" => PipeSystemEnum.Kobberflex,
                "AquaTherm11" => PipeSystemEnum.AquaTherm11,
                "Pe" => PipeSystemEnum.PE,
                "FibreFlexFL" or "FibreFlexSL" => PipeSystemEnum.FibreFlex,
                _ => PipeSystemEnum.Ukendt,
            };
    }
}
