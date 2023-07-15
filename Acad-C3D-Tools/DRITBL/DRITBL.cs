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
using NetTopologySuite.IO;

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

        [CommandMethod("TBLEXPORTTBL")]
        public void tblexporttbl()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataReferencesOptions dro = new DataReferencesOptions();
            Database fremDb = new Database(false, true);
            fremDb.ReadDwgFile(GetPathToDataFiles(dro.ProjectName, dro.EtapeName, "Fremtid"),
                FileOpenMode.OpenForReadAndAllShare, false, null);
            Transaction fremTx = fremDb.TransactionManager.StartTransaction();

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
                        return;
                    }

                    HashSet<Polyline> pipes = fremDb.GetFjvPipes(fremTx);
                    HashSet<BlockReference> comps = fremDb.HashSetOfType<BlockReference>(fremTx);

                    #endregion

                    for (int i = 0; i < cplines.Count; i++)
                    {
                        Polyline plineI = cplines[i];
                        if (!plineI.Closed)
                            throw new System.Exception($"Polyline {plineI.Handle} is not closed!");
                        Polygon pgon = NTSConversion.ConvertClosedPlineToNTSPolygon(plineI);

                        foreach (Polyline pipe in pipes)
                        {
                            LineString lineString = NTSConversion.ConvertPlineToNTSLineString(pipe);
                            if (!pgon.Intersects(lineString)) continue;
                            Geometry intersect = pgon.Intersection(lineString);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    fremTx.Abort();
                    fremTx.Dispose();
                    fremDb.Dispose();
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
                fremTx.Abort();
                fremTx.Dispose();
                fremDb.Dispose();
            }
        }
    }
}
