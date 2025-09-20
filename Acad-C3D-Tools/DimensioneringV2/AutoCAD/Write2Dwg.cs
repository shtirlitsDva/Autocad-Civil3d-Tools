﻿using System;
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

using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

using NetTopologySuite.Geometries;

using DimensioneringV2.GraphFeatures;
using IntersectUtilities.UtilsCommon;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using IntersectUtilities.PipeScheduleV2;
using NorsynHydraulicCalc.Pipes;
using System.Windows;

namespace DimensioneringV2.AutoCAD
{
    internal static class Write2Dwg
    {
        internal static void Write(IEnumerable<AnalysisFeature> features)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "DWG Files (*.dwg)|*.dwg|All Files (*.*)|*.*",
                DefaultExt = "dwg",
                Title = "Save current network configuration",
                AddExtension = true
            };

            string fileName;
            if (saveFileDialog.ShowDialog() == true)
            {
                fileName = saveFileDialog.FileName;
            }
            else
            {
                return;
            }

            if (string.IsNullOrEmpty(fileName)) return;

            if (File.Exists(fileName))
            {
                MessageBoxResult result = MessageBox.Show(
                    "The file already exists. Do you want to overwrite it?",
                    "File already exists",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;
            }

            string dimDbFilename = fileName;

            using Database dimDb = new Database(true, true);
            Commands.dim2preparedwgmethod(dimDb);

            using Transaction dimTx = dimDb.TransactionManager.StartTransaction();

            try
            {
                //Modify the standard text style to have Arial font
                TextStyleTable tst = dimDb.TextStyleTableId.Go<TextStyleTable>(dimTx);
                if (tst.Has("Standard"))
                {
                    TextStyleTableRecord tsr = tst["Standard"]
                        .Go<TextStyleTableRecord>(dimTx, OpenMode.ForWrite);
                    tsr.FileName = "arial.ttf";
                }

                //Preapare for linetype creation
                LinetypeTable ltt = dimDb.LinetypeTableId.Go<LinetypeTable>(dimTx, (OpenMode)1);

                foreach (AnalysisFeature feature in features)
                {
                    var lineString = feature.Geometry as LineString;
                    if (lineString == null)
                    {
                        prdDbg($"Feature is not a LineString! Check your geometry.");
                        continue;
                    }
                    
                    if (feature.OriginalGeometry.Stik != null)
                    {
                        var ntssp = feature.OriginalGeometry.Stik.StartPoint;
                        var ntsep = feature.OriginalGeometry.Stik.EndPoint;

                        Line stik = new Line(
                            new Point3d(ntssp.X, ntssp.Y, 0),
                            new Point3d(ntsep.X, ntsep.Y, 0));
                        stik.AddEntityToDbModelSpace(dimDb);
                        stik.Layer = cv.LayerConnectionLine;
                    }

                    if (feature.OriginalGeometry.Vej != null)
                    {
                        var pline = NTSConversion.ConvertNTSLineStringToPline(
                            feature.OriginalGeometry.Vej);

                        pline.AddEntityToDbModelSpace(dimDb);
                        if (feature.NumberOfBuildingsSupplied == 0)
                            pline.Layer = cv.LayerVejmidteSlukket;
                        else pline.Layer = cv.LayerVejmidteTændt;
                    }
                }
            }
            catch (Exception ex)
            {
                prdDbg(ex);
                dimTx.Abort();
                return;
            }

            dimTx.Commit();
            dimDb.SaveAs(dimDbFilename, DwgVersion.Current);
        }
    }
}