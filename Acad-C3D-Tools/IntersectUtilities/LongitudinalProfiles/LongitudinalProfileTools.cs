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
using Color = Autodesk.AutoCAD.Colors.Color;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        [CommandMethod("CREATESURFACEPROFILES")]
        public void createsurfaceprofiles()
        {
            createsurfaceprofilesmethod();
        }
        private void createsurfaceprofilesmethod(
            DataReferencesOptions dro = null, List<Alignment> allAlignments = null)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database db = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                if (dro == null)
                    dro = new DataReferencesOptions();

                string projectName = dro.ProjectName;
                string etapeName = dro.EtapeName;

                editor.WriteMessage("\n" + GetPathToDataFiles(projectName, etapeName, "Surface"));

                #region Read surface from file
                // open the xref database
                Database xRefSurfaceDB = new Database(false, true);
                xRefSurfaceDB.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Surface"),
                    FileOpenMode.OpenForReadAndAllShare, false, null);
                Transaction xRefSurfaceTx = xRefSurfaceDB.TransactionManager.StartTransaction();

                CivSurface surface = null;
                try
                {

                    surface = xRefSurfaceDB
                        .HashSetOfType<TinSurface>(xRefSurfaceTx)
                        .FirstOrDefault() as CivSurface;
                }
                catch (System.Exception)
                {
                    throw;
                }

                if (surface == null)
                {
                    editor.WriteMessage("\nSurface could not be loaded from the xref!");
                    xRefSurfaceTx.Commit();
                    return;
                }
                #endregion

                try
                {
                    if (allAlignments == null)
                        allAlignments = db.ListOfType<Alignment>(tx)
                            .OrderBy(x => x.Name).ToList();

                    #region Create surface profiles

                    #region Get terrain layer id

                    LayerTable lt = db.LayerTableId.GetObject(OpenMode.ForRead) as LayerTable;
                    string terrainLayerName = "0_TERRAIN_PROFILE";
                    Oid terrainLayerId = Oid.Null;
                    if (!lt.Has(terrainLayerName))
                    {
                        LayerTableRecord ltr = new LayerTableRecord();
                        ltr.Name = terrainLayerName;
                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 34);
                        lt.CheckOrOpenForWrite();
                        terrainLayerId = lt.Add(ltr);
                        tx.AddNewlyCreatedDBObject(ltr, true);
                    }
                    else terrainLayerId = lt[terrainLayerName];

                    if (terrainLayerId == Oid.Null)
                    {
                        editor.WriteMessage("Terrain layer missing!");
                        throw new System.Exception("Terrain layer missing!");
                    }

                    #endregion

                    Oid profileStyleId = civilDoc.Styles.ProfileStyles["Terræn"];
                    Oid profileLabelSetStyleId = civilDoc.Styles.LabelSetStyles.ProfileLabelSetStyles["_No Labels"];

                    foreach (Alignment alignment in allAlignments)
                    {
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
                            try
                            {
                                surfaceProfileId = Profile.CreateFromSurface(
                                    profileName, alignment.ObjectId, surface.ObjectId,
                                    terrainLayerId, profileStyleId, profileLabelSetStyleId);
                            }
                            catch (System.Exception)
                            {
                                prdDbg(alignment.Name + "failed!");
                                continue;
                            }
                            editor.WriteMessage($"\nSurface profile created for {alignment.Name}.");
                        }

                        System.Windows.Forms.Application.DoEvents();
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    xRefSurfaceTx.Abort();
                    xRefSurfaceTx.Dispose();
                    xRefSurfaceDB.Dispose();
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                xRefSurfaceTx.Commit();
                xRefSurfaceTx.Dispose();
                xRefSurfaceDB.Dispose();
                tx.Commit();
            }
        }

        [CommandMethod("CREATEPROFILEVIEWS")]
        public void createprofileviews()
        {
            createprofileviewsmethod();
        }
        private void createprofileviewsmethod(Point3d selectedPoint = default)
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
                    //Prepare for saving all the time
                    HostApplicationServices hs = HostApplicationServices.Current;

                    string path = hs.FindFile(doc.Name, doc.Database, FindFileHint.Default);

                    #region Alignments and profile views
                    List<Alignment> allAlignments = localDb.ListOfType<Alignment>(tx)
                                    .OrderBy(x => x.Name)
                                    .ToList();
                    prdDbg($"Number of all alignments in drawing: {allAlignments.Count}");
                    HashSet<ProfileView> pvSetExisting = localDb.HashSetOfType<ProfileView>(tx);
                    HashSet<string> pvNames = pvSetExisting.Select(x => x.Name).ToHashSet();
                    //Filter out already created profile views
                    allAlignments = allAlignments.Where(x => !pvNames.Contains(x.Name + "_PV")).OrderBy(x => x.Name).ToList();
                    prdDbg($"Number of alignments without profile view: {allAlignments.Count}");
                    #endregion

                    #region Create profile views
                    Oid profileViewBandSetStyleId = civilDoc.Styles
                            .ProfileViewBandSetStyles["EG-FG Elevations and Stations"];
                    Oid profileViewStyleId = civilDoc.Styles
                        .ProfileViewStyles["PROFILE VIEW L TO R NO SCALE"];

                    int index = 1;

                    #region Select point
                    if (selectedPoint == default)
                    {
                        PromptPointOptions pPtOpts = new PromptPointOptions("");
                        // Prompt for the start point
                        pPtOpts.Message = "\nSelect location where to draw first profile view:";
                        PromptPointResult pPtRes = editor.GetPoint(pPtOpts);
                        selectedPoint = pPtRes.Value;
                        // Exit if the user presses ESC or cancels the command
                        if (pPtRes.Status != PromptStatus.OK) throw new System.Exception("Aborted by user!");
                    }
                    #endregion

                    editor.Regen();

                    if (allAlignments.Count == 0) throw new System.Exception("Selection of alignment(s) failed!");

                    foreach (Alignment alignment in allAlignments)
                    {
                        #region Create profile view
                        //Profile view Id init
                        Oid pvId = Oid.Null;

                        editor.WriteMessage($"\n_-*-_ | Processing alignment {alignment.Name}. | _-*-_");
                        System.Windows.Forms.Application.DoEvents();

                        prdDbg($"Creating ProfileView: {alignment.Name}_PV");
                        Point3d insertionPoint = new Point3d(
                            selectedPoint.X, selectedPoint.Y + (index - 1) * -120, 0);
                        pvId = ProfileView.Create(alignment.ObjectId, insertionPoint,
                            $"{alignment.Name}_PV_temp", profileViewBandSetStyleId, profileViewStyleId);
                        ProfileView pv = pvId.Go<ProfileView>(tx, OpenMode.ForWrite);
                        pv.Name = $"{alignment.Name}_PV";
                        index++;
                        #endregion

                    }
                    #endregion
                }
                catch (System.Exception e)
                {
                    tx.Abort();
                    prdDbg(e);
                    return;
                }

                tx.Commit();
            }
        }

        /// <summary>
        /// New method to create Ler intersection data using PropertySets
        /// Because ODTables are unstable and sometimes do not work
        /// as expected when cloning the objects from drawing.
        /// It is also possible to access data across databases
        /// while ODTables require cloning of objects to host drawing
        /// which is slow and not always works
        /// </summary>
        [CommandMethod("CREATELERDATAPSS")]
        public void createlerdatapss()
        {
            DataReferencesOptions dro = new DataReferencesOptions();
            createlerdatapssmethod(dro);
        }
        public void createlerdatapssmethod(
            DataReferencesOptions dro, List<Alignment> allAlignments = null)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                string projectName = dro.ProjectName;
                string etapeName = dro.EtapeName;
                Application.DocumentManager.MdiActiveDocument.Editor
                    .WriteMessage("\n" + GetPathToDataFiles(projectName, etapeName, "Ler"));
                Application.DocumentManager.MdiActiveDocument.Editor
                    .WriteMessage("\n" + GetPathToDataFiles(projectName, etapeName, "Surface"));

                #region Read surface from file
                // open the xref database
                Database xRefSurfaceDB = new Database(false, true);
                xRefSurfaceDB.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Surface"),
                    FileOpenMode.OpenForReadAndAllShare, false, null);
                Transaction xRefSurfaceTx = xRefSurfaceDB.TransactionManager.StartTransaction();

                CivSurface surface = null;
                try
                {
                    surface = xRefSurfaceDB
                        .HashSetOfType<TinSurface>(xRefSurfaceTx)
                        .FirstOrDefault() as CivSurface;
                }
                catch (System.Exception)
                {
                    xRefSurfaceTx.Abort();
                    xRefSurfaceTx.Dispose();
                    xRefSurfaceDB.Dispose();
                    throw;
                }

                if (surface == null)
                {
                    editor.WriteMessage("\nSurface could not be loaded from the xref!");
                    xRefSurfaceTx.Commit();
                    xRefSurfaceTx.Dispose();
                    xRefSurfaceDB.Dispose();
                    return;
                }
                #endregion

                #region Load linework from LER Xref
                // open the LER dwg database
                Database xRefLerDB = new Database(false, true);

                xRefLerDB.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Ler"),
                    FileOpenMode.OpenForReadAndAllShare, false, null);

                Transaction xRefLerTx = xRefLerDB.TransactionManager.StartTransaction();

                List<Polyline3d> remoteLerData = xRefLerDB.ListOfType<Polyline3d>(xRefLerTx);
                editor.WriteMessage($"\nNr. of 3D polies: {remoteLerData.Count}");
                #endregion

                try
                {
                    if (allAlignments == null)
                    {
                        allAlignments = localDb.ListOfType<Alignment>(tx)
                        .OrderBy(x => x.Name)
                        .ToList();
                    }

                    HashSet<ProfileView> pvSetExisting = localDb.HashSetOfType<ProfileView>(tx);

                    #region Check or create propertysetdata to store crossing data
                    PropertySetManager psmDriCrossingData = new PropertySetManager(localDb,
                        PSetDefs.DefinedSets.DriCrossingData);
                    PSetDefs.DriCrossingData dCDdef = new PSetDefs.DriCrossingData();
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

                    //!!!!!Problem: How to determine that points for this alignmen not already created?
                    //!!!!!So right now it works only on empty projects, so no deletion of points is needed

                    //PointGroupCollection pgs = civilDoc.PointGroups;

                    //for (int i = 0; i < pgs.Count; i++)
                    //{
                    //    PointGroup pg = tx.GetObject(pgs[i], OpenMode.ForRead) as PointGroup;
                    //    if (allAlignments.Any(x => x.Name == pg.Name))
                    //    {
                    //        pg.CheckOrOpenForWrite();
                    //        pg.Update();
                    //        uint[] numbers = pg.GetPointNumbers();

                    //        CogoPointCollection cpc = civilDoc.CogoPoints;

                    //        for (int j = 0; j < numbers.Length; j++)
                    //        {
                    //            uint number = numbers[j];

                    //            if (cpc.Contains(number))
                    //            {
                    //                cpc.Remove(number);
                    //            }
                    //        }

                    //        StandardPointGroupQuery spgqEmpty = new StandardPointGroupQuery();
                    //        spgqEmpty.IncludeNumbers = "";
                    //        pg.SetQuery(spgqEmpty);

                    //        pg.Update();
                    //    }
                    //}
                    #endregion

                    #region Name handling of point names
                    //Used to keep track of point names
                    HashSet<string> pNames = new HashSet<string>();
                    CogoPointCollection cogoPoints = civilDoc.CogoPoints;
                    cogoPoints.Select(x => pNames.Add(x.Go<CogoPoint>(tx).PointName));

                    int index = 1;
                    #endregion

                    #region CogoPoint style and label reference
                    Oid cogoPointStyle = civilDoc.Styles.PointStyles["LER KRYDS"];
                    #endregion

                    foreach (Alignment alignment in allAlignments)
                    {
                        #region Create ler data
                        #region Do not clone anymore, use property sets

                        Plane plane = new Plane();

                        int intersections = 0;

                        List<Entity> sourceEnts = new List<Entity>();

                        //Gather the intersected objectIds
                        foreach (Entity ent in remoteLerData)
                        {
                            string type = ReadStringParameterFromDataTable(ent.Layer, dtKrydsninger, "Type", 0);
                            if (type == "IGNORE") continue;

                            using (Point3dCollection p3dcol = new Point3dCollection())
                            {
                                alignment.IntersectWith(
                                    ent,
                                    Autodesk.AutoCAD.DatabaseServices.Intersect.OnBothOperands,
                                    plane, p3dcol, new IntPtr(0), new IntPtr(0));

                                if (p3dcol.Count > 0)
                                {
                                    intersections++;
                                    sourceEnts.Add(ent);
                                }
                            }
                        }

                        editor.WriteMessage($"\nTotal intersecting pipes detected: {intersections}");
                        if (intersections == 0) continue;
                        #endregion

                        #region Prepare variables
                        HashSet<CogoPoint> allNewlyCreatedPoints = new HashSet<CogoPoint>();
                        #endregion

                        #region Handle PointGroups
                        bool pointGroupAlreadyExists = civilDoc.PointGroups.Contains(alignment.Name);

                        PointGroup currentPointGroup = null;

                        if (pointGroupAlreadyExists)
                        {
                            currentPointGroup = tx.GetObject(
                                civilDoc.PointGroups[alignment.Name],
                                    OpenMode.ForWrite) as PointGroup;
                        }
                        else
                        {
                            Oid pgId = civilDoc.PointGroups.Add(alignment.Name);
                            currentPointGroup = pgId.GetObject(OpenMode.ForWrite) as PointGroup;
                        }
                        #endregion

                        #region Create Points, assign elevation, layer and PS data
                        foreach (Entity ent in sourceEnts)
                        {
                            #region Read data parameters from csvs
                            //Read 'Type' value
                            string type = ReadStringParameterFromDataTable(ent.Layer, dtKrydsninger, "Type", 0);
                            if (type.IsNoE())
                            {//Exit, if a layer is not defined in Krydsninger.csv
                                AbortGracefully(
                                    new[] { xRefLerTx, xRefSurfaceTx },
                                    new[] { xRefLerDB, xRefSurfaceDB },
                                    $"Fejl: For lag {ent.Layer} mangler der enten " +
                                    $"selve definitionen eller 'Type'!");

                                tx.Abort();
                                return;
                            }

                            //Read depth value for type
                            double depth = 0;
                            if (!type.IsNoE())
                            {
                                depth = ReadDoubleParameterFromDataTable(type, dtDybde, "Dybde", 0);
                            }

                            //Read layer value for the object
                            string localPointLayerName = ReadStringParameterFromDataTable(
                                                ent.Layer, dtKrydsninger, "Layer", 0);
                            #endregion

                            #region Populate description field
                            string descrFromKrydsninger = ReadStringParameterFromDataTable(
                                ent.Layer, dtKrydsninger, "Description", 0);

                            //Guard against empty descriptions
                            //All descriptions must contain some information
                            //If it does not, then abort the whole thing
                            //Do not allow to proceed
                            //Force the user to keep descriptions up to date
                            if (descrFromKrydsninger.IsNoE())
                            {
                                AbortGracefully(
                                    new[] { xRefLerTx, xRefSurfaceTx },
                                    new[] { xRefLerDB, xRefSurfaceDB },
                                    $"Fejl: For lag {ent.Layer} mangler der en 'Description'!" +
                                    $"Fejl: Kan ikke fortsætte før dette er rettet i Krydsninger.csv");

                                tx.Abort();
                                return;
                            }

                            string description = ProcessDescription(ent, descrFromKrydsninger, dtKrydsninger);
                            #endregion

                            string entHandle = "";
                            entHandle = ent.Handle.ToString();

                            #region Create points
                            using (Point3dCollection p3dcol = new Point3dCollection())
                            {
                                alignment.IntersectWith(ent, 0, plane, p3dcol, new IntPtr(0), new IntPtr(0));

                                int count = 1;
                                foreach (Point3d p3d in p3dcol)
                                {
                                    Oid pointId = cogoPoints.Add(p3d, true);
                                    CogoPoint cogoPoint = pointId.Go<CogoPoint>(tx, OpenMode.ForWrite);

                                    #region Assign elevation based on 3D conditions
                                    double zElevation = 0;
                                    if (type != "3D")
                                    {
                                        var intPoint = surface.GetIntersectionPoint(
                                            new Point3d(p3d.X, p3d.Y, -99.0), new Vector3d(0, 0, 1));
                                        zElevation = intPoint.Z;

                                        //Subtract the depth (if invalid it is zero, so no modification will occur)
                                        zElevation -= depth;

                                        cogoPoint.Elevation = zElevation;
                                    }
                                    else if (type == "3D")
                                    {
                                        Polyline3d pline3d = (Polyline3d)ent;
                                        Point3d p3dInt = pline3d.GetClosestPointTo(
                                            p3d, new Vector3d(0.0, 0.0, 1.0), false);

                                        //Assume only one intersection
                                        cogoPoint.Elevation = p3dInt.Z;

                                        if (cogoPoint.Elevation == 0)
                                        {
                                            editor.WriteMessage($"\nFor type 3D entity {ent.Handle.ToString()}" +
                                                $" layer {ent.Layer}," +
                                                $" elevation is 0!");
                                        }
                                    }
                                    #endregion

                                    //Set the layer
                                    #region Layer handling
                                    localDb.CheckOrCreateLayer(localPointLayerName);

                                    cogoPoint.Layer = localPointLayerName;
                                    #endregion

                                    #region Point names, avoids duplicate names
                                    string pointName = entHandle + "_" + count;

                                    while (pNames.Contains(pointName))
                                    {
                                        count++;
                                        pointName = entHandle + "_" + count;
                                    }
                                    pNames.Add(pointName);
                                    cogoPoint.PointName = pointName;
                                    cogoPoint.RawDescription = description;
                                    cogoPoint.StyleId = cogoPointStyle;
                                    #endregion

                                    #region Populate DriCrossingData property set
                                    //Fetch diameter definitions if any
                                    string diaDef = ReadStringParameterFromDataTable(ent.Layer,
                                        dtKrydsninger, "Diameter", 0);

                                    if (diaDef.IsNotNoE())
                                    {
                                        var parts = FindPropertySetParts(diaDef);
                                        if (parts.propertyName != default && parts.setName != default)
                                        {
                                            int diameter = PropertySetManager.ReadNonDefinedPropertySetInt(
                                                ent, parts.setName, parts.propertyName);

                                            psmDriCrossingData.WritePropertyObject(cogoPoint, dCDdef.Diameter, diameter);
                                        }
                                    }

                                    psmDriCrossingData.WritePropertyString(
                                        cogoPoint, dCDdef.Alignment, alignment.Name);
                                    psmDriCrossingData.WritePropertyString(
                                        cogoPoint, dCDdef.SourceEntityHandle, entHandle);
                                    #endregion

                                    //Reference newly created cogoPoint to gathering collection
                                    allNewlyCreatedPoints.Add(cogoPoint);
                                }
                            }
                            #endregion
                        }
                        #endregion

                        #region Assign newly created points to projection on a profile view
                        #region Build query for PointGroup
                        //Build query
                        StandardPointGroupQuery spgq = new StandardPointGroupQuery();
                        List<string> newPointNumbers = allNewlyCreatedPoints.Select(x => x.PointNumber.ToString()).ToList();
                        string pointNumbersToInclude = string.Join(",", newPointNumbers.ToArray());
                        spgq.IncludeNumbers = pointNumbersToInclude;
                        currentPointGroup.SetQuery(spgq);
                        currentPointGroup.Update();
                        #endregion

                        #region Manage PVs
                        ObjectIdCollection pIds = alignment.GetProfileIds();
                        Profile pSurface = null;
                        foreach (Oid oid in pIds)
                        {
                            Profile pt = oid.Go<Profile>(tx);
                            if (pt.Name == $"{alignment.Name}_surface_P") pSurface = pt;
                        }
                        if (pSurface == null)
                        {
                            AbortGracefully(
                                new[] { xRefLerTx, xRefSurfaceTx },
                                new[] { xRefLerDB, xRefSurfaceDB },
                                $"No profile named {alignment.Name}_surface_P found!");

                            tx.Abort();
                            return;
                        }
                        else editor.WriteMessage($"\nProfile {pSurface.Name} found!");

                        #region Find bottom profile
                        Profile bundProfile = null;
                        Profile midtProfile = null;
                        Profile topProfile = null;
                        Profile bottomProfile = null;
                        foreach (Oid oid in pIds)
                        {
                            Profile pt = oid.Go<Profile>(tx);
                            if (pt.Name.Contains("BUND")) bundProfile = pt;
                            if (pt.Name.Contains("MIDT")) midtProfile = pt;
                            if (pt.Name.Contains("TOP")) topProfile = pt;
                        }
                        if (bundProfile == null &&
                            midtProfile == null &&
                            topProfile == null)
                        {
                            bottomProfile = pSurface;
                        }
                        else if (bundProfile != null) bottomProfile = bundProfile;
                        else if (midtProfile != null) bottomProfile = midtProfile;
                        else if (topProfile != null) bottomProfile = topProfile;
                        #endregion

                        //Sorting is not verified!!!
                        //Must be sorted from start alignment to end
                        ObjectIdCollection pvIds = alignment.GetProfileViewIds();
                        List<ProfileView> pvs = new List<ProfileView>();
                        foreach (Oid pvId in pvIds) pvs.Add(pvId.Go<ProfileView>(tx, OpenMode.ForWrite));
                        //ProfileView[] pvs = localDb.ListOfType<ProfileView>(tx).ToArray();

                        //Create StationPoints and assign PV number to them
                        HashSet<StationPoint> staPoints = new HashSet<StationPoint>(allNewlyCreatedPoints.Count);
                        foreach (CogoPoint cp in allNewlyCreatedPoints)
                        {
                            StationPoint sp;
                            try
                            {
                                sp = new StationPoint(cp, alignment);
                            }
                            catch (System.Exception)
                            {
                                continue;
                            }

                            int counter = 0;
                            foreach (ProfileView pv in pvs)
                            {
                                //Sorting of ProfileViews is not verified!!!
                                counter++;
                                if (sp.Station >= pv.StationStart &&
                                    sp.Station <= pv.StationEnd)
                                {
                                    sp.ProfileViewNumber = counter;
                                    break;
                                }
                            }
                            staPoints.Add(sp);
                        }

                        for (int i = 0; i < pvs.Count; i++)
                        {
                            int idx = i + 1;
                            ProfileView pv = pvs[i];

                            #region Determine profile top and bottom elevations
                            double pvStStart = pv.StationStart;
                            double pvStEnd = pv.StationEnd;

                            int nrOfIntervals = (int)((pvStEnd - pvStStart) / 0.25);
                            double delta = (pvStEnd - pvStStart) / nrOfIntervals;
                            HashSet<double> topElevs = new HashSet<double>();
                            HashSet<double> minElevs = new HashSet<double>();

                            for (int j = 0; j < nrOfIntervals + 1; j++)
                            {
                                double topTestEl = 0;
                                try
                                {
                                    topTestEl = pSurface.ElevationAt(pvStStart + delta * j);
                                }
                                catch (System.Exception)
                                {
                                    editor.WriteMessage($"\nTop profile at {pvStStart + delta * i} threw an exception! " +
                                        $"PV: {pv.StationStart}-{pv.StationEnd}.");
                                    continue;
                                }
                                topElevs.Add(topTestEl);
                                double bottomTestEl = 0;
                                try
                                {
                                    bottomTestEl = bottomProfile.ElevationAt(pvStStart + delta * j);
                                }
                                catch (System.Exception)
                                {
                                    editor.WriteMessage($"\nBottom profile {bottomProfile.Name} at " +
                                        $"{pvStStart + delta * i} threw an exception! " +
                                        $"PV {pv.Name}: {pv.StationStart}-{pv.StationEnd}.");
                                    continue;
                                }
                                minElevs.Add(bottomTestEl);
                            }

                            double maxEl = topElevs.Max();
                            double profileMinEl = minElevs.Min();
                            double pointsMinEl = staPoints.Where(x => x.ProfileViewNumber == idx)
                                                    .Select(x => x.CogoPoint.Elevation)
                                                    .Min();
                            double minEl = profileMinEl > pointsMinEl ? pointsMinEl : profileMinEl;
                            prdDbg($"\nElevations of PV {pv.Name}> Max: {Math.Round(maxEl, 2)} | Min: {Math.Round(minEl, 2)}");
                            #endregion

                            //Set the elevations
                            pv.CheckOrOpenForWrite();
                            pv.ElevationRangeMode = ElevationRangeType.UserSpecified;
                            pv.ElevationMax = Math.Ceiling(maxEl);
                            pv.ElevationMin = Math.Floor(minEl) - 1;

                            //Project the points
                            editor.SetImpliedSelection(staPoints
                                .Where(x => x.ProfileViewNumber == idx)
                                .Select(x => x.CogoPoint.ObjectId)
                                .ToArray());
                            prdDbg("");
                            editor.Command("_AeccProjectObjectsToProf", pv.ObjectId);
                        }
                        #endregion

                        #endregion

                        #endregion
                    }

                    xRefLerTx.Commit();
                    xRefSurfaceTx.Commit();
                    xRefLerDB.Dispose();
                    xRefSurfaceDB.Dispose();
                }

                catch (System.Exception ex)
                {
                    xRefLerTx.Abort();
                    xRefLerDB.Dispose();

                    xRefSurfaceTx.Abort();
                    xRefSurfaceDB.Dispose();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("DELETEALLCOGOPOINTS")]
        public void deleteallcogopoints()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var allpoints = localDb.HashSetOfType<CogoPoint>(tx);
                    foreach (var item in allpoints)
                    {
                        item.CheckOrOpenForWrite();
                        item.Erase(true);
                    }

                    prdDbg(civilDoc.PointGroups.Count.ToString());

                    civilDoc.PointGroups.UpdateAllPointGroups();

                    foreach (var item in civilDoc.PointGroups)
                    {
                        //item.Go<PointGroup>(tx, OpenMode.ForWrite).Erase(true);
                    }

                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex.ToString());
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("POPULATEPROFILES")]
        public void populateprofiles()
        {
            populateprofilesmethod();
        }
        public void populateprofilesmethod(HashSet<Oid> pvs = null)
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
                    #region Read Csv Data for Layers
                    //Establish the pathnames to files
                    //Files should be placed in a specific folder on desktop
                    string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";

                    System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
                    #endregion

                    BlockTableRecord space = (BlockTableRecord)tx.GetObject(localDb.CurrentSpaceId, OpenMode.ForWrite);
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForWrite) as BlockTable;

                    #region Pss manager
                    PropertySetManager psm = new PropertySetManager(localDb,
                        PSetDefs.DefinedSets.DriCrossingData);

                    PSetDefs.DriCrossingData dcd = new PSetDefs.DriCrossingData();
                    #endregion

                    if (pvs == null)
                        pvs = localDb.HashSetIdsOfType<ProfileView>();

                    foreach (ProfileView pv in pvs.Select(x => x.Go<ProfileView>(tx)))
                    {
                        #region Create a block for profile view detailing
                        //First, get the profile view

                        if (pv == null)
                        {
                            editor.WriteMessage($"\nNo profile view found in document!");
                            tx.Abort();
                            return;
                        }

                        #region Find zero point of profile view
                        //Find the zero-point of the profile view
                        pv.CheckOrOpenForWrite();
                        double x = 0.0;
                        double y = 0.0;
                        if (pv.ElevationRangeMode == ElevationRangeType.Automatic)
                        {
                            pv.ElevationRangeMode = ElevationRangeType.UserSpecified;
                            pv.FindXYAtStationAndElevation(pv.StationStart, pv.ElevationMin, ref x, ref y);
                        }
                        else
                            pv.FindXYAtStationAndElevation(pv.StationStart, pv.ElevationMin, ref x, ref y);
                        #endregion

                        #region Erase existing detailing block if it exists
                        if (bt.Has(pv.Name))
                        {
                            if (!EraseBlock(doc, pv.Name))
                            {
                                editor.WriteMessage($"\nFailed to erase block: {pv.Name}.");
                                return;
                            }
                        }
                        #endregion

                        BlockTableRecord detailingBlock = new BlockTableRecord();
                        detailingBlock.Name = pv.Name;
                        detailingBlock.Origin = new Point3d(x, y, 0);

                        bt.Add(detailingBlock);
                        tx.AddNewlyCreatedDBObject(detailingBlock, true);
                        #endregion

                        #region Process labels
                        LabelStyleCollection stc = civilDoc.Styles.LabelStyles
                                                       .ProjectionLabelStyles.ProfileViewProjectionLabelStyles;

                        Oid prStId = stc["PROFILE PROJEKTION MGO"];

                        HashSet<Label> labels = localDb.HashSetOfType<Label>(tx);
                        Extents3d extentsPv = pv.GeometricExtents;

                        var pvLabels = labels.Where(l => extentsPv.IsPointInsideXY(l.LabelLocation));
                        prdDbg($"Number of labels inside extents: {pvLabels.Count()}");

                        foreach (Label label in pvLabels)
                        {
                            label.CheckOrOpenForWrite();
                            label.StyleId = prStId;

                            Oid fId = label.FeatureId;
                            Entity fEnt = fId.Go<Entity>(tx);

                            var diaOriginal = psm.ReadPropertyInt(fEnt, dcd.Diameter);

                            double dia = Convert.ToDouble(diaOriginal) / 1000.0;

                            if (dia == 0 || diaOriginal == 999) dia = 0.11;

                            string blockName = ReadStringParameterFromDataTable(
                                fEnt.Layer, dtKrydsninger, "Block", 1);

                            if (blockName.IsNotNoE())
                            {
                                if (blockName == "Cirkel, Bund" || blockName == "Cirkel, Top")
                                {
                                    Circle circle = null;
                                    if (blockName.Contains("Bund"))
                                    {
                                        circle = new Circle(new Point3d(
                                        label.LabelLocation.X, label.LabelLocation.Y + (dia / 2), 0),
                                        Vector3d.ZAxis, dia / 2);
                                    }
                                    else if (blockName.Contains("Top"))
                                    {
                                        circle = new Circle(new Point3d(
                                        label.LabelLocation.X, label.LabelLocation.Y - (dia / 2), 0),
                                        Vector3d.ZAxis, dia / 2);
                                    }

                                    space.AppendEntity(circle);
                                    tx.AddNewlyCreatedDBObject(circle, false);
                                    circle.Layer = fEnt.Layer;

                                    Entity clone = circle.Clone() as Entity;
                                    detailingBlock.AppendEntity(clone);
                                    tx.AddNewlyCreatedDBObject(clone, true);

                                    circle.Erase(true);
                                }
                                else if (bt.Has(blockName))
                                {
                                    using (var br = new Autodesk.AutoCAD.DatabaseServices.BlockReference(
                                        label.LabelLocation, bt[blockName]))
                                    {
                                        space.AppendEntity(br);
                                        tx.AddNewlyCreatedDBObject(br, false);
                                        br.Layer = fEnt.Layer;

                                        Entity clone = br.Clone() as Entity;
                                        detailingBlock.AppendEntity(clone);
                                        tx.AddNewlyCreatedDBObject(clone, true);

                                        br.Erase(true);
                                    }
                                }
                            }

                            label.CheckOrOpenForWrite();
                            label.Layer = fEnt.Layer;
                        }
                        #endregion

                        using (var br = new Autodesk.AutoCAD.DatabaseServices.BlockReference(
                                        new Point3d(x, y, 0), bt[pv.Name]))
                        {
                            space.AppendEntity(br);
                            tx.AddNewlyCreatedDBObject(br, true);
                        }
                    }
                }

                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("COLORIZEALLLERLAYERS")]
        public void colorizealllerlayers()
        {
            colorizealllerlayersmethod();
        }

        [CommandMethod("CREATEPROFILES")]
        public void createprofiles()
        {
            createprofilesmethod();
        }
        public void createprofilesmethod(
            DataReferencesOptions dro = null, HashSet<Alignment> als = null)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                #region Open fremtidig db and get entities
                if (dro == null)
                    dro = new DataReferencesOptions();
                string projectName = dro.ProjectName;
                string etapeName = dro.EtapeName;

                // open the xref database
                Database fremDb = new Database(false, true);
                fremDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Fremtid"),
                    FileOpenMode.OpenForReadAndAllShare, false, null);
                Transaction fremTx = fremDb.TransactionManager.StartTransaction();

                HashSet<Curve> allCurves = fremDb.GetFjvPipes(fremTx).Cast<Curve>().ToHashSet();
                HashSet<BlockReference> allBrs = fremDb.HashSetOfType<BlockReference>(fremTx);
                #endregion

                //////////////////////////////////////
                string draftProfileLayerName = "0-FJV-PROFILE-DRAFT";
                //////////////////////////////////////

                try
                {
                    #region Create layer for draft profile
                    using (Transaction txLag = localDb.TransactionManager.StartTransaction())
                    {

                        LayerTable lt = txLag.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;

                        if (!lt.Has(draftProfileLayerName))
                        {
                            LayerTableRecord ltr = new LayerTableRecord();
                            ltr.Name = draftProfileLayerName;
                            ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 40);
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

                    #region Common variables
                    BlockTableRecord modelSpace = localDb.GetModelspaceForWrite();
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    Plane plane = new Plane(); //For intersecting
                    if (als == null) 
                        als = localDb.HashSetOfType<Alignment>(tx);
                    #endregion

                    #region Delete previous lines
                    //Delete previous blocks
                    var existingPlines = localDb.HashSetOfType<Polyline>(tx, false).Where(x => x.Layer == draftProfileLayerName).ToHashSet();
                    foreach (Entity ent in existingPlines)
                    {
                        ent.CheckOrOpenForWrite();
                        ent.Erase(true);
                    }
                    #endregion

                    #region Initialize PS for Alignment
                    PropertySetManager psmPipeLineData = new PropertySetManager(
                        fremDb,
                        PSetDefs.DefinedSets.DriPipelineData);
                    PSetDefs.DriPipelineData driPipelineData =
                        new PSetDefs.DriPipelineData();
                    #endregion

                    foreach (Alignment al in als)
                    {
                        prdDbg($"\nProcessing: {al.Name}...");
                        #region If exist get surface profile and profile view
                        ObjectIdCollection profileIds = al.GetProfileIds();
                        ObjectIdCollection profileViewIds = al.GetProfileViewIds();

                        ProfileView pv = null;
                        foreach (Oid oid in profileViewIds)
                        {
                            ProfileView pTemp = oid.Go<ProfileView>(tx);
                            if (pTemp.Name == $"{al.Name}_PV") pv = pTemp;
                        }
                        if (pv == null)
                        {
                            prdDbg($"No profile view found for alignment: {al.Name}, skip to next.");
                            continue;
                        }

                        Profile surfaceProfile = null;
                        foreach (Oid oid in profileIds)
                        {
                            Profile pTemp = oid.Go<Profile>(tx);
                            if (pTemp.Name == $"{al.Name}_surface_P") surfaceProfile = pTemp;
                        }
                        if (surfaceProfile == null)
                        {
                            prdDbg($"No surface profile found for alignment: {al.Name}, skip to next.");
                            continue;
                        }
                        prdDbg(pv.Name);
                        prdDbg(surfaceProfile.Name);
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
                        prdDbg($"Curves: {curves.Count}, Components: {brs.Count}");
                        #endregion

                        #region Variables and settings
                        Point3d pvOrigin = pv.Location;
                        double originX = pvOrigin.X;
                        double originY = pvOrigin.Y;

                        double pvStStart = pv.StationStart;
                        double pvStEnd = pv.StationEnd;
                        double pvElBottom = pv.ElevationMin;
                        double pvElTop = pv.ElevationMax;
                        double pvLength = pvStEnd - pvStStart;

                        //Settings
                        double weedAngle = 5; //In degrees
                        double weedAngleRad = weedAngle.ToRadians();
                        double DouglasPeuckerTolerance = .05;

                        double stepLength = 0.1;
                        int nrOfSteps = (int)(pvLength / stepLength);
                        #endregion

                        #region Build size array
                        PipelineSizeArray sizeArray = new PipelineSizeArray(al, curves, brs);
                        prdDbg(sizeArray.ToString());
                        #endregion

                        #region Local method to sample profiles
                        //Local method to sample profiles
                        double SampleProfile(Profile profile, double station)
                        {
                            double sampledElevation = 0;
                            try { sampledElevation = profile.ElevationAt(station); }
                            catch (System.Exception)
                            {
                                prdDbg($"Station {station} threw an exception when placing size change blocks! Skipping...");
                                return 0;
                            }
                            return sampledElevation;
                        }
                        #endregion

                        #region Sample profile with cover
                        double startStation = 0;
                        double endStation = 0;
                        double curStation = 0;
                        for (int i = 0; i < sizeArray.Length; i++)
                        {
                            List<Point2d> allSteps = new List<Point2d>();
                            //Station management
                            endStation = sizeArray[i].EndStation;
                            double segmentLength = endStation - startStation;
                            nrOfSteps = (int)(segmentLength / stepLength);
                            //Cover depth management
                            int curDn = sizeArray[i].DN;
                            double cover = curDn <= 65 ? 0.6 : 1.0; //CWO info
                            double halfKappeOd = sizeArray[i].Kod / 2.0 / 1000.0;
                            prdDbg($"S: {startStation.ToString("0000.0")}, " +
                                   $"E: {endStation.ToString("0000.00")}, " +
                                   $"L: {segmentLength.ToString("0000.00")}, " +
                                   $"Steps: {nrOfSteps.ToString("D5")}");
                            //Sample elevation at each step and create points at current offset from surface
                            for (int j = 0; j < nrOfSteps + 1; j++) //+1 because first step is an "extra" step
                            {
                                curStation = startStation + stepLength * j;
                                double sampledSurfaceElevation = SampleProfile(surfaceProfile, curStation);
                                allSteps.Add(new Point2d(originX + curStation, originY + (sampledSurfaceElevation - pvElBottom - cover - halfKappeOd)));
                            }
                            #region Apply Douglas Peucker reduction
                            List<Point2d> reducedSteps = DouglasPeuckerReduction.DouglasPeuckerReductionMethod(allSteps, DouglasPeuckerTolerance);
                            #endregion

                            #region Draw middle profile
                            Polyline draftProfile = new Polyline();
                            draftProfile.SetDatabaseDefaults();
                            draftProfile.Layer = draftProfileLayerName;
                            for (int j = 0; j < reducedSteps.Count; j++)
                            {
                                var curStep = reducedSteps[j];
                                draftProfile.AddVertexAt(j, curStep, 0, 0, 0);
                            }
                            draftProfile.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                            modelSpace.AppendEntity(draftProfile);
                            tx.AddNewlyCreatedDBObject(draftProfile, true);
                            #endregion

                            #region Draw offset profiles
                            using (DBObjectCollection col = draftProfile.GetOffsetCurves(halfKappeOd))
                            {
                                foreach (var ent in col)
                                {
                                    if (ent is Polyline poly)
                                    {
                                        poly.Color = Color.FromColorIndex(ColorMethod.ByLayer, 256);
                                        modelSpace.AppendEntity(poly);
                                        tx.AddNewlyCreatedDBObject(poly, true);
                                    }
                                }
                            }
                            using (DBObjectCollection col = draftProfile.GetOffsetCurves(-halfKappeOd))
                            {
                                foreach (var ent in col)
                                {
                                    if (ent is Polyline poly)
                                    {
                                        poly.Color = Color.FromColorIndex(ColorMethod.ByLayer, 256);
                                        modelSpace.AppendEntity(poly);
                                        tx.AddNewlyCreatedDBObject(poly, true);
                                    }
                                }
                            }
                            #endregion

                            startStation = sizeArray[i].EndStation;
                        }
                        #endregion

                        #region Test Douglas Peucker reduction
                        ////Test Douglas Peucker reduction
                        //List<double> coverList = new List<double>();
                        //int factor = 10; //Using factor to get more sampling points
                        //for (int i = 0; i < (nrOfSteps + 1) * factor; i++) //+1 because first step is an "extra" step
                        //{
                        //    double sampledSurfaceElevation = 0;

                        //    double curStation = pvStStart + stepLength / factor * i;
                        //    try
                        //    {
                        //        sampledSurfaceElevation = surfaceProfile.ElevationAt(curStation);
                        //    }
                        //    catch (System.Exception)
                        //    {
                        //        //prdDbg($"\nStation {curStation} threw an exception! Skipping...");
                        //        continue;
                        //    }

                        //    //To find point perpendicularly beneath the surface point
                        //    //Use graphical method of intersection with a helper line
                        //    //Cannot find or think of a mathematical solution
                        //    //Create new line to intersect with the draft profile
                        //    Line intersectLine = new Line(
                        //        new Point3d(originX + curStation, originY + (sampledSurfaceElevation - pvElBottom), 0),
                        //        new Point3d(originX + curStation, originY + (sampledSurfaceElevation - pvElBottom) - 10, 0));

                        //    //Intersect and get the intersection point
                        //    Point3dCollection intersectionPoints = new Point3dCollection();

                        //    intersectLine.IntersectWith(draftProfile, 0, plane, intersectionPoints, new IntPtr(0), new IntPtr(0));
                        //    if (intersectionPoints.Count < 1) continue;

                        //    Point3d intersection = intersectionPoints[0];
                        //    coverList.Add(intersection.DistanceTo(
                        //        new Point3d(originX + curStation, originY + (sampledSurfaceElevation - pvElBottom), 0)));
                        //}

                        //prdDbg($"Max. cover: {(int)(coverList.Max() * 1000)} mm");
                        //prdDbg($"Min. cover: {(int)(coverList.Min() * 1000)} mm");
                        //prdDbg($"Average cover: {(int)((coverList.Sum() / coverList.Count) * 1000)} mm");
                        //prdDbg($"Percent values below cover req.: " +
                        //    $"{((coverList.Count(x => x < cover) / Convert.ToDouble(coverList.Count)) * 100.0).ToString("0.##")} %");
                        //#endregion

                        //#region Test Douglas Peucker reduction again
                        //////Test Douglas Peucker reduction
                        ////coverList = new List<double>();

                        ////for (int i = 0; i < (nrOfSteps + 1) * factor; i++) //+1 because first step is an "extra" step
                        ////{
                        ////    double sampledSurfaceElevation = 0;

                        ////    double curStation = pvStStart + stepLength / factor * i;
                        ////    try
                        ////    {
                        ////        sampledSurfaceElevation = surfaceProfile.ElevationAt(curStation);
                        ////    }
                        ////    catch (System.Exception)
                        ////    {
                        ////        //prdDbg($"\nStation {curStation} threw an exception! Skipping...");
                        ////        continue;
                        ////    }

                        ////    //To find point perpendicularly beneath the surface point
                        ////    //Use graphical method of intersection with a helper line
                        ////    //Cannot find or think of a mathematical solution
                        ////    //Create new line to intersect with the draft profile
                        ////    Line intersectLine = new Line(
                        ////        new Point3d(originX + curStation, originY + (sampledSurfaceElevation - pvElBottom), 0),
                        ////        new Point3d(originX + curStation, originY + (sampledSurfaceElevation - pvElBottom) - 10, 0));

                        ////    //Intersect and get the intersection point
                        ////    Point3dCollection intersectionPoints = new Point3dCollection();

                        ////    intersectLine.IntersectWith(draftProfile, 0, plane, intersectionPoints, new IntPtr(0), new IntPtr(0));
                        ////    if (intersectionPoints.Count < 1) continue;

                        ////    Point3d intersection = intersectionPoints[0];
                        ////    coverList.Add(intersection.DistanceTo(
                        ////        new Point3d(originX + curStation, originY + (sampledSurfaceElevation - pvElBottom), 0)));
                        ////}

                        ////prdDbg("After fitting polyline:");
                        ////prdDbg($"Max. cover: {(int)(coverList.Max() * 1000)} mm");
                        ////prdDbg($"Min. cover: {(int)(coverList.Min() * 1000)} mm");
                        ////prdDbg($"Average cover: {(int)((coverList.Sum() / coverList.Count) * 1000)} mm");
                        ////prdDbg($"Percent values below cover req.: " +
                        ////    $"{((coverList.Count(x => x < cover) / Convert.ToDouble(coverList.Count)) * 100.0).ToString("0.##")} %");
                        //#endregion

                        #endregion
                    }
                }
                catch (System.Exception ex)
                {
                    fremTx.Abort();
                    fremTx.Dispose();
                    fremDb.Dispose();
                    tx.Abort();
                    prdDbg(ex.ExceptionInfo());
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
        /// Creates detailing based on SURFACE profile.
        /// </summary>
        [CommandMethod("CREATEDETAILINGPRELIMINARY")]
        public void createdetailingpreliminary()
        {
            createdetailingpreliminarymethod();
        }
        public void createdetailingpreliminarymethod(
            DataReferencesOptions dataReferencesOptions = default,
            Database database = default,
            HashSet<Alignment> alignments = default)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database dB = database ?? docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            DataReferencesOptions dro = dataReferencesOptions ?? new DataReferencesOptions();
            string projectName = dro.ProjectName;
            string etapeName = dro.EtapeName;

            System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(
                @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");

            //PropertySetManager.UpdatePropertySetDefinition(dB, PSetDefs.DefinedSets.DriSourceReference);

            using (Transaction tx = dB.TransactionManager.StartTransaction())
            {
                #region Open fremtidig db
                // open the xref database
                Database fremDb = new Database(false, true);
                fremDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Fremtid"),
                    FileOpenMode.OpenForReadAndAllShare, false, null);
                Transaction fremTx = fremDb.TransactionManager.StartTransaction();
                HashSet<Curve> allCurves = fremDb.GetFjvPipes(fremTx).Cast<Curve>().ToHashSet();
                HashSet<BlockReference> allBrs = fremDb.HashSetOfType<BlockReference>(fremTx);
                #endregion

                #region Initialize PS for Alignment adn detailing
                PropertySetManager psmPipeLineData = new PropertySetManager(
                    fremDb,
                    PSetDefs.DefinedSets.DriPipelineData);
                PSetDefs.DriPipelineData driPipelineData =
                    new PSetDefs.DriPipelineData();

                PropertySetManager psmSourceReference = new PropertySetManager(
                    dB, PSetDefs.DefinedSets.DriSourceReference);
                PSetDefs.DriSourceReference driSourceReference =
                    new PSetDefs.DriSourceReference();
                #endregion

                //////////////////////////////////////
                string komponentBlockName = "DRISizeChangeAnno";
                string bueBlockName = "DRIPipeArcAnno";
                //////////////////////////////////////

                try
                {
                    #region Common variables
                    //BlockTableRecord modelSpace = localDb.GetModelspaceForWrite();
                    BlockTable bt = tx.GetObject(dB.BlockTableId, OpenMode.ForRead) as BlockTable;
                    //Plane plane = new Plane(); //For intersecting
                    HashSet<Alignment> als = alignments ?? dB.HashSetOfType<Alignment>(tx);
                    #endregion

                    #region Import blocks if missing
                    if (!bt.Has(komponentBlockName) ||
                        !bt.Has(bueBlockName))
                    {
                        prdDbg("Some of the blocks for detailing are missing! Importing...");
                        Database blockDb = new Database(false, true);
                        blockDb.ReadDwgFile("X:\\AutoCAD DRI - 01 Civil 3D\\DynBlokke\\Symboler.dwg",
                            FileOpenMode.OpenForReadAndAllShare, false, null);
                        Transaction blockTx = blockDb.TransactionManager.StartTransaction();

                        Oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(blockDb);
                        //Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);
                        Oid destDbMsId = dB.BlockTableId;

                        BlockTable sourceBt = blockTx.GetObject(blockDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                        ObjectIdCollection idsToClone = new ObjectIdCollection();
                        if (!bt.Has(komponentBlockName)) idsToClone.Add(sourceBt[komponentBlockName]);
                        if (!bt.Has(bueBlockName)) idsToClone.Add(sourceBt[bueBlockName]);

                        IdMapping mapping = new IdMapping();
                        blockDb.WblockCloneObjects(idsToClone, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);
                        blockTx.Commit();
                        blockTx.Dispose();
                        blockDb.Dispose();
                    }
                    #endregion

                    #region Delete previous blocks
                    //Delete previous blocks
                    deletedetailing();

                    //var existingBlocks = localDb.GetBlockReferenceByName(komponentBlockName);
                    //existingBlocks.UnionWith(localDb.GetBlockReferenceByName(bueBlockName));

                    //foreach (BlockReference br in existingBlocks)
                    //{
                    //    br.CheckOrOpenForWrite();
                    //    br.Erase(true);
                    //}
                    #endregion

                    foreach (Alignment al in als.OrderBy(x => x.Name))
                    {
                        prdDbg($"\nProcessing: {al.Name}...");
                        #region If exist get surface profile and profile view
                        ObjectIdCollection profileIds = al.GetProfileIds();
                        ObjectIdCollection profileViewIds = al.GetProfileViewIds();
                        ProfileViewCollection pvs = new ProfileViewCollection(profileViewIds);
                        //Polyline alPl = al.GetPolyline().Go<Polyline>(tx);

                        #region Fetch surface profile
                        Profile surfaceProfile = null;
                        foreach (Oid oid in profileIds)
                        {
                            Profile pTemp = oid.Go<Profile>(tx);
                            if (pTemp.Name == $"{al.Name}_surface_P") surfaceProfile = pTemp;
                        }
                        if (surfaceProfile == null)
                        {
                            prdDbg($"No surface profile found for alignment: {al.Name}, skipping current alignment.");
                            continue;
                        }
                        #endregion
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

                        HashSet<BlockReference> afgreningsStudse = allBrs
                            .Where(x =>
                                psmPipeLineData.FilterPropetyString(
                                    x, driPipelineData.BranchesOffToAlignment, al.Name) &&
                                (x.RealName() == "AFGRSTUDS" || x.RealName() == "SH LIGE"))
                            .ToHashSet();

                        //Tilføj afgreningsstudse til blokke
                        brs.UnionWith(afgreningsStudse);

                        prdDbg($"Curves: {curves.Count}, Components: {brs.Count}");
                        #endregion

                        prdDbg("Blocks:");
                        PipelineSizeArray sizeArray = new PipelineSizeArray(al, curves, brs);
                        prdDbg(sizeArray.ToString());

                        foreach (ProfileView pv in pvs)
                        {
                            prdDbg($"Processing PV {pv.Name}.");

                            #region Variables and settings
                            Point3d pvOrigin = pv.Location;
                            double originX = pvOrigin.X;
                            double originY = pvOrigin.Y;

                            double pvStStart = pv.StationStart;
                            double pvStEnd = pv.StationEnd;
                            double pvElBottom = pv.ElevationMin;
                            double pvElTop = pv.ElevationMax;
                            double pvLength = pvStEnd - pvStStart;
                            #endregion

                            #region Determine what sizes appear in current PV
                            var pvSizeArray = sizeArray.GetPartialSizeArrayForPV(pv);
                            prdDbg(pvSizeArray.ToString());
                            #endregion

                            #region Prepare exaggeration handling
                            ProfileViewStyle profileViewStyle = tx
                                .GetObject(((Autodesk.Aec.DatabaseServices.Entity)pv)
                                .StyleId, OpenMode.ForRead) as ProfileViewStyle;
                            #endregion

                            double curStationBL = 0;
                            double sampledMidtElevation = 0;
                            double curX = 0, curY = 0;

                            #region Place size change blocks
                            for (int i = 0; i < pvSizeArray.Length; i++)
                            {   //Although look ahead is used, normal iteration is required
                                //Or cases where sizearray is only 1 size will not run at all
                                //In more general case the last iteration must be aborted
                                if (pvSizeArray.Length != 1 && i != pvSizeArray.Length - 1)
                                {
                                    //General case
                                    curStationBL = pvSizeArray[i].EndStation;
                                    sampledMidtElevation = SampleProfile(surfaceProfile, curStationBL);
                                    curX = originX + pvSizeArray[i].EndStation - pvStStart;
                                    curY = originY + (sampledMidtElevation - pvElBottom) *
                                        profileViewStyle.GraphStyle.VerticalExaggeration;
                                    double deltaY = (sampledMidtElevation - pvElBottom) *
                                        profileViewStyle.GraphStyle.VerticalExaggeration;
                                    //prdDbg($"{originY} + ({sampledMidtElevation} - {pvElBottom}) * " +
                                    //    $"{profileViewStyle.GraphStyle.VerticalExaggeration} = {deltaY}");
                                    BlockReference brInt =
                                        dB.CreateBlockWithAttributes(komponentBlockName, new Point3d(curX, curY, 0));
                                    brInt.SetAttributeStringValue("LEFTSIZE", $"DN {pvSizeArray[i].DN}");
                                    brInt.SetAttributeStringValue("RIGHTSIZE", $"DN {pvSizeArray[i + 1].DN}");

                                    psmSourceReference.WritePropertyObject(
                                        brInt, driSourceReference.AlignmentStation, curStationBL);
                                }
                                //Special cases
                                if (i == 0)
                                {//First iteration
                                    curStationBL = pvStStart;
                                    sampledMidtElevation = SampleProfile(surfaceProfile, curStationBL);
                                    curX = originX;
                                    curY = originY + (sampledMidtElevation - pvElBottom) *
                                        profileViewStyle.GraphStyle.VerticalExaggeration;
                                    BlockReference brAt0 =
                                        dB.CreateBlockWithAttributes(komponentBlockName, new Point3d(curX, curY, 0));
                                    brAt0.SetAttributeStringValue("LEFTSIZE", "");
                                    brAt0.SetAttributeStringValue("RIGHTSIZE", $"DN {pvSizeArray[0].DN}");

                                    psmSourceReference.WritePropertyObject(
                                        brAt0, driSourceReference.AlignmentStation, curStationBL);

                                    if (pvSizeArray.Length == 1)
                                    {//If only one size in the array also place block at end
                                        curStationBL = pvStEnd;
                                        sampledMidtElevation = SampleProfile(surfaceProfile, curStationBL - .1);
                                        curX = originX + curStationBL - pvStStart;
                                        curY = originY + (sampledMidtElevation - pvElBottom) *
                                            profileViewStyle.GraphStyle.VerticalExaggeration;
                                        BlockReference brAtEnd =
                                            dB.CreateBlockWithAttributes(komponentBlockName, new Point3d(curX, curY, 0));
                                        brAtEnd.SetAttributeStringValue("LEFTSIZE", $"DN {pvSizeArray[0].DN}");
                                        brAtEnd.SetAttributeStringValue("RIGHTSIZE", "");

                                        psmSourceReference.WritePropertyObject(
                                        brAtEnd, driSourceReference.AlignmentStation, curStationBL);
                                    }
                                }
                                if (i == pvSizeArray.Length - 2)
                                {//End of the iteration
                                    curStationBL = pvStEnd;
                                    sampledMidtElevation = SampleProfile(surfaceProfile, curStationBL - .1);
                                    curX = originX + curStationBL - pvStStart;
                                    curY = originY + (sampledMidtElevation - pvElBottom) *
                                        profileViewStyle.GraphStyle.VerticalExaggeration;
                                    BlockReference brAtEnd =
                                        dB.CreateBlockWithAttributes(komponentBlockName, new Point3d(curX, curY, 0));
                                    brAtEnd.SetAttributeStringValue("LEFTSIZE", $"DN {pvSizeArray[i + 1].DN}");
                                    brAtEnd.SetAttributeStringValue("RIGHTSIZE", "");

                                    psmSourceReference.WritePropertyObject(
                                        brAtEnd, driSourceReference.AlignmentStation, curStationBL);
                                }
                            }
                            #endregion

                            #region Local method to sample profiles
                            //Local method to sample profiles
                            double SampleProfile(Profile profile, double station)
                            {
                                double sampledElevation = 0;
                                try { sampledElevation = profile.ElevationAt(station); }
                                catch (System.Exception)
                                {
                                    prdDbg($"Station {station} threw an exception when placing size change blocks! Skipping...");
                                    return 0;
                                }
                                return sampledElevation;
                            }
                            #endregion

                            #region Place component blocks
                            foreach (BlockReference br in brs)
                            {
                                string type = ReadStringParameterFromDataTable(br.RealName(), fjvKomponenter, "Type", 0);
                                if (type == "Reduktion" || type == "Svejsning") continue;
                                //Buerør need special treatment
                                if (br.RealName() == "BUEROR1" || br.RealName() == "BUEROR2") continue;
                                Point3d brLocation = al.GetClosestPointTo(br.Position, false);

                                double station = 0;
                                double offset = 0;
                                try
                                {
                                    al.StationOffset(brLocation.X, brLocation.Y, ref station, ref offset);
                                }
                                catch (System.Exception)
                                {
                                    prdDbg(br.Position.ToString());
                                    prdDbg(brLocation.ToString());
                                    throw;
                                }

                                //Determine if blockref is within current PV
                                //If within -> place block, else go to next iteration
                                if (!(station >= pvStStart && station <= pvStEnd)) continue;

                                sampledMidtElevation = SampleProfile(surfaceProfile, station);
                                double X = originX + station - pvStStart;
                                double Y = originY + (sampledMidtElevation - pvElBottom) *
                                        profileViewStyle.GraphStyle.VerticalExaggeration;

                                BlockReference brSign = dB.CreateBlockWithAttributes(komponentBlockName, new Point3d(X, Y, 0));

                                //Get br type and process it if it is dynamic
                                //Write the type of augmentedType to the Left attribute
                                string augmentedType = default;
                                if (type.StartsWith("$"))
                                    augmentedType = ComponentSchedule.ReadComponentType(br, fjvKomponenter);
                                if (augmentedType != default) brSign.SetAttributeStringValue("LEFTSIZE", augmentedType);
                                else brSign.SetAttributeStringValue("LEFTSIZE", type);

                                //Manage writing of right attribute
                                if ((new[] {
                                    "Parallelafgrening",
                                    "Lige afgrening",
                                    "Afgrening med spring",
                                    "Afgrening, parallel" }).Contains(type))
                                    brSign.SetAttributeStringValue("RIGHTSIZE",
                                        psmPipeLineData.ReadPropertyString(br, driPipelineData.BranchesOffToAlignment));
                                else if (type == "Afgreningsstuds" || type == "Svanehals")
                                    brSign.SetAttributeStringValue("RIGHTSIZE",
                                        psmPipeLineData.ReadPropertyString(br, driPipelineData.BelongsToAlignment));
                                else brSign.SetAttributeStringValue("RIGHTSIZE", "");

                                psmSourceReference.WritePropertyString(
                                    brSign, driSourceReference.SourceEntityHandle, br.Handle.ToString());
                                psmSourceReference.WritePropertyObject(
                                    brSign, driSourceReference.AlignmentStation, station);
                            }
                            #endregion

                            #region Place buerør blocks
                            foreach (BlockReference br in brs)
                            {
                                //Buerør need special treatment
                                if (br.RealName() != "BUEROR1" && br.RealName() != "BUEROR2") continue;
                                string type = ReadStringParameterFromDataTable(br.RealName(), fjvKomponenter, "Type", 0);
                                string augmentedType = ComponentSchedule.ReadComponentType(br, fjvKomponenter);

                                BlockTableRecord btr = br.BlockTableRecord.Go<BlockTableRecord>(fremTx);

                                List<Point3d> locs = new List<Point3d>();

                                foreach (Oid id in btr)
                                {
                                    if (!id.IsDerivedFrom<BlockReference>()) continue;
                                    BlockReference nestedBr = id.Go<BlockReference>(fremTx);
                                    if (!nestedBr.Name.Contains("MuffeIntern")) continue;
                                    Point3d wPt = nestedBr.Position;
                                    wPt = wPt.TransformBy(br.BlockTransform);

                                    locs.Add(wPt);
                                    //Line line = new Line(new Point3d(), wPt);
                                    //line.AddEntityToDbModelSpace(localDb);
                                }

                                if (locs.Count > 2) prdDbg($"Block: {br.Handle} have more than two locations!");

                                double firstStation = 0;
                                double secondStation = 0;
                                double offset = 0;
                                Point3d pos = default;
                                try
                                {
                                    pos = locs.First();
                                    al.StationOffset(pos.X, pos.Y, ref firstStation, ref offset);
                                    pos = locs.Last();
                                    al.StationOffset(pos.X, pos.Y, ref secondStation, ref offset);
                                }
                                catch (System.Exception)
                                {
                                    prdDbg(pos);
                                    prdDbg(br.Position.ToString());
                                    throw;
                                }

                                //prdDbg($"First st: {firstStation}");
                                //prdDbg($"Second st: {secondStation}");

                                //Determine the middle point
                                double station = firstStation > secondStation ?
                                    secondStation + (firstStation - secondStation) / 2 :
                                    firstStation + (secondStation - firstStation) / 2;

                                //Determine the length of buerør
                                double bueRorLength = firstStation > secondStation ?
                                    firstStation - secondStation :
                                    secondStation - firstStation;

                                //Determine if blockref is within current PV
                                //If within -> place block, else go to next iteration
                                if (!(station > pvStStart && station < pvStEnd)) continue;

                                sampledMidtElevation = SampleProfile(surfaceProfile, station);
                                double X = originX + station - pvStStart;
                                double Y = originY + (sampledMidtElevation - pvElBottom) *
                                        profileViewStyle.GraphStyle.VerticalExaggeration;

                                BlockReference brBueRor = dB.CreateBlockWithAttributes(bueBlockName, new Point3d(X, Y, 0));

                                //Get br type and process it if it is dynamic
                                //Write the type of augmentedType to the Left attribute

                                DynamicBlockReferencePropertyCollection dbrpc = brBueRor.DynamicBlockReferencePropertyCollection;
                                foreach (DynamicBlockReferenceProperty dbrp in dbrpc)
                                {
                                    if (dbrp.PropertyName == "Length")
                                    {
                                        //prdDbg(length.ToString());
                                        dbrp.Value = Math.Abs(bueRorLength);
                                    }
                                }

                                //Set length text
                                brBueRor.SetAttributeStringValue("LGD", Math.Abs(bueRorLength).ToString("0.0") + " m");
                                brBueRor.SetAttributeStringValue("TEXT", augmentedType);

                                psmSourceReference.WritePropertyString(
                                    brBueRor, driSourceReference.SourceEntityHandle, br.Handle.ToString());
                                psmSourceReference.WritePropertyObject(
                                    brBueRor, driSourceReference.AlignmentStation, station);
                            }
                            #endregion

                            #region Find curves and annotate
                            foreach (Curve curve in curves)
                            {
                                if (curve is Polyline pline)
                                {
                                    //Detect arcs and determine if it is a buerør or not
                                    for (int i = 0; i < pline.NumberOfVertices; i++)
                                    {
                                        TypeOfSegment tos;
                                        double bulge = pline.GetBulgeAt(i);
                                        if (bulge == 0) tos = TypeOfSegment.Straight;
                                        else
                                        {
                                            //Determine if centre of arc is within view
                                            CircularArc2d arcSegment2dAt;
                                            try
                                            {
                                                arcSegment2dAt = pline.GetArcSegment2dAt(i);
                                            }
                                            catch (System.Exception)
                                            {
                                                prdDbg($"Pline {pline.Handle} threw when accessing arc segment at {i}!");
                                                prdDbg("Én af grundene kunne være, at ikke er alle plinjer blevet vendt om med strømmen.");
                                                throw;
                                            }

                                            Point2d samplePoint = ((Curve2d)arcSegment2dAt).GetSamplePoints(11)[5];
                                            Point3d location = al.GetClosestPointTo(
                                                        new Point3d(samplePoint.X, samplePoint.Y, 0), false);
                                            double centreStation = 0;
                                            double centreOffset = 0;
                                            al.StationOffset(location.X, location.Y, ref centreStation, ref centreOffset);

                                            //If centre of arc is not within PV -> continue
                                            if (!(centreStation > pvStStart && centreStation < pvStEnd)) continue;

                                            //Calculate radius
                                            double u = pline.GetPoint2dAt(i).GetDistanceTo(pline.GetPoint2dAt(i + 1));
                                            double radius = u * ((1 + bulge.Pow(2)) / (4 * Math.Abs(bulge)));
                                            double minRadius = GetPipeMinElasticRadius(pline);

                                            if (radius < minRadius) tos = TypeOfSegment.CurvedPipe;
                                            else tos = TypeOfSegment.ElasticArc;

                                            //Acquire start and end stations
                                            location = al.GetClosestPointTo(pline.GetPoint3dAt(i), false);
                                            double curveStartStation = 0;
                                            double offset = 0;
                                            al.StationOffset(location.X, location.Y, ref curveStartStation, ref offset);

                                            location = al.GetClosestPointTo(pline.GetPoint3dAt(i + 1), false);
                                            double curveEndStation = 0;
                                            al.StationOffset(location.X, location.Y, ref curveEndStation, ref offset);
                                            double length = curveEndStation - curveStartStation;
                                            //double midStation = curveStartStation + length / 2;

                                            sampledMidtElevation = SampleProfile(surfaceProfile, centreStation);
                                            curX = originX + centreStation - pvStStart;
                                            curY = originY + (sampledMidtElevation - pvElBottom) *
                                                    profileViewStyle.GraphStyle.VerticalExaggeration;
                                            Point3d curvePt = new Point3d(curX, curY, 0);
                                            BlockReference brCurve =
                                                dB.CreateBlockWithAttributes(bueBlockName, curvePt);

                                            DynamicBlockReferencePropertyCollection dbrpc = brCurve.DynamicBlockReferencePropertyCollection;
                                            foreach (DynamicBlockReferenceProperty dbrp in dbrpc)
                                            {
                                                if (dbrp.PropertyName == "Length")
                                                {
                                                    //prdDbg(length.ToString());
                                                    dbrp.Value = Math.Abs(length);
                                                }
                                            }

                                            //Set length text
                                            brCurve.SetAttributeStringValue("LGD", Math.Abs(length).ToString("0.0") + " m");

                                            switch (tos)
                                            {
                                                case TypeOfSegment.ElasticArc:
                                                    brCurve.SetAttributeStringValue("TEXT", $"Elastisk bue {radius.ToString("0.0")} m");
                                                    break;
                                                case TypeOfSegment.CurvedPipe:
                                                    brCurve.SetAttributeStringValue("TEXT", $"Buerør {radius.ToString("0.0")} m");
                                                    break;
                                                default:
                                                    break;
                                            }
                                        }
                                    }
                                }
                            }
                            #endregion
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    fremTx.Abort();
                    fremTx.Dispose();
                    fremDb.Dispose();
                    tx.Abort();
                    prdDbg(ex.ExceptionInfo());
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
        /// Creates detailing based on an existing MIDT profile
        /// </summary>
        [CommandMethod("CREATEDETAILING")]
        public void createdetailing()
        {
            createdetailingmethod();
        }
        public void createdetailingmethod(DataReferencesOptions dataReferencesOptions = default, Database database = default)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database dB = database ?? docCol.MdiActiveDocument.Database;
            Editor ed = docCol.MdiActiveDocument.Editor;

            using (Transaction tx = dB.TransactionManager.StartTransaction())
            {
                #region Open fremtidig db
                DataReferencesOptions dro = dataReferencesOptions ?? new DataReferencesOptions();
                string projectName = dro.ProjectName;
                string etapeName = dro.EtapeName;

                // open the xref database
                Database fremDb = new Database(false, true);
                fremDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Fremtid"),
                    FileOpenMode.OpenForReadAndAllShare, false, null);
                Transaction fremTx = fremDb.TransactionManager.StartTransaction();
                HashSet<Curve> allCurves = fremDb.GetFjvPipes(fremTx).Cast<Curve>().ToHashSet();
                HashSet<BlockReference> allBrs = fremDb.HashSetOfType<BlockReference>(fremTx);
                #endregion

                //////////////////////////////////////
                string komponentBlockName = "DRISizeChangeAnno";
                string bueBlockName = "DRIPipeArcAnno";
                string weldBlockName = "DRIWeldAnno";
                string weldNumberBlockName = "DRIWeldAnnoText";
                //////////////////////////////////////

                try
                {
                    #region Common variables
                    BlockTable bt = tx.GetObject(dB.BlockTableId, OpenMode.ForRead) as BlockTable;
                    HashSet<Alignment> als = dB.HashSetOfType<Alignment>(tx);
                    #endregion

                    #region Initialize PS for source object reference
                    PropertySetManager psmSource = new PropertySetManager(
                        dB, PSetDefs.DefinedSets.DriSourceReference);
                    PSetDefs.DriSourceReference driSourceReference =
                        new PSetDefs.DriSourceReference();
                    #endregion

                    #region Initialize PS for Alignment
                    PropertySetManager psmPipeLineData = new PropertySetManager(
                        fremDb,
                        PSetDefs.DefinedSets.DriPipelineData);
                    PSetDefs.DriPipelineData driPipelineData =
                        new PSetDefs.DriPipelineData();
                    #endregion

                    #region Import blocks if missing
                    if (!bt.Has(komponentBlockName) ||
                        !bt.Has(bueBlockName) ||
                        !bt.Has(weldBlockName) ||
                        !bt.Has(weldNumberBlockName))
                    {
                        prdDbg("Some of the blocks for detailing are missing! Importing...");
                        Database blockDb = new Database(false, true);
                        blockDb.ReadDwgFile("X:\\AutoCAD DRI - 01 Civil 3D\\DynBlokke\\Symboler.dwg",
                            FileOpenMode.OpenForReadAndAllShare, false, null);
                        Transaction blockTx = blockDb.TransactionManager.StartTransaction();

                        Oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(blockDb);
                        //Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);
                        Oid destDbMsId = dB.BlockTableId;

                        BlockTable sourceBt = blockTx.GetObject(blockDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                        ObjectIdCollection idsToClone = new ObjectIdCollection();
                        if (!bt.Has(komponentBlockName)) idsToClone.Add(sourceBt[komponentBlockName]);
                        if (!bt.Has(bueBlockName)) idsToClone.Add(sourceBt[bueBlockName]);
                        if (!bt.Has(weldBlockName)) idsToClone.Add(sourceBt[weldBlockName]);
                        if (!bt.Has(weldNumberBlockName)) idsToClone.Add(sourceBt[weldNumberBlockName]);

                        IdMapping mapping = new IdMapping();
                        blockDb.WblockCloneObjects(idsToClone, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);
                        blockTx.Commit();
                        blockTx.Dispose();
                        blockDb.Dispose();
                    }
                    #endregion

                    #region Delete previous blocks
                    deletedetailingmethod(dB);
                    #endregion

                    foreach (Alignment al in als.OrderBy(x => x.Name))
                    {
                        prdDbg($"\nProcessing: {al.Name}...");

                        //Polyline alPline = al.GetPolyline().Go<Polyline>(tx);

                        #region If exist get surface profile and profile view
                        ObjectIdCollection profileIds = al.GetProfileIds();
                        ObjectIdCollection profileViewIds = al.GetProfileViewIds();
                        ProfileViewCollection pvs = new ProfileViewCollection(profileViewIds);

                        #region Fetch surface profile
                        Profile surfaceProfile = null;
                        foreach (Oid oid in profileIds)
                        {
                            Profile pTemp = oid.Go<Profile>(tx);
                            if (pTemp.Name == $"{al.Name}_surface_P") surfaceProfile = pTemp;
                        }
                        if (surfaceProfile == null)
                        {
                            prdDbg($"No surface profile found for alignment: {al.Name}, skipping current alignment.");
                            continue;
                        }
                        #endregion
                        #region Fetch midt profile
                        Profile midtProfile = null;
                        foreach (Oid oid in profileIds)
                        {
                            Profile pTemp = oid.Go<Profile>(tx);
                            if (pTemp.Name.Contains("MIDT")) midtProfile = pTemp;
                        }
                        if (midtProfile == null)
                        {
                            prdDbg($"No midt profile found for alignment: {al.Name}, skipping current alignment.");
                            continue;
                        }
                        #endregion
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

                        HashSet<BlockReference> afgreningsStudse = allBrs
                            .Where(x =>
                                psmPipeLineData.FilterPropetyString(
                                    x, driPipelineData.BranchesOffToAlignment, al.Name) &&
                                (x.RealName() == "AFGRSTUDS" || x.RealName() == "SH LIGE"))
                            .ToHashSet();

                        //Tilføj afgreningsstudse til blokke
                        brs.UnionWith(afgreningsStudse);

                        prdDbg($"Curves: {curves.Count}, Components: {brs.Count}");
                        #endregion

                        prdDbg("Blocks:");
                        PipelineSizeArray sizeArray = new PipelineSizeArray(al, curves, brs);
                        prdDbg(sizeArray.ToString());

                        #region Explode midt profile for later sampling
                        DBObjectCollection objs = new DBObjectCollection();
                        //First explode
                        midtProfile.Explode(objs);
                        //Explodes to 1 block
                        prdDbg($"Profile exploded to number of items: {objs.Count}.");
                        Entity firstExplode = (Entity)objs[0];

                        //Second explode
                        objs = new DBObjectCollection();
                        firstExplode.Explode(objs);
                        prdDbg($"Subsequent object exploded to number of items: {objs.Count}.");

                        HashSet<Line> lines = new HashSet<Line>();
                        foreach (DBObject obj in objs) lines.Add((Line)obj);

                        Extents3d extentsPv = default;
                        var isInsideQuery = lines
                            .Where(line =>
                            extentsPv.IsPointInsideXY(line.StartPoint) &&
                            extentsPv.IsPointInsideXY(line.EndPoint));

                        HashSet<Polyline> polylinesToGetDerivative = new HashSet<Polyline>();

                        //Join the resulting lines
                        foreach (ProfileView pv in pvs)
                        {
                            Extents3d te = pv.GeometricExtents;
                            extentsPv = new Extents3d(
                                new Point3d(te.MinPoint.X - 1, te.MinPoint.Y - 1, 0),
                                new Point3d(te.MaxPoint.X + 1, te.MaxPoint.Y + 1, 0));

                            var linesInside = isInsideQuery.ToList();

                            Line seedLine = linesInside[0];
                            linesInside.RemoveAt(0);

                            Polyline pline = new Polyline();
                            pline.AddVertexAt(0, new Point2d(seedLine.StartPoint.X, seedLine.StartPoint.Y), 0, 0, 0);
                            pline.AddVertexAt(1, new Point2d(seedLine.EndPoint.X, seedLine.EndPoint.Y), 0, 0, 0);

                            try
                            {
                                if (linesInside.Count != 0)
                                    pline.JoinEntities(linesInside.Cast<Entity>().ToArray());
                            }
                            catch (System.Exception)
                            {
                                prdDbg($"Midt i {pv.Name} could not be joined!");
                                throw;
                            }
                            polylinesToGetDerivative.Add(pline);

                            //pline.AddEntityToDbModelSpace(localDb);
                        }

                        #endregion

                        foreach (ProfileView pv in pvs)
                        {
                            prdDbg($"Processing PV {pv.Name}.");

                            //Collection to hold component symbol blocks for overlap analysis
                            List<BlockReference> allNewBrs = new List<BlockReference>();

                            #region Variables and settings
                            Point3d pvOrigin = pv.Location;
                            double originX = pvOrigin.X;
                            double originY = pvOrigin.Y;

                            double pvStStart = pv.StationStart;
                            double pvStEnd = pv.StationEnd;
                            //This is needed because polyline gives slightly deviating
                            //Station values at start and ends
                            double extension = 0.01;
                            double extendedPvStStart = pvStStart - extension;
                            double extendedPvStEnd = pvStEnd + extension;

                            double pvElBottom = pv.ElevationMin;
                            double pvElTop = pv.ElevationMax;
                            double pvLength = pvStEnd - pvStStart;
                            #endregion

                            #region Determine what sizes appear in current PV
                            var pvSizeArray = sizeArray.GetPartialSizeArrayForPV(pv);
                            prdDbg(pvSizeArray.ToString());
                            #endregion

                            #region Prepare exaggeration handling
                            ProfileViewStyle profileViewStyle = tx
                                .GetObject(((Autodesk.Aec.DatabaseServices.Entity)pv)
                                .StyleId, OpenMode.ForRead) as ProfileViewStyle;
                            #endregion

                            double curStationBL = 0;
                            double sampledMidtElevation = 0;
                            double curX = 0, curY = 0;

                            #region Place size change blocks
                            for (int i = 0; i < pvSizeArray.Length; i++)
                            {   //Although look ahead is used, normal iteration is required
                                //Or cases where sizearray is only 1 size will not run at all
                                //In more general case the last iteration must be aborted
                                if (pvSizeArray.Length != 1 && i != pvSizeArray.Length - 1)
                                {
                                    //General case
                                    curStationBL = pvSizeArray[i].EndStation;
                                    sampledMidtElevation = SampleProfile(midtProfile, curStationBL);
                                    curX = originX + pvSizeArray[i].EndStation - pvStStart;
                                    curY = originY + (sampledMidtElevation - pvElBottom) *
                                        profileViewStyle.GraphStyle.VerticalExaggeration;
                                    double deltaY = (sampledMidtElevation - pvElBottom) *
                                        profileViewStyle.GraphStyle.VerticalExaggeration;
                                    //prdDbg($"{originY} + ({sampledMidtElevation} - {pvElBottom}) * " +
                                    //    $"{profileViewStyle.GraphStyle.VerticalExaggeration} = {deltaY}");
                                    BlockReference brInt =
                                        dB.CreateBlockWithAttributes(komponentBlockName, new Point3d(curX, curY, 0));
                                    brInt.SetAttributeStringValue("LEFTSIZE", $"DN {pvSizeArray[i].DN}");
                                    brInt.SetAttributeStringValue("RIGHTSIZE", $"DN {pvSizeArray[i + 1].DN}");

                                    allNewBrs.Add(brInt);
                                }
                                //Special cases
                                if (i == 0)
                                {//First iteration
                                    curStationBL = pvStStart;
                                    sampledMidtElevation = SampleProfile(midtProfile, curStationBL);
                                    curX = originX;
                                    curY = originY + (sampledMidtElevation - pvElBottom) *
                                        profileViewStyle.GraphStyle.VerticalExaggeration;
                                    BlockReference brAt0 =
                                        dB.CreateBlockWithAttributes(komponentBlockName, new Point3d(curX, curY, 0));
                                    brAt0.SetAttributeStringValue("LEFTSIZE", "");
                                    brAt0.SetAttributeStringValue("RIGHTSIZE", $"DN {pvSizeArray[0].DN}");

                                    allNewBrs.Add(brAt0);

                                    if (pvSizeArray.Length == 1)
                                    {//If only one size in the array also place block at end
                                        curStationBL = pvStEnd;
                                        sampledMidtElevation = SampleProfile(midtProfile, curStationBL - .1);
                                        curX = originX + curStationBL - pvStStart;
                                        curY = originY + (sampledMidtElevation - pvElBottom) *
                                            profileViewStyle.GraphStyle.VerticalExaggeration;
                                        BlockReference brAtEnd =
                                            dB.CreateBlockWithAttributes(komponentBlockName, new Point3d(curX, curY, 0));
                                        brAtEnd.SetAttributeStringValue("LEFTSIZE", $"DN {pvSizeArray[0].DN}");
                                        brAtEnd.SetAttributeStringValue("RIGHTSIZE", "");

                                        allNewBrs.Add(brAtEnd);
                                    }
                                }
                                if (i == pvSizeArray.Length - 2)
                                {//End of the iteration
                                    curStationBL = pvStEnd;
                                    sampledMidtElevation = SampleProfile(midtProfile, curStationBL - .1);
                                    curX = originX + curStationBL - pvStStart;
                                    curY = originY + (sampledMidtElevation - pvElBottom) *
                                        profileViewStyle.GraphStyle.VerticalExaggeration;
                                    BlockReference brAtEnd =
                                        dB.CreateBlockWithAttributes(komponentBlockName, new Point3d(curX, curY, 0));
                                    brAtEnd.SetAttributeStringValue("LEFTSIZE", $"DN {pvSizeArray[i + 1].DN}");
                                    brAtEnd.SetAttributeStringValue("RIGHTSIZE", "");

                                    allNewBrs.Add(brAtEnd);
                                }
                            }
                            #endregion

                            #region Local method to sample profiles
                            //Local method to sample profiles
                            double SampleProfile(Profile profile, double station)
                            {
                                double sampledElevation = 0;
                                try { sampledElevation = profile.ElevationAt(station); }
                                catch (System.Exception)
                                {
                                    prdDbg($"Station {station} threw an exception when placing size change blocks! Skipping...");
                                    return 0;
                                }
                                return sampledElevation;
                            }
                            #endregion

                            #region Place component blocks
                            System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(
                                @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");
                            foreach (BlockReference br in brs)
                            {
                                string type = ReadStringParameterFromDataTable(br.RealName(), fjvKomponenter, "Type", 0);
                                if (type == "Reduktion" || type == "Svejsning") continue;

                                //Buerør need special treatment
                                if (br.RealName() == "BUEROR1" || br.RealName() == "BUEROR2") continue;

                                Polyline pl = al.GetPolyline().Go<Polyline>(tx);
                                //Point3d tpt = al.GetClosestPointTo(br.Position, false);
                                Point3d tpt = pl.GetClosestPointTo(br.Position, false);
                                pl.CheckOrOpenForWrite();
                                pl.Erase();

                                double station = 0;
                                double offset = 0;
                                try
                                {
                                    al.StationOffset(tpt.X, tpt.Y, ref station, ref offset);
                                }
                                catch (System.Exception)
                                {
                                    prdDbg(br.RealName());
                                    prdDbg(br.Handle.ToString());
                                    prdDbg(br.Position.ToString());
                                    throw;
                                }

                                //Determine if blockref is within current PV
                                //If within -> place block, else go to next iteration

                                if (!(station >= extendedPvStStart && station <= extendedPvStEnd)) continue;

                                sampledMidtElevation = SampleProfile(midtProfile, station);
                                double X = originX + station - pvStStart;
                                double Y = originY + (sampledMidtElevation - pvElBottom) *
                                        profileViewStyle.GraphStyle.VerticalExaggeration;
                                BlockReference brSign = dB.CreateBlockWithAttributes(komponentBlockName, new Point3d(X, Y, 0));
                                brSign.SetAttributeStringValue("LEFTSIZE",
                                    ComponentSchedule.ReadComponentType(br, fjvKomponenter));

                                //Manage writing of right attribute
                                if ((new[] {
                                    "Parallelafgrening",
                                    "Lige afgrening",
                                    "Afgrening med spring",
                                    "Afgrening, parallel" }).Contains(type))
                                    brSign.SetAttributeStringValue("RIGHTSIZE",
                                        psmPipeLineData.ReadPropertyString(
                                            br, driPipelineData.BranchesOffToAlignment));
                                else if (type == "Afgreningsstuds" || type == "Svanehals")
                                    brSign.SetAttributeStringValue("RIGHTSIZE",
                                        psmPipeLineData.ReadPropertyString(
                                            br, driPipelineData.BelongsToAlignment));
                                else brSign.SetAttributeStringValue("RIGHTSIZE", "");

                                allNewBrs.Add(brSign);

                                psmSource.WritePropertyString(brSign,
                                    driSourceReference.SourceEntityHandle, br.Handle.ToString());
                            }
                            #endregion

                            #region Place buerør blocks
                            foreach (BlockReference br in brs)
                            {
                                //Buerør need special treatment
                                if (br.RealName() != "BUEROR1" && br.RealName() != "BUEROR2") continue;
                                string type = ReadStringParameterFromDataTable(
                                    br.RealName(), fjvKomponenter, "Type", 0);
                                string augmentedType = ComponentSchedule.ReadComponentType(br, fjvKomponenter);

                                //The idea is to get the muffer at ends
                                //Get the station at both muffe intern
                                //And then use the station values to calculate position and length
                                BlockTableRecord btr = br.BlockTableRecord.Go<BlockTableRecord>(fremTx);

                                List<Point3d> locs = new List<Point3d>();

                                foreach (Oid id in btr)
                                {
                                    if (!id.IsDerivedFrom<BlockReference>()) continue;
                                    BlockReference nestedBr = id.Go<BlockReference>(fremTx);
                                    if (!nestedBr.Name.Contains("MuffeIntern")) continue;
                                    Point3d wPt = nestedBr.Position;
                                    wPt = wPt.TransformBy(br.BlockTransform);

                                    locs.Add(wPt);
                                    //Line line = new Line(new Point3d(), wPt);
                                    //line.AddEntityToDbModelSpace(localDb);
                                }

                                if (locs.Count > 2) prdDbg($"Block: {br.Handle} have more than two locations!");

                                double firstStation = 0;
                                double secondStation = 0;
                                double offset = 0;
                                Point3d pos = default;
                                try
                                {
                                    pos = locs.First();
                                    al.StationOffset(pos.X, pos.Y, ref firstStation, ref offset);
                                    pos = locs.Last();
                                    al.StationOffset(pos.X, pos.Y, ref secondStation, ref offset);
                                }
                                catch (System.Exception)
                                {
                                    prdDbg(pos);
                                    prdDbg(br.Position.ToString());
                                    throw;
                                }

                                //prdDbg($"First st: {firstStation}");
                                //prdDbg($"Second st: {secondStation}");

                                //Determine the middle point
                                double station = firstStation > secondStation ?
                                    secondStation + (firstStation - secondStation) / 2 :
                                    firstStation + (secondStation - firstStation) / 2;

                                //Determine the length of buerør
                                double bueRorLength = firstStation > secondStation ?
                                    firstStation - secondStation :
                                    secondStation - firstStation;

                                //Determine if blockref is within current PV
                                //If within -> place block, else go to next iteration
                                if (!(station > pvStStart && station < pvStEnd)) continue;

                                sampledMidtElevation = SampleProfile(surfaceProfile, station);
                                double X = originX + station - pvStStart;
                                double Y = originY + (sampledMidtElevation - pvElBottom) *
                                        profileViewStyle.GraphStyle.VerticalExaggeration;

                                BlockReference brBueRor = dB.CreateBlockWithAttributes(bueBlockName, new Point3d(X, Y, 0));

                                DynamicBlockReferencePropertyCollection dbrpc =
                                    brBueRor.DynamicBlockReferencePropertyCollection;
                                foreach (DynamicBlockReferenceProperty dbrp in dbrpc)
                                {
                                    if (dbrp.PropertyName == "Length")
                                    {
                                        //prdDbg(length.ToString());
                                        dbrp.Value = Math.Abs(bueRorLength);
                                    }
                                }

                                //Set length text
                                brBueRor.SetAttributeStringValue("LGD", Math.Abs(bueRorLength).ToString("0.0") + " m");
                                brBueRor.SetAttributeStringValue("TEXT", augmentedType);
                            }
                            #endregion

                            #region Sort overlapping component and size labels
                            var clusters = allNewBrs.GroupByCluster((x, y) => Overlaps(x, y), 0.0001);

                            foreach (IGrouping<BlockReference, BlockReference> cluster in clusters)
                            {
                                if (cluster.Count() < 2) continue;

                                var xSorted = cluster.OrderBy(x => x.Position.X).ToArray();
                                double deltaY = 0;
                                for (int i = 0; i < xSorted.Length - 1; i++)
                                {
                                    Extents3d extents = xSorted[i].GeometricExtents;
                                    deltaY = deltaY + (extents.MaxPoint.Y - xSorted[i].Position.Y) - 0.6156 + 0.1;

                                    BlockReference nextBlock = xSorted[i + 1];
                                    DynamicBlockReferencePropertyCollection dbrpc =
                                        nextBlock.DynamicBlockReferencePropertyCollection;
                                    foreach (DynamicBlockReferenceProperty dbrp in dbrpc)
                                    {
                                        if (dbrp.PropertyName == "TOP_EXTENSION")
                                        {
                                            //prdDbg(length.ToString());
                                            dbrp.Value = deltaY;
                                        }
                                    }
                                }
                            }

                            #endregion

                            #region Place weld blocks
                            HashSet<BlockReference> newWeldNumberBlocks = new HashSet<BlockReference>();

                            foreach (BlockReference br in brs)
                            {
                                #region Determine placement
                                string type = ReadStringParameterFromDataTable(br.RealName(), fjvKomponenter, "Type", 0);
                                if (type != "Svejsning") continue;

                                Polyline pl = al.GetPolyline().Go<Polyline>(tx);
                                //Point3d brLocation = al.GetClosestPointTo(br.Position, false);
                                Point3d brLocation = pl.GetClosestPointTo(br.Position, false);
                                pl.CheckOrOpenForWrite();
                                pl.Erase(true);

                                double station = 0;
                                double offset = 0;
                                al.StationOffset(brLocation.X, brLocation.Y, ref station, ref offset);

                                //Determine if blockref is within current PV
                                //If within -> place block, else go to next iteration
                                if (!(station >= extendedPvStStart && station <= extendedPvStEnd)) continue;

                                sampledMidtElevation = SampleProfile(midtProfile, station);
                                double X = originX + station - pvStStart;
                                double Y = originY + (sampledMidtElevation - pvElBottom) *
                                        profileViewStyle.GraphStyle.VerticalExaggeration;

                                Point3d wPt = new Point3d(X, Y, 0);

                                BlockReference brWeld =
                                    dB.CreateBlockWithAttributes(weldBlockName, wPt);

                                //Set attributes
                                string nummer = br.GetAttributeStringValue("NUMMER");

                                if (nummer != "")
                                {
                                    BlockReference brWeldNumber =
                                    dB.CreateBlockWithAttributes(weldNumberBlockName, wPt);

                                    //Gather new weld numebrs in a collection to be able to find overlaps
                                    newWeldNumberBlocks.Add(brWeldNumber);
                                    brWeldNumber.SetAttributeStringValue("NUMMER", nummer);
                                }

                                psmSource.WritePropertyString(brWeld,
                                    driSourceReference.SourceEntityHandle, br.Handle.ToString());
                                #endregion

                                #region Determine rotation
                                //Get the nearest exploded profile polyline and sample first derivative
                                HashSet<(Polyline pline, double dist)> ps = new HashSet<(Polyline pline, double dist)>();
                                foreach (Polyline pline in polylinesToGetDerivative)
                                {
                                    Point3d distPt = pline.GetClosestPointTo(wPt, false);
                                    ps.Add((pline, distPt.DistanceHorizontalTo(wPt)));
                                }
                                Polyline nearest = ps.MinBy(x => x.dist).FirstOrDefault().pline;

                                Vector3d deriv = nearest.GetFirstDerivative(
                                    nearest.GetClosestPointTo(wPt, false));

                                double rotation = Math.Atan2(deriv.Y, deriv.X);
                                brWeld.Rotation = rotation;
                                #endregion

                                #region Scale block to fit kappe
                                SizeEntry curSize = sizeArray.GetSizeAtStation(station);
                                brWeld.ScaleFactors = new Scale3d(1, curSize.Kod / 1000 *
                                    profileViewStyle.GraphStyle.VerticalExaggeration, 1);
                                #endregion
                            }
                            #endregion

                            #region Overlaps function
                            double Overlaps(BlockReference i, BlockReference j)
                            {
                                try
                                {
                                    Extents3d extI = i.GeometricExtents;
                                    Extents3d extJ = j.GeometricExtents;

                                    double wI = extI.MaxPoint.X - extI.MinPoint.X;
                                    double wJ = extJ.MaxPoint.X - extJ.MinPoint.X;

                                    double threshold = wI / 2 + wJ / 2;

                                    double centreIX = extI.MinPoint.X + wI / 2;
                                    double centreJX = extJ.MinPoint.X + wJ / 2;

                                    double dist = Math.Abs(centreIX - centreJX);
                                    double result = dist - threshold;

                                    return result < 0 ? 0 : result;
                                }
                                catch (System.Exception ex)
                                {
                                    prdDbg(ex);
                                    prdDbg(i.Handle + " " + j.Handle);
                                    throw;
                                }
                            }
                            #endregion

                            #region Find overlapping weld labels and find a solution
                            if (newWeldNumberBlocks.Count() > 1)
                            {
                                clusters = newWeldNumberBlocks.GroupByCluster((x, y) => Overlaps(x, y), 0.0001);

                                foreach (IGrouping<BlockReference, BlockReference> cluster in clusters)
                                {
                                    if (cluster.Count() < 2) continue;

                                    List<string> numbers = new List<string>();
                                    string prefix = "";
                                    foreach (BlockReference item in cluster)
                                    {
                                        string number = item.GetAttributeStringValue("NUMMER");
                                        var splits = number.Split('.');
                                        prefix = splits[0];
                                        numbers.Add(splits[1]);
                                    }

                                    List<int> convertedNumbers = new List<int>();
                                    foreach (string number in numbers)
                                    {
                                        int result;
                                        if (int.TryParse(number, out result)) convertedNumbers.Add(result);
                                    }

                                    convertedNumbers.Sort();

                                    string finalNumber = $"{prefix}.{convertedNumbers.First().ToString("000")}" +
                                        $" - {convertedNumbers.Last().ToString("000")}";

                                    int i = 0;
                                    foreach (BlockReference item in cluster)
                                    {
                                        if (i == 0) item.SetAttributeStringValue("NUMMER", finalNumber);
                                        else { item.Erase(true); }
                                        i++;
                                    }
                                }
                            }
                            #endregion

                            #region Find curves and annotate
                            foreach (Curve curve in curves)
                            {
                                if (curve is Polyline pline)
                                {
                                    //Detect arcs and determine if it is a buerør or not
                                    for (int i = 0; i < pline.NumberOfVertices; i++)
                                    {
                                        TypeOfSegment tos;
                                        double bulge = pline.GetBulgeAt(i);
                                        if (bulge == 0) tos = TypeOfSegment.Straight;
                                        else
                                        {
                                            //Determine if centre of arc is within view
                                            CircularArc2d arcSegment2dAt = pline.GetArcSegment2dAt(i);
                                            Point2d samplePoint = ((Curve2d)arcSegment2dAt).GetSamplePoints(11)[5];
                                            Point3d location = al.GetClosestPointTo(
                                                        new Point3d(samplePoint.X, samplePoint.Y, 0), false);
                                            double centreStation = 0;
                                            double centreOffset = 0;
                                            al.StationOffset(location.X, location.Y, ref centreStation, ref centreOffset);

                                            //If centre of arc is not within PV -> continue
                                            if (!(centreStation > pvStStart && centreStation < pvStEnd)) continue;

                                            //Calculate radius
                                            double u = pline.GetPoint2dAt(i).GetDistanceTo(pline.GetPoint2dAt(i + 1));
                                            double radius = u * ((1 + bulge.Pow(2)) / (4 * Math.Abs(bulge)));
                                            double minRadius = GetPipeMinElasticRadius(pline);

                                            if (radius < minRadius) tos = TypeOfSegment.CurvedPipe;
                                            else tos = TypeOfSegment.ElasticArc;

                                            //Acquire start and end stations
                                            location = al.GetClosestPointTo(pline.GetPoint3dAt(i), false);
                                            double curveStartStation = 0;
                                            double offset = 0;
                                            al.StationOffset(location.X, location.Y, ref curveStartStation, ref offset);

                                            location = al.GetClosestPointTo(pline.GetPoint3dAt(i + 1), false);
                                            double curveEndStation = 0;
                                            al.StationOffset(location.X, location.Y, ref curveEndStation, ref offset);

                                            double length = curveEndStation - curveStartStation;
                                            //double midStation = curveStartStation + length / 2;

                                            sampledMidtElevation = SampleProfile(midtProfile, centreStation);
                                            curX = originX + centreStation - pvStStart;
                                            curY = originY + (sampledMidtElevation - pvElBottom) *
                                                    profileViewStyle.GraphStyle.VerticalExaggeration;
                                            Point3d curvePt = new Point3d(curX, curY, 0);
                                            BlockReference brCurve =
                                                dB.CreateBlockWithAttributes(bueBlockName, curvePt);

                                            DynamicBlockReferencePropertyCollection dbrpc = brCurve.DynamicBlockReferencePropertyCollection;
                                            foreach (DynamicBlockReferenceProperty dbrp in dbrpc)
                                            {
                                                if (dbrp.PropertyName == "Length")
                                                {
                                                    //prdDbg(length.ToString());
                                                    dbrp.Value = Math.Abs(length);
                                                }
                                            }

                                            //Set length text
                                            brCurve.SetAttributeStringValue("LGD", "L=" + Math.Abs(length).ToString("0.0") + " m");

                                            switch (tos)
                                            {
                                                case TypeOfSegment.ElasticArc:
                                                    brCurve.SetAttributeStringValue("TEXT", $"Elastisk bue R={radius.ToString("0.0")} m");
                                                    break;
                                                case TypeOfSegment.CurvedPipe:
                                                    brCurve.SetAttributeStringValue("TEXT", $"Buerør R={radius.ToString("0.0")} m");
                                                    break;
                                                default:
                                                    break;
                                            }

                                            #region Determine rotation
                                            //Get the nearest exploded profile polyline and sample first derivative
                                            HashSet<(Polyline pline, double dist)> ps = new HashSet<(Polyline pline, double dist)>();
                                            foreach (Polyline pline2 in polylinesToGetDerivative)
                                            {
                                                Point3d distPt = pline2.GetClosestPointTo(curvePt, false);
                                                ps.Add((pline2, distPt.DistanceHorizontalTo(curvePt)));
                                            }
                                            Polyline nearest = ps.MinBy(x => x.dist).FirstOrDefault().pline;

                                            Vector3d deriv = nearest.GetFirstDerivative(
                                                nearest.GetClosestPointTo(curvePt, false));

                                            double rotation = Math.Atan2(deriv.Y, deriv.X);
                                            brCurve.Rotation = rotation;
                                            #endregion
                                        }
                                    }
                                }
                            }
                            #endregion
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    fremTx.Abort();
                    fremTx.Dispose();
                    fremDb.Dispose();
                    tx.Abort();
                    prdDbg(ex.ExceptionInfo());
                    prdDbg(ex.ToString());
                    return;
                }
                fremTx.Abort();
                fremTx.Dispose();
                fremDb.Dispose();
                tx.Commit();
            }
        }

        [CommandMethod("DELETEDETAILING")]
        public void deletedetailing()
        {
            deletedetailingmethod();
        }
        public void deletedetailingmethod(Database db = default)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = db ?? docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                //////////////////////////////////////
                List<string> names = new List<string>();
                names.Add("DRISizeChangeAnno");
                names.Add("DRIPipeArcAnno");
                names.Add("DRIWeldAnno");
                names.Add("DRIWeldAnnoText");
                //////////////////////////////////////

                #region Delete previous blocks
                //Delete previous blocks
                foreach (string name in names)
                {
                    var existingBlocks = localDb.HashSetOfType<BlockReference>(tx);
                    //prdDbg(existingBlocks.Count.ToString());
                    foreach (BlockReference br in existingBlocks)
                    {
                        if (!br.IsErased && br.RealName() == name)
                        {
                            br.CheckOrOpenForWrite();
                            br.Erase(true);
                        }
                    }
                }
                #endregion
                tx.Commit();
            }
        }

        [CommandMethod("CREATEPOINTSATVERTICES")]
        public void createpointsatvertices() => createpointsatverticesmethod();
        public void createpointsatverticesmethod(Extents3d bbox = default)
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
                    if (bbox != default)
                    {
                        plines = plines.Where(
                            x => bbox.IsExtentsInsideXY(x.GeometricExtents))
                            .ToHashSet();
                    }
                    #endregion

                    #region Layer handling
                    string localLayerName = "0-POINTS_FOR_PL_VERTICES";
                    localDb.CheckOrCreateLayer(localLayerName);
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
                    Regex regx = new Regex(@"(?<number>\d{2,3}?\s)");
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

        [CommandMethod("TESTPROFILEPARABOLA")]
        public void testprofileparabola()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            var profileId = Interaction.GetEntity("Select profile: ", typeof(Profile));
            if (profileId == Oid.Null) return;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    Profile profile = profileId.Go<Profile>(tx);

                    var entities = profile.Entities;

                    foreach (var entity in entities)
                    {
                        switch (entity)
                        {
                            case ProfileTangent tan:
                                prdDbg("Tangent entity!");
                                continue;
                            case ProfileCircular circular:
                                prdDbg($"Circular entity R:{circular.Radius}.");
                                continue;
                            case ProfileParabolaSymmetric parabolaSymmetric:
                                prdDbg($"ParabolcSymmetric entity CurveType: {parabolaSymmetric.CurveType}, " +
                                       $"EntityType: {parabolaSymmetric.EntityType}, " +
                                       $"Radius: {parabolaSymmetric.Radius}");
                                continue;
                            default:
                                prdDbg("Segment type: " + entity.GetType().ToString() + ". Lav om til circular!");
                                throw new System.Exception($"LookAheadAndGetBulge: ProfileEntity unknown type encountered!");
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
        public void staggerlabelsallmethod(
            Database db = default, HashSet<Oid> pvs = null)
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
                    if (pvs == null) pvs = localDb.HashSetIdsOfType<ProfileView>();
                    HashSet<ProfileProjectionLabel> labelsSet = 
                        localDb.HashSetOfType<ProfileProjectionLabel>(tx);

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

                    Oid rightStyleId = profileProjection_RIGHT_Style;
                    Oid leftStyleId = profileProjection_LEFT_Style;

                    var pm = new ProgressMeter();
                    pm.Start("Staggering labels...");
                    pm.SetLimit(pvs.Count);
                    foreach (var pv in pvs.Select(x => x.Go<ProfileView>(tx)))
                    {
                        ProfileProjectionLabel[] allLabels;
                        extents = pv.GeometricExtents;
                        allLabels = labelsInView.OrderBy(x => x.LabelLocation.X).ToArray();

                        //split allLabels into two arrays with exactly half elements in each
                        //left array is ordered by X ascending
                        //right array is ordere by X descending
                        int half = allLabels.Length / 2;
                        ProfileProjectionLabel[] leftLabels =
                            allLabels.Take(half).ToArray();
                        ProfileProjectionLabel[] rightLabels =
                            allLabels.Skip(half).OrderByDescending(x => x.LabelLocation.X).ToArray();

                        var pIds = pv.AlignmentId.Go<Alignment>(tx).GetProfileIds();
                        Profile surfaceP = default;
                        foreach (Oid oid in pIds) if (oid.Go<Profile>(tx).Name.EndsWith("_surface_P"))
                                surfaceP = oid.Go<Profile>(tx);

                        SortLabels(leftLabels, surfaceP, leftStyleId);
                        SortLabels(rightLabels, surfaceP, rightStyleId);

                        double FirstLabelCalculateLength(
                            ProfileProjectionLabel label, Profile sP)
                        {
                            if (sP != default)
                            {
                                //get the first label which is setting the start elevation
                                double station = 0;
                                double labelElevation = 0;
                                //Get station and elevation of label
                                pv.FindStationAndElevationAtXY(
                                    label.LabelLocation.X, label.LabelLocation.Y, ref station, ref labelElevation);
                                //Update elevation to be that of surface
                                double surfaceElevation = sP.ElevationAt(station);
                                double labelDepthUnderSurface = (surfaceElevation - labelElevation) * 2.5;
                                double userSpecifiedLabelHeightOverSurfaceM = 5;
                                double deltaM = labelDepthUnderSurface + userSpecifiedLabelHeightOverSurfaceM;
                                double calculatedLengthOfFirstLabel = deltaM / 250;
                                prdDbg($"{surfaceElevation}, {labelElevation}, {labelDepthUnderSurface}, {deltaM}, {calculatedLengthOfFirstLabel}");
                                return calculatedLengthOfFirstLabel;
                            }
                            else return 0;
                        }

                        void SortLabels(
                            ProfileProjectionLabel[] labels, Profile sP, Oid styleId)
                        {
                            if (labels.Length < 1) return;
                            double calculatedLengthOfFirstLabel =
                                FirstLabelCalculateLength(labels[0], sP);

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
                                secondLabel.CheckOrOpenForWrite();
                                secondLabel.StyleId = styleId;
                                //Handle first label
                                if (i == 0)
                                {
                                    firstLabel.CheckOrOpenForWrite();
                                    firstLabel.DimensionAnchorValue = calculatedLengthOfFirstLabel;
                                    firstLabel.StyleId = styleId;
                                }

                                Point3d firstLocationPoint = firstLabel.LabelLocation;
                                Point3d secondLocationPoint = secondLabel.LabelLocation;

                                //secondLabel.GeometricExtents.DrawExtents(localDb);
                                //Check to see if extents overlap
                                double secondAnchorDimensionInMeters = 0;
                                if (!secondLabel.GeometricExtents.ToExtents2d().IsOverlapping(
                                    firstLabel.GeometricExtents.ToExtents2d()))
                                {//Labels do not overlap, get length from surface
                                    secondAnchorDimensionInMeters =
                                        FirstLabelCalculateLength(secondLabel, sP);
                                }
                                else
                                {//Labels overlap, get length from previous
                                    double firstAnchorDimensionInMeters =
                                        firstLabel.DimensionAnchorValue * 250 + 0.0625;
                                    double locationDelta =
                                        firstLocationPoint.Y - secondLocationPoint.Y;
                                    secondAnchorDimensionInMeters =
                                        (locationDelta + firstAnchorDimensionInMeters + 0.75) / 250;
                                }

                                secondLabel.DimensionAnchorValue = secondAnchorDimensionInMeters;
                                secondLabel.DowngradeOpen();

                                //editor.WriteMessage($"\nAnchorDimensionValue: {firstLabel.DimensionAnchorValue}.");
                            }
                        }
                        pm.MeterProgress();
                        System.Windows.Forms.Application.DoEvents();
                    }
                    pm.Stop();
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

        [CommandMethod("mypfp")]
        public void profilefrompolyline()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database database = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            CivilDocument doc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = database.TransactionManager.StartTransaction())
            {
                try
                {
                    PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions("\n Select a polyline : ");
                    promptEntityOptions1.SetRejectMessage("\n Not a polyline");
                    promptEntityOptions1.AddAllowedClass(typeof(Polyline), true);
                    PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    Autodesk.AutoCAD.DatabaseServices.ObjectId plObjId = entity1.ObjectId;
                    PromptEntityOptions promptEntityOptions2 = new PromptEntityOptions("\n Select a ProfileView: ");
                    promptEntityOptions2.SetRejectMessage("\n Not a ProfileView");
                    promptEntityOptions2.AddAllowedClass(typeof(ProfileView), true);
                    PromptEntityResult entity2 = editor.GetEntity(promptEntityOptions2);
                    if (((PromptResult)entity2).Status != PromptStatus.OK) return;

                    ProfileView profileView = tx.GetObject(entity2.ObjectId, OpenMode.ForWrite) as ProfileView;
                    double x = 0.0;
                    double y = 0.0;
                    if (profileView.ElevationRangeMode == ElevationRangeType.Automatic)
                    {
                        profileView.ElevationRangeMode = ElevationRangeType.UserSpecified;
                        profileView.FindXYAtStationAndElevation(profileView.StationStart, profileView.ElevationMin, ref x, ref y);
                    }
                    else
                        profileView.FindXYAtStationAndElevation(profileView.StationStart, profileView.ElevationMin, ref x, ref y);

                    ProfileViewStyle profileViewStyle = tx
                        .GetObject(((Autodesk.Aec.DatabaseServices.Entity)profileView)
                        .StyleId, OpenMode.ForRead) as ProfileViewStyle;

                    Autodesk.AutoCAD.DatabaseServices.ObjectId layerId =
                        ((Autodesk.Aec.DatabaseServices.Entity)
                        (tx.GetObject(profileView.AlignmentId, OpenMode.ForRead) as Alignment)).LayerId;

                    Autodesk.AutoCAD.DatabaseServices.ObjectId profileStyleId = ((StyleCollectionBase)doc.Styles.ProfileStyles).FirstOrDefault();

                    Autodesk.AutoCAD.DatabaseServices.ObjectId profileLabelSetStylesId =
                        ((StyleCollectionBase)doc.Styles.LabelSetStyles.ProfileLabelSetStyles).FirstOrDefault();

                    Autodesk.AutoCAD.DatabaseServices.ObjectId profByLayout =
                        Profile.CreateByLayout("New Profile", profileView.AlignmentId, layerId, profileStyleId, profileLabelSetStylesId);

                    Profile profile = tx.GetObject(profByLayout, OpenMode.ForWrite) as Profile;

                    BlockTableRecord blockTableRecord = tx.GetObject(database.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;

                    Polyline polyline = tx.GetObject(plObjId, OpenMode.ForRead, false) as Polyline;

                    if (polyline != null)
                    {
                        int numOfVert = polyline.NumberOfVertices - 1;
                        Point2d point2d1;
                        Point2d point2d2;
                        Point2d point2d3;

                        for (int i = 0; i < numOfVert; i++)
                        {
                            switch (polyline.GetSegmentType(i))
                            {
                                case SegmentType.Line:
                                    LineSegment2d lineSegment2dAt = polyline.GetLineSegment2dAt(i);
                                    point2d1 = lineSegment2dAt.StartPoint;
                                    double x1 = point2d1.X;
                                    double y1 = point2d1.Y;
                                    double num4 = x1 - x;
                                    double num5 = (y1 - y) / profileViewStyle.GraphStyle.VerticalExaggeration + profileView.ElevationMin;
                                    point2d2 = new Point2d(num4, num5);

                                    point2d1 = lineSegment2dAt.EndPoint;
                                    double x2 = point2d1.X;
                                    double y2 = point2d1.Y;
                                    double num6 = x2 - x;
                                    double num7 = (y2 - y) / profileViewStyle.GraphStyle.VerticalExaggeration + profileView.ElevationMin;
                                    point2d3 = new Point2d(num6, num7);

                                    profile.Entities.AddFixedTangent(point2d2, point2d3);
                                    break;
                                case SegmentType.Arc:
                                    CircularArc2d arcSegment2dAt = polyline.GetArcSegment2dAt(i);

                                    point2d1 = arcSegment2dAt.StartPoint;
                                    double x3 = point2d1.X;
                                    double y3 = point2d1.Y;
                                    point2d1 = arcSegment2dAt.EndPoint;
                                    double x4 = point2d1.X;
                                    double y4 = point2d1.Y;

                                    double num8 = x3 - x;
                                    double num9 = (y3 - y) / profileViewStyle.GraphStyle.VerticalExaggeration + profileView.ElevationMin;
                                    double num10 = x4 - x;
                                    double num11 = (y4 - y) / profileViewStyle.GraphStyle.VerticalExaggeration + profileView.ElevationMin;

                                    Point2d samplePoint = ((Curve2d)arcSegment2dAt).GetSamplePoints(11)[5]; //<-- was (10)[6] here, is wrong?
                                    double num12 = samplePoint.X - x;
                                    double num13 = (samplePoint.Y - y) / profileViewStyle.GraphStyle.VerticalExaggeration + profileView.ElevationMin;

                                    Point2d point2d4 = new Point2d(num12, num13);
                                    point2d3 = new Point2d(num10, num11);
                                    point2d2 = new Point2d(num8, num9);
                                    profile.Entities.AddFixedSymmetricParabolaByThreePoints(point2d2, point2d4, point2d3);

                                    break;
                                case SegmentType.Coincident:
                                    break;
                                case SegmentType.Point:
                                    break;
                                case SegmentType.Empty:
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                }
                tx.Commit();
            }
        }

        [CommandMethod("POLYLINEFROMPROFILE")]
        public void polylinefromprofile()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database database = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            CivilDocument doc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = database.TransactionManager.StartTransaction())
            {
                try
                {
                    PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions("\n Select a Profile: ");
                    promptEntityOptions1.SetRejectMessage("\n Not a profile!");
                    promptEntityOptions1.AddAllowedClass(typeof(Profile), true);
                    PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    Profile profile = entity1.ObjectId.Go<Profile>(tx);

                    PromptEntityOptions promptEntityOptions2 = new PromptEntityOptions("\n Select a ProfileView: ");
                    promptEntityOptions2.SetRejectMessage("\n Not a ProfileView");
                    promptEntityOptions2.AddAllowedClass(typeof(ProfileView), true);
                    PromptEntityResult entity2 = editor.GetEntity(promptEntityOptions2);
                    if (((PromptResult)entity2).Status != PromptStatus.OK) return;
                    ProfileView profileView = entity2.ObjectId.Go<ProfileView>(tx);

                    ProfileEntityCollection entities = profile.Entities;
                    prdDbg($"Count of entities: {entities.Count}");
                    HashSet<string> types = new HashSet<string>();

                    Polyline pline = new Polyline(entities.Count + 1);

                    //Place first point
                    ProfileEntity pe = entities.EntityAtId(entities.FirstEntity);
                    double startX = 0.0, startY = 0.0;
                    profileView.FindXYAtStationAndElevation(pe.StartStation, pe.StartElevation, ref startX, ref startY);
                    Point2d startPoint = new Point2d(startX, startY);
                    Point2d endPoint = new Point2d();
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

                    pline.AddEntityToDbModelSpace(database);
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                }
                tx.Commit();
            }
        }

        [CommandMethod("POPULATEDISTANCES")]
        public void populatedistances()
        {
            populatedistancesmethod();
        }
        public void populatedistancesmethod(
            DataReferencesOptions dro = null, HashSet<Oid> pvs = null)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

            #region Open fremtidig db
            if (dro == null) dro = new DataReferencesOptions();
            string projectName = dro.ProjectName;
            string etapeName = dro.EtapeName;

            // open the xref database
            Database fremDb = new Database(false, true);
            fremDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Fremtid"),
                FileOpenMode.OpenForReadAndAllShare, false, null);
            Transaction fremTx = fremDb.TransactionManager.StartTransaction();
            HashSet<Curve> allCurves = fremDb.GetFjvPipes(fremTx).Cast<Curve>().ToHashSet();
            HashSet<BlockReference> allBrs = fremDb.HashSetOfType<BlockReference>(fremTx);
            // open the LER database
            Database lerDb = new Database(false, true);
            lerDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Ler"),
                FileOpenMode.OpenForReadAndAllShare, false, null);
            Transaction lerTx = lerDb.TransactionManager.StartTransaction();
            #endregion

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Read Csv Data for Layers
                    //Establish the pathnames to files
                    //Files should be placed in a specific folder on desktop
                    string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";
                    string pathDistnces = "X:\\AutoCAD DRI - 01 Civil 3D\\Distances.csv";

                    System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
                    System.Data.DataTable dtDistances = CsvReader.ReadCsvToDataTable(pathDistnces, "Distancer");
                    #endregion

                    BlockTableRecord space = (BlockTableRecord)tx.GetObject(localDb.CurrentSpaceId, OpenMode.ForWrite);
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForWrite) as BlockTable;

                    #region Create layer for draft profile
                    string afstandsMarkeringLayerName = "0-PROFILE_AFSTANDS_MARKERING";
                    using (Transaction txLag = localDb.TransactionManager.StartTransaction())
                    {
                        LayerTable lt = txLag.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;

                        if (!lt.Has(afstandsMarkeringLayerName))
                        {
                            LayerTableRecord ltr = new LayerTableRecord();
                            ltr.Name = afstandsMarkeringLayerName;
                            ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 0);
                            ltr.LineWeight = LineWeight.LineWeight000;

                            //Make layertable writable
                            lt.CheckOrOpenForWrite();

                            //Add the new layer to layer table
                            Oid ltId = lt.Add(ltr);
                            txLag.AddNewlyCreatedDBObject(ltr, true);
                        }
                        txLag.Commit();
                    }
                    #endregion

                    #region Instantiate property set manager
                    PSetDefs.DriCrossingData driCrossingData = new PSetDefs.DriCrossingData();
                    PropertySetManager psmDriCrossingData =
                        new PropertySetManager(localDb, driCrossingData.SetName);

                    PropertySetManager psmPipeLineData = new PropertySetManager(
                        fremDb,
                        PSetDefs.DefinedSets.DriPipelineData);
                    PSetDefs.DriPipelineData driPipelineData =
                        new PSetDefs.DriPipelineData();
                    #endregion

                    if (pvs == null) pvs = localDb.HashSetIdsOfType<ProfileView>();
                    foreach (ProfileView pv in pvs.Select(x => x.Go<ProfileView>(tx)))
                    {
                        #region Variables declaration
                        System.Windows.Forms.Application.DoEvents();
                        Alignment al = pv.AlignmentId.Go<Alignment>(tx);
                        Point3d pvOrigin = pv.Location;
                        double originX = pvOrigin.X;
                        double originY = pvOrigin.Y;

                        double pvStStart = pv.StationStart;
                        double pvStEnd = pv.StationEnd;
                        double pvElBottom = pv.ElevationMin;
                        double pvElTop = pv.ElevationMax;
                        double pvLength = pvStEnd - pvStStart;
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
                        prdDbg($"Curves: {curves.Count}, Components: {brs.Count}");
                        #endregion

                        #region Build size array
                        PipelineSizeArray sizeArray = new PipelineSizeArray(al, curves, brs);
                        prdDbg(sizeArray.ToString());
                        #endregion

                        #region Blockrefs and labels
                        BlockTableRecord btr;
                        if (bt.Has(pv.Name))
                        {
                            btr = bt[pv.Name].Go<BlockTableRecord>(tx, OpenMode.ForWrite);
                        }
                        else throw new System.Exception($"Block {pv.Name} is missing!");

                        ObjectIdCollection brefIds = btr.GetBlockReferenceIds(true, true);
                        if (brefIds.Count == 0) throw new System.Exception($"Block {pv.Name} does not have any references!");
                        Oid brefId = brefIds[0];
                        BlockReference bref = brefId.Go<BlockReference>(tx);

                        HashSet<ProfileProjectionLabel> ppls = localDb.HashSetOfType<ProfileProjectionLabel>(tx);
                        #endregion

                        foreach (ProfileProjectionLabel ppl in ppls)
                        {
                            Oid pId = ppl.FeatureId;
                            if (!pId.IsDerivedFrom<CogoPoint>()) continue;
                            CogoPoint cp = pId.Go<CogoPoint>(tx);
                            //if (ReadStringPropertyValue(tables, pId, "CrossingData", "Alignment")
                            //!= al.Name) continue;
                            if (psmDriCrossingData.ReadPropertyString(cp,
                                driCrossingData.Alignment) != al.Name) continue;

                            //Get original object from LER dwg
                            //string handle = ReadStringPropertyValue(tables, pId, "IdRecord", "Handle");
                            string handle = psmDriCrossingData.ReadPropertyString(
                                cp, driCrossingData.SourceEntityHandle);
                            //If returned handle string is empty
                            //For any reason, fx missing OD data
                            //Fall back to the name of the cogopoint
                            //else continue
                            if (handle.IsNoE())
                            {
                                string pName = cp.PointName;
                                string[] res = pName.Split('_');
                                handle = res[0];
                            }
                            if (handle.IsNoE()) continue;
                            long ln;
                            Handle hn;
                            try
                            {
                                ln = Convert.ToInt64(handle, 16);
                                hn = new Handle(ln);
                            }
                            catch (System.Exception ex)
                            {
                                prdDbg("Creation of handle failed!");
                                throw;
                            }
                            Oid originalId = Oid.Null;
                            try
                            { originalId = lerDb.GetObjectId(false, hn, 0); }
                            catch (System.Exception ex)
                            {
                                prdDbg($"Getting object by handle failed! {ex.Message}");
                                throw;
                            }
                            Entity originalEnt = originalId.Go<Entity>(lerTx);

                            //Determine type and distance
                            string distanceType = ReadStringParameterFromDataTable(originalEnt.Layer, dtKrydsninger, "Distance", 0);
                            string blockType = ReadStringParameterFromDataTable(originalEnt.Layer, dtKrydsninger, "Block", 0);
                            double distance = ReadDoubleParameterFromDataTable(distanceType, dtDistances, "Distance", 0);
                            int originalDia = psmDriCrossingData.ReadPropertyInt(
                                cp, driCrossingData.Diameter);
                            //int originalDia = ReadIntPropertyValue(tables, pId, "CrossingData", "Diameter");
                            double dia = Convert.ToDouble(originalDia) / 1000;
                            if (dia == 0) dia = 0.11;

                            //Determine kOd
                            double station = 0;
                            double elevation = 0;
                            if (!pv.FindStationAndElevationAtXY(ppl.LabelLocation.X, ppl.LabelLocation.Y, ref station, ref elevation))
                                throw new System.Exception($"Point {ppl.Handle} couldn't finde elevation and station!!!");

                            //Determine dn
                            double kappeOd = 0;
                            for (int i = 0; i < sizeArray.Length; i++)
                            {
                                if (station <= sizeArray[i].EndStation) { kappeOd = sizeArray[i].Kod / 1000; break; }
                            }

                            Circle circle = null;
                            switch (blockType)
                            {
                                case "Cirkel, Bund":
                                    {
                                        foreach (Oid oid in btr)
                                        {
                                            if (!oid.IsDerivedFrom<Circle>()) continue;
                                            Circle tempC = oid.Go<Circle>(tx);
                                            //prdDbg("C: " + tempC.Center.ToString());
                                            Point3d theoreticalLocation = new Point3d(ppl.LabelLocation.X, ppl.LabelLocation.Y + (dia / 2), 0);
                                            theoreticalLocation = theoreticalLocation.TransformBy(bref.BlockTransform.Inverse());
                                            //prdDbg("T: " + theoreticalLocation.ToString());
                                            //prdDbg($"dX: {tempC.Center.X - theoreticalLocation.X}, dY: {tempC.Center.Y - theoreticalLocation.Y}");
                                            if (tempC.Center.DistanceHorizontalTo(theoreticalLocation) < 0.0001)
                                            {
                                                //prdDbg("Found Cirkel, Bund!");
                                                circle = tempC;
                                                break;
                                            }
                                        }
                                    }
                                    break;
                                case "Cirkel, Top":
                                    {
                                        foreach (Oid oid in btr)
                                        {
                                            if (!oid.IsDerivedFrom<Circle>()) continue;
                                            Circle tempC = oid.Go<Circle>(tx);
                                            Point3d theoreticalLocation = new Point3d(ppl.LabelLocation.X, ppl.LabelLocation.Y - (dia / 2), 0);
                                            theoreticalLocation = theoreticalLocation.TransformBy(bref.BlockTransform.Inverse());
                                            if (tempC.Center.DistanceHorizontalTo(theoreticalLocation) < 0.0001)
                                            {
                                                //prdDbg("Found Cirkel, Top!");
                                                circle = tempC;
                                                break;
                                            }
                                        }
                                    }
                                    break;
                                case "EL 0.4kV":
                                    foreach (Oid oid in btr)
                                    {
                                        if (!oid.IsDerivedFrom<BlockReference>()) continue;
                                        BlockReference tempBref = oid.Go<BlockReference>(tx);
                                        //prdDbg("C: " + tempBref.Position.ToString());
                                        BlockTableRecord tempBtr = tempBref.BlockTableRecord.Go<BlockTableRecord>(tx);
                                        Point3d theoreticalLocation = new Point3d(ppl.LabelLocation.X, ppl.LabelLocation.Y, 0);
                                        theoreticalLocation = theoreticalLocation.TransformBy(bref.BlockTransform.Inverse());
                                        //prdDbg("T: " + theoreticalLocation.ToString());
                                        //prdDbg($"dX: {tempBref.Position.X - theoreticalLocation.X}, dY: {tempBref.Position.Y - theoreticalLocation.Y}");
                                        if (tempBref.Position.DistanceHorizontalTo(theoreticalLocation) < 0.0001)
                                        {
                                            //prdDbg("Found block!");
                                            Extents3d ext = tempBref.GeometricExtents;
                                            //prdDbg(ext.ToString());
                                            using (Polyline pl = new Polyline(4))
                                            {
                                                pl.AddVertexAt(0, new Point2d(ext.MinPoint.X, ext.MinPoint.Y), 0.0, 0.0, 0.0);
                                                pl.AddVertexAt(1, new Point2d(ext.MinPoint.X, ext.MaxPoint.Y), 0.0, 0.0, 0.0);
                                                pl.AddVertexAt(2, new Point2d(ext.MaxPoint.X, ext.MaxPoint.Y), 0.0, 0.0, 0.0);
                                                pl.AddVertexAt(3, new Point2d(ext.MaxPoint.X, ext.MinPoint.Y), 0.0, 0.0, 0.0);
                                                pl.Closed = true;
                                                pl.SetDatabaseDefaults();
                                                pl.ReverseCurve();

                                                using (DBObjectCollection col = pl.GetOffsetCurves(distance))
                                                {
                                                    foreach (var obj in col)
                                                    {
                                                        Entity ent = (Entity)obj;
                                                        ent.Layer = afstandsMarkeringLayerName;
                                                        btr.AppendEntity(ent);
                                                        tx.AddNewlyCreatedDBObject(ent, true);
                                                    }
                                                }
                                                using (DBObjectCollection col = pl.GetOffsetCurves(distance + kappeOd / 2))
                                                {
                                                    foreach (var obj in col)
                                                    {
                                                        Entity ent = (Entity)obj;
                                                        ent.Layer = afstandsMarkeringLayerName;
                                                        btr.AppendEntity(ent);
                                                        tx.AddNewlyCreatedDBObject(ent, true);
                                                    }
                                                }
                                            }
                                            break;
                                        }
                                    }
                                    break;
                                default:
                                    break;
                            }

                            if (circle != null)
                            {
                                using (DBObjectCollection col = circle.GetOffsetCurves(distance))
                                {
                                    foreach (var obj in col)
                                    {
                                        Entity ent = (Entity)obj;
                                        ent.Layer = afstandsMarkeringLayerName;
                                        btr.AppendEntity(ent);
                                        tx.AddNewlyCreatedDBObject(ent, true);
                                    }
                                }
                                using (DBObjectCollection col = circle.GetOffsetCurves(distance + kappeOd / 2))
                                {
                                    foreach (var obj in col)
                                    {
                                        Entity ent = (Entity)obj;
                                        ent.Layer = afstandsMarkeringLayerName;
                                        btr.AppendEntity(ent);
                                        tx.AddNewlyCreatedDBObject(ent, true);
                                    }
                                }
                            }
                        }

                        //Update block references
                        ObjectIdCollection brsToUpdate = btr.GetBlockReferenceIds(true, true);
                        foreach (Oid oid in brsToUpdate)
                        {
                            BlockReference br = oid.Go<BlockReference>(tx, OpenMode.ForWrite);
                            br.RecordGraphicsModified(true);
                        }
                    }
                }

                catch (System.Exception ex)
                {
                    fremTx.Abort();
                    fremTx.Dispose();
                    fremDb.Dispose();
                    lerTx.Abort();
                    lerTx.Dispose();
                    lerDb.Dispose();
                    tx.Abort();
                    editor.WriteMessage(ex.ToString());
                    return;
                }
                fremTx.Abort();
                fremTx.Dispose();
                fremDb.Dispose();
                lerTx.Abort();
                lerTx.Dispose();
                lerDb.Dispose();
                tx.Commit();
            }
        }

        [CommandMethod("UPDATESINGLEPROFILEVIEW")]
        public void updatesingleprofileview()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;

            PromptEntityOptions promptEntityOptions2 = new PromptEntityOptions(
                        "\n Select a ProfileView to update: ");
            promptEntityOptions2.SetRejectMessage("\n Not a ProfileView");
            promptEntityOptions2.AddAllowedClass(typeof(ProfileView), true);
            PromptEntityResult entity2 = editor.GetEntity(promptEntityOptions2);
            if (((PromptResult)entity2).Status != PromptStatus.OK) return;

            DataReferencesOptions dro = new DataReferencesOptions();
            if (dro.ProjectName.IsNoE() || dro.EtapeName.IsNoE())
                return;

            PropertySetManager.UpdatePropertySetDefinition(
                localDb, PSetDefs.DefinedSets.DriSourceReference);

            Point3d originalProfileViewLocation = default;
            double originalStStart;
            double originalStEnd;
            Extents3d bufferedOriginalBbox = default;
            Oid alId = Oid.Null;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Get Profile view and location
                    ProfileView pv = entity2.ObjectId.Go<ProfileView>(tx);

                    originalProfileViewLocation = pv.Location;
                    originalStStart = pv.StationStart;
                    originalStEnd = pv.StationEnd;
                    bufferedOriginalBbox = pv.GetBufferedXYGeometricExtents(5.0);
                    #endregion

                    #region Erase detailing block
                    var detailingBlock =
                                   localDb.GetBlockReferenceByName(pv.Name)
                                   .FirstOrDefault();

                    if (detailingBlock == default)
                        throw new System.Exception(
                            $"Detailing block {pv.Name} was not found!");

                    detailingBlock.CheckOrOpenForWrite();
                    detailingBlock.Erase(true);
                    #endregion

                    Alignment al = pv.AlignmentId.Go<Alignment>(tx);
                    alId = al.Id;
                    Polyline alPline = al.GetPolyline().Go<Polyline>(tx);

                    #region Erase PV CogoPoints
                    PropertySetManager psm = new PropertySetManager(
                        localDb, PSetDefs.DefinedSets.DriCrossingData);
                    PSetDefs.DriCrossingData psDef =
                        new PSetDefs.DriCrossingData();

                    //Delete all pv cogos
                    var allCogos = localDb.HashSetOfType<CogoPoint>(tx);
                    var alCogos = allCogos.Where(
                        x => psm.FilterPropetyString(
                            x, psDef.Alignment, al.Name));
                    foreach (var item in alCogos)
                    {
                        double cogoStation =
                            alPline.GetDistAtPoint(
                                alPline.GetClosestPointTo(item.Location, false));

                        if (cogoStation >= originalStStart && cogoStation <= originalStEnd)
                        { item.CheckOrOpenForWrite(); item.Erase(); }
                    }
                    #endregion

                    #region Erase detailing blocks
                    HashSet<BlockReference> pvDetailingBrs =
                        pv.GetDetailingBlocks(localDb, 5.0);
                    foreach (var item in pvDetailingBrs)
                    {
                        item.CheckOrOpenForWrite();
                        item.Erase();
                    }
                    #endregion

                    #region Erase PV
                    pv.CheckOrOpenForWrite();
                    pv.Erase(true);
                    #endregion

                    #region Erase polylines and points from prelim profile
                    HashSet<Polyline> prelim = localDb.HashSetOfType<Polyline>(tx);
                    foreach (var item in prelim)
                    {
                        if (!bufferedOriginalBbox.IsExtentsInsideXY(
                            item.GeometricExtents)) continue;

                        item.CheckOrOpenForWrite();
                        item.Erase(true);
                    }
                    HashSet<DBPoint> points = localDb.HashSetOfType<DBPoint>(tx);
                    foreach (var item in points)
                    {
                        if (!bufferedOriginalBbox.IsPointInsideXY(
                            item.Position)) continue;

                        item.CheckOrOpenForWrite();
                        item.Erase(true);
                    }
                    #endregion

                    #region Erase existing surface profile and create new one
                    ObjectIdCollection profs = al.GetProfileIds();
                    string surfaceProfName;
                    foreach (Oid item in profs)
                    {
                        Profile prof = item.Go<Profile>(tx);
                        if (!prof.Name.EndsWith("_surface_P")) continue;
                        surfaceProfName = prof.Name;
                        prof.CheckOrOpenForWrite();
                        prof.Erase(true);
                    }
                    #endregion

                    alPline.CheckOrOpenForWrite();
                    alPline.Erase();
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                }
                tx.Commit();
            }

            //docCol.MdiActiveDocument.TransactionManager.QueueForGraphicsFlush();

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    Alignment al = alId.Go<Alignment>(tx);
                    createsurfaceprofilesmethod(dro, new List<Alignment>() { al });
                    createprofileviewsmethod(originalProfileViewLocation);
                    createlerdatapssmethod(dro, new List<Alignment>() { al });
                    populateprofilesmethod(al.GetProfileViewIds().ToHashSet());
                    colorizealllerlayersmethod();
                    createprofilesmethod(dro, new HashSet<Alignment> { al });
                    createpointsatverticesmethod(bufferedOriginalBbox);
                    createdetailingpreliminarymethod(dro, null, new HashSet<Alignment> { al });
                    staggerlabelsallmethod(null, al.GetProfileViewIds().ToHashSet());
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                }
                tx.Commit();
            }

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    Alignment al = alId.Go<Alignment>(tx);
                    populatedistancesmethod(dro, al.GetProfileViewIds().ToHashSet());
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                }
                tx.Commit();
            }

            prdDbg("Update finished! Run AUDIT (Y) to clean up drawing!");
        }
    }
}