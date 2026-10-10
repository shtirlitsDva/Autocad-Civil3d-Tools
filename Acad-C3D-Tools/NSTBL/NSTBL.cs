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
        /// Welds without a Serie inherit it from the pipe or component they join (TBLEXPORTV2).
        /// The older exports keep the blank Serie so their output stays unchanged.
        /// </param>
        /// <param name="bendAngleInName">
        /// Bends with a free angle get the drawn angle in their name instead of 90° (TBLEXPORTV2).
        /// </param>
        /// <param name="fixComponentData">
        /// Component rows are made usable for the tilbudsliste (TBLEXPORTV2): blocks with no row
        /// in FJV Dynamiske Komponenter.csv are left out and listed, Materialeskift gets its plastic
        /// system and steel-side serie, plastic components get their pipe's serie and their own
        /// system's name prefix, and steel components without a serie get one. See FixComponentData.
        /// </param>
        internal HashSet<IntersectResult> gatherintersectdata(
            bool inheritWeldSerie, bool bendAngleInName = false, bool fixComponentData = false)
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
                    var componentPorts = new Lazy<(Point3d Port, string Serie, string SysNavn, Oid Owner)[]>(
                        () => ReadComponentPortSeries(comps, pipesWithExtents));
                    var weldSerieCache = new Dictionary<Oid, string>();
                    #endregion

                    var skippedBlocks = new Dictionary<string, int>();
                    var componentNotes = new List<string>();

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
                            //Every CSV row has a TBLNavn, so an empty one means the block has no
                            //row at all (detailing blocks and the like): nothing to count.
                            if (fixComponentData && irp.Navn.IsNoE())
                            {
                                string blockName = br.RealName();
                                skippedBlocks[blockName] = skippedBlocks.GetValueOrDefault(blockName) + 1;
                                continue;
                            }
                            if (bendAngleInName) irp.Navn = NameWithBendAngle(br, irp.Navn);
                            irp.DN1 = br.ReadDynamicCsvProperty(DynamicProperty.DN1, true);
                            irp.DN2 = br.ReadDynamicCsvProperty(DynamicProperty.DN2, true);
                            irp.System = br.ReadDynamicCsvProperty(DynamicProperty.System, true);
                            irp.Serie = br.ReadDynamicCsvProperty(DynamicProperty.Serie, true);
                            irp.SystemType = br.ReadDynamicCsvProperty(DynamicProperty.SysNavn, true);

                            bool isWeld = br.ReadDynamicCsvProperty(DynamicProperty.Type, false) == "Svejsning";
                            if (inheritWeldSerie && irp.Serie.IsNoE() && isWeld)
                                irp.Serie = GetSerieFromNeighbour(
                                    br, pipesWithExtents, componentPorts, weldSerieCache);
                            if (fixComponentData && !isWeld)
                                FixComponentData(br, irp, pipesWithExtents, componentPorts, componentNotes);

                            if (irp.System == "Frem" || irp.System == "Retur") irp.System = "Enkelt";

                            allResults.Add(irp);
                        }
                    }
                    #endregion

                    foreach (var (blockName, count) in skippedBlocks)
                        prdDbg($"Udeladt, ingen række i FJV Dynamiske Komponenter.csv: {blockName} ({count} stk.)");
                    foreach (string note in componentNotes) prdDbg(note);
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
            Lazy<(Point3d Port, string Serie, string SysNavn, Oid Owner)[]> componentPorts,
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
                foreach (var (port, portSerie, _, _) in componentPorts.Value)
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
        /// The component's system and id come along so FixComponentData can ask only neighbours of
        /// one system and never the component itself. A serie fixed in the CSV gives way to the
        /// pipes at the component's ports (SerieFromPipesOverCsv), so a weld next to it inherits
        /// the serie the component is exported with.
        /// </summary>
        private static (Point3d Port, string Serie, string SysNavn, Oid Owner)[] ReadComponentPortSeries(
            IEnumerable<BlockReference> comps, (Polyline Pipe, Extents3d Ext)[] pipes)
        {
            var ports = new List<(Point3d, string, string, Oid)>();
            foreach (BlockReference br in comps)
            {
                string serie = br.ReadDynamicCsvProperty(DynamicProperty.Serie, true);
                if (serie.IsNoE()) continue;
                string sysNavn = br.ReadDynamicCsvProperty(DynamicProperty.SysNavn, true);
                string pipeSerie = SerieFromPipesOverCsv(br, pipes, sysNavn);
                if (!pipeSerie.IsNoE()) serie = pipeSerie;
                foreach (Point3d port in br.GetAllEndPoints()) ports.Add((port, serie, sysNavn, br.Id));
            }
            return ports.ToArray();
        }
        /// <summary>
        /// Many CSV rows fix one serie per block (SH LIGE and PA TWIN S3, the T blocks S2 or S3,
        /// Materialeskift S3) although the block is placed on pipes of any serie. For those the serie
        /// of the pipe at the ports wins: of the block's own system and DN1 first, and for a
        /// Materialeskift the pipe on its M1 side. "" when the CSV names a block parameter ("$Serie"),
        /// which the drafter sets, or when no such pipe touches the block.
        /// </summary>
        private static string SerieFromPipesOverCsv(
            BlockReference br, (Polyline Pipe, Extents3d Ext)[] pipes, string sysNavn)
        {
            string csvSerie = br.ReadDynamicCsvProperty(DynamicProperty.Serie, false);
            if (csvSerie.IsNoE() || csvSerie.StartsWith("$")) return "";
            var pipesOnly = Array.Empty<(Point3d, string, string, Oid)>();

            string navn = br.ReadDynamicCsvProperty(DynamicProperty.TBLNavn, true);
            if (navn.Contains("{M1}"))
            {
                string m1 = br.ReadDynamicCsvProperty(DynamicProperty.M1, true);
                return systemDict.TryGetValue(m1, out var from)
                    ? SerieAtPorts(br, pipes, pipesOnly, from, null) : "";
            }

            if (!Enum.TryParse(sysNavn, out PipeSystemEnum system) || system == PipeSystemEnum.Ukendt)
                return "";
            string dn1 = br.ReadDynamicCsvProperty(DynamicProperty.DN1, true);
            return SerieAtPorts(br, pipes, pipesOnly, system, dn1);
        }
        /// <summary>
        /// A component port sits on the pipe end or on the neighbouring component's port; measured
        /// in 7.21.12 they are at most 0.009 apart, while the next nearest geometry is 0.1 away.
        /// </summary>
        private const double componentPortTolerance = 0.01;
        /// <summary>
        /// Plastic TBL names start with their system's prefix. ALUPEX-REDUKTION's CSV row borrowed
        /// PertFlextra's ("PRTFLEX Reduktion"), so the prefix is set from the block's SysNavn.
        /// </summary>
        private static readonly Dictionary<PipeSystemEnum, string> tblNamePrefix = new()
        {
            { PipeSystemEnum.AluPex, "ALUPEX" },
            { PipeSystemEnum.PertFlextra, "PRTFLEX" },
            { PipeSystemEnum.PertPIPE, "PRTPIPE" },
        };
        /// <summary>
        /// TBLEXPORTV2 only. Fills in what FJV Dynamiske Komponenter.csv cannot give a component row:
        /// - Materialeskift: the CSV leaves "{M1}x{M2}" in the name and the system Ukendt. The block's
        ///   Type (e.g. ALUPEX63xDN50) holds both sides; M1 is the DN1 side, M2 the side it changes
        ///   to. The name drops the placeholders, the system becomes M2's, and the serie is that of
        ///   the M1 side (the steel pipe, or the steel component it touches).
        /// - Plastic components: the CSV gives one fixed serie per block (AluPex S2, PertFlextra S3)
        ///   while the pipes are S1 or S2, so the serie comes from the pipe at the DN1 port (else a
        ///   neighbouring plastic component, else the CSV value stays). The name prefix follows SysNavn.
        /// - Steel components with no serie (the CSV column is empty, e.g. PRÆBØJN 90GR ENKELT v2 and
        ///   VENTIL E): the block's own Serie parameter, else what it is joined to. These are listed
        ///   on the command line, as are those still without a serie.
        /// - Steel components whose CSV serie is a fixed value (e.g. SH LIGE S3) on pipes of another
        ///   serie: the pipe's serie (SerieFromPipesOverCsv), listed on the command line.
        /// - Afgreningsstuds, whose CSV System is fixed Twin: twin or enkelt from the main pipe under the
        ///   stud (SystemFromMainPipe), listed on the command line when it changes.
        /// </summary>
        private static void FixComponentData(
            BlockReference br,
            IntersectResultComponent irp,
            (Polyline Pipe, Extents3d Ext)[] pipes,
            Lazy<(Point3d Port, string Serie, string SysNavn, Oid Owner)[]> componentPorts,
            List<string> notes)
        {
            if (irp.Navn.Contains("{M1}"))
            {
                string m1 = br.ReadDynamicCsvProperty(DynamicProperty.M1, true);
                string m2 = br.ReadDynamicCsvProperty(DynamicProperty.M2, true);
                PipeSystemEnum from = systemDict.TryGetValue(m1, out var f) ? f : PipeSystemEnum.Ukendt;
                irp.Navn = irp.Navn.Replace("{M1}x{M2}", "").Trim();
                if (from != PipeSystemEnum.Stål && from != PipeSystemEnum.Ukendt) irp.Navn += " fra " + from;
                if (systemDict.TryGetValue(m2, out var to)) irp.SystemType = to.ToString();

                string sideSerie = SerieAtPorts(br, pipes, componentPorts.Value, from, null);
                if (!sideSerie.IsNoE()) irp.Serie = sideSerie;
                else notes.Add($"{br.RealName()} {br.Handle}: intet {from} ved portene, Serie {irp.Serie} fra CSV beholdt.");
                return;
            }

            if (!Enum.TryParse(irp.SystemType, out PipeSystemEnum system)) return;

            if (system == PipeSystemEnum.Stål)
            {
                SystemFromMainPipe(br, irp, pipes, notes);
                if (!irp.Serie.IsNoE())
                {
                    string atPorts = SerieFromPipesOverCsv(br, pipes, irp.SystemType);
                    if (!atPorts.IsNoE() && atPorts != irp.Serie)
                    {
                        notes.Add($"{br.RealName()} {br.Handle}: Serie {irp.Serie} fra CSV erstattet af {atPorts} fra røret.");
                        irp.Serie = atPorts;
                    }
                    return;
                }
                string own = OwnSerie(br);
                if (!own.IsNoE()) { irp.Serie = own; return; }
                string joined = SerieAtPorts(br, pipes, componentPorts.Value, PipeSystemEnum.Stål, irp.DN1);
                irp.Serie = joined;
                notes.Add(joined.IsNoE()
                    ? $"{br.RealName()} {br.Handle}: ingen Serie i CSV, blok eller ved portene - Serie tom."
                    : $"{br.RealName()} {br.Handle}: ingen Serie i CSV eller blok, {joined} hentet ved portene.");
                return;
            }

            if (system == PipeSystemEnum.Ukendt) return;
            if (tblNamePrefix.TryGetValue(system, out string ownPrefix))
                foreach (string other in tblNamePrefix.Values)
                    if (other != ownPrefix && irp.Navn.StartsWith(other + " "))
                        irp.Navn = ownPrefix + irp.Navn.Substring(other.Length);
            string pipeSerie = SerieAtPorts(br, pipes, componentPorts.Value, system, irp.DN1);
            if (!pipeSerie.IsNoE()) irp.Serie = pipeSerie;
        }
        /// <summary>
        /// One afgreningsstuds block (AFGRSTUDS) serves twin and enkelt mains alike, but its CSV row fixes
        /// System to Twin, so a stud on an enkelt main was exported as twin. The steel pipe of the main DN
        /// under the stud's seat on the main (its ports that are not the branch port) decides instead.
        /// A System the CSV leaves to a block parameter ("$System") is the drafter's and is kept.
        /// </summary>
        private static void SystemFromMainPipe(
            BlockReference br, IntersectResultComponent irp,
            (Polyline Pipe, Extents3d Ext)[] pipes, List<string> notes)
        {
            if (br.ReadDynamicCsvProperty(DynamicProperty.Type, false) != "Afgreningsstuds") return;
            string csvSystem = br.ReadDynamicCsvProperty(DynamicProperty.System, false);
            if (csvSystem.IsNoE() || csvSystem.StartsWith("$")) return;

            const double tol = componentPortTolerance;
            foreach (ComponentPort port in ComponentPorts.Read(br, br.GetTopTx()))
            {
                if (port.Role == ComponentPortRole.Branch) continue;
                Point3d pt = port.Position;
                foreach (var (pipe, ext) in pipes)
                {
                    if (pt.X < ext.MinPoint.X - tol || pt.X > ext.MaxPoint.X + tol ||
                        pt.Y < ext.MinPoint.Y - tol || pt.Y > ext.MaxPoint.Y + tol) continue;
                    if (GetPipeSystem(pipe) != PipeSystemEnum.Stål) continue;
                    if (GetPipeDN(pipe).ToString() != irp.DN1) continue;
                    if (pipe.GetClosestPointTo(pt, false).DistanceHorizontalTo(pt) > tol) continue;

                    string mainSystem = GetPipeType(pipe, true) switch
                    {
                        PipeTypeEnum.Twin => "Twin",
                        PipeTypeEnum.Enkelt => "Enkelt",
                        _ => "",
                    };
                    if (mainSystem.IsNoE()) continue;
                    if (mainSystem != irp.System)
                    {
                        notes.Add($"{br.RealName()} {br.Handle}: System {irp.System} fra CSV erstattet af {mainSystem} fra hovedrøret.");
                        irp.System = mainSystem;
                    }
                    return;
                }
            }
            notes.Add($"{br.RealName()} {br.Handle}: intet stålrør DN{irp.DN1} under studsen, System {irp.System} fra CSV beholdt.");
        }
        /// <summary>The block's own dynamic Serie parameter, or "" if it has none.</summary>
        private static string OwnSerie(BlockReference br)
        {
            if (!br.IsDynamicBlock) return "";
            foreach (DynamicBlockReferenceProperty p in br.DynamicBlockReferencePropertyCollection)
                if (p.PropertyName == "Serie") return p.Value?.ToString() ?? "";
            return "";
        }
        /// <summary>
        /// The serie of what the component is joined to at its ports, of one system: a pipe at a
        /// port first (of DN preferDn when given, else any), else a neighbouring component's port.
        /// "" when nothing of that system is found.
        /// </summary>
        private static string SerieAtPorts(
            BlockReference br,
            (Polyline Pipe, Extents3d Ext)[] pipes,
            (Point3d Port, string Serie, string SysNavn, Oid Owner)[] neighbours,
            PipeSystemEnum system,
            string preferDn)
        {
            const double tol = componentPortTolerance;
            var ports = br.GetAllEndPoints();
            string anyPipeSerie = "";
            foreach (Point3d port in ports)
                foreach (var (pipe, ext) in pipes)
                {
                    if (port.X < ext.MinPoint.X - tol || port.X > ext.MaxPoint.X + tol ||
                        port.Y < ext.MinPoint.Y - tol || port.Y > ext.MaxPoint.Y + tol) continue;
                    if (GetPipeSystem(pipe) != system) continue;
                    if (pipe.GetClosestPointTo(port, false).DistanceHorizontalTo(port) > tol) continue;

                    var series = GetPipeSeriesV2(pipe);
                    if (series == PipeSeriesEnum.Undefined) continue;
                    if (preferDn == null || GetPipeDN(pipe).ToString() == preferDn) return series.ToString();
                    if (anyPipeSerie.IsNoE()) anyPipeSerie = series.ToString();
                }
            if (!anyPipeSerie.IsNoE()) return anyPipeSerie;

            foreach (Point3d port in ports)
                foreach (var (nPort, nSerie, nSysNavn, owner) in neighbours)
                    if (owner != br.Id && nSysNavn == system.ToString() &&
                        nPort.DistanceHorizontalTo(port) <= tol) return nSerie;
            return "";
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
        /// <summary>
        /// The variable præbøjning (PRÆBØJN-90GR-VARIABEL-GLD) has a free angle V, but its TBLNavn in
        /// FJV Dynamiske Komponenter.csv is written for 90°. When a block's Vinkel is a parameter
        /// rather than a fixed value, the "90°" in its name is replaced by the angle actually drawn,
        /// so bends at other angles reach the tilbudsliste as their own component. 90° keeps its name.
        /// </summary>
        private static string NameWithBendAngle(BlockReference br, string navn)
        {
            if (navn.IsNoE() || !navn.Contains("90°")) return navn;
            string vinkelDef = br.ReadDynamicCsvProperty(DynamicProperty.Vinkel, false);
            if (!vinkelDef.StartsWith("$")) return navn;
            string vinkel = br.ReadDynamicCsvProperty(DynamicProperty.Vinkel, true);
            if (vinkel.IsNoE()) return navn;
            return navn.Replace("90°", vinkel + "°");
        }
        internal HashSet<IntersectResult> processintersectdataV2()
        {
            var results = gatherintersectdata(true, true, true);
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
                    //The tilbudsliste prices pipes per kanalmeter. Enkelt is drawn as a frem and a
                    //retur polyline, so the summed length is halved to give the route length.
                    Antal = g.Sum(x => x.Length) / (g.Key.System == "Enkelt" ? 2 : 1),
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

        /// <command>TBLEXPORTV2</command>
        /// <summary>
        /// Computes intersections between tender areas (layer "0-OMRÅDER-OK") and FJV objects, then
        /// exports a CSV whose rows paste straight into the tilbudsliste sheet:
        /// Egne noter;Vejklasse;Belægningstype;Komponent;Standardlængde;Materiale;DN;DN;Rørsystem;Serie;Antal.
        /// Egne noter is left empty for the etape to be written in the sheet. Pipes are summed by
        /// length (Antal in kanalmeter: enkelt frem + retur is halved) and split by standard delivery
        /// length; components are counted per piece.
        /// Welds without a Serie inherit it from the pipe or component they join. Bends with a free
        /// angle are named by the angle drawn ("… bøjning 45° 1.5m"). Blocks with no row in
        /// FJV Dynamiske Komponenter.csv are left out and listed; Materialeskift, plastic components
        /// and steel components without a serie are completed (FixComponentData). The file has no
        /// header row.
        /// Output is written to C:\Temp\IntersectResult.csv.
        /// </summary>
        /// <category>Tilbudsliste</category>
        [CommandMethod("TBLEXPORTV2")]
        public void tblexportv2()
        {
            var results = processintersectdataV2();
            if (results == null)
            {
                prdDbg("Received null instead of results. Aborting.");
                return;
            }

            #region Export Intersection Results
            StringBuilder sb = new StringBuilder();
            foreach (IntersectResult ir in results.OrderBy(x => x.IntersectType))
                sb.AppendLine(ir.ToV2Row());

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
}
