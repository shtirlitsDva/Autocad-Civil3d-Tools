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
using System.Xml;
using System.Xml.Serialization;
using Newtonsoft.Json;
//using MoreLinq;
//using GroupByCluster;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;

//using static IntersectUtilities.Enums;
//using static IntersectUtilities.HelperMethods;
//using static IntersectUtilities.Utils;
//using static IntersectUtilities.PipeSchedule;

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

namespace LERImporter
{
    public class LerImporter : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\nLER Import Application indlæst.");
        }

        public void Terminate()
        {
        }
        #endregion

        public static readonly string ImplementedVersion = "1.0.1";

        [CommandMethod("TESTLER")]
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
                                      @"\Dev\53296456-7831-4836-95ae-6aeb955daf9c.gml";

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
