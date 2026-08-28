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

            // SaveAs overwrites an existing file silently, which is what we want. The one way
            // it can fail is the target already being open in AutoCAD (or read-only), and the
            // raw exception for that reads as a mystery — so name the cause.
            try
            {
                db.SaveAs(path, DwgVersion.Current);
            }
            catch (System.Exception ex) when (File.Exists(path))
            {
                throw new InvalidOperationException(
                    $"Could not overwrite \"{path}\". It is most likely open in AutoCAD or "
                        + "read-only — close it and run SPLITSTIK again.",
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

                Polyline pline = new(run.Vertices.Length);
                pline.SetDatabaseDefaults(db);
                for (int i = 0; i < run.Vertices.Length; i++)
                    pline.AddVertexAt(i, run.Vertices[i], 0, 0, 0);

                pline.AddEntityToDbModelSpace(db);

                pline.Layer = layerName;
                pline.ConstantWidth = PSv2.GetPipeKOd(ps, dn, pt, PipeSeriesEnum.S3) / 1000;
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
