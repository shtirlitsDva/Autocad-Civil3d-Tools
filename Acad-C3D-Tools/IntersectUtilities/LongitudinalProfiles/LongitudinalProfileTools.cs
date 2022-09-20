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

namespace IntersectUtilities
{
    public partial class Intersect
    {
        [CommandMethod("CREATEPOINTSATVERTICES")]
        public void createpointsatvertices()
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
                    #region Load linework
                    HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx);
                    #endregion

                    #region Layer handling
                    string localLayerName = "0-PLDECORATOR";
                    bool localLayerExists = false;

                    LayerTable lt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;
                    if (lt.Has(localLayerName))
                    {
                        localLayerExists = true;
                    }
                    else
                    {
                        //Create layer if it doesn't exist
                        try
                        {
                            //Validate the name of layer
                            //It throws an exception if not, so need to catch it
                            SymbolUtilityServices.ValidateSymbolName(localLayerName, false);

                            LayerTableRecord ltr = new LayerTableRecord();
                            ltr.Name = localLayerName;

                            //Make layertable writable
                            lt.UpgradeOpen();

                            //Add the new layer to layer table
                            Oid ltId = lt.Add(ltr);
                            tx.AddNewlyCreatedDBObject(ltr, true);

                            //Flag that the layer exists now
                            localLayerExists = true;

                        }
                        catch (System.Exception)
                        {
                            //Eat the exception and continue
                            //localLayerExists must remain false
                        }
                    }
                    #endregion

                    #region Decorate polyline vertices
                    BlockTableRecord space = (BlockTableRecord)tx.GetObject(localDb.CurrentSpaceId, OpenMode.ForWrite);
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForWrite) as BlockTable;

                    foreach (Polyline pline in plines)
                    {
                        int numOfVerts = pline.NumberOfVertices - 1;
                        for (int i = 0; i < numOfVerts; i++)
                        {
                            Point3d location = pline.GetPoint3dAt(i);
                            using (var pt = new DBPoint(new Point3d(location.X, location.Y, 0)))
                            {
                                space.AppendEntity(pt);
                                tx.AddNewlyCreatedDBObject(pt, true);
                                pt.Layer = localLayerName;
                            }
                        }
                    }
                    #endregion
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

        [CommandMethod("CREATEOFFSETPROFILES")]
        public void createoffsetprofiles()
        {
            createoffsetprofilesmethod();
        }
        public void createoffsetprofilesmethod(Profile p = null, ProfileView pv = null,
            DataReferencesOptions dataReferencesOptions = null)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                Profile profile;
                ProfileView profileView;

                profile = p;
                profileView = pv;

                #region Select Profile
                if (p == null)
                {
                    PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions("\n Select a profile: ");
                    promptEntityOptions1.SetRejectMessage("\n Not a profile!");
                    promptEntityOptions1.AddAllowedClass(typeof(Profile), true);
                    PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    Autodesk.AutoCAD.DatabaseServices.ObjectId profileId = entity1.ObjectId;
                    profile = profileId.Go<Profile>(tx);
                }

                #endregion

                #region Select Profile View
                if (profileView == null)
                {
                    PromptEntityOptions promptEntityOptions2 = new PromptEntityOptions("\n Select a ProfileView: ");
                    promptEntityOptions2.SetRejectMessage("\n Not a ProfileView");
                    promptEntityOptions2.AddAllowedClass(typeof(ProfileView), true);
                    PromptEntityResult entity2 = editor.GetEntity(promptEntityOptions2);
                    if (((PromptResult)entity2).Status != PromptStatus.OK) return;
                    profileView = tx.GetObject(entity2.ObjectId, OpenMode.ForWrite) as ProfileView;
                }
                #endregion

                #region Open fremtidig db
                DataReferencesOptions dro = dataReferencesOptions;
                if (dro == null) dro = new DataReferencesOptions();
                string projectName = dro.ProjectName;
                string etapeName = dro.EtapeName;

                //open the xref database
                Database fremDb = new Database(false, true);
                fremDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Fremtid"),
                    FileOpenMode.OpenForReadAndAllShare, false, null);
                Transaction fremTx = fremDb.TransactionManager.StartTransaction();
                HashSet<Curve> allCurves = fremDb.HashSetOfType<Curve>(fremTx);
                HashSet<BlockReference> allBrs = fremDb.HashSetOfType<BlockReference>(fremTx);
                #endregion

                #region Initialize PS for Alignment
                PropertySetManager psmPipeLineData = new PropertySetManager(
                    fremDb,
                    PSetDefs.DefinedSets.DriPipelineData);
                PSetDefs.DriPipelineData driPipelineData =
                    new PSetDefs.DriPipelineData();
                #endregion

                //////////////////////////////////////
                string profileLayerName = "0-FJV-PROFILE";
                //////////////////////////////////////

                try
                {
                    #region Create layer for profile
                    using (Transaction txLag = localDb.TransactionManager.StartTransaction())
                    {

                        LayerTable lt = txLag.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;

                        if (!lt.Has(profileLayerName))
                        {
                            LayerTableRecord ltr = new LayerTableRecord();
                            ltr.Name = profileLayerName;
                            ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 6);
                            ltr.LineWeight = LineWeight.LineWeight030;
                            ltr.IsPlottable = false;

                            //Make layertable writable
                            lt.CheckOrOpenForWrite();

                            //Add the new layer to layer table
                            Oid ltId = lt.Add(ltr);
                            txLag.AddNewlyCreatedDBObject(ltr, true);
                        }
                        txLag.Commit();
                    }
                    #endregion

                    #region Initialize variables
                    Plane plane = new Plane(); //For intersecting

                    Alignment al = profileView.AlignmentId.Go<Alignment>(tx);

                    Point3d pvOrigin = profileView.Location;
                    double originX = pvOrigin.X;
                    double originY = pvOrigin.Y;

                    double pvStStart = profileView.StationStart;
                    double pvStEnd = profileView.StationEnd;
                    double pvElBottom = profileView.ElevationMin;
                    double pvElTop = profileView.ElevationMax;
                    double pvLength = pvStEnd - pvStStart;

                    //Settings
                    //double weedAngle = 5; //In degrees
                    //double weedAngleRad = weedAngle.ToRadians();
                    //double DouglasPeuckerTolerance = .05;

                    double stepLength = 0.1;
                    int nrOfSteps = (int)(pvLength / stepLength);
                    #endregion

                    #region GetCurvesAndBRs from fremtidig
                    HashSet<Curve> curves = allCurves
                            .Where(x => psmPipeLineData
                            .FilterPropetyString(x, driPipelineData.BelongsToAlignment, al.Name))
                            .ToHashSet();

                    HashSet<BlockReference> brs = allBrs
                        .Where(x => psmPipeLineData
                        .FilterPropetyString(x, driPipelineData.BelongsToAlignment, al.Name))
                        .ToHashSet();
                    #endregion

                    //PipelineSizeArray sizeArray = new PipelineSizeArray(al, curves);
                    //prdDbg("Curves:");
                    //prdDbg(sizeArray.ToString());

                    prdDbg("Blocks:");
                    PipelineSizeArray sizeArray = new PipelineSizeArray(al, curves, brs);
                    prdDbg(sizeArray.ToString());

                    #region Create polyline from centre profile
                    ProfileEntityCollection entities = profile.Entities;
                    prdDbg($"Count of entities: {entities.Count}");
                    HashSet<string> types = new HashSet<string>();

                    Polyline pline = new Polyline(entities.Count + 1);

                    //Place first point
                    ProfileEntity pe = entities.EntityAtId(entities.FirstEntity);
                    double startX = originX, startY = originY;
                    profileView.FindXYAtStationAndElevation(pe.StartStation, pe.StartElevation, ref startX, ref startY);
                    Point2d startPoint = new Point2d(startX, startY);
                    Point2d endPoint = new Point2d(originX, originY);
                    pline.AddVertexAt(0, startPoint, pe.GetBulge(profileView), 0, 0);
                    int vertIdx = 1;
                    for (int i = 0; i < entities.Count + 1; i++)
                    {
                        endPoint = profileView.GetPoint2dAtStaAndEl(pe.EndStation, pe.EndElevation);
                        double bulge = entities.LookAheadAndGetBulge(pe, profileView);
                        pline.AddVertexAt(vertIdx, endPoint, bulge, 0, 0);
                        vertIdx++;
                        startPoint = endPoint;
                        try { pe = entities.EntityAtId(pe.EntityAfter); }
                        catch (System.Exception) { break; }
                    }
                    #endregion

                    #region Create partial curves
                    HashSet<Polyline> offsetCurvesTop = new HashSet<Polyline>();
                    HashSet<Polyline> offsetCurvesBund = new HashSet<Polyline>();
                    //Small offset to avoid vertical segments in profile
                    //************************************************//
                    double pDelta = 0.125;
                    //************************************************//
                    //Create lines to split the offset curves
                    //And it happens for each size segment
                    for (int i = 0; i < sizeArray.Length; i++)
                    {
                        var size = sizeArray[i];
                        double halfKod = size.Kod / 2.0 / 1000.0;

                        HashSet<Line> splitLines = new HashSet<Line>();
                        if (i != 0)
                        {
                            Point3d sP = new Point3d(originX + sizeArray[i - 1].EndStation + pDelta, originY, 0);
                            Point3d eP = new Point3d(originX + sizeArray[i - 1].EndStation + pDelta, originY + 100, 0);
                            Line splitLineStart = new Line(sP, eP);
                            splitLines.Add(splitLineStart);
                        }
                        if (i != sizeArray.Length - 1)
                        {
                            Point3d sP = new Point3d(originX + sizeArray[i].EndStation - pDelta, originY, 0);
                            Point3d eP = new Point3d(originX + sizeArray[i].EndStation - pDelta, originY + 100, 0);
                            Line splitLineEnd = new Line(sP, eP);
                            splitLines.Add(splitLineEnd);
                        }

                        //Top offset
                        //Handle case of only one size pipe
                        if (sizeArray.Length == 1)
                        {
                            using (DBObjectCollection col = pline.GetOffsetCurves(halfKod))
                            {
                                offsetCurvesTop.Add(col[0] as Polyline);
                            }
                        }
                        else
                        {
                            using (DBObjectCollection col = pline.GetOffsetCurves(halfKod))
                            {
                                if (col.Count == 0) throw new System.Exception("Offsetting pline failed!");
                                Polyline offsetPline = col[0] as Polyline;
                                List<double> splitPts = new List<double>();
                                foreach (Line line in splitLines)
                                {
                                    Point3dCollection ipts = new Point3dCollection();
                                    offsetPline.IntersectWith(line,
                                        Autodesk.AutoCAD.DatabaseServices.Intersect.OnBothOperands,
                                        ipts, new IntPtr(0), new IntPtr(0));
                                    foreach (Point3d pt in ipts)
                                        splitPts.Add(offsetPline.GetParameterAtPoint(offsetPline.GetClosestPointTo(pt, false)));
                                }
                                if (splitPts.Count == 0) throw new System.Exception("Getting split points failed!");
                                splitPts.Sort();
                                try
                                {
                                    DBObjectCollection objs = offsetPline
                                        .GetSplitCurves(new DoubleCollection(splitPts.ToArray()));
                                    if (i == 0) offsetCurvesTop.Add(objs[0] as Polyline);
                                    else offsetCurvesTop.Add(objs[1] as Polyline);
                                }
                                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                                {
                                    Application.ShowAlertDialog(ex.Message + "\n" + ex.StackTrace);
                                    throw new System.Exception("Splitting of pline failed!");
                                }
                            }
                        }
                        //Bund offset
                        if (sizeArray.Length == 1)
                        {
                            using (DBObjectCollection col = pline.GetOffsetCurves(-halfKod))
                            {
                                offsetCurvesBund.Add(col[0] as Polyline);
                            }
                        }
                        else
                        {
                            using (DBObjectCollection col = pline.GetOffsetCurves(-halfKod))
                            {
                                if (col.Count == 0) throw new System.Exception("Offsetting pline failed!");
                                Polyline offsetPline = col[0] as Polyline;
                                List<double> splitPts = new List<double>();
                                foreach (Line line in splitLines)
                                {
                                    Point3dCollection ipts = new Point3dCollection();
                                    offsetPline.IntersectWith(line,
                                        Autodesk.AutoCAD.DatabaseServices.Intersect.OnBothOperands,
                                        ipts, new IntPtr(0), new IntPtr(0));
                                    foreach (Point3d pt in ipts)
                                        splitPts.Add(offsetPline.GetParameterAtPoint(offsetPline.GetClosestPointTo(pt, false)));
                                }
                                if (splitPts.Count == 0) throw new System.Exception("Getting split points failed!");
                                splitPts.Sort();
                                try
                                {
                                    DBObjectCollection objs = offsetPline
                                        .GetSplitCurves(new DoubleCollection(splitPts.ToArray()));
                                    if (i == 0) offsetCurvesBund.Add(objs[0] as Polyline);
                                    else offsetCurvesBund.Add(objs[1] as Polyline);
                                }
                                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                                {
                                    Application.ShowAlertDialog(ex.Message + "\n" + ex.StackTrace);
                                    throw new System.Exception("Splitting of pline failed!");
                                }
                            }
                        }
                    }
                    #endregion

                    #region Combine partial plines and convert to profile
                    //Get the number of the alignment
                    Regex regx = new Regex(@"^(?<number>\d\d\s)");
                    string number = "";
                    if (regx.IsMatch(al.Name))
                    {
                        number = regx.Match(al.Name).Groups["number"].Value;
                    }

                    //Combine to polylines
                    Polyline plineTop = new Polyline();
                    foreach (Polyline partPline in offsetCurvesTop)
                    {
                        for (int i = 0; i < partPline.NumberOfVertices; i++)
                        {
                            Point2d cp = new Point2d(partPline.GetPoint3dAt(i).X, partPline.GetPoint3dAt(i).Y);
                            plineTop.AddVertexAt(
                                plineTop.NumberOfVertices,
                                cp, partPline.GetBulgeAt(i), 0, 0);
                        }
                    }
                    Profile profileTop = CreateProfileFromPolyline(
                        number + "BUND",
                        profileView,
                        al.Name,
                        profileLayerName,
                        "PROFIL STYLE MGO",
                        "_No Labels",
                        plineTop
                        );

                    //Combine to polylines
                    Polyline plineBund = new Polyline();
                    foreach (Polyline partPline in offsetCurvesBund)
                    {
                        for (int i = 0; i < partPline.NumberOfVertices; i++)
                        {
                            Point2d cp = new Point2d(partPline.GetPoint3dAt(i).X, partPline.GetPoint3dAt(i).Y);
                            plineBund.AddVertexAt(
                                plineBund.NumberOfVertices,
                                cp, partPline.GetBulgeAt(i), 0, 0);
                        }
                    }
                    Profile profileBund = CreateProfileFromPolyline(
                        number + "TOP",
                        profileView,
                        al.Name,
                        profileLayerName,
                        "PROFIL STYLE MGO",
                        "_No Labels",
                        plineBund
                        );
                    #endregion
                }
                catch (System.Exception ex)
                {
                    fremTx.Abort();
                    fremTx.Dispose();
                    fremDb.Dispose();
                    tx.Abort();
                    prdDbg(ex.ToString());
                    return;
                }
                fremTx.Abort();
                fremTx.Dispose();
                fremDb.Dispose();
                tx.Commit();
            }
        }
        /// <summary>
        /// Creates offset profiles for all middle profiles
        /// FOR USE ONLY WITH CONTINUOUS PVs!!!
        /// </summary>
        [CommandMethod("CREATEOFFSETPROFILESALL")]
        public void createoffsetprofilesall()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                DataReferencesOptions dro = new DataReferencesOptions();

                HashSet<Profile> pvs = localDb.HashSetOfType<Profile>(tx);
                foreach (Profile profile in pvs)
                {
                    if (profile.Name.Contains("MIDT"))
                    {
                        Alignment al = profile.AlignmentId.Go<Alignment>(tx);
                        prdDbg($"Processing: {al.Name}...");
                        ProfileView pv = al.GetProfileViewIds()[0].Go<ProfileView>(tx);

                        createoffsetprofilesmethod(profile, pv, dro);
                    }
                    System.Windows.Forms.Application.DoEvents();
                }
                tx.Commit();
            }
        }

        [CommandMethod("DELETEOFFSETPROFILES")]
        public void deleteoffsetprofiles()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                HashSet<Profile> pvs = localDb.HashSetOfType<Profile>(tx);
                foreach (Profile profile in pvs)
                {
                    if (profile.Name.Contains("TOP") ||
                        profile.Name.Contains("BUND"))
                    {
                        profile.CheckOrOpenForWrite();
                        profile.Erase(true);
                    }
                }
                tx.Commit();
            }
        }

        [CommandMethod("LISTMIDTPROFILESSTARTENDSTATIONS")]
        public void listmidtprofilesstartendstations()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            //Lists MIDT profiles for which sampling of distances at start and ends failed.
            //These profiles must be fixed.

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                HashSet<Profile> pvs = localDb.HashSetOfType<Profile>(tx);
                foreach (Profile profile in pvs)
                {
                    if (profile.Name.Contains("MIDT"))
                    {
                        Alignment al = profile.AlignmentId.Go<Alignment>(tx);


                        bool success = true;
                        double startElevation = SampleProfile(profile, 0, ref success);
                        double endElevation = SampleProfile(profile, al.EndingStation, ref success);
                        if (!success)
                        {
                            prdDbg($"Processing: {al.Name}...");
                            prdDbg($"S: 0 -> {startElevation.ToString("0.0")}, E: {al.EndingStation.ToString("0.0")} -> {endElevation.ToString("0.0")}");
                        }
                    }
                    System.Windows.Forms.Application.DoEvents();
                }
                tx.Commit();
            }

            //Local method to sample profiles
            double SampleProfile(Profile profile, double station, ref bool success)
            {
                double sampledElevation = 0;
                try { sampledElevation = profile.ElevationAt(station); }
                catch (System.Exception)
                {
                    //prdDbg($"Station {station} threw an exception when sampling!");
                    success = false;
                    return 0;
                }
                return sampledElevation;
            }
        }

        [CommandMethod("createmultipleprofileviews")]
        public void createmultipleprofileviews()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database db = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                try
                {
                    BlockTableRecord space = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                    BlockTable bt = tx.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;

                    List<Alignment> allAlignments = db.ListOfType<Alignment>(tx).OrderBy(x => x.Name).ToList();
                    HashSet<ProfileView> pvSetExisting = db.HashSetOfType<ProfileView>(tx);

                    #region Select and open XREF
                    PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions("\n Select a LER XREF : ");
                    promptEntityOptions1.SetRejectMessage("\n Not a XREF");
                    promptEntityOptions1.AddAllowedClass(typeof(Autodesk.AutoCAD.DatabaseServices.BlockReference), true);
                    PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    Autodesk.AutoCAD.DatabaseServices.ObjectId blkObjId = entity1.ObjectId;
                    BlockReference blkRef = tx.GetObject(blkObjId, OpenMode.ForRead, false)
                        as Autodesk.AutoCAD.DatabaseServices.BlockReference;

                    // open the block definition?
                    BlockTableRecord blockDef = tx.GetObject(blkRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                    // is not from external reference, exit
                    if (!blockDef.IsFromExternalReference) return;

                    // open the xref database
                    Database xRefDB = new Database(false, true);
                    editor.WriteMessage($"\nPathName of the blockDef -> {blockDef.PathName}");

                    //Relative path handling
                    //I
                    string curPathName = blockDef.PathName;
                    bool isFullPath = IsFullPath(curPathName);
                    if (isFullPath == false)
                    {
                        string sourcePath = Path.GetDirectoryName(doc.Name);
                        editor.WriteMessage($"\nSourcePath -> {sourcePath}");
                        curPathName = GetAbsolutePath(sourcePath, blockDef.PathName);
                        editor.WriteMessage($"\nTargetPath -> {curPathName}");
                    }

                    xRefDB.ReadDwgFile(curPathName, FileOpenMode.OpenForReadAndAllShare, false, null);
                    #endregion

                    #region Read Csv Data for Layers and Depth

                    //Establish the pathnames to files
                    //Files should be placed in a specific folder on desktop
                    string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";
                    string pathDybde = "X:\\AutoCAD DRI - 01 Civil 3D\\Dybde.csv";

                    System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
                    System.Data.DataTable dtDybde = CsvReader.ReadCsvToDataTable(pathDybde, "Dybde");

                    #endregion

                    #region Delete existing points
                    PointGroupCollection pgs = civilDoc.PointGroups;

                    for (int i = 0; i < pgs.Count; i++)
                    {
                        PointGroup pg = tx.GetObject(pgs[i], OpenMode.ForRead) as PointGroup;
                        //If profile views already exist -- skip deleting of points
                        if (allAlignments.Any(x => x.Name == pg.Name) &&
                            !pvSetExisting.Any(x => x.Name.Contains(pg.Name + "_PV")))
                        {
                            pg.CheckOrOpenForWrite();
                            pg.Update();
                            uint[] numbers = pg.GetPointNumbers();

                            CogoPointCollection cpc = civilDoc.CogoPoints;

                            for (int j = 0; j < numbers.Length; j++)
                            {
                                uint number = numbers[j];

                                if (cpc.Contains(number))
                                {
                                    cpc.Remove(number);
                                }
                            }

                            StandardPointGroupQuery spgqEmpty = new StandardPointGroupQuery();
                            spgqEmpty.IncludeNumbers = "";
                            pg.SetQuery(spgqEmpty);

                            pg.Update();
                        }
                    }
                    #endregion

                    #region Create surface profiles and profile views

                    #region Select "surface"
                    //Get surface
                    PromptEntityOptions promptEntityOptions3 = new PromptEntityOptions("\n Select surface to get elevations: ");
                    promptEntityOptions3.SetRejectMessage("\n Not a surface");
                    promptEntityOptions3.AddAllowedClass(typeof(TinSurface), true);
                    promptEntityOptions3.AddAllowedClass(typeof(GridSurface), true);
                    PromptEntityResult entity3 = editor.GetEntity(promptEntityOptions3);
                    if (((PromptResult)entity3).Status != PromptStatus.OK) return;
                    Oid surfaceObjId = entity3.ObjectId;
                    CivSurface surface = surfaceObjId.GetObject(OpenMode.ForRead, false) as CivSurface;
                    #endregion

                    #region Get terrain layer id

                    LayerTable lt = db.LayerTableId.GetObject(OpenMode.ForRead) as LayerTable;
                    string terrainLayerName = "0_TERRAIN_PROFILE";
                    Oid terrainLayerId = Oid.Null;
                    foreach (Oid id in lt)
                    {
                        LayerTableRecord ltr = id.GetObject(OpenMode.ForRead) as LayerTableRecord;
                        if (ltr.Name == terrainLayerName) terrainLayerId = ltr.Id;
                    }
                    if (terrainLayerId == Oid.Null)
                    {
                        editor.WriteMessage("Terrain layer missing!");
                        return;
                    }

                    #endregion

                    #region ProfileView styles ids
                    Oid profileStyleId = civilDoc.Styles.ProfileStyles["Terræn"];
                    Oid profileLabelSetStyleId = civilDoc.Styles.LabelSetStyles.ProfileLabelSetStyles["_No Labels"];

                    Oid profileViewBandSetStyleId = civilDoc.Styles
                            .ProfileViewBandSetStyles["EG-FG Elevations and Stations"];
                    Oid profileViewStyleId = civilDoc.Styles
                        .ProfileViewStyles["PROFILE VIEW L TO R NO SCALE"];
                    #endregion

                    //Used to keep track of point names
                    HashSet<string> pNames = new HashSet<string>();

                    int index = 1;

                    #region Select point
                    PromptPointOptions pPtOpts = new PromptPointOptions("");
                    // Prompt for the start point
                    pPtOpts.Message = "\nSelect location where to draw first profile view:";
                    PromptPointResult pPtRes = editor.GetPoint(pPtOpts);
                    Point3d selectedPoint = pPtRes.Value;
                    // Exit if the user presses ESC or cancels the command
                    if (pPtRes.Status != PromptStatus.OK) return;
                    #endregion

                    foreach (Alignment alignment in allAlignments)
                    {
                        #region Create surface profiles
                        //If ProfileView already exists -> continue
                        if (pvSetExisting.Any(x => x.Name == $"{alignment.Name}_PV")) continue;

                        Oid surfaceProfileId = Oid.Null;
                        string profileName = $"{alignment.Name}_surface_P";
                        bool noProfileExists = true;
                        ObjectIdCollection pIds = alignment.GetProfileIds();
                        foreach (Oid pId in pIds)
                        {
                            Profile p = pId.Go<Profile>(tx);
                            if (p.Name == profileName)
                            {
                                noProfileExists = false;
                                surfaceProfileId = pId;
                            }
                        }

                        if (noProfileExists)
                        {
                            surfaceProfileId = Profile.CreateFromSurface(
                                                profileName, alignment.ObjectId, surfaceObjId,
                                                terrainLayerId, profileStyleId, profileLabelSetStyleId);
                        }
                        #endregion

                        #region Create profile view
                        #region Calculate point
                        Point3d insertionPoint = new Point3d(selectedPoint.X, selectedPoint.Y + index * -200, 0);
                        #endregion

                        //oid pvId = ProfileView.Create(alignment.ObjectId, insertionPoint,
                        //    $"{alignment.Name}_PV", profileViewBandSetStyleId, profileViewStyleId);

                        MultipleProfileViewsCreationOptions mpvco = new MultipleProfileViewsCreationOptions();
                        mpvco.DrawOrder = ProfileViewPlotType.ByRows;
                        mpvco.GapBetweenViewsInColumn = 100;
                        mpvco.GapBetweenViewsInRow = 100;
                        mpvco.LengthOfEachView = 200;
                        mpvco.MaxViewInRowOrColumn = 50;
                        mpvco.StartCorner = ProfileViewStartCornerType.LowerLeft;

                        //Naming format of created multiple PVs
                        //j = row, k = col
                        // {AlignmentName}_PV (jk)
                        ObjectIdCollection mPvIds = ProfileView.CreateMultiple(alignment.ObjectId, insertionPoint,
                            $"{alignment.Name}_PV", profileViewBandSetStyleId, profileViewStyleId, mpvco);

                        index++;
                        #endregion

                        #region Create ler data

                        //createlerdataloop(xRefDB, alignment, surface, pvId.Go<ProfileView>(tx),
                        //                  dtKrydsninger, dtDybde, ref pNames);
                        #endregion
                    }

                    #endregion

                }

                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.ToString());
                    throw new System.Exception(ex.Message);
                }
                tx.Commit();
            }
        }

        [CommandMethod("staggerlabels")]
        [CommandMethod("sg")]
        public void staggerlabels()
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
                    #region Get the selection set of all objects and profile view
                    PromptSelectionOptions pOptions = new PromptSelectionOptions();
                    PromptSelectionResult sSetResult = editor.GetSelection(pOptions);
                    if (sSetResult.Status != PromptStatus.OK) return;
                    HashSet<Entity> allEnts = sSetResult.Value.GetObjectIds().Select(e => e.Go<Entity>(tx)).ToHashSet();
                    #endregion

                    #region Setup styles
                    LabelStyleCollection stc = civilDoc.Styles.LabelStyles
                                                                   .ProjectionLabelStyles.ProfileViewProjectionLabelStyles;

                    Oid profileProjection_RIGHT_Style = Oid.Null;
                    Oid profileProjection_LEFT_Style = Oid.Null;

                    try
                    {
                        profileProjection_RIGHT_Style = stc["PROFILE PROJECTION RIGHT"];
                    }
                    catch (System.Exception)
                    {
                        editor.WriteMessage($"\nPROFILE PROJECTION RIGHT style missing!");
                        tx.Abort();
                        return;
                    }

                    try
                    {
                        profileProjection_LEFT_Style = stc["PROFILE PROJECTION LEFT"];
                    }
                    catch (System.Exception)
                    {
                        editor.WriteMessage($"\nPROFILE PROJECTION LEFT style missing!");
                        tx.Abort();
                        return;
                    }
                    #endregion

                    #region Choose left or right orientation

                    string AskToChooseDirection(Editor locEd)
                    {
                        const string kwd1 = "Right";
                        const string kwd2 = "Left";
                        PromptKeywordOptions pKeyOpts2 = new PromptKeywordOptions("");
                        pKeyOpts2.Message = "\nChoose next label direction: ";
                        pKeyOpts2.Keywords.Add(kwd1);
                        pKeyOpts2.Keywords.Add(kwd2);
                        pKeyOpts2.AllowNone = true;
                        pKeyOpts2.Keywords.Default = kwd1;
                        PromptResult locpKeyRes2 = locEd.GetKeywords(pKeyOpts2);
                        return locpKeyRes2.StringResult;
                    }
                    #endregion

                    bool dirRight = AskToChooseDirection(editor) == "Right";

                    #region Labels
                    HashSet<ProfileProjectionLabel> unSortedLabels = new HashSet<ProfileProjectionLabel>();

                    foreach (Entity ent in allEnts)
                        if (ent is ProfileProjectionLabel label) unSortedLabels.Add(label);

                    ProfileProjectionLabel[] labels;

                    if (dirRight)
                    {
                        labels = unSortedLabels.OrderByDescending(x => x.LabelLocation.X).ToArray();
                    }
                    else
                    {
                        labels = unSortedLabels.OrderBy(x => x.LabelLocation.X).ToArray();
                    }

                    for (int i = 0; i < labels.Length - 1; i++)
                    {
                        ProfileProjectionLabel firstLabel = labels[i];
                        ProfileProjectionLabel secondLabel = labels[i + 1];

                        Point3d firstLocationPoint = firstLabel.LabelLocation;
                        Point3d secondLocationPoint = secondLabel.LabelLocation;

                        double firstAnchorDimensionInMeters = firstLabel.DimensionAnchorValue * 250 + 0.0625;

                        double locationDelta = firstLocationPoint.Y - secondLocationPoint.Y;

                        double secondAnchorDimensionInMeters = (locationDelta + firstAnchorDimensionInMeters + 0.75) / 250;

                        Oid styleId = dirRight ? profileProjection_RIGHT_Style : profileProjection_LEFT_Style;

                        //Handle first label
                        if (i == 0)
                        {
                            firstLabel.CheckOrOpenForWrite();
                            firstLabel.StyleId = styleId;
                        }

                        secondLabel.CheckOrOpenForWrite();
                        secondLabel.DimensionAnchorValue = secondAnchorDimensionInMeters;
                        secondLabel.StyleId = styleId;
                        secondLabel.DowngradeOpen();

                        //editor.WriteMessage($"\nAnchorDimensionValue: {firstLabel.DimensionAnchorValue}.");
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("staggerlabelsall")]
        [CommandMethod("sgall")]
        public void staggerlabelsall()
        {
            staggerlabelsallmethod();
        }
        public void staggerlabelsallmethod(Database db = default)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = db ?? docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = CivilDocument.GetCivilDocument(localDb);

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    HashSet<ProfileView> pvs = localDb.HashSetOfType<ProfileView>(tx);
                    HashSet<ProfileProjectionLabel> labelsSet = localDb.HashSetOfType<ProfileProjectionLabel>(tx);

                    #region Setup styles
                    LabelStyleCollection stc = civilDoc.Styles.LabelStyles
                                                                   .ProjectionLabelStyles.ProfileViewProjectionLabelStyles;

                    Oid profileProjection_RIGHT_Style = Oid.Null;
                    Oid profileProjection_LEFT_Style = Oid.Null;

                    try
                    {
                        profileProjection_RIGHT_Style = stc["PROFILE PROJECTION RIGHT"];
                    }
                    catch (System.Exception)
                    {
                        editor.WriteMessage($"\nPROFILE PROJECTION RIGHT style missing!");
                        tx.Abort();
                        return;
                    }

                    try
                    {
                        profileProjection_LEFT_Style = stc["PROFILE PROJECTION LEFT"];
                    }
                    catch (System.Exception)
                    {
                        editor.WriteMessage($"\nPROFILE PROJECTION LEFT style missing!");
                        tx.Abort();
                        return;
                    }
                    #endregion

                    #region Labels
                    Extents3d extents = default;
                    var labelsInView = labelsSet.Where(x => extents.IsPointInsideXY(x.LabelLocation));

                    Oid styleId = profileProjection_RIGHT_Style;

                    foreach (var pv in pvs)
                    {
                        ProfileProjectionLabel[] labels;
                        extents = pv.GeometricExtents;
                        labels = labelsInView.OrderByDescending(x => x.LabelLocation.X).ToArray();

                        var pIds = pv.AlignmentId.Go<Alignment>(tx).GetProfileIds();
                        Profile surfaceP = default;
                        foreach (Oid oid in pIds) if (oid.Go<Profile>(tx).Name.EndsWith("_surface_P"))
                                surfaceP = oid.Go<Profile>(tx);
                        double calculatedLengthOfFirstLabel = 0;
                        if (surfaceP != default && labels.Length > 0)
                        {
                            //get the first label which is setting the start elevation
                            ProfileProjectionLabel label = labels[0];
                            double station = 0;
                            double labelElevation = 0;
                            //Get station and elevation of label
                            pv.FindStationAndElevationAtXY(
                                label.LabelLocation.X, label.LabelLocation.Y, ref station, ref labelElevation);
                            //Update elevation to be that of surface
                            double surfaceElevation = surfaceP.ElevationAt(station);
                            double labelDepthUnderSurface = (surfaceElevation - labelElevation) * 2.5;
                            double userSpecifiedLabelHeightOverSurfaceM = 5;
                            double deltaM = labelDepthUnderSurface + userSpecifiedLabelHeightOverSurfaceM;
                            calculatedLengthOfFirstLabel = deltaM / 250;
                            prdDbg($"{surfaceElevation}, {labelElevation}, {labelDepthUnderSurface}, {deltaM}, {calculatedLengthOfFirstLabel}");
                        }

                        if (labels.Length == 1)
                        {
                            ProfileProjectionLabel label = labels.First();
                            label.CheckOrOpenForWrite();
                            label.DimensionAnchorValue = calculatedLengthOfFirstLabel;
                            label.StyleId = styleId;
                        }

                        for (int i = 0; i < labels.Length - 1; i++)
                        {
                            ProfileProjectionLabel firstLabel = labels[i];
                            ProfileProjectionLabel secondLabel = labels[i + 1];

                            //Handle first label
                            if (i == 0)
                            {
                                firstLabel.CheckOrOpenForWrite();
                                firstLabel.DimensionAnchorValue = calculatedLengthOfFirstLabel;
                                firstLabel.StyleId = styleId;
                            }

                            Point3d firstLocationPoint = firstLabel.LabelLocation;
                            Point3d secondLocationPoint = secondLabel.LabelLocation;

                            double firstAnchorDimensionInMeters = firstLabel.DimensionAnchorValue * 250 + 0.0625;

                            double locationDelta = firstLocationPoint.Y - secondLocationPoint.Y;

                            double secondAnchorDimensionInMeters = (locationDelta + firstAnchorDimensionInMeters + 0.75) / 250;

                            secondLabel.CheckOrOpenForWrite();
                            secondLabel.DimensionAnchorValue = secondAnchorDimensionInMeters;
                            secondLabel.StyleId = styleId;
                            secondLabel.DowngradeOpen();

                            //editor.WriteMessage($"\nAnchorDimensionValue: {firstLabel.DimensionAnchorValue}.");
                        }

                    }
                    #endregion 
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("setlabelslength")]
        public void setlabelslength()
        {

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                HashSet<ProfileProjectionLabel> labels = localDb.HashSetOfType<ProfileProjectionLabel>(tx);
                editor.WriteMessage($"\nNumber of labels: {labels.Count}.");

                #region Get length
                PromptDoubleResult result = editor.GetDouble("\nEnter length: ");
                if (((PromptResult)result).Status != PromptStatus.OK) return;
                double length = result.Value;
                #endregion

                foreach (ProfileProjectionLabel label in labels)
                {
                    label.CheckOrOpenForWrite();

                    label.DimensionAnchorValue = length / 250 / 4;
                }

                tx.Commit();
            }
        }

        [CommandMethod("FIXMIDTPROFILESTYLE")]
        public void fixmidtprofilestyle()
        {
            try
            {
                DocumentCollection docCol = Application.DocumentManager;
                Database localDb = docCol.MdiActiveDocument.Database;
                Editor editor = docCol.MdiActiveDocument.Editor;
                Document doc = docCol.MdiActiveDocument;
                CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

                #region Setup styles and clone blocks

                using (Transaction tx = localDb.TransactionManager.StartTransaction())
                {
                    try
                    {
                        //Profile Style
                        var psc = civilDoc.Styles.ProfileStyles;
                        ProfileStyle ps = psc["PROFIL STYLE MGO MIDT"].Go<ProfileStyle>(tx);
                        ps.CheckOrOpenForWrite();

                        DisplayStyle ds;
                        ds = ps.GetDisplayStyleProfile(ProfileDisplayStyleProfileType.Line);
                        ds.LinetypeScale = 10;

                        ds = ps.GetDisplayStyleProfile(ProfileDisplayStyleProfileType.Curve);
                        ds.LinetypeScale = 10;

                        ds = ps.GetDisplayStyleProfile(ProfileDisplayStyleProfileType.SymmetricalParabola);
                        ds.LinetypeScale = 10;

                    }
                    catch (System.Exception)
                    {
                        tx.Abort();
                        throw;
                    }
                    tx.Commit();
                }

                #endregion
            }
            catch (System.Exception ex)
            {
                prdDbg(ex.ToString());
            }
        }

        [CommandMethod("DELETESURFACEPROFILES")]
        public void deletesurfaceprofiles()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                //////////////////////////////////////
                List<Profile> profiles = localDb.ListOfType<Profile>(tx)
                    .Where(x => x.Name.Contains("_surface_P")).ToList();
                //////////////////////////////////////

                #region Delete previous blocks
                //Delete previous blocks
                foreach (Profile p in profiles)
                {
                    p.CheckOrOpenForWrite();
                    p.Erase(true);
                }
                #endregion
                tx.Commit();
            }
        }
    }
}