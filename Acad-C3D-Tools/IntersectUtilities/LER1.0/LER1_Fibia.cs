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
using Microsoft.VisualBasic.Logging;
using static Autodesk.AutoCAD.DataExtraction.DxDrawingDataExtractor;
using System.Security.Cryptography;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        [CommandMethod("FIBIACREATE2DAND3D")]
        public void fibiacreate2dand3d()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Database ler2dDb = new Database(true, true))
            using (Database ler3dDb = new Database(true, true))
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx);
                    HashSet<Polyline3d> pl3ds = localDb.HashSetOfType<Polyline3d>(tx);

                    //Create 2d representation
                    ler2dDb.ReadDwgFile(@"X:\AutoCAD DRI - 01 Civil 3D\Templates\LerTemplate.dwt",
                                    FileOpenMode.OpenForReadAndAllShare, false, null);

                    Random r = new Random();
                    int rInt = r.Next(0, 1000);
                    string dbFilename = localDb.OriginalFileName;
                    string path = Path.GetDirectoryName(dbFilename);
                    string ler2dFilename = path + $"\\Fibia_2D_{rInt}.dwg";
                    string ler3dFilename = path + $"\\Fibia_3D_{rInt}.dwg";

                    using (Transaction ler2dTx = ler2dDb.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            string layerName = "Fiberkabel";
                            ler2dDb.CheckOrCreateLayer(layerName);

                            foreach (Polyline pline in plines)
                            {
                                Polyline newPline = new Polyline(pline.NumberOfVertices);

                                for (int i = 0; i < pline.NumberOfVertices; i++)
                                {
                                    newPline.AddVertexAt(
                                        newPline.NumberOfVertices, pline.GetPoint2dAt(i), 0, 0, 0);
                                }

                                newPline.AddEntityToDbModelSpace(ler2dDb);
                                newPline.Layer = layerName;
                            }

                            foreach (Polyline3d pl3d in pl3ds)
                            {
                                PolylineVertex3d[] vertici = pl3d.GetVertices(tx);

                                Polyline newPline = new Polyline(vertici.Length);

                                for (int i = 0; i < vertici.Length; i++)
                                {
                                    newPline.AddVertexAt(
                                        newPline.NumberOfVertices, vertici[i].Position.To2D(), 0, 0, 0);
                                }

                                newPline.AddEntityToDbModelSpace(ler2dDb);
                                newPline.Layer = layerName;
                            }
                        }
                        catch (System.Exception ex)
                        {
                            ler2dTx.Abort();
                            DisposeOfDbs();
                            throw;
                        }

                        ler2dTx.Commit();
                    }

                    fixlerlayersmethod(ler2dDb);

                    //Save the new dwg file
                    ler2dDb.SaveAs(ler2dFilename, DwgVersion.Current);

                    //Create 3d representation
                    using (Transaction ler3dTx = ler3dDb.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            string layerName2d = "Fiberkabel";
                            string layerName3d = "Fiberkabel-3D";
                            ler3dDb.CheckOrCreateLayer(layerName2d, 1);
                            ler3dDb.CheckOrCreateLayer(layerName3d, 1);

                            foreach (Polyline pline in plines)
                            {
                                Point3dCollection newVertices = new Point3dCollection();

                                for (int i = 0; i < pline.NumberOfVertices; i++)
                                {
                                    newVertices.Add(pline.GetPoint3dAt(i));
                                }

                                Polyline3d newPline = new Polyline3d(Poly3dType.SimplePoly, newVertices, false);

                                newPline.AddEntityToDbModelSpace(ler3dDb);
                                newPline.Layer = layerName2d;
                            }

                            foreach (Polyline3d pl3d in pl3ds)
                            {
                                PolylineVertex3d[] vertici = pl3d.GetVertices(tx);

                                Point3dCollection newVertices = new Point3dCollection();

                                for (int i = 0; i < vertici.Length; i++)
                                {
                                    newVertices.Add(vertici[i].Position);
                                }

                                Polyline3d newPline = new Polyline3d(Poly3dType.SimplePoly, newVertices, false);

                                newPline.AddEntityToDbModelSpace(ler3dDb);
                                newPline.Layer = layerName3d;
                            }
                        }
                        catch (System.Exception ex)
                        {
                            ler3dTx.Abort();
                            DisposeOfDbs();
                            throw;
                        }

                        ler3dTx.Commit();
                    }

                    fixlerlayersmethod(ler3dDb);

                    //Save the new dwg file
                    ler3dDb.SaveAs(ler3dFilename, DwgVersion.Current);
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();

                void DisposeOfDbs()
                {
                    ler2dDb.Dispose();
                    ler3dDb.Dispose();
                }
            }
        }
    }
}