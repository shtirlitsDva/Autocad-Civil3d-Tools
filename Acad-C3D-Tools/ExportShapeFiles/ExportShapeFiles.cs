using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Gis.Map;
using System.IO;
using System;
using IntersectUtilities;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
                string logFileName = @"C:\1\DRI\0371-1158 - Gentofte Fase 4 - Dokumenter\02 Ekstern\" +
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

                    string finalExportFileNameBase = @"C:\1\DRI\0371-1158 - Gentofte Fase 4 - Dokumenter\02 Ekstern\" +
                                                     @"01 Gældende tegninger\01 GIS input\02 Trace shape";
                    string finalExportFileNamePipes = finalExportFileNameBase + "\\" + phaseNumber + ".shp";
                    string finalExportFileNameBlocks = finalExportFileNameBase + "\\" + phaseNumber + "-komponenter.shp";

                    #region Create GIS Data
                    GisData.creategisdata(); 
                    #endregion

                    #region Export af rør
                    File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: Exporting pipes to {finalExportFileNamePipes}." });

                    HashSet<Polyline> pls = localDb.HashSetOfType<Polyline>(tx, true);
                    HashSet<Line> ls = localDb.HashSetOfType<Line>(tx, true);
                    HashSet<Arc> arcs = localDb.HashSetOfType<Arc>(tx, true);
                    HashSet<Entity> ents = new HashSet<Entity>();
                    ents.UnionWith(pls);
                    ents.UnionWith(ls);
                    ents.UnionWith(arcs);
                    //Filter ents for forbidden values
                    ents = ents.Where(x => !DataQa.Gis.ContainsForbiddenValues(x.Layer)).ToHashSet();

                    ObjectIdCollection ids = new ObjectIdCollection();
                    ObjectIdCollection rejectedIds = new ObjectIdCollection();
                    foreach (Entity ent in ents)
                    {
                        if (ent.Layer.Contains("FJV-TWIN") ||
                            ent.Layer.Contains("FJV-FREM") ||
                            ent.Layer.Contains("FJV-RETUR"))
                            ids.Add(ent.Id);
                        else rejectedIds.Add(ent.Id);
                    }

                    foreach (ObjectId id in rejectedIds)
                    {
                        File.AppendAllLines(logFileName, new string[] 
                        { $"{DateTime.Now}: PIPEERROR!!! Pipe {id.Handle} has wrong layer: {id.Layer()}" });
                    }

                    File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: {ids.Count} pipe(s) found for export." });

                    exporter.Init("SHP", finalExportFileNamePipes);
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

                    File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: Starting pipes export." });
                    exporter.Export(true);
                    File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: Exporting of pipes completed!" });
                    #endregion

                    #region Export af komponenter
                    File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: Exporting components to {finalExportFileNameBlocks}." });
                    System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(
                        @"C:\1\DRI\AutoCAD DRI - 01 Civil 3D\FJV Komponenter.csv", "FjvKomponenter");
                    System.Data.DataTable fjvKompDyn = CsvReader.ReadCsvToDataTable(
                        @"C:\1\DRI\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");
                    ObjectIdCollection allBlockIds = new ObjectIdCollection();

                    #region Gather ordinary blocks
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    foreach (ObjectId oid in bt)
                    {
                        BlockTableRecord btr = tx.GetObject(oid, OpenMode.ForRead) as BlockTableRecord;
                        if (btr.GetBlockReferenceIds(true, true).Count == 0) continue;

                        if (ReadStringParameterFromDataTable(btr.Name, fjvKomponenter, "Navn", 0) != null)
                        {
                            ObjectIdCollection blkIds = btr.GetBlockReferenceIds(true, true);
                            foreach (ObjectId blkId in blkIds) allBlockIds.Add(blkId);
                        }
                    }
                    #endregion

                    #region Gather dynamic blocks
                    HashSet<BlockReference> brSet = localDb.HashSetOfType<BlockReference>(tx);
                    foreach (BlockReference br in brSet)
                    {
                        if (br.IsDynamicBlock)
                        {
                            string realName = ((BlockTableRecord)tx.GetObject(br.DynamicBlockTableRecord, OpenMode.ForRead)).Name;
                            if (ReadStringParameterFromDataTable(realName, fjvKompDyn, "Navn", 0) != null)
                            {
                                allBlockIds.Add(br.ObjectId);
                            }
                        }
                    }
                    #endregion
                    File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: {allBlockIds.Count} block(s) found for export." });

                    #region QA block export
                    HashSet<string> blockNamesInModel = new HashSet<string>();
                    foreach (BlockReference br in brSet)
                    {
                        if (br.IsDynamicBlock)
                        {
                            blockNamesInModel.Add(((BlockTableRecord)tx.GetObject(br.DynamicBlockTableRecord, OpenMode.ForRead)).Name);
                        }
                        else blockNamesInModel.Add(br.Name);
                    }

                    HashSet<string> blockNamesGathered = new HashSet<string>();
                    foreach (ObjectId oid in allBlockIds)
                    {
                        BlockReference br = oid.Go<BlockReference>(tx);
                        if (br.IsDynamicBlock)
                        {
                            blockNamesGathered.Add(((BlockTableRecord)tx.GetObject(br.DynamicBlockTableRecord, OpenMode.ForRead)).Name);
                        }
                        else blockNamesGathered.Add(br.Name);
                    }

                    var query = blockNamesInModel.Where(x => !blockNamesGathered.Contains(x));
                    foreach (string name in query)
                    {
                        File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: BLOCKERROR!!!: Block named {name} not included in export!" });
                    }
                    #endregion

                    #region Export components
                    int i = 1;
                    exporter.Init("SHP", finalExportFileNameBlocks);
                    exporter.SetStorageOptions(
                        Autodesk.Gis.Map.ImportExport.StorageType.FileOneEntityType,
                        Autodesk.Gis.Map.ImportExport.GeometryType.Point, null);
                    exporter.SetSelectionSet(allBlockIds);
                    Autodesk.Gis.Map.ImportExport.ExpressionTargetCollection mappingsForBlocks =
                        exporter.GetExportDataMappings();
                    mappings.Add(":BlockName@Components", "BlockName");
                    mappings.Add(":Type@Components", "Type");
                    mappings.Add(":Rotation@Components", "Rotation");
                    // mappings.Add(":System@Components", "System");
                    mappings.Add(":DN1@Components", "DN1");
                    mappings.Add(":DN2@Components", "DN2");
                    //mappings.Add(":Serie@Components", "Serie");
                    exporter.SetExportDataMappings(mappings);
                    

                    File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: Starting blocks export." });
                    exporter.Export(true);
                    File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: Exporting of blocks complete." });
                    #endregion

                    #endregion
                }
                catch (MapImportExportException mex)
                {
                    tx.Abort();
                    File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: EXCEPTION!!! Message: {mex.Message}. Aborting export of current file!" });
                    editor.WriteMessage("\n" + mex.Message);
                    return;
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: EXCEPTION!!! Message: {ex.Message}. Aborting export of current file!" });
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Abort();
            }
        }
    }
}
