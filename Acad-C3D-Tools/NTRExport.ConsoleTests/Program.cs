using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using NTRExport.ConsoleTests.TestCases;

namespace NTRExport.ConsoleTests
{
    internal static class Program
    {
        private static int Main()
        {
            var accore = @"C:\Program Files\Autodesk\AutoCAD 2025\AcCoreConsole.exe";
            if (string.IsNullOrWhiteSpace(accore) || !File.Exists(accore))
            {
                Console.WriteLine("SKIPPED: ACCORE_PATH not set or invalid.");
                return 0;
            }

            var root = FindSolutionRoot();
            var ntrDll = Path.Combine(root, "Acad-C3D-Tools", "NTRExport", "bin", "Debug", "NTRExport.dll");
            if (!File.Exists(ntrDll))
            {
                Console.Error.WriteLine($"ERROR: Could not find NTRExport.dll under path:\n{ntrDll}");
                return 2;
            }

            var cases = new BaseTestCase[]
            {
                new PreinsulatedTeeTwinTest(),
            };

            var failures = 0;
            foreach (var testCase in cases)
            {
                failures += RunCase(accore, ntrDll, testCase);
            }

            Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : $"FAILURES: {failures}");
            return failures == 0 ? 0 : 1;
        }

        private static int RunCase(string accoreExe, string ntrDll, BaseTestCase testCase)
        {
            try
            {
                var tmp = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ntr-tests", Guid.NewGuid().ToString("N"))).FullName;
                return testCase.Execute(tmp, accoreExe, ntrDll);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static string FindSolutionRoot()
        {
            var dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                var acd = Path.Combine(dir, "Acad-C3D-Tools");
                if (Directory.Exists(acd)) return dir;
                dir = Path.GetDirectoryName(dir)!;
            }
            return AppContext.BaseDirectory;
        }
    }
}


