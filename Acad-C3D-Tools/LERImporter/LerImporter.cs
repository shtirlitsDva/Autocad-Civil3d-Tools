using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Data;
using System.Xml;
using System.Xml.Serialization;
//using MoreLinq;
//using GroupByCluster;
using IntersectUtilities.UtilsCommon;
using FolderSelect;
using static IntersectUtilities.UtilsCommon.Utils;

//using static IntersectUtilities.Enums;
//using static IntersectUtilities.HelperMethods;
//using static IntersectUtilities.Utils;
//using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

using static IntersectUtilities.UtilsCommon.UtilsDataTables;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = Autodesk.Civil.DatabaseServices.Label;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using Log = LERImporter.SimpleLogger;
using System.Xml.Linq;
using LERImporter.Schema;
using LERImporter.Enhancer;

namespace LERImporter
{
    public class LerImporter : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\nLER Import Application indlæst.");
            doc.Editor.WriteMessage("\nKommando til LER 2.0 -> IMPORTCONSOLIDATEDGML.");

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

        //[CommandMethod("TESTLER")]
        public void testler()
        {

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    string fileName = @"D:\OneDrive - Damgaard Rådgivende Ingeniører ApS\34 Lerimporter" +
                                      //@"\Dev\53296456-7831-4836-95ae-6aeb955daf9c.gml";
                                      @"\Dev\test.gml";

                    Log.log($"Starting import of {Path.GetFileName(fileName)}");
                    Log.log($"Located at {Path.GetDirectoryName(fileName)}");

                    var serializer = new XmlSerializer(typeof(Schema.GraveforespoergselssvarType));
                    Schema.GraveforespoergselssvarType gf;

                    //Schema.GraveforespoergselssvarType gf = new Schema.GraveforespoergselssvarType();
                    using (var fileStream = new FileStream(fileName, FileMode.Open))
                    {
                        gf = (Schema.GraveforespoergselssvarType)serializer.Deserialize(fileStream);
                        //gf = Schema.GraveforespoergselssvarType.Deserialize(fileStream);
                    }

                    gf.WorkingDatabase = localDb;

                    gf.CreateLerData();

                    #region Archive
                    ////Check or create directory
                    //if (!Directory.Exists(@"C:\Temp\"))
                    //    Directory.CreateDirectory(@"C:\Temp\");

                    //string graph = Visualizer.ObjectGraphVisualizer.Visualize(gf);

                    ////Write the collected graphs to one file
                    //using (System.IO.StreamWriter file = new System.IO.StreamWriter($"C:\\Temp\\MyGraph.dot"))
                    //{
                    //    file.WriteLine(graph); // "sb" is the StringBuilder
                    //}

                    //string fileName = @"D:\OneDrive - Damgaard Rådgivende Ingeniører ApS\34 Lerimporter" +
                    //                  @"\Dev\gf.json";
                    //JsonSerializer serializer = new JsonSerializer();
                    //using (StreamWriter sw = new StreamWriter(fileName))
                    //using (JsonWriter writer = new JsonTextWriter(sw))
                    //{
                    //    serializer.Serialize(writer, gf);
                    //} 
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    editor.WriteMessage(ex.ToString());
                    return;
                }
                tx.Commit();
            }
        }

        //[CommandMethod("TESTPS")]
        public void testps()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    string fileName = @"D:\OneDrive - Damgaard Rådgivende Ingeniører ApS\34 Lerimporter" +
                                      @"\Dev\53296456-7831-4836-95ae-6aeb955daf9c.gml";
                    //"\Dev\test.gml";

                    var serializer = new XmlSerializer(typeof(Schema.GraveforespoergselssvarType));
                    Schema.GraveforespoergselssvarType gf;

                    using (var fileStream = new FileStream(fileName, FileMode.Open))
                    {
                        gf = (Schema.GraveforespoergselssvarType)serializer.Deserialize(fileStream);
                    }

                    gf.WorkingDatabase = localDb;

                    gf.TestPs();
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
        //[CommandMethod("IMPORTGMLLERDATA")]
        public void importgmllerdata()
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
                if (dialog.ShowDialog() == DialogResult.OK) pathToGml = dialog.FileName;
                else return;

                string folderPath = Path.GetDirectoryName(pathToGml) + "\\";
                #endregion

                Log.LogFileName = folderPath + "LerImport.log";
                Log.log($"Importing {pathToGml}");

                #region Deserialize gml
                var serializer = new XmlSerializer(typeof(Schema.GraveforespoergselssvarType));
                Schema.GraveforespoergselssvarType gf;

                using (var fileStream = new FileStream(pathToGml, FileMode.Open))
                {
                    gf = (Schema.GraveforespoergselssvarType)serializer.Deserialize(fileStream);
                }
                #endregion

                #region Create database for 2D ler
                using (Database ler2dDb = new Database(false, true))
                {
                    ler2dDb.ReadDwgFile(@"X:\AutoCAD DRI - 01 Civil 3D\LerImport\Support\LerTemplate.dwt",
                                FileOpenMode.OpenForReadAndAllShare, false, null);
                    //Build the new future file name of the drawing
                    string newFilename = $"{folderPath}{gf.Owner}_2D.dwg";
                    Log.log($"Writing Ler 2D to new dwg file:\n" + $"{newFilename}.");
                    //ler2dDb.SaveAs(newFilename, true, DwgVersion.Newest, null);

                    using (Transaction ler2dTx = ler2dDb.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            gf.WorkingDatabase = ler2dDb;
                            gf.CreateLerData();
                            gf.WorkingDatabase = null;
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
                    ler2dDb.SaveAs(newFilename, DwgVersion.Current);
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

        [CommandMethod("IMPORTCONSOLIDATEDGML")]
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
                if (dialog.ShowDialog() == DialogResult.OK) pathToGml = dialog.FileName;
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

        [CommandMethod("IGMLBATCH")]
        [CommandMethod("IMPORTCONSOLIDATEDGMLBATCH")]
        public void importconsolidatedbatch()
        {
            try
            {
                #region Get file and folder of gml
                string pathToTopFolder = string.Empty;
                FolderSelectDialog fsd = new FolderSelectDialog()
                {
                    Title = "Choose folder where gml files are stored: ",
                    //InitialDirectory = @"C:\"
                };
                if (fsd.ShowDialog(IntPtr.Zero))
                {
                    pathToTopFolder = fsd.FileName + "\\";
                }
                else return;

                #region Unzip zip files
                ZipExtractor.UnzipFilesInDirectory(pathToTopFolder);
                #endregion

                var files = Directory.EnumerateFiles(
                    pathToTopFolder, "consolidated.gml", SearchOption.AllDirectories);
                #endregion

                #region Actual converting
                Log.LogFileName = pathToTopFolder + "LerImport.log";

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
                    Schema.FeatureCollection gf;
                    using (var fileStream = new FileStream(modFile, FileMode.Open))
                    {
                        gf = (Schema.FeatureCollection)serializer.Deserialize(fileStream);
                    }

                    //Gather combined file for Ler 2D
                    gfCombined.featureCollection.AddRange(gf.featureCollection);

                    //Create LER 3D file
                    using (Database ler3dDb = new Database(false, true))
                    {
                        ler3dDb.ReadDwgFile(
                            @"X:\AutoCAD DRI - 01 Civil 3D\LerImport\Support\LerTemplate.dwt",
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
                    ler2dDb.ReadDwgFile(@"X:\AutoCAD DRI - 01 Civil 3D\LerImport\Support\LerTemplate.dwt",
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