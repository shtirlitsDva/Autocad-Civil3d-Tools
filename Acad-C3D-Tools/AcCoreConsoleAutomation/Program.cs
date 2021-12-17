﻿using System;
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
            string pathToFileList = @"X:\037-1178 - Gladsaxe udbygning - Dokumenter\01 Intern\" +
                          @"02 Tegninger\01 Autocad - xxx\Etape 1.2\Sheets\fileList.txt";
            string path = @"X:\037-1178 - Gladsaxe udbygning - Dokumenter\01 Intern\" +
                          @"02 Tegninger\01 Autocad - xxx\Etape 1.2\Sheets\";
            List<string> names = File.ReadAllLines(pathToFileList).ToList();

            foreach (string name in names)
            {
                string fullPath = path + name;

                //Process acad = new Process();
                //acad.StartInfo.FileName = @"C:\Program Files\Autodesk\AutoCAD 2022\AcCoreConsole.exe";
                string fileName = @"C:\Program Files\Autodesk\AutoCAD 2022\AcCoreConsole.exe";
                //acad.StartInfo.Arguments = "/ld \"C:\\Program Files\\Autodesk\\AutoCAD 2022\\AecBase.dbx\" " +
                string arguments = $"/i \"{fullPath}\" " +
                                    "/s \"X:\\AutoCAD DRI - 01 Civil 3D\\Dev\\saveexit.scr\" " +
                                    "/product C3D " +
                                    "/language en - US " +
                                    "/p \"<<C3D_Metric>>\" " +
                                    "/loadmodule \"C:\\Program Files\\Autodesk\\AutoCAD 2022\\AecBase.dbx\"";
                Process acad = Process.Start(fileName, arguments);
                acad.WaitForExit();
            }
        }
    }
}
