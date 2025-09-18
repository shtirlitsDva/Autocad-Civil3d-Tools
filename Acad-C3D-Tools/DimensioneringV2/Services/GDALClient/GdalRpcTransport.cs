using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using BitMiracle.LibTiff.Classic;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Xps;
using System.Xml;

namespace DimensioneringV2.Services.GDALClient
{
    internal class GdalRpcTransport : IRpcTransport
    {
        private static readonly Lazy<GdalRpcTransport> _lazy = new(() => new GdalRpcTransport());
        public static GdalRpcTransport Instance => _lazy.Value;
        private static string? _token;
        private GdalRpcTransport() { }

        #region Gdalservice
        private string ServiceFolder =
            @"X:\AutoCAD DRI - 01 Civil 3D\NetloadV2\Dependencies\GdalService";
        private string ServiceExeName = "GDALService.exe";

        private readonly object _procSync = new();
        private readonly SemaphoreSlim _callLock = new(1, 1);

        // Progress routing for the current (single-flight) call
        private volatile string? _activeRpcId;
        private volatile IProgress<(int done, int total)>? _activeProgress;

        // ---- process state ----
        private Process? _proc;
        private StreamWriter? _stdin;
        private StreamReader? _stdout;
        private Task? _stderrPump;
        private Task? _stdoutPump;
        private int _nextReqId;

        // Simple request/response DTOs for NDJSON
        internal sealed record RpcReq(string id, string type, object? payload);
        internal sealed record RpcResp(string id, int status, JsonElement? result, string? error);
        private sealed record ProgressMsg(string? id, string? type, int? done, int? total, double? pct, string? message);

        private readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals |
                         JsonNumberHandling.AllowReadingFromString
        };

        public async Task EnsureServerAsync(CancellationToken ct)
        {
            lock (_procSync)
            {
                if (_proc is { HasExited: false } && _stdin != null && _stdout != null) return;

                // (Re)start server process
                _proc?.Dispose();
                _proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(ServiceFolder, ServiceExeName),
                        WorkingDirectory = ServiceFolder,
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    },
                    EnableRaisingEvents = true
                };
                if (!_proc.Start())
                    throw new InvalidOperationException("Failed to start GDALService.");

                _stdin = _proc.StandardInput;
                _stdout = _proc.StandardOutput;

                // Pump stderr for logs / READY / PROGRESS
                var srErr = _proc!.StandardError;
                _stderrPump = Task.Run(async () =>
                {
                    string? line;
                    while ((line = await srErr.ReadLineAsync().ConfigureAwait(false)) != null)
                    {
                        // Fast path: try parse JSON; ignore plain text logs
                        if (line.Length > 1 && line[0] == '{')
                        {
                            try
                            {
                                var pm = JsonSerializer.Deserialize<ProgressMsg>(line);
                                if (pm?.type?.Equals("PROGRESS", StringComparison.OrdinalIgnoreCase) == true)
                                {
                                    var id = pm.id;
                                    var progress = _activeProgress;
                                    // single-flight: just ensure it matches the current call
                                    if (progress != null && _activeRpcId != null && id == _activeRpcId)
                                    {
                                        var done = pm.done ?? 0;
                                        var total = pm.total ?? 0;
                                        progress.Report((done, total));
                                    }
                                    continue;
                                }
                            }
                            catch
                            {
                                // If it's not valid JSON or not a PROGRESS, just fall through to log
                            }
                        }

                        // Non-progress stderr: keep logging for diagnostics
                        System.Diagnostics.Debug.WriteLine("[GDALService] " + line);
                    }
                });
            }

            // wait for READY banner (stderr is already pumped), give it a short grace
            await Task.Delay(50, ct).ConfigureAwait(false);
        }

        public async Task<RpcResp> CallAsync(
            string type,
            object? payload, 
            CancellationToken ct = default,
            IProgress<(int done, int total)>? progress = null)
        {
            await EnsureServerAsync(ct).ConfigureAwait(false);

            await _callLock.WaitAsync(ct).ConfigureAwait(false); // one call at a time
            string? prevId = null;
            IProgress<(int, int)>? prevProgress = null;

            try
            {
                var id = Interlocked.Increment(ref _nextReqId).ToString();
                var req = new RpcReq(id, type, payload);
                var json = JsonSerializer.Serialize(req, _json);

                // Register progress sink for THIS call
                prevId = _activeRpcId;           // (should be null in single-flight)
                prevProgress = _activeProgress;  // (should be null in single-flight)
                _activeRpcId = id;
                _activeProgress = progress;

                // Optional: cancellation → send CANCEL control to service
                using var ctr = ct.Register(() =>
                {
                    try
                    {
                        // Best-effort CANCEL message; ignore failures
                        var cancel = JsonSerializer.Serialize(new RpcReq(id, "CANCEL", new { }));
                        _stdin?.WriteLine(cancel);
                        _stdin?.Flush();
                    }
                    catch { }
                });

                // Write the request
                try
                {
                    _stdin!.WriteLine(json);
                    _stdin.Flush();
                }
                catch
                {
                    SafeTearDownProcess();
                    throw;
                }

                // Read the final response line
                var line = await _stdout!.ReadLineAsync().WaitAsync(ct).ConfigureAwait(false);
                if (line is null)
                {
                    SafeTearDownProcess();
                    throw new EndOfStreamException("GDALService ended.");
                }

                var resp = JsonSerializer.Deserialize<RpcResp>(line, _json)!;
                if (resp.id != id)
                    throw new InvalidOperationException($"Mismatched RPC id. Expected {id}, got {resp.id}.");

                return resp;
            }
            finally
            {
                // Clear progress routing
                _activeRpcId = prevId;
                _activeProgress = prevProgress;

                _callLock.Release();
            }
        }
        public async Task<T> CallAsync<T>(
            string type,
            object? payload,
            CancellationToken ct = default,
            IProgress<(int done, int total)>? progress = null)
        {
            var resp = await CallAsync(type, payload, ct, progress).ConfigureAwait(false);
            if (resp.status != 0)
                throw new RpcException(type, resp.id, resp.status, resp.error ?? "Unknown error");
            if (resp.result is null)
                throw new RpcException(type, resp.id, resp.status, "Missing result");
            return resp.result.Value.Deserialize<T>(_json)!;
        }
        private void SafeTearDownProcess()
        {
            lock (_procSync)
            {
                try { _stdin?.Close(); } catch { }
                try
                {
                    if (_proc is { HasExited: false })
                        _proc.Kill(entireProcessTree: true);
                }
                catch { }
                _proc?.Dispose();
                _proc = null;
                _stdin = null;
                _stdout = null;
            }
        }
        public async ValueTask DisposeAsync()
        {
            // Only call this when the app is shutting down
            SafeTearDownProcess();
            if (_stderrPump is not null)
            {
                try { await _stderrPump.ConfigureAwait(false); } catch { }
            }
        }
        #endregion

        #region Downloading of GeoTIFFs
        public void DownloadElevationData(Extents3d bbox, string dbFileName)
        {
            string token = _token ??= GetToken();

            //Handle width and/or height larger than 10.000
            double resolution = 0.4; //m per. pixel
            int maxSideLength = 10000;

            //Final bbox tuple list
            HashSet<(Extents3d bbox, (int width, int height) wh)> finalExtents =
                new HashSet<(Extents3d bbox, (int width, int height) wh)>();

            //instantiate and initialize stack of BBOXes
            Stack<Extents3d> BBOXs = new Stack<Extents3d>();
            BBOXs.Push(bbox);

            while (BBOXs.Count > 0)
            {
                Extents3d currentBbox = BBOXs.Pop();
                var wAndH = CalcWandH(currentBbox, resolution);

                if (wAndH.width > maxSideLength || wAndH.height > maxSideLength)
                {//Need to split the BBOX

                    int divX = 1;
                    int divY = 1;

                    if (wAndH.width > maxSideLength)
                        divX = wAndH.width / maxSideLength + (wAndH.width / maxSideLength > 0 ? 1 : 0);
                    if (wAndH.height > maxSideLength)
                        divY = wAndH.height / maxSideLength + (wAndH.height / maxSideLength > 0 ? 1 : 0);

                    Utils.prtDbg($"Splitting image into parts: W: {divX}, H: {divY}, T: {divX * divY}.");
                    System.Windows.Forms.Application.DoEvents();
                    foreach (Extents3d newBbox in SplitBBOX(currentBbox, divX * divY))
                        BBOXs.Push(newBbox);
                }
                else
                {//Need NOT to split the BBOX
                    System.Windows.Forms.Application.DoEvents();
                    finalExtents.Add((currentBbox, wAndH));
                }
            }

            int imageCount = 0;

            string dbFilename = dbFileName;
            string elevationsDir = EnsureElevationsFolder(dbFilename);

            //Remember to persist
            string baseName = GenerateUniqueBaseName(elevationsDir);

            var elevationSettings = new ElevationSettings();
            elevationSettings.BaseFileName = baseName;
            SettingsSerializer<ElevationSettings>.Save(
                Autodesk.AutoCAD.ApplicationServices.Core.Application.
                DocumentManager.MdiActiveDocument, elevationSettings);

            foreach (var fe in finalExtents)
            {
                byte[] buffer = RequestWcsData(fe.bbox, fe.wh.width, fe.wh.height);
                if (buffer == default || buffer.Length == 0) continue;

                string iterationFileName = NextAvailableFilePath(
                    elevationsDir, baseName, ref imageCount);

                File.Delete(iterationFileName);
                File.WriteAllBytes(iterationFileName, buffer);

                MemoryStream ms = new MemoryStream(buffer);

                using (Tiff image = Tiff.ClientOpen("in-memory", "r", ms, new TiffStream()))
                {
                    FieldValue[] value = image.GetField(TiffTag.IMAGEWIDTH);
                    int width = value[0].ToInt();

                    value = image.GetField(TiffTag.IMAGELENGTH);
                    int height = value[0].ToInt();

                    value = image.GetField(TiffTag.XRESOLUTION);
                    float dpiX = value[0].ToFloat();

                    value = image.GetField(TiffTag.YRESOLUTION);
                    float dpiY = value[0].ToFloat();

                    Utils.prtDbg($"W: {width}, H: {height}, T: {width * height}, DPI: {dpiX}x{dpiY}");

                    image.SetFileName(iterationFileName);
                }

                Utils.prtDbg($"GeoTiff til terræn gemt som:\n{iterationFileName}.\n");
            }

            Utils.prtDbg($"Download af terræn data færdig.\n" +
                $"{imageCount} filer gemt i mappen:\n{elevationsDir}.\n");
        }
        private static byte[] RequestWcsData(Extents3d bbox, int width, int height)
        {
            string USER_NAME = IntersectUtilities.UtilsCommon.Infrastructure.USER_NAME_SHORT;
            string PASSWORD = IntersectUtilities.UtilsCommon.Infrastructure.PASSWORD;

            Uri BASE_ADDRESS = new Uri("https://services.datafordeler.dk");
            string REQUEST_URI =
                    $"https://services.datafordeler.dk/DHMNedboer/dhm_wcs/1.0.0/WCS" +
                    $"?username={USER_NAME}&password={PASSWORD}" +
                    $"&COVERAGE=dhm_terraen" +
                    $"&SERVICE=WCS" +
                    $"&REQUEST=GetCoverage" +
                    $"&VERSION=1.0.0" +
                    $"&STYLE=default" +
                    $"&FORMAT=GTiff" +
                    $"&BBOX={BboxToString(bbox)}" +
                    $"&CRS=EPSG:25832" +
                    $"&RESPONSE_CRS=EPSG:25832" +
                    $"&WIDTH={width}" +
                    $"&HEIGHT={height}"
                ;

            //request a resource with the token
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization
                = new AuthenticationHeaderValue("Bearer", _token ??= GetToken());
            client.BaseAddress = BASE_ADDRESS;
            var task = Task.Run(() => client.GetByteArrayAsync(REQUEST_URI));
            task.Wait();

            return task.Result;
        }
        #endregion

        #region Filename handling
        private static string EnsureElevationsFolder(string projectFilePath)
        {
            var projectDir = Path.GetDirectoryName(projectFilePath)
                ?? throw new InvalidOperationException("Project path has no directory.");
            var elevDir = Path.Combine(projectDir, "Elevations");
            Directory.CreateDirectory(elevDir);
            return elevDir;
        }

        private static string GenerateUniqueBaseName(string elevDir)
        {
            // Ensure the random base doesn't collide with anything like BASE_*.tif already there.
            while (true)
            {
                var candidate = RandomBase(8);
                bool exists = Directory.EnumerateFiles(elevDir, candidate + "_*.tif").Any();
                if (!exists) return candidate;
            }
        }

        private static string NextAvailableFilePath(string elevDir, string baseName, ref int index)
        {
            while (true)
            {
                index++;
                var path = Path.Combine(elevDir, $"{baseName}_{index}.tif");
                // Reserve atomically: CreateNew will throw if it exists (race-safe).
                try
                {
                    using var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    return path; // caller will write the bytes to this same path (or re-open if preferred)
                }
                catch (IOException)
                {
                    // File existed; try next index
                    continue;
                }
            }
        }

        private static string RandomBase(int len)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var bytes = new byte[len];
            RandomNumberGenerator.Fill(bytes);
            var chars = new char[len];
            for (int i = 0; i < len; i++)
                chars[i] = alphabet[bytes[i] % alphabet.Length];
            return new string(chars);
        }
        #endregion

        #region BBOX manipulation
        private static (int width, int height) CalcWandH(Extents3d bbox, double resolution)
        {
            int width = (int)((bbox.MaxPoint.X - bbox.MinPoint.X) / resolution);
            int height = (int)((bbox.MaxPoint.Y - bbox.MinPoint.Y) / resolution);
            return (width, height);
        }
        private static List<Extents3d> SplitBBOX(Extents3d bbox, int div)
        {
            var factors = Multipliers(div);

            List<Extents3d> result = new List<Extents3d>();

            double startX = bbox.MinPoint.X;
            double startY = bbox.MinPoint.Y;

            double Lx = bbox.MaxPoint.X - bbox.MinPoint.X;
            double Ly = bbox.MaxPoint.Y - bbox.MinPoint.Y;
            double factor = Lx > Ly ? Lx / Ly : Ly / Lx;

            var nearest = factors.MinBy(x => Math.Abs(factor - x.F));
            double largeFactor = nearest.A;
            double smallFactor = nearest.B;
            double largeSide = 0;
            double smallSide = 0;
            double largeStart = 0;
            double smallStart = 0;
            if (Lx > Ly)
            {//large number to X side
                largeSide = Lx;
                smallSide = Ly;
                largeStart = startX;
                smallStart = startY;
            }
            else
            {//large number to Y side
                largeSide = Ly;
                smallSide = Lx;
                largeStart = startY;
                smallStart = startX;
            }

            double deltaLarge = largeSide / largeFactor;
            double deltaSmall = smallSide / smallFactor;
            List<double> Largelist = new List<double>();
            for (int i = 0; i < largeFactor + 1; i++) Largelist.Add(largeStart + i * deltaLarge);
            List<double> Smalllist = new List<double>();
            for (int i = 0; i < smallFactor + 1; i++) Smalllist.Add(smallStart + i * deltaSmall);

            if (Lx > Ly) //X is the large side
            {
                for (int i = 0; i < Smalllist.Count - 1; i++)
                {
                    double minY = Smalllist[i];
                    double maxY = Smalllist[i + 1];

                    for (int j = 0; j < Largelist.Count - 1; j++)
                    {
                        double minX = Largelist[j];
                        double maxX = Largelist[j + 1];

                        result.Add(new Extents3d(
                            new Point3d(minX, minY, 0.0),
                            new Point3d(maxX, maxY, 0.0)));
                    }
                }
            }
            else //Y is the large side
            {
                for (int i = 0; i < Largelist.Count - 1; i++)
                {
                    double minY = Largelist[i];
                    double maxY = Largelist[i + 1];

                    for (int j = 0; j < Smalllist.Count - 1; j++)
                    {
                        double minX = Smalllist[j];
                        double maxX = Smalllist[j + 1];

                        result.Add(new Extents3d(
                            new Point3d(minX, minY, 0.0),
                            new Point3d(maxX, maxY, 0.0)));
                    }
                }
            }
            return result;
        }
        private static IEnumerable<(int A, int B, double F)> Multipliers(int m)
        {
            yield return (m, 1, m / 1.0);

            int finalVal = (int)Math.Sqrt(m);
            int increment = m % 2 != 0 ? 2 : 1;
            int i = m % 2 != 0 ? 3 : 2;

            while (i <= finalVal)
            {
                if (m % i == 0)
                {
                    yield return (m / i, i, (double)m / i / i);
                }

                i += increment;
            }
        }
        private static string BboxToString(Extents3d extents)
        {
            return
                $"{extents.MinPoint.X}," +
                $"{extents.MinPoint.Y}," +
                $"{extents.MaxPoint.X}," +
                $"{extents.MaxPoint.Y}";
        }
        #endregion

        #region Get token
        private static string GetToken()
        {
            string ADFS_URL = IntersectUtilities.UtilsCommon.Infrastructure.ADFS_URL;
            string USER_NAME = IntersectUtilities.UtilsCommon.Infrastructure.USER_NAME_LONG;
            string PASSWORD = IntersectUtilities.UtilsCommon.Infrastructure.PASSWORD;
            string REALM = IntersectUtilities.UtilsCommon.Infrastructure.REALM;

            string soapMessage = CreateSoapMessage(USER_NAME, PASSWORD, REALM);
            string soapAction = "http://docs.oasis-open.org/ws-sx/ws-trust/200512/RST/Issue";

            using var httpClient = new HttpClient();
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ADFS_URL)
            {
                Content = new StringContent(soapMessage, Encoding.UTF8, "application/soap+xml")
            };

            httpRequest.Headers.Add("SOAPAction", soapAction);

            var response = httpClient.Send(httpRequest);
            if (response.IsSuccessStatusCode)
            {
                string responseContent = response.Content.ReadAsStringAsync().Result;  // Synchronous read of the response content
                return ExtractToken(responseContent);
            }

            throw new Exception("Failed to obtain token");
        }
        private static string CreateSoapMessage(string username, string password, string realm)
        {
            string ADFS_URL = IntersectUtilities.UtilsCommon.Infrastructure.ADFS_URL;

            return $@"<s:Envelope xmlns:s='http://www.w3.org/2003/05/soap-envelope'
                               xmlns:a='http://www.w3.org/2005/08/addressing'
                               xmlns:u='http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd'>
            <s:Header>
                <a:Action s:mustUnderstand='1'>http://docs.oasis-open.org/ws-sx/ws-trust/200512/RST/Issue</a:Action>
                <a:MessageID>urn:uuid:{Guid.NewGuid()}</a:MessageID>
                <a:ReplyTo>
                    <a:Address>http://www.w3.org/2005/08/addressing/anonymous</a:Address>
                </a:ReplyTo>
                <a:To s:mustUnderstand='1'>{ADFS_URL}</a:To>
                <o:Security s:mustUnderstand='1'
                    xmlns:o='http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd'>
                    <o:UsernameToken>
                        <o:Username>{username}</o:Username>
                        <o:Password>{password}</o:Password>
                    </o:UsernameToken>
                </o:Security>
            </s:Header>
            <s:Body>
                <trust:RequestSecurityToken xmlns:trust='http://docs.oasis-open.org/ws-sx/ws-trust/200512'>
                    <wsp:AppliesTo xmlns:wsp='http://schemas.xmlsoap.org/ws/2004/09/policy'>
                        <a:EndpointReference>
                            <a:Address>{realm}</a:Address>
                        </a:EndpointReference>
                    </wsp:AppliesTo>
                    <trust:KeyType>http://docs.oasis-open.org/ws-sx/ws-trust/200512/Bearer</trust:KeyType>
                    <trust:RequestType>http://docs.oasis-open.org/ws-sx/ws-trust/200512/Issue</trust:RequestType>
                    <trust:TokenType>urn:oasis:names:tc:SAML:2.0:assertion</trust:TokenType>
                </trust:RequestSecurityToken>
            </s:Body>
        </s:Envelope>";
        }
        private static string ExtractToken(string soapResponse)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(soapResponse);
            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("s", "http://www.w3.org/2003/05/soap-envelope");
            nsmgr.AddNamespace("a", "http://www.w3.org/2005/08/addressing");
            nsmgr.AddNamespace("assertion", "urn:oasis:names:tc:SAML:2.0:assertion");
            XmlNode tokenNode = doc.SelectSingleNode("//assertion:Assertion", nsmgr);
            if (tokenNode != null)
            {
                return tokenNode.OuterXml;
            }
            throw new Exception("Token not found in response");
        }
        #endregion
    }
}
