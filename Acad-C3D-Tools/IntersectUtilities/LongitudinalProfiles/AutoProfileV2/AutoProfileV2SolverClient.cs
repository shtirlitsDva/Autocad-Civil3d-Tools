using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.LongitudinalProfiles.AutoProfileV2;
using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

namespace IntersectUtilities
{
    internal sealed class AutoProfileV2SolverClient : IDisposable
    {
        private const string PipeSolverServiceUrlEnvVar = "AUTOPROFILE_SOLVER_URL";
        private const string PipeSolverServiceExeEnvVar = "AUTOPROFILE_SOLVER_EXE";
        private const string PipeSolverDefaultBaseUrl = "http://127.0.0.1:5061";
        //private const string PipeSolverEngine = "python";
        private const string PipeSolverEngine = "native";
        private const string PipeSolverDefaultExecutablePath =
            @"X:\AutoCAD DRI - 01 Civil 3D\NetloadV2\Dependencies\ApService\PipeSolver.Interop.exe";
        private static readonly TimeSpan PipeSolverRequestTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan PipeSolverHealthTimeout = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan PipeSolverJobTimeout = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan PipeSolverPollInterval = TimeSpan.FromSeconds(2);
        private static readonly JsonSerializerOptions PipeSolverJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
        };

        private readonly Action<string> _log;
        private readonly HttpClient _httpClient;
        private string? _baseUrl;

        public AutoProfileV2SolverClient(Action<string> log)
        {
            _log = log;
            _httpClient = new HttpClient
            {
                Timeout = PipeSolverRequestTimeout
            };
        }

        public Polyline SolveProfilePolyline(AP2_PipelineData pipelineData)
        {
            _baseUrl ??= ResolvePipeSolverBaseUrl();
            EnsurePipeSolverServiceRunning(_baseUrl);
            WaitForPipeSolverReady(_baseUrl);

            var scene = BuildPipeSolverScene(pipelineData);
            var submission = SubmitPipeSolverJob(_baseUrl, pipelineData.Name, scene);
            var result = WaitForPipeSolverResult(_baseUrl, submission.JobId, pipelineData.Name);

            foreach (string warning in result.Warnings ?? [])
            {
                _log($"Pipe solver warning for {pipelineData.Name}: {warning}");
            }

            return BuildProfilePolylineFromSolveResult(pipelineData, result);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        private string ResolvePipeSolverBaseUrl()
        {
            var configured = Environment.GetEnvironmentVariable(PipeSolverServiceUrlEnvVar);
            var baseUrl = string.IsNullOrWhiteSpace(configured)
                ? PipeSolverDefaultBaseUrl
                : configured.Trim().TrimEnd('/');

            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new System.Exception(
                    $"Pipe solver service URL is invalid: '{baseUrl}'. " +
                    $"Set {PipeSolverServiceUrlEnvVar} to a valid absolute http(s) URL.");
            }

            return baseUrl;
        }

        private void EnsurePipeSolverServiceRunning(string baseUrl)
        {
            if (TryGetPipeSolverHealth(baseUrl) != null)
            {
                return;
            }

            string serviceExe = ResolvePipeSolverExecutablePath();
            string? directory = Path.GetDirectoryName(serviceExe);
            if (directory == null || !Directory.Exists(directory))
            {
                throw new FileNotFoundException(
                    $"Pipe solver service executable directory does not exist: {serviceExe}");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = serviceExe,
                WorkingDirectory = directory,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            _log($"Starting pipe solver service: {serviceExe}");
            Process.Start(startInfo);
        }

        private string ResolvePipeSolverExecutablePath()
        {
            var configured = Environment.GetEnvironmentVariable(PipeSolverServiceExeEnvVar);
            var serviceExe = string.IsNullOrWhiteSpace(configured)
                ? PipeSolverDefaultExecutablePath
                : Environment.ExpandEnvironmentVariables(configured.Trim().Trim('"'));

            if (!File.Exists(serviceExe))
            {
                throw new FileNotFoundException(
                    $"Pipe solver service executable was not found: {serviceExe}. " +
                    $"Publish PipeSolver.Interop to that path or override it with {PipeSolverServiceExeEnvVar}.",
                    serviceExe);
            }

            return Path.GetFullPath(serviceExe);
        }

        private void WaitForPipeSolverReady(string baseUrl)
        {
            DateTime deadline = DateTime.UtcNow + PipeSolverHealthTimeout;
            string lastState = "unknown";
            string? lastError = null;

            while (DateTime.UtcNow < deadline)
            {
                var health = TryGetPipeSolverHealth(baseUrl);
                if (health != null)
                {
                    lastState = health.Environment ?? health.Status ?? "unknown";
                    lastError = health.Error;

                    if (string.Equals(health.Environment, "ready", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(health.Status, "ok", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    if (string.Equals(health.Environment, "failed", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(health.Status, "failed", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new System.Exception(
                            $"Pipe solver service failed during startup: {health.Error ?? "unknown error"}");
                    }
                }

                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(PipeSolverPollInterval);
            }

            throw new TimeoutException(
                $"Timed out waiting for pipe solver service readiness at {baseUrl}. " +
                $"Last state: {lastState}. Last error: {lastError ?? "<none>"}.");
        }

        private PipeSolverHealthResponse? TryGetPipeSolverHealth(string baseUrl)
        {
            try
            {
                using var response = _httpClient.GetAsync($"{baseUrl.TrimEnd('/')}/health").GetAwaiter().GetResult();
                var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonSerializer.Deserialize<PipeSolverHealthResponse>(json, PipeSolverJsonOptions);
            }
            catch
            {
                return null;
            }
        }

        private PipeSolverSceneSpec BuildPipeSolverScene(AP2_PipelineData pipelineData)
        {
            if (pipelineData.ProfileView == null) throw new System.Exception($"No profile view found for {pipelineData.Name}.");
            if (pipelineData.SurfaceProfile == null) throw new System.Exception($"No surface profile found for {pipelineData.Name}.");
            if (pipelineData.SurfaceProfile.SurfacePolylineSimplified == null)
                throw new System.Exception($"No simplified surface profile found for {pipelineData.Name}.");
            if (pipelineData.SizeArray == null) throw new System.Exception($"No size array found for {pipelineData.Name}.");

            var sizeEntries = pipelineData.SizeArray.Sizes.ToList();
            if (sizeEntries.Count == 0) throw new System.Exception($"No size entries found for {pipelineData.Name}.");

            double coverDepth = GetUniformCoverDepth(sizeEntries, pipelineData.Name);
            var pv = pipelineData.ProfileView.ProfileView;
            var surfacePolyline = pipelineData.SurfaceProfile.SurfacePolylineSimplified;
            var surfacePoints = new List<(double Station, double Elevation)>();

            for (int i = 0; i < surfacePolyline.NumberOfVertices; i++)
            {
                double station = 0.0;
                double elevation = 0.0;
                var point = surfacePolyline.GetPoint2dAt(i);
                pv.FindStationAndElevationAtXY(point.X, point.Y, ref station, ref elevation);
                surfacePoints.Add((station, elevation));
            }

            var dedupedSurface = surfacePoints
                .OrderBy(point => point.Station)
                .GroupBy(point => point.Station, (_, group) => group.Last())
                .Select(point => new List<double> { point.Station, point.Elevation })
                .ToList();

            if (dedupedSurface.Count < 2)
                throw new System.Exception($"Surface profile for {pipelineData.Name} did not yield enough unique points.");

            var pipelineSizes = sizeEntries
                .OrderBy(size => size.StartStation)
                .Select(size => new PipeSolverPipelineSizeRange
                {
                    StartStation = size.StartStation,
                    EndStation = size.EndStation,
                    MinVerticalRadiusM = size.VerticalMinRadius,
                    JodMm = size.Kod
                })
                .ToList();

            var utilities = pipelineData.Utility
                .OrderBy(utility => utility.StartStation)
                .Select((utility, index) => new PipeSolverUtilityObstacle
                {
                    Name = $"{pipelineData.Name}_utility_{index:000}",
                    Box =
                    [
                        utility.StartStation,
                        utility.BottomElevation,
                        utility.EndStation,
                        utility.TopElevation
                    ],
                    Active = true,
                    MinDistanceM = 0.0
                })
                .ToList();

            return new PipeSolverSceneSpec
            {
                Name = pipelineData.Name,
                Environment = new PipeSolverEnvironmentSpec
                {
                    SurfaceProfile = dedupedSurface,
                    PipelineSizes = pipelineSizes,
                    CoverM = coverDepth,
                    DefaultJodM = pipelineSizes[0].JodMm / 1000.0,
                    DefaultMinRadiusM = pipelineSizes[0].MinVerticalRadiusM
                },
                Local = new PipeSolverLocalConstraints
                {
                    HorizontalArcs = pipelineData.HorizontalArcs
                        .OrderBy(arc => arc.StartStation)
                        .Select(arc => new List<double> { arc.StartStation, arc.EndStation })
                        .ToList(),
                    Utilities = utilities
                },
                Metadata = new Dictionary<string, object?>
                {
                    ["source"] = "civil3d_apcreatev2",
                    ["alignment_name"] = pipelineData.Name
                }
            };
        }

        private static double GetUniformCoverDepth(IReadOnlyList<SizeEntryV2> sizeEntries, string pipelineName)
        {
            var distinctCoverDepths = new List<double>();

            foreach (var size in sizeEntries)
            {
                double coverDepth = GetCoverDepth(size.DN, size.System, size.Type);
                if (!distinctCoverDepths.Any(existing => Math.Abs(existing - coverDepth) < 1e-6))
                {
                    distinctCoverDepths.Add(coverDepth);
                }
            }

            if (distinctCoverDepths.Count != 1)
            {
                throw new System.Exception(
                    $"Pipe solver currently requires one uniform cover depth per alignment. " +
                    $"{pipelineName} has {distinctCoverDepths.Count} distinct cover depths: " +
                    $"{string.Join(", ", distinctCoverDepths.Select(x => x.ToString("F3", CultureInfo.InvariantCulture)))}");
            }

            return distinctCoverDepths[0];
        }

        private PipeSolverJobSubmissionResponse SubmitPipeSolverJob(
            string baseUrl,
            string pipelineName,
            PipeSolverSceneSpec scene)
        {
            string safeName = Regex.Replace(pipelineName, "[^A-Za-z0-9_\\-]+", "_");
            var request = new PipeSolverSubmitRequest
            {
                Engine = PipeSolverEngine,
                JobName = $"apcreatev2_{safeName}",
                SessionName = $"apcreatev2_{safeName}_{DateTime.UtcNow:yyyyMMddHHmmssfff}",
                Scene = scene,
                SaveFigures = false,
                Strict = true,
                CorridorBackend = "legacy",
                AlignmentBackend = "parallel_atomic"
            };

            using var response = _httpClient
                .PostAsJsonAsync($"{baseUrl.TrimEnd('/')}/jobs", request, PipeSolverJsonOptions)
                .GetAwaiter()
                .GetResult();

            string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                throw new System.Exception(
                    $"Pipe solver job submission failed for {pipelineName}: {(int)response.StatusCode} {response.ReasonPhrase}. {json}");
            }

            var submission = JsonSerializer.Deserialize<PipeSolverJobSubmissionResponse>(json, PipeSolverJsonOptions);
            if (submission == null || string.IsNullOrWhiteSpace(submission.JobId))
            {
                throw new System.Exception($"Pipe solver job submission for {pipelineName} returned no job id.");
            }

            return submission;
        }

        private PipeSolverSolveResult WaitForPipeSolverResult(string baseUrl, string jobId, string pipelineName)
        {
            DateTime deadline = DateTime.UtcNow + PipeSolverJobTimeout;

            while (DateTime.UtcNow < deadline)
            {
                using var statusResponse = _httpClient
                    .GetAsync($"{baseUrl.TrimEnd('/')}/jobs/{jobId}")
                    .GetAwaiter()
                    .GetResult();

                string statusJson = statusResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!statusResponse.IsSuccessStatusCode)
                {
                    throw new System.Exception(
                        $"Failed to query pipe solver job status for {pipelineName}: " +
                        $"{(int)statusResponse.StatusCode} {statusResponse.ReasonPhrase}. {statusJson}");
                }

                var status = JsonSerializer.Deserialize<PipeSolverJobStatusResponse>(statusJson, PipeSolverJsonOptions);
                if (status == null || string.IsNullOrWhiteSpace(status.State))
                {
                    throw new System.Exception(
                        $"Pipe solver returned an invalid job status payload for {pipelineName}: {statusJson}");
                }

                if (string.Equals(status.State, "succeeded", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(status.State, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    using var resultResponse = _httpClient
                        .GetAsync($"{baseUrl.TrimEnd('/')}/jobs/{jobId}/result")
                        .GetAwaiter()
                        .GetResult();

                    string resultJson = resultResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    if (!resultResponse.IsSuccessStatusCode)
                    {
                        throw new System.Exception(
                            $"Failed to fetch pipe solver result for {pipelineName}: " +
                            $"{(int)resultResponse.StatusCode} {resultResponse.ReasonPhrase}. {resultJson}");
                    }

                    var envelope = JsonSerializer.Deserialize<PipeSolverResultEnvelope>(resultJson, PipeSolverJsonOptions);
                    if (envelope?.Result == null)
                    {
                        throw new System.Exception(
                            $"Pipe solver returned no result payload for {pipelineName}. Envelope: {resultJson}");
                    }

                    if (!envelope.Result.Success)
                    {
                        throw new System.Exception(
                            $"Pipe solver failed for {pipelineName}: " +
                            $"{envelope.Result.Error ?? envelope.Error ?? status.Error ?? "unknown error"}");
                    }

                    return envelope.Result;
                }

                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(PipeSolverPollInterval);
            }

            throw new TimeoutException(
                $"Timed out waiting for pipe solver job {jobId} for {pipelineName} after {PipeSolverJobTimeout.TotalMinutes:0} minutes.");
        }

        private static Polyline BuildProfilePolylineFromSolveResult(
            AP2_PipelineData pipelineData,
            PipeSolverSolveResult result)
        {
            if (pipelineData.ProfileView == null) throw new System.Exception($"No profile view found for {pipelineData.Name}.");
            if (result.FinalSolution == null) throw new System.Exception($"Pipe solver produced no final solution for {pipelineData.Name}.");
            if (result.FinalSolution.Segments == null || result.FinalSolution.Segments.Count == 0)
                throw new System.Exception($"Pipe solver produced no segments for {pipelineData.Name}.");

            var pv = pipelineData.ProfileView.ProfileView;
            var polyline = new Polyline();

            foreach (var segment in result.FinalSolution.Segments)
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

        private static Point2d ToProfileViewPoint(ProfileView profileView, PipeSolverJointState joint)
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

        private static double GetSegmentBulge(PipeSolverPrimitiveSegment segment)
        {
            if (!string.Equals(segment.Kind, "arc", StringComparison.OrdinalIgnoreCase) ||
                segment.SweepRad == null)
            {
                return 0.0;
            }

            return Math.Tan(segment.SweepRad.Value / 4.0);
        }

        private sealed class PipeSolverHealthResponse
        {
            public string? Status { get; set; }
            public string? Environment { get; set; }
            public string? Error { get; set; }
        }

        private sealed class PipeSolverSubmitRequest
        {
            public string Engine { get; set; } = PipeSolverEngine;
            public string? JobName { get; set; }
            public string? SessionName { get; set; }
            public PipeSolverSceneSpec? Scene { get; set; }
            public bool SaveFigures { get; set; }
            public bool Strict { get; set; }
            public string CorridorBackend { get; set; } = "legacy";
            public string AlignmentBackend { get; set; } = "parallel_atomic";
        }

        private sealed class PipeSolverJobSubmissionResponse
        {
            public string JobId { get; set; } = string.Empty;
        }

        private sealed class PipeSolverJobStatusResponse
        {
            public string State { get; set; } = string.Empty;
            public string? Error { get; set; }
        }

        private sealed class PipeSolverResultEnvelope
        {
            public PipeSolverSolveResult? Result { get; set; }
            public string? Error { get; set; }
        }

        private sealed class PipeSolverSolveResult
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
            public List<string>? Warnings { get; set; }
            public PipeSolverArcActionSolution? FinalSolution { get; set; }
        }

        private sealed class PipeSolverArcActionSolution
        {
            public List<PipeSolverPrimitiveSegment> Segments { get; set; } = [];
        }

        private sealed class PipeSolverPrimitiveSegment
        {
            public string Kind { get; set; } = string.Empty;
            public PipeSolverJointState Start { get; set; } = new();
            public PipeSolverJointState End { get; set; } = new();
            public double? SweepRad { get; set; }
        }

        private sealed class PipeSolverJointState
        {
            public double Station { get; set; }
            public double Elevation { get; set; }
        }

        private sealed class PipeSolverSceneSpec
        {
            public string Name { get; set; } = string.Empty;
            public PipeSolverEnvironmentSpec Environment { get; set; } = new();
            public PipeSolverLocalConstraints Local { get; set; } = new();
            public Dictionary<string, object?> Metadata { get; set; } = new();
        }

        private sealed class PipeSolverEnvironmentSpec
        {
            public List<List<double>> SurfaceProfile { get; set; } = [];
            public List<PipeSolverPipelineSizeRange> PipelineSizes { get; set; } = [];
            public double CoverM { get; set; }
            public double DefaultJodM { get; set; }
            public double DefaultMinRadiusM { get; set; }
        }

        private sealed class PipeSolverLocalConstraints
        {
            public List<List<double>> HorizontalArcs { get; set; } = [];
            public List<PipeSolverUtilityObstacle> Utilities { get; set; } = [];
        }

        private sealed class PipeSolverPipelineSizeRange
        {
            public double StartStation { get; set; }
            public double EndStation { get; set; }
            public double MinVerticalRadiusM { get; set; }
            public double JodMm { get; set; }
        }

        private sealed class PipeSolverUtilityObstacle
        {
            public string Name { get; set; } = string.Empty;
            public List<double> Box { get; set; } = [];
            public bool Active { get; set; }
            public double MinDistanceM { get; set; }
        }
    }
}
