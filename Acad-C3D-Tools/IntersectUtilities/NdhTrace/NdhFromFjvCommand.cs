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
        /// Sporer alle eksisterende fjernvarmerørledninger i den xref'ede FJV-tegning og
        /// opretter en ny NDH-rørledning for hver af dem i den aktuelle tegning.
        ///
        /// Rørledningens rute er den eksakte centerlinje fra DRAWFJVCL. System, type og
        /// dimension langs ruten tages fra rørledningens størrelsesarray (PipelineSizeArrayV2).
        /// Buer bliver til bøjninger med buens radius; skarpe knæk bliver kun til
        /// knæ, hvor den gamle tegning har en bøjningskomponent eller et F-rør, ellers til
        /// en elastisk bøjning. Et skift af dimension eller type der falder i en bøjning
        /// flyttes ud på et lige stykke.
        ///
        /// Kræver at NDH-modulet (NSNorsynDistrictHeating.dbx) er indlæst. Til sidst
        /// rapporteres hvilke rørledninger der blev oprettet, afvist eller justeret.
        /// </summary>
        /// <category>Fjernvarme Fremtidig</category>
        [CommandMethod("NDHFROMFJV")]
        public void ndhfromfjv()
        {
            Database localDb = Application.DocumentManager.MdiActiveDocument.Database;

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

            NdhImportReport report;
            try
            {
                //No transaction of ours may be open here: every build opens and
                //closes the working drawing's model space itself.
                report = NdhFromFjvImport.Run(fjvPath, new NsDhPipelineBridge());
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                return;
            }

            prdDbg($"NDHFROMFJV: {report.Created.Count} af {report.LegacyPipelineCount} " +
                $"rørledninger oprettet fra {fjvPath}.");
            if (report.Skipped.Count > 0)
            {
                prdDbg($"Ikke sporet ({report.Skipped.Count}):");
                foreach (string s in report.Skipped) prdDbg("  " + s);
            }
            if (report.Refused.Count > 0)
            {
                prdDbg($"Afvist ({report.Refused.Count}):");
                foreach (string s in report.Refused) prdDbg("  " + s);
            }
            if (report.Adjusted.Count > 0)
            {
                prdDbg($"Justeret ({report.Adjusted.Count}):");
                foreach (string s in report.Adjusted) prdDbg("  " + s);
            }
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
                BlockTable bt = localDb.BlockTableId.Go<BlockTable>(tx);
                foreach (ObjectId id in bt)
                {
                    BlockTableRecord btr = id.Go<BlockTableRecord>(tx);
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
