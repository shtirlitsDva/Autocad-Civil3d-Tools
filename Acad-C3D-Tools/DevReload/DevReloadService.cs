using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.EditorInput;
using EnvDTE;
using EnvDTE80;

namespace DevReload
{
    public record VsProjectInfo(string Name, string DebugDllPath, string SolutionName);

    public static class DevReloadService
    {
        private const string SolutionFolderKind = "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}";

        public static List<VsProjectInfo> GetAvailableProjects(Editor? ed)
        {
            var result = new List<VsProjectInfo>();
            var vsInstances = VsInstanceFinder.GetRunningVSInstances();
            if (vsInstances.Count == 0)
            {
                ed?.WriteMessage("\nNo running Visual Studio instances found.");
                return result;
            }

            foreach (var kvp in vsInstances)
            {
                _DTE dte = kvp.Value;
                try
                {
                    if (string.IsNullOrEmpty(dte.Solution?.FullName))
                        continue;

                    string solName = Path.GetFileNameWithoutExtension(dte.Solution.FullName);
                    CollectProjects(dte.Solution, solName, result);
                }
                catch { }
            }

            return result;
        }

        private static void CollectProjects(Solution solution, string solName, List<VsProjectInfo> result)
        {
            foreach (Project prj in solution.Projects)
                CollectProject(prj, solName, result);
        }

        private static void CollectProject(Project prj, string solName, List<VsProjectInfo> result)
        {
            try
            {
                if (prj.Kind == SolutionFolderKind)
                {
                    foreach (ProjectItem item in prj.ProjectItems)
                    {
                        if (item.SubProject != null)
                            CollectProject(item.SubProject, solName, result);
                    }
                    return;
                }

                string projectDir = Path.GetDirectoryName(prj.FullName)!;
                string assemblyName = prj.Properties.Item("AssemblyName").Value.ToString()!;

                string? debugOutputPath = null;
                var configMgr = prj.ConfigurationManager;
                if (configMgr != null)
                {
                    for (int i = 1; i <= configMgr.Count; i++)
                    {
                        try
                        {
                            var cfg = configMgr.Item(i);
                            if (cfg.ConfigurationName.Equals("Debug", StringComparison.OrdinalIgnoreCase))
                            {
                                debugOutputPath = cfg.Properties.Item("OutputPath").Value.ToString();
                                break;
                            }
                        }
                        catch { }
                    }

                    if (debugOutputPath == null)
                    {
                        try
                        {
                            debugOutputPath = configMgr.ActiveConfiguration
                                .Properties.Item("OutputPath").Value.ToString();
                        }
                        catch { }
                    }
                }

                if (debugOutputPath == null) return;

                string dllPath = Path.GetFullPath(
                    Path.Combine(projectDir, debugOutputPath, assemblyName + ".dll"));

                result.Add(new VsProjectInfo(prj.Name, dllPath, solName));
            }
            catch { }
        }

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
                // Multiple matches - ask user via StringGridForm
                var solNames = matches.Select(m => m.solutionName).ToList();
                string selection = IntersectUtilities.StringGridFormCaller.Call(
                    solNames,
                    $"Project '{projectName}' found in {matches.Count} instances. Select:");

                if (string.IsNullOrEmpty(selection))
                {
                    ed?.WriteMessage("\nCancelled.");
                    return null;
                }

                var selected = matches.FirstOrDefault(m => m.solutionName == selection);

                if (selected.dte == null)
                {
                    ed?.WriteMessage("\nInvalid selection.");
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

            SolutionBuild solBuild;
            string solutionConfig;
            try
            {
                solBuild = targetDte.Solution.SolutionBuild;
                solutionConfig = solBuild.ActiveConfiguration.Name;
            }
            catch (Exception ex)
            {
                ed?.WriteMessage($"\nFailed to access VS build system: {ex.Message}");
                return null;
            }

            try
            {
                solBuild.BuildProject(solutionConfig, targetProject.UniqueName, true);
            }
            catch (Exception ex)
            {
                ed?.WriteMessage(
                    $"\nBuildProject COM call failed: {ex.GetType().Name}: {ex.Message}");
                return null;
            }

            if (solBuild.LastBuildInfo != 0)
            {
                ed?.WriteMessage(
                    $"\nBuild failed ({solBuild.LastBuildInfo} project(s) failed).");

                try
                {
                    if (targetDte is DTE2 dte2)
                    {
                        foreach (OutputWindowPane pane in dte2.ToolWindows.OutputWindow.OutputWindowPanes)
                        {
                            if (pane.Name != "Build") continue;
                            var doc = pane.TextDocument;
                            var sel = doc.Selection;
                            sel.SelectAll();
                            string buildLog = sel.Text ?? "";
                            var errorLines = buildLog
                                .Split('\n')
                                .Where(l => l.Contains(": error "))
                                .Take(10)
                                .ToList();
                            foreach (var line in errorLines)
                                ed?.WriteMessage($"\n  {line.Trim()}");
                            break;
                        }
                    }
                }
                catch
                {
                }

                return null;
            }

            ed?.WriteMessage("\nBuild succeeded.");

            // 6. Get output DLL path
            string projectDir;
            string outputPath;
            string assemblyName;
            try
            {
                projectDir = Path.GetDirectoryName(targetProject.FullName)!;

                outputPath = targetProject.ConfigurationManager
                    .ActiveConfiguration.Properties.Item("OutputPath").Value.ToString()!;

                assemblyName = targetProject.Properties
                    .Item("AssemblyName").Value.ToString()!;
            }
            catch (Exception ex)
            {
                ed?.WriteMessage(
                    $"\nFailed to read project output properties: {ex.GetType().Name}: {ex.Message}");
                return null;
            }

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
