using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Reflection;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;

using static IntersectUtilities.CsvReader;
using static IsoTools.Utils;
using System.IO;
using Ude;

namespace IsoTools
{
    public partial class IsoTools : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            prdDbg("\nIsoTools loaded!");

#if DEBUG
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(Debug_AssemblyResolve);
#endif
        }

        public void Terminate()
        {
        }
        #endregion

        [CommandMethod("ISOCONVERTREPORTSTOCSV")]
        public void isoconvertreportstocsv()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Start processing
                    var brefs = localDb.GetBlockReferenceByName("Title Block");
                    if (brefs.Count == 0) throw new System.Exception("No block named Title Block found!");
                    var br = brefs.FirstOrDefault();

                    string pipelineName = br.GetAttributeStringValue("LINE_NAME");
                    string drawingDir = Path.GetDirectoryName(localDb.Filename);
                    //Create reference to the reports directory
                    var reportsDir = Path.Combine(Directory.GetParent(drawingDir).FullName, "Reports");

                    #region Process Materials
                    string matFile = Path.Combine(reportsDir, pipelineName + "-MATERIAL.txt");
                    if (!File.Exists(matFile)) throw new System.Exception("Material file not found!");

                    Encoding encoding = DetectFileEncoding(matFile);

                    var cws = new[] { 6, 15, 52, 20 };
                    
                    var data = ConvertFixedWidthToCollection(matFile, cws);
                    //foreach (var item in data) prdDbg(string.Join("|", item));
                    var hdngs = data[0];
                    data.RemoveAt(0);




                    #endregion

                    #region Process welds
                    string drawingXml = Path.Combine(drawingDir, pipelineName + ".xml");
                    if (!File.Exists(drawingXml)) throw new System.Exception("Drawing XML file not found!");

                    XDocument xmlDoc = XDocument.Load(drawingXml);
                    var outputFiles = xmlDoc.Descendants("OUTPUT-FILE").Select(node => node.Value.Trim());

                    //foreach (string file in outputFiles) prdDbg(file);
                    #endregion

                    #region Utils
                    List<string[]> ConvertFixedWidthToCollection(string inputFilePath, int[] widths)
                    {
                        var records = new List<string[]>();
                        var lines = File.ReadAllLines(inputFilePath);

                        prdDbg(string.Join(", ", lines.Select(x => x.Length)));

                        foreach (var line in lines)
                        {
                            var record = new List<string>();
                            int position = 0;

                            for (int i = 0; i < widths.Length; i++)
                            {
                                if (position + widths[i] > line.Length) // Check if the width goes out of the string's bounds
                                {
                                    string field = line.Substring(position).Trim(); // Take the rest of the string
                                    record.Add(field);
                                    break; // Exit the loop after the last field
                                }
                                else
                                {
                                    string field = line.Substring(position, widths[i]).Trim();
                                    record.Add(field);
                                    position += widths[i];
                                }
                            }

                            records.Add(record.ToArray());
                        }

                        return records;
                    }

                    Encoding DetectFileEncoding(string filePath)
                    {
                        using (FileStream fs = File.OpenRead(filePath))
                        {
                            ICharsetDetector cd = new CharsetDetector();
                            cd.Feed(fs);
                            cd.DataEnd();

                            if (cd.Charset != null && cd.Confidence > 0.5) // You can adjust the confidence threshold
                            {
                                string file = Path.GetFileName(filePath);
                                prdDbg($"File {file} detected encoding: {cd.Charset} with confidence {cd.Confidence}");
                                return Encoding.GetEncoding(cd.Charset);
                            }
                            throw new System.Exception("Material file could not establish an encoding!");
                        }
                    }
                    #endregion

                    #region testing
                    //Testing filename reveals that the new drawing
                    //knows its' new location
                    //prdDbg(localDb.Filename);
                    //prdDbg(localDb.OriginalFileName); 
                    #endregion
                    #endregion
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
