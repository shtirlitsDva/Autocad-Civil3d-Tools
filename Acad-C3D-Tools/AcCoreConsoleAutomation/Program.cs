using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AcCoreConsoleAutomation
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string pathToFileList =
                @"X:\069-1306 - Fase 1 - udbygning af fjernvarme - Dokumenter\01 Intern\02 Tegninger\01 Autocad - xxx\Sheets\1.2\fileList.txt";
            string path = Path.GetDirectoryName(pathToFileList) + "\\";
            List<string> names = File.ReadAllLines(pathToFileList).ToList();

            foreach (string name in names)
            {
                string fullPath = path + name;

                //Process acad = new Process();
                //acad.StartInfo.FileName = @"C:\Program Files\Autodesk\AutoCAD 2022\AcCoreConsole.exe";
                string fileName = @"C:\Program Files\Autodesk\AutoCAD 2023\AcCoreConsole.exe";
                //acad.StartInfo.Arguments = "/ld \"C:\\Program Files\\Autodesk\\AutoCAD 2022\\AecBase.dbx\" " +
                string arguments = $"/i \"{fullPath}\" " +
                                    "/s \"X:\\AutoCAD DRI - 01 Civil 3D\\Dev\\saveexit.scr\" " +
                                    "/product C3D " +
                                    "/language en - US " +
                                    "/p \"<<C3D_Metric>>\" " +
                                    "/loadmodule \"C:\\Program Files\\Autodesk\\AutoCAD 2023\\AecBase.dbx\"";
                Console.WriteLine("Processing: " + name);
                Process acad = Process.Start(fileName, arguments);
                acad.WaitForExit();
            }
        }
    }
}
