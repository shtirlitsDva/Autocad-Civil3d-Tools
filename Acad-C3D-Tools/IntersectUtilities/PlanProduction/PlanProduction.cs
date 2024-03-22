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
using System.Text.Json;
using IntersectUtilities.DynamicBlocks;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Plane = Autodesk.AutoCAD.Geometry.Plane;
using netDxf.Entities;
using NetTopologySuite.Triangulate;
using IntersectUtilities.LongitudinalProfiles;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        [CommandMethod("finalizesheets")]
        public void finalizesheets()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor ed = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            DataReferencesOptions dro = new DataReferencesOptions();

            //Create crossing points first
            createlerdatapssmethod2(dro);
            //Populateprofileviews with crossing data
            populateprofiles();

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForWrite) as BlockTable;

                    #region Stylize Profile Views
                    HashSet<ProfileView> pvs = localDb.HashSetOfType<ProfileView>(tx);

                    Oid pvStyleId = Oid.Null;
                    try
                    {
                        pvStyleId = civilDoc.Styles.ProfileViewStyles["PROFILE VIEW L TO R 1:250:100"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nProfile view style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    HashSet<Alignment> alss = localDb.HashSetOfType<Alignment>(tx);
                    foreach (Alignment al in alss)
                    {
                        al.CheckOrOpenForWrite();
                        al.StyleId = civilDoc.Styles.AlignmentStyles["FJV TRACÉ SHOW"];
                        al.ImportLabelSet("STD 20-5");
                        al.DowngradeOpen();
                    }

                    foreach (ProfileView pv in pvs)
                    {
                        pv.CheckOrOpenForWrite();
                        pv.StyleId = pvStyleId;

                        Oid alId = pv.AlignmentId;
                        Alignment al = alId.Go<Alignment>(tx);

                        ObjectIdCollection psIds = al.GetProfileIds();
                        HashSet<Profile> ps = new HashSet<Profile>();
                        foreach (Oid oid in psIds) ps.Add(oid.Go<Profile>(tx));

                        Profile surfaceProfile = ps.Where(x => x.Name.Contains("surface")).FirstOrDefault();
                        Oid surfaceProfileId = Oid.Null;
                        if (surfaceProfile != null) surfaceProfileId = surfaceProfile.ObjectId;
                        else ed.WriteMessage("\nSurface profile not found!");

                        Profile topProfile = ps.Where(x => x.Name.Contains("TOP")).FirstOrDefault();
                        Oid topProfileId = Oid.Null;
                        if (topProfile != null) topProfileId = topProfile.ObjectId;
                        else ed.WriteMessage("\nTop profile not found!");

                        //this doesn't quite work
                        Oid pvbsId = civilDoc.Styles.ProfileViewBandSetStyles["EG-FG Elevations and Stations"];
                        ProfileViewBandSet pvbs = pv.Bands;
                        pvbs.ImportBandSetStyle(pvbsId);

                        //try this
                        Oid pvBSId1 = civilDoc.Styles.BandStyles.ProfileViewProfileDataBandStyles["Elevations and Stations"];
                        Oid pvBSId2 = civilDoc.Styles.BandStyles.ProfileViewProfileDataBandStyles["TitleBuffer"];
                        ProfileViewBandItemCollection pvic = new ProfileViewBandItemCollection(pv.Id, BandLocationType.Bottom);
                        pvic.Add(pvBSId1);
                        pvic.Add(pvBSId2);
                        pvbs.SetBottomBandItems(pvic);

                        ProfileViewBandItemCollection pbic = pvbs.GetBottomBandItems();
                        for (int i = 0; i < pbic.Count; i++)
                        {
                            ProfileViewBandItem pvbi = pbic[i];
                            if (i == 0) pvbi.Gap = 0;
                            else if (i == 1) pvbi.Gap = 0.016;
                            if (surfaceProfileId != Oid.Null) pvbi.Profile1Id = surfaceProfileId;
                            if (topProfileId != Oid.Null) pvbi.Profile2Id = topProfileId;
                            pvbi.LabelAtStartStation = true;
                            pvbi.LabelAtEndStation = true;
                        }
                        pvbs.SetBottomBandItems(pbic);

                        #region Scale LER block
                        if (bt.Has(pv.Name))
                        {
                            BlockTableRecord btr = tx.GetObject(bt[pv.Name], OpenMode.ForRead)
                                as BlockTableRecord;
                            ObjectIdCollection brefIds = btr.GetBlockReferenceIds(false, true);

                            foreach (Oid oid in brefIds)
                            {
                                BlockReference bref = oid.Go<BlockReference>(tx, OpenMode.ForWrite);
                                bref.ScaleFactors = new Scale3d(1, 2.5, 1);
                            }

                        }
                        #endregion
                    }
                    #endregion

                    #region ProfileStyles
                    Oid pPipeStyleKantId = Oid.Null;
                    try
                    {
                        pPipeStyleKantId = civilDoc.Styles.ProfileStyles["PROFIL STYLE MGO KANT"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nPROFIL STYLE MGO KANT style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    Oid pPipeStyleMidtId = Oid.Null;
                    try
                    {
                        pPipeStyleMidtId = civilDoc.Styles.ProfileStyles["PROFIL STYLE MGO MIDT"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nPROFIL STYLE MGO MIDT style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    Oid pTerStyleId = Oid.Null;
                    try
                    {
                        pTerStyleId = civilDoc.Styles.ProfileStyles["Terræn"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nTerræn style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    Oid alStyleId = Oid.Null;
                    try
                    {
                        alStyleId = civilDoc.Styles.AlignmentStyles["FJV TRACÉ SHOW"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nFJV TRACÈ SHOW style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    Oid alLabelSetStyleId = Oid.Null;
                    try
                    {
                        alLabelSetStyleId = civilDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles["STD 20-5"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nSTD 20-5 style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    Oid crestCurveLabelId = Oid.Null;
                    try
                    {
                        crestCurveLabelId = civilDoc.Styles.LabelStyles.ProfileLabelStyles.CurveLabelStyles["Radius Crest"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nRADIUS CREST style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    Oid sagCurveLabelId = Oid.Null;
                    try
                    {
                        sagCurveLabelId = civilDoc.Styles.LabelStyles.ProfileLabelStyles.CurveLabelStyles["Radius Sag"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nRADIUS SAG style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);
                    foreach (Alignment al in als)
                    {
                        al.CheckOrOpenForWrite();
                        al.StyleId = alStyleId;
                        al.ImportLabelSet(alLabelSetStyleId);

                        ObjectIdCollection pIds = al.GetProfileIds();
                        foreach (Oid oid in pIds)
                        {
                            Profile p = oid.Go<Profile>(tx);
                            if (p.Name == $"{al.Name}_surface_P")
                            {
                                p.CheckOrOpenForWrite();
                                p.StyleId = pTerStyleId;
                            }
                            else
                            {
                                p.CheckOrOpenForWrite();
                                p.StyleId = pPipeStyleKantId;

                                if (p.Name.Contains("MIDT"))
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
                        }
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    ed.WriteMessage("\n" + ex.ToString());
                    return;
                }
                tx.Commit();
            }

            //Create detailing blocks on top of exaggerated views
            createdetailingmethod(dro, localDb);
            //Auto stagger all labels to right
            staggerlabelsall();
            //Draw rectangles representing viewports around longitudinal profiles
            //Can be used to check if labels are inside
            drawviewportrectangles();
            //Colorize layer as per krydsninger table
            colorizealllerlayersmethod();
        }

        [CommandMethod("finalizesheetsauto")]
        public void finalizesheetsauto()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor ed = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            #region Fix wrong PV style at start
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForWrite) as BlockTable;

                    HashSet<ProfileView> pvs = localDb.HashSetOfType<ProfileView>(tx);

                    Oid pvStyleId = Oid.Null;
                    try
                    {
                        pvStyleId = civilDoc.Styles.ProfileViewStyles["PROFILE VIEW L TO R NO SCALE"];
                        //pvStyleId = civilDoc.Styles.ProfileViewStyles["PROFILE VIEW L TO R 1:250:100"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nProfile view style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    foreach (ProfileView pv in pvs)
                    {
                        Oid pvCurStyleId = pv.StyleId;
                        ProfileViewStyle curPvStyle = pvCurStyleId.Go<ProfileViewStyle>(tx);
                        if (curPvStyle.Name != "PROFILE VIEW L TO R NO SCALE")
                        {
                            pv.CheckOrOpenForWrite();
                            pv.StyleId = pvStyleId;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    ed.WriteMessage("\n" + ex.ToString());
                    return;
                }
                tx.Commit();
            }
            #endregion

            //Create crossing points first
            DataReferencesOptions dro = new DataReferencesOptions();
            createlerdatapssmethod2(dro);

            //Populateprofileviews with crossing data
            populateprofiles();

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForWrite) as BlockTable;

                    #region Stylize Profile Views
                    HashSet<ProfileView> pvs = localDb.HashSetOfType<ProfileView>(tx);

                    Oid pvStyleId = Oid.Null;
                    try
                    {
                        pvStyleId = civilDoc.Styles.ProfileViewStyles["PROFILE VIEW L TO R 1:250:100"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nProfile view style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    foreach (ProfileView pv in pvs)
                    {
                        pv.CheckOrOpenForWrite();
                        pv.StyleId = pvStyleId;

                        Oid alId = pv.AlignmentId;
                        Alignment al = alId.Go<Alignment>(tx);

                        ObjectIdCollection psIds = al.GetProfileIds();
                        HashSet<Profile> ps = new HashSet<Profile>();
                        foreach (Oid oid in psIds) ps.Add(oid.Go<Profile>(tx));

                        Profile surfaceProfile = ps.Where(x => x.Name.Contains("surface")).FirstOrDefault();
                        Oid surfaceProfileId = Oid.Null;
                        if (surfaceProfile != null) surfaceProfileId = surfaceProfile.ObjectId;
                        else ed.WriteMessage("\nSurface profile not found!");

                        Profile topProfile = ps.Where(x => x.Name.Contains("TOP")).FirstOrDefault();
                        Oid topProfileId = Oid.Null;
                        if (topProfile != null) topProfileId = topProfile.ObjectId;
                        else ed.WriteMessage("\nTop profile not found!");

                        //this doesn't quite work
                        Oid pvbsId = civilDoc.Styles.ProfileViewBandSetStyles["EG-FG Elevations and Stations"];
                        ProfileViewBandSet pvbs = pv.Bands;
                        pvbs.ImportBandSetStyle(pvbsId);

                        //try this
                        Oid pvBSId1 = civilDoc.Styles.BandStyles.ProfileViewProfileDataBandStyles["Elevations and Stations"];
                        Oid pvBSId2 = civilDoc.Styles.BandStyles.ProfileViewProfileDataBandStyles["TitleBuffer"];
                        ProfileViewBandItemCollection pvic = new ProfileViewBandItemCollection(pv.Id, BandLocationType.Bottom);
                        pvic.Add(pvBSId1);
                        pvic.Add(pvBSId2);
                        pvbs.SetBottomBandItems(pvic);

                        ProfileViewBandItemCollection pbic = pvbs.GetBottomBandItems();
                        for (int i = 0; i < pbic.Count; i++)
                        {
                            ProfileViewBandItem pvbi = pbic[i];
                            if (i == 0) pvbi.Gap = 0;
                            else if (i == 1) pvbi.Gap = 0.016;
                            if (surfaceProfileId != Oid.Null) pvbi.Profile1Id = surfaceProfileId;
                            if (topProfileId != Oid.Null) pvbi.Profile2Id = topProfileId;
                            pvbi.LabelAtStartStation = true;
                            pvbi.LabelAtEndStation = true;
                        }
                        pvbs.SetBottomBandItems(pbic);

                        #region Scale LER block
                        if (bt.Has(pv.Name))
                        {
                            BlockTableRecord btr = tx.GetObject(bt[pv.Name], OpenMode.ForRead)
                                as BlockTableRecord;
                            ObjectIdCollection brefIds = btr.GetBlockReferenceIds(false, true);

                            foreach (Oid oid in brefIds)
                            {
                                BlockReference bref = oid.Go<BlockReference>(tx, OpenMode.ForWrite);
                                bref.ScaleFactors = new Scale3d(1, 2.5, 1);
                            }

                        }
                        #endregion
                    }
                    #endregion

                    #region ProfileStyles
                    Oid pPipeStyleKantId = Oid.Null;
                    try
                    {
                        pPipeStyleKantId = civilDoc.Styles.ProfileStyles["PROFIL STYLE MGO KANT"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nPROFIL STYLE MGO KANT style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    Oid pPipeStyleMidtId = Oid.Null;
                    try
                    {
                        pPipeStyleMidtId = civilDoc.Styles.ProfileStyles["PROFIL STYLE MGO MIDT"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nPROFIL STYLE MGO MIDT style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    Oid pTerStyleId = Oid.Null;
                    try
                    {
                        pTerStyleId = civilDoc.Styles.ProfileStyles["Terræn"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nTerræn style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    Oid alStyleId = Oid.Null;
                    try
                    {
                        alStyleId = civilDoc.Styles.AlignmentStyles["FJV TRACE NO SHOW"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nFJV TRACE NO SHOW style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    Oid alLabelSetStyleId = Oid.Null;
                    try
                    {
                        alLabelSetStyleId = civilDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles["STD 20-5"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nSTD 20-5 style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    Oid crestCurveLabelId = Oid.Null;
                    try
                    {
                        crestCurveLabelId = civilDoc.Styles.LabelStyles.ProfileLabelStyles.CurveLabelStyles["Radius Crest"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nRADIUS CREST style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    Oid sagCurveLabelId = Oid.Null;
                    try
                    {
                        sagCurveLabelId = civilDoc.Styles.LabelStyles.ProfileLabelStyles.CurveLabelStyles["Radius Sag"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nRADIUS SAG style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);
                    foreach (Alignment al in als)
                    {
                        al.CheckOrOpenForWrite();
                        al.StyleId = alStyleId;
                        al.ImportLabelSet(alLabelSetStyleId);

                        ObjectIdCollection pIds = al.GetProfileIds();
                        foreach (Oid oid in pIds)
                        {
                            Profile p = oid.Go<Profile>(tx);
                            if (p.Name == $"{al.Name}_surface_P")
                            {
                                p.CheckOrOpenForWrite();
                                p.StyleId = pTerStyleId;
                            }
                            else
                            {
                                p.CheckOrOpenForWrite();
                                p.StyleId = pPipeStyleKantId;

                                if (p.Name.Contains("MIDT"))
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
                        }
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    ed.WriteMessage("\n" + ex.ToString());
                    return;
                }
                tx.Commit();
            }

            //Create detailing blocks on top of exaggerated views
            //Detect if drawing has "MIDT" profile
            bool hasMidt = false;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                var profiles = localDb.HashSetOfType<Profile>(tx);
                if (profiles.Any(x => x.Name.Contains("MIDT"))) hasMidt = true;
            }
            if (hasMidt) createdetailingmethod(dro, localDb);
            else createdetailingpreliminarymethod(dro, localDb);
            //Auto stagger all labels to right
            staggerlabelsall();
            //Draw rectangles representing viewports around longitudinal profiles
            //Can be used to check if labels are inside
            drawviewportrectangles();
            //Colorize layer as per krydsninger table
            colorizealllerlayersmethod();
        }

        [CommandMethod("FINALIZESHEETSUIPATH")]
        public void finalizesheetsuipath()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor ed = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            resetprofileviews();
            importlabelstyles();

            var droText = File.ReadAllLines(Environment.ExpandEnvironmentVariables("%temp%") + "\\DRO.txt");

            //Create crossing points first
            DataReferencesOptions dro = new DataReferencesOptions(droText[0], droText[1]);
            #region The whole sequence
            createlerdatapssmethod2(dro);

            //Populateprofileviews with crossing data
            populateprofilesmethod(dro);

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForWrite) as BlockTable;

                    #region Stylize Profile Views
                    HashSet<ProfileView> pvs = localDb.HashSetOfType<ProfileView>(tx);

                    Oid pvStyleId = Oid.Null;
                    try
                    {
                        pvStyleId = civilDoc.Styles.ProfileViewStyles["PROFILE VIEW L TO R 1:250:100"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nProfile view style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    foreach (ProfileView pv in pvs)
                    {
                        pv.CheckOrOpenForWrite();
                        pv.StyleId = pvStyleId;

                        Oid alId = pv.AlignmentId;
                        Alignment al = alId.Go<Alignment>(tx);

                        ObjectIdCollection psIds = al.GetProfileIds();
                        HashSet<Profile> ps = new HashSet<Profile>();
                        foreach (Oid oid in psIds) ps.Add(oid.Go<Profile>(tx));

                        Profile surfaceProfile = ps.Where(x => x.Name.Contains("surface")).FirstOrDefault();
                        Oid surfaceProfileId = Oid.Null;
                        if (surfaceProfile != null) surfaceProfileId = surfaceProfile.ObjectId;
                        else ed.WriteMessage("\nSurface profile not found!");

                        Profile topProfile = ps.Where(x => x.Name.Contains("TOP")).FirstOrDefault();
                        Oid topProfileId = Oid.Null;
                        if (topProfile != null) topProfileId = topProfile.ObjectId;
                        else ed.WriteMessage("\nTop profile not found!");

                        //this doesn't quite work
                        Oid pvbsId = civilDoc.Styles.ProfileViewBandSetStyles["EG-FG Elevations and Stations"];
                        ProfileViewBandSet pvbs = pv.Bands;
                        pvbs.ImportBandSetStyle(pvbsId);

                        //try this
                        Oid pvBSId1 = civilDoc.Styles.BandStyles.ProfileViewProfileDataBandStyles["Elevations and Stations"];
                        Oid pvBSId2 = civilDoc.Styles.BandStyles.ProfileViewProfileDataBandStyles["TitleBuffer"];
                        ProfileViewBandItemCollection pvic = new ProfileViewBandItemCollection(pv.Id, BandLocationType.Bottom);
                        pvic.Add(pvBSId1);
                        pvic.Add(pvBSId2);
                        pvbs.SetBottomBandItems(pvic);

                        ProfileViewBandItemCollection pbic = pvbs.GetBottomBandItems();
                        for (int i = 0; i < pbic.Count; i++)
                        {
                            ProfileViewBandItem pvbi = pbic[i];
                            if (i == 0) pvbi.Gap = 0;
                            else if (i == 1) pvbi.Gap = 0.016;
                            if (surfaceProfileId != Oid.Null) pvbi.Profile1Id = surfaceProfileId;
                            if (topProfileId != Oid.Null) pvbi.Profile2Id = topProfileId;
                            pvbi.LabelAtStartStation = true;
                            pvbi.LabelAtEndStation = true;
                        }
                        pvbs.SetBottomBandItems(pbic);

                        #region Scale LER block
                        if (bt.Has(pv.Name))
                        {
                            BlockTableRecord btr = tx.GetObject(bt[pv.Name], OpenMode.ForRead)
                                as BlockTableRecord;
                            ObjectIdCollection brefIds = btr.GetBlockReferenceIds(false, true);

                            foreach (Oid oid in brefIds)
                            {
                                BlockReference bref = oid.Go<BlockReference>(tx, OpenMode.ForWrite);
                                bref.ScaleFactors = new Scale3d(1, 2.5, 1);
                            }

                        }
                        #endregion
                    }
                    #endregion

                    #region ProfileStyles
                    Oid pPipeStyleKantId = Oid.Null;
                    try
                    {
                        pPipeStyleKantId = civilDoc.Styles.ProfileStyles["PROFIL STYLE MGO KANT"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nPROFIL STYLE MGO KANT style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    Oid pPipeStyleMidtId = Oid.Null;
                    try
                    {
                        pPipeStyleMidtId = civilDoc.Styles.ProfileStyles["PROFIL STYLE MGO MIDT"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nPROFIL STYLE MGO MIDT style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    Oid pTerStyleId = Oid.Null;
                    try
                    {
                        pTerStyleId = civilDoc.Styles.ProfileStyles["Terræn"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nTerræn style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    Oid alStyleId = Oid.Null;
                    try
                    {
                        alStyleId = civilDoc.Styles.AlignmentStyles["FJV TRACE NO SHOW"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nFJV TRACE NO SHOW style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    Oid alLabelSetStyleId = Oid.Null;
                    try
                    {
                        alLabelSetStyleId = civilDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles["STD 20-5"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nSTD 20-5 style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    Oid crestCurveLabelId = Oid.Null;
                    try
                    {
                        crestCurveLabelId = civilDoc.Styles.LabelStyles.ProfileLabelStyles.CurveLabelStyles["Radius Crest"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nRADIUS CREST style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    Oid sagCurveLabelId = Oid.Null;
                    try
                    {
                        sagCurveLabelId = civilDoc.Styles.LabelStyles.ProfileLabelStyles.CurveLabelStyles["Radius Sag"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nRADIUS SAG style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);
                    foreach (Alignment al in als)
                    {
                        al.CheckOrOpenForWrite();
                        al.StyleId = alStyleId;
                        al.ImportLabelSet(alLabelSetStyleId);

                        ObjectIdCollection pIds = al.GetProfileIds();
                        foreach (Oid oid in pIds)
                        {
                            Profile p = oid.Go<Profile>(tx);
                            if (p.Name == $"{al.Name}_surface_P")
                            {
                                p.CheckOrOpenForWrite();
                                p.StyleId = pTerStyleId;
                            }
                            else
                            {
                                p.CheckOrOpenForWrite();
                                p.StyleId = pPipeStyleKantId;

                                if (p.Name.Contains("MIDT"))
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
                        }
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    ed.WriteMessage("\n" + ex.ToString());
                    return;
                }
                tx.Commit();
            }

            //Create detailing blocks on top of exaggerated views
            //Detect if drawing has "MIDT" profile
            bool hasMidt = false;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                var profiles = localDb.HashSetOfType<Profile>(tx);
                if (profiles.Any(x => x.Name.Contains("MIDT"))) hasMidt = true;
            }
            if (hasMidt) createdetailingmethod(dro, localDb);
            else createdetailingpreliminarymethod(dro, localDb);

            //Auto stagger all labels to right
            staggerlabelsall();
            //Draw rectangles representing viewports around longitudinal profiles
            //Can be used to check if labels are inside
            drawviewportrectangles();
            //Colorize layer as per krydsninger table
            colorizealllerlayersmethod();
            #endregion

            Interaction.TaskDialog("Finilization finished!", "OK", "Not OK");
        }

        [CommandMethod("LISTNUMBEROFPROFILEVIEWS")]
        public void listnumberofprofileviews()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Determine number of profileviews with LER data
                    var droText = File.ReadAllLines(Environment.ExpandEnvironmentVariables("%temp%") + "\\DRO.txt");

                    //Create crossing points first
                    DataReferencesOptions dro = new DataReferencesOptions(droText[0], droText[1]);
                    string projectName = dro.ProjectName;
                    string etapeName = dro.EtapeName;

                    string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";
                    System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");

                    Plane plane = new Plane();

                    #region Load linework from LER Xref
                    ILer3dManager lman = Ler3dManagerFactory.LoadLer3d(
                        GetPathToDataFiles(projectName, etapeName, "Ler"));
                    #endregion

                    try
                    {
                        List<Alignment> als = localDb.ListOfType<Alignment>(tx);
                        if (als.Count > 1)
                        {
                            prdDbg("Multiple alignments detected in drawing! Must only be one!");
                            throw new System.Exception("Multiple alignments!");
                        }
                        Alignment alignment = als.First();
                        var pvs = alignment.GetProfileViewIds().Entities<ProfileView>(tx);

                        HashSet<double> stationsOfIntersection = new HashSet<double>();
                        var remoteLerData = lman.GetIntersectingEntities(alignment);

                        foreach (Entity ent in remoteLerData)
                        {
                            using (Point3dCollection p3dcol = new Point3dCollection())
                            {
                                alignment.IntersectWith(
                                    ent,
                                    Autodesk.AutoCAD.DatabaseServices.Intersect.OnBothOperands,
                                    plane, p3dcol, new IntPtr(0), new IntPtr(0));

                                if (p3dcol.Count > 0)
                                    foreach (Point3d p3d in p3dcol)
                                        stationsOfIntersection.Add(
                                            alignment.StationAtPoint(p3d));
                            }
                        }

                        int pvsWithLer = 0;
                        foreach (ProfileView pv in pvs)
                        {
                            if (stationsOfIntersection.Any(
                                x => pv.StationStart <= x && pv.StationEnd >= x))
                                pvsWithLer++;
                        }

                        prdDbg($"Number of PVs with LER: {{{pvsWithLer}}}");

                        var path = Environment.ExpandEnvironmentVariables("%temp%");
                        string fileName = path + "\\pvCount.txt";
                        File.WriteAllText(fileName, pvsWithLer.ToString());
                    }
                    catch (System.Exception ex)
                    {
                        prdDbg(ex);
                        throw;
                    }
                    finally
                    {
                        lman.Dispose(true);
                    }

                    System.Windows.Forms.Application.DoEvents();

                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                finally
                {
                }
                tx.Abort();
            }
        }

        [CommandMethod("resetprofileviews")]
        public void resetprofileviews()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor ed = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            deletedetailingmethod(localDb);

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Delete cogo points
                    CogoPointCollection cogoPoints = civilDoc.CogoPoints;
                    ObjectIdCollection cpIds = new ObjectIdCollection();
                    foreach (Oid oid in cogoPoints) cpIds.Add(oid);
                    foreach (Oid oid in cpIds) cogoPoints.Remove(oid);
                    #endregion

                    #region Stylize Profile Views
                    HashSet<ProfileView> pvs = localDb.HashSetOfType<ProfileView>(tx);

                    Oid pvStyleId = Oid.Null;
                    try
                    {
                        pvStyleId = civilDoc.Styles.ProfileViewStyles["PROFILE VIEW L TO R NO SCALE"];
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage($"\nProfile view style missing! Run IMPORTLABELSTYLES.");
                        tx.Abort();
                        return;
                    }

                    foreach (ProfileView pv in pvs)
                    {
                        pv.CheckOrOpenForWrite();
                        pv.StyleId = pvStyleId;

                        var brs = localDb.HashSetOfType<BlockReference>(tx);
                        foreach (BlockReference br in brs)
                        {
                            if (br.Name == pv.Name)
                            {
                                br.CheckOrOpenForWrite();
                                br.Erase(true);
                            }
                        }
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage("\n" + ex.Message);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("colorviewframes")]
        public void colorviewframes()
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
                    #region XrefNodeMethod
                    //XrefGraph graph = localDb.GetHostDwgXrefGraph(false);

                    ////skip node zero, hence i=1
                    //for (int i = 1; i < graph.NumNodes; i++)
                    //{
                    //    XrefGraphNode node = graph.GetXrefNode(i);
                    //    if (node.Name.Contains("alignment"))
                    //    {
                    //        editor.WriteMessage($"\nXref: {node.Name}.");
                    //        node.
                    //    }
                    //} 
                    #endregion

                    var bt = (BlockTable)tx.GetObject(localDb.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                    foreach (Oid id in ms)
                    {
                        var br = tx.GetObject(id, OpenMode.ForRead) as BlockReference;
                        if (br != null)
                        {
                            var bd = (BlockTableRecord)tx.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                            if (bd.IsFromExternalReference)
                            {
                                var xdb = bd.GetXrefDatabase(false);
                                if (xdb != null)
                                {
                                    string fileName = xdb.Filename;
                                    if (fileName.Contains("_VF"))
                                    {
                                        editor.WriteMessage($"\n{xdb.Filename}.");
                                        System.Windows.Forms.Application.DoEvents();
                                        if (IsFileLockedOrReadOnly(new FileInfo(fileName)))
                                        {
                                            editor.WriteMessage("\nUnable to modify the external reference. " +
                                                                  "It may be open in the editor or read-only.");
                                            System.Windows.Forms.Application.DoEvents();
                                        }
                                        else
                                        {
                                            using (var xf = XrefFileLock.LockFile(xdb.XrefBlockId))
                                            {
                                                //Make sure the original symbols are loaded
                                                xdb.RestoreOriginalXrefSymbols();
                                                // Depending on the operation you're performing,
                                                // you may need to set the WorkingDatabase to
                                                // be that of the Xref
                                                //HostApplicationServices.WorkingDatabase = xdb;

                                                using (Transaction xTx = xdb.TransactionManager.StartTransaction())
                                                {
                                                    try
                                                    {
                                                        CivilDocument stylesDoc = CivilDocument.GetCivilDocument(xdb);

                                                        //View Frame Styles edit
                                                        Oid vfsId = stylesDoc.Styles.ViewFrameStyles["Basic"];
                                                        ViewFrameStyle vfs = xTx.GetObject(vfsId, OpenMode.ForWrite) as ViewFrameStyle;
                                                        DisplayStyle planStyle = vfs.GetViewFrameBoundaryDisplayStylePlan();
                                                        planStyle.Color = Color.FromColorIndex(ColorMethod.ByLayer, 256);

                                                        string layName = "C-ANNO-VFRM";

                                                        LayerTable lt = xTx.GetObject(xdb.LayerTableId, OpenMode.ForRead)
                                                            as LayerTable;
                                                        if (lt.Has(layName))
                                                        {
                                                            LayerTableRecord ltr = xTx.GetObject(lt[layName], OpenMode.ForWrite)
                                                                as LayerTableRecord;
                                                            ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 7);
                                                        }
                                                    }
                                                    catch (System.Exception)
                                                    {
                                                        xTx.Abort();
                                                        tx.Abort();
                                                        return;
                                                        //throw;
                                                    }

                                                    xTx.Commit();
                                                }
                                                // And then set things back, afterwards
                                                //HostApplicationServices.WorkingDatabase = db;
                                                xdb.RestoreForwardingXrefSymbols();
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    string viewFrameLayerName = "C-ANNO-VFRM";

                    LayerTable localLt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead)
                        as LayerTable;

                    short[] sequenceInit = new short[] { 1, 3, 4, 5, 6, 40 };
                    LinkedList<short> colorSequence = new LinkedList<short>(sequenceInit);
                    LinkedListNode<short> curNode;
                    curNode = colorSequence.First;

                    foreach (Oid id in localLt)
                    {
                        LayerTableRecord ltr = (LayerTableRecord)tx.GetObject(id, OpenMode.ForRead);

                        if (ltr.Name.Contains("_VF") &&
                            ltr.Name.Contains(viewFrameLayerName) &&
                            !ltr.Name.Contains("TEXT"))
                        {
                            editor.WriteMessage($"\n{ltr.Name}");
                            System.Windows.Forms.Application.DoEvents();
                            ltr.UpgradeOpen();
                            //Set color back to black
                            //ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 0);

                            //Set color
                            ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, curNode.Value);
                            if (curNode.Next == null) curNode = colorSequence.First;
                            else curNode = curNode.Next;

                        }
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

        [CommandMethod("HALXREFS")]
        public void halxrefs()
        {

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tx.GetObject(localDb.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                    foreach (Oid id in ms)
                    {
                        var br = tx.GetObject(id, OpenMode.ForRead) as BlockReference;
                        if (br == null) continue;

                        var bd = (BlockTableRecord)tx.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                        if (!bd.IsFromExternalReference) continue;

                        var xdb = bd.GetXrefDatabase(false);
                        if (xdb == null) continue;
                        string fileName = xdb.Filename;

                        editor.WriteMessage($"\n{xdb.Filename}.");
                        System.Windows.Forms.Application.DoEvents();
                        if (IsFileLockedOrReadOnly(new FileInfo(fileName)))
                        {
                            editor.WriteMessage("\nUnable to modify the external reference. " +
                                                  "It may be open in the editor or read-only.");
                            System.Windows.Forms.Application.DoEvents();
                        }
                        else
                        {
                            using (var xf = XrefFileLock.LockFile(xdb.XrefBlockId))
                            {
                                //Make sure the original symbols are loaded
                                xdb.RestoreOriginalXrefSymbols();
                                // Depending on the operation you're performing,
                                // you may need to set the WorkingDatabase to
                                // be that of the Xref
                                //HostApplicationServices.WorkingDatabase = xdb;

                                using (Transaction xTx = xdb.TransactionManager.StartTransaction())
                                {
                                    try
                                    {
                                        CivilDocument sDoc = CivilDocument.GetCivilDocument(xdb);
                                        Oid alStyle;
                                        Oid labelSetStyle;
                                        if (sDoc.Styles.AlignmentStyles.Contains("FJV TRACE NO SHOW") &&
                                            sDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles
                                            .Contains("_No Labels"))
                                        {
                                            alStyle = sDoc.Styles.AlignmentStyles["FJV TRACE NO SHOW"];
                                            labelSetStyle = sDoc.Styles.LabelSetStyles
                                                .AlignmentLabelSetStyles["_No Labels"];

                                            HashSet<Alignment> als = xdb.HashSetOfType<Alignment>(xTx);

                                            foreach (Alignment al in als)
                                            {
                                                al.CheckOrOpenForWrite();
                                                al.StyleId = alStyle;
                                                al.ImportLabelSet(labelSetStyle);
                                            }
                                        }
                                    }
                                    catch (System.Exception)
                                    {
                                        xTx.Abort();
                                        tx.Abort();
                                        xdb.RestoreForwardingXrefSymbols();
                                        return;
                                        //throw;
                                    }

                                    xTx.Commit();
                                }
                                // And then set things back, afterwards
                                //HostApplicationServices.WorkingDatabase = db;
                                xdb.RestoreForwardingXrefSymbols();
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

        [CommandMethod("RALXREFS")]
        public void ralxrefs()
        {

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tx.GetObject(localDb.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                    foreach (Oid id in ms)
                    {
                        var br = tx.GetObject(id, OpenMode.ForRead) as BlockReference;
                        if (br == null) continue;

                        var bd = (BlockTableRecord)tx.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                        if (!bd.IsFromExternalReference) continue;

                        var xdb = bd.GetXrefDatabase(false);
                        if (xdb == null) continue;
                        string fileName = xdb.Filename;

                        editor.WriteMessage($"\n{xdb.Filename}.");
                        System.Windows.Forms.Application.DoEvents();
                        if (IsFileLockedOrReadOnly(new FileInfo(fileName)))
                        {
                            editor.WriteMessage("\nUnable to modify the external reference. " +
                                                  "It may be open in the editor or read-only.");
                            System.Windows.Forms.Application.DoEvents();
                        }
                        else
                        {
                            using (var xf = XrefFileLock.LockFile(xdb.XrefBlockId))
                            {
                                //Make sure the original symbols are loaded
                                xdb.RestoreOriginalXrefSymbols();
                                // Depending on the operation you're performing,
                                // you may need to set the WorkingDatabase to
                                // be that of the Xref
                                //HostApplicationServices.WorkingDatabase = xdb;

                                using (Transaction xTx = xdb.TransactionManager.StartTransaction())
                                {
                                    try
                                    {
                                        CivilDocument sDoc = CivilDocument.GetCivilDocument(xdb);
                                        Oid alStyle;
                                        Oid labelSetStyle;
                                        if (sDoc.Styles.AlignmentStyles.Contains("FJV TRACÉ SHOW") &&
                                            sDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles
                                            .Contains("STD 20-5"))
                                        {
                                            alStyle = sDoc.Styles.AlignmentStyles["FJV TRACÉ SHOW"];
                                            labelSetStyle = sDoc.Styles.LabelSetStyles
                                                .AlignmentLabelSetStyles["STD 20-5"];

                                            HashSet<Alignment> als = xdb.HashSetOfType<Alignment>(xTx);

                                            foreach (Alignment al in als)
                                            {
                                                al.CheckOrOpenForWrite();
                                                al.StyleId = alStyle;
                                                al.ImportLabelSet(labelSetStyle);
                                            }
                                        }
                                    }
                                    catch (System.Exception)
                                    {
                                        xTx.Abort();
                                        tx.Abort();
                                        xdb.RestoreForwardingXrefSymbols();
                                        return;
                                        //throw;
                                    }

                                    xTx.Commit();
                                }
                                // And then set things back, afterwards
                                //HostApplicationServices.WorkingDatabase = db;
                                xdb.RestoreForwardingXrefSymbols();
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

        [CommandMethod("EXPORTVIEWFRAMESTOGEOJSON")]
        public void exportviewframestogeojson()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;

            GeoJsonFeatureCollection gjfc = new GeoJsonFeatureCollection("ViewFrames");

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tx.GetObject(localDb.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                    foreach (Oid id in ms)
                    {
                        var br = tx.GetObject(id, OpenMode.ForRead) as BlockReference;
                        if (br == null) continue;

                        var bd = (BlockTableRecord)tx.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                        if (!bd.IsFromExternalReference) continue;

                        var xdb = bd.GetXrefDatabase(false);
                        if (xdb == null) continue;
                        string fileName = xdb.Filename;

                        //editor.WriteMessage($"\n{xdb.Filename}.");
                        System.Windows.Forms.Application.DoEvents();
                        if (IsFileLockedOrReadOnly(new FileInfo(fileName)))
                        {
                            editor.WriteMessage("\nUnable to modify the external reference. " +
                                                  "It may be open in the editor or read-only.");
                            System.Windows.Forms.Application.DoEvents();
                        }
                        else
                        {
                            using (var xf = XrefFileLock.LockFile(xdb.XrefBlockId))
                            {
                                //Make sure the original symbols are loaded
                                xdb.RestoreOriginalXrefSymbols();
                                // Depending on the operation you're performing,
                                // you may need to set the WorkingDatabase to
                                // be that of the Xref
                                //HostApplicationServices.WorkingDatabase = xdb;

                                using (Transaction xTx = xdb.TransactionManager.StartTransaction())
                                {
                                    try
                                    {
                                        var vfs = xdb.HashSetOfType<ViewFrame>(xTx);

                                        foreach (Entity vf in vfs)
                                        {
                                            var converter = ViewFrameToGeoJsonConverterFactory.CreateConverter(vf);
                                            if (converter == null) continue;
                                            var geoJsonFeature = converter.Convert(vf);
                                            gjfc.Features.AddRange(geoJsonFeature);
                                        }
                                    }
                                    catch (System.Exception)
                                    {
                                        xTx.Abort();
                                        tx.Abort();
                                        xdb.RestoreForwardingXrefSymbols();
                                        return;
                                        //throw;
                                    }

                                    xTx.Commit();
                                }
                                // And then set things back, afterwards
                                //HostApplicationServices.WorkingDatabase = db;
                                xdb.RestoreForwardingXrefSymbols();
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

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            string path = Path.GetDirectoryName(localDb.Filename);
            string geoJsonFileName = Path.Combine(path, "ViewFrames.geojson");

            string json = JsonSerializer.Serialize(gjfc, options);
            File.WriteAllText(geoJsonFileName, json);
        }

        [CommandMethod("EXPORTVIEWFRAMESTODWG")]
        public void exportviewframestodwg()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            Database blockDb = new Database(false, true);
            if (!File.Exists(@"X:\AutoCAD DRI - SETUP\Templates\acadiso.dwt"))
                throw new System.Exception(@"X:\AutoCAD DRI - SETUP\Templates\acadiso.dwt does not exist!");
            blockDb.ReadDwgFile(@"X:\AutoCAD DRI - SETUP\Templates\acadiso.dwt", 
                FileOpenMode.OpenForReadAndAllShare, false, null);

            string lyrName = "0-ViewFrames";
            blockDb.CheckOrCreateLayer(lyrName);

            string hostFileName = localDb.Filename;

            using (Transaction blockTx = blockDb.TransactionManager.StartTransaction())
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var bt = (BlockTable)tx.GetObject(localDb.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                    foreach (Oid id in ms)
                    {
                        var br = tx.GetObject(id, OpenMode.ForRead) as BlockReference;
                        if (br == null) continue;

                        var bd = (BlockTableRecord)tx.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                        if (!bd.IsFromExternalReference) continue;

                        var xdb = bd.GetXrefDatabase(false);
                        if (xdb == null) continue;
                        string fileName = xdb.Filename;

                        //editor.WriteMessage($"\n{xdb.Filename}.");
                        System.Windows.Forms.Application.DoEvents();
                        if (IsFileLockedOrReadOnly(new FileInfo(fileName)))
                        {
                            prdDbg("\nUnable to modify the external reference. " +
                                "It may be open in the editor or read-only.");
                            System.Windows.Forms.Application.DoEvents();
                        }
                        else
                        {
                            using (var xf = XrefFileLock.LockFile(xdb.XrefBlockId))
                            {
                                //Make sure the original symbols are loaded
                                xdb.RestoreOriginalXrefSymbols();
                                // Depending on the operation you're performing,
                                // you may need to set the WorkingDatabase to
                                // be that of the Xref
                                //HostApplicationServices.WorkingDatabase = xdb;

                                using (Transaction xTx = xdb.TransactionManager.StartTransaction())
                                {
                                    try
                                    {
                                        var vfs = xdb.HashSetOfType<ViewFrame>(xTx);

                                        foreach (ViewFrame vf in vfs)
                                        {
                                            string name = vf.Name;

                                            DBObjectCollection objs = new DBObjectCollection();
                                            vf.Explode(objs);

                                            foreach (DBObject obj in objs)
                                            {
                                                if (obj is BlockReference vfbr)
                                                {
                                                    objs = new DBObjectCollection();
                                                    vfbr.Explode(objs);

                                                    foreach (DBObject obj2 in objs)
                                                    {
                                                        if (obj2 is Polyline pline)
                                                        {
                                                            Polyline newPline = new Polyline(pline.NumberOfVertices);
                                                            for (int i = 0; i < pline.NumberOfVertices; i++)
                                                            {
                                                                newPline.AddVertexAt(i, pline.GetPoint2dAt(i), 0, 0, 0);
                                                            }

                                                            newPline.Closed = true;
                                                            newPline.AddEntityToDbModelSpace(blockDb);
                                                            newPline.Layer = lyrName;

                                                            LineSegment2d segment = newPline.GetLineSegment2dAt(0);

                                                            var text = new Autodesk.AutoCAD.DatabaseServices.MText();
                                                            text.Contents = name;
                                                            text.TextHeight = 10;

                                                            double rotationRadians = segment.Direction.Angle;
                                                            double rotationDegrees = rotationRadians * (180.0 / Math.PI);

                                                            if (rotationDegrees > 90 && rotationDegrees < 270)
                                                                rotationRadians += Math.PI;

                                                            if (name == "011")
                                                                prdDbg($"Rad: {rotationRadians}, Deg: {rotationDegrees}");

                                                            text.Rotation = rotationRadians;

                                                            Extents3d extents = newPline.GeometricExtents;
                                                            Point3d center = new Point3d(
                                                                (extents.MaxPoint.X + extents.MinPoint.X) / 2.0,
                                                                (extents.MaxPoint.Y + extents.MinPoint.Y) / 2.0,
                                                                0);
                                                            
                                                            text.Location = center;
                                                            text.Attachment = AttachmentPoint.MiddleCenter;

                                                            text.BackgroundFill = true;
                                                            text.UseBackgroundColor = true;
                                                            text.BackgroundScaleFactor = 1.2;

                                                            text.AddEntityToDbModelSpace(blockDb);
                                                            text.Layer = lyrName;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    catch (System.Exception)
                                    {
                                        xTx.Abort();
                                        tx.Abort();
                                        xdb.RestoreForwardingXrefSymbols();
                                        blockTx.Abort();
                                        blockTx.Dispose();
                                        blockDb.Dispose();
                                        return;
                                        //throw;
                                    }

                                    xTx.Commit();
                                }
                                // And then set things back, afterwards
                                //HostApplicationServices.WorkingDatabase = db;
                                xdb.RestoreForwardingXrefSymbols();
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    blockTx.Abort();
                    blockTx.Dispose();
                    blockDb.Dispose();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
                blockTx.Commit();

                var path = Path.Combine(Path.GetDirectoryName(hostFileName), "ViewFrames.dwg");
                prdDbg("Filen gemt i " + path);
                blockDb.SaveAs(path, DwgVersion.Newest);
            }

            blockDb.Dispose();
        }

        [CommandMethod("EXPORTFJVTOGEOJSON")]
        public void exportfjvtogeojson3()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;

            System.Data.DataTable dt = CsvReader.ReadCsvToDataTable(
                @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");

            GeoJsonFeatureCollection gjfc = new GeoJsonFeatureCollection("FjernVarme");

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var ents = localDb.GetFjvEntities(tx, true);

                    foreach (var ent in ents)
                    {
                        var converter = FjvToGeoJsonConverterFactory.CreateConverter(ent);
                        if (converter == null) continue;
                        var geoJsonFeature = converter.Convert(ent);
                        gjfc.Features.AddRange(geoJsonFeature);
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

            var encoderSettings = new TextEncoderSettings();
            encoderSettings.AllowRange(UnicodeRanges.All);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = JavaScriptEncoder.Create(encoderSettings)
            };

            string path = Path.GetDirectoryName(localDb.Filename);

            string geoJsonFileName = Path.Combine(path, "Fjernvarme.geojson");
            string json = JsonSerializer.Serialize(gjfc, options);

            try
            {
                File.WriteAllText(geoJsonFileName, json);
            }
            catch (System.IO.IOException)
            {
                prdDbg("File is locked for write! Abort operation.");
                return;
            }
        }

        [CommandMethod("SETPROFILEVIEWSTYLE")]
        public void setprofileviewstyle()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor ed = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForWrite) as BlockTable;

                    #region Stylize Profile Views
                    HashSet<ProfileView> pvs = localDb.HashSetOfType<ProfileView>(tx);

                    foreach (ProfileView pv in pvs)
                    {
                        pv.CheckOrOpenForWrite();

                        ObjectIdCollection psIds = pv.AlignmentId.Go<Alignment>(tx).GetProfileIds();
                        HashSet<Profile> ps = new HashSet<Profile>();
                        foreach (Oid oid in psIds) ps.Add(oid.Go<Profile>(tx));

                        Profile surfaceProfile = ps.Where(x => x.Name.Contains("surface")).FirstOrDefault();
                        Oid surfaceProfileId = Oid.Null;
                        if (surfaceProfile != null) surfaceProfileId = surfaceProfile.ObjectId;
                        else ed.WriteMessage("\nSurface profile not found!");

                        //this doesn't quite work
                        Oid pvbsId = civilDoc.Styles.ProfileViewBandSetStyles["EG-FG Elevations and Stations"];
                        ProfileViewBandSet pvbs = pv.Bands;
                        pvbs.ImportBandSetStyle(pvbsId);

                        //try this
                        Oid pvBSId1 = civilDoc.Styles.BandStyles.ProfileViewProfileDataBandStyles["Elevations and Stations"];
                        Oid pvBSId2 = civilDoc.Styles.BandStyles.ProfileViewProfileDataBandStyles["TitleBuffer"];
                        ProfileViewBandItemCollection pvic = new ProfileViewBandItemCollection(pv.Id, BandLocationType.Bottom);
                        pvic.Add(pvBSId1);
                        pvic.Add(pvBSId2);
                        pvbs.SetBottomBandItems(pvic);

                        ProfileViewBandItemCollection pbic = pvbs.GetBottomBandItems();
                        for (int i = 0; i < pbic.Count; i++)
                        {
                            ProfileViewBandItem pvbi = pbic[i];
                            if (i == 0) pvbi.Gap = 0;
                            else if (i == 1) pvbi.Gap = 0.016;
                            if (surfaceProfileId != Oid.Null) pvbi.Profile1Id = surfaceProfileId;
                            pvbi.LabelAtStartStation = true;
                            pvbi.LabelAtEndStation = true;
                        }
                        pvbs.SetBottomBandItems(pbic);
                    }
                    #endregion

                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    ed.WriteMessage("\n" + ex.ToString());
                    return;
                }
                tx.Commit();
            }
        }
    }
}
