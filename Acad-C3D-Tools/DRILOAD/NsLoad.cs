using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using static IntersectUtilities.CsvReader;
using Autodesk.AutoCAD.EditorInput;
using System.Reflection;

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
            System.Data.DataTable dt = ReadCsvToDataTable(
                @"X:\AutoCAD DRI - 01 Civil 3D\NetloadV2\Register-2025.csv", "Register");

            Dictionary<string, string> dllDict = new Dictionary<string, string>();

            foreach (System.Data.DataRow row in dt.Rows)
                dllDict.Add(row["DisplayName"].ToString(), row["Path"].ToString());

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

        [CommandMethod("NSLOADALL")]
        public void nsloadall()
        {
            System.Data.DataTable dt = ReadCsvToDataTable(
                @"X:\AutoCAD DRI - 01 Civil 3D\NetloadV2\Register-2025.csv", "Register");

            Dictionary<string, string> dllDict = new Dictionary<string, string>();

            foreach (System.Data.DataRow row in dt.Rows)
                dllDict.Add(row["DisplayName"].ToString(), row["Path"].ToString());

            foreach (var dllFile in dllDict)
            {
                string pathToLoad = dllFile.Value;

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
        }

        //private static string GetKeywords(string message, ICollection<string> keywords)
        //{
        //    if (keywords.Count == 0) return null;

        //    string messageAndKeywords = message + " [";
        //    messageAndKeywords += string.Join("/", keywords.ToArray());
        //    messageAndKeywords += "]";

        //    string globalKeywords = string.Join(" ", keywords.ToArray());

        //    var ed = Application.DocumentManager.MdiActiveDocument.Editor;
        //    var opt = new PromptKeywordOptions(message);
        //    opt.AllowNone = true;

        //    opt.SetMessageAndKeywords(messageAndKeywords, globalKeywords);
        //    opt.Keywords.Default = keywords.FirstOrDefault();

        //    var res = ed.GetKeywords(opt);
        //    if (res.Status == PromptStatus.OK)
        //    {
        //        return res.StringResult;
        //    }

        //    return null;
        //}
    }
}

