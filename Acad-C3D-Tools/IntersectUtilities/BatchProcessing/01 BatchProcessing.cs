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
using Result = IntersectUtilities.Result;
using System.Diagnostics;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        [CommandMethod("BPUI")]
        [CommandMethod("BATCHPROCESSDRAWINGS")]
        public void processallsheets()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            //Ref variables
            Counter counter = new Counter();

            BatchProccessingForm bpf = new BatchProccessingForm(counter);
            bpf.ShowDialog();
            if (bpf.MethodsToExecute == null ||
                bpf.MethodsToExecute.Count == 0)
            {
                return;
            }

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

            Stopwatch sw = new Stopwatch();
            sw.Start();

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    //!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                    //Project and etape selection object
                    //Comment out if not needed
                    //DataReferencesOptions dro = new DataReferencesOptions();
                    foreach (string fileName in fileList)
                    {
                        //prdDbg(fileName);
                        string file = path + fileName;
                        if (!File.Exists(file)) continue;
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

                                    foreach (var method in bpf.MethodsToExecute)
                                    {
                                        var args = bpf.ArgsToExecute[method.Name];
                                        args[0] = xDb;

                                        result = (Result)method.Invoke(null, args);

                                        switch (result.Status)
                                        {
                                            case ResultStatus.OK:
                                                break;
                                            case ResultStatus.FatalError:
                                                AbortGracefully(
                                                result.ErrorMsg,
                                                xDb);
                                                tx.Abort();
                                                return;
                                            case ResultStatus.SoftError:
                                                //No implementation yet
                                                break;
                                            default:
                                                break;
                                        }
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
                    prdDbg(ex);
                    return;
                }
                finally
                {
                    sw.Stop();
                    prdDbg($"Total processing time: {sw.Elapsed}");
                }
                tx.Commit();
            }
        }
        #region Old Code
        //private Result fixler2dplotstyles(Database xDb)
        //{
        //    Transaction xTx = xDb.TransactionManager.TopTransaction;

        //    #region Set linetypes of xref
        //    LayerTable extLt = xDb.LayerTableId.Go<LayerTable>(xTx);
        //    foreach (Oid oid in extLt)
        //    {
        //        LayerTableRecord ltr = oid.Go<LayerTableRecord>(xTx);
        //        if (ltr.Name.Contains("|"))
        //        {
        //            var split = ltr.Name.Split('|');
        //            string xrefName = split[0];
        //            string layerName = split[1];
        //            if (xrefName == "LER_2D")
        //            {
        //                prdDbg(ltr.Name);
        //                ltr.CheckOrOpenForWrite();
        //                ltr.PlotStyleName = "Nedtonet 50%";
        //            }
        //        }
        //    }
        //    #endregion

        //    return new Result();
        //}
        //private Result fixdrawings(Database xDb)
        //{
        //    try
        //    {
        //        Transaction xTx = xDb.TransactionManager.TopTransaction;
        //        CivilDocument civilDoc = CivilDocument.GetCivilDocument(xDb);
        //        #region Fix profile styles
        //        #region Profile line weight and ltscale
        //        var psc = civilDoc.Styles.ProfileStyles;
        //        ProfileStyle ps = psc["PROFIL STYLE MGO MIDT"].Go<ProfileStyle>(xTx);
        //        ps.CheckOrOpenForWrite();

        //        DisplayStyle ds;
        //        ds = ps.GetDisplayStyleProfile(ProfileDisplayStyleProfileType.Line);
        //        ds.LinetypeScale = 10;
        //        ds.Lineweight = LineWeight.LineWeight000;

        //        ds = ps.GetDisplayStyleProfile(ProfileDisplayStyleProfileType.Curve);
        //        ds.LinetypeScale = 10;
        //        ds.Lineweight = LineWeight.LineWeight000;

        //        ds = ps.GetDisplayStyleProfile(ProfileDisplayStyleProfileType.SymmetricalParabola);
        //        ds.LinetypeScale = 10;
        //        ds.Lineweight = LineWeight.LineWeight000;
        //        #endregion
        //        Oid pPipeStyleKantId = civilDoc.Styles.ProfileStyles["PROFIL STYLE MGO KANT"];
        //        Oid pPipeStyleMidtId = civilDoc.Styles.ProfileStyles["PROFIL STYLE MGO MIDT"];
        //        Oid crestCurveLabelId =
        //            civilDoc.Styles.LabelStyles.ProfileLabelStyles.CurveLabelStyles["Radius Crest"];
        //        Oid sagCurveLabelId =
        //            civilDoc.Styles.LabelStyles.ProfileLabelStyles.CurveLabelStyles["Radius Sag"];

        //        HashSet<ProfileView> pvs = xDb.HashSetOfType<ProfileView>(xTx);
        //        HashSet<Alignment> als = xDb.HashSetOfType<Alignment>(xTx);
        //        foreach (Alignment al in als)
        //        {
        //            ObjectIdCollection pIds = al.GetProfileIds();
        //            Oid surfaceProfileId = Oid.Null;
        //            Oid topProfileId = Oid.Null;
        //            foreach (Oid oid in pIds)
        //            {
        //                Profile p = oid.Go<Profile>(xTx);
        //                if (p.Name == $"{al.Name}_surface_P")
        //                {
        //                    surfaceProfileId = p.Id;
        //                    continue;
        //                }
        //                else if (
        //                    p.Name.Contains("TOP") ||
        //                    p.Name.Contains("BUND"))
        //                {
        //                    p.CheckOrOpenForWrite();
        //                    p.StyleId = pPipeStyleKantId;
        //                    if (p.Name.Contains("TOP")) topProfileId = p.Id;
        //                }
        //                else if (p.Name.Contains("MIDT"))
        //                {
        //                    p.StyleId = pPipeStyleMidtId;

        //                    foreach (ProfileView pv in pvs)
        //                    {
        //                        pv.CheckOrOpenForWrite();
        //                        ProfileCrestCurveLabelGroup.Create(pv.ObjectId, p.ObjectId, crestCurveLabelId);
        //                        ProfileSagCurveLabelGroup.Create(pv.ObjectId, p.ObjectId, sagCurveLabelId);
        //                    }
        //                }
        //            }

        //            foreach (ProfileView pv in pvs)
        //            {
        //                ProfileViewBandSet pvbs = pv.Bands;
        //                ProfileViewBandItemCollection pvbic = pvbs.GetBottomBandItems();

        //                for (int i = 0; i < pvbic.Count; i++)
        //                {
        //                    if (i == 0)
        //                    {
        //                        ProfileViewBandItem pvbi = pvbic[i];
        //                        pvbi.Profile1Id = surfaceProfileId;
        //                        pvbi.Profile2Id = topProfileId;
        //                        pvbi.LabelAtStartStation = true;
        //                        pvbi.LabelAtEndStation = true;
        //                    }
        //                }
        //                pvbs.SetBottomBandItems(pvbic);
        //            }
        //        }
        //        #endregion
        //    }
        //    catch (System.Exception ex)
        //    {
        //        return new Result(ResultStatus.FatalError, ex.ToString());
        //    }

        //    return new Result();
        //}
        //private Result detachdwg(Database xDb)
        //{
        //    Transaction xTx = xDb.TransactionManager.TopTransaction;

        //    BlockTable bt = xDb.BlockTableId.Go<BlockTable>(xTx, OpenMode.ForRead);
        //    var modelSpace = xDb.GetModelspaceForWrite();
        //    DrawOrderTable dot = modelSpace.DrawOrderTableId.Go<DrawOrderTable>(
        //        xTx, OpenMode.ForWrite);

        //    foreach (Oid oid in bt)
        //    {
        //        BlockTableRecord btr = xTx.GetObject(oid, OpenMode.ForWrite) as BlockTableRecord;
        //        //if (btr.Name.Contains("_alignment"))
        //        if (btr.Name == "1.1 Alignment" && btr.IsFromExternalReference)
        //        {
        //            prdDbg("Found alignment xref!");
        //            xDb.DetachXref(btr.ObjectId);
        //        }

        //        if (btr.Name == "1.1 Ortofoto" && btr.IsFromExternalReference)
        //        {
        //            prdDbg("Found ortofoto!");
        //            var ids = btr.GetBlockReferenceIds(true, false);
        //            foreach (Oid id in ids)
        //            {
        //                prdDbg("Sending to bottom!");
        //                dot.MoveToBottom(new ObjectIdCollection() { id });
        //            }
        //        }
        //    }

        //    return new Result();
        //}
        ///// <summary>
        ///// For batch processing.
        ///// Recreates detailing across drawings.
        ///// </summary>
        //private Result createdetailing(Database xDb, DataReferencesOptions dro)
        //{
        //    //Recreate detailing in affected drawings
        //    deletedetailingmethod(xDb);
        //    createdetailingmethod(dro, xDb);
        //    return new Result();
        //}
        //private Result createreferencetopipeprofiles(Database xDb)
        //{
        //    //Used when sheets were created before pipe profiles were available
        //    //Finds those profiles and creates a reference to them in drawing
        //    //Then it deletes the detailing and recreates it
        //    Transaction xTx = xDb.TransactionManager.TopTransaction;
        //    var als = xDb.HashSetOfType<Alignment>(xTx);

        //    Regex reg1 = new Regex(@"(?<number>\d{2,3}?\s)");

        //    bool isValidCreation = false;
        //    DataShortcuts.DataShortcutManager sm = DataShortcuts.CreateDataShortcutManager(ref isValidCreation);
        //    if (isValidCreation != true)
        //    {
        //        prdDbg("DataShortcutManager failed to be created!");
        //        return new Result(ResultStatus.FatalError, "DataShortcutManager failed to be created!");
        //    }
        //    int publishedCount = sm.GetPublishedItemsCount();

        //    foreach (Alignment al in als)
        //    {
        //        string number = reg1.Match(al.Name).Groups["number"].Value;
        //        prdDbg($"{al.Name} -> {number}");

        //        for (int i = 0; i < publishedCount; i++)
        //        {
        //            DataShortcuts.DataShortcutManager.PublishedItem item =
        //                sm.GetPublishedItemAt(i);

        //            if (item.DSEntityType == DataShortcutEntityType.Alignment)
        //            {
        //                if (item.Name.StartsWith(number))
        //                {
        //                    var items = GetItemsByPipelineNumber(sm, number);

        //                    foreach (int idx in items)
        //                    {
        //                        DataShortcuts.DataShortcutManager.PublishedItem entity =
        //                            sm.GetPublishedItemAt(idx);

        //                        if (entity.DSEntityType == DataShortcutEntityType.Alignment ||
        //                            entity.Name.Contains("surface")) continue;

        //                        sm.CreateReference(idx, xDb);
        //                    }
        //                }
        //            }
        //        }

        //        IEnumerable<int> GetItemsByPipelineNumber(
        //            DataShortcuts.DataShortcutManager dsMan, string pipelineNumber)
        //        {
        //            int count = dsMan.GetPublishedItemsCount();

        //            for (int j = 0; j < count; j++)
        //            {
        //                string name = dsMan.GetPublishedItemAt(j).Name;
        //                if (name.StartsWith(pipelineNumber)) yield return j;
        //            }
        //        }
        //    }

        //    sm.Dispose();

        //    return new Result();
        //}
        //private Result fixlongitudinalprofiles(Database xDb)
        //{
        //    //Used when no pipe profiles have been drawn to make default profile views look good
        //    CivilDocument cDoc = CivilDocument.GetCivilDocument(xDb);
        //    Transaction xTx = xDb.TransactionManager.TopTransaction;
        //    var als = xDb.HashSetOfType<Alignment>(xTx);
        //    foreach (Alignment al in als)
        //    {
        //        var pIds = al.GetProfileIds();
        //        var pvIds = al.GetProfileViewIds();

        //        Profile pSurface = null;
        //        foreach (Oid oid in pIds)
        //        {
        //            Profile pt = oid.Go<Profile>(xTx);
        //            if (pt.Name == $"{al.Name}_surface_P") pSurface = pt;
        //        }
        //        if (pSurface == null)
        //        {
        //            return new Result(ResultStatus.FatalError, $"No profile named {al.Name}_surface_P found!");
        //        }
        //        else prdDbg($"\nProfile {pSurface.Name} found!");

        //        foreach (ProfileView pv in pvIds.Entities<ProfileView>(xTx))
        //        {
        //            #region Determine profile top and bottom elevations
        //            double pvStStart = pv.StationStart;
        //            double pvStEnd = pv.StationEnd;

        //            int nrOfIntervals = (int)((pvStEnd - pvStStart) / 0.25);
        //            double delta = (pvStEnd - pvStStart) / nrOfIntervals;
        //            HashSet<double> topElevs = new HashSet<double>();

        //            for (int j = 0; j < nrOfIntervals + 1; j++)
        //            {
        //                double topTestEl;
        //                try
        //                {
        //                    topTestEl = pSurface.ElevationAt(pvStStart + delta * j);
        //                }
        //                catch (System.Exception)
        //                {
        //                    prdDbg($"\nTop profile at {pvStStart + delta * j} threw an exception! " +
        //                        $"PV: {pv.StationStart}-{pv.StationEnd}.");
        //                    continue;
        //                }
        //                topElevs.Add(topTestEl);
        //            }

        //            double maxEl = topElevs.Max();
        //            double minEl = topElevs.Min();

        //            prdDbg($"\nElevations of surf.p.> Max: {Math.Round(maxEl, 2)} | Min: {Math.Round(minEl, 2)}");

        //            //Set the elevations
        //            pv.CheckOrOpenForWrite();
        //            pv.ElevationRangeMode = ElevationRangeType.UserSpecified;
        //            pv.ElevationMax = Math.Ceiling(maxEl);
        //            pv.ElevationMin = Math.Floor(minEl) - 3.0;
        //            #endregion

        //            Oid sId = cDoc.Styles.ProfileViewStyles["PROFILE VIEW L TO R 1:250:100"];
        //            pv.CheckOrOpenForWrite();
        //            pv.StyleId = sId;
        //        }

        //        //Set profile style
        //        xDb.CheckOrCreateLayer("0_TERRAIN_PROFILE", 34);

        //        Oid profileStyleId = cDoc.Styles.ProfileStyles["Terræn"];
        //        pSurface.CheckOrOpenForWrite();
        //        pSurface.StyleId = profileStyleId;
        //    }

        //    return new Result();
        //}
        //private Result listvfnumbers(Database xDb, ref int count)
        //{
        //    Transaction xTx = xDb.TransactionManager.TopTransaction;

        //    ViewFrameGroup vfg = xDb.ListOfType<ViewFrameGroup>(xTx).FirstOrDefault();
        //    if (vfg != null)
        //    {
        //        var ids = vfg.GetViewFrameIds();
        //        var ents = ids.Entities<ViewFrame>(xTx);
        //        foreach (var item in ents)
        //        {
        //            count++;
        //            int vfNumber = Convert.ToInt32(item.Name);
        //            if (count != vfNumber) prdDbg(item.Name + " <- Fejl! Skal være " + count + ".");
        //            else prdDbg(item.Name);
        //        }
        //    }
        //    return new Result();
        //}
        //private Result renumbervfs(Database xDb, ref int count)
        //{
        //    Transaction xTx = xDb.TransactionManager.TopTransaction;

        //    ViewFrameGroup vfg = xDb.ListOfType<ViewFrameGroup>(xTx).FirstOrDefault();
        //    if (vfg != null)
        //    {
        //        var ids = vfg.GetViewFrameIds();
        //        var ents = ids.Entities<ViewFrame>(xTx);

        //        Dictionary<Oid, string> oNames = new Dictionary<Oid, string>();

        //        Random rnd = new Random();
        //        foreach (var item in ents)
        //        {
        //            oNames.Add(item.Id, item.Name);

        //            item.CheckOrOpenForWrite();
        //            item.Name = rnd.Next(1, 999999).ToString("000000");
        //        }

        //        foreach (var item in ents)
        //        {
        //            count++;
        //            string previousName = item.Name;
        //            item.CheckOrOpenForWrite();
        //            item.Name = count.ToString("000");
        //            prdDbg($"{oNames[item.Id]} -> {item.Name}");
        //        }
        //    }
        //    return new Result();
        //}
        

        //    return new Result();
        //}
        //private Result hidealignments(Database xDb)
        //{
        //    Transaction xTx = xDb.TransactionManager.TopTransaction;

        //    var cDoc = CivilDocument.GetCivilDocument(xDb);
        //    Oid alStyle = cDoc.Styles.AlignmentStyles["FJV TRACE NO SHOW"];
        //    //Oid labelSetStyle = cDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles["STD 20-5"];
        //    Oid labelSetStyle = cDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles["_No Labels"];
        //    HashSet<Alignment> als = xDb.HashSetOfType<Alignment>(xTx);

        //    foreach (Alignment al in als)
        //    {
        //        al.CheckOrOpenForWrite();
        //        al.StyleId = alStyle;
        //        al.ImportLabelSet(labelSetStyle);
        //    }
        //    return new Result();
        //}
        //private Result alignmentsnoshow(Database xDb)
        //{
        //    Transaction xTx = xDb.TransactionManager.TopTransaction;

        //    var cDoc = CivilDocument.GetCivilDocument(xDb);
        //    Oid alStyle = cDoc.Styles.AlignmentStyles["FJV TRACE NO SHOW"];

        //    HashSet<Alignment> als = xDb.HashSetOfType<Alignment>(xTx);

        //    foreach (Alignment al in als)
        //    {
        //        al.CheckOrOpenForWrite();
        //        al.StyleId = alStyle;
        //    }
        //    return new Result();
        //}
        //private Result alignmentsnoshowandlabels(Database xDb)
        //{
        //    Transaction xTx = xDb.TransactionManager.TopTransaction;

        //    var cDoc = CivilDocument.GetCivilDocument(xDb);
        //    Oid alStyle = cDoc.Styles.AlignmentStyles["FJV TRACE NO SHOW"];
        //    Oid labelSetStyle = cDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles["STD 20-5"];

        //    HashSet<Alignment> als = xDb.HashSetOfType<Alignment>(xTx);

        //    foreach (Alignment al in als)
        //    {
        //        al.CheckOrOpenForWrite();
        //        al.StyleId = alStyle;
        //        al.ImportLabelSet(labelSetStyle);
        //    }
        //    return new Result();
        //}
        #endregion
    }

    public static class BatchProcesses
    {
        [MethodDescription(
            "Fix Ler2D plot styles",
            "Sætter alle lag fra angivet Ler 2D\n" +
            "xref til plotstyle Nedtonet 50%",
            new string[1] { "Ler2D Xref Navn (uden .dwg)" })]
        public static Result fixler2dplotstyles(Database xDb, string ler2dXrefName)
        {
            Transaction xTx = xDb.TransactionManager.TopTransaction;

            #region Set linetypes of xref
            LayerTable extLt = xDb.LayerTableId.Go<LayerTable>(xTx);
            foreach (Oid oid in extLt)
            {
                LayerTableRecord ltr = oid.Go<LayerTableRecord>(xTx);
                if (ltr.Name.Contains("|"))
                {
                    var split = ltr.Name.Split('|');
                    string xrefName = split[0];
                    string layerName = split[1];
                    if (xrefName == ler2dXrefName)
                    {
                        prdDbg(ltr.Name);
                        ltr.CheckOrOpenForWrite();
                        ltr.PlotStyleName = "Nedtonet 50%";
                    }
                }
            }
            #endregion

            return new Result();
        }
        [MethodDescription(
            "Fix profile styles",
            "Sætter alle profil styles til DRI\n" +
            "standard og sætter profiles views\n" +
            "til DRI standard")]
        public static Result fixdrawings(Database xDb)
        {
            try
            {
                Transaction xTx = xDb.TransactionManager.TopTransaction;
                CivilDocument civilDoc = CivilDocument.GetCivilDocument(xDb);
                #region Fix profile styles
                #region Profile line weight and ltscale
                var psc = civilDoc.Styles.ProfileStyles;
                ProfileStyle ps = psc["PROFIL STYLE MGO MIDT"].Go<ProfileStyle>(xTx);
                ps.CheckOrOpenForWrite();

                DisplayStyle ds;
                ds = ps.GetDisplayStyleProfile(ProfileDisplayStyleProfileType.Line);
                ds.LinetypeScale = 10;
                ds.Lineweight = LineWeight.LineWeight000;

                ds = ps.GetDisplayStyleProfile(ProfileDisplayStyleProfileType.Curve);
                ds.LinetypeScale = 10;
                ds.Lineweight = LineWeight.LineWeight000;

                ds = ps.GetDisplayStyleProfile(ProfileDisplayStyleProfileType.SymmetricalParabola);
                ds.LinetypeScale = 10;
                ds.Lineweight = LineWeight.LineWeight000;
                #endregion
                Oid pPipeStyleKantId = civilDoc.Styles.ProfileStyles["PROFIL STYLE MGO KANT"];
                Oid pPipeStyleMidtId = civilDoc.Styles.ProfileStyles["PROFIL STYLE MGO MIDT"];
                Oid crestCurveLabelId =
                    civilDoc.Styles.LabelStyles.ProfileLabelStyles.CurveLabelStyles["Radius Crest"];
                Oid sagCurveLabelId =
                    civilDoc.Styles.LabelStyles.ProfileLabelStyles.CurveLabelStyles["Radius Sag"];

                HashSet<ProfileView> pvs = xDb.HashSetOfType<ProfileView>(xTx);
                HashSet<Alignment> als = xDb.HashSetOfType<Alignment>(xTx);
                foreach (Alignment al in als)
                {
                    ObjectIdCollection pIds = al.GetProfileIds();
                    Oid surfaceProfileId = Oid.Null;
                    Oid topProfileId = Oid.Null;
                    foreach (Oid oid in pIds)
                    {
                        Profile p = oid.Go<Profile>(xTx);
                        if (p.Name == $"{al.Name}_surface_P")
                        {
                            surfaceProfileId = p.Id;
                            continue;
                        }
                        else if (
                            p.Name.Contains("TOP") ||
                            p.Name.Contains("BUND"))
                        {
                            p.CheckOrOpenForWrite();
                            p.StyleId = pPipeStyleKantId;
                            if (p.Name.Contains("TOP")) topProfileId = p.Id;
                        }
                        else if (p.Name.Contains("MIDT"))
                        {
                            p.StyleId = pPipeStyleMidtId;

                            foreach (ProfileView pv in pvs)
                            {
                                pv.CheckOrOpenForWrite();
                                ProfileCrestCurveLabelGroup.Create(pv.ObjectId, p.ObjectId, crestCurveLabelId);
                                ProfileSagCurveLabelGroup.Create(pv.ObjectId, p.ObjectId, sagCurveLabelId);
                            }
                        }
                    }

                    foreach (ProfileView pv in pvs)
                    {
                        ProfileViewBandSet pvbs = pv.Bands;
                        ProfileViewBandItemCollection pvbic = pvbs.GetBottomBandItems();

                        for (int i = 0; i < pvbic.Count; i++)
                        {
                            if (i == 0)
                            {
                                ProfileViewBandItem pvbi = pvbic[i];
                                pvbi.Profile1Id = surfaceProfileId;
                                pvbi.Profile2Id = topProfileId;
                                pvbi.LabelAtStartStation = true;
                                pvbi.LabelAtEndStation = true;
                            }
                        }
                        pvbs.SetBottomBandItems(pvbic);
                    }
                }
                #endregion
            }
            catch (System.Exception ex)
            {
                return new Result(ResultStatus.FatalError, ex.ToString());
            }

            return new Result();
        }
        [MethodDescription(
            "Detach an xref by name",
            "Detacher en xref i tegningen\n" +
            "ved navn (uden .dwg)",
            new string[1] { "Xref Navn (uden .dwg)" })]
        public static Result detachdwg(Database xDb, string detachXrefName)
        {
            Transaction xTx = xDb.TransactionManager.TopTransaction;

            BlockTable bt = xDb.BlockTableId.Go<BlockTable>(xTx, OpenMode.ForRead);
            var modelSpace = xDb.GetModelspaceForWrite();
            DrawOrderTable dot = modelSpace.DrawOrderTableId.Go<DrawOrderTable>(
                xTx, OpenMode.ForWrite);

            foreach (Oid oid in bt)
            {
                BlockTableRecord btr = xTx.GetObject(oid, OpenMode.ForWrite) as BlockTableRecord;
                //if (btr.Name.Contains("_alignment"))
                if (btr.Name == detachXrefName && btr.IsFromExternalReference)
                {
                    prdDbg("Found specified xref!");
                    xDb.DetachXref(btr.ObjectId);
                    return new Result();
                }
            }
            prdDbg("Specified xref NOT found!");
            return new Result();
        }
        [MethodDescription(
            "Detach and attach an xref by name with optional draw order",
            "Detacher en xref i tegningen og attacher den igen\n" +
            "ved navn (uden .dwg), samtidig giver mulighed for\n" +
            "at angive en anden xref den skal placeres under\n" +
            "eller over i draw order. Der skal kun angives over eller under!",
            new string[3] { "Xref Navn (uden .dwg)", "Xref Navn til at placere i draw order", "Draw Order type: Over eller Under" })]
        public static Result detachattachdwg(
            Database xDb, string detachXrefName, string drawOrderXref, string drawOrderType)
        {
            string xrefPath = "";
            string xrefLayerName = "";

            using (Transaction nestedTx = xDb.TransactionManager.StartTransaction())
            {
                BlockTable bt = xDb.BlockTableId.Go<BlockTable>(nestedTx, OpenMode.ForRead);
                BlockTableRecord ms = xDb.GetModelspaceForWrite();
                DrawOrderTable dot = ms.DrawOrderTableId.Go<DrawOrderTable>(
                    nestedTx, OpenMode.ForWrite);

                foreach (Oid oid in ms)
                {
                    var br = oid.Go<BlockReference>(nestedTx);
                    if (br == null) continue;
                    BlockTableRecord btr = nestedTx.GetObject(br.BlockTableRecord, OpenMode.ForWrite) as BlockTableRecord;
                    if (btr.IsFromExternalReference && btr.Name == detachXrefName)
                    {
                        prdDbg("Found specified xref!");
                        xrefPath = btr.PathName;
                        xrefLayerName = br.Layer;
                        xDb.DetachXref(btr.ObjectId);
                    }
                }

                if (xrefPath.IsNoE() || xrefLayerName.IsNoE())
                {
                    prdDbg("Specified xref NOT found or layer name is empty!");
                    nestedTx.Abort();
                    return new Result(
                        ResultStatus.FatalError, $"Xref {detachXrefName} not found or layer name is empty!");
                }

                nestedTx.Commit();
            }

            using (Transaction nestedTx = xDb.TransactionManager.StartTransaction())
            {
                try
                {
                    Oid xrefId = xDb.AttachXref(xrefPath, detachXrefName);
                    if (xrefId == Oid.Null)
                    {
                        nestedTx.Abort();
                        return new Result(
                            ResultStatus.FatalError, $"Could not reattach xref {detachXrefName}!");
                    }

                    Point3d insPt = new Point3d(0, 0, 0);
                    using (BlockReference br = new BlockReference(insPt, xrefId))
                    {
                        BlockTableRecord modelSpace = xDb.GetModelspaceForWrite();
                        modelSpace.AppendEntity(br);
                        nestedTx.AddNewlyCreatedDBObject(br, true);

                        br.Layer = xrefLayerName;

                        if (drawOrderXref.IsNotNoE())
                        {
                            foreach (Oid oid in modelSpace)
                            {
                                var doBr = oid.Go<BlockReference>(nestedTx);
                                if (doBr == null) continue;
                                BlockTableRecord btr = nestedTx.GetObject(doBr.BlockTableRecord, OpenMode.ForWrite) as BlockTableRecord;
                                if (btr.IsFromExternalReference && btr.Name == drawOrderXref)
                                {
                                    prdDbg("Found specified draw order xref!");

                                    DrawOrderTable dot = modelSpace.DrawOrderTableId.Go<DrawOrderTable>(nestedTx);
                                    dot.CheckOrOpenForWrite();

                                    ObjectIdCollection idCol = new ObjectIdCollection(new Oid[1] { br.Id });

                                    if (drawOrderType == "Under") dot.MoveBelow(idCol, doBr.Id);
                                    else if (drawOrderType == "Over") dot.MoveAbove(idCol, doBr.Id);
                                }
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    nestedTx.Abort();
                    return new Result(
                        ResultStatus.FatalError,
                        ex.ToString());
                }

                nestedTx.Commit();
            }

            return new Result();
        }
        [MethodDescription(
            "Recreate detailing",
            "Sletter eksisterende detaljering\n" +
            "af længdeprofiler og laver ny",
            new string[1] { "Vælg projekt og etape" })]
        public static Result createdetailing(Database xDb, DataReferencesOptions dro)
        {
            //Recreate detailing in affected drawings
            new Intersect().deletedetailingmethod(xDb);
            new Intersect().createdetailingmethod(dro, xDb);
            return new Result();
        }
        [MethodDescription(
            "Create reference to pipe profiles",
            "Opretter alle rør-profiler fra shortcuts.\n" +
            "Springer terrænprofil over.\n" +
            "Husk at sætte shortcuts folder!")]
        public static Result createreferencetopipeprofiles(Database xDb)
        {
            //Used when sheets were created before pipe profiles were available
            //Finds those profiles and creates a reference to them in drawing
            //Then it deletes the detailing and recreates it
            Transaction xTx = xDb.TransactionManager.TopTransaction;
            var als = xDb.HashSetOfType<Alignment>(xTx);

            Regex reg1 = new Regex(@"(?<number>\d{2,3})");

            bool isValidCreation = false;
            DataShortcuts.DataShortcutManager sm = DataShortcuts.CreateDataShortcutManager(ref isValidCreation);
            if (isValidCreation != true)
            {
                prdDbg("DataShortcutManager failed to be created!");
                return new Result(ResultStatus.FatalError, "DataShortcutManager failed to be created!");
            }
            int publishedCount = sm.GetPublishedItemsCount();

            foreach (Alignment al in als)
            {
                string number = reg1.Match(al.Name).Groups["number"].Value;
                prdDbg($"{al.Name} -> {number}");

                for (int i = 0; i < publishedCount; i++)
                {
                    DataShortcuts.DataShortcutManager.PublishedItem item =
                        sm.GetPublishedItemAt(i);

                    if (item.DSEntityType == DataShortcutEntityType.Alignment)
                    {
                        if (item.Name.StartsWith(number))
                        {
                            var items = GetItemsByPipelineNumber(sm, number);

                            foreach (int idx in items)
                            {
                                DataShortcuts.DataShortcutManager.PublishedItem entity =
                                    sm.GetPublishedItemAt(idx);

                                if (entity.DSEntityType == DataShortcutEntityType.Alignment ||
                                    entity.Name.Contains("surface")) continue;

                                sm.CreateReference(idx, xDb);
                            }
                        }
                    }
                }

                IEnumerable<int> GetItemsByPipelineNumber(
                    DataShortcuts.DataShortcutManager dsMan, string pipelineNumber)
                {
                    int count = dsMan.GetPublishedItemsCount();

                    for (int j = 0; j < count; j++)
                    {
                        string name = dsMan.GetPublishedItemAt(j).Name;
                        if (name.StartsWith(pipelineNumber)) yield return j;
                    }
                }
            }

            sm.Dispose();

            return new Result();
        }
        [MethodDescription(
            "Create reference to SURFACE profiles",
            "Opretter reference til SURFACE profile fra shortcuts.\n" +
            "Husk at sætte shortcuts folder!")]
        public static Result createreferencetosurfaceprofile(Database xDb)
        {
            //Used when sheets were created before pipe profiles were available
            //Finds those profiles and creates a reference to them in drawing
            //Then it deletes the detailing and recreates it
            Transaction xTx = xDb.TransactionManager.TopTransaction;
            var als = xDb.HashSetOfType<Alignment>(xTx);

            Regex reg1 = new Regex(@"(?<number>\d{2,3})");

            bool isValidCreation = false;
            DataShortcuts.DataShortcutManager sm = DataShortcuts.CreateDataShortcutManager(ref isValidCreation);
            if (isValidCreation != true)
            {
                prdDbg("DataShortcutManager failed to be created!");
                return new Result(ResultStatus.FatalError, "DataShortcutManager failed to be created!");
            }
            int publishedCount = sm.GetPublishedItemsCount();

            foreach (Alignment al in als)
            {
                string number = reg1.Match(al.Name).Groups["number"].Value;
                prdDbg($"{al.Name} -> {number}");

                for (int i = 0; i < publishedCount; i++)
                {
                    DataShortcuts.DataShortcutManager.PublishedItem item =
                        sm.GetPublishedItemAt(i);

                    if (item.DSEntityType == DataShortcutEntityType.Alignment)
                    {
                        if (item.Name.StartsWith(number))
                        {
                            var items = GetItemsByPipelineNumber(sm, number);

                            foreach (int idx in items)
                            {
                                DataShortcuts.DataShortcutManager.PublishedItem entity =
                                    sm.GetPublishedItemAt(idx);

                                if (entity.DSEntityType == DataShortcutEntityType.Alignment) continue;
                                if (entity.Name.Contains("surface"))
                                {
                                    sm.CreateReference(idx, xDb);
                                }
                            }
                        }
                    }
                }

                IEnumerable<int> GetItemsByPipelineNumber(
                    DataShortcuts.DataShortcutManager dsMan, string pipelineNumber)
                {
                    int count = dsMan.GetPublishedItemsCount();

                    for (int j = 0; j < count; j++)
                    {
                        string name = dsMan.GetPublishedItemAt(j).Name;
                        if (name.StartsWith(pipelineNumber)) yield return j;
                    }
                }
            }

            sm.Dispose();

            return new Result();
        }
        [MethodDescription(
            "Fix surface profile and longitudinal profile view",
            "Kan bruges ved tomme længdeprofiler, når der ikke er tegnet rør.\n" +
            "Sætter alle profile views til min 3 meters dybde og giver\n" +
            "terrænprofilet rigtig farve. Ellers kan man køre finalize,\n" +
            "hvis man vil have detaljering på tegningen.",
            new string[1] { "Vælg projekt og etape" })]
        public static Result fixlongitudinalprofiles(Database xDb, DataReferencesOptions dro)
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
            new Intersect().createdetailingpreliminarymethod(dro, xDb);

            return new Result();
        }
        [MethodDescription(
            "Lists all ViewFrame numbers",
            "Viser alle ViewFrame numre og advarer\n" +
            "hvis ikke de følger rækkefølgen.",
            new string[1] { "Tæller" })]
        public static Result listvfnumbers(Database xDb, Counter count)
        {
            Transaction xTx = xDb.TransactionManager.TopTransaction;

            ViewFrameGroup vfg = xDb.ListOfType<ViewFrameGroup>(xTx).FirstOrDefault();
            if (vfg != null)
            {
                var ids = vfg.GetViewFrameIds();
                var ents = ids.Entities<ViewFrame>(xTx);
                foreach (var item in ents)
                {
                    count.counter++;
                    int vfNumber = Convert.ToInt32(item.Name);
                    if (count.counter != vfNumber) prdDbg(item.Name + " <- Fejl! Skal være " + count.counter + ".");
                    else prdDbg(item.Name);
                }
            }
            return new Result();
        }
        [MethodDescription(
            "Renumber all ViewFrame numbers",
            "Omnummererer alle ViewFrame numre til at følge rækkefølgen.\n" +
            "Bruges når der har været indsat ekstra ViewFrames under KS\n" +
            "og dermed ødelagt rækkefølgen.",
            new string[1] { "Tæller" })]
        public static Result renumbervfs(Database xDb, Counter count)
        {
            Transaction xTx = xDb.TransactionManager.TopTransaction;

            ViewFrameGroup vfg = xDb.ListOfType<ViewFrameGroup>(xTx).FirstOrDefault();
            if (vfg != null)
            {
                var ids = vfg.GetViewFrameIds();
                var ents = ids.Entities<ViewFrame>(xTx);

                Dictionary<Oid, string> oNames = new Dictionary<Oid, string>();

                Random rnd = new Random();
                foreach (var item in ents)
                {
                    oNames.Add(item.Id, item.Name);

                    item.CheckOrOpenForWrite();
                    item.Name = rnd.Next(1, 999999).ToString("000000");
                }

                foreach (var item in ents)
                {
                    count.counter++;
                    string previousName = item.Name;
                    item.CheckOrOpenForWrite();
                    item.Name = count.counter.ToString("000");
                    prdDbg($"{oNames[item.Id]} -> {item.Name}");
                }
            }
            return new Result();
        }
        [MethodDescription(
            "Corrects SAG2 field in the block Tegningsskilt",
            "Retter feltet SAG2 i blokken Tegningsskilt. Dette bruges hvis\n" +
            "ét af felter har været manuelt redigeret og referencen til\n" +
            "SSM har været fjernet. Hvis der ønskes at rette på andre\n" +
            "felter, så skal koden revideres.")]
        public static Result correctfieldinblock(Database xDb)
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
        [MethodDescription(
            "Run HAL",
            "Skjuler alle alignments og fjerner labels.\n" +
            "Det samme som at køre HAL.")]
        public static Result hidealignments(Database xDb)
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
        [MethodDescription(
            "Set alignment style to NO SHOW",
            "Sætter alignment style til NO SHOW.\n" +
            "Rører ikke ved labels.")]
        public static Result alignmentsnoshow(Database xDb)
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
        [MethodDescription(
            "Set NO SHOW and STD 20-5",
            "Sætter alignment style til NO SHOW\n" +
            "og sætter labels STD 20-5.")]
        public static Result alignmentsnoshowandlabels(Database xDb)
        {
            Transaction xTx = xDb.TransactionManager.TopTransaction;

            var cDoc = CivilDocument.GetCivilDocument(xDb);
            Oid alStyle = cDoc.Styles.AlignmentStyles["FJV TRACE NO SHOW"];
            Oid labelSetStyle = cDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles["STD 20-5"];

            HashSet<Alignment> als = xDb.HashSetOfType<Alignment>(xTx);

            foreach (Alignment al in als)
            {
                al.CheckOrOpenForWrite();
                al.StyleId = alStyle;
                al.ImportLabelSet(labelSetStyle);
            }
            return new Result();
        }
        [MethodDescription(
            "Freeze LER points in minimap",
            "Finder alle lag brugt af LER punkter og\n" +
            "freezer dem i minikortets ViewPort.\n" +
            "Angiv viewportens center X og Y til hele tal.",
            new string[2] { "ViewPort Center X", "ViewPort Center Y" })]
        public static Result vpfreezelayers(Database xDb, int X, int Y)
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
            foreach (DBDictionaryEntry item in layoutDict)
            {
                if (item.Key == "Model")
                {
                    //prdDbg("Skipping model...");
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
                        if (centerX == X && centerY == Y)
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
        [MethodDescription(
            "Freeze match line in minimap",
            "Freezer match linje i minikortet.\n" +
            "Match linje er det stiplede\n" +
            "linje, som angiver grænsen mellem delplaner.",
            new string[2] { "ViewPort Center X", "ViewPort Center Y" })]
        public static Result vpfreezecannomtch(Database xDb, int X, int Y)
        {
            Transaction xTx = xDb.TransactionManager.TopTransaction;
            ObjectIdCollection oids = new ObjectIdCollection();

            try
            {
                LayerTable lt = xDb.LayerTableId.Go<LayerTable>(xTx);
                //Find c-anno-mtch
                foreach (Oid loid in lt)
                {
                    LayerTableRecord ltr = loid.Go<LayerTableRecord>(xTx);
                    if (ltr.Name.EndsWith("_VF|C-ANNO-MTCH"))
                    {
                        oids.Add(loid);
                        //prdDbg(ltr.Name);
                    }
                }

                DBDictionary layoutDict = xDb.LayoutDictionaryId.Go<DBDictionary>(xTx);
                foreach (DBDictionaryEntry item in layoutDict)
                {
                    if (item.Key == "Model")
                    {
                        //prdDbg("Skipping model...");
                        continue;
                    }
                    Layout layout = item.Value.Go<Layout>(xTx);
                    BlockTableRecord layBlock = layout.BlockTableRecordId.Go<BlockTableRecord>(xTx);

                    foreach (Oid id in layBlock)
                    {
                        if (id.IsDerivedFrom<Viewport>())
                        {
                            Viewport vp = id.Go<Viewport>(xTx);
                            //Truncate doubles to whole numbers for easier comparison
                            int centerX = (int)vp.CenterPoint.X;
                            int centerY = (int)vp.CenterPoint.Y;
                            if (centerX == X && centerY == Y)
                            {
                                prdDbg("Found minikort viewport!");
                                ObjectIdCollection notFrozenIds = new ObjectIdCollection();
                                foreach (Oid oid in oids)
                                {
                                    if (vp.IsLayerFrozenInViewport(oid)) continue;
                                    notFrozenIds.Add(oid);
                                }

                                if (notFrozenIds.Count == 0) continue;

                                prdDbg("Freezing C-ANNO-MTCH!");
                                vp.CheckOrOpenForWrite();
                                vp.FreezeLayersInViewport(notFrozenIds.GetEnumerator());
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                return new Result(ResultStatus.FatalError, ex.ToString());
            }
            return new Result();
        }
        [MethodDescription(
            "Stylize layers in minimap, hardcoded for now",
            "Finder lag brugt af XREF i minikort og\n" +
            "freezer dem i minikortets ViewPort.\n" +
            "Samt ændrer farve på bygninger og vejkanter.\n" +
            "Angiv viewportens center X og Y til hele tal.",
            new string[3] { "Xref name", "Minikort ViewPort Center X", "Minikort ViewPort Center Y" })]
        public static Result vpstylizelayers(Database xDb, string XrefName, int X, int Y)
        {
            Transaction xTx = xDb.TransactionManager.TopTransaction;

            ObjectIdCollection oids = new ObjectIdCollection();
            LayerTable lt = xDb.LayerTableId.Go<LayerTable>(xTx);
            foreach (Oid id in lt)
            {
                LayerTableRecord ltr = id.Go<LayerTableRecord>(xTx);
                if (ltr.Name.StartsWith(XrefName)) oids.Add(id);
            }
            prdDbg($"Number of oids: {oids.Count}");

            DBDictionary layoutDict = xDb.LayoutDictionaryId.Go<DBDictionary>(xTx);
            foreach (DBDictionaryEntry item in layoutDict)
            {
                if (item.Key == "Model")
                {
                    //prdDbg("Skipping model...");
                    continue;
                }
                Layout layout = item.Value.Go<Layout>(xTx);
                BlockTableRecord layBlock = layout.BlockTableRecordId.Go<BlockTableRecord>(xTx);

                foreach (Oid vpid in layBlock)
                {
                    if (vpid.IsDerivedFrom<Viewport>())
                    {
                        Viewport vp = vpid.Go<Viewport>(xTx, OpenMode.ForWrite);
                        //Truncate doubles to whole numebers for easier comparison
                        int centerX = (int)vp.CenterPoint.X;
                        int centerY = (int)vp.CenterPoint.Y;
                        if (centerX == X && centerY == Y)
                        {
                            prdDbg("Found minikort viewport!");
                            ObjectIdCollection notFrozenIds = new ObjectIdCollection();
                            ObjectIdCollection idsToColor = new ObjectIdCollection();
                            foreach (Oid oid in oids)
                            {
                                LayerTableRecord ltr = oid.Go<LayerTableRecord>(xTx);
                                if (ltr.Name.Contains("Bygning") || ltr.Name.Contains("Vejkant"))
                                { idsToColor.Add(oid); continue; }
                                if (vp.IsLayerFrozenInViewport(oid)) continue;
                                notFrozenIds.Add(oid);
                            }
                            prdDbg($"Number of not frozen layers: {notFrozenIds.Count}");
                            if (notFrozenIds.Count != 0)
                                vp.FreezeLayersInViewport(notFrozenIds.GetEnumerator());

                            prdDbg($"Number of layers to color: {idsToColor.Count}");
                            if (idsToColor.Count == 0) continue;

                            foreach (Oid ltrid in idsToColor)
                            {
                                LayerTableRecord ltr = ltrid.Go<LayerTableRecord>(xTx);
                                ltr.UpgradeOpen();
                                LayerViewportProperties lvp = ltr.GetViewportOverrides(vpid);
                                lvp.Color = ColorByName("grey");
                            }
                        }
                        vp.UpdateDisplay();
                    }
                }
            }
            return new Result();
        }
        [MethodDescription(
            "Place block on paperspace\n",
            "Placerer specificeret block på paperspace.\n" +
            "Blocken skal findes i tegningen.\n" +
            "Angiv viewportens center X og Y til hele tal.\n" +
            "Blokkens rotation sættes til ViewPortens twistangle.\n" +
            "Dette bruges specifikt til at placere nordpilsblokke",
            new string[5] { "Block name", "Block X", "Block Y",
                "ViewPort Center X", "ViewPort Center Y"})]
        public static Result placeblockpaperspace(
            Database xDb, string blockName, int brX, int brY, int X, int Y)
        {
            Transaction xTx = xDb.TransactionManager.TopTransaction;

            BlockTable bt = xTx.GetObject(xDb.BlockTableId, OpenMode.ForRead) as BlockTable;
            Oid btrId = bt[blockName];

            DBDictionary layoutDict = xDb.LayoutDictionaryId.Go<DBDictionary>(xTx);

            foreach (DBDictionaryEntry item in layoutDict)
            {
                //prdDbg(item.Key);
                if (item.Key == "Model")
                {
                    //prdDbg("Skipping model...");
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
                        if (centerX == X && centerY == Y)
                        {
                            prdDbg($"Found main viewport, placing {blockName}!");

                            var br = new BlockReference(new Point3d(brX, brY, 0), btrId);
                            layBlock.CheckOrOpenForWrite();
                            layBlock.AppendEntity(br);
                            xTx.AddNewlyCreatedDBObject(br, true);

                            br.Rotation = vp.TwistAngle;
                        }
                    }
                }
            }
            return new Result();
        }
        [MethodDescription(
            "Replace (title)block on paperspace\n",
            "Udskifter specificeret block på paperspace.\n" +
            "Blocken importeres fra den angivne dwg.\n" +
            "Dette kan bruges til at udskifte tegningsskilte",
            new string[5] { "Name of block to replace", "Path to dwg with new block", "Name of new block",
                "Block placement X", "Block placement Y"})]
        public static Result replaceblockpaperspace(
            Database xDb, string blockToReplace, string pathToBlockLibrary, string blockReplacement, int brX, int brY)
        {
            Transaction xTx = xDb.TransactionManager.TopTransaction;
            BlockTable bt = xTx.GetObject(xDb.BlockTableId, OpenMode.ForRead) as BlockTable;

            #region Delete old block
            using (Transaction dxTx = xDb.TransactionManager.StartTransaction())
            {
                if (bt.Has(blockToReplace))
                {
                    Oid btrId = bt[blockToReplace];
                    BlockTableRecord btr = btrId.Go<BlockTableRecord>(dxTx);
                    var refIds = btr.GetBlockReferenceIds(true, false);
                    var brs = refIds.Entities<BlockReference>(dxTx);
                    foreach (var br in brs)
                    {
                        br.UpgradeOpen();
                        br.Erase();
                    }
                    btr.UpgradeOpen();
                    btr.Erase();
                    dxTx.Commit();
                }
                else dxTx.Abort();
            }
            #endregion

            #region Import new block
            xDb.CheckOrImportBlockRecord(pathToBlockLibrary, blockReplacement);
            if (!bt.Has(blockReplacement))
                return new Result(
                    ResultStatus.FatalError, $"{Path.GetFileName(xDb.Filename)} " +
                    $"failed to import {blockReplacement} from {pathToBlockLibrary}!");
            BlockTableRecord newBtr = bt[blockReplacement].Go<BlockTableRecord>(xTx);
            #endregion

            DBDictionary layoutDict = xDb.LayoutDictionaryId.Go<DBDictionary>(xTx);

            foreach (DBDictionaryEntry item in layoutDict)
            {
                //prdDbg(item.Key);
                if (item.Key == "Model")
                {
                    //prdDbg("Skipping model...");
                    continue;
                }
                Layout layout = item.Value.Go<Layout>(xTx);
                BlockTableRecord layBlock = layout.BlockTableRecordId.Go<BlockTableRecord>(xTx);

                var br = new BlockReference(new Point3d(brX, brY, 0), newBtr.Id);
                layBlock.CheckOrOpenForWrite();
                layBlock.AppendEntity(br);
                xTx.AddNewlyCreatedDBObject(br, true);

                foreach (Oid arOid in newBtr)
                {
                    if (arOid.IsDerivedFrom<AttributeDefinition>())
                    {
                        AttributeDefinition at = arOid.Go<AttributeDefinition>(xTx);
                        if (!at.Constant)
                        {
                            using (AttributeReference atRef = new AttributeReference())
                            {
                                atRef.SetAttributeFromBlock(at, br.BlockTransform);
                                atRef.Position = at.Position.TransformBy(br.BlockTransform);
                                atRef.TextString = at.getTextWithFieldCodes();
                                br.AttributeCollection.AppendAttribute(atRef);
                                xTx.AddNewlyCreatedDBObject(atRef, true);
                            }
                        }
                    }
                }

                br.AttSync();
            }
            return new Result();
        }
        [MethodDescription(
            "Unhide layer(s)\n",
            "Unhide specifikke lag i tegningen.\n" +
            "Ved input af flere lag skal\n" +
            "de enkelte lagnavne deles op med \";\".\n",
            new string[1] { "Layer name(s)" })]
        public static Result unhidelayer(Database xDb, string layerNames)
        {
            Transaction xTx = xDb.TransactionManager.TopTransaction;
            LayerTable extLt = xDb.LayerTableId.Go<LayerTable>(xTx);

            var split = layerNames.Split(';');

            foreach (string layName in split)
            {
                foreach (Oid oid in extLt)
                {
                    LayerTableRecord ltr = oid.Go<LayerTableRecord>(xTx);

                    if (ltr.Name == layName)
                    {
                        ltr.CheckOrOpenForWrite();
                        ltr.IsFrozen = false;
                        ltr.IsOff = false;
                    }
                }
            }

            return new Result();
        }
        [MethodDescription(
            "Hide layer(s)\n",
            "Hide specifikke lag i tegningen.\n" +
            "Ved input af flere lag skal\n" +
            "de enkelte lagnavne deles op med \";\".\n",
            new string[1] { "Layer name(s)" })]
        public static Result hidelayer(Database xDb, string layerNames)
        {
            Transaction xTx = xDb.TransactionManager.TopTransaction;
            LayerTable extLt = xDb.LayerTableId.Go<LayerTable>(xTx);

            var split = layerNames.Split(';');

            foreach (string layName in split)
            {
                foreach (Oid oid in extLt)
                {
                    LayerTableRecord ltr = oid.Go<LayerTableRecord>(xTx);

                    if (ltr.Name == layName)
                    {
                        ltr.CheckOrOpenForWrite();
                        ltr.IsFrozen = true;
                        ltr.IsOff = true;
                    }
                }
            }

            return new Result();
        }
        [MethodDescription(
            "Change layer for an xref with partial name match",
            "Skifter laget til det angivne\n" +
            "for en xref, hvor navnet på xref'en\n"+
            "matches med en del af navnet.",
            new string[2] { "Xref Del af Navnet", "Laget Xref'en skal sættes til" })]
        public static Result changelayerforxref(Database xDb, string xrefPartialName, string layerName)
        {
            Transaction xTx = xDb.TransactionManager.TopTransaction;

            BlockTable bt = xDb.BlockTableId.Go<BlockTable>(xTx, OpenMode.ForRead);
            var modelSpace = xDb.GetModelspaceForWrite();

            foreach (Oid oid in bt)
            {
                BlockTableRecord btr = xTx.GetObject(oid, OpenMode.ForWrite) as BlockTableRecord;
                if (btr.Name.Contains(xrefPartialName) && btr.IsFromExternalReference)
                {
                    prdDbg($"Found specified xref: {btr.Name}");

                    var ids = btr.GetBlockReferenceIds(true, false);
                    foreach (Oid id in ids)
                    {
                        var br = id.Go<BlockReference>(xTx, OpenMode.ForWrite);
                        br.Layer = layerName;
                    }

                    return new Result();
                }
            }
            prdDbg("Specified xref NOT found!");
            return new Result();
        }
        [MethodDescription(
            "Delete wrongly shortcuttet profiles and alignment",
            "Sletter alle profiler i tegningen.\n" +
            "Sletter alignments som har \"(\"\n" +
            "som en del af navnet. Det betyder at det\n" +
            "har været en dublikat alignment som er shortcuttet.")]
        public static Result deletewronglyshortcuttedprofilesandalignments(Database xDb)
        {
            Transaction xTx = xDb.TransactionManager.TopTransaction;

            var profiles = xDb.ListOfType<Profile>(xTx);

            foreach (var item in profiles)
            {
                item.CheckOrOpenForWrite();
                item.Erase(true);
            }

            var als = xDb.ListOfType<Alignment>(xTx);
            foreach (var item in als)
            {
                if (item.Name.Contains("("))
                {
                    prdDbg(item.Name);
                    item.CheckOrOpenForWrite();
                    item.Erase(true);
                }
            }
            return new Result();
        }
    }

    public class Result
    {
        private ResultStatus _status = ResultStatus.OK;
        internal ResultStatus Status { get { return _status; } set { if (_status != ResultStatus.FatalError) _status = value; } }
        private string _errorMsg;
        internal string ErrorMsg
        {
            get { return _errorMsg + "\n"; }
            set
            {
                if (_errorMsg.IsNoE()) _errorMsg = value;
                else _errorMsg += "\n" + value;
            }
        }
        internal Result() { }
        internal Result(ResultStatus status, string errorMsg)
        {
            Status = status;
            ErrorMsg = errorMsg;
        }
    }
    internal enum ResultStatus
    {
        OK,
        FatalError, //Execution of processing must stop
        SoftError //Exection may continue, changes to current drawing aborted
    }
    public class MethodDescription : Attribute
    {
        public MethodDescription(string shortDescription, string longDescription)
        {
            ShortDescription = shortDescription;
            LongDescription = longDescription;
        }
        public MethodDescription(string shortDescription, string longDescription, string[] argDescriptions)
        {
            ShortDescription = shortDescription;
            LongDescription = longDescription;
            ArgDescriptions = argDescriptions;
        }
        public string ShortDescription { get; set; }
        public string LongDescription { get; set; }
        public string[] ArgDescriptions { get; set; }
    }
    public class Counter
    {
        public int counter = 0;
    }
}