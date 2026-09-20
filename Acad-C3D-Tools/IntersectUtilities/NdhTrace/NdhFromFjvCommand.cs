using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

using IntersectUtilities.NdhTrace;
using IntersectUtilities.UtilsCommon;

using System;
using System.Collections.Generic;
using System.Linq;

using static IntersectUtilities.UtilsCommon.Utils;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        /// <command>NDHFROMFJV</command>
        /// <summary>
        /// Oversætter den xref'ede FJV-tegning ord for ord til NDH: en NDH-rørledning for
        /// hver gammel rørledning, og hver gammel afgrening som en NDH-forbindelse med den
        /// oversatte del fastlåst.
        ///
        /// Rørledningens rute er den eksakte centerlinje fra DRAWFJVCL. System, type og
        /// dimension langs ruten tages fra rørledningens størrelsesarray (PipelineSizeArrayV2).
        /// Buer bliver til bøjninger med buens radius; skarpe knæk bliver kun til
        /// knæ, hvor den gamle tegning har en bøjningskomponent eller et F-rør, ellers til
        /// en elastisk bøjning. Et skift af dimension eller type der falder i en bøjning
        /// flyttes ud på et lige stykke. Rørledninger der mødes ende mod ende uden
        /// afgreningsdel lægges sammen til én, opkaldt efter den der er nærmest nettets rod.
        ///
        /// Tegningens producent og seriematrix sættes fra den gamle tegning; hvor den er
        /// tvetydig, vælges i en dialog. En afgrening der ikke står vinkelret rettes med
        /// mindst mulig flytning af afgreningen (over 3° med en gul MLeader). Hvad der ikke
        /// kan forbindes, markeres med en gul cirkel og en note på laget NDH-IMPORT-NOTE.
        ///
        /// Kræver at NDH-modulet (NSNorsynDistrictHeating.dbx) er indlæst i den version,
        /// denne build forventer. Til sidst rapporteres oprettet, sammenlagt, forbundet,
        /// justeret, sprunget over og afvist, samt producent og serier. Én UNDO fortryder
        /// hele importen.
        /// </summary>
        /// <category>Fjernvarme Fremtidig</category>
        [CommandMethod("NDHFROMFJV")]
        public void ndhfromfjv()
        {
            Database localDb = Application.DocumentManager.MdiActiveDocument.Database;

            //Refuse before anything is read or written when the module is
            //missing or speaks another version of any surface the import uses.
            try
            {
                foreach (NsDhSurface surface in NsDhSurface.UsedByImport) NsDhModule.Verify(surface);
            }
            catch (InvalidOperationException ex)
            {
                prdDbg($"NDHFROMFJV: {ex.Message}");
                return;
            }

            string? fjvPath;
            try
            {
                fjvPath = PickFjvXref(localDb);
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                return;
            }
            if (fjvPath == null) return;

            //The command owns the report, so an import that throws half way
            //still says what it already built and connected before it says
            //that it stopped (review of #319, I2).
            NdhImportReport report = new NdhImportReport();
            try
            {
                //No transaction of ours may be open here: every NDH export opens
                //and closes the working drawing's objects itself. Everything
                //happens inside this one command, so one UNDO takes it all back.
                NsDhPipelineBridge ndh = new NsDhPipelineBridge();
                NdhFromFjvImport.Run(fjvPath, new NdhImportServices(
                    ndh,
                    ndh,
                    new NsDhConnectionBridge(),
                    new NsDhSettingsBridge(),
                    new WpfImportDialogs(),
                    new AcadImportMarkers(localDb)), report);
            }
            catch (System.Exception ex)
            {
                //Nothing to say before the legacy drawing was read.
                if (report.LegacyPipelineCount > 0) PrintNdhImportReport(report, fjvPath);
                prdDbg($"NDHFROMFJV stoppede: {ex.Message}");
                prdDbg(ex);
                return;
            }

            PrintNdhImportReport(report, fjvPath);
            PrintNdhComplaints(report);
        }

        /// <summary>
        /// Prints what NDH says about the pipelines the import built.
        ///
        /// Read inline, because NDH settles both ledgers before it answers.
        /// This used to wait for the first <c>Application.Idle</c> after the
        /// command: a branch's own runs were not re-planned by the time
        /// <c>NsDh_ConnectBranch</c> returned, so pipeline 059 read no
        /// complaints inside the command and one the moment it was over
        /// (2026-09-20), and a <c>Regen</c> did not settle it either. NDH now
        /// re-derives the CHILD as well as the main as step (5) of a connect,
        /// and the inline read matches the deferred one - 10 and 10.
        /// </summary>
        private static void PrintNdhComplaints(NdhImportReport report)
        {
            if (report.Cancelled != null || report.Built.Count == 0) return;
            try
            {
                PrintNdhSection("Bemærkninger fra NDH",
                    NdhFromFjvImport.Complaints(report.Built, new NsDhIssuesBridge()));
            }
            catch (System.Exception ex)
            {
                prdDbg($"NDHFROMFJV: NDH's bemærkninger kunne ikke læses: {ex.Message}");
            }
        }

        private static void PrintNdhImportReport(NdhImportReport report, string fjvPath)
        {
            if (report.Cancelled != null)
            {
                prdDbg($"NDHFROMFJV: {report.Cancelled}");
                return;
            }

            prdDbg($"NDHFROMFJV: {report.Created.Count} rørledninger oprettet af " +
                $"{report.LegacyPipelineCount} gamle fra {fjvPath}.");
            PrintNdhSection("Oprettet", report.Created);
            PrintNdhSection("Sammenlagt", report.Merged);
            PrintNdhSection("Forbundet", report.Connected);
            PrintNdhSection("Justeret", report.Adjusted);
            PrintNdhSection("Sprunget over", report.Skipped);
            PrintNdhSection("Afvist", report.Refused);
            PrintNdhSection("Producent", report.ProducerReport);
            PrintNdhSection("Serierapport", report.SeriesReport);
            if (report.MarkersPlaced > 0)
                prdDbg($"{report.MarkersPlaced} markeringer ligger på laget {AcadImportMarkers.Layer}.");
        }

        private static void PrintNdhSection(string title, List<string> lines)
        {
            if (lines.Count == 0) return;
            prdDbg($"{title} ({lines.Count}):");
            foreach (string s in lines) prdDbg("  " + s);
        }

        /// <summary>
        /// The file of the resolved xref that holds FJV pipes; asks when there are
        /// several, null when there is none or the user cancels.
        /// </summary>
        private static string? PickFjvXref(Database localDb)
        {
            List<string> paths = new List<string>();
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tx.GetObject(localDb.BlockTableId, OpenMode.ForRead);
                foreach (ObjectId id in bt)
                {
                    BlockTableRecord btr = (BlockTableRecord)tx.GetObject(id, OpenMode.ForRead);
                    if (!btr.IsFromExternalReference || !btr.IsResolved) continue;

                    //false: an unloaded xref is not loaded for this.
                    Database xDb = btr.GetXrefDatabase(false);
                    if (xDb == null) continue;

                    using Transaction xTx = xDb.TransactionManager.StartTransaction();
                    if (xDb.GetFjvPipes(xTx).Count > 0 &&
                        !paths.Contains(xDb.Filename, StringComparer.OrdinalIgnoreCase))
                        paths.Add(xDb.Filename);
                    xTx.Abort();
                }
                tx.Abort();
            }

            if (paths.Count == 0)
            {
                prdDbg("Ingen xref med fjernvarmerør fundet.");
                return null;
            }
            if (paths.Count == 1) return paths[0];
            return StringGridFormCaller.Call(paths, "Vælg FJV-tegningen der skal spores:");
        }
    }
}
