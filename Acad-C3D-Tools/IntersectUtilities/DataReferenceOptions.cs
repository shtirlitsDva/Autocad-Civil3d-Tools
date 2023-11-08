using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using IntersectUtilities.Forms;

namespace IntersectUtilities
{
    public class DataReferencesOptions
    {
        public const string PathToStierCsv = "X:\\AutoCAD DRI - 01 Civil 3D\\Stier.csv";
        public string ProjectName;
        public string EtapeName;
        public static string GetProjectName()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Editor editor = docCol.MdiActiveDocument.Editor;

            if (!File.Exists(PathToStierCsv))
                throw new System.Exception(
                    "X:\\AutoCAD DRI - 01 Civil 3D\\Stier.csv findes ikke!");

            #region Read Csv for paths
            string pathStier = "X:\\AutoCAD DRI - 01 Civil 3D\\Stier.csv";
            System.Data.DataTable dtStier =
                CsvReader.ReadCsvToDataTable(pathStier, "Stier");
            #endregion

            HashSet<string> kwds = new HashSet<string>();
            foreach (DataRow row in dtStier.Rows)
                kwds.Add(((string)row["PrjId"]));

            var sgf = new StringGridForm(kwds, 4, "VÆLG PROJEKT");
            sgf.ShowDialog();

            if (kwds.Contains(sgf.SelectedValue)) return sgf.SelectedValue;
            else throw new System.Exception($"No project of this name \"{sgf.SelectedValue}\" found!");

            #region Old keywords method
            //string msg = "\nVælg projekt [";
            //string keywordsJoined = string.Join("/", kwds);
            //msg = msg + keywordsJoined + "]: ";

            //string displayKewords = string.Join(" ", kwds);

            //PromptKeywordOptions pKeyOpts = new PromptKeywordOptions(msg, displayKewords);
            ////pKeyOpts.Message = "\nVælg projekt: ";
            ////foreach (string kwd in kwds)
            ////{
            ////    pKeyOpts.Keywords.Add(kwd, kwd, kwd);
            ////}
            //pKeyOpts.AllowNone = true;
            //pKeyOpts.Keywords.Default = kwds.First();
            ////pKeyOpts.AllowNone = false;
            ////pKeyOpts.AllowArbitraryInput = true;
            ////for (int i = 0; i < pKeyOpts.Keywords.Count; i++)
            ////{
            ////    prdDbg("\nLocal name: " + pKeyOpts.Keywords[i].LocalName);
            ////    prdDbg("\nGlobal name: " + pKeyOpts.Keywords[i].GlobalName);
            ////    prdDbg("\nDisplay name: " + pKeyOpts.Keywords[i].DisplayName);
            ////}

            ////pKeyOpts.Keywords.Default = kwds[0];
            //PromptResult pKeyRes = editor.GetKeywords(pKeyOpts);
            ////For some reason keywords returned are only the first part, so this is a workaround
            ////Depends on what input is
            ////The project name must start with the project number
            ////If the code returns wrong values, there must be something wrong with project names
            ////Like same project number and/or occurence of same substring in two or more keywords
            ////This is a mess...
            //string returnedPartOfTheKeyword = pKeyRes.StringResult;
            //foreach (string kwd in kwds)
            //{
            //    if (kwd.Contains(returnedPartOfTheKeyword)) return kwd;
            //}
            //throw new System.Exception("No project of this name found!"); 
            #endregion
        }
        public static string GetEtapeName(string projectName)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Editor editor = docCol.MdiActiveDocument.Editor;

            #region Read Csv for paths
            string pathStier = "X:\\AutoCAD DRI - 01 Civil 3D\\Stier.csv";
            System.Data.DataTable dtStier = CsvReader.ReadCsvToDataTable(pathStier, "Stier");
            #endregion

            var query = dtStier.AsEnumerable()
                .Where(row => (string)row["PrjId"] == projectName);

            HashSet<string> kwds = new HashSet<string>();
            foreach (DataRow row in query)
                kwds.Add((string)row["Etape"]);

            var sgf = new StringGridForm(kwds, 4, "VÆLG ETAPE");
            sgf.ShowDialog();

            if (kwds.Contains(sgf.SelectedValue)) return sgf.SelectedValue;
            else throw new System.Exception($"No etape of this name \"{sgf.SelectedValue}\" found!");

            #region Old method
            //PromptKeywordOptions pKeyOpts = new PromptKeywordOptions("");
            //pKeyOpts.Message = "\nVælg etape: ";
            //foreach (string kwd in kwds)
            //{
            //    pKeyOpts.Keywords.Add(kwd);
            //}
            //pKeyOpts.AllowNone = true;
            //pKeyOpts.Keywords.Default = kwds.First();
            //PromptResult pKeyRes = editor.GetKeywords(pKeyOpts);

            //return pKeyRes.StringResult; 
            #endregion
        }
        public DataReferencesOptions()
        {
            ProjectName = GetProjectName();
            EtapeName = GetEtapeName(ProjectName);
        }
        public DataReferencesOptions(string projectName, string etapeName)
        {
            ProjectName = projectName;
            EtapeName = etapeName;
        }
    }
}
