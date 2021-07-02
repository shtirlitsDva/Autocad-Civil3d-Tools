using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Gis.Map;
using System.IO;
using System;
using IntersectUtilities;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static IntersectUtilities.Utils;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;

namespace ExportShapeFiles
{
    public class ExportShapeFiles : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\nExport lines to shapefiles: EXPORTSHAPEFILES");
            //doc.Editor.WriteMessage("\n-> Intersect alignment with XREF: INTAL");
            //doc.Editor.WriteMessage("\n-> Write a list of all XREF layers: LISTINTLAY");
            //doc.Editor.WriteMessage("\n-> Change the elevation of CogoPoint by selecting projection label: CHEL");
        }

        public void Terminate()
        {
        }
        #endregion

        [CommandMethod("EXPORTSHAPEFILES")]
        public void exportshapefiles()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            MapApplication mapApp = HostMapApplicationServices.Application;
            Autodesk.Gis.Map.ImportExport.Exporter exporter = mapApp.Exporter;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                string logFileName = @"X:\0371-1158 - Gentofte Fase 4 - Dokumenter\02 Ekstern\" +
                                 @"01 Gældende tegninger\01 GIS input\02 Trace shape\export.log";

                try
                {
                    string fileName = localDb.OriginalFileName;
                    string phaseNumber = "";

                    Regex regex = new Regex(@"(?<number>\d.\d)(?<extension>\.[^.]*$)");

                    if (regex.IsMatch(fileName))
                    {
                        Match match = regex.Match(fileName);
                        phaseNumber = match.Groups["number"].Value;
                        phaseNumber = phaseNumber.Replace(".", "");
                        File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: Phase number detected: <{phaseNumber}>." });
                    }
                    else
                    {
                        File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: " +
                            $"Detection of phase from filename failed! Aborting export for current file." });
                        tx.Abort();
                        return;
                    }

                    string finalExportFileName = @"X:\0371-1158 - Gentofte Fase 4 - Dokumenter\02 Ekstern\" +
                                                 @"01 Gældende tegninger\01 GIS input\02 Trace shape";
                    finalExportFileName += "\\" + phaseNumber + ".shp";

                    File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: Exporting to {finalExportFileName}." });
                    
                    HashSet<Polyline> pls = localDb.HashSetOfType<Polyline>(tx);
                    HashSet<Line> ls = localDb.HashSetOfType<Line>(tx);

                    ObjectIdCollection ids = new ObjectIdCollection();
                    foreach (Polyline pl in pls)
                    {
                        if (pl.Layer.Contains("FJV-TWIN") ||
                            pl.Layer.Contains("FJV-FREM") ||
                            pl.Layer.Contains("FJV-RETUR"))
                            ids.Add(pl.Id);
                    }
                    foreach (Line l in ls)
                    {
                        if (l.Layer.Contains("FJV-TWIN") ||
                            l.Layer.Contains("FJV-FREM") ||
                            l.Layer.Contains("FJV-RETUR"))
                            ids.Add(l.Id);
                    }

                    File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: {ids.Count} object(s) found for export." });

                    exporter.Init("SHP", finalExportFileName);
                    exporter.SetStorageOptions(
                        Autodesk.Gis.Map.ImportExport.StorageType.FileOneEntityType,
                        Autodesk.Gis.Map.ImportExport.GeometryType.Line, null);
                    exporter.SetSelectionSet(ids);
                    Autodesk.Gis.Map.ImportExport.ExpressionTargetCollection mappings =
                        exporter.GetExportDataMappings();
                    mappings.Add(":Serie@Pipes", "Serie");
                    mappings.Add(":System@Pipes", "System");
                    mappings.Add(":DN@Pipes", "DN");
                    exporter.SetExportDataMappings(mappings);

                    File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: Starting export." });
                    exporter.Export(true);
                    File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: Export completed!" });
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: An exception was caught! Message: {ex.Message}. Aborting export of current file!" });
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Abort();
            }
        }
    }
}
