using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using IntersectUtilities.UtilsCommon;
using CadColor = Autodesk.AutoCAD.Colors.Color;

namespace IntersectUtilities.MPE.TerrainKoteCompare
{
    // Which number the marker text and the baked labels carry. Classification, colors and the Excel
    // export are unaffected — this only switches what is written next to each point.
    internal enum TerrainKoteCompareValueMode
    {
        Difference,
        TerrainElevation
    }

    internal enum TerrainKoteCompareClassification
    {
        Above,
        Below,
        OutsideSurface,
        NoHeight
    }

    // Single source of truth for the WPF legend, the transient markers and the baked label layers,
    // mirroring LERCompareTerrainColors. Same colorblind-safe family as its sibling so the two
    // tools read consistently on AutoCAD's black background.
    internal static class TerrainKoteCompareColors
    {
        private static (byte R, byte G, byte B) GetRgb(TerrainKoteCompareClassification classification)
        {
            return classification switch
            {
                TerrainKoteCompareClassification.Above => (156, 70, 255),
                TerrainKoteCompareClassification.Below => (204, 51, 17),
                TerrainKoteCompareClassification.NoHeight => (0, 119, 187),
                _ => (238, 221, 0)
            };
        }

        public static CadColor GetCadColor(TerrainKoteCompareClassification classification)
        {
            (byte r, byte g, byte b) = GetRgb(classification);
            return CadColor.FromColor(System.Drawing.Color.FromArgb(r, g, b));
        }

        public static System.Windows.Media.Color GetMediaColor(TerrainKoteCompareClassification classification)
        {
            (byte r, byte g, byte b) = GetRgb(classification);
            return System.Windows.Media.Color.FromRgb(r, g, b);
        }
    }

    internal static class TerrainKoteCompareLayerNames
    {
        public const string AboveLayerName = "0 - Terrænkote over";
        public const string BelowLayerName = "0 - Terrænkote under";
        public const string OutsideLayerName = "0 - Terrænkote uden for flade";
        public const string NoHeightLayerName = "0 - Terrænkote uden kote";

        public static string GetLayerName(TerrainKoteCompareClassification classification)
        {
            return classification switch
            {
                TerrainKoteCompareClassification.Above => AboveLayerName,
                TerrainKoteCompareClassification.Below => BelowLayerName,
                TerrainKoteCompareClassification.NoHeight => NoHeightLayerName,
                _ => OutsideLayerName
            };
        }

        public static IEnumerable<string> AllLayerNames()
        {
            yield return AboveLayerName;
            yield return BelowLayerName;
            yield return OutsideLayerName;
            yield return NoHeightLayerName;
        }
    }

    // Shared by the transient preview and the baked labels so "Show" is a faithful preview of what
    // "Create Labels" will produce — the preview only adds the marker circles.
    internal static class TerrainKoteCompareTextLayout
    {
        private const double MarkerGapFactor = 1.4;

        // Up-right of the point. Used as the MText BottomLeft attachment point, so the two-line
        // label (number over value) grows up and to the right, clear of the marker.
        public static Point3d LabelPosition(Point3d position, double markerSize)
        {
            double gap = markerSize * MarkerGapFactor;
            return new Point3d(position.X + gap, position.Y + gap, position.Z);
        }
    }

    // One row for the terrain-file list: the file, how many TIN surfaces it contributed, and the
    // display filename.
    internal sealed class TerrainKoteCompareFileInfo
    {
        public TerrainKoteCompareFileInfo(string filePath, int surfaceCount)
        {
            FilePath = filePath;
            SurfaceCount = surfaceCount;
        }

        public string FilePath { get; }
        public int SurfaceCount { get; }
        public string FileName => Path.GetFileName(FilePath);
    }

    internal sealed class TerrainKoteCompareSurfaceRef
    {
        public TerrainKoteCompareSurfaceRef(Database database, ObjectId surfaceId, string surfaceName, string filePath)
        {
            Database = database;
            SurfaceId = surfaceId;
            SurfaceName = surfaceName;
            FilePath = filePath;
        }

        public Database Database { get; }
        public ObjectId SurfaceId { get; }
        public string SurfaceName { get; }
        public string FilePath { get; }
    }

    internal sealed class TerrainKoteCompareOpenSurface
    {
        public TerrainKoteCompareOpenSurface(TinSurface surface, string surfaceName, string filePath)
        {
            Surface = surface;
            SurfaceName = surfaceName;
            FilePath = filePath;
        }

        public TinSurface Surface { get; }
        public string SurfaceName { get; }
        public string FilePath { get; }
    }

    // TinSurface.FindElevationAtXY only works while a transaction on that surface's OWN database is
    // open. With several terrain files loaded we therefore open one transaction per file up front,
    // run the whole analysis, and commit them all at the end — never one transaction per point.
    internal sealed class TerrainKoteCompareSurfaceScope : IDisposable
    {
        private readonly List<Transaction> _transactions = new List<Transaction>();
        private bool _disposed;

        public TerrainKoteCompareSurfaceScope(IEnumerable<TerrainKoteCompareSurfaceRef> surfaceRefs)
        {
            List<TerrainKoteCompareOpenSurface> opened = new List<TerrainKoteCompareOpenSurface>();
            Dictionary<Database, Transaction> transactionByDatabase = new Dictionary<Database, Transaction>();

            foreach (TerrainKoteCompareSurfaceRef surfaceRef in surfaceRefs)
            {
                if (!transactionByDatabase.TryGetValue(surfaceRef.Database, out Transaction? tx))
                {
                    tx = surfaceRef.Database.TransactionManager.StartTransaction();
                    transactionByDatabase[surfaceRef.Database] = tx;
                    _transactions.Add(tx);
                }

                if (tx.GetObject(surfaceRef.SurfaceId, OpenMode.ForRead) is TinSurface surface)
                {
                    opened.Add(new TerrainKoteCompareOpenSurface(surface, surfaceRef.SurfaceName, surfaceRef.FilePath));
                }
            }

            Surfaces = opened;
        }

        public IReadOnlyList<TerrainKoteCompareOpenSurface> Surfaces { get; }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (Transaction tx in _transactions)
            {
                try
                {
                    tx.Commit();
                }
                catch
                {
                    // Intentionally ignored: read-only transactions on side databases.
                }

                tx.Dispose();
            }

            _transactions.Clear();
        }
    }

    // Holds one retained side Database per loaded terrain DWG. Unlike LERCompareTerrain, which keeps
    // a single database and lets the user pick one TIN from a combobox, every TinSurface found in
    // every loaded file is active here.
    internal sealed class TerrainKoteCompareSurfaceSet : IDisposable
    {
        private readonly Dictionary<string, Database> _databaseByFile =
            new Dictionary<string, Database>(StringComparer.OrdinalIgnoreCase);
        private readonly List<TerrainKoteCompareSurfaceRef> _surfaces = new List<TerrainKoteCompareSurfaceRef>();

        public IReadOnlyList<TerrainKoteCompareSurfaceRef> Surfaces => _surfaces;

        public IReadOnlyList<string> Files => _databaseByFile.Keys.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();

        public IReadOnlyList<TerrainKoteCompareFileInfo> FileInfos()
        {
            return _databaseByFile.Keys
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Select(f => new TerrainKoteCompareFileInfo(
                    f,
                    _surfaces.Count(s => string.Equals(s.FilePath, f, StringComparison.OrdinalIgnoreCase))))
                .ToList();
        }

        public bool Contains(string filePath) => _databaseByFile.ContainsKey(filePath);

        // Returns the number of TIN surfaces found in the file. Throws on read failure so the
        // caller can report it; the partially-created database is disposed before rethrowing.
        public int AddFile(string filePath)
        {
            if (_databaseByFile.ContainsKey(filePath))
            {
                return _surfaces.Count(s => string.Equals(s.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
            }

            Database database = new Database(false, true);
            try
            {
                database.ReadDwgFile(filePath, FileOpenMode.OpenForReadAndAllShare, false, null);

                List<TerrainKoteCompareSurfaceRef> found;
                using (Transaction tx = database.TransactionManager.StartTransaction())
                {
                    found = database
                        .HashSetOfType<TinSurface>(tx)
                        .Select(surface => new TerrainKoteCompareSurfaceRef(database, surface.ObjectId, surface.Name, filePath))
                        .OrderBy(item => item.SurfaceName, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    tx.Commit();
                }

                if (found.Count == 0)
                {
                    database.Dispose();
                    return 0;
                }

                _databaseByFile[filePath] = database;
                _surfaces.AddRange(found);
                return found.Count;
            }
            catch
            {
                database.Dispose();
                throw;
            }
        }

        public void RemoveFile(string filePath)
        {
            if (!_databaseByFile.TryGetValue(filePath, out Database? database))
            {
                return;
            }

            _surfaces.RemoveAll(s => string.Equals(s.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
            _databaseByFile.Remove(filePath);
            database.Dispose();
        }

        public void Clear()
        {
            foreach (Database database in _databaseByFile.Values)
            {
                try
                {
                    database.Dispose();
                }
                catch
                {
                    // Intentionally ignored during teardown.
                }
            }

            _databaseByFile.Clear();
            _surfaces.Clear();
        }

        public TerrainKoteCompareSurfaceScope OpenSurfaces() => new TerrainKoteCompareSurfaceScope(_surfaces);

        public void Dispose() => Clear();
    }

    internal sealed class TerrainKoteComparePoint
    {
        public TerrainKoteComparePoint(ObjectId objectId, string handle, Point3d position, double? surveyElevation)
        {
            ObjectId = objectId;
            Handle = handle;
            Position = position;
            SurveyElevation = surveyElevation;
        }

        public ObjectId ObjectId { get; }
        public string Handle { get; }
        public Point3d Position { get; }
        public double? SurveyElevation { get; }
    }

    internal sealed class TerrainKoteCompareResultPoint
    {
        public TerrainKoteCompareResultPoint(
            TerrainKoteComparePoint source,
            double? terrainElevation,
            TerrainKoteCompareClassification classification,
            string surfaceName,
            string sourceFile,
            int coveringSurfaceCount)
        {
            Source = source;
            TerrainElevation = terrainElevation;
            Classification = classification;
            SurfaceName = surfaceName;
            SourceFile = sourceFile;
            CoveringSurfaceCount = coveringSurfaceCount;
        }

        public TerrainKoteComparePoint Source { get; }
        public double? TerrainElevation { get; }
        public TerrainKoteCompareClassification Classification { get; }
        public string SurfaceName { get; }
        public string SourceFile { get; }
        public int CoveringSurfaceCount { get; }

        public int Number { get; set; }

        public Point3d Position => Source.Position;

        // Positive when the surveyed point sits ABOVE the terrain model.
        public double? Difference =>
            Source.SurveyElevation.HasValue && TerrainElevation.HasValue
                ? Source.SurveyElevation.Value - TerrainElevation.Value
                : null;

        public string FormatDifference()
        {
            double? difference = Difference;
            return difference.HasValue
                ? difference.Value.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture)
                : "-";
        }

        // The projected terrain elevation, i.e. the kote read off the TIN under this point.
        public string FormatTerrainElevation()
        {
            return TerrainElevation.HasValue
                ? TerrainElevation.Value.ToString("0.00", CultureInfo.InvariantCulture)
                : "-";
        }

        // Single formatting entry point so the transient preview and the baked labels can never
        // show different text for the same mode.
        public string FormatValue(TerrainKoteCompareValueMode mode)
        {
            return mode == TerrainKoteCompareValueMode.TerrainElevation
                ? FormatTerrainElevation()
                : FormatDifference();
        }

        // The full label as a single MText string: the point number, an MText paragraph break
        // (\P), then the value. Used by both the preview and the baked label so they match.
        public string FormatLabelContents(TerrainKoteCompareValueMode mode)
        {
            return $"{Number.ToString(CultureInfo.InvariantCulture)}\\P{FormatValue(mode)}";
        }
    }

    internal sealed class TerrainKoteCompareResult
    {
        public TerrainKoteCompareResult(IReadOnlyList<TerrainKoteCompareResultPoint> points, int surfaceCount, int fileCount)
        {
            Points = points;
            SurfaceCount = surfaceCount;
            FileCount = fileCount;
        }

        public IReadOnlyList<TerrainKoteCompareResultPoint> Points { get; }
        public int SurfaceCount { get; }
        public int FileCount { get; }

        public int CountOf(TerrainKoteCompareClassification classification) =>
            Points.Count(p => p.Classification == classification);

        public int MultiCoverageCount => Points.Count(p => p.CoveringSurfaceCount > 1);
    }

    internal static class TerrainKoteCompareAnalyzer
    {
        public static TerrainKoteCompareResult Analyze(
            IReadOnlyList<TerrainKoteComparePoint> points,
            TerrainKoteCompareSurfaceSet surfaceSet,
            double rowHeight)
        {
            using TerrainKoteCompareSurfaceScope scope = surfaceSet.OpenSurfaces();
            IReadOnlyList<TerrainKoteCompareOpenSurface> surfaces = scope.Surfaces;

            List<TerrainKoteCompareResultPoint> results = new List<TerrainKoteCompareResultPoint>(points.Count);

            foreach (TerrainKoteComparePoint point in points)
            {
                if (!point.SurveyElevation.HasValue)
                {
                    results.Add(new TerrainKoteCompareResultPoint(
                        point, null, TerrainKoteCompareClassification.NoHeight, string.Empty, string.Empty, 0));
                    continue;
                }

                double surveyElevation = point.SurveyElevation.Value;
                int coveringCount = 0;
                double bestElevation = 0.0;
                double bestDistance = double.MaxValue;
                string bestSurfaceName = string.Empty;
                string bestFile = string.Empty;

                foreach (TerrainKoteCompareOpenSurface openSurface in surfaces)
                {
                    if (!TryFindElevation(openSurface.Surface, point.Position, out double elevation))
                    {
                        continue;
                    }

                    coveringCount++;

                    // "Closest wins": the terrain files are adjacent tiles, so overlap is rare. When
                    // it does happen we take the surface whose elevation is nearest the surveyed Z,
                    // and flag the point so the ambiguity shows up in the export instead of hiding.
                    double distance = Math.Abs(surveyElevation - elevation);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestElevation = elevation;
                        bestSurfaceName = openSurface.SurfaceName;
                        bestFile = openSurface.FilePath;
                    }
                }

                if (coveringCount == 0)
                {
                    results.Add(new TerrainKoteCompareResultPoint(
                        point, null, TerrainKoteCompareClassification.OutsideSurface, string.Empty, string.Empty, 0));
                    continue;
                }

                TerrainKoteCompareClassification classification =
                    surveyElevation - bestElevation >= 0.0
                        ? TerrainKoteCompareClassification.Above
                        : TerrainKoteCompareClassification.Below;

                results.Add(new TerrainKoteCompareResultPoint(
                    point, bestElevation, classification, bestSurfaceName, bestFile, coveringCount));
            }

            AssignSnakeNumbers(results, rowHeight);

            return new TerrainKoteCompareResult(
                results,
                surfaces.Count,
                surfaces.Select(s => s.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        private static bool TryFindElevation(TinSurface surface, Point3d point, out double elevation)
        {
            try
            {
                elevation = surface.FindElevationAtXY(point.X, point.Y);
                return true;
            }
            catch (Autodesk.Civil.PointNotOnEntityException)
            {
                elevation = 0.0;
                return false;
            }
            catch (System.ArgumentException)
            {
                elevation = 0.0;
                return false;
            }
        }

        // Numbers run in reading order across the site: rows banded by Y (top down), and within each
        // band X alternates direction so consecutive numbers stay physically adjacent. Deterministic
        // for a given point set and row height, so re-running produces the same numbers.
        private static void AssignSnakeNumbers(List<TerrainKoteCompareResultPoint> points, double rowHeight)
        {
            if (points.Count == 0)
            {
                return;
            }

            double effectiveRowHeight = rowHeight > 0.0 ? rowHeight : 5.0;

            List<TerrainKoteCompareResultPoint> byY = points
                .OrderByDescending(p => p.Position.Y)
                .ThenBy(p => p.Position.X)
                .ToList();

            List<List<TerrainKoteCompareResultPoint>> bands = new List<List<TerrainKoteCompareResultPoint>>();
            List<TerrainKoteCompareResultPoint> currentBand = new List<TerrainKoteCompareResultPoint> { byY[0] };
            double bandTopY = byY[0].Position.Y;

            for (int i = 1; i < byY.Count; i++)
            {
                TerrainKoteCompareResultPoint point = byY[i];
                if (bandTopY - point.Position.Y > effectiveRowHeight)
                {
                    bands.Add(currentBand);
                    currentBand = new List<TerrainKoteCompareResultPoint>();
                    bandTopY = point.Position.Y;
                }

                currentBand.Add(point);
            }

            bands.Add(currentBand);

            int number = 1;
            for (int bandIndex = 0; bandIndex < bands.Count; bandIndex++)
            {
                IEnumerable<TerrainKoteCompareResultPoint> ordered = bandIndex % 2 == 0
                    ? bands[bandIndex].OrderBy(p => p.Position.X)
                    : bands[bandIndex].OrderByDescending(p => p.Position.X);

                foreach (TerrainKoteCompareResultPoint point in ordered)
                {
                    point.Number = number++;
                }
            }
        }
    }
}
