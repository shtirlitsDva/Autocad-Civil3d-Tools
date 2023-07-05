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
                                string newLayerName = currentLayerName.Replace("-3D", "");
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
                        prdDbg(ex.ToString());
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
    }
}