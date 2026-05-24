using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.LongitudinalProfiles.AutoProfileV2;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace IntersectUtilities
{
    /// <summary>
    /// Talks to the v2 ("seam-coupled ArcFit") Python solver as a stateless
    /// command-line JSON service: write a case file, run the Python CLI, read
    /// the result file. There is no long-running service / native engine / job
    /// queue any more — that was the v1 PipeSolver.Interop server.
    /// </summary>
    internal sealed class AutoProfileV2SolverClient
    {
        // PoC defaults target this machine. Both are overridable via env vars so
        // the shipping decision (bundled venv / system python / one-file exe)
        // can be made later without touching code.
        private const string SolverPythonEnvVar = "AUTOPROFILE_SOLVER_PYTHON";
        private const string SolverDirEnvVar = "AUTOPROFILE_SOLVER_DIR";
        private const string DefaultSolverPython =
            @"H:\GitHub\DamgaardRI\AutoProfileSolver\v2\.venv\Scripts\python.exe";
        private const string DefaultSolverDir =
            @"H:\GitHub\DamgaardRI\AutoProfileSolver\v2\Code";

        private const string CaseSchema = "Norsyn.seam.case.v1";
        private static readonly TimeSpan SolveTimeout = TimeSpan.FromMinutes(10);

        private static readonly JsonSerializerOptions CaseJsonOptions = new()
        {
            WriteIndented = true,
        };

        private static readonly JsonSerializerOptions ResultJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
        };

        private readonly Action<string> _log;

        public AutoProfileV2SolverClient(Action<string> log)
        {
            _log = log;
        }

        public Polyline SolveProfilePolyline(AP2_PipelineData pipelineData)
        {
            SeamCase seamCase = BuildSeamCase(pipelineData);
            SeamSolveResult result = RunSolver(seamCase, pipelineData.Name);

            if (!result.Success)
            {
                // v2 reports success=false for non-convergent terminations
                // (e.g. "cooldown_exhausted") even when it produced a usable
                // chain, so this is a warning, not a hard failure. We gate the
                // actual draw on segment presence below.
                _log($"Pipe solver did not fully converge for {pipelineData.Name}: {result.Message}");
            }

            LogDiagnostics(pipelineData.Name, result.Summary);

            return BuildProfilePolylineFromSolveResult(pipelineData, result);
        }

        private SeamCase BuildSeamCase(AP2_PipelineData pipelineData)
        {
            if (pipelineData.ProfileView == null) throw new System.Exception($"No profile view found for {pipelineData.Name}.");
            if (pipelineData.SurfaceProfile == null) throw new System.Exception($"No surface profile found for {pipelineData.Name}.");
            if (pipelineData.SizeArray == null) throw new System.Exception($"No size array found for {pipelineData.Name}.");

            var sizeEntries = pipelineData.SizeArray.Sizes.ToList();
            if (sizeEntries.Count == 0) throw new System.Exception($"No size entries found for {pipelineData.Name}.");

            // Cover depth is fixed at 0.6 m inside the v2 solver
            // (optimizer/spec.py DEFAULT_COVER_M), so it is no longer collected
            // or transmitted here.

            var pipeSizes = sizeEntries
                .OrderBy(size => size.StartStation)
                .Select(size => new SeamPipeSize
                {
                    SLo = size.StartStation,
                    SHi = size.EndStation,
                    RMinM = size.VerticalMinRadius,
                    JodM = size.Kod / 1000.0,
                })
                .ToList();

            // AP2_Utility.Box is [MinX, MinY, MaxX, MaxY] == [s_lo, y_lo, s_hi, y_hi].
            var utilities = pipelineData.Utility
                .OrderBy(utility => utility.Box[0])
                .Select(utility => new SeamUtility
                {
                    SLo = utility.Box[0],
                    YLo = utility.Box[1],
                    SHi = utility.Box[2],
                    YHi = utility.Box[3],
                })
                .ToList();

            var forbidden = pipelineData.HorizontalArcs.HorizontalArcs
                .OrderBy(arc => arc.StartStation)
                .Select(arc => new[] { arc.StartStation, arc.EndStation })
                .ToList();

            return new SeamCase
            {
                Schema = CaseSchema,
                Name = pipelineData.Name,
                SurfaceProfile = pipelineData.SurfaceProfile.GetSimplifiedProfileDTO(),
                PipeSizes = pipeSizes,
                Utilities = utilities,
                Forbidden = forbidden,
            };
        }

        private SeamSolveResult RunSolver(SeamCase seamCase, string pipelineName)
        {
            string pythonExe = ResolveSolverPython();
            string workingDir = ResolveSolverDir();

            string safeName = Regex.Replace(pipelineName, "[^A-Za-z0-9_\\-]+", "_");
            string runDir = Path.Combine(
                Path.GetTempPath(),
                "AutoProfileV2",
                $"{safeName}_{DateTime.UtcNow:yyyyMMddHHmmssfff}");
            Directory.CreateDirectory(runDir);

            string inputPath = Path.Combine(runDir, "case.json");
            string outputPath = Path.Combine(runDir, "result.json");
            File.WriteAllText(inputPath, JsonSerializer.Serialize(seamCase, CaseJsonOptions));

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonExe,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add("-m");
            startInfo.ArgumentList.Add("lib.api.seam_demo_cli");
            startInfo.ArgumentList.Add("solve");
            startInfo.ArgumentList.Add("--input");
            startInfo.ArgumentList.Add(inputPath);
            startInfo.ArgumentList.Add("--output");
            startInfo.ArgumentList.Add(outputPath);
            startInfo.ArgumentList.Add("--no-samples");
            // The CLI prints status text containing non-ASCII characters; force
            // UTF-8 so a cp1252 console can't crash the Python process.
            startInfo.Environment["PYTHONUTF8"] = "1";
            startInfo.Environment["PYTHONIOENCODING"] = "utf-8";

            _log($"Solving {pipelineName}: \"{pythonExe}\" -m lib.api.seam_demo_cli solve (cwd: {workingDir})");

            using var process = Process.Start(startInfo)
                ?? throw new System.Exception($"Failed to start pipe solver process: {pythonExe}");

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            DateTime deadline = DateTime.UtcNow + SolveTimeout;
            while (!process.WaitForExit(200))
            {
                System.Windows.Forms.Application.DoEvents();
                if (DateTime.UtcNow > deadline)
                {
                    try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
                    throw new TimeoutException(
                        $"Pipe solver timed out for {pipelineName} after {SolveTimeout.TotalMinutes:0} minutes.");
                }
            }
            process.WaitForExit(); // ensure the async stream reads complete

            string stderr = stderrTask.GetAwaiter().GetResult();
            _ = stdoutTask.GetAwaiter().GetResult();

            // Exit code 2 == hard CLI error (bad input, exception). Exit code 1 ==
            // solver ran but reported a non-success status; the result file is
            // still written and may carry a usable chain.
            if (process.ExitCode == 2)
            {
                throw new System.Exception(
                    $"Pipe solver failed for {pipelineName}: {(string.IsNullOrWhiteSpace(stderr) ? "unknown error" : stderr.Trim())}");
            }

            if (!File.Exists(outputPath))
            {
                throw new System.Exception(
                    $"Pipe solver produced no result file for {pipelineName} (exit {process.ExitCode}). " +
                    $"{(string.IsNullOrWhiteSpace(stderr) ? string.Empty : stderr.Trim())}");
            }

            string resultJson = File.ReadAllText(outputPath);
            var result = JsonSerializer.Deserialize<SeamSolveResult>(resultJson, ResultJsonOptions)
                ?? throw new System.Exception($"Pipe solver returned an unreadable result for {pipelineName}.");

            return result;
        }

        private string ResolveSolverPython()
        {
            var configured = Environment.GetEnvironmentVariable(SolverPythonEnvVar);
            var pythonExe = string.IsNullOrWhiteSpace(configured)
                ? DefaultSolverPython
                : Environment.ExpandEnvironmentVariables(configured.Trim().Trim('"'));

            if (!File.Exists(pythonExe))
            {
                throw new FileNotFoundException(
                    $"Pipe solver Python interpreter was not found: {pythonExe}. " +
                    $"Override it with the {SolverPythonEnvVar} environment variable.",
                    pythonExe);
            }

            return Path.GetFullPath(pythonExe);
        }

        private string ResolveSolverDir()
        {
            var configured = Environment.GetEnvironmentVariable(SolverDirEnvVar);
            var solverDir = string.IsNullOrWhiteSpace(configured)
                ? DefaultSolverDir
                : Environment.ExpandEnvironmentVariables(configured.Trim().Trim('"'));

            if (!Directory.Exists(solverDir))
            {
                throw new DirectoryNotFoundException(
                    $"Pipe solver working directory was not found: {solverDir}. " +
                    $"It must contain the v2 'lib', 'optimizer', and 'segmenter' packages. " +
                    $"Override it with the {SolverDirEnvVar} environment variable.");
            }

            return Path.GetFullPath(solverDir);
        }

        private void LogDiagnostics(string pipelineName, SeamSummary? summary)
        {
            if (summary == null) return;
            if (!summary.RadiusOk)
                _log($"Pipe solver warning for {pipelineName}: minimum-radius deficit {summary.RadiusDeficitM:0.###} m.");
            if (summary.UtilityIntrusions > 0)
                _log($"Pipe solver warning for {pipelineName}: {summary.UtilityIntrusions} utility intrusion(s).");
            if (summary.CoverViolationM > 1e-3)
                _log($"Pipe solver warning for {pipelineName}: cover violation {summary.CoverViolationM:0.###} m.");
        }

        private static Polyline BuildProfilePolylineFromSolveResult(
            AP2_PipelineData pipelineData,
            SeamSolveResult result)
        {
            if (pipelineData.ProfileView == null) throw new System.Exception($"No profile view found for {pipelineData.Name}.");
            if (result.Segments == null || result.Segments.Count == 0)
                throw new System.Exception($"Pipe solver produced no segments for {pipelineData.Name}: {result.Message}");

            var pv = pipelineData.ProfileView.ProfileView;
            var polyline = new Polyline();

            foreach (var segment in result.Segments)
            {
                var startPoint = ToProfileViewPoint(pv, segment.Start);
                var endPoint = ToProfileViewPoint(pv, segment.End);

                if (polyline.NumberOfVertices == 0)
                {
                    polyline.AddVertexAt(polyline.NumberOfVertices, startPoint, 0.0, 0.0, 0.0);
                }
                else
                {
                    var existing = polyline.GetPoint2dAt(polyline.NumberOfVertices - 1);
                    if (!PointsEqual(existing, startPoint))
                    {
                        polyline.AddVertexAt(polyline.NumberOfVertices, startPoint, 0.0, 0.0, 0.0);
                    }
                }

                int bulgeIndex = polyline.NumberOfVertices - 1;
                polyline.SetBulgeAt(bulgeIndex, GetSegmentBulge(segment));

                if (!PointsEqual(polyline.GetPoint2dAt(polyline.NumberOfVertices - 1), endPoint))
                {
                    polyline.AddVertexAt(polyline.NumberOfVertices, endPoint, 0.0, 0.0, 0.0);
                }
            }

            if (polyline.NumberOfVertices < 2)
                throw new System.Exception($"Pipe solver polyline for {pipelineData.Name} has fewer than two vertices.");

            return polyline;
        }

        private static Point2d ToProfileViewPoint(ProfileView profileView, SeamPoint joint)
        {
            double x = 0.0;
            double y = 0.0;
            profileView.FindXYAtStationAndElevation(joint.Station, joint.Elevation, ref x, ref y);
            return new Point2d(x, y);
        }

        private static bool PointsEqual(Point2d left, Point2d right)
        {
            return left.GetDistanceTo(right) < 1e-6;
        }

        private static double GetSegmentBulge(SeamSegment segment)
        {
            if (!string.Equals(segment.Kind, "arc", StringComparison.OrdinalIgnoreCase) ||
                segment.SweepRad == null)
            {
                return 0.0;
            }

            return Math.Tan(segment.SweepRad.Value / 4.0);
        }

        // ── Request contract: Norsyn.seam.case.v1 ────────────────────────────

        private sealed class SeamCase
        {
            [JsonPropertyName("schema")] public string Schema { get; set; } = CaseSchema;
            [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
            [JsonPropertyName("surface_profile")] public List<double[]> SurfaceProfile { get; set; } = [];
            [JsonPropertyName("pipe_sizes")] public List<SeamPipeSize> PipeSizes { get; set; } = [];
            [JsonPropertyName("utilities")] public List<SeamUtility> Utilities { get; set; } = [];
            [JsonPropertyName("forbidden")] public List<double[]> Forbidden { get; set; } = [];
        }

        private sealed class SeamPipeSize
        {
            [JsonPropertyName("s_lo")] public double SLo { get; set; }
            [JsonPropertyName("s_hi")] public double SHi { get; set; }
            [JsonPropertyName("r_min_m")] public double RMinM { get; set; }
            [JsonPropertyName("jod_m")] public double JodM { get; set; }
        }

        private sealed class SeamUtility
        {
            [JsonPropertyName("s_lo")] public double SLo { get; set; }
            [JsonPropertyName("s_hi")] public double SHi { get; set; }
            [JsonPropertyName("y_lo")] public double YLo { get; set; }
            [JsonPropertyName("y_hi")] public double YHi { get; set; }
        }

        // ── Response contract: Norsyn.seam.result.v1 ─────────────────────────

        private sealed class SeamSolveResult
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
            public SeamSummary? Summary { get; set; }
            public List<SeamSegment> Segments { get; set; } = [];
        }

        private sealed class SeamSummary
        {
            public bool Success { get; set; }
            public bool RadiusOk { get; set; }
            public double RadiusDeficitM { get; set; }
            public int UtilityIntrusions { get; set; }
            public double CoverViolationM { get; set; }
        }

        private sealed class SeamSegment
        {
            public string Kind { get; set; } = string.Empty;
            public SeamPoint Start { get; set; } = new();
            public SeamPoint End { get; set; } = new();
            public double? SweepRad { get; set; }
        }

        private sealed class SeamPoint
        {
            public double Station { get; set; }
            public double Elevation { get; set; }
        }
    }
}
