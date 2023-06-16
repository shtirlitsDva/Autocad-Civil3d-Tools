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
using System.Windows.Documents;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        /// <summary>
        /// Used to move 3d polylines of Novafos Gladsaxe data to elevation points
        /// </summary>
        [CommandMethod("TRNBY2DTO3D")]
        public void trnby2dto3d()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            //Process all lines and detect with nodes at both ends
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Load linework from local db
                    HashSet<Polyline3d> p3ds = localDb
                        .HashSetOfType<Polyline3d>(tx, true);
                    prdDbg($"\nNr. of local 3D polies: {p3ds.Count}");
                    HashSet<DBPoint> ps = localDb.HashSetOfType<DBPoint>(tx, true);
                    prdDbg($"\nNr. of local points: {ps.Count}");
                    #endregion

                    #region Poly3ds with knudepunkter at ends
                    foreach (Polyline3d p3d in p3ds)
                    {
                        var vertices = p3d.GetVertices(tx);

                        int endIdx = vertices.Length - 1;

                        double startElevation = 0.0;
                        double endElevation = 0.0;

                        Point3d spL = vertices[0].Position;
                        Point3d epL = vertices[endIdx].Position;

                        var spP = ps.Where(x => x.Position.HorizontalEqualz(spL, 0.001)).FirstOrDefault();
                        var epP = ps.Where(x => x.Position.HorizontalEqualz(epL, 0.001)).FirstOrDefault();

                        if (spP != default && epP != default)
                        {
                            startElevation = spP.Position.Z;
                            endElevation = epP.Position.Z;
                        }

                        
                        if (!startElevation.IsZero() && !endElevation.IsZero())
                        {
                            //Start match
                            vertices[0].CheckOrOpenForWrite();
                            vertices[0].Position = new Point3d(
                                vertices[0].Position.X, vertices[0].Position.Y, startElevation);

                            //End match
                            vertices[endIdx].CheckOrOpenForWrite();
                            vertices[endIdx].Position = new Point3d(
                                vertices[endIdx].Position.X, vertices[endIdx].Position.Y, endElevation);

                            //Trig
                            //Start elevation is higher, thus we must start from backwards
                            if (startElevation > endElevation)
                            {
                                double AB = p3d.GetHorizontalLength(tx);
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
                                double AB = p3d.GetHorizontalLength(tx);
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
                                editor.WriteMessage($"\nElevations are the same! " +
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

        [CommandMethod("TRNBYCHANGELAYERFOR2D")]
        public void trnbychangelayerfor2d()
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
                    HashSet<Polyline3d> p3ds = localDb.HashSetOfType<Polyline3d>(tx, true);
                    foreach (Polyline3d p3d in p3ds)
                    {
                        if (p3d.IsAtZeroElevation())
                        {
                            localDb.CheckOrCreateLayer(p3d.Layer + "-2D");
                            p3d.CheckOrOpenForWrite();
                            p3d.Layer = p3d.Layer + "-2D";
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
    }
}