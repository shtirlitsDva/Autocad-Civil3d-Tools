using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;

using System.Linq;
using System.Reflection;
using static IntersectUtilities.UtilsCommon.Utils;

namespace PipeScheduleV2Tests
{
    public partial class PipeScheduleV2TestsClass : IExtensionApplication
    {
        [CommandMethod("RUNPS2TESTS")]
        public void runps2tests()
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            // Registry now lives alongside the test dll (local folder under PipeScheduleV2Tests bin)
            string registryPath = PipeScheduleV2EntityRegistry.GetRegistryPath();
            string reportsDir = System.IO.Path.Combine(dwgDir, ReportsFolderName);
            System.IO.Directory.CreateDirectory(reportsDir);

            PipeScheduleV2EntityRegistry.EnsureRegistryFileExists(registryPath);
            PipeScheduleV2EntityRegistry.EnsureBaselineRegistry(registryPath);
            PipeScheduleV2EntityRegistry.EnsurePolylinesFromRegistry(db, registryPath);

            var results = new System.Collections.Generic.List<Ps2Result>();
            var methods = Assembly.GetExecutingAssembly()
                .GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                .Where(m => m.GetCustomAttribute<Ps2TestAttribute>() != null && m.GetParameters().Length == 0)
                .ToList();

            int processed = 0;
            foreach (var m in methods)
            {
                var r = new Ps2Result { Name = (m.DeclaringType?.Name ?? "?") + "." + m.Name };
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    object? instance = null;
                    if (!m.IsStatic) instance = System.Activator.CreateInstance(m.DeclaringType!);
                    m.Invoke(instance, null);
                    r.Status = Ps2Status.Passed;
                    r.Message = "OK";
                }
                catch (System.Reflection.TargetInvocationException tex)
                {
                    var ex = tex.InnerException ?? tex;
                    if (ex is Ps2SkipException)
                    {
                        r.Status = Ps2Status.Skipped;
                        r.Message = ex.Message;
                    }
                    else
                    {
                        r.Status = Ps2Status.Failed;
                        r.Message = ex.GetBaseException().Message;
                        r.StackTrace = ex.GetBaseException().StackTrace ?? string.Empty;
                        prdDbg($"FAIL {r.Name}: {r.Message}");
                    }
                }
                catch (System.Exception ex)
                {
                    r.Status = Ps2Status.Error;
                    r.Message = ex.GetBaseException().Message;
                    r.StackTrace = ex.GetBaseException().StackTrace ?? string.Empty;
                    prdDbg($"ERROR {r.Name}: {r.Message}");
                }
                finally
                {
                    sw.Stop();
                    r.Duration = sw.Elapsed;
                    results.Add(r);
                }

                processed++;
                if (processed % 10 == 0) prdDbg($"Processed {processed}/{methods.Count}...");
            }

            string reportPath = PipeScheduleV2Report.WriteHtmlReport(results, reportsDir);
            PipeScheduleV2Report.WriteRawLog(results, System.IO.Path.ChangeExtension(reportPath, ".log"));

            int passed = results.Count(x => x.Status == Ps2Status.Passed);
            int failed = results.Count(x => x.Status == Ps2Status.Failed);
            int errors = results.Count(x => x.Status == Ps2Status.Error);
            int skipped = results.Count(x => x.Status == Ps2Status.Skipped);
            prdDbg($"Tests: {results.Count}, Passed: {passed}, Failed: {failed}, Errors: {errors}, Skipped: {skipped}. Report: {reportPath}");
        }
    }
}


