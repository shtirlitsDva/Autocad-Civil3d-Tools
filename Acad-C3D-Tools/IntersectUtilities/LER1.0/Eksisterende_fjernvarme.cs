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
                        localDb.ListOfType<DBPoint>(tx)
                        .Where(x =>
                        x.Position.Z > -98.0
                        && !x.Position.Z.IsZero(0.0001)
                        ),
                        new PointDBHorizontalComparer());

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
                                vertices[i].UpgradeOpen();
                                vertices[i].Position = new Point3d(
                                    vertices[i].Position.X, vertices[i].Position.Y, match.Position.Z);
                                vertices[i].DowngradeOpen();
                            }
                        }
                    }

                    #region Second pass looking at end points
                    //But only for vertices still at 0

                    HashSet<Point3d> allEnds = new HashSet<Point3d>(new Point3dHorizontalComparer());

                    foreach (Polyline3d pline3d in localPlines3d)
                    {
                        var vertices = pline3d.GetVertices(tx);
                        int end = vertices.Length - 1;
                        allEnds.Add(new Point3d(
                            vertices[0].Position.X, vertices[0].Position.Y, vertices[0].Position.Z));
                        allEnds.Add(new Point3d(
                            vertices[end].Position.X, vertices[end].Position.Y, vertices[end].Position.Z));
                    }

                    foreach (Polyline3d pline3d in localPlines3d)
                    {
                        var vertices = pline3d.GetVertices(tx);

                        for (int i = 0; i < vertices.Length; i++)
                        {
                            //Only change vertices still at zero
                            if (vertices[i].Position.Z > 0.1) continue;

                            Point3d match = allEnds.Where(x => x.HorizontalEqualz(
                                vertices[i].Position, 0.05)).FirstOrDefault();
                            if (match != null)
                            {
                                vertices[i].UpgradeOpen();
                                vertices[i].Position = new Point3d(
                                    vertices[i].Position.X, vertices[i].Position.Y, match.Z);
                                vertices[i].DowngradeOpen();
                            }
                        }
                    }

                    #endregion
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }
    }
}