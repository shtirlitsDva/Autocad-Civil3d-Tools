using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using System.Reflection;

using Microsoft.VisualBasic.FileIO;

namespace DRILOAD
{
    public partial class NsLoad : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\nUse NSLOAD to load Norsyn programs!");
        }

        public void Terminate()
        {
        }
        #endregion

        [CommandMethod("NSLOAD")]
        public void nsload()
        {
            string csvPath = @"X:\AutoCAD DRI - 01 Civil 3D\NetloadV2\Register-2025.csv";
            var dllDict = LoadRegisterCsv(csvPath);

            var kwd = IntersectUtilities.StringGridFormCaller.Call(
                dllDict.Keys, "Select Norsyn program to load: ");

            if (kwd == null) return;

            string pathToLoad = dllDict[kwd];

            if (!System.IO.File.Exists(pathToLoad))
                throw new System.Exception($"DLL file {pathToLoad} does not exist!");

            try
            {
                Assembly.LoadFrom(pathToLoad);
            }
            catch (System.Exception ex)
            {
                Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage(
                  "\nCannot load {0}: {1}",
                  pathToLoad,
                  ex.Message
                );
                Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage(
                  "\n" + ex.ToString()
                );
                return;
            }

            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage(
                $"DLL {System.IO.Path.GetFileName(pathToLoad)} loaded!");
        }

        /// <summary>
        /// Reads the register CSV and returns a dictionary of DisplayName -> Path.
        /// </summary>
        private static Dictionary<string, string> LoadRegisterCsv(string csvPath)
        {
            var dict = new Dictionary<string, string>();

            using (var parser = new TextFieldParser(csvPath))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(";");
                parser.HasFieldsEnclosedInQuotes = true;

                // Skip header
                if (!parser.EndOfData)
                    parser.ReadFields();

                while (!parser.EndOfData)
                {
                    string[]? fields = parser.ReadFields();
                    if (fields != null && fields.Length >= 2)
                    {
                        string displayName = fields[0].Trim();
                        string path = fields[1].Trim();
                        if (!string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(path))
                            dict[displayName] = path;
                    }
                }
            }

            return dict;
        }
    }
}
