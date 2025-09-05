using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using BitMiracle.LibTiff.Classic;

using OSGeo.GDAL;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace DimensioneringV2.Services.Elevations
{
    internal class ElevationService
    {
        private static ElevationService? _instance;
        public static ElevationService Instance => _instance ??= new ElevationService();
        private static string? _token;
        private ElevationSettings? elevationSettings;        
        private ElevationService() { }
        private ElevationGdalVrtHandle? _vrt;
        private ThreadLocal<ElevationSampler>? _tls;

        #region Elevation Sampler
        public double? SampleElevation25832(double x, double y, bool bilinear = true)
        {
            PublishElevationData();
            return _tls!.Value!.Sample(x, y, bilinear);
        }

        /// <summary>
        /// Samples elevations for the given points. Returns elevations in the same order.
        /// </summary>
        public async Task<IReadOnlyList<double?>> SampleBulkAsync(
            IReadOnlyList<PointXY> points,
            int maxDegreeOfParallelism = 0,
            IProgress<(int done, int total)>? progress = null,
            CancellationToken ct = default,
            int progressBatchSize = 50)
        {
            PublishElevationData();
            if (_vrt == null) throw new InvalidOperationException("VRT not ready.");

            if (maxDegreeOfParallelism <= 0)
                maxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1);

            var total = points.Count;
            if (total == 0) return Array.Empty<double?>();

            var results = new double?[total];
            var range = System.Collections.Concurrent.Partitioner.Create(
                0, total, Math.Max(256, total / (maxDegreeOfParallelism * 8)));

            var vrtPath = _vrt.VrtPath;
            int done = 0;

            await Task.WhenAll(
                Enumerable.Range(0, maxDegreeOfParallelism).Select(async _ =>
                {
                    using var sampler = new ElevationSampler(vrtPath);

                    int localCompleted = 0; // <-- per worker counter

                    foreach (var (from, to) in range.GetDynamicPartitions())
                    {
                        for (int i = from; i < to; i++)
                        {
                            ct.ThrowIfCancellationRequested();

                            var p = points[i];
                            results[i] = sampler.Sample(p.X, p.Y, bilinear: true);

                            // Batch progress
                            if (progress != null && ++localCompleted >= progressBatchSize)
                            {
                                int now = Interlocked.Add(ref done, localCompleted);
                                localCompleted = 0;
                                progress.Report((now, total));
                            }
                        }
                        // Optional: yield to keep UI responsive (cheap)
                        await Task.Yield();
                    }

                    // Final flush for any remainder
                    if (progress != null && localCompleted > 0)
                    {
                        int now = Interlocked.Add(ref done, localCompleted);
                        progress.Report((now, total));
                    }
                })
            ).ConfigureAwait(false);

            return new ReadOnlyCollection<double?>(results);
        }

        private sealed class ElevationSampler : IDisposable
        {
            private readonly Dataset _ds;
            private readonly Band _band;
            private readonly double[] _gt = new double[6];
            private readonly double _noData; private readonly bool _hasNoData;

            public ElevationSampler(string vrtPath)
            {
                _ds = Gdal.Open(vrtPath, Access.GA_ReadOnly) ?? throw new InvalidOperationException($"Open failed: {vrtPath}");
                _band = _ds.GetRasterBand(1);
                _ds.GetGeoTransform(_gt);
                double nd; int has; _band.GetNoDataValue(out nd, out has); _noData = nd; _hasNoData = has != 0;
            }

            public double? Sample(double x, double y, bool bilinear = true)
            {
                double det = _gt[1] * _gt[5] - _gt[2] * _gt[4];
                if (Math.Abs(det) < 1e-18) return null;

                double col = (_gt[5] * (x - _gt[0]) - _gt[2] * (y - _gt[3])) / det;
                double row = (-_gt[4] * (x - _gt[0]) + _gt[1] * (y - _gt[3])) / det;

                return bilinear ? ReadBilinear(row, col) : ReadNearest((int)Math.Round(row), (int)Math.Round(col));
            }

            private double? ReadNearest(int r, int c)
            {
                if (r < 0 || c < 0 || r >= _band.YSize || c >= _band.XSize) return null;
                float[] buf = new float[1];
                _band.ReadRaster(c, r, 1, 1, buf, 1, 1, 0, 0);
                if (_hasNoData && Math.Abs(buf[0] - _noData) <= 1e-6) return null;
                return buf[0];
            }

            private double? ReadBilinear(double r, double c)
            {
                int r0 = (int)Math.Floor(r), c0 = (int)Math.Floor(c);
                int r1 = r0 + 1, c1 = c0 + 1;
                if (r0 < 0 || c0 < 0 || r1 >= _band.YSize || c1 >= _band.XSize) return null;

                float[] b = new float[4];
                _band.ReadRaster(c0, r0, 2, 2, b, 2, 2, 0, 0);
                bool bad(int i) => _hasNoData && Math.Abs(b[i] - _noData) <= 1e-6;
                if (bad(0) || bad(1) || bad(2) || bad(3)) return ReadNearest((int)Math.Round(r), (int)Math.Round(c));

                double dr = r - r0, dc = c - c0;
                double v00 = b[0], v01 = b[1], v10 = b[2], v11 = b[3];
                double v0 = v00 * (1 - dc) + v01 * dc;
                double v1 = v10 * (1 - dc) + v11 * dc;
                return v0 * (1 - dr) + v1 * dr;
            }

            public void Dispose() { _band.Dispose(); _ds.Dispose(); }
        }
        #endregion

        #region Publish elevation data
        public void PublishElevationData()
        {
            elevationSettings = SettingsSerializer<ElevationSettings>.Load(
                Autodesk.AutoCAD.ApplicationServices.Core.Application.
                DocumentManager.MdiActiveDocument);

            if (string.IsNullOrEmpty(elevationSettings?.BaseFileName))
            {
                Utils.prtDbg("Ingen indstillinger for terrændata fundet.\n" +
                    "Download, venligst, først noget terræn data.");
                return;
            }

            var baseName = elevationSettings.BaseFileName;

            // If cached VRT matches, we can simply return (already prepared)
            if (_vrt != null && string.Equals(_vrt.BaseName, baseName, StringComparison.OrdinalIgnoreCase))
            {
                // already built & cached
                return;
            }            

            //Build new VRT
            var dbFileName = Autodesk.AutoCAD.ApplicationServices.Core.Application.
                    DocumentManager.MdiActiveDocument.Database.Filename;
            string elevationsDir = EnsureElevationsFolder(dbFileName);

            var geoTiffFiles = Directory.EnumerateFiles(
                elevationsDir, elevationSettings.BaseFileName + "_*.tif").ToList();

            if (geoTiffFiles.Count == 0)
            {
                Utils.prtDbg("Ingen GeoTIFF filer, tilhørende projektet, fundet i mappen:\n" +
                    $"{elevationsDir}.\n" +
                    "Hent venligst først noget terræn data.");
                return;
            }

            // Load existing VRT if present; otherwise create a new one and cache it.
            EnsureVrtCached(baseName, elevationsDir, geoTiffFiles);
            if (_vrt == null)
                throw new InvalidOperationException("Failed to build or load VRT.");

            if (_vrt != null && _tls == null)
            {
                var vrtPath = _vrt.VrtPath;
                _tls = new ThreadLocal<ElevationSampler>(
                    () => new ElevationSampler(vrtPath),
                    trackAllValues: true);
            }

            // (Optional) warm-up: read geotransform / nodata once for later queries
            double[] gt = new double[6];
            _vrt.Dataset.GetGeoTransform(gt);
            var band = _vrt.Dataset.GetRasterBand(1);
            
            double noDataValue;
            int hasNoData;
            band.GetNoDataValue(out noDataValue, out hasNoData);

            Utils.prtDbg($"VRT ready: {Path.GetFileName(_vrt.VrtPath)} | " +
                         $"Size: {_vrt.Dataset.RasterXSize}x{_vrt.Dataset.RasterYSize} | " +
                         $"NoData: {(hasNoData != 0 ? noDataValue.ToString() : "n/a")}");
        }
        private readonly object _vrtLock = new();        
        private static string VrtPathOf(string elevationsDir, string baseName)
            => Path.Combine(elevationsDir, baseName + ".vrt");
        private ElevationGdalVrtHandle EnsureVrtCached(string baseName, string elevationsDir, List<string> geoTiffFiles)
        {
            if (geoTiffFiles.Count == 0)
                throw new InvalidOperationException("No GeoTIFF files to build or validate VRT.");

            geoTiffFiles = geoTiffFiles
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            string vrtPath = VrtPathOf(elevationsDir, baseName);

            lock (_vrtLock)
            {
                // If we already have a cached handle for the same base, reuse it.
                if (_vrt != null && _vrt.BaseName.Equals(baseName, StringComparison.OrdinalIgnoreCase))
                    return _vrt;

                // Dispose any previous cached handle (different base)
                _vrt?.Dispose();
                _vrt = null;

                // Case 1: VRT exists on disk -> reuse it (do NOT delete or rebuild)
                if (File.Exists(vrtPath))
                {
                    // We still ensure that there are tiles on disk (already checked by caller);
                    // Open and cache the existing VRT.
                    var ds = Gdal.Open(vrtPath, Access.GA_ReadOnly)
                        ?? throw new InvalidOperationException($"Could not open existing VRT: {vrtPath}");

                    _vrt = new ElevationGdalVrtHandle(baseName, vrtPath, geoTiffFiles, ds);
                    return _vrt;
                }

                // Case 2: No VRT -> create one from the tiles, then open and cache it
                using (var opts = new GDALBuildVRTOptions(
                ["-resolution", "highest"
                // add -srcnodata/-vrtnodata here if you have a known NoData
                ]))
                {
                    // This call creates the VRT file at vrtPath (no deleting of any pre-existing file here)
                    var vrtDs = Gdal.wrapper_GDALBuildVRT_names(vrtPath, geoTiffFiles.ToArray(), opts, null, null);
                    if (vrtDs == null)
                        throw new InvalidOperationException("GDAL failed to build VRT.");
                    vrtDs.Dispose(); // writes the .vrt
                }

                var opened = Gdal.Open(vrtPath, Access.GA_ReadOnly)
                    ?? throw new InvalidOperationException($"Failed to open newly built VRT: {vrtPath}");

                _vrt = new ElevationGdalVrtHandle(baseName, vrtPath, geoTiffFiles, opened);
                return _vrt;
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

            if (elevationSettings == null)
                elevationSettings = new ElevationSettings();
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
