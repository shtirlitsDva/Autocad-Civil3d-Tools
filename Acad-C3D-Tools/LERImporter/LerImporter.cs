using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
//using MoreLinq;
//using GroupByCluster;
using IntersectUtilities.UtilsCommon;

using LERImporter.Enhancer;

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

using static IntersectUtilities.UtilsCommon.Utils;
//using static IntersectUtilities.Enums;
//using static IntersectUtilities.HelperMethods;
//using static IntersectUtilities.Utils;
//using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Log = LERImporter.SimpleLogger;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace LERImporter
{
    public class LerImporter : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\nLER Import Application indlæst.");
            doc.Editor.WriteMessage("\nKommando til LER 2.0 -> IGMLBATCH.");

            if (doc != null)
            {
                SystemObjects.DynamicLinker.LoadModule(
                    "AcMPolygonObj" + Application.Version.Major + ".dbx", false, false);
            }
        }

        public void Terminate()
        {
        }
        #endregion

        public static readonly string ImplementedVersion = "2.0.0";        

        //[CommandMethod("IMPORTCONSOLIDATEDGML")]
        public void importconsolidated()
        {
            try
            {
                #region Get file and folder of gml
                string pathToGml = string.Empty;
                OpenFileDialog dialog = new OpenFileDialog()
                {
                    Title = "Choose gml file:",
                    DefaultExt = "gml",
                    Filter = "gml files (*.gml)|*.gml|All files (*.*)|*.*",
                    FilterIndex = 0
                };
                if (dialog.ShowDialog() == true) pathToGml = dialog.FileName;
                else return;
                #endregion

                string fileName = Path.GetFileNameWithoutExtension(pathToGml);
                string extension = Path.GetExtension(pathToGml);
                string folderPath = Path.GetDirectoryName(pathToGml) + "\\";

                Log.LogFileName = folderPath + "LerImport.log";
                Log.log($"Importing {pathToGml}");

                #region Handle bad objects
                string modifiedFileName = Enhance.Run(pathToGml);
                #endregion

                #region Deserialize gml
                var serializer = new XmlSerializer(typeof(Schema.FeatureCollection));
                Schema.FeatureCollection gf;

                using (var fileStream = new FileStream(modifiedFileName, FileMode.Open))
                {
                    gf = (Schema.FeatureCollection)serializer.Deserialize(fileStream);
                }
                #endregion

                #region Create database for 2D ler
                using (Database ler2dDb = new Database(false, true))
                using (Database ler3dDb = new Database(false, true))
                {
                    ler2dDb.ReadDwgFile(@"X:\AutoCAD DRI - 01 Civil 3D\LerImport\Support\LerTemplate.dwt",
                                FileOpenMode.OpenForReadAndAllShare, false, null);
                    ler3dDb.ReadDwgFile(@"X:\AutoCAD DRI - 01 Civil 3D\LerImport\Support\LerTemplate.dwt",
                                FileOpenMode.OpenForReadAndAllShare, false, null);
                    //Build the new future file name of the drawing
                    var query = gf.featureCollection.Where(x => x.item is Schema.Graveforesp);
                    string bemaerkning = default;
                    if (query.Count() > 0)
                    {
                        Schema.Graveforesp gfsp = query.First().item as Schema.Graveforesp;
                        bemaerkning = gfsp.bemaerkning;
                        if (bemaerkning.IsNotNoE()) bemaerkning += "_";
                    }
                    string new2dFilename = $"{folderPath}{bemaerkning ?? ""}LER_2D.dwg";
                    Log.log($"Writing Ler 2D to new dwg file:\n" + $"{new2dFilename}.");
                    string new3dFilename = $"{folderPath}{bemaerkning ?? ""}LER_3D_KUN_TIL_TEST.dwg";
                    Log.log($"Writing Ler 3D to new dwg file:\n" + $"{new3dFilename}.");

                    using (Transaction ler2dTx = ler2dDb.TransactionManager.StartTransaction())
                    using (Transaction ler3dTx = ler3dDb.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            LERImporter.ConsolidatedCreator.CreateLerData(ler2dDb, ler3dDb, gf);
                        }
                        catch (System.Exception ex)
                        {
                            Log.log(ex.ToString());
                            ler2dTx.Abort();
                            ler2dDb.Dispose();
                            ler3dTx.Abort();
                            ler3dDb.Dispose();
                            throw;
                        }

                        ler2dTx.Commit();
                        ler3dTx.Commit();
                    }

                    //Save the new dwg file
                    ler2dDb.SaveAs(new2dFilename, DwgVersion.Current);
                    ler3dDb.SaveAs(new3dFilename, DwgVersion.Current);
                    //ler2dDb.Dispose();
                }
                #endregion

            }
            catch (System.Exception ex)
            {
                Log.log(ex.ToString());
                return;
            }
        }

        /// <command>IGMLBATCH, IMPORTCONSOLIDATEDGMLBATCH</command>
        /// <summary>
        /// Batch-imports consolidated LER GML files from a chosen top folder. Unzips any archives
        /// in-place, finds all "consolidated.gml" files recursively, enhances each with Enhance.Run,
        /// and for each file creates a 3D LER DWG "{Bemærkning}_3DLER.dwg" using the template at
        /// X:\AutoCAD DRI - 01 Civil 3D\LerImport\Support\LerTemplate.dwt. Also builds a combined
        /// Schema.FeatureCollection across all files and creates a single 2D LER DWG "2DLER.dwg"
        /// in the top folder. Progress is logged to LerImport.log in the top folder. Intended to convert
        /// all partial LER2 requests into standardized DWGs in one run.
        /// </summary>
        /// <category>LER2</category>
        [CommandMethod("IGMLBATCH")]
        [CommandMethod("IMPORTCONSOLIDATEDGMLBATCH")]
        public void importconsolidatedbatch()
        {
            try
            {
                #region Get file and folder of gml
                string pathToTopFolder = string.Empty;
                var folderDialog = new Microsoft.Win32.OpenFolderDialog()
                {
                    Title = "Choose folder where gml files are stored: ",
                };
                if (folderDialog.ShowDialog() == true)
                {
                    pathToTopFolder = folderDialog.FolderName;
                }
                else return;

                #region Unzip zip files
                ZipExtractor.UnzipFilesInDirectory(pathToTopFolder);
                #endregion

                var files = Directory.EnumerateFiles(
                    pathToTopFolder, "consolidated.gml", SearchOption.AllDirectories);
                #endregion

                #region Actual converting
                Log.LogFileName = Path.Combine(pathToTopFolder, "LerImport.log");

                #region Enhance gml files and create 3D ler files
                List<string> modFiles = new List<string>();
                Schema.FeatureCollection gfCombined = new Schema.FeatureCollection();
                gfCombined.featureCollection = new List<Schema.FeatureMember>();
                foreach (var file in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    string extension = Path.GetExtension(file);
                    string folderPath = Path.GetDirectoryName(file) + "\\";

                    Log.log($"Importing {file}");
                    string modFile = Enhance.Run(file);
                    modFiles.Add(modFile);

                    var serializer = new XmlSerializer(typeof(Schema.FeatureCollection));

                    using var fs = File.OpenRead(modFile);

                    var settings = new XmlReaderSettings
                    {
                        IgnoreComments = true,
                        IgnoreWhitespace = false
                    };
                    using var reader = XmlReader.Create(fs, settings);

                    var events = new XmlDeserializationEvents
                    {
#if DEBUG
                        //OnUnknownElement = (s, e) =>
                        //                    Log.log($"Unknown element '{e.Element.Name}' at {e.LineNumber}:{e.LinePosition} while deserializing {e.ObjectBeingDeserialized?.GetType().Name ?? "root"}"),
                        //OnUnknownNode = (s, e) =>
                        //    Log.log($"Unknown node '{e.Name}' (type {e.NodeType}) at {e.LineNumber}:{e.LinePosition}"),
                        //OnUnreferencedObject = (s, e) =>
                        //    Log.log($"Unreferenced object id='{e.UnreferencedId}'")
#endif
                    };

                    var gf = (Schema.FeatureCollection)serializer.Deserialize(reader, null, events);


                    //Gather combined file for Ler 2D
                    gfCombined.featureCollection.AddRange(gf.featureCollection);

                    //Create LER 3D file
                    using (Database ler3dDb = new Database(false, true))
                    {
                        ler3dDb.ReadDwgFile(
                            @"X:\AutoCAD DRI - SETUP\Templates\NS_LerTemplate.dwt",
                            FileOpenMode.OpenForReadAndAllShare, false, null);
                        //Build the new future file name of the drawing
                        string new3dFilename = Path.Combine(
                            pathToTopFolder, $"{gf.GetGraveForespBemaerkning()}_3DLER.dwg");
                        Log.log($"Writing Ler 3D to new dwg file:\n" + $"{new3dFilename}.");

                        using (Transaction ler3dTx = ler3dDb.TransactionManager.StartTransaction())
                        {
                            try
                            {
                                LERImporter.ConsolidatedCreator.CreateLerData(null, ler3dDb, gf);
                            }
                            catch (System.Exception ex)
                            {
                                Log.log(ex.ToString());
                                ler3dTx.Abort();
                                ler3dDb.Dispose();
                                throw;
                            }

                            ler3dTx.Commit();
                        }

                        //Save the new dwg file
                        ler3dDb.SaveAs(new3dFilename, DwgVersion.Current);
                    }
                }
                #endregion

                #region Create database for 2D ler
                using (Database ler2dDb = new Database(false, true))
                {
                    ler2dDb.ReadDwgFile(@"X:\AutoCAD DRI - SETUP\Templates\NS_LerTemplate.dwt",
                                FileOpenMode.OpenForReadAndAllShare, false, null);
                    //Build the new future file name of the drawing
                    string new2dFilename = Path.Combine(pathToTopFolder, "2DLER.dwg");
                    Log.log($"Writing Ler 2D to new dwg file:\n" + $"{new2dFilename}.");

                    using (Transaction ler2dTx = ler2dDb.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            LERImporter.ConsolidatedCreator.CreateLerData(ler2dDb, null, gfCombined);
                        }
                        catch (System.Exception ex)
                        {
                            Log.log(ex.ToString());
                            ler2dTx.Abort();
                            ler2dDb.Dispose();
                            throw;
                        }

                        ler2dTx.Commit();
                    }

                    //Save the new dwg file
                    ler2dDb.SaveAs(new2dFilename, DwgVersion.Current);
                }
                #endregion 
                #endregion

            }
            catch (System.Exception ex)
            {
                Log.log(ex.ToString());
                return;
            }
            Log.log("Finished importing LER data.");
        }
    }

    public static class SimpleLogger
    {
        public static bool EchoToEditor { get; set; } = true;
        public static string LogFileName { get; set; } = "C:\\Temp\\LerImportLog.txt";
        public static void log(string msg)
        {
            File.AppendAllLines(LogFileName, new string[] { $"{DateTime.Now}: {msg}" });
            if (EchoToEditor) prdDbg(msg);
        }
    }
}