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
using FolderSelect;

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
        //[CommandMethod("MASSIMPORTTDCYOUSEE")]
        //public void massimporttdcyousee()
        //{
        //    DocumentCollection docCol = Application.DocumentManager;
        //    Database localDb = docCol.MdiActiveDocument.Database;
        //    Editor editor = docCol.MdiActiveDocument.Editor;
        //    Document doc = docCol.MdiActiveDocument;

        //    using (Transaction tx = localDb.TransactionManager.StartTransaction())
        //    {
        //        try
        //        {
        //            #region Get file and folder of gml
        //            string pathToTopFolder = string.Empty;
        //            FolderSelectDialog fsd = new FolderSelectDialog()
        //            {
        //                Title = "Choose folder where TDC files are stored: ",
        //                InitialDirectory = @"C:\"
        //            };
        //            if (fsd.ShowDialog(IntPtr.Zero))
        //            {
        //                pathToTopFolder = fsd.FileName + "\\";
        //            }
        //            else return;

        //            var tdcFiles = Directory.EnumerateFiles(pathToTopFolder, "TDCVector_*.dxf", SearchOption.AllDirectories);
        //            var youseeFiles = Directory.EnumerateFiles(pathToTopFolder, "YouSeeVector_*.dxf", SearchOption.AllDirectories);
        //            #endregion

        //            using (Database xDb = new Database(true, true))
        //            using (Transaction xTx = xDb.TransactionManager.StartTransaction())
        //            {
        //                xDb.CheckOrCreateLayer("TDC");
        //                xDb.CheckOrCreateLayer("Yousee");

        //                try
        //                {
        //                    HashSet<(netDxf.Entities.Polyline3D dxfPl3d, string layer)> collector =
        //                        new HashSet<(netDxf.Entities.Polyline3D dxfPl3d, string layer)>();

        //                    foreach (string file in tdcFiles)
        //                    {
        //                        DxfDocument dxf = DxfDocument.Load(file);
        //                        if (dxf == null)
        //                        {
        //                            prdDbg("File " + file + " failed to load!");
        //                            continue;
        //                        }
        //                        foreach (netDxf.Entities.Polyline3D item in dxf.Entities.Polylines3D)
        //                            collector.Add((item, "TDC"));

        //                    }
        //                    foreach (string file in youseeFiles)
        //                    {
        //                        DxfDocument dxf = DxfDocument.Load(file);
        //                        if (dxf == null)
        //                        {
        //                            prdDbg("File " + file + " failed to load!");
        //                            continue;
        //                        }
        //                        foreach (netDxf.Entities.Polyline3D item in dxf.Entities.Polylines3D)
        //                            collector.Add((item, "Yousee"));
        //                    }

        //                    foreach (var item in collector)
        //                    {
        //                        Point2dCollection p2ds = new Point2dCollection();
        //                        var verts = item.dxfPl3d.Vertexes;
        //                        if (verts.Count == 1) continue;
        //                        foreach (var vert in verts)
        //                            p2ds.Add(new Point2d(vert.X, vert.Y));
        //                        Polyline pl = new Polyline(p2ds.Count);
        //                        foreach (var p2d in p2ds)
        //                            pl.AddVertexAt(pl.NumberOfVertices, p2d, 0, 0, 0);
        //                        pl.AddEntityToDbModelSpace(xDb);
        //                        pl.Layer = item.layer;
        //                    }

        //                    System.Windows.Forms.Application.DoEvents();

        //                }
        //                catch (System.Exception ex)
        //                {
        //                    prdDbg(ex);
        //                    //xDb.CloseInput(true);
        //                    xTx.Abort();
        //                    throw;
        //                }
        //                xTx.Commit();
        //                xDb.SaveAs(pathToTopFolder + "TDC_SAMLET.dwg", true, DwgVersion.Newest, xDb.SecurityParameters);
        //            }
        //        }
        //        catch (System.Exception ex)
        //        {
        //            tx.Abort();
        //            prdDbg(ex);
        //            return;
        //        }
        //        tx.Commit();
        //    }
        //}

        //[CommandMethod("MASSIMPORTGLOBALCONNECT")]
        //public void massimportglobalconnect()
        //{
        //    DocumentCollection docCol = Application.DocumentManager;
        //    Database localDb = docCol.MdiActiveDocument.Database;

        //    using (Transaction tx = localDb.TransactionManager.StartTransaction())
        //    {
        //        try
        //        {
        //            #region Get file and folder of gml
        //            string pathToTopFolder = string.Empty;
        //            FolderSelectDialog fsd = new FolderSelectDialog()
        //            {
        //                Title = "Choose folder where Global Connect files are stored: ",
        //                InitialDirectory = @"C:\"
        //            };
        //            if (fsd.ShowDialog(IntPtr.Zero))
        //            {
        //                pathToTopFolder = fsd.FileName + "\\";
        //            }
        //            else return;

        //            var files = Directory.EnumerateFiles(pathToTopFolder, "*DXF-utm32.dxf", SearchOption.AllDirectories);

        //            #endregion

        //            using (Database xDb = new Database(true, true))
        //            using (Transaction xTx = xDb.TransactionManager.StartTransaction())
        //            {
        //                try
        //                {
        //                    var col = new HashSet<(Point2dCollection p2ds, string layer)>();

        //                    foreach (string file in files)
        //                    {
        //                        DxfDocument dxf;
        //                        try
        //                        {
        //                            dxf = DxfDocument.Load(file);
        //                        }
        //                        catch (System.Exception ex)
        //                        {
        //                            prdDbg($"File {file} threw an exception! Skipping.");
        //                            prdDbg(ex.Message);
        //                            continue;
        //                        }
        //                        if (dxf == null)
        //                        {
        //                            prdDbg("File " + file + " failed to load!");
        //                            continue;
        //                        }
        //                        foreach (netDxf.Entities.Polyline3D item in dxf.Entities.Polylines3D)
        //                        {
        //                            List<Vector3> verts = item.Vertexes;
        //                            if (verts.Count == 1) continue;
        //                            Point2dCollection p2ds = new Point2dCollection();
        //                            foreach (var vert in verts)
        //                                p2ds.Add(new Point2d(vert.X, vert.Y));
        //                            col.Add((p2ds, item.Layer.Name));
        //                        }
        //                        foreach (netDxf.Entities.Polyline2D item in dxf.Entities.Polylines2D)
        //                        {
        //                            var verts = item.Vertexes;
        //                            if (verts.Count == 1) continue;
        //                            Point2dCollection p2ds = new Point2dCollection();
        //                            foreach (var vert in verts)
        //                                p2ds.Add(new Point2d(vert.Position.X, vert.Position.Y));
        //                            col.Add((p2ds, item.Layer.Name));
        //                        }
        //                        foreach (netDxf.Entities.Line item in dxf.Entities.Lines)
        //                        {
        //                            Point2dCollection p2ds = new Point2dCollection();
        //                            p2ds.Add(new Point2d(item.StartPoint.X, item.StartPoint.Y));
        //                            p2ds.Add(new Point2d(item.EndPoint.X, item.EndPoint.Y));
        //                            col.Add((p2ds, item.Layer.Name));
        //                        }
        //                    }

        //                    foreach (var layer in col.Select(x => x.layer)
        //                        .Distinct()
        //                        .Where(x => x != "NON_TELIA_TRACE"))
        //                        xDb.CheckOrCreateLayer(layer);

        //                    void CreatePolyline(Point2dCollection p2ds, string layer)
        //                    {
        //                        Polyline pl = new Polyline(p2ds.Count);
        //                        foreach (var p2d in p2ds)
        //                            pl.AddVertexAt(pl.NumberOfVertices, p2d, 0, 0, 0);
        //                        pl.AddEntityToDbModelSpace(xDb);
        //                        pl.Layer = layer;
        //                    }

        //                    foreach (var item in col.Where(x => x.layer != "NON_TELIA_TRACE"))
        //                        CreatePolyline(item.p2ds, item.layer);

        //                    System.Windows.Forms.Application.DoEvents();

        //                }
        //                catch (System.Exception ex)
        //                {
        //                    prdDbg(ex);
        //                    //xDb.CloseInput(true);
        //                    xTx.Abort();
        //                    throw;
        //                }
        //                xTx.Commit();
        //                xDb.SaveAs(pathToTopFolder + "SAMLET.dwg", true, DwgVersion.Newest, xDb.SecurityParameters);
        //            }
        //        }
        //        catch (System.Exception ex)
        //        {
        //            tx.Abort();
        //            prdDbg(ex);
        //            return;
        //        }
        //        tx.Commit();
        //    }
        //}

        //[CommandMethod("MASSIMPORTTELIA")]
        //public void massimporttelia()
        //{
        //    DocumentCollection docCol = Application.DocumentManager;
        //    Database localDb = docCol.MdiActiveDocument.Database;

        //    using (Transaction tx = localDb.TransactionManager.StartTransaction())
        //    {
        //        try
        //        {
        //            #region Get file and folder of gml
        //            string pathToTopFolder = string.Empty;
        //            FolderSelectDialog fsd = new FolderSelectDialog()
        //            {
        //                Title = "Choose folder where Global Connect files are stored: ",
        //                InitialDirectory = @"C:\"
        //            };
        //            if (fsd.ShowDialog(IntPtr.Zero))
        //            {
        //                pathToTopFolder = fsd.FileName + "\\";
        //            }
        //            else return;

        //            string pathToCadLog = pathToTopFolder + "cadLog.txt";
        //            File.WriteAllText(pathToCadLog, "");
        //            var files = Directory.EnumerateFiles(pathToTopFolder, "*DXF-utm32.dxf", SearchOption.AllDirectories);

        //            #endregion

        //            using (Database xDb = new Database(true, true))
        //            using (Transaction xTx = xDb.TransactionManager.StartTransaction())
        //            {
        //                try
        //                {
        //                    foreach (var f in files)
        //                    {
        //                        using (Database xDbDxf = new Database(true, true))
        //                        using (Transaction xTxDxf = xDbDxf.TransactionManager.StartTransaction())
        //                        {
        //                            try
        //                            {
        //                                xDbDxf.DxfIn(f, pathToCadLog);
        //                                xDbDxf.CloseInput(true);

        //                                Oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(xDbDxf);
        //                                Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(xDb);

        //                                ObjectIdCollection idsToClone = new ObjectIdCollection();
        //                                var objs = xDbDxf.HashSetIdsOfType<Entity>();
        //                                foreach (var obj in objs) idsToClone.Add(obj);

        //                                IdMapping mapping = new IdMapping();
        //                                xDbDxf.WblockCloneObjects(idsToClone, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);
        //                                xTxDxf.Commit();
        //                            }
        //                            catch (System.Exception)
        //                            {
        //                                xTxDxf.Abort();
        //                                throw;
        //                            }
        //                        }
        //                    }

        //                    System.Windows.Forms.Application.DoEvents();

        //                }
        //                catch (System.Exception ex)
        //                {
        //                    prdDbg(ex);
        //                    //xDb.CloseInput(true);
        //                    xTx.Abort();
        //                    throw;
        //                }
        //                xTx.Commit();
        //                xDb.SaveAs(pathToTopFolder + "SAMLET.dwg", true, DwgVersion.Newest, xDb.SecurityParameters);
        //            }
        //        }
        //        catch (System.Exception ex)
        //        {
        //            tx.Abort();
        //            prdDbg(ex);
        //            return;
        //        }
        //        tx.Commit();
        //    }
        //}
    }
}