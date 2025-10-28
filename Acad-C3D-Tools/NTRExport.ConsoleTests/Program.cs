using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

using NTRExport.ConsoleTests.TestCases;

namespace NTRExport.ConsoleTests
{
    internal static class Program
    {
        private static async Task<int> Main()
        {
            var accore = @"C:\Program Files\Autodesk\AutoCAD 2025\AcCoreConsole.exe";
            if (string.IsNullOrWhiteSpace(accore) || !File.Exists(accore))
            {
                Console.WriteLine("SKIPPED: ACCORE_PATH not set or invalid.");
                return 0;
            }

            var root = FindSolutionRoot();
            var ntrDll = Path.Combine(root, "Acad-C3D-Tools", "NTRExport", "bin", "Debug", "NTRExport.dll");
            var ntrDllResolved = ResolveSubstPath(ntrDll);
            if (!File.Exists(ntrDllResolved))
			{
				Console.WriteLine("NTRExport.dll not found. Attempting to build (build-ntrexport-debug.bat)...");
				var buildScript = Path.Combine(root, "build-ntrexport-debug.bat");
				try
				{
					var psi = new ProcessStartInfo
					{
						FileName = "cmd.exe",
						Arguments = "/c \"" + buildScript + "\"",
						UseShellExecute = false,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						CreateNoWindow = true,
						WorkingDirectory = root,
					};
					using var p = Process.Start(psi);
					if (p is not null)
					{
						var stdoutTask = p.StandardOutput.ReadToEndAsync();
						var stderrTask = p.StandardError.ReadToEndAsync();
						await Task.WhenAll(stdoutTask, stderrTask, p.WaitForExitAsync());
						var stdout = stdoutTask.Result;
						var stderr = stderrTask.Result;
						if (!string.IsNullOrWhiteSpace(stdout)) Console.WriteLine(stdout);
						if (!string.IsNullOrWhiteSpace(stderr)) Console.Error.WriteLine(stderr);
					}
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine(ex);
				}

                // Re-check for the DLL after build attempt
                if (!File.Exists(ntrDllResolved))
				{
                    Console.Error.WriteLine($"ERROR: Could not find NTRExport.dll under path:\n{ntrDllResolved}");
					return 2;
				}
			}

            var cases = new BaseTestCase[]
            {
                new StandalonePipesTest(),
                new StandaloneBlockTest(),
                new PreinsulatedTeeTwinTest(),
            };

            var failures = 0;
            foreach (var testCase in cases)
            {
                failures += await RunCase(accore, ntrDllResolved, testCase);
            }

            Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : $"FAILURES: {failures}");
            return failures == 0 ? 0 : 1;
        }

        private static async Task<int> RunCase(string accoreExe, string ntrDll, BaseTestCase testCase)
        {
            try
            {
                var tmp = Directory.CreateDirectory(
                    Path.Combine(Path.GetTempPath(), "ntr-tests", Guid.NewGuid().ToString("N"))).FullName;
                return await testCase.Execute(tmp, accoreExe, ntrDll);
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

        private static string ResolveSubstPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;

            var root = Path.GetPathRoot(path);
            if (string.IsNullOrEmpty(root)) return path;

            var drive = root.TrimEnd('\\', '/'); // e.g. "X:"
            if (drive.Length < 2 || drive[1] != ':') return path;

            var target = QueryDosDeviceTarget(drive);
            if (string.IsNullOrEmpty(target)) return path;

            // SUBST maps like "\\??\\C:\\...". Only rewrite those
            const string substPrefix = @"\??\"; // "\\??\\" 
            if (!target.StartsWith(substPrefix, StringComparison.OrdinalIgnoreCase)) return path;

            var physicalRoot = target.Substring(substPrefix.Length); // e.g. "C:\..."
            var rest = path.Substring(root.Length);
            return Path.Combine(physicalRoot, rest);
        }

        private static string QueryDosDeviceTarget(string drive)
        {
            var sb = new StringBuilder(1024);
            try
            {
                var result = QueryDosDevice(drive, sb, sb.Capacity);
                if (result == 0) return string.Empty;
                var s = sb.ToString();
                var idx = s.IndexOf('\0');
                return idx >= 0 ? s.Substring(0, idx) : s;
            }
            catch
            {
                return string.Empty;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern uint QueryDosDevice(string lpDeviceName, StringBuilder lpTargetPath, int ucchMax);
    }
}


