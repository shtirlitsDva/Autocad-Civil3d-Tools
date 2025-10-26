using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace NTRExport.ConsoleTests.TestCases
{
    internal abstract class BaseTestCase
    {
        protected abstract string DwgName { get; }
        public abstract string DisplayName { get; }

        public async Task<int> Execute2(string tempDir, string accoreExe, string ntrDll)
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
            p.Start();

            // read both streams concurrently to avoid deadlocks
            var outTask = p.StandardOutput.ReadToEndAsync();
            var errTask = p.StandardError.ReadToEndAsync();

            p.WaitForExit();
            var stdout = await outTask;
            var stderr = await errTask;
            
            p.WaitForExit();

            // filter once
            Console.Write(Filter(stdout));
            if (!string.IsNullOrWhiteSpace(stderr))
                Console.Error.Write(Filter(stderr));

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

        public async Task<int> Execute(string tempDir, string accoreExe, string ntrDll)
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
                StandardOutputEncoding = Encoding.Unicode, // UTF-16LE
                StandardErrorEncoding = Encoding.Unicode, // UTF-16LE
            };
            var p = new Process { StartInfo = psi };
            p.Start();

            // read both pipes fully with the correct encoding
            var stdoutTask = p.StandardOutput.ReadToEndAsync();
            var stderrTask = p.StandardError.ReadToEndAsync();
            await Task.WhenAll(stdoutTask, stderrTask, p.WaitForExitAsync());

            var stdout = stdoutTask.Result;
            var stderr = stderrTask.Result;

            // now your existing Filter(...) will work
            Console.Write(Filter(stdout));
            if (!string.IsNullOrWhiteSpace(stderr))
                Console.Error.Write(Filter(stderr));

            p.WaitForExit();            

            string uri = new Uri(tempDir).AbsoluteUri;
            Console.WriteLine($"\u001b[32m\u001b]8;;{uri}\u001b\\{tempDir}\u001b]8;;\u001b\\\u001b[0m");

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

        static string Filter(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;

            s = s.Replace("\r\n", "\n").Replace("\r", "\n");

            var sb = new StringBuilder();
            bool skipping = false;

            foreach (var raw in s.Split('\n'))
            {
                var line = raw.TrimEnd();

                // block suppression
                if (!skipping && line.Contains("Version Number:", StringComparison.OrdinalIgnoreCase))
                {
                    skipping = true;
                    continue;
                }
                if (skipping && line.Contains("Regenerating model.", StringComparison.OrdinalIgnoreCase))
                {
                    skipping = false;
                    continue;
                }
                if (skipping) continue;

                // drop blanks
                if (string.IsNullOrWhiteSpace(line)) continue;

                sb.AppendLine(line);
            }
            return sb.ToString();
        }

        protected abstract bool Validate(string ntrPath);

        protected Ntr.NtrDocument LoadActual(string ntrPath)
        {
            return Ntr.NtrDocument.Load(ntrPath);
        }

        protected Ntr.NtrDocument LoadTemplate()
        {
            var asset = TestAssets.Resolve(DwgName);
            var templatePath = Path.ChangeExtension(asset, ".ntr");
            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException($"Missing golden NTR template for {DwgName}", templatePath);
            }
            return Ntr.NtrDocument.Load(templatePath);
        }
 
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

