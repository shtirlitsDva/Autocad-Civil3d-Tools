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
using NetTopologySuite.Geometries;

using static IntersectUtilities.Enums;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.Utils;
using static IntersectUtilities.PipeSchedule;

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
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using IntersectUtilities.LER2;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        [CommandMethod("LER2SORT2DFROM3D")]
        public void ler2sort2dfrom3d()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    HashSet<Polyline3d> pls = localDb.HashSetOfType<Polyline3d>(tx, true);
                    foreach (Polyline3d pl in pls)
                    {
                        PolylineVertex3d[] vertices = pl.GetVertices(tx);
                        HashSet<double> elevs = new HashSet<double>();
                        for (int i = 0; i < vertices.Length; i++) elevs.Add(vertices[i].Position.Z);

                        if (elevs.All(x => x.is2D()))
                        {
                            for (int i = 0; i < vertices.Length; i++)
                            {
                                PolylineVertex3d vert = vertices[i];
                                vert.CheckOrOpenForWrite();
                                vert.Position =
                                    new Point3d(
                                        vert.Position.X, vert.Position.Y, -99.0);
                            }

                            //Handle the layer name
                            string currentLayerName = pl.Layer;
                            if (currentLayerName.Contains("-3D"))
                            {
                                string newLayerName = currentLayerName.Replace("-3D", "-2D");
                                localDb.CheckOrCreateLayer(newLayerName);

                                pl.CheckOrOpenForWrite();
                                pl.Layer = newLayerName;
                            }
                        }

                        if (elevs.All(x => x.is3D()))
                        {
                            //Handle the layer name
                            string currentLayerName = pl.Layer;
                            if (!currentLayerName.Contains("-3D"))
                            {
                                string newLayerName = currentLayerName + "-3D";
                                localDb.CheckOrCreateLayer(newLayerName);

                                pl.CheckOrOpenForWrite();
                                pl.Layer = newLayerName;
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

        [CommandMethod("CHECK3DELEVATIONS")]
        public void check3delevations()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            #region Read Csv Data for Layers and Depth

            //Establish the pathnames to files
            //Files should be placed in a specific folder on desktop
            string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";

            System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
            #endregion

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                DataReferencesOptions dro = new DataReferencesOptions();
                string projectName = dro.ProjectName;
                string etapeName = dro.EtapeName;

                editor.WriteMessage("\n" + GetPathToDataFiles(projectName, etapeName, "Alignments"));
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
                    xRefSurfaceTx.Abort();
                    xRefSurfaceDB.Dispose();
                    prdDbg("No surface found in file! Aborting...");
                    throw;
                }

                if (surface == null)
                {
                    editor.WriteMessage("\nSurface could not be loaded from the xref!");
                    xRefSurfaceTx.Commit();
                    xRefSurfaceDB.Dispose();
                    throw new System.Exception("Surface is null!");
                }
                #endregion

                #region Load alignments from drawing
                HashSet<Alignment> alignments = null;
                //To be able to check local or external alignments following hack is implemented
                alignments = localDb.HashSetOfType<Alignment>(tx);

                // open the LER dwg database
                Database xRefAlsDB = new Database(false, true);

                xRefAlsDB.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Alignments"),
                    FileOpenMode.OpenForReadAndAllShare, false, null);
                Transaction xRefAlsTx = xRefAlsDB.TransactionManager.StartTransaction();

                if (alignments.Count < 1)
                {
                    alignments = xRefAlsDB.HashSetOfType<Alignment>(xRefAlsTx)
                        .OrderBy(x => x.Name).ToHashSet();
                }

                HashSet<Polyline3d> allLinework = localDb
                    .HashSetOfType<Polyline3d>(tx)
                    .Where(x => ReadStringParameterFromDataTable(x.Layer, dtKrydsninger, "Type", 0) == "3D")
                    .ToHashSet();
                editor.WriteMessage($"\nNr. of 3D polies: {allLinework.Count}");
                #endregion

                Plane plane = new Plane();

                try
                {
                    foreach (Alignment al in alignments)
                    {
                        //editor.WriteMessage($"\n++++++++ Indlæser alignment {al.Name}. ++++++++");
                        System.Windows.Forms.Application.DoEvents();

                        //Filtering is required because else I would be dealing with all layers
                        //We need to limit the processed layers only to the crossed ones.
                        HashSet<Polyline3d> filteredLinework = FilterForCrossingEntities(allLinework, al);
                        //editor.WriteMessage($"\nCrossing lines: {filteredLinework.Count}.");

                        int count = 0;
                        foreach (var ent in filteredLinework)
                        {
                            #region Create points
                            using (Point3dCollection p3dcol = new Point3dCollection())
                            {
                                al.IntersectWith(ent, 0, plane, p3dcol, new IntPtr(0), new IntPtr(0));

                                foreach (Point3d p3d in p3dcol)
                                {
                                    #region Assign elevation based on 3D conditions
                                    Point3d p3dInt = ent.GetClosestPointTo(p3d, Vector3d.ZAxis, false);

                                    count++;
                                    if (p3dInt.Z.IsZero(Tolerance.Global.EqualPoint))
                                    {
                                        editor.WriteMessage($"\nEntity {ent.Handle} returned {p3dInt.Z}" +
                                            $" elevation for a 3D layer.");
                                    }

                                    double surfaceElevation = surface.FindElevationAtXY(p3dInt.X, p3dInt.Y);
                                    if (p3dInt.Z >= surfaceElevation)
                                    {
                                        prdDbg($"Entity {ent.Handle} return intersection point above surface!\n" +
                                               $"Location: {p3dInt}, Surface E: {surfaceElevation}.");
                                    }

                                    if (p3dInt.Z < -98.0)
                                    {
                                        prdDbg($"Entity {ent.Handle} return returned {p3dInt.Z} " +
                                               $"elevation for a 3D layer!");
                                    }

                                    //prdDbg(
                                    //    $"Ler elev: {p3dInt.Z.ToString("0.##")}, " +
                                    //    $"Surface elev: {surfaceElevation.ToString("0.##")}");
                                    System.Windows.Forms.Application.DoEvents();
                                    #endregion
                                }
                            }
                            #endregion
                        }

                        editor.WriteMessage($"\nIntersections detected: {count}.");
                    }
                }
                catch (System.Exception e)
                {
                    xRefAlsTx.Abort();
                    xRefAlsTx.Dispose();
                    xRefAlsDB.Dispose();
                    xRefSurfaceTx.Abort();
                    xRefSurfaceTx.Dispose();
                    xRefSurfaceDB.Dispose();
                    tx.Abort();
                    editor.WriteMessage($"\n{e.ToString()}");
                    return;
                }

                xRefAlsTx.Abort();
                xRefAlsTx.Dispose();
                xRefAlsDB.Dispose();
                xRefSurfaceTx.Abort();
                xRefSurfaceTx.Dispose();
                xRefSurfaceDB.Dispose();
                tx.Commit();
            }
        }

        [CommandMethod("FLATTENPL3D")]
        public void flattenpl3d()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            while (true)
            {
                var id = Interaction.GetEntity("Select Plyline3d to flatten: ", typeof(Polyline3d));
                if (id == Oid.Null) return;

                using (Transaction tx = localDb.TransactionManager.StartTransaction())
                {
                    try
                    {
                        #region Polylines 3d
                        Polyline3d p3d = id.Go<Polyline3d>(tx, OpenMode.ForWrite);

                        PolylineVertex3d[] vertices = p3d.GetVertices(tx);

                        for (int i = 0; i < vertices.Length; i++)
                        {
                            vertices[i].CheckOrOpenForWrite();
                            vertices[i].Position = new Point3d(
                                vertices[i].Position.X, vertices[i].Position.Y, -99);
                        }
                        #endregion
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
        }

        [CommandMethod("LER2INTERPOLATEPL3DS")]
        public void ler2interpolatepl3ds()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            //Process all lines and detect with nodes at both ends
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Load linework from local db
                    HashSet<Polyline3d> localPlines3d =
                        localDb.HashSetOfType<Polyline3d>(tx, true);
                    prdDbg($"\nNr. of local 3D polies: {localPlines3d.Count}");
                    #endregion

                    #region Poly3ds with knudepunkter at ends
                    foreach (Polyline3d pline3d in localPlines3d)
                    {
                        var vertices = pline3d.GetVertices(tx);
                        int endIdx = vertices.Length - 1;

                        double startElevation = vertices[0].Position.Z;
                        double endElevation = vertices[endIdx].Position.Z; ;

                        if (startElevation.is3D() && endElevation.is3D())
                        {
                            //Trig
                            //Start elevation is higher, thus we must start from backwards
                            if (startElevation > endElevation)
                            {
                                double AB = pline3d.GetHorizontalLength(tx);
                                //editor.WriteMessage($"\nAB: {AB}.");
                                double AAmark = startElevation - endElevation;
                                //editor.WriteMessage($"\nAAmark: {AAmark}.");
                                double PB = 0;

                                for (int i = endIdx; i >= 0; i--)
                                {
                                    //We don't need to interpolate start and end points,
                                    //So skip them
                                    if (i != 0 && i != endIdx)
                                    {
                                        PB += vertices[i + 1].Position.DistanceHorizontalTo(
                                             vertices[i].Position);
                                        //editor.WriteMessage($"\nPB: {PB}.");
                                        double newElevation = endElevation + PB * (AAmark / AB);
                                        //editor.WriteMessage($"\nNew elevation: {newElevation}.");
                                        //Change the elevation
                                        vertices[i].CheckOrOpenForWrite();
                                        vertices[i].Position = new Point3d(
                                            vertices[i].Position.X, vertices[i].Position.Y, newElevation);
                                    }
                                }
                            }
                            else if (startElevation < endElevation)
                            {
                                double AB = pline3d.GetHorizontalLength(tx);
                                double AAmark = endElevation - startElevation;
                                double PB = 0;

                                for (int i = 0; i < endIdx + 1; i++)
                                {
                                    //We don't need to interpolate start and end points,
                                    //So skip them
                                    if (i != 0 && i != endIdx)
                                    {
                                        PB += vertices[i - 1].Position.DistanceHorizontalTo(
                                             vertices[i].Position);
                                        double newElevation = startElevation + PB * (AAmark / AB);
                                        //editor.WriteMessage($"\nNew elevation: {newElevation}.");
                                        //Change the elevation
                                        vertices[i].CheckOrOpenForWrite();
                                        vertices[i].Position = new Point3d(
                                            vertices[i].Position.X, vertices[i].Position.Y, newElevation);
                                    }
                                }
                            }
                            else
                            {
                                prdDbg($"\nElevations are the same! " +
                                    $"Start: {startElevation}, End: {endElevation}.");
                                for (int i = 0; i < endIdx + 1; i++)
                                {
                                    //We don't need to interpolate start and end points,
                                    //So skip them
                                    if (i != 0 && i != endIdx)
                                    {
                                        //Change the elevation
                                        vertices[i].CheckOrOpenForWrite();
                                        vertices[i].Position = new Point3d(
                                            vertices[i].Position.X, vertices[i].Position.Y, startElevation);
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
                    prdDbg(ex.ToString());
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("LER2INTERPOLATEPL3D")]
        public void ler2interpolatepl3d()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            #region Load linework from local db
            var id = Interaction.GetEntity("Select polyline3d: ", typeof(Polyline3d));
            if (id == Oid.Null) return;
            #endregion

            //Process all lines and detect with nodes at both ends
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    Polyline3d pline3d = id.Go<Polyline3d>(tx);

                    #region Poly3ds with knudepunkter at ends

                    var vertices = pline3d.GetVertices(tx);
                    int endIdx = vertices.Length - 1;

                    double startElevation = vertices[0].Position.Z;
                    double endElevation = vertices[endIdx].Position.Z; ;

                    if (startElevation.is3D() && endElevation.is3D())
                    {
                        //Trig
                        //Start elevation is higher, thus we must start from backwards
                        if (startElevation > endElevation)
                        {
                            double AB = pline3d.GetHorizontalLength(tx);
                            //editor.WriteMessage($"\nAB: {AB}.");
                            double AAmark = startElevation - endElevation;
                            //editor.WriteMessage($"\nAAmark: {AAmark}.");
                            double PB = 0;

                            for (int i = endIdx; i >= 0; i--)
                            {
                                //We don't need to interpolate start and end points,
                                //So skip them
                                if (i != 0 && i != endIdx)
                                {
                                    PB += vertices[i + 1].Position.DistanceHorizontalTo(
                                         vertices[i].Position);
                                    //editor.WriteMessage($"\nPB: {PB}.");
                                    double newElevation = endElevation + PB * (AAmark / AB);
                                    //editor.WriteMessage($"\nNew elevation: {newElevation}.");
                                    //Change the elevation
                                    vertices[i].CheckOrOpenForWrite();
                                    vertices[i].Position = new Point3d(
                                        vertices[i].Position.X, vertices[i].Position.Y, newElevation);
                                }
                            }
                        }
                        else if (startElevation < endElevation)
                        {
                            double AB = pline3d.GetHorizontalLength(tx);
                            double AAmark = endElevation - startElevation;
                            double PB = 0;

                            for (int i = 0; i < endIdx + 1; i++)
                            {
                                //We don't need to interpolate start and end points,
                                //So skip them
                                if (i != 0 && i != endIdx)
                                {
                                    PB += vertices[i - 1].Position.DistanceHorizontalTo(
                                         vertices[i].Position);
                                    double newElevation = startElevation + PB * (AAmark / AB);
                                    //editor.WriteMessage($"\nNew elevation: {newElevation}.");
                                    //Change the elevation
                                    vertices[i].CheckOrOpenForWrite();
                                    vertices[i].Position = new Point3d(
                                        vertices[i].Position.X, vertices[i].Position.Y, newElevation);
                                }
                            }
                        }
                        else
                        {
                            prdDbg($"\nElevations are the same! " +
                                $"Start: {startElevation}, End: {endElevation}.");
                            for (int i = 0; i < endIdx + 1; i++)
                            {
                                //We don't need to interpolate start and end points,
                                //So skip them
                                if (i != 0 && i != endIdx)
                                {
                                    //Change the elevation
                                    vertices[i].CheckOrOpenForWrite();
                                    vertices[i].Position = new Point3d(
                                        vertices[i].Position.X, vertices[i].Position.Y, startElevation);
                                }
                            }
                        }
                    }
                    #endregion
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

        [CommandMethod("LER2DETECTCOINCIDENTENDS")]
        public void ler2detectcoincidentends()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            //Process all lines and detect ends, that are coincident with another vertex
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Load linework from local db
                    HashSet<Polyline3d> localPlines3d =
                        localDb.HashSetOfType<Polyline3d>(tx, true);
                    prdDbg($"\nNr. of local 3D polies: {localPlines3d.Count}");

                    //Gather all endpoints
                    var locs3d = new HashSet<(Point3d loc, Polyline3d host, int idx)>();
                    var locs2d = new HashSet<(Point3d loc, Polyline3d host, int idx)>();

                    foreach (Polyline3d pline3d in localPlines3d)
                    {
                        var vertices = pline3d.GetVertices(tx);
                        int endIdx = vertices.Length - 1;

                        double startElevation = vertices[0].Position.Z;
                        double endElevation = vertices[endIdx].Position.Z;

                        if (startElevation.is2D()) locs2d.Add((vertices[0].Position, pline3d, 0));
                        else locs3d.Add((vertices[0].Position, pline3d, 0));

                        if (endElevation.is2D()) locs2d.Add((vertices[endIdx].Position, pline3d, endIdx));
                        else locs3d.Add((vertices[endIdx].Position, pline3d, endIdx));
                    }

                    //Analyze points
                    foreach (var loc2d in locs2d)
                    {
                        //Detect the coincident 3d location
                        var loc3d = locs3d.Where(
                            x => loc2d.loc.HorizontalEqualz(x.loc, 0.001)).FirstOrDefault();

                        if (loc3d == default) continue;

                        var vert = loc2d.host.GetVertices(tx)[loc2d.idx];
                        vert.CheckOrOpenForWrite();
                        vert.Position = loc3d.loc;
                    }
                    #endregion
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

        [CommandMethod("LISTLAYERSOFSELECTION", CommandFlags.UsePickSet)]
        public void listlayersofselection()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    HashSet<string> layers = new HashSet<string>();
                    var oids = Interaction.GetPickSet();
                    foreach (var oid in oids)
                    {
                        Entity ent = oid.Go<Entity>(tx);
                        layers.Add(ent.Layer);
                    }
                    prdDbg(string.Join("\n", layers));
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

        [CommandMethod("LER2ANALYZEOVERLAPS")]
        public void ler2analyzeoverlaps()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            Tolerance tolerance = new Tolerance(1e-4, 2.54 * 1e-4);

            prdDbg("Remember to run LER2ANALYZEDUPLICATES first!");

            //Process all lines and detect with nodes at both ends
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Load linework from local db
                    HashSet<Polyline3d> localPlines3d =
                        localDb.HashSetOfType<Polyline3d>(tx, true);
                    prdDbg($"\nNr. of local 3D polies: {localPlines3d.Count}");
                    HashSet<MyPl3d> myPl3Ds = localPlines3d.Select(
                        x => new MyPl3d(x, tolerance)).ToHashSet();
                    #endregion

                    HashSet<MyPl3d> nonOverlapping = new HashSet<MyPl3d>(new MyPl3dHandleComparer());
                    HashSet<HashSet<MyPl3d>> overlappingGroups = new HashSet<HashSet<MyPl3d>>();
                    HashSet<Handle> categorized = new HashSet<Handle>();

                    var layerGroups = myPl3Ds.GroupBy(x => x.Layer);

                    foreach (var layerGrouping in layerGroups)
                    {
                        prdDbg($"Processing Polylines3d on layer: {layerGrouping.Key}.");
                        System.Windows.Forms.Application.DoEvents();
                        var layerGroup = layerGrouping.ToHashSet();
                        foreach (MyPl3d pline in layerGroup)
                        {
                            // Check if the polyline is already categorized
                            if (categorized.Contains(pline.Handle))
                                continue;

                            HashSet<MyPl3d> overlaps = GetOverlappingPolylines(pline, layerGroup, tolerance)
                                .Where(x => !categorized.Contains(x.Handle))
                                .ToHashSet();

                            if (overlaps.Count == 0)
                            {
                                nonOverlapping.Add(pline);
                                categorized.Add(pline.Handle);
                            }
                            else
                            {
                                HashSet<MyPl3d> newGroup = new HashSet<MyPl3d>(new MyPl3dHandleComparer()) { pline };
                                Queue<MyPl3d> toProcess = new Queue<MyPl3d>(overlaps);

                                // Iterate through the new group and add any polyline that overlaps 
                                // with a member of the group but isn't already in the group
                                while (toProcess.Count > 0)
                                {
                                    var current = toProcess.Dequeue();
                                    if (!categorized.Contains(current.Handle))
                                    {
                                        newGroup.Add(current);
                                        categorized.Add(current.Handle);

                                        var externalOverlaps = GetOverlappingPolylines(
                                            current,
                                            layerGroup.Where(x => !categorized.Contains(x.Handle)).ToHashSet(),
                                            tolerance).ToHashSet();

                                        foreach (var overlap in externalOverlaps)
                                            toProcess.Enqueue(overlap);
                                    }
                                }

                                overlappingGroups.Add(newGroup);
                            }
                        }
                    }

                    //Debug and test
                    int groupCount = 0;
                    HashSet<HashSet<SerializablePolyline3d>> serializableGroups = new HashSet<HashSet<SerializablePolyline3d>>();
                    foreach (var group in overlappingGroups)
                    {
                        HashSet<SerializablePolyline3d> serializableGroup = new HashSet<SerializablePolyline3d>();
                        groupCount++;
                        foreach (var item in group)
                            serializableGroup.Add(new SerializablePolyline3d(item.Handle.Go<Polyline3d>(localDb), groupCount));
                        serializableGroups.Add(serializableGroup);
                    }

                    prdDbg($"Number of overlapping groups: {overlappingGroups.Count}");
                    List<int> counts = overlappingGroups.Select(x => x.Count()).Distinct().OrderBy(x => x).ToList();

                    foreach (int count in counts)
                    {
                        prdDbg("Count: " + count.ToString() + " -> Antal i grupppen: "
                            + overlappingGroups.Where(x => x.Count() == count).Count().ToString());

                        if (count != 2)
                        {
                            foreach (var item in overlappingGroups.Where(x => x.Count() == count))
                            {
                                foreach (var pline in item)
                                {
                                    prdDbg(pline.Handle.ToString());
                                }

                                prdDbg("");
                            }
                        }
                    }

                    var encoderSettings = new TextEncoderSettings();
                    encoderSettings.AllowRange(UnicodeRanges.All);

                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = false,
                        Encoder = JavaScriptEncoder.Create(encoderSettings)
                    };

                    string jsonString = JsonSerializer.Serialize(serializableGroups, options);
                    string path = Path.GetDirectoryName(localDb.Filename);
                    OutputWriter(path + "\\OverlapsAnalysis.json", jsonString, true, false);

                    var groups = serializableGroups.GroupBy(x => x.First().Properties["Ler2Type"]);

                    foreach (var group in groups)
                    {
                        File.WriteAllText($"{path}\\OverlapsReport-{group.Key}.html",
                            HtmlGenerator.GenerateHtmlReport(group.ToHashSet()));
                    }

                    bool ArePolylines3dOverlapping(MyPl3d pl1, MyPl3d pl2, Tolerance tol)
                    {
                        // 1. BBOX and layer checks
                        if (pl1.Handle == pl2.Handle) return false;
                        if (!pl1.GeometricExtents.Intersects(pl2.GeometricExtents)) return false;
                        if (pl1.Layer != pl2.Layer) return false;

                        // 2. Vertex Overlap Check
                        //prdDbg($"pl1: {pl1.Handle} pl2: {pl2.Handle}");
                        var overlapType1to2 = pl1.GetOverlapType(pl2); //prdDbg(overlapType1to2);
                        if (overlapType1to2 == MyPl3d.OverlapType.None) return false;
                        var overlapType2to1 = pl2.GetOverlapType(pl1); //prdDbg(overlapType2to1);
                        if (overlapType1to2 == MyPl3d.OverlapType.Full &&
                            overlapType1to2 == overlapType2to1) return true;
                        if (overlapType1to2 == MyPl3d.OverlapType.Partial &&
                            overlapType1to2 == overlapType2to1)
                        {
                            //This implementation assumes that the data is to some degree favorable
                            //meaning that the partial overlaps in majority are co-linear
                            //We do not anticipate highly complex overlaps with coincident vertices
                            //but non-co-linear segments

                            //One case found in the wild
                            //Polylines touching both ends but not overlapping

                            if ((pl1.StartPoint.IsEqualTo(pl2.StartPoint, tol) ||
                                pl1.StartPoint.IsEqualTo(pl2.EndPoint, tol)) &&
                                (pl1.EndPoint.IsEqualTo(pl2.EndPoint, tol) ||
                                pl1.EndPoint.IsEqualTo(pl2.StartPoint, tol)))
                            {
                                var der1 = pl1.StartVector;
                                var der2 = pl2.StartVector;
                                if (!der1.IsParallelTo(der2, tol)) return false;
                                der1 = pl1.EndVector;
                                der2 = pl2.EndVector;
                                if (!der1.IsParallelTo(der2, tol)) return false;
                            }

                            //Now we need to filter cases where the overlap is only
                            //One point at the end or start of the polyline

                            if (pl1.StartPoint.IsEqualTo(pl2.StartPoint, tol) ||
                                pl1.StartPoint.IsEqualTo(pl2.EndPoint, tol) ||
                                pl1.EndPoint.IsEqualTo(pl2.StartPoint, tol) ||
                                pl1.EndPoint.IsEqualTo(pl2.EndPoint, tol))
                            {
                                if (pl1.Vertices.Where(x => x.IsOn(pl2)).Count() == 1 &&
                                    pl2.Vertices.Where(x => x.IsOn(pl1)).Count() == 1) return false;
                            }

                            //Now we need to filter cases where the other polyline
                            //Is touching this polyline with one vertex

                            if (pl1.StartPoint.IsOn(pl2) || pl1.EndPoint.IsOn(pl2))
                            {
                                //Firste case where pl1s' point is between pl2s' vertices
                                if (pl2.Vertices.Where(x => x.IsOn(pl1)).Count() == 0) return false;

                                //Second case where pl1s' point is on one of pl2s' vertices, but not start or end
                                if (pl2.Vertices.Where(x => x.IsOn(pl1)).Count() == 1)
                                {
                                    if (!pl2.StartPoint.IsOn(pl1) && !pl2.EndPoint.IsOn(pl1)) return false;
                                }
                            }

                            if (pl2.StartPoint.IsOn(pl1) || pl2.EndPoint.IsOn(pl1))
                            {
                                if (pl1.Vertices.Where(x => x.IsOn(pl2)).Count() == 0) return false;

                                //Second case where pl1s' point is on one of pl2s' vertices, but not start or end
                                if (pl1.Vertices.Where(x => x.IsOn(pl2)).Count() == 1)
                                {
                                    if (!pl1.StartPoint.IsOn(pl2) && !pl1.EndPoint.IsOn(pl2)) return false;
                                }
                            }
                        }

                        return true;
                    }

                    HashSet<MyPl3d> GetOverlappingPolylines(MyPl3d pl, HashSet<MyPl3d> pls, Tolerance tol)
                    {
                        return pls.Where(x => ArePolylines3dOverlapping(pl, x, tol)).ToHashSet();
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

        [CommandMethod("LER2MERGEOVERLAPS")]
        public void ler2mergeoverlaps()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            Tolerance tolerance = new Tolerance(1e-3, 2.54 * 1e-3);

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    string path = Path.GetDirectoryName(localDb.Filename);
                    string jsonString = File.ReadAllText(path + "\\OverlapsAnalysis.json");
                    HashSet<HashSet<SerializablePolyline3d>> groups =
                        (HashSet<HashSet<SerializablePolyline3d>>)JsonSerializer.Deserialize(
                            jsonString, typeof(HashSet<HashSet<SerializablePolyline3d>>));

                    StringBuilder log = new StringBuilder();
                    var ler2groups = groups.GroupBy(x => x.First().Properties["Ler2Type"].ToString());

                    Ler2MergeValidator validator = new Ler2MergeValidator();
                    string rulesPath = @"X:\AutoCAD DRI - 01 Civil 3D\LER2.0\01 MergeRules\";
                    foreach (var group in ler2groups)
                        validator.LoadRule(group.Key, rulesPath);

                    int totalNewCount = 0;
                    int totalDeletedCount = 0;
                    foreach (var ler2TypeGroup in ler2groups)
                    {
                        log.AppendLine("----------------------");
                        log.AppendLine(ler2TypeGroup.Key);
                        log.AppendLine("----------------------");
                        prdDbg(ler2TypeGroup.Key);

                        var toMerge = validator.Validate(ler2TypeGroup.ToHashSet(), log);

                        int newCount = 0;
                        int deletedCount = 0;
                        foreach (var group in toMerge)
                        {
                            //Merge the pl3ds
                            var mypl3ds = group.Select(x => new MyPl3d(x.GetPolyline3d(), tolerance)).ToList();

                            MyPl3d seed = mypl3ds.First();
                            var newPoints = seed.Merge(mypl3ds.Skip(1));

                            //Continue merging here!
                            Polyline3d seedPl3d = group.First().GetPolyline3d();
                            Polyline3d newPl3d = new Polyline3d(Poly3dType.SimplePoly, newPoints, false);

                            newPl3d.AddEntityToDbModelSpace(localDb);
                            newPl3d.Layer = seedPl3d.Layer;

                            PropertySetManager.CopyAllProperties(seedPl3d, newPl3d);
                            newCount++;
                            //Delete the old pl3ds
                            foreach (var item in group)
                            {
                                var pl3d = item.GetPolyline3d();
                                pl3d.UpgradeOpen();
                                pl3d.Erase();
                                deletedCount++;
                            }
                        }
                        log.AppendLine($"New Polyline3d created: {newCount}.");
                        log.AppendLine($"Old Polyline3d deleted: {deletedCount}.");
                        totalNewCount += newCount;
                        totalDeletedCount += deletedCount;
                    }

                    log.AppendLine();
                    log.AppendLine("----------------------");
                    log.AppendLine("Summary");
                    log.AppendLine($"Total New Polyline3d created: {totalNewCount}.");
                    log.AppendLine($"Total Old Polyline3d deleted: {totalDeletedCount}.");
                    prdDbg($"Total New Polyline3d created: {totalNewCount}.");
                    prdDbg($"Total Old Polyline3d deleted: {totalDeletedCount}.");

                    File.WriteAllText(path +
                        $"\\MergeOverlaps_{DateTime.Now.ToString("yyyy.MM.dd-HH.mm.ss")}.log",
                        log.ToString());
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

        [CommandMethod("LER2ANALYZEDUPLICATES")]
        public void ler2analyzeduplicates()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Tolerance tolerance = new Tolerance(1e-6, 2.54 * 1e-6);

            //Process all lines and detect with nodes at both ends
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Load linework from local db
                    HashSet<Polyline3d> localPlines3d =
                        localDb.HashSetOfType<Polyline3d>(tx, true);
                    prdDbg($"\nNr. of local 3D polies: {localPlines3d.Count}");
                    #endregion

                    HashSet<HashSet<Polyline3d>> duplicateGroups = new HashSet<HashSet<Polyline3d>>();

                    var layerGroups = localPlines3d.GroupBy(p => p.Layer);

                    foreach (var layerGrouping in layerGroups)
                    {
                        prdDbg($"Processing Polylines3d on layer: {layerGrouping.Key}.");
                        System.Windows.Forms.Application.DoEvents();
                        var layerGroup = layerGrouping.ToHashSet();
                        foreach (Polyline3d pline in layerGroup)
                        {
                            // Check if the polyline is already categorized
                            if (IsPolylineCategorized(pline, duplicateGroups))
                                continue;

                            var overlaps = GetDuplicatePolylines(pline, layerGroup, tolerance)
                                .Where(x => !IsPolylineCategorized(x, duplicateGroups))
                                .ToList();

                            if (overlaps.Count == 0)
                            {
                                continue;
                            }
                            else
                            {
                                HashSet<Polyline3d> newGroup = new HashSet<Polyline3d>(new Polyline3dHandleComparer()) { pline };
                                HashSet<Handle> processed = new HashSet<Handle>();
                                Queue<Polyline3d> toProcess = new Queue<Polyline3d>(overlaps);

                                // Iterate through the new group and add any polyline that overlaps 
                                // with a member of the group but isn't already in the group
                                while (toProcess.Count > 0)
                                {
                                    var current = toProcess.Dequeue();
                                    if (!processed.Contains(current.Handle))
                                    {
                                        newGroup.Add(current);
                                        processed.Add(current.Handle);

                                        var externalOverlaps = GetDuplicatePolylines(current, layerGroup, tolerance)
                                            .ExceptWhere(x =>
                                            newGroup.Any(y => x.Handle == y.Handle) ||
                                            processed.Contains(x.Handle)).ToList();

                                        foreach (var overlap in externalOverlaps)
                                            toProcess.Enqueue(overlap);
                                    }
                                }

                                duplicateGroups.Add(newGroup);
                            }
                        }
                    }

                    //Debug and test

                    HashSet<HashSet<SerializablePolyline3d>> serializableGroups = new HashSet<HashSet<SerializablePolyline3d>>();
                    int groupCount = 0;
                    foreach (var group in duplicateGroups)
                    {
                        HashSet<SerializablePolyline3d> serializableGroup = new HashSet<SerializablePolyline3d>();
                        groupCount++;
                        foreach (var item in group)
                            serializableGroup.Add(new SerializablePolyline3d(item, groupCount));
                        serializableGroups.Add(serializableGroup);
                    }

                    prdDbg($"Number of duplicate groups: {duplicateGroups.Count}");
                    List<int> counts = duplicateGroups.Select(x => x.Count()).Distinct().OrderBy(x => x).ToList();

                    foreach (int count in counts)
                    {
                        prdDbg("Count: " + count.ToString() + " -> Antal i grupppen: "
                            + duplicateGroups.Where(x => x.Count() == count).Count().ToString());

                        if (count != 2)
                        {
                            foreach (var item in duplicateGroups.Where(x => x.Count() == count))
                            {
                                foreach (var pline in item)
                                {
                                    prdDbg(pline.Handle.ToString());
                                }

                                prdDbg("");
                            }
                        }
                    }

                    var encoderSettings = new TextEncoderSettings();
                    encoderSettings.AllowRange(UnicodeRanges.All);

                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = false,
                        Encoder = JavaScriptEncoder.Create(encoderSettings)
                    };

                    string path = Path.GetDirectoryName(localDb.Filename);

                    string jsonString = JsonSerializer.Serialize(serializableGroups, options);
                    OutputWriter(path + "\\DuplicatesAnalysis.json", jsonString, true, false);

                    foreach (var item in serializableGroups.Select(x => x.First().Properties.First().Key).Distinct())
                    {
                        prdDbg(item);
                    }

                    var groups = serializableGroups.GroupBy(x => x.First().Properties["Ler2Type"]);

                    foreach (var group in groups)
                    {
                        File.WriteAllText($"{path}\\DuplicatesReport-{group.Key}.html",
                            HtmlGenerator.GenerateHtmlReport(group.ToHashSet()));
                    }

                    bool ArePolylines3dDuplicate(Polyline3d pl1, Polyline3d pl2, Tolerance tol)
                    {
                        // 1. BBOX and layer checks
                        if (pl1.Handle == pl2.Handle) return false;
                        if (!pl1.GeometricExtents.Intersects2D(pl2.GeometricExtents)) return false;
                        if (pl1.Layer != pl2.Layer) return false;

                        var vs1 = pl1.GetVertices(tx);
                        var vs2 = pl2.GetVertices(tx);

                        if (vs1.Length != vs2.Length) return false;

                        return !vs1.Where((v, i) => !v.IsEqualTo(vs2[i], tol)).Any();
                    }

                    bool IsPolylineCategorized(Polyline3d pl, HashSet<HashSet<Polyline3d>> dG)
                    {
                        if (dG.Any(x => x.Any(y => y.Handle == pl.Handle))) return true;
                        return false;
                    }

                    List<Polyline3d> GetDuplicatePolylines(Polyline3d pl, HashSet<Polyline3d> pls, Tolerance tol) =>
                        pls.Where(x => ArePolylines3dDuplicate(pl, x, tol)).ToList();

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

        [CommandMethod("LER2REMOVEDUPLICATES")]
        public void ler2removeduplicates()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Tolerance tolerance = new Tolerance(1e-6, 2.54 * 1e-6);

            //Process all lines and detect with nodes at both ends
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Load linework from local db
                    HashSet<Polyline3d> localPlines3d =
                        localDb.HashSetOfType<Polyline3d>(tx, true);
                    prdDbg($"\nNr. of local 3D polies: {localPlines3d.Count}");
                    #endregion

                    HashSet<HashSet<Polyline3d>> duplicateGroups = new HashSet<HashSet<Polyline3d>>();

                    var layerGroups = localPlines3d.GroupBy(p => p.Layer);

                    foreach (var layerGrouping in layerGroups)
                    {
                        prdDbg($"Processing Polylines3d on layer: {layerGrouping.Key}.");
                        System.Windows.Forms.Application.DoEvents();
                        var layerGroup = layerGrouping.ToHashSet();
                        foreach (Polyline3d pline in layerGroup)
                        {
                            // Check if the polyline is already categorized
                            if (IsPolylineCategorized(pline, duplicateGroups))
                                continue;

                            var overlaps = GetDuplicatePolylines(pline, layerGroup, tolerance)
                                .Where(x => !IsPolylineCategorized(x, duplicateGroups))
                                .ToList();

                            if (overlaps.Count == 0)
                            {
                                continue;
                            }
                            else
                            {
                                HashSet<Polyline3d> newGroup = new HashSet<Polyline3d>(new Polyline3dHandleComparer()) { pline };
                                HashSet<Handle> processed = new HashSet<Handle>();
                                Queue<Polyline3d> toProcess = new Queue<Polyline3d>(overlaps);

                                // Iterate through the new group and add any polyline that overlaps 
                                // with a member of the group but isn't already in the group
                                while (toProcess.Count > 0)
                                {
                                    var current = toProcess.Dequeue();
                                    if (!processed.Contains(current.Handle))
                                    {
                                        newGroup.Add(current);
                                        processed.Add(current.Handle);

                                        var externalOverlaps = GetDuplicatePolylines(current, layerGroup, tolerance)
                                            .ExceptWhere(x =>
                                            newGroup.Any(y => x.Handle == y.Handle) ||
                                            processed.Contains(x.Handle)).ToList();

                                        foreach (var overlap in externalOverlaps)
                                            toProcess.Enqueue(overlap);
                                    }
                                }

                                duplicateGroups.Add(newGroup);
                            }
                        }
                    }

                    //Debug and test

                    List<List<SerializablePolyline3d>> serializableGroups = new List<List<SerializablePolyline3d>>();
                    int groupCount = 0;
                    foreach (var group in duplicateGroups)
                    {
                        List<SerializablePolyline3d> serializableGroup = new List<SerializablePolyline3d>();
                        groupCount++;
                        foreach (var item in group)
                            serializableGroup.Add(new SerializablePolyline3d(item, groupCount));
                        serializableGroups.Add(serializableGroup);
                    }

                    prdDbg($"Number of duplicate groups: {duplicateGroups.Count}");
                    List<int> counts = duplicateGroups.Select(x => x.Count()).Distinct().OrderBy(x => x).ToList();

                    foreach (int count in counts)
                    {
                        prdDbg("Count: " + count.ToString() + " -> Antal i grupppen: "
                            + duplicateGroups.Where(x => x.Count() == count).Count().ToString());
                    }

                    //This keeps track of the group numbers that have been processed
                    //To avoid processing them again and accidentally trying to delete
                    //entities that have already been deleted
                    HashSet<int> processedGroupNumbers = new HashSet<int>();

                    //Delete duplicate entities
                    int nrOfDeletedEntities = 0;
                    foreach (var group in serializableGroups)
                    {
                        var referenceObject = group.First().Properties;
                        if (!group.All(
                            x => PropertiesAreEqual(referenceObject,
                            x.Properties,
                            new List<string> { "GmlId", "LerId", "EtableringsTidspunkt", "RegistreringFra" }))) continue;

                        processedGroupNumbers.Add(group.First().GroupNumber);
                        foreach (var item in group.Skip(1))
                        {
                            var pl = item.GetPolyline3d();
                            pl.UpgradeOpen();
                            pl.Erase(true);
                            nrOfDeletedEntities++;
                        }
                    }

                    //Case where there everything is matching except for UdvendigDiameter
                    //Specific Case: Afloebsledning and others
                    foreach (var group in serializableGroups)
                    {
                        if (processedGroupNumbers.Contains(group.First().GroupNumber)) continue;

                        var referenceObject = group.First().Properties;
                        if (!group.All(
                            x => PropertiesAreEqual(referenceObject,
                            x.Properties,
                            new List<string> {
                                "GmlId", "LerId", "EtableringsTidspunkt",
                                "RegistreringFra", "UdvendigDiameter", "UdvendigDiameterUnits" }))) continue;

                        processedGroupNumbers.Add(group.First().GroupNumber);
                        Handle maxDnHandle = group.MaxBy(
                            x => Convert.ToInt32(x.Properties["UdvendigDiameter"]))
                            .First().GetPolyline3d().Handle;
                        foreach (var item in group)
                        {
                            if (item.GetPolyline3d().Handle != maxDnHandle)
                            {
                                var pl = item.GetPolyline3d();
                                pl.UpgradeOpen();
                                pl.Erase(true);
                                nrOfDeletedEntities++;
                            }
                        }
                    }

                    //Report on how many entities were deleted
                    prdDbg($"Number of deleted polylines3d: {nrOfDeletedEntities}");

                    bool ArePolylines3dDuplicate(Polyline3d pl1, Polyline3d pl2, Tolerance tol)
                    {
                        // 1. BBOX and layer checks
                        if (pl1.Handle == pl2.Handle) return false;
                        if (!pl1.GeometricExtents.Intersects2D(pl2.GeometricExtents)) return false;
                        if (pl1.Layer != pl2.Layer) return false;

                        var vs1 = pl1.GetVertices(tx);
                        var vs2 = pl2.GetVertices(tx);

                        if (vs1.Length != vs2.Length) return false;

                        return !vs1.Where((v, i) => !v.IsEqualTo(vs2[i], tol)).Any();
                    }

                    bool IsPolylineCategorized(Polyline3d pl, HashSet<HashSet<Polyline3d>> dG)
                    {
                        if (dG.Any(x => x.Any(y => y.Handle == pl.Handle))) return true;
                        return false;
                    }

                    List<Polyline3d> GetDuplicatePolylines(Polyline3d pl, HashSet<Polyline3d> pls, Tolerance tol) =>
                        pls.Where(x => ArePolylines3dDuplicate(pl, x, tol)).ToList();

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

        private class Polyline3dHandleComparer : IEqualityComparer<Polyline3d>
        {
            public bool Equals(Polyline3d x, Polyline3d y)
            {
                if (x == null || y == null)
                    return false;

                return x.Handle == y.Handle;
            }

            public int GetHashCode(Polyline3d obj)
            {
                if (obj == null)
                    return 0;

                return obj.Handle.GetHashCode();
            }
        }

        private static bool PropertiesAreEqual(
                        Dictionary<string, object> d1,
                        Dictionary<string, object> d2,
                        List<string> propertiesToSkip = null)
        {
            if (propertiesToSkip == null) propertiesToSkip = new List<string>();

            foreach (var key in d1.Keys)
            {
                if (propertiesToSkip.Contains(key)) continue;
                if (!d2.ContainsKey(key)) return false;
                if (!d1[key].Equals(d2[key])) return false;
            }
            return true;
        }
    }
}