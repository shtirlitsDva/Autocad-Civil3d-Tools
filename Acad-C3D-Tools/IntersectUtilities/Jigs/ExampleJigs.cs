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
        public class JigUtils
        {
            //https://through-the-interface.typepad.com/through_the_interface/2010/12/jigging-an-autocad-polyline-with-arc-segments-using-net.html
            // Custom ArcTangent method, as the Math.Atan
            // doesn't handle specific cases
            public static double Atan(double y, double x)
            {
                if (x > 0)
                    return Math.Atan(y / x);
                else if (x < 0)
                    return Math.Atan(y / x) - Math.PI;
                else  // x == 0
                {
                    if (y > 0)
                        return Math.PI;
                    else if (y < 0)
                        return -Math.PI;
                    else // if (y == 0) theta is undefined
                        return 0.0;
                }
            }

            // Computes Angle between current direction
            // (vector from last vertex to current vertex)
            // and the last pline segment

            public static double ComputeAngle(
              Point3d startPoint, Point3d endPoint,
              Vector3d xdir, Matrix3d ucs
            )
            {
                Vector3d v =
                  new Vector3d(
                    (endPoint.X - startPoint.X) / 2,
                    (endPoint.Y - startPoint.Y) / 2,
                    (endPoint.Z - startPoint.Z) / 2
                  );

                double cos = v.DotProduct(xdir);
                double sin =
                  v.DotProduct(
                    Vector3d.ZAxis.TransformBy(ucs).CrossProduct(xdir)
                  );

                return Atan(sin, cos);
            }
        }

        public class BulgePolyJig : EntityJig
        {
            Point3d _tempPoint;
            Plane _plane;
            bool _isArcSeg = false;
            bool _isUndoing = false;
            Matrix3d _ucs;

            public BulgePolyJig(Matrix3d ucs) : base(new Polyline())
            {
                _ucs = ucs;
                Vector3d normal = Vector3d.ZAxis.TransformBy(ucs);
                _plane = new Plane(Point3d.Origin, normal);
                Polyline pline = Entity as Polyline;
                pline.SetDatabaseDefaults();
                pline.Normal = normal;
                AddDummyVertex();
            }

            protected override SamplerStatus Sampler(JigPrompts prompts)
            {
                JigPromptPointOptions jigOpts = new JigPromptPointOptions();

                jigOpts.UserInputControls =
                  (UserInputControls.Accept3dCoordinates |
                  UserInputControls.NullResponseAccepted |
                  UserInputControls.NoNegativeResponseAccepted |
                  UserInputControls.GovernedByOrthoMode |
                  UserInputControls.GovernedByUCSDetect
                  );

                _isUndoing = false;

                Polyline pline = Entity as Polyline;

                if (pline.NumberOfVertices == 1)
                {
                    // For the first vertex, just ask for the point

                    jigOpts.Message = "\nSpecify start point: ";
                }
                else if (pline.NumberOfVertices > 1)
                {
                    string msgAndKwds =
                      (_isArcSeg ?
                        "\nSpecify endpoint of arc or [Line/Undo]: " :
                        "\nSpecify next point or [Arc/Undo]: "
                      );

                    string kwds = (_isArcSeg ? "Line Undo" : "Arc Undo");
                }
                else
                    return SamplerStatus.Cancel; // Should never happen

                // Get the point itself
                PromptPointResult res = prompts.AcquirePoint(jigOpts);

                if (res.Status == PromptStatus.Keyword)
                {
                    if (res.StringResult.ToUpper() == "ARC")
                        _isArcSeg = true;
                    else if (res.StringResult.ToUpper() == "LINE")
                        _isArcSeg = false;
                    else if (res.StringResult.ToUpper() == "UNDO")
                        _isUndoing = true;

                    return SamplerStatus.OK;
                }
                else if (res.Status == PromptStatus.OK)
                {
                    // Check if it has changed or not (reduces flicker)
                    if (_tempPoint == res.Value)
                        return SamplerStatus.NoChange;
                    else
                    {
                        _tempPoint = res.Value;
                        return SamplerStatus.OK;
                    }
                }

                return SamplerStatus.Cancel;
            }

            protected override bool Update()
            {
                // Update the dummy vertex to be our 3D point
                // projected onto our plane
                Polyline pl = Entity as Polyline;

                if (_isArcSeg)
                {
                    Point3d lastVertex =
                      pl.GetPoint3dAt(pl.NumberOfVertices - 2);

                    Vector3d refDir;

                    if (pl.NumberOfVertices < 3)
                        refDir = new Vector3d(1.0, 1.0, 0.0);
                    else
                    {
                        // Check bulge to see if last segment was an arc or a line
                        if (pl.GetBulgeAt(pl.NumberOfVertices - 3) != 0)
                        {
                            CircularArc3d arcSegment =
                              pl.GetArcSegmentAt(pl.NumberOfVertices - 3);

                            Line3d tangent = arcSegment.GetTangent(lastVertex);

                            // Reference direction is the invert of the arc tangent
                            // at last vertex

                            refDir = tangent.Direction.MultiplyBy(-1.0);
                        }
                        else
                        {
                            Point3d pt =
                              pl.GetPoint3dAt(pl.NumberOfVertices - 3);

                            refDir =
                              new Vector3d(
                                lastVertex.X - pt.X,
                                lastVertex.Y - pt.Y,
                                lastVertex.Z - pt.Z
                              );
                        }
                    }

                    double angle =
                      JigUtils.ComputeAngle(
                        lastVertex, _tempPoint, refDir, _ucs);

                    // Bulge is defined as tan of one fourth of included angle
                    // Need to double the angle since it represents the included
                    // angle of the arc
                    // So formula is: bulge = Tan(angle * 2 * 0.25)

                    double bulge = Math.Tan(angle * 0.5);

                    pl.SetBulgeAt(pl.NumberOfVertices - 2, bulge);
                }
                else
                {
                    // Line mode. Need to remove last bulge if there was one

                    if (pl.NumberOfVertices > 1)
                        pl.SetBulgeAt(pl.NumberOfVertices - 2, 0);
                }

                pl.SetPointAt(
                  pl.NumberOfVertices - 1, _tempPoint.Convert2d(_plane)
                );

                return true;
            }

            public bool IsUndoing
            {
                get
                {
                    return _isUndoing;
                }
            }

            public void AddDummyVertex()
            {
                // Create a new dummy vertex... can have any initial value
                Polyline pline = Entity as Polyline;
                pline.AddVertexAt(
                  pline.NumberOfVertices, new Point2d(0, 0), 0, 0, 0);
            }

            public void RemoveLastVertex()
            {
                Polyline pline = Entity as Polyline;

                // Let's first remove our dummy vertex   
                if (pline.NumberOfVertices > 0)
                    pline.RemoveVertexAt(pline.NumberOfVertices - 1);

                // And then check the type of the last segment
                if (pline.NumberOfVertices >= 2)
                {
                    double blg = pline.GetBulgeAt(pline.NumberOfVertices - 2);
                    _isArcSeg = (blg != 0);
                }
            }

            public void Append()
            {
                Database db = HostApplicationServices.WorkingDatabase;

                Transaction tr =
                  db.TransactionManager.StartTransaction();
                using (tr)
                {
                    BlockTable bt =
                      tr.GetObject(
                        db.BlockTableId, OpenMode.ForRead
                      ) as BlockTable;
                    BlockTableRecord btr =
                      tr.GetObject(
                        bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite
                      ) as BlockTableRecord;

                    btr.AppendEntity(this.Entity);
                    tr.AddNewlyCreatedDBObject(this.Entity, true);
                    tr.Commit();
                }
            }

            [CommandMethod("BPJIG")]
            public static void RunBulgePolyJig()
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                Database db = doc.Database;
                Editor ed = doc.Editor;

                BulgePolyJig jig =
                  new BulgePolyJig(ed.CurrentUserCoordinateSystem);

                while (true)
                {
                    PromptResult res = ed.Drag(jig);

                    switch (res.Status)
                    {
                        // New point was added, keep going
                        case PromptStatus.OK:
                            jig.AddDummyVertex();
                            break;

                        // Keyword was entered
                        case PromptStatus.Keyword:
                            if (jig.IsUndoing)
                                jig.RemoveLastVertex();
                            break;

                        // If the jig completed successfully, add the polyline
                        case PromptStatus.None:
                            jig.RemoveLastVertex();
                            jig.Append();
                            return;

                        // User cancelled the command, get out of here
                        // and don't forget to dispose the jigged entity
                        default:
                            jig.Entity.Dispose();
                            return;
                    }
                }
            }
        }
}
}
