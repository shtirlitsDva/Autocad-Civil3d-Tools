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

namespace IntersectUtilities.DRITBL
{
    public class DimensioneringExtension : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            //Document doc = Application.DocumentManager.MdiActiveDocument;
            //if (doc != null)
            //{
            //    SystemObjects.DynamicLinker.LoadModule(
            //        "AcMPolygonObj" + Application.Version.Major + ".dbx", false, false);
            //}

        }

        public void Terminate()
        {
        }
        #endregion
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

        internal HashSet<IntersectResult> gatherintersectdata()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataReferencesOptions dro = new DataReferencesOptions();
            Database fremDb = new Database(false, true);
            fremDb.ReadDwgFile(GetPathToDataFiles(dro.ProjectName, dro.EtapeName, "Fremtid"),
                FileOpenMode.OpenForReadAndAllShare, false, null);
            Transaction fremTx = fremDb.TransactionManager.StartTransaction();

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

                    System.Data.DataTable dt = CsvReader.ReadCsvToDataTable(
                        @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");

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
        internal HashSet<IntersectResult> processintersectdataCWO()
        {
            var results = gatherintersectdata();
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
                    x.Serie
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
                    Length = g.Sum(x => x.Length) // sum Length for each group
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
                    x.Serie
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
                    Count = g.Count() // count items for each group
                });

            HashSet<IntersectResult> allResults = new HashSet<IntersectResult>();
            allResults.UnionWith(pipeSummary);
            allResults.UnionWith(componentSummary);
            return allResults;
            #endregion
        }
        internal HashSet<IntersectResult> processintersectdataJJR()
        {
            var results = gatherintersectdata();
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
                    x.Serie
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
                    Length = g.Sum(x => x.Length) // sum Length for each group
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
                    x.Serie
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
                    Count = g.Count() // count items for each group
                });

            HashSet<IntersectResult> allResults = new HashSet<IntersectResult>();
            allResults.UnionWith(pipeSummary);
            allResults.UnionWith(componentSummary);
            return allResults;
            #endregion
        }

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
            sb.AppendLine("Vejklasse;Belægningstype;Komponent;DN1;DN2;Rørsystem;Serie;Længde/Antal");

            foreach (IntersectResult ir in results.OrderBy(x => x.IntersectType)) sb.AppendLine(ir.ToString(ExportType.JJR));

            File.WriteAllText(@"C:\Temp\IntersectResult.csv", sb.ToString(), Encoding.UTF8);
            prdDbg("I AM FINISH! (Results written to C:\\Temp\\IntersectResult.csv)");
            #endregion
        }
    }
}
