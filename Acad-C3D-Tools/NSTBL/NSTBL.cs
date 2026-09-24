using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices.Core;
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
using Autodesk.Aec.PropertyData;
using Autodesk.Aec.PropertyData.DatabaseServices;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using MoreLinq;
//using GroupByCluster;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;
//using Microsoft.Office.Interop.Excel;

//using static IntersectUtilities.Enums;
//using static IntersectUtilities.HelperMethods;
//using static IntersectUtilities.Utils;
using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.UtilsCommon.UtilsODData;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using Line = Autodesk.AutoCAD.DatabaseServices.Line;
using NetTopologySuite.Geometries;
using Point = NetTopologySuite.Geometries.Point;
using IntersectUtilities.UtilsCommon.DataManager;

[assembly: CommandClass(typeof(IntersectUtilities.NSTBL.NoCommands))]

namespace IntersectUtilities.NSTBL
{
    public class DimensioneringExtension : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\n(◠‿◠) NSTBL loaded! (◠‿◠)\n");
        }

        public void Terminate()
        {
        }
        #endregion
        /// <command>TBLCHECKPOLYOVERLAP</command>
        /// <summary>
        /// Quality-checks for overlapping area polylines on layer "0-OMRÅDER-OK". Converts each
        /// closed polyline to an NTS polygon and checks pairwise intersections. When overlap area
        /// exceeds 0.01 m², writes a report to the command line with both handles, the overlap area,
        /// and the percentage relative to each polygon’s area. Intended to validate area polygons
        /// before intersection-based quantity extraction and tilbudsliste exports to prevent
        /// polygons intersecting each other.
        /// </summary>
        /// <category>Tilbudsliste</category>
        [CommandMethod("TBLCHECKPOLYOVERLAP")]
        public void tblcheckpolyoverlap()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    List<Polyline> cplines =
                        localDb.ListOfType<Polyline>(tx)
                        .Where(x => x.Layer == "0-OMRÅDER-OK")
                        .ToList();

                    for (int i = 0; i < cplines.Count; i++)
                    {
                        Polyline plineI = cplines[i];
                        if (!plineI.Closed)
                            throw new System.Exception($"Polyline {plineI.Handle} is not closed!");
                        Polygon pgonI = NTSConversion.ConvertClosedPlineToNTSPolygon(plineI);
                        for (int j = i + 1; j < cplines.Count; j++)
                        {
                            Polyline plineJ = cplines[j];
                            if (!plineJ.Closed)
                                throw new System.Exception($"Polyline {plineJ.Handle} is not closed!");
                            Polygon pgonJ = NTSConversion.ConvertClosedPlineToNTSPolygon(plineJ);

                            Geometry intersection = pgonI.Intersection(pgonJ);

                            if (intersection.Area > 0.01)
                            {
                                //write that polyI and polyJ overlap (writing their handles)
                                //Then calculate the percentage of overlap area
                                //of each polygon's area and write it                                
                                var percentageOfI = intersection.Area / pgonI.Area;
                                var percentageOfJ = intersection.Area / pgonJ.Area;

                                prdDbg(
                                    $"Pline 1 {plineI.Handle} and pline 2 {plineJ.Handle} overlap!\n" +
                                    $"Overlap area: {intersection.Area.ToString("0.000")}m².\n" +
                                    $"Percent of 1's area: {(percentageOfI * 100).ToString("0.00")}%\n" +
                                    $"Percent of 2's area: {(percentageOfJ * 100).ToString("0.00")}%\n");
                            }
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
        }

        /// <param name="inheritWeldSerie">
        /// Welds without a Serie inherit it from the pipe or component they join (TBLEXPORTCWOV2).
        /// The older exports keep the blank Serie so their output stays unchanged.
        /// </param>
        internal HashSet<IntersectResult> gatherintersectdata(bool inheritWeldSerie)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            var dro = DataReferencesOptions.Create();
            if (dro == null) return null;
            DataManager dm = new DataManager(dro);

            using Database fremDb = dm.Fremtid();            
            using Transaction fremTx = fremDb.TransactionManager.StartTransaction();

            HashSet<IntersectResult> allResults = new HashSet<IntersectResult>();

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Gather entities for TBL Export
                    List<Polyline> cplines =
                        localDb.ListOfType<Polyline>(tx)
                        .Where(x => x.Layer == "0-OMRÅDER-OK")
                        .ToList();

                    if (cplines.Count < 1)
                    {
                        AbortGracefully("Ingen lukkede polylinjer på lag 0-OMRÅDER-OK fundet!",
                            fremDb, localDb);
                        return null;
                    }

                    HashSet<Polyline> pipes = fremDb.GetFjvPipes(fremTx);
                    HashSet<BlockReference> comps = fremDb.HashSetOfType<BlockReference>(fremTx);

                    #endregion

                    PropertySetManager psm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriOmråder);
                    PSetDefs.DriOmråder psDef = new PSetDefs.DriOmråder();

                    #region Serie fallback for welds
                    //Welds carry no Serie in FJV Dynamiske Komponenter.csv, so they export a blank
                    //Serie and fall through the staging grid. They inherit it from whatever they
                    //join: the pipe they sit on, or - for a weld between two components - the port
                    //of the neighbouring component. Extents are cached up front so the per-weld
                    //pipe lookup can reject most pipes without paying for a closest-point call.
                    var pipesWithExtents = pipes
                        .Where(p => p.Bounds.HasValue)
                        .Select(p => (Pipe: p, Ext: p.Bounds.Value))
                        .ToArray();
                    //Reading ports costs an ARX call per component, and most welds are resolved by
                    //the pipe lookup alone, so this is only built if a weld actually needs it.
                    var componentPorts = new Lazy<(Point3d Port, string Serie)[]>(
                        () => ReadComponentPortSeries(comps));
                    var weldSerieCache = new Dictionary<Oid, string>();
                    #endregion

                    #region Collect Intersection Results
                    for (int i = 0; i < cplines.Count; i++)
                    {
                        Polyline polygonPline = cplines[i];
                        if (!polygonPline.Closed)
                            throw new System.Exception($"Polyline {polygonPline.Handle} is not closed!");
                        Polygon pgon = NTSConversion.ConvertClosedPlineToNTSPolygon(polygonPline);

                        foreach (Polyline pipe in pipes)
                        {
                            LineString pipeLineString = NTSConversion.ConvertPlineToNTSLineString(pipe);
                            if (!pgon.Intersects(pipeLineString)) continue;
                            Geometry intersect = pgon.Intersection(pipeLineString);

                            IntersectResultPipe irp = new IntersectResultPipe();
                            irp.Vejnavn = psm.ReadPropertyString(polygonPline, psDef.Vejnavn);
                            irp.Vejklasse = psm.ReadPropertyString(polygonPline, psDef.Vejklasse);
                            irp.Belægning = psm.ReadPropertyString(polygonPline, psDef.Belægning);
                            irp.DN1 = GetPipeDN(pipe).ToString();
                            irp.System = GetPipeType(pipe, true).ToString();
                            irp.Serie = GetPipeSeriesV2(pipe, true).ToString();
                            irp.Length = intersect.Length;
                            irp.SystemType = GetPipeSystem(pipe).ToString();

                            //Standard delivery length from the pipe schedule (DefaultL).
                            //0 means the system has no standard length defined (e.g. FibreFlex),
                            //in which case the field must stay empty so it drops out of the key.
                            double stdLength = GetPipeStdLength(pipe);
                            irp.StdLength = stdLength > 0
                                ? stdLength.ToString("0.##", CultureInfo.InvariantCulture)
                                : "";

                            allResults.Add(irp);
                        }

                        foreach (BlockReference br in comps)
                        {
                            Point compPoint = NTSConversion.ConvertBrToNTSPoint(br);
                            if (!pgon.Intersects(compPoint)) continue;

                            IntersectResultComponent irp = new IntersectResultComponent();
                            irp.Vejnavn = psm.ReadPropertyString(polygonPline, psDef.Vejnavn);
                            irp.Vejklasse = psm.ReadPropertyString(polygonPline, psDef.Vejklasse);
                            irp.Belægning = psm.ReadPropertyString(polygonPline, psDef.Belægning);
                            irp.Navn = br.ReadDynamicCsvProperty(DynamicProperty.TBLNavn, true);
                            irp.DN1 = br.ReadDynamicCsvProperty(DynamicProperty.DN1, true);
                            irp.DN2 = br.ReadDynamicCsvProperty(DynamicProperty.DN2, true);
                            irp.System = br.ReadDynamicCsvProperty(DynamicProperty.System, true);
                            irp.Serie = br.ReadDynamicCsvProperty(DynamicProperty.Serie, true);
                            irp.SystemType = br.ReadDynamicCsvProperty(DynamicProperty.SysNavn, true);

                            if (inheritWeldSerie && irp.Serie.IsNoE() &&
                                br.ReadDynamicCsvProperty(DynamicProperty.Type, false) == "Svejsning")
                                irp.Serie = GetSerieFromNeighbour(
                                    br, pipesWithExtents, componentPorts, weldSerieCache);

                            if (irp.System == "Frem" || irp.System == "Retur") irp.System = "Enkelt";

                            allResults.Add(irp);
                        }
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    fremTx.Abort();
                    fremTx.Dispose();
                    fremDb.Dispose();
                    tx.Abort();
                    prdDbg(ex);
                    return null;
                }
                tx.Commit();
                fremTx.Abort();
                fremTx.Dispose();
                fremDb.Dispose();
                return allResults;
            }
        }
        /// <summary>
        /// A weld is placed at cluster.First().WeldPoint by PipelineNetwork.CreateWeldBlocks, which
        /// clusters candidate points within 0.005. It can therefore sit up to that far from the very
        /// geometry it joins, so the same tolerance is used here to decide what a weld is welded to.
        /// </summary>
        private const double weldSnapTolerance = 0.005;
        /// <summary>
        /// Welds have no Serie of their own, so they inherit it from what they are welded onto:
        /// the pipe underneath, or - when two components are welded directly together - the port
        /// of the neighbouring component. Returns "" when neither is found or when the pipe itself
        /// has no series (non-steel systems); the caller then exports a blank Serie as before.
        /// </summary>
        private static string GetSerieFromNeighbour(
            BlockReference br,
            (Polyline Pipe, Extents3d Ext)[] pipes,
            Lazy<(Point3d Port, string Serie)[]> componentPorts,
            Dictionary<Oid, string> cache)
        {
            if (cache.TryGetValue(br.Id, out var cached)) return cached;

            const double tol = weldSnapTolerance;
            Point3d pos = br.Position;
            string serie = "";

            //A pipe states its series through its own geometry, so it is asked first.
            foreach (var (pipe, ext) in pipes)
            {
                if (pos.X < ext.MinPoint.X - tol || pos.X > ext.MaxPoint.X + tol ||
                    pos.Y < ext.MinPoint.Y - tol || pos.Y > ext.MaxPoint.Y + tol) continue;
                if (pipe.GetClosestPointTo(pos, false).DistanceHorizontalTo(pos) > tol) continue;

                var series = GetPipeSeriesV2(pipe);
                if (series != PipeSeriesEnum.Undefined) { serie = series.ToString(); break; }
            }

            //No pipe under the weld: it joins two components, so ask the neighbouring port.
            if (serie.IsNoE())
                foreach (var (port, portSerie) in componentPorts.Value)
                    if (port.DistanceHorizontalTo(pos) <= tol) { serie = portSerie; break; }

            if (serie.IsNoE())
                prdDbg($"Svejsning {br.Handle} kunne ikke arve Serie fra hverken rør eller komponent!");

            cache[br.Id] = serie;
            return serie;
        }
        /// <summary>
        /// Port positions of every component that states a Serie, so a weld between two components
        /// can inherit from its neighbour. Components without a Serie of their own are skipped -
        /// they have nothing to give, and skipping them keeps the port read to known FJV components.
        /// </summary>
        private static (Point3d Port, string Serie)[] ReadComponentPortSeries(
            IEnumerable<BlockReference> comps)
        {
            var ports = new List<(Point3d, string)>();
            foreach (BlockReference br in comps)
            {
                string serie = br.ReadDynamicCsvProperty(DynamicProperty.Serie, true);
                if (serie.IsNoE()) continue;
                foreach (Point3d port in br.GetAllEndPoints()) ports.Add((port, serie));
            }
            return ports.ToArray();
        }
        internal HashSet<IntersectResult> processintersectdataCWO()
        {
            var results = gatherintersectdata(false);
            if (results == null)
            {
                prdDbg("Received null instead of results. Aborting.");
                return null;
            }
            #region Process Intersection Results
            var pipeSummary = results
                .Where(x => x is IntersectResultPipe)
                .Cast<IntersectResultPipe>()
                .GroupBy(x => new
                {
                    x.IntersectType,
                    x.Vejnavn,
                    x.Vejklasse,
                    x.Belægning,
                    x.Navn,
                    x.DN1,
                    x.System,
                    x.Serie,
                    x.SystemType
                })
                .Select(g => new IntersectResultPipe
                {
                    IntersectType = g.Key.IntersectType,
                    Vejnavn = g.Key.Vejnavn,
                    Vejklasse = g.Key.Vejklasse,
                    Belægning = g.Key.Belægning,
                    Navn = g.Key.Navn,
                    DN1 = g.Key.DN1,
                    System = g.Key.System,
                    Serie = g.Key.Serie,
                    Length = g.Sum(x => x.Length), // sum Length for each group
                    SystemType = g.Key.SystemType
                });
            var componentSummary = results
                .Where(x => x is IntersectResultComponent)
                .Cast<IntersectResultComponent>()
                .GroupBy(x => new
                {
                    x.IntersectType,
                    x.Vejnavn,
                    x.Vejklasse,
                    x.Belægning,
                    x.Navn,
                    x.DN1,
                    x.DN2,
                    x.System,
                    x.Serie,
                    x.SystemType
                })
                .Select(g => new IntersectResultComponent
                {
                    IntersectType = g.Key.IntersectType,
                    Vejnavn = g.Key.Vejnavn,
                    Vejklasse = g.Key.Vejklasse,
                    Belægning = g.Key.Belægning,
                    Navn = g.Key.Navn,
                    DN1 = g.Key.DN1,
                    DN2 = g.Key.DN2,
                    System = g.Key.System,
                    Serie = g.Key.Serie,
                    Count = g.Count(), // count items for each group
                    SystemType = g.Key.SystemType
                });

            HashSet<IntersectResult> allResults = new HashSet<IntersectResult>();
            allResults.UnionWith(pipeSummary);
            allResults.UnionWith(componentSummary);
            return allResults;
            #endregion
        }
        internal HashSet<IntersectResult> processintersectdataCWOV2()
        {
            var results = gatherintersectdata(true);
            if (results == null)
            {
                prdDbg("Received null instead of results. Aborting.");
                return null;
            }
            #region Process Intersection Results
            //Vejnavn is not in the key: the row has no column for it, so grouping on it
            //would emit rows that look identical in the sheet.
            var pipeSummary = results
                .Where(x => x is IntersectResultPipe)
                .Cast<IntersectResultPipe>()
                .GroupBy(x => new
                {
                    x.IntersectType,
                    x.Vejklasse,
                    x.Belægning,
                    x.Navn,
                    x.StdLength,
                    x.DN1,
                    x.System,
                    x.Serie,
                    x.SystemType
                })
                .Select(g => new IntersectResultPipe
                {
                    IntersectType = g.Key.IntersectType,
                    Vejklasse = g.Key.Vejklasse,
                    Belægning = g.Key.Belægning,
                    Navn = g.Key.Navn,
                    StdLength = g.Key.StdLength,
                    DN1 = g.Key.DN1,
                    System = g.Key.System,
                    Serie = g.Key.Serie,
                    Antal = g.Sum(x => x.Length),
                    Length = g.Sum(x => x.Length),
                    SystemType = g.Key.SystemType
                });
            var componentSummary = results
                .Where(x => x is IntersectResultComponent)
                .Cast<IntersectResultComponent>()
                .GroupBy(x => new
                {
                    x.IntersectType,
                    x.Vejklasse,
                    x.Belægning,
                    x.Navn,
                    x.DN1,
                    x.DN2,
                    x.System,
                    x.Serie,
                    x.SystemType
                })
                .Select(g => new IntersectResultComponent
                {
                    IntersectType = g.Key.IntersectType,
                    Vejklasse = g.Key.Vejklasse,
                    Belægning = g.Key.Belægning,
                    Navn = g.Key.Navn,
                    DN1 = g.Key.DN1,
                    DN2 = g.Key.DN2,
                    System = g.Key.System,
                    Serie = g.Key.Serie,
                    Count = g.Count(),
                    SystemType = g.Key.SystemType
                });

            HashSet<IntersectResult> allResults = new HashSet<IntersectResult>();
            allResults.UnionWith(pipeSummary);
            allResults.UnionWith(componentSummary);
            return allResults;
            #endregion
        }
        internal HashSet<IntersectResult> processintersectdataJJR()
        {
            var results = gatherintersectdata(false);
            if (results == null)
            {
                prdDbg("Received null instead of results. Aborting.");
                return null;
            }
            #region Process Intersection Results
            var pipeSummary = results
                .Where(x => x is IntersectResultPipe)
                .Cast<IntersectResultPipe>()
                .GroupBy(x => new
                {
                    x.IntersectType,
                    //x.Vejnavn,
                    x.Vejklasse,
                    x.Belægning,
                    x.Navn,
                    x.DN1,
                    x.System,
                    x.Serie,
                    x.SystemType
                })
                .Select(g => new IntersectResultPipe
                {
                    IntersectType = g.Key.IntersectType,
                    //Vejnavn = g.Key.Vejnavn,
                    Vejklasse = g.Key.Vejklasse,
                    Belægning = g.Key.Belægning,
                    Navn = g.Key.Navn,
                    DN1 = g.Key.DN1,
                    System = g.Key.System,
                    Serie = g.Key.Serie,
                    Antal = g.Sum(x => x.Length),
                    Length = g.Sum(x => x.Length), // sum Length for each group
                    SystemType = g.Key.SystemType
                });
            var componentSummary = results
                .Where(x => x is IntersectResultComponent)
                .Cast<IntersectResultComponent>()
                .GroupBy(x => new
                {
                    x.IntersectType,
                    //x.Vejnavn,
                    x.Vejklasse,
                    x.Belægning,
                    x.Navn,
                    x.DN1,
                    x.DN2,
                    x.System,
                    x.Serie,
                    x.SystemType
                })
                .Select(g => new IntersectResultComponent
                {
                    IntersectType = g.Key.IntersectType,
                    //Vejnavn = g.Key.Vejnavn,
                    Vejklasse = g.Key.Vejklasse,
                    Belægning = g.Key.Belægning,
                    Navn = g.Key.Navn,
                    DN1 = g.Key.DN1,
                    DN2 = g.Key.DN2,
                    System = g.Key.System,
                    Serie = g.Key.Serie,
                    Count = g.Count(), // count items for each group
                    SystemType = g.Key.SystemType
                });

            HashSet<IntersectResult> allResults = new HashSet<IntersectResult>();
            allResults.UnionWith(pipeSummary);
            allResults.UnionWith(componentSummary);
            return allResults;
            #endregion
        }

        /// <command>TBLEXPORTCWO</command>
        /// <summary>
        /// Computes intersections between tender areas (layer "0-OMRÅDER-OK") and FJV objects, then
        /// exports a CSV in the "CWO" format. Pipes are length-summed by key attributes; components are
        /// counted by type and attributes. Output is written to C:\Temp\IntersectResult.csv and a brief
        /// note clarifies that twin welds are not multiplied. Intended for tilbudsliste generation in the
        /// CWO delivery format.
        /// </summary>
        /// <category>Tilbudsliste</category>
        [CommandMethod("TBLEXPORTCWO")]
        public void tblexportcwo()
        {
            var results = processintersectdataCWO();
            if (results == null)
            {
                prdDbg("Received null instead of results. Aborting.");
                return;
            }

            #region Export Intersection Results
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("BEMÆRK: Twin svejsninger bliver IKKE ganget med 2!");

            foreach (IntersectResult ir in results) sb.AppendLine(ir.ToString(ExportType.CWO));

            File.WriteAllText(@"C:\Temp\IntersectResult.csv", sb.ToString(), Encoding.UTF8);
            prdDbg("I AM FINISH! (Results written to C:\\Temp\\IntersectResult.csv)");
            #endregion
        }

        /// <command>TBLEXPORTCWOV2</command>
        /// <summary>
        /// Computes intersections between tender areas (layer "0-OMRÅDER-OK") and FJV objects, then
        /// exports a CSV whose rows paste straight into the CWO tilbudsliste sheet:
        /// Egne noter;Vejklasse;Belægningstype;Komponent;Standardlængde;Materiale;DN;DN;Rørsystem;Serie;Antal.
        /// Egne noter is left empty for the etape to be written in the sheet. Pipes are summed by
        /// length (Antal in metres) and split by standard delivery length; components are counted.
        /// Welds without a Serie inherit it from the pipe or component they join. The file has no
        /// header row. Output is written to C:\Temp\IntersectResult.csv.
        /// </summary>
        /// <category>Tilbudsliste</category>
        [CommandMethod("TBLEXPORTCWOV2")]
        public void tblexportcwov2()
        {
            var results = processintersectdataCWOV2();
            if (results == null)
            {
                prdDbg("Received null instead of results. Aborting.");
                return;
            }

            #region Export Intersection Results
            StringBuilder sb = new StringBuilder();
            foreach (IntersectResult ir in results.OrderBy(x => x.IntersectType))
                sb.AppendLine(ir.ToCwoV2Row());

            File.WriteAllText(@"C:\Temp\IntersectResult.csv", sb.ToString(), Encoding.UTF8);
            prdDbg("BEMÆRK: Twin svejsninger bliver IKKE ganget med 2!");
            prdDbg("I AM FINISH! (Results written to C:\\Temp\\IntersectResult.csv)");
            #endregion
        }

        /// <command>TBLEXPORTJJR</command>
        /// <summary>
        /// Computes intersections between tender areas (layer "0-OMRÅDER-OK") and FJV objects, then
        /// exports a CSV in the "JJR" format with headers:
        /// Vejklasse;Belægningstype;Komponent;DN1;DN2;Rørsystem;Serie;Antal;Længde;SystemType.
        /// Pipes are summed by length and components by count per attribute grouping. Output is written
        /// to C:\Temp\IntersectResult.csv. Intended for tilbudsliste generation in JJR’s required
        /// format.
        /// </summary>
        /// <category>Tilbudsliste</category>
        [CommandMethod("TBLEXPORTJJR")]
        public void tblexportjjr()
        {
            var results = processintersectdataJJR();
            if (results == null)
            {
                prdDbg("Received null instead of results. Aborting.");
                return;
            }

            #region Export Intersection Results
            StringBuilder sb = new StringBuilder();
            //sb.AppendLine("BEMÆRK: Twin svejsninger bliver IKKE ganget med 2!");
            sb.AppendLine("Vejklasse;Belægningstype;Komponent;DN1;DN2;Rørsystem;Serie;Antal;Længde;SystemType");

            foreach (IntersectResult ir in results.OrderBy(x => x.IntersectType)) sb.AppendLine(ir.ToString(ExportType.JJR));

            File.WriteAllText(@"C:\Temp\IntersectResult.csv", sb.ToString(), Encoding.UTF8);
            prdDbg("I AM FINISH! (Results written to C:\\Temp\\IntersectResult.csv)");
            #endregion
        }
    }

    public class NoCommands { }
}
