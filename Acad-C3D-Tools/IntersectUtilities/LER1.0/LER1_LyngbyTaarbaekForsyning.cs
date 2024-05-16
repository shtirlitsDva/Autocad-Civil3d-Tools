using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.DataShortcuts;
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;
using Autodesk.Gis.Map.Utilities;
using Autodesk.Aec.PropertyData;
using Autodesk.Aec.PropertyData.DatabaseServices;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Data;
using MoreLinq;
using GroupByCluster;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using Dreambuild.AutoCAD;

using static IntersectUtilities.Enums;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.Utils;
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.UtilsCommon.UtilsODData;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = Autodesk.Civil.DatabaseServices.Label;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;

namespace IntersectUtilities
{
    public partial class Intersect
    {

        [CommandMethod("LTFREADDATA")]
        public void ltfreaddata()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            #region ReadAndParseData
            var kpsRaw = CsvReader.ReadCsvToExpando(
                    @"X:\117-1398 - VF - Program 2 (LY1 og GL1) - Dokumenter\01 Intern\02 Tegninger\" +
                    @"04 Modtaget\7.28.1a\2024.05.15 - Afl data\Knuder_Virum_SBP_2024.05.14.csv");
            var lednsRaw = CsvReader.ReadCsvToExpando(
                @"X:\117-1398 - VF - Program 2 (LY1 og GL1) - Dokumenter\01 Intern\02 Tegninger\" +
                @"04 Modtaget\7.28.1a\2024.05.15 - Afl data\Ledninger_Virum_SBP_2024.05.14.csv");

            IEnumerable<(
                string Knudenavn, double Bundkote, int Diameter,
                double Terraenkote, double X, double Y, double Daekselkote)> readKps(IEnumerable<ExpandoObject> rawData)
            {
                foreach (dynamic record in rawData)
                {
                    IDictionary<string, object> rowDict = record;
                    yield return (
                        Knudenavn: rowDict["Knudenavn"]?.ToString(),
                        Bundkote: ParseDouble(rowDict["Bundkote"]?.ToString()),
                        Diameter: ParseInt(rowDict["Diameter"]?.ToString()),
                        Terraenkote: ParseDouble(rowDict["Terraenkote"]?.ToString()),
                        X: ParseDouble(rowDict["X"]?.ToString()),
                        Y: ParseDouble(rowDict["Y"]?.ToString()),
                        Daekselkote: ParseDouble(rowDict["Daekselkote"]?.ToString())
                        );
                }
            }

            IEnumerable<(
                string OpsKnude, string NedsKnude, double NedsBK, double OpsBK,
                int Handelsmaal, double Laengde)> readLedns(IEnumerable<ExpandoObject> rawData)
            {
                foreach (dynamic record in rawData)
                {
                    IDictionary<string, object> rowDict = record;
                    yield return (
                        OpsKnude: rowDict["OpsKnude"]?.ToString(),
                        NedsKnude: rowDict["NedsKnude"]?.ToString(),
                        NedsBK: ParseDouble(rowDict["NedsBK"]?.ToString()),
                        OpsBK: ParseDouble(rowDict["OpsBK"]?.ToString()),
                        Handelsmaal: ParseInt(rowDict["Handelsmaal"]?.ToString()),
                        Laengde: ParseDouble(rowDict["Laengde"]?.ToString())
                        );
                }
            }

            double ParseDouble(string input) =>
                double.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out double result) ? result : 0.0;

            int ParseInt(string input) =>
                int.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out int result) ? result : 0;
            #endregion

            var kps = readKps(kpsRaw);
            var ledns = readLedns(lednsRaw);

            #region Count groups and counts
            prdDbg(string.Join("\n", ledns
                .GroupBy(x => x.OpsKnude + ";" + x.NedsKnude)
                .GroupBy(x => x.Count())  // Group by the count of items in each group
                .Select(g => new
                {
                    Count = g.Key,
                    Frequency = g.Count(),
                    Keys = g.Key > 2 ? g.Select(x => x.Key).Distinct() : new List<string>() // Collect keys if count > 2
                })
                .OrderBy(x => x.Count)  // Order by the count of items
                .Select(x => x.Count + ": " + x.Frequency +
                (x.Keys.Any() ? " Keys: [" + string.Join(", ", x.Keys) + "]" : "")))); // Format the output with keys if present

            //prdDbg(string.Join("\n", kps
            //    .GroupBy(x => x.Knudenavn)
            //    .GroupBy(x => x.Count())  // Group by the count of items in each group
            //    .Select(g => new
            //    {
            //        Count = g.Key,
            //        Frequency = g.Count(),
            //        Keys = g.Key > 1 ? g.Select(x => x.Key).Distinct() : new List<string>(), // Collect keys if count > 2
            //        BundKoter = g.Key > 1 ? g.SelectMany(x => x.Select(y => y.Bundkote)).Distinct() : new List<double>(), // Collect keys if count > 2
            //    })
            //    .OrderBy(x => x.Count)  // Order by the count of items
            //    .Select(x => x.Count + ": " + x.Frequency +
            //    (x.BundKoter.Count() > 1 ?
            //    //$"\n[{string.Join(", ", x.Keys)}]" +
            //    $"\n[{string.Join(", ", x.Keys)} : {string.Join(", ", x.BundKoter)}]"
            //    : "")))); // Format the output with keys if present

            var gsNavn = kps.GroupBy(x => x.Knudenavn);
            var gsCount = gsNavn.GroupBy(x => x.Count());

            foreach (var cg in gsCount)
            {
                if (cg.Key < 2) continue;

                prdDbg($"Count: {cg.Key}, Frequency: {cg.Count()}");

                foreach (var kg in cg)
                {
                    var bks = kg.Select(x => x.Bundkote).Distinct();

                    if (bks.Count() < 2) continue;

                    prdDbg($"Knudenavn: {kg.Key}, Bundkoter: {string.Join(", ", bks)}");
                }
            }
            #endregion

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    foreach (var kg in gsNavn)
                    {
                        //Check to see if BK might be different
                        var bks = kg.Select(x => x.Bundkote).Distinct();
                        if (bks.Count() > 1)
                            throw new System.Exception(
                                $"Requirement of only one BK per group is NOT kept!\n"+
                                $"Knudenavn: {kg.Key}, Bundkoter: {string.Join(", ", bks)}");

                        var k = kg.First();

                        if (k.X.Equalz(0.0, 0.00001) || k.Y.Equalz(0.0, 0.00001))
                            throw new System.Exception(
                                $"Requirement of X and Y being different from 0 is NOT kept!\n" +
                                $"Knudenavn: {kg.Key}, X: {k.X}, Y: {k.Y}");

                        if (k.Bundkote.Equalz(0.0, 0.00001)) k.Bundkote = -99;

                        DBPoint p = new DBPoint(new Point3d(k.X, k.Y, k.Bundkote));
                        p.AddEntityToDbModelSpace(localDb);
                    }
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }
        }
    }
}