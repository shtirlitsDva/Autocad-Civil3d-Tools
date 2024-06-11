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

            Encoding encoding = Encoding.UTF8;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var brefs = localDb.GetBlockReferenceByName("Title Block");
                    if (brefs.Count == 0) throw new System.Exception("No block named Title Block found!");
                    var localTb = brefs.FirstOrDefault();

                    string pipelineName = localTb.GetAttributeStringValue("LINE_NAME");
                    string drawingDir = Path.GetDirectoryName(localDb.Filename);
                    //Create reference to the reports directory
                    var reportsDir = Path.Combine(Directory.GetParent(drawingDir).FullName, "Reports");

                    #region Process Materials
                    string matFile = Path.Combine(reportsDir, pipelineName + "-MATERIAL.txt");
                    if (!File.Exists(matFile)) throw new System.Exception("Material file not found!");

                    //Encoding encoding = DetectFileEncoding(matFile);

                    var cws = new[] { 6, 15, 52, 20 };

                    var data = ConvertFixedWidthToCollection(matFile, cws);
                    //Handle the M in the last column
                    foreach (var item in data)
                    {
                        if (item.Last().Contains("M"))
                        {
                            var split = item.Last().Split(' ');
                            item[item.Count - 1] = split[0];
                            item.Add(split[1]);
                        }
                    }

                    File.WriteAllLines(
                        Path.Combine(reportsDir, Path.GetFileNameWithoutExtension(matFile) + ".csv"),
                        data.Select(x => string.Join(";", x)),
                        new UTF8Encoding(true)
                        );
                    #endregion

                    #region Process Cutlist
                    string cutFile = Path.Combine(reportsDir, pipelineName + "-CUT_LIST.txt");
                    if (!File.Exists(cutFile)) throw new System.Exception("Cut list file not found!");

                    //encoding = DetectFileEncoding(cutFile);

                    cws = new[] { 5, 9, 11, 100 };

                    data = ConvertFixedWidthToCollection(cutFile, cws);

                    File.WriteAllLines(
                        Path.Combine(reportsDir, Path.GetFileNameWithoutExtension(cutFile) + ".csv"),
                        data.Select(x => string.Join(";", x)),
                        new UTF8Encoding(true)
                        );
                    #endregion

                    #region Process welds
                    string drawingXml = Path.Combine(drawingDir, pipelineName + ".xml");
                    if (!File.Exists(drawingXml)) throw new System.Exception("Drawing XML file not found!");

                    XDocument xmlDoc = XDocument.Load(drawingXml);
                    var outputFiles = xmlDoc.Descendants("OUTPUT-FILE")
                        .Select(node => node.Value.Trim()).ToList();

                    //Remove the open file from the list
                    outputFiles.RemoveAt(outputFiles.IndexOf(localDb.Filename));

                    //Gather the data about the blocks and their UCIs
                    //Start with localDb
                    var blockUcis = new Dictionary<string, string>();

                    #region Populate blockUcis by reading local and external files
                    //Determine blad number
                    var localTexts = localDb.HashSetOfType<DBText>(tx);
                    string localBlad = "";
                    foreach (var txt in localTexts)
                    {
                        if ((int)txt.Position.X == 395 && (int)txt.Position.Y == 17)
                        {
                            prdDbg($"Blad {txt.TextString} text found!");
                            localBlad = txt.TextString;
                        }
                    }
                    if (localBlad == "")
                        throw new System.Exception($"Blad number could not be determined for file: " +
                            $"{Path.GetFileName(localDb.Filename)}!");

                    //Cache all UCIS and blad numbers
                    var localBrs = localDb.HashSetOfType<BlockReference>(tx);
                    foreach (var br in localBrs)
                    {
                        try
                        {
                            if (br.GetAttributeStringValue("UCI").IsNoE()) continue;
                            blockUcis.Add(
                                br.GetAttributeStringValue("UCI"), localBlad);
                        }
                        catch (System.Exception)
                        {
                            prdDbg($"UCI {br.GetAttributeStringValue("UCI")} alrady exists!");
                            throw;
                        }
                    }

                    //Process the rest of the files
                    foreach (var file in outputFiles)
                    {
                        var db = new Database(false, true);
                        db.ReadDwgFile(file, FileOpenMode.OpenForReadAndAllShare, false, "");

                        using (Transaction t = db.TransactionManager.StartTransaction())
                        {
                            try
                            {
                                string blad = "";
                                var texts = db.HashSetOfType<DBText>(t);
                                foreach (var txt in texts)
                                {
                                    if ((int)txt.Position.X == 395 && (int)txt.Position.Y == 17)
                                    {
                                        prdDbg($"Blad {txt.TextString} text found!");
                                        blad = txt.TextString;
                                    }
                                }
                                if (blad == "")
                                    throw new System.Exception($"Blad number could not be determined for file: " +
                                        $"{Path.GetFileName(localDb.Filename)}!");

                                var brs = db.HashSetOfType<BlockReference>(t);
                                foreach (var br in brs)
                                {
                                    if (br.GetAttributeStringValue("UCI").IsNoE()) continue;
                                    blockUcis.Add(
                                        br.GetAttributeStringValue("UCI"), blad);
                                }
                            }
                            catch (System.Exception)
                            {
                                prdDbg($"Processing failed for file {Path.GetFileName(file)}!");
                                t.Abort();
                                t.Dispose();
                                db.Dispose();
                                throw;
                            }
                            t.Abort();
                        }
                        db.Dispose();
                    }
                    #endregion

                    //Now process the welds text file
                    string wldFile = Path.Combine(reportsDir, pipelineName + "-WELD_LIST.txt");
                    if (!File.Exists(wldFile)) throw new System.Exception("Weld list file not found!");

                    //encoding = DetectFileEncoding(wldFile);

                    cws = new[] { 10, 10, 100 };

                    data = ConvertFixedWidthToCollection(wldFile, cws);

                    data[0][2] = "Tegning";

                    foreach (var d in data.Skip(1))
                    {
                        var uci = d[2];
                        if (blockUcis.ContainsKey(uci))
                        {
                            d[2] = pipelineName + " Blad " + blockUcis[uci];
                        }
                        else prdDbg($"Weld UCI {uci} not found in drawings!");
                    }

                    SortListAsIntsKeepingHeader(data);

                    File.WriteAllLines(
                        Path.Combine(reportsDir, Path.GetFileNameWithoutExtension(wldFile) + ".csv"),
                        data.Select(x => string.Join(";", x)),
                        new UTF8Encoding(true)
                        );
                    #endregion

                    #region Utils
                    void SortListAsIntsKeepingHeader(List<List<string>> lists)
                    {
                        if (lists.Count > 1)
                        {
                            var sorted = lists.Skip(1)
                                .OrderBy(list => int.Parse(list[0]))
                                .ToList();
                            lists.RemoveRange(1, lists.Count - 1);
                            lists.AddRange(sorted);
                        }
                    }
                    List<List<string>> ConvertFixedWidthToCollection(string inputFilePath, int[] widths)
                    {
                        var records = new List<List<string>>();
                        var lines = File.ReadAllLines(inputFilePath);

                        //prdDbg(string.Join(", ", lines.Select(x => x.Length)));

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

                            records.Add(record);
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
                            throw new System.Exception($"{Path.GetFileName(filePath)}" +
                                $" file could not establish an encoding!");
                        }
                    }
                    #endregion

                    #region testing
                    //Testing filename reveals that the new drawing
                    //knows its' new location
                    //prdDbg(localDb.Filename);
                    //prdDbg(localDb.OriginalFileName); 
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
