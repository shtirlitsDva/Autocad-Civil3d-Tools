
#region ##### .NET Imports #####
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using EnvDTE;
using System.IO;
using System.Windows.Forms;
using System.Reflection;
#endregion

#region ##### Autodesk.AutoCAD Imports #####
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.ComponentModel;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.GraphicsSystem;
using Autodesk.AutoCAD.Internal;
using Autodesk.AutoCAD.LayerManager;
using Autodesk.AutoCAD.PlottingServices;
using Autodesk.AutoCAD.Publishing;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
#endregion

[assembly: CommandClass(typeof(NetReload.Commands))]

namespace NetReload
{

    public class Commands
    {
        #region // Sources.

        //
        //      Source code is from here:
        //      https://forums.autodesk.com/t5/net/net-reload-utility-for-visual-studio-download-here/td-p/3185104
        //
        //	1.	Get running Visual Studio instances and corresponding _DTE objects:
        //		http://www.christophdebaene.com/blog/2006/11/01/get-running-visual-studio-instances-and-corresponding-_dte-objects/

        #endregion

        // Default keyword application-level variable.
        private static string defaultKeyword = "";

        // NETRELOAD command.
        [CommandMethod("NRL")]
        public static void NRL()
        {
            // Current AutoCAD Document, Database and Editor.
            Autodesk.AutoCAD.ApplicationServices.Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            string netReloadDir = null;

//#if DEBUG
            ResolveEventHandler assemblyResolveHandler = (sender, args) =>
                {
                    if (netReloadDir == null) return null;
                    //ed.WriteMessage($"Resolving assembly: {args.Name}\n");

                    string parentDir = Directory.GetParent(netReloadDir).FullName;
                    string assemblyPath = Path.Combine(parentDir, new AssemblyName(args.Name).Name + ".dll");

                    if (File.Exists(assemblyPath))
                    {
                        //ed.WriteMessage($"Found and loading assembly!\n");
                        return Assembly.LoadFrom(assemblyPath);
                    }

                    //ed.WriteMessage($"Assembly not found in parent path!\n");
                    return null; // Let the default resolution process handle it
                };

            AppDomain.CurrentDomain.AssemblyResolve += assemblyResolveHandler; 
//#endif

            try
            {
                // Get running Visual Studio instances (using helper class).
                IDictionary<string, _DTE> vsInstances = RunningObjectTable.GetRunningVSIDETable();

                // Check for no Visual Studio instances.
                if (vsInstances.Count == 0)
                {
                    ed.WriteMessage("\nNo running Visual Studio instances were found. *Cancel*");
                    return;
                }

                // Create list of solution names.

                List<(string, _DTE)> tuples = new();
                foreach (KeyValuePair<string, _DTE> item in vsInstances)
                {
                    tuples.Add((Path.GetFileNameWithoutExtension(item.Value.Solution.FullName), item.Value));
                }

                // Check if all names are unique.
                // if not, increment the names.

                var duplicates = tuples.GroupBy(x => x.Item1)
                    .Where(g => g.Count() > 1)
                    .Select(y => y)
                    .ToList();

                foreach (var dupes in duplicates)
                {
                    int i = 1;
                    for (int j = 0; j < dupes.Count(); j++)
                    {
                        var dupeIndex = tuples.FindIndex(
                            t => t.Item1 == dupes.Key && t.Item2 == dupes.ElementAt(j).Item2);
                        if (dupeIndex >= 0)
                        {
                            tuples[dupeIndex] = ($"{tuples[dupeIndex].Item1}_{i}", tuples[dupeIndex].Item2);
                            i++;
                        }
                    }
                }

                Dictionary<string, _DTE> solNames = new();
                foreach (var item in tuples)
                {
                    solNames.Add(
                        item.Item1,
                        item.Item2);
                }

                // Check if all solution names equal "".
                // i.e. no solutions loaded in any of the Visual Studio instances.
                bool allSolNamesEmpty = true;
                foreach (string name in solNames.Keys)
                {
                    if (name != "")
                    {
                        allSolNamesEmpty = false;
                        break;
                    }
                }
                if (allSolNamesEmpty == true)
                {
                    ed.WriteMessage("\nNo active Visual Studio solutions were found. *Cancel*");
                    return;
                }

                var solName = IntersectUtilities.StringGridFormCaller.Call(
                    solNames.Keys, "Select Visual Studio instance to increment:");
                if (string.IsNullOrEmpty(solName))
                {
                    ed.WriteMessage("\nNo Visual Studio instance selected. *Cancel*");
                    return;
                }

                // Use prompt result to set Visual Studio instance variable.
                //_DTE dte = vsInstances.ElementAt(solNames.IndexOf(solName)).Value;
                _DTE dte = solNames[solName];

                // Use custom WaitCursor class for long operation.
                using (WaitCursor wc = new WaitCursor())
                {
                    Project prj = null;

                    // Active Visual Studio Document.
                    EnvDTE.Document vsDoc = dte.ActiveDocument;
                    if (vsDoc == null || vsDoc.ProjectItem == null || vsDoc.ProjectItem.ContainingProject == null)
                    {
                        ed.WriteMessage(String.Format("\nNo active document found for the '{0}' solution. *Cancel*",
                            solName));

                        Array? projects = dte.ActiveSolutionProjects as Array;
                        if (projects == null || projects.Length == 0 || projects.Length > 1)
                        {
                            ed.WriteMessage(String.Format("\nNo or multiple active project found for the '{0}' solution. *Cancel*",
                                solName));
                            return;
                        }

                        foreach (Project item in projects)
                        {
                            ed.WriteMessage(String.Format("\nProject: {0}", item.Name));
                            prj = item;
                        }
                        if (prj == null)
                        {
                            ed.WriteMessage("Project is null.");
                            return;
                        }
                    }
                    else
                    {
                        //Active Visual Studio Project.
                        prj = vsDoc.ProjectItem.ContainingProject;
                    }

                    // Check if active configuration is Debug.
                    // If not -- exit
                    if (prj.ConfigurationManager.ActiveConfiguration.ConfigurationName != "Debug")
                    {
                        ed.WriteMessage(
                            String.Format(
                                "\nActive configuration is not 'Debug' for the '{0}' solution. *Cancel*",
                                solName));
                        return;
                    }


                    // Debug directory - i.e. \bin\Debug.
                    string debugDir = prj.FullName;
                    debugDir = Path.GetDirectoryName(debugDir);
                    debugDir = Path.Combine(debugDir, @"bin\Debug");

                    // NetReload directory - i.e. \bin\Debug\NetReload.
                    netReloadDir = Path.Combine(debugDir, "NetReload");

                    // Create NetReload directory if it doens't exist.
                    if (Directory.Exists(netReloadDir) == false)
                    {
                        Directory.CreateDirectory(netReloadDir);
                    }

                    // Temporary random assembly file name (check it doesn't already exist).
                    string tempAssemblyName;
                    do
                    {
                        tempAssemblyName = Path.GetRandomFileName();
                    } while (File.Exists(Path.Combine(netReloadDir, tempAssemblyName + ".dll")));

                    // Project's initial "AssemblyName" property setting.
                    string initAssemblyName = prj.Properties.Item("AssemblyName").Value as string;

                    // Set project's "AssemblyName" property to temp value.
                    prj.Properties.Item("AssemblyName").Value = tempAssemblyName;

                    // Build solution.
                    SolutionBuild solBuild = dte.Solution.SolutionBuild;
                    ed.WriteMessage($"Building project {prj.Name}...\n");
                    solBuild.Build(true);

                    // Re-set project's "AssemblyName" property back to initial value.
                    prj.Properties.Item("AssemblyName").Value = initAssemblyName;

                    // Check if build was successful.
                    // # Note: LastBuildInfo property reports number of projects in the solution that failed to build.
                    if (solBuild.LastBuildInfo != 0)
                    {
                        ed.WriteMessage(String.Format("\nBuild failed for the '{0}' solution. *Cancel*",
                            solName));

                        //But I've found that it sometimes builds anyway, so let's check if the assembly exists
                        ed.WriteMessage($"\nLooking for assembly {tempAssemblyName}.dll in {debugDir}\n");
                        if (!File.Exists(Path.Combine(debugDir, tempAssemblyName + ".dll")))
                        {
                            ed.WriteMessage("\nAssembly not found in Debug directory. *Cancel*");
                            return;
                        }
                    }

                    // Move new assembly (.dll) from Debug directory to NetReload directory.
                    File.Move(
                        Path.Combine(debugDir, tempAssemblyName + ".dll"),
                        Path.Combine(netReloadDir, tempAssemblyName + ".dll")
                        );

                    // Move new .pdb file from Debug directory to NetReload directory.
                    File.Move(
                        Path.Combine(debugDir, tempAssemblyName + ".pdb"),
                        Path.Combine(netReloadDir, tempAssemblyName + ".pdb")
                        );
                    File.Move(
                        Path.Combine(debugDir, tempAssemblyName + ".deps.json"),
                        Path.Combine(netReloadDir, tempAssemblyName + ".deps.json")
                        );

                    //// Move new .config file from Debug directory to NetReload directory.
                    //// This is not needed for .NET 8.0 and later.???
                    //File.Move(
                    //    Path.Combine(debugDir, initAssemblyName + ".dll.config"),
                    //    Path.Combine(debugDir, tempAssemblyName + ".dll.config")
                    //    );



                    // NETLOAD new assembly file.
                    var assembly = System.Reflection.Assembly.LoadFrom(Path.Combine(netReloadDir, tempAssemblyName + ".dll"));

                    // Output summary.
                    ed.WriteMessage("\nNETRELOAD complete for {0}.dll.", initAssemblyName);
                }

            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                // Catch AutoCAD exception.
                Application.ShowAlertDialog(String.Format("ERROR" +
                    "\nMessage: {0}\nErrorStatus: {1}", ex.Message, ex.ErrorStatus));
                File.WriteAllText(@"C:\Temp\NRL_ERROR.txt", ex.ToString());
            }
            catch (System.Exception ex)
            {
                // Catch Windows exception.
                Application.ShowAlertDialog(String.Format("ERROR" +
                    "\nMessage: {0}", ex.Message));
                File.WriteAllText(@"C:\Temp\NRL_ERROR.txt", ex.ToString());
            }
            finally
            {
//#if DEBUG
                AppDomain.CurrentDomain.AssemblyResolve -= assemblyResolveHandler;
//#endif
            }
        }

        // RunningObjectTable helper class.
        class RunningObjectTable
        {

            const uint S_OK = 0;

            [DllImport("ole32.dll")]
            public static extern uint GetRunningObjectTable(uint reserved, out IRunningObjectTable ROT);

            [DllImport("ole32.dll")]
            public static extern uint CreateBindCtx(uint reserved, out IBindCtx ctx);

            static IDictionary<string, object> GetRunningObjectTable()
            {
                IDictionary<string, object> rotTable = new Dictionary<string, object>();

                IRunningObjectTable runningObjectTable;
                IEnumMoniker monikerEnumerator;
                IMoniker[] monikers = new IMoniker[1];

                GetRunningObjectTable(0, out runningObjectTable);
                runningObjectTable.EnumRunning(out monikerEnumerator);
                monikerEnumerator.Reset();

                IntPtr numberFetched = IntPtr.Zero;

                while (monikerEnumerator.Next(1, monikers, numberFetched) == 0)
                {
                    IBindCtx ctx;
                    CreateBindCtx(0, out ctx);

                    string runningObjectName;
                    monikers[0].GetDisplayName(ctx, null, out runningObjectName);
                    Marshal.ReleaseComObject(ctx);

                    object runningObjectValue;
                    runningObjectTable.GetObject(monikers[0], out runningObjectValue);

                    if (!rotTable.ContainsKey(runningObjectName))
                        rotTable.Add(runningObjectName, runningObjectValue);
                }

                return rotTable;
            }

            public static IDictionary<string, _DTE> GetRunningVSIDETable()
            {
                IDictionary<string, object> runningObjects = GetRunningObjectTable();
                IDictionary<string, _DTE> runningDTEObjects = new Dictionary<string, _DTE>();

                foreach (string objectName in runningObjects.Keys)
                {
                    if (!objectName.StartsWith("!VisualStudio.DTE"))
                        continue;

                    _DTE ide = runningObjects[objectName] as _DTE;
                    if (ide == null)
                        continue;

                    runningDTEObjects.Add(objectName, ide);
                }

                return runningDTEObjects;
            }
        }

        // WaitCursor helper class.
        // # Note: Add project reference to System.Windows.Forms.dll.
        private class WaitCursor : IDisposable
        {

            // Field.
            System.Windows.Forms.Cursor savedCursor;

            // Constructor.
            public WaitCursor()
            {
                this.savedCursor = System.Windows.Forms.Cursor.Current;
                System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.WaitCursor;
            }

            // IDisposable implementation.
            public void Dispose()
            {
                System.Windows.Forms.Cursor.Current = this.savedCursor;
            }
        }

    }
}
