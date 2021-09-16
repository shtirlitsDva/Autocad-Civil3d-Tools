using System;
using System.Data;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using IntersectUtilities;

namespace ExportShapeFilesBatch
{
    class ExportShapeFilesBatch
    {
        static void Main(string[] args)
        {
            //log file name
            string logFileName = @"C:\1\DRI\0371-1158 - Gentofte Fase 4 - Dokumenter\02 Ekstern\" +
                                 @"01 Gældende tegninger\01 GIS input\02 Trace shape\export.log";
            File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: -~*~- Starting new export -~*~-" });

            DataTable dt = CsvReader.ReadCsvToDataTable(@"C:\1\DRI\AutoCAD DRI - 01 Civil 3D\Stier.csv", "Stier");

            if (dt == null)
            {
                File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: Datatable creation failed (null)! Aborting..." });
                return;
            }
            else if (dt.Rows.Count == 0)
            {
                File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: Datatable creation failed (0 rows)! Aborting..." });
                return;
            }
            else
            {
                File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: Datatable created with {dt.Rows.Count} record(s)." });
            }

            List<string> list = dt.AsEnumerable()
                .Where(x=>x["PrjId"].ToString() == "Gentofte1158")
                .Select(x => x["Fremtid"].ToString()).ToList();

            List<string> faulty = new List<string>();

            foreach (string s in list)
                if (!File.Exists(s)) faulty.Add(s);

            if (faulty.Count > 0)
            {
                //Remove faulty entries from the list to execute
                list = list.Except(faulty).ToList();

                foreach (string s in faulty)
                {
                    File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: Failed to find file {s}! Removing from export list..." });
                }
                File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: {list.Count} file(s) left in export list." });
            }
            else
            {
                File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: All files present." });
            }

            foreach (string fileName in list)
            {
                File.AppendAllLines(logFileName, new string[] { $"Processing " + Path.GetFileName(fileName) });

                Process acad = new Process();
                acad.StartInfo.FileName = @"C:\Program Files\Autodesk\AutoCAD 2022\acad.exe";
                acad.StartInfo.Arguments = "/ld \"C:\\Program Files\\Autodesk\\AutoCAD 2022\\AecBase.dbx\" " +
                                           "/p \"<<C3D_Metric>>\" /product C3D /language en-US " +
                                          $"\"{fileName}\" " +
                                           "/b \"X:\\AutoCAD DRI - 01 Civil 3D\\Export\\ExportShapeFiles.scr\" " +
                                           "/nologo";
                acad.Start();
                acad.WaitForExit();
            }
            File.AppendAllLines(logFileName, new string[] { "Export completed!" });
            //Console.ReadKey();
        }
    }
}
