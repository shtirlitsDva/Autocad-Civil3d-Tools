using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;

using IntersectUtilities.Forms;
using IntersectUtilities.UtilsCommon;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

using static IntersectUtilities.UtilsCommon.DataManager.StierManager;

namespace IntersectUtilities.UtilsCommon.DataManager
{
    public class DataReferencesOptions
    {        
        public string ProjectName;
        public string EtapeName;
        public static string GetProjectName()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Editor editor = docCol.MdiActiveDocument.Editor;

            var sgf = StringGridFormCaller.Call(Projects(), "VÆLG PROJEKT");
            if (sgf == null) throw new Exception("Cancelled!");
            return sgf;
        }
        public static string GetEtapeName(string projectName)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Editor editor = docCol.MdiActiveDocument.Editor;

            var sgf = StringGridFormCaller.Call(Phases(), "VÆLG ETAPE");
            if (sgf == null) throw new Exception("Cancelled!");
            return sgf;
        }
        public DataReferencesOptions(bool useAuto = true)
        {
            if (useAuto)
            {
                var dwgName = Application.DocumentManager.MdiActiveDocument.Database.Filename;                
                var results = DetectProjectAndEtape(dwgName);

                if (results.Count() > 0)
                {
                    if (results.Count() == 1)
                    {
                        ProjectName = results.First().ProjectId;
                        EtapeName = results.First().EtapeId;
                        return;
                    }
                    else
                    {
                        var dict = results.ToDictionary(x => $"{x.ProjectId}:{x.EtapeId}", x => x);
                        var choice = StringGridFormCaller.Call(dict.Keys.Order(), "Matched projects, choose one:");
                        if (choice == null)
                            throw new Exception("No project/etape selected!");

                        var pe = dict[choice];
                        ProjectName = pe.ProjectId;
                        EtapeName = pe.EtapeId;
                        return;
                    }
                }
            }

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