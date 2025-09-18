using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Win32;

using static IntersectUtilities.UtilsCommon.Utils;
using IntersectUtilities.NTS;
using cv = DimensioneringV2.CommonVariables;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

using NetTopologySuite.Geometries;

using DimensioneringV2.GraphFeatures;
using IntersectUtilities.UtilsCommon;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using IntersectUtilities.PipeScheduleV2;
using NorsynHydraulicCalc.Pipes;
using System.Windows;
using DimensioneringV2.Services.GDALClient;

namespace DimensioneringV2.AutoCAD
{
    internal class WriteElevations2CurrentDrawing
    {
        internal static void Write(IEnumerable<ElevationProfileCache> caches)
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            using DocumentLock docLock = docCol.MdiActiveDocument.LockDocument();
            using Transaction tx = localDb.TransactionManager.StartTransaction();

            try
            {
                foreach (var cache in caches)
                {
                    var elevations = cache.GetDefaultProfile();

                    foreach (var e in elevations)
                    {
                        var p1 = new Point3d(e.X, e.Y, e.Elevation);
                        var bp = new Point3d(e.X, e.Y, 0);

                        var p = new DBPoint(p1);
                        p.SetDatabaseDefaults();
                        p.AddEntityToDbModelSpace(localDb);

                        var line = new Line(bp, p1);
                        line.SetDatabaseDefaults();    
                        line.AddEntityToDbModelSpace(localDb);
                    }
                }
            }
            catch (Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }

            tx.Commit();
        }
    }
}
