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
            DataTable dt = CsvReader.ReadCsvToDataTable(@"X:\AutoCAD DRI - 01 Civil 3D\Stier.csv", "Stier");
            List<string> list = dt.AsEnumerable().Select(x => x["Fremtid"].ToString()).ToList();

            List<string> faulty = new List<string>();

            foreach (string s in list)
                if (!File.Exists(s)) faulty.Add(s);

            //log file name
            string logFileName = @"X:\0371-1158 - Gentofte Fase 4 - Dokumenter\02 Ekstern\" +
                                 @"01 Gældende tegninger\01 GIS input\02 Trace shape\export.log";
            if (faulty.Count > 0)
            {
                list = list.Except(faulty).ToList();

                foreach (string s in faulty)
                {
                    File.AppendAllLines(logFileName, new string[] {$"{DateTime.Now}: Failed to find file {s}! Removing from export list..." } );
                }
            }
            else
            {
                File.AppendAllLines(logFileName, new string[] { $"{DateTime.Now}: All files present." });
            }
            
            foreach (string fileName in list)
            {
                Console.WriteLine("Processing " + Path.GetFileName(fileName));

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
            Console.WriteLine("Export completed!");
            Console.ReadKey();
        }
    }
}
