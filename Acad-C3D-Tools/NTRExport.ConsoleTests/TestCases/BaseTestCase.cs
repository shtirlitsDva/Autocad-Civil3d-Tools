using System.Diagnostics;
using System.Text;

namespace NTRExport.ConsoleTests.TestCases
{
    internal abstract class BaseTestCase
    {
        protected abstract string DwgName { get; }
        public abstract string DisplayName { get; }

        public int Execute(string tempDir, string accoreExe, string ntrDll)
        {
            var assetPath = TestAssets.Resolve(DwgName);
            if (!File.Exists(assetPath))
            {
                Console.WriteLine($"SKIPPED {DwgName}");
                return 0;
            }

            var dwgPath = Path.Combine(tempDir, Path.GetFileName(assetPath));
            File.Copy(assetPath, dwgPath, overwrite: true);

            var scriptPath = Path.Combine(tempDir, "run.scr");
            File.WriteAllText(scriptPath, BuildScript(ntrDll));

            string arguments = $"/i \"{dwgPath}\" " +
                               $"/s \"{scriptPath}\" " +
                                "/product ACAD " +
                                "/language en - US"; // +
                                //"/p \"<<C3D_Metric>>\"";// " +
                                //"/loadmodule \"C:\\Program Files\\Autodesk\\AutoCAD 2023\\AecBase.dbx\"";

            var psi = new ProcessStartInfo
            {
                FileName = accoreExe,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,                
                CreateNoWindow = true,
                WorkingDirectory = tempDir,
            };
            var p = new Process { StartInfo = psi };
            p.OutputDataReceived += (_, e) => 
            { if (!string.IsNullOrEmpty(e.Data)) Console.WriteLine(e.Data); };
            p.ErrorDataReceived += (_, e) => 
            { if (!string.IsNullOrEmpty(e.Data)) Console.Error.WriteLine(e.Data); };

            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            p.WaitForExit();

            string uri = new Uri(tempDir).AbsoluteUri;
            Console.WriteLine($"\u001b]8;;{uri}\u001b\\{tempDir}\u001b]8;;\u001b\\");

            if (p.ExitCode != 0)
            {
                Console.Error.WriteLine("accoreconsole failed:");                
                return 1;
            }
            string ntrPath = Path.ChangeExtension(dwgPath, ".ntr");
            if (!File.Exists(ntrPath))
            {
                Console.Error.WriteLine("Expected NTR file not found: " + ntrPath);
                return 1;
            }            

            return Validate(ntrPath) ? 0 : 1;
        }

        protected abstract bool Validate(string ntrPath);

        private static string BuildScript(string ntrDll)
        {
            var sb = new StringBuilder();
            sb.AppendLine("SECURELOAD");
            sb.AppendLine("0");
            sb.AppendLine("NETLOAD");
            sb.AppendLine(ntrDll);
            sb.AppendLine("NTRTEST");
            sb.AppendLine("QUIT Y");
            sb.AppendLine();
            return sb.ToString();
        }
    }
}

