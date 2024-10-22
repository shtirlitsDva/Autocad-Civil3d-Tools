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
using Dreambuild.AutoCAD;

using static IntersectUtilities.Enums;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.Utils;
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.UtilsCommon.UtilsODData;
using static IntersectUtilities.UtilsCommon.Utils;

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
using System.Windows.Documents;
using IntersectUtilities.PipelineNetworkSystem;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        [CommandMethod("APGSPD")]
        [CommandMethod("APGATHERSURFACEPROFILEDATA")]
        public void gathersurfaceprofiledata()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var als = localDb.HashSetOfType<Alignment>(tx);

                    if (als.Count == 0)
                    {
                        prdDbg("No Alignments found in the drawing");
                        tx.Abort();
                        return;
                    }

                    string filePath = @"c:\Temp\SurfaceProfileData.txt";
                    using var w = new StreamWriter(filePath);

                    foreach (Alignment al in als.OrderBy(x => x.Name))
                    {
                        w.WriteLine($"Alignment: {al.Name}");

                        var pids = al.GetProfileIds();
                        Profile p = null;

                        foreach (Oid pid in pids)
                        {
                            Profile ptemp = pid.Go<Profile>(tx);
                            if (ptemp.Name.EndsWith("surface_P"))
                            {
                                p = ptemp;
                                break;
                            }
                        }

                        if (p == null)
                        {
                            prdDbg($"No surface profile found for {al.Name}");
                            continue;
                        }

                        ProfilePVICollection pvis = p.PVIs;

                        var query = pvis.Select(
                            pvis => new { pvis.RawStation, pvis.Elevation }).OrderBy(x => x.RawStation);

                        w.WriteLine(string.Join(";", query.Select(x => x.RawStation)));
                        w.WriteLine(string.Join(";", query.Select(x => x.Elevation)));
                        w.WriteLine();
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

        [CommandMethod("APGPLD")]
        [CommandMethod("APGATHERPIPELINEDATA")]
        public void gatherpipelinedata()
        {
            prdDbg("Dette skal køres i Længdeprofiler!");

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataManager.DataManager dm = new DataManager.DataManager(new DataReferencesOptions());
            Database fjvDb = dm.GetForRead("Fremtid");
            Transaction fjvTx = fjvDb.TransactionManager.StartTransaction();
            
            using Transaction tx = localDb.TransactionManager.StartTransaction();

            try
            {
                var ents = fjvDb.GetFjvEntities(fjvTx, true, false);
                var als = localDb.HashSetOfType<Alignment>(tx);

                PipelineNetwork pn = new PipelineNetwork();
                pn.CreatePipelineNetwork(ents, als);
                var sb = pn.CreateSizeArraysAndPrint();

                string filePath = @"c:\Temp\PipelineSizeData.txt";
                using var w = new StreamWriter(filePath);

                w.WriteLine(sb.ToString());
            }
            catch (System.Exception ex)
            {
                tx.Abort();
                fjvTx.Abort();
                fjvTx.Dispose();
                fjvDb.Dispose();
                prdDbg(ex);
                return;
            }
            tx.Commit();
            fjvTx.Commit();
            fjvTx.Dispose();
            fjvDb.Dispose();
        }
    }
}