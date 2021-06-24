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
