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
using netDxf;

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
        [CommandMethod("MASSIMPORTTDCYOUSEE")]
        public void massimporttdcyousee()
        {

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Get file and folder of gml
                    string pathToTopFolder = string.Empty;
                    FolderSelectDialog fsd = new FolderSelectDialog()
                    {
                        Title = "Choose folder where TDC files are stored: ",
                        InitialDirectory = @"C:\"
                    };
                    if (fsd.ShowDialog(IntPtr.Zero))
                    {
                        pathToTopFolder = fsd.FileName + "\\";
                    }
                    else return;

                    string pathToCadLog = pathToTopFolder + "cadLog.txt";
                    File.WriteAllText(pathToCadLog, "");
                    var files = Directory.EnumerateFiles(pathToTopFolder, "TDCVector_*.dxf", SearchOption.AllDirectories);
                    #endregion

                    using (Database xDb = new Database(false, true))
                    using (Transaction xTx = xDb.TransactionManager.StartTransaction())
                    {
                        xDb.CheckOrCreateLayer("TDC");

                        try
                        {
                            foreach (string file in files)
                            {
                                DxfDocument dxf = DxfDocument.Load(file);
                                if (dxf == null)
                                {
                                    prdDbg("File " + file + " failed to load!");
                                    continue;
                                }

                                foreach (netDxf.Entities.Polyline3D item in dxf.Entities.Polylines3D)
                                {
                                    Point2dCollection p2ds = new Point2dCollection();
                                    var verts = item.Vertexes;
                                    foreach (var vert in verts)
                                        p2ds.Add(new Point2d(vert.X, vert.Y));
                                    Polyline pl = new Polyline(p2ds.Count);
                                    foreach (var p2d in p2ds)
                                        pl.AddVertexAt(pl.NumberOfVertices, p2d, 0, 0, 0);
                                    pl.AddEntityToDbModelSpace(xDb);
                                    pl.Layer = "TDC";
                                }

                                //xDb.DxfIn(file, pathToCadLog);
                                //xDb.CloseInput(true);
                            }
                            System.Windows.Forms.Application.DoEvents();

                        }
                        catch (System.Exception ex)
                        {
                            prdDbg(ex);
                            //xDb.CloseInput(true);
                            xTx.Abort();
                            throw;
                        }
                        xTx.Commit();
                        xDb.SaveAs(pathToTopFolder + "TDC_SAMLET.dwg", true, DwgVersion.Newest, xDb.SecurityParameters);
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