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
using static IntersectUtilities.PipeSchedule;

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
using System.Windows.Documents;
using System.Security.Cryptography;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        [CommandMethod("BATCHPROCESSDRAWINGS")]
        public void processallsheets()
        {

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Dialog box for file list selection and path determination
                    string path = string.Empty;
                    OpenFileDialog dialog = new OpenFileDialog()
                    {
                        Title = "Choose txt file:",
                        DefaultExt = "txt",
                        Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*",
                        FilterIndex = 0
                    };
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        path = dialog.FileName;
                    }
                    else return;

                    List<string> fileList;
                    fileList = File.ReadAllLines(path).ToList();
                    path = Path.GetDirectoryName(path) + "\\";
                    #endregion

                    //!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                    //Project and etape selection object
                    //Comment out if not needed
                    //DataReferencesOptions dro = new DataReferencesOptions();

                    //Specific variables
                    int count = 0;

                    foreach (string fileName in fileList)
                    {
                        prdDbg(fileName);
                        string file = path + fileName;
                        using (Database xDb = new Database(false, true))
                        {
                            xDb.ReadDwgFile(file, System.IO.FileShare.ReadWrite, false, "");
                            using (Transaction xTx = xDb.TransactionManager.StartTransaction())
                            {
                                try
                                {
                                    //Bogus result to be able to comment stuff
                                    Result result = new Result();
                                    #region Correct line weight layers
                                    //string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";
                                    //System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");

                                    //LayerTable lt = xDb.LayerTableId.Go<LayerTable>(xDb.TransactionManager.TopTransaction);

                                    //HashSet<string> layerNames = dtKrydsninger.AsEnumerable().Select(x => x["Layer"].ToString()).ToHashSet();

                                    //foreach (string layerName in layerNames.Where(x => x.IsNotNoE()).OrderBy(x => x))
                                    //{
                                    //    if (lt.Has(layerName))
                                    //    {
                                    //        LayerTableRecord ltr = lt[layerName].Go<LayerTableRecord>(
                                    //            xDb.TransactionManager.TopTransaction, OpenMode.ForWrite);
                                    //        ltr.LineWeight = LineWeight.LineWeight013;
                                    //    }
                                    //}
                                    #endregion
                                    #region Stagger labels
                                    //staggerlabelsallmethod(xDb);
                                    #endregion
                                    #region Unhide specific layer in DB
                                    //LayerTable extLt = xDb.LayerTableId.Go<LayerTable>(xTx);
                                    //foreach (Oid oid in extLt)
                                    //{
                                    //    LayerTableRecord ltr = oid.Go<LayerTableRecord>(xTx);
                                    //    if (ltr.Name.Contains("|"))
                                    //    {
                                    //        var split = ltr.Name.Split('|');
                                    //        string xrefName = split[0];
                                    //        string layerName = split[1];
                                    //        if (xrefName == "FJV-Fremtid-E1" &&
                                    //            layerName.Contains("SVEJSEPKT-NR"))
                                    //        {
                                    //            prdDbg(ltr.Name);
                                    //            prdDbg(ltr.IsDependent.ToString());
                                    //            ltr.CheckOrOpenForWrite();
                                    //            prdDbg(ltr.IsOff.ToString());
                                    //            ltr.IsOff = false;
                                    //            prdDbg(ltr.IsOff.ToString());
                                    //        }
                                    //    }
                                    //}
                                    #endregion
                                    #region Set linetypes of xref
                                    //LayerTable extLt = xDb.LayerTableId.Go<LayerTable>(xTx);
                                    //foreach (Oid oid in extLt)
                                    //{
                                    //    LayerTableRecord ltr = oid.Go<LayerTableRecord>(xTx);
                                    //    if (ltr.Name.Contains("|"))
                                    //    {
                                    //        var split = ltr.Name.Split('|');
                                    //        string xrefName = split[0];
                                    //        string layerName = split[1];
                                    //        if (xrefName == "FJV-Fremtid-E1" &&
                                    //            layerName.Contains("TWIN"))
                                    //        {
                                    //            prdDbg(ltr.Name);
                                    //            prdDbg(ltr.IsDependent.ToString());
                                    //            LinetypeTable ltt = xDb.LinetypeTableId.Go<LinetypeTable>(xTx);
                                    //            Oid contId = ltt["Continuous"];
                                    //            ltr.CheckOrOpenForWrite();
                                    //            ltr.LinetypeObjectId = contId;
                                    //        }
                                    //    }
                                    //}
                                    #endregion
                                    #region CreateDetailing
                                    //createdetailingmethod(dro, xDb);
                                    #endregion
                                    #region Change xref layer
                                    //BlockTable bt = xTx.GetObject(xDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                                    //foreach (oid oid in bt)
                                    //{
                                    //    BlockTableRecord btr = xTx.GetObject(oid, OpenMode.ForWrite) as BlockTableRecord;
                                    //    if (btr.Name.Contains("_alignment"))
                                    //    {
                                    //        var ids = btr.GetBlockReferenceIds(true, true);
                                    //        foreach (oid brId in ids)
                                    //        {
                                    //            BlockReference br = brId.Go<BlockReference>(xTx, OpenMode.ForWrite);
                                    //            prdDbg(br.Name);
                                    //            if (br.Layer == "0") { prdDbg("Already in 0! Skipping..."); continue; }
                                    //            prdDbg("Was in: :" + br.Layer);
                                    //            br.Layer = "0";
                                    //            prdDbg("Moved to: " + br.Layer);
                                    //            System.Windows.Forms.Application.DoEvents();
                                    //        }
                                    //    }
                                    //} 
                                    #endregion
                                    #region Fix midt profile style
                                    //CivilDocument extDoc = CivilDocument.GetCivilDocument(xDb);
                                    //var psc = extDoc.Styles.ProfileStyles;
                                    //ProfileStyle ps = psc["PROFIL STYLE MGO MIDT"].Go<ProfileStyle>(xTx);
                                    //ps.CheckOrOpenForWrite();

                                    //DisplayStyle ds;
                                    //ds = ps.GetDisplayStyleProfile(ProfileDisplayStyleProfileType.Line);
                                    //ds.LinetypeScale = 10;
                                    //ds.Lineweight = LineWeight.LineWeight000;

                                    //ds = ps.GetDisplayStyleProfile(ProfileDisplayStyleProfileType.Curve);
                                    //ds.LinetypeScale = 10;
                                    //ds.Lineweight = LineWeight.LineWeight000;

                                    //ds = ps.GetDisplayStyleProfile(ProfileDisplayStyleProfileType.SymmetricalParabola);
                                    //ds.LinetypeScale = 10;
                                    //ds.Lineweight = LineWeight.LineWeight000;
                                    #endregion
                                    #region List all VF numbers

                                    #endregion
                                    //Fix longitudinal profiles
                                    //result = fixlongitudinalprofiles(xDb);

                                    //List viewFrame numbers
                                    //result = listvfnumbers(xDb);

                                    //Renumber viewframes
                                    //result = renumbervfs(xDb, ref count);

                                    //Correct field in blocks
                                    //result = correctfieldinblock(xDb);

                                    //Hide alignments in files (run hal)
                                    //result = hidealignments(xDb);

                                    //Set alignment to NO SHOW
                                    //result = alignmentsnoshow(xDb);

                                    //Freeze layers in viewport
                                    result = vpfreezelayers(xDb);

                                    switch (result.Status)
                                    {
                                        case ResultStatus.OK:
                                            break;
                                        case ResultStatus.FatalError:
                                            AbortGracefully(
                                            new[] { xTx },
                                            new[] { xDb },
                                            result.ErrorMsg);
                                            tx.Abort();
                                            return;
                                        case ResultStatus.SoftError:
                                            //No implementation yet
                                            break;
                                        default:
                                            break;
                                    }
                                }
                                catch (System.Exception ex)
                                {
                                    prdDbg(ex);
                                    xTx.Abort();
                                    xDb.Dispose();
                                    throw;
                                }
                                xTx.Commit();
                            }
                            xDb.SaveAs(xDb.Filename, true, DwgVersion.Newest, xDb.SecurityParameters);
                        }
                        System.Windows.Forms.Application.DoEvents();
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        private Result fixlongitudinalprofiles(Database xDb)
        {
            //Used when no pipe profiles have been drawn to make default profile views look good
            CivilDocument cDoc = CivilDocument.GetCivilDocument(xDb);
            Transaction xTx = xDb.TransactionManager.TopTransaction;
            var als = xDb.HashSetOfType<Alignment>(xTx);
            foreach (Alignment al in als)
            {
                var pIds = al.GetProfileIds();
                var pvIds = al.GetProfileViewIds();

                Profile pSurface = null;
                foreach (Oid oid in pIds)
                {
                    Profile pt = oid.Go<Profile>(xTx);
                    if (pt.Name == $"{al.Name}_surface_P") pSurface = pt;
                }
                if (pSurface == null)
                {
                    return new Result(ResultStatus.FatalError, $"No profile named {al.Name}_surface_P found!");
                }
                else prdDbg($"\nProfile {pSurface.Name} found!");

                foreach (ProfileView pv in pvIds.Entities<ProfileView>(xTx))
                {
                    #region Determine profile top and bottom elevations
                    double pvStStart = pv.StationStart;
                    double pvStEnd = pv.StationEnd;

                    int nrOfIntervals = (int)((pvStEnd - pvStStart) / 0.25);
                    double delta = (pvStEnd - pvStStart) / nrOfIntervals;
                    HashSet<double> topElevs = new HashSet<double>();

                    for (int j = 0; j < nrOfIntervals + 1; j++)
                    {
                        double topTestEl;
                        try
                        {
                            topTestEl = pSurface.ElevationAt(pvStStart + delta * j);
                        }
                        catch (System.Exception)
                        {
                            prdDbg($"\nTop profile at {pvStStart + delta * j} threw an exception! " +
                                $"PV: {pv.StationStart}-{pv.StationEnd}.");
                            continue;
                        }
                        topElevs.Add(topTestEl);
                    }

                    double maxEl = topElevs.Max();
                    double minEl = topElevs.Min();

                    prdDbg($"\nElevations of surf.p.> Max: {Math.Round(maxEl, 2)} | Min: {Math.Round(minEl, 2)}");

                    //Set the elevations
                    pv.CheckOrOpenForWrite();
                    pv.ElevationRangeMode = ElevationRangeType.UserSpecified;
                    pv.ElevationMax = Math.Ceiling(maxEl);
                    pv.ElevationMin = Math.Floor(minEl) - 3.0;
                    #endregion

                    Oid sId = cDoc.Styles.ProfileViewStyles["PROFILE VIEW L TO R 1:250:100"];
                    pv.CheckOrOpenForWrite();
                    pv.StyleId = sId;
                }

                //Set profile style
                xDb.CheckOrCreateLayer("0_TERRAIN_PROFILE", 34);

                Oid profileStyleId = cDoc.Styles.ProfileStyles["Terræn"];
                pSurface.CheckOrOpenForWrite();
                pSurface.StyleId = profileStyleId;
            }

            return new Result();
        }
        private Result listvfnumbers(Database xDb)
        {
            Transaction xTx = xDb.TransactionManager.TopTransaction;

            ViewFrameGroup vfg = xDb.ListOfType<ViewFrameGroup>(xTx).FirstOrDefault();
            if (vfg != null)
            {
                var ids = vfg.GetViewFrameIds();
                var ents = ids.Entities<ViewFrame>(xTx);
                foreach (var item in ents)
                {
                    prdDbg(item.Name);
                }
            }
            return new Result();
        }
        private Result renumbervfs(Database xDb, ref int count)
        {
            Transaction xTx = xDb.TransactionManager.TopTransaction;
            
            ViewFrameGroup vfg = xDb.ListOfType<ViewFrameGroup>(xTx).FirstOrDefault();
            if (vfg != null)
            {
                var ids = vfg.GetViewFrameIds();
                var ents = ids.Entities<ViewFrame>(xTx);

                Random rnd = new Random();
                foreach (var item in ents)
                {
                    item.CheckOrOpenForWrite();
                    item.Name = rnd.Next(1, 999999).ToString("000000");
                }

                foreach (var item in ents)
                {
                    count++;
                    string previousName = item.Name;
                    item.CheckOrOpenForWrite();
                    item.Name = count.ToString("000");
                    prdDbg($"{previousName} -> {item.Name}");
                }
            }
            return new Result();
        }
        private Result correctfieldinblock(Database xDb)
        {
            Transaction xTx = xDb.TransactionManager.TopTransaction;

            HashSet<BlockReference> brs = xDb
                .GetBlockReferenceByName("Tegningsskilt");

            BlockTableRecord btr = brs
                .First()
                .BlockTableRecord
                .Go<BlockTableRecord>(xTx);

            foreach (Oid oid in btr)
            {
                if (!oid.IsDerivedFrom<AttributeDefinition>()) continue;
                AttributeDefinition attDef = oid.Go<AttributeDefinition>(xTx);
                if (attDef == null) continue;
                if (attDef.Tag != "SAG2") continue;

                attDef.CheckOrOpenForWrite();
                attDef.TextString = "%<\\AcSm SheetSet.Description \\f \"%tc1\">%";
            }

            foreach (var br in brs)
            {
                foreach (Oid oid in br.AttributeCollection)
                {
                    AttributeReference ar = oid.Go<AttributeReference>(xTx);
                    if (ar.Tag == "SAG2")
                    {
                        ar.CheckOrOpenForWrite();
                        ar.TextString = "%<\\AcSm SheetSet.Description \\f \"%tc1\">%";
                    }
                }
            }

            return new Result();
        }
        private Result hidealignments(Database xDb)
        {
            Transaction xTx = xDb.TransactionManager.TopTransaction;

            var cDoc = CivilDocument.GetCivilDocument(xDb);
            Oid alStyle = cDoc.Styles.AlignmentStyles["FJV TRACE NO SHOW"];
            //Oid labelSetStyle = cDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles["STD 20-5"];
            Oid labelSetStyle = cDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles["_No Labels"];
            HashSet<Alignment> als = xDb.HashSetOfType<Alignment>(xTx);

            foreach (Alignment al in als)
            {
                al.CheckOrOpenForWrite();
                al.StyleId = alStyle;
                al.ImportLabelSet(labelSetStyle);
            }
            return new Result();
        }
        private Result alignmentsnoshow(Database xDb)
        {
            Transaction xTx = xDb.TransactionManager.TopTransaction;

            var cDoc = CivilDocument.GetCivilDocument(xDb);
            Oid alStyle = cDoc.Styles.AlignmentStyles["FJV TRACE NO SHOW"];

            HashSet<Alignment> als = xDb.HashSetOfType<Alignment>(xTx);

            foreach (Alignment al in als)
            {
                al.CheckOrOpenForWrite();
                al.StyleId = alStyle;
            }
            return new Result();
        }
        private Result vpfreezelayers(Database xDb)
        {
            Transaction xTx = xDb.TransactionManager.TopTransaction;

            HashSet<ProfileProjectionLabel> labels =
                        xDb.HashSetOfType<ProfileProjectionLabel>(xTx);
            var layerNames = labels.Select(x => x.Layer).ToHashSet();
            ObjectIdCollection oids = new ObjectIdCollection();
            LayerTable lt = xDb.LayerTableId.Go<LayerTable>(xTx);
            foreach (string name in layerNames) oids.Add(lt[name]);
            prdDbg($"Number of layers: {layerNames.Count}");
            prdDbg($"Number of oids: {oids.Count}");

            DBDictionary layoutDict = xDb.LayoutDictionaryId.Go<DBDictionary>(xTx);
            var enumerator = layoutDict.GetEnumerator();
            while (enumerator.MoveNext())
            {
                DBDictionaryEntry item = enumerator.Current;
                prdDbg(item.Key);
                if (item.Key == "Model")
                {
                    prdDbg("Skipping model...");
                    continue;
                }
                Layout layout = item.Value.Go<Layout>(xTx);
                BlockTableRecord layBlock = layout.BlockTableRecordId.Go<BlockTableRecord>(xTx);

                foreach (Oid id in layBlock)
                {
                    if (id.IsDerivedFrom<Viewport>())
                    {
                        Viewport vp = id.Go<Viewport>(xTx);
                        //Truncate doubles to whole numebers for easier comparison
                        int centerX = (int)vp.CenterPoint.X;
                        int centerY = (int)vp.CenterPoint.Y;
                        if (centerX == 958 && centerY == 193)
                        {
                            prdDbg("Found minikort viewport!");
                            ObjectIdCollection notFrozenIds = new ObjectIdCollection();
                            foreach (Oid oid in oids)
                            {
                                if (vp.IsLayerFrozenInViewport(oid)) continue;
                                notFrozenIds.Add(oid);
                            }
                            prdDbg($"Number of not frozen layers: {notFrozenIds.Count}");
                            if (notFrozenIds.Count == 0) continue;

                            vp.CheckOrOpenForWrite();
                            vp.FreezeLayersInViewport(notFrozenIds.GetEnumerator());
                        }
                    }
                }
            }
            return new Result();
        }
        private enum ResultStatus
        {
            OK,
            FatalError, //Execution of processing must stop
            SoftError //Exection may continue, changes to current drawing aborted
        }
        private class Result
        {
            internal ResultStatus Status = ResultStatus.OK;
            internal string ErrorMsg;
            internal Result() { }
            internal Result(ResultStatus status, string errorMsg)
            {
                Status = status;
                ErrorMsg = errorMsg;
            }
        }
    }
}