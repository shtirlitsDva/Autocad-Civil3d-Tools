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
        //Used to move 3d polylines of gas to elevation points
        [CommandMethod("EKSFJVPLINE3DFROMPOINTS")]
        public void eksfjvpline3dfrompoints()
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
                    #region Load linework from local db
                    HashSet<Polyline3d> localPlines3d = localDb.HashSetOfType<Polyline3d>(tx);
                    editor.WriteMessage($"\nNr. of local 3D polies: {localPlines3d.Count}");
                    #endregion

                    //Points to intersect
                    HashSet<DBPoint> points = new HashSet<DBPoint>(
                        localDb.ListOfType<DBPoint>(tx),
                        new PointDBHorizontalComparer());

                    foreach ( DBPoint p in points )
                    {
                        var elevation = PropertySetManager.ReadNonDefinedPropertySetDouble(
                                    p, "komponenter", "NodeLevel");

                        if (elevation.IsZero()) continue;

                        p.UpgradeOpen();
                        p.Position = new Point3d(
                            p.Position.X, p.Position.Y, elevation);
                        p.DowngradeOpen();
                    }

                    editor.WriteMessage($"\nNr. of local points: {points.Count}");
                    editor.WriteMessage($"\nTotal number of combinations: " +
                        $"{points.Count * localPlines3d.Count}");

                    foreach (Polyline3d pline3d in localPlines3d)
                    {
                        var vertices = pline3d.GetVertices(tx);

                        for (int i = 0; i < vertices.Length; i++)
                        {
                            DBPoint match = points.Where(x => x.Position.HorizontalEqualz(
                                vertices[i].Position, 0.05)).FirstOrDefault();
                            if (match != null)
                            {
                                var elevation = PropertySetManager.ReadNonDefinedPropertySetDouble(
                                    match, "komponenter", "NodeLevel");

                                if (elevation.IsZero()) continue;

                                vertices[i].UpgradeOpen();
                                vertices[i].Position = new Point3d(
                                    vertices[i].Position.X, vertices[i].Position.Y, elevation);
                                vertices[i].DowngradeOpen();
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("EKSFJVINTERPOLATION")]
        public void eksfjvinterpolation()
        {

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Polylines 3d
                    /////////////////////////////////
                    bool atZero(double value) => value > -0.0001 && value < 0.0001;
                    ////////////////////////////////
                    HashSet<Polyline3d> plines3d = localDb.HashSetOfType<Polyline3d>(tx, true);
                    foreach (Polyline3d p3d in plines3d)
                    {
                        PolylineVertex3d[] vertices = p3d.GetVertices(tx);
                        int endIdx = vertices.Length - 1;

                        #region Skip completely zeroed p3ds
                        //Test if p3d is completely at zero and skip if it is
                        if (p3d.IsAtZeroElevation()) continue;
                        #endregion

                        //Take care of first and last vertices first
                        #region First vertice
                        {
                            PolylineVertex3d vert = vertices[0];

                            //If vertice is already non-zero then skip
                            if (atZero(vert.Position.Z))
                            {
                                bool elevationUnknown = true;
                                Point3d pos = new Point3d();
                                int j = 1; //Start at second vertice
                                do
                                {
                                    if (!atZero(vertices[j].Position.Z))
                                    {
                                        elevationUnknown = false;
                                        pos = vertices[j].Position;
                                    }
                                    j++;
                                    //Catch out of bounds
                                    if (j > endIdx) break;
                                } while (elevationUnknown);

                                vert.CheckOrOpenForWrite();
                                vert.Position = new Point3d(
                                    vert.Position.X, vert.Position.Y, pos.Z);
                            }
                        }
                        #endregion
                        #region Last vertice
                        {
                            PolylineVertex3d vert = vertices[endIdx];

                            //If vertice is already non-zero then skip
                            if (atZero(vert.Position.Z))
                            {
                                bool elevationUnknown = true;
                                Point3d pos = new Point3d();
                                int j = endIdx - 1;
                                do
                                {
                                    if (!atZero(vertices[j].Position.Z))
                                    {
                                        elevationUnknown = false;
                                        pos = vertices[j].Position;
                                    }
                                    j--;
                                    //Catch out of bounds
                                    if (j < 0) break;
                                } while (elevationUnknown);

                                vert.CheckOrOpenForWrite();
                                vert.Position = new Point3d(
                                    vert.Position.X, vert.Position.Y, pos.Z);
                            }
                        }
                        #endregion

                        //Start at second vertice and end at next to last
                        for (int i = 1; i < endIdx; i++)
                        {
                            var vert = vertices[i];

                            //Intermediary vertex case
                            //Activate only if vertex at zero
                            if (atZero(vert.Position.Z))
                            {
                                Point3d forwardPos = new Point3d();
                                Point3d backwardPos = new Point3d();

                                int forwardIdx = i + 1;
                                int backwardIdx = i - 1;

                                bool forwardElevationUnknown = true;
                                bool backwardElevationUnknown = true;

                                #region Back and forward detection
                                while (true) //Forward detection
                                {
                                    if (!atZero(vertices[forwardIdx].Position.Z))
                                    {
                                        forwardElevationUnknown = false;
                                        forwardPos = vertices[forwardIdx].Position;
                                        break; //Break here to avoid increasing forwardIdx
                                    }
                                    forwardIdx++;
                                    //Catch out of bounds
                                    if (forwardIdx > endIdx) break;
                                }

                                while (true) //Backward detection
                                {
                                    if (!atZero(vertices[backwardIdx].Position.Z))
                                    {
                                        backwardElevationUnknown = false;
                                        backwardPos = vertices[backwardIdx].Position;
                                        break; //Break here to avoid increasing backwardIdx
                                    }
                                    backwardIdx--;
                                    //Catch out of bounds
                                    if (backwardIdx < 0) break;
                                }
                                #endregion

                                if (!backwardElevationUnknown && !forwardElevationUnknown)
                                {
                                    //Interpolate between vertices
                                    double startElevation = vertices[backwardIdx].Position.Z;
                                    double endElevation = vertices[forwardIdx].Position.Z;
                                    double AB = p3d.GetHorizontalLengthBetweenIdxs(backwardIdx, forwardIdx);
                                    double PB = p3d.GetHorizontalLengthBetweenIdxs(backwardIdx, i);

                                    double m = (endElevation - startElevation) / AB;
                                    double b = endElevation - m * AB;
                                    double newElevation = m * PB + b;

                                    //Change the elevation
                                    vert.CheckOrOpenForWrite();
                                    vert.Position = new Point3d(
                                        vert.Position.X, vert.Position.Y, newElevation);
                                }
                                else if (!backwardElevationUnknown)
                                {
                                    //prdDbg("Warning! Should not execute!");
                                    //vertices[i].CheckOrOpenForWrite();
                                    //vertices[i].Position = new Point3d(
                                    //    vertices[i].Position.X, vertices[i].Position.Y,
                                    //    backwardPos.Z);
                                }
                                else if (!forwardElevationUnknown)
                                {
                                    //prdDbg("Warning! Should not execute!");
                                    //vertices[i].CheckOrOpenForWrite();
                                    //vertices[i].Position = new Point3d(
                                    //    vertices[i].Position.X, vertices[i].Position.Y,
                                    //    forwardPos.Z);
                                }
                            }
                        }
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    UtilsCommon.Utils.prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }
    }
}