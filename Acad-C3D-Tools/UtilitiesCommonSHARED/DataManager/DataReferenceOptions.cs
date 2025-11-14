using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;

using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;

using System;
using System.Linq;

using IntersectUtilities;
using IntersectUtilities.UtilsCommon.Enums;

using static IntersectUtilities.UtilsCommon.DataManager.StierManager;

namespace IntersectUtilities.UtilsCommon.DataManager
{
    public class DataReferencesOptions
    {
        private string _projectId;
        private string _etapeId;
        public string ProjectName { get => _projectId; }
        public string EtapeName { get => _etapeId; }
        public string GetProjectName()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Editor editor = docCol.MdiActiveDocument.Editor;

            var sgf = StringGridFormCaller.Call(Projects(), "VÆLG PROJEKT");
            if (sgf == null) throw new Exception("Cancelled!");
            _projectId = sgf;
            return sgf;
        }
        public string GetEtapeName(string projectName)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Editor editor = docCol.MdiActiveDocument.Editor;

            var sgf = StringGridFormCaller.Call(PhasesForProject(_projectId), "VÆLG ETAPE");
            if (sgf == null) throw new Exception("Cancelled!");
            _etapeId = sgf;
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
                        _projectId = results.First().ProjectId;
                        _etapeId = results.First().EtapeId;
                        return;
                    }
                    else
                    {
                        var dict = results.ToDictionary(x => $"{x.ProjectId}:{x.EtapeId}", x => x);
                        var choice = StringGridFormCaller.Call(dict.Keys.Order(), "Matched projects, choose one:");
                        if (choice == null)
                            throw new Exception("No project/etape selected!");

                        var pe = dict[choice];
                        _projectId = pe.ProjectId;
                        _etapeId = pe.EtapeId;
                        return;
                    }
                }
            }

            _projectId = GetProjectName();
            _etapeId = GetEtapeName(ProjectName);
        }
        public DataReferencesOptions(string projectName, string etapeName)
        {
            _projectId = projectName;
            _etapeId = etapeName;
        }
    }
}