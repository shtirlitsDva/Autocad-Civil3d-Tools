using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.EditorInput;
using EnvDTE;

namespace DevReload
{
    public static class DevReloadService
    {
        private const string SolutionFolderKind = "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}";

        public static string? FindAndBuild(string projectName, Editor? ed)
        {
            using var wc = new WaitCursorScope();

            // 1. Get running VS instances
            var vsInstances = VsInstanceFinder.GetRunningVSInstances();
            if (vsInstances.Count == 0)
            {
                ed?.WriteMessage("\nNo running Visual Studio instances found.");
                return null;
            }

            // 2. Find project across all VS instances
            var matches = new List<(string solutionName, _DTE dte, Project project)>();

            foreach (var kvp in vsInstances)
            {
                _DTE dte = kvp.Value;
                try
                {
                    if (string.IsNullOrEmpty(dte.Solution?.FullName))
                        continue;

                    foreach (Project prj in dte.Solution.Projects)
                    {
                        SearchProject(prj, projectName, dte, matches);
                    }
                }
                catch
                {
                    // Skip VS instances without loaded solutions
                }
            }

            if (matches.Count == 0)
            {
                ed?.WriteMessage($"\nProject '{projectName}' not found in any running Visual Studio instance.");
                return null;
            }

            // 3. Select the right match
            _DTE targetDte;
            Project targetProject;

            if (matches.Count == 1)
            {
                targetDte = matches[0].dte;
                targetProject = matches[0].project;
                ed?.WriteMessage($"\nFound '{projectName}' in '{matches[0].solutionName}'.");
            }
            else
            {
                // Multiple matches - ask user via AutoCAD Editor keywords
                var options = new PromptKeywordOptions(
                    $"\nProject '{projectName}' found in {matches.Count} instances. Select:");

                for (int i = 0; i < matches.Count; i++)
                {
                    string keyword = SanitizeKeyword(matches[i].solutionName);
                    options.Keywords.Add(keyword);
                    ed?.WriteMessage($"\n  [{keyword}] {matches[i].solutionName}");
                }

                options.AllowNone = false;

                PromptResult result = ed!.GetKeywords(options);
                if (result.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\nCancelled.");
                    return null;
                }

                var selected = matches.FirstOrDefault(m =>
                    SanitizeKeyword(m.solutionName) == result.StringResult);

                if (selected.dte == null)
                {
                    ed.WriteMessage("\nInvalid selection.");
                    return null;
                }

                targetDte = selected.dte;
                targetProject = selected.project;
            }

            // 4. Verify Debug configuration
            string activeConfig = targetProject.ConfigurationManager
                .ActiveConfiguration.ConfigurationName;

            if (activeConfig != "Debug")
            {
                ed?.WriteMessage(
                    $"\nActive configuration is '{activeConfig}', expected 'Debug'. Aborting.");
                return null;
            }

            // 5. Build the project (+ dependencies)
            ed?.WriteMessage($"\nBuilding '{projectName}'...");

            SolutionBuild solBuild = targetDte.Solution.SolutionBuild;
            string solutionConfig = solBuild.ActiveConfiguration.Name;
            solBuild.BuildProject(solutionConfig, targetProject.UniqueName, true);

            if (solBuild.LastBuildInfo != 0)
            {
                ed?.WriteMessage(
                    $"\nBuild failed ({solBuild.LastBuildInfo} project(s) failed).");
                return null;
            }

            ed?.WriteMessage("\nBuild succeeded.");

            // 6. Get output DLL path
            string projectDir = Path.GetDirectoryName(targetProject.FullName)!;

            string outputPath = targetProject.ConfigurationManager
                .ActiveConfiguration.Properties.Item("OutputPath").Value.ToString()!;

            string assemblyName = targetProject.Properties
                .Item("AssemblyName").Value.ToString()!;

            string dllPath = Path.GetFullPath(
                Path.Combine(projectDir, outputPath, assemblyName + ".dll"));

            if (!File.Exists(dllPath))
            {
                ed?.WriteMessage($"\nBuild output not found at: {dllPath}");
                return null;
            }

            ed?.WriteMessage($"\nOutput: {dllPath}");
            return dllPath;
        }

        private static void SearchProject(
            Project prj,
            string projectName,
            _DTE dte,
            List<(string solutionName, _DTE dte, Project project)> matches)
        {
            try
            {
                if (prj.Kind == SolutionFolderKind)
                {
                    // Recurse into solution folders
                    foreach (ProjectItem item in prj.ProjectItems)
                    {
                        if (item.SubProject != null)
                            SearchProject(item.SubProject, projectName, dte, matches);
                    }
                }
                else if (prj.Name == projectName)
                {
                    string solName = Path.GetFileNameWithoutExtension(
                        dte.Solution.FullName);
                    matches.Add((solName, dte, prj));
                }
            }
            catch
            {
                // Skip inaccessible projects
            }
        }

        private static string SanitizeKeyword(string name)
        {
            // AutoCAD keywords must be alphabetic
            return new string(name.Where(char.IsLetter).ToArray());
        }

        private class WaitCursorScope : IDisposable
        {
            private readonly Cursor _savedCursor;

            public WaitCursorScope()
            {
                _savedCursor = Cursor.Current;
                Cursor.Current = Cursors.WaitCursor;
            }

            public void Dispose()
            {
                Cursor.Current = _savedCursor;
            }
        }
    }
}
