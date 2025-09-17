using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static IntersectUtilities.UtilsCommon.Utils;
using IntersectUtilities.NTS;
using cv = DimensioneringV2.CommonVariables;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

using NetTopologySuite.Geometries;

using DimensioneringV2.GraphFeatures;
using IntersectUtilities.UtilsCommon;
using System.Windows;

namespace DimensioneringV2.AutoCAD
{
    internal class MarkNullEdges
    {
        internal static void Mark(IEnumerable<AnalysisFeature> features)
        {

            MessageBoxResult result = MessageBox.Show(
                "Null edges detected! This is not allowed!\n" +
                "See 0-FJV_Debug layer for marking of the offenders.",
                "Null edges detected!",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using DocumentLock docLock = docCol.MdiActiveDocument.LockDocument();
            using Transaction tx = localDb.TransactionManager.StartTransaction();

            try
            {
                foreach (AnalysisFeature feature in features)
                {
                    var lineString = feature.OriginalGeometry.FullGeometry as LineString;
                    if (lineString == null)
                    {
                        prdDbg($"Feature is not a LineString! Check your geometry.");
                        continue;
                    }

                    var pline = NTSConversion.ConvertNTSLineStringToPline(lineString);

                    pline.ConstantWidth = 1;

                    pline.AddEntityToDbModelSpace(localDb);
                    pline.Layer = cv.LayerDebugLines;
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
