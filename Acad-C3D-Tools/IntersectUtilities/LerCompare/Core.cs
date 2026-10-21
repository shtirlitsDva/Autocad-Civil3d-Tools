using System.Globalization;
using System.IO;
using System.Threading;
using System.Text;
using System.Text.Json;

namespace IntersectUtilities.LerCompare;

[Flags]
public enum Change
{
    None = 0, New = 1, Missing = 2, Status = 4, Material = 8, Properties = 16,
    Geometry = 32, Split = 64, Merged = 128, Review = 256,
    Coverage = 512, Identifier = 1024, Delivery = 2048, Layer = 4096, TextFormat = 8192
}

public readonly record struct XY(double X, double Y)
{
    public double Distance(XY p) => Math.Sqrt((X - p.X) * (X - p.X) + (Y - p.Y) * (Y - p.Y));
    public static XY operator +(XY a, XY b) => new(a.X + b.X, a.Y + b.Y);
    public static XY operator -(XY a, XY b) => new(a.X - b.X, a.Y - b.Y);
    public static XY operator *(XY a, double b) => new(a.X * b, a.Y * b);
}
public sealed class Pipe
{
    public string Handle { get; set; } = "";
    public string Layer { get; set; } = "";
    public string EntityType { get; set; } = "Polyline";
    public List<XY> Points { get; set; } = new();
    public List<double> Bulges { get; set; } = new();
    public bool Closed
    {
        get; set;
    }
    public Dictionary<string, string> Properties { get; set; } = new(StringComparer.Ordinal);
    public List<string> Errors { get; set; } = new();
    [System.Text.Json.Serialization.JsonIgnore] public List<XY> Samples { get; set; } = new();
    public string Field(string name) => Properties.Where(p => Leaf(p.Key).Equals(name, StringComparison.OrdinalIgnoreCase)).Select(p => p.Value).DefaultIfEmpty("").First();
    public static string Leaf(string key) => key[(key.LastIndexOf('/') + 1)..];
    public string Owner => Field("LedningsEjersNavn").Trim();
    public string Utility
    {
        get
        {
            var l = Layer.ToUpperInvariant();
            if (l.StartsWith("VAND"))
                return "Water";
            if (l.StartsWith("AFL"))
                return "Drainage";
            if (l.StartsWith("EL-"))
                return "Electricity";
            if (l.StartsWith("TELE") || l.StartsWith("LEDNINGSTRACE-TELE"))
                return "Telecom";
            if (l.StartsWith("GAS"))
                return "Gas";
            if (l.StartsWith("FJV"))
                return "District heating";
            if (l.StartsWith("FORING") || l.StartsWith("FØRING"))
                return "Duct/" + l.Split('-').Last();
            return Field("Forsyningsart").Length > 0 ? Field("Forsyningsart") : l.Replace("_UAD", "");
        }
    }
    public List<XY> Flatten(double tol)
    {
        if (Samples.Count == 0)
            Samples = Geometry.Flatten(Points, Bulges, Closed, tol / 4);
        return Samples;
    }
    public double Length(double tol) => Geometry.Length(Flatten(tol));
}
public sealed class CoveragePolygon
{
    public List<List<XY>> Rings { get; set; } = new();
    public bool Contains(XY p, double tol) => Rings.Any(r => Geometry.OnBoundary(p, r, tol)) || Rings.Count(r => Geometry.InRing(p, r)) % 2 == 1;
}
public sealed class Snapshot
{
    public string Path { get; set; } = "";
    public string Units { get; set; } = "";
    public double MetersPerUnit
    {
        get; set;
    }
    public List<Pipe> Pipes { get; set; } = new();
    public List<CoveragePolygon> Coverage { get; set; } = new();
    public string CoverageLayer { get; set; } = "GraveforespPolygon";
    public List<string> Warnings { get; set; } = new();
}
public sealed class Options
{
    public double ToleranceMeters { get; set; } = .01;
    public double MatchRadiusMeters { get; set; } = 1;
    public bool UseCoverage { get; set; } = true;
    public string CoverageLayer { get; set; } = "GraveforespPolygon";
    public double ToleranceUnits { get; set; } = .01;
    public double MatchRadiusUnits { get; set; } = 1;
}
public sealed record Difference(string Property, string OldValue, string NewValue, Change Category, bool OldPresent = true, bool NewPresent = true);
public sealed class Result
{
    public int Number
    {
        get; set;
    }
    public Change Flags
    {
        get; set;
    }
    public List<Pipe> Old { get; set; } = new();
    public List<Pipe> New { get; set; } = new();
    public List<Pipe> CandidateOld { get; set; } = new();
    public List<Pipe> CandidateNew { get; set; } = new();
    public string Match { get; set; } = "";
    public string Note { get; set; } = "";
    public List<Difference> Differences { get; set; } = new();
    public List<int> OldChangedVertices { get; set; } = new();
    public List<int> NewChangedVertices { get; set; } = new();
    public List<int> OldChangedEdges { get; set; } = new();
    public List<int> NewChangedEdges { get; set; } = new();
    public string Utility => New.Concat(Old).Select(p => p.Utility).DefaultIfEmpty("").First();
    public string Owner => string.Join(" → ", Old.Concat(New).Select(p => p.Owner).Distinct());
    public string OldHandles => string.Join(",", Old.Select(p => p.Handle).Concat(CandidateOld.Select(p => "?" + p.Handle)));
    public string NewHandles => string.Join(",", New.Select(p => p.Handle).Concat(CandidateNew.Select(p => "?" + p.Handle)));
}
public sealed class Report
{
    public string Version { get; set; } = "0.3.0";
    public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
    public string OldPath { get; set; } = "";
    public string NewPath { get; set; } = "";
    public int OldCount
    {
        get; set;
    }
    public int NewCount
    {
        get; set;
    }
    public int Unchanged
    {
        get; set;
    }
    public int AdministrativeOnly => Results.Count(r => (r.Flags & ~(Change.Identifier | Change.Delivery | Change.TextFormat)) == Change.None);
    public Options Options { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<Result> Results { get; set; } = new();
    public Dictionary<string, int> Counts => Enum.GetValues<Change>().Where(c => c != Change.None).ToDictionary(c => c.ToString(), c => Results.Count(r => r.Flags.HasFlag(c)));
    public void WriteJson(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }), new UTF8Encoding(false));
    public void WriteCsv(string path)
    {
        static string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder("Result,Flags,Utility,Owner,OldHandles,NewHandles,Match,Property,OldValue,NewValue,Note\r\n");
        foreach (var r in Results)
            foreach (var d in r.Differences.Count > 0 ? r.Differences : new List<Difference> { new("", "", "", Change.None, false, false) })
                sb.AppendLine(string.Join(",", new[] { r.Number.ToString(), r.Flags.ToString(), r.Utility, r.Owner, r.OldHandles, r.NewHandles, r.Match, d.Property, d.OldPresent ? d.OldValue : "<absent>", d.NewPresent ? d.NewValue : "<absent>", r.Note }.Select(Q)));
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }
}
public static class Geometry
{
    public static double Dot(XY a, XY b) => a.X * b.X + a.Y * b.Y;
    public static double Cross(XY a, XY b) => a.X * b.Y - a.Y * b.X;
    public static List<XY> Flatten(IReadOnlyList<XY> pts, IReadOnlyList<double> bulges, bool closed, double sagitta)
    {
        var result = new List<XY>();
        if (pts.Count == 0)
            return result;
        result.Add(pts[0]);
        int edges = closed ? pts.Count : pts.Count - 1;
        for (int i = 0; i < edges; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Count];
            var bulge = i < bulges.Count ? bulges[i] : 0;
            if (Math.Abs(bulge) < 1e-12 || a.Distance(b) < 1e-12)
            {
                result.Add(b);
                continue;
            }
            double chord = a.Distance(b), theta = 4 * Math.Atan(bulge), radius = chord * (1 + bulge * bulge) / (4 * Math.Abs(bulge));
            var mid = (a + b) * .5;
            var dir = b - a;
            var center = mid + new XY(-dir.Y, dir.X) * ((1 - bulge * bulge) / (4 * bulge));
            double start = Math.Atan2(a.Y - center.Y, a.X - center.X);
            double step = 2 * Math.Acos(Math.Clamp(1 - Math.Max(sagitta, 1e-8) / radius, -1, 1));
            int n = Math.Clamp((int)Math.Ceiling(Math.Abs(theta) / Math.Max(step, 1e-6)), 1, 100000);
            for (int j = 1; j < n; j++)
            {
                double t = start + theta * j / n;
                result.Add(center + new XY(Math.Cos(t), Math.Sin(t)) * radius);
            }
            result.Add(b);
        }
        return result;
    }
    public static double Length(IReadOnlyList<XY> p)
    {
        double n = 0;
        for (int i = 1; i < p.Count; i++)
            n += p[i - 1].Distance(p[i]);
        return n;
    }
    public static (double Distance, double Along) Project(XY p, IReadOnlyList<XY> line)
    {
        if (line.Count == 0)
            return (double.PositiveInfinity, 0);
        if (line.Count == 1)
            return (p.Distance(line[0]), 0);
        double best = double.PositiveInfinity, along = 0, run = 0;
        for (int i = 1; i < line.Count; i++)
        {
            var a = line[i - 1];
            var d = line[i] - a;
            double len = Dot(d, d), t = len == 0 ? 0 : Math.Clamp(Dot(p - a, d) / len, 0, 1);
            double dist = p.Distance(a + d * t);
            if (dist < best)
            {
                best = dist;
                along = run + Math.Sqrt(len) * t;
            }
            run += Math.Sqrt(len);
        }
        return (best, along);
    }
    public static double Hausdorff(IReadOnlyList<XY> a, IReadOnlyList<XY> b, double stop = double.PositiveInfinity)
    {
        double max = 0;
        foreach (var pair in new[] { (a, b), (b, a) })
        {
            for (int i = 0; i < pair.Item1.Count; i++)
            {
                max = Math.Max(max, Project(pair.Item1[i], pair.Item2).Distance);
                if (max > stop)
                    return max;
                if (i > 0)
                    max = Math.Max(max, Project((pair.Item1[i - 1] + pair.Item1[i]) * .5, pair.Item2).Distance);
                if (max > stop)
                    return max;
            }
        }
        return max;
    }
    // Length on the source route supported by parallel segments in other routes.
    // Clip perpendicular distance and along-segment projections independently:
    // a crossing or nearby endpoint must not count as a shared pipe route.
    public static double SharedLength(IReadOnlyList<XY> source, IEnumerable<IReadOnlyList<XY>> others, double radius)
    {
        var segments = others.SelectMany(line => Enumerable.Range(1, Math.Max(0, line.Count - 1))
            .Select(index => (A: line[index - 1], B: line[index]))).ToArray();
        double total = 0;
        for (int index = 1; index < source.Count; index++)
        {
            var start = source[index - 1];
            var direction = source[index] - start;
            double length = start.Distance(source[index]);
            if (length <= 1e-12)
                continue;
            var unit = direction * (1 / length);
            var intervals = new List<(double Start, double End)>();
            foreach (var segment in segments)
            {
                var otherDirection = segment.B - segment.A;
                double otherLength = segment.A.Distance(segment.B);
                if (otherLength <= 1e-12 || Math.Abs(Dot(unit, otherDirection)) / otherLength < .98)
                    continue;
                var relative = segment.A - start;
                double normalStart = Cross(unit, relative), normalDelta = Cross(unit, otherDirection);
                double first = 0, last = 1;
                if (Math.Abs(normalDelta) <= 1e-12)
                {
                    if (Math.Abs(normalStart) > radius + 1e-9)
                        continue;
                }
                else
                {
                    double a = (-radius - normalStart) / normalDelta, b = (radius - normalStart) / normalDelta;
                    first = Math.Max(first, Math.Min(a, b));
                    last = Math.Min(last, Math.Max(a, b));
                    if (last < first)
                        continue;
                }
                double projectedStart = Dot(unit, relative), projectedDelta = Dot(unit, otherDirection);
                double from = Math.Max(0, Math.Min(projectedStart + projectedDelta * first, projectedStart + projectedDelta * last));
                double to = Math.Min(length, Math.Max(projectedStart + projectedDelta * first, projectedStart + projectedDelta * last));
                if (to > from)
                    intervals.Add((from, to));
            }
            intervals.Sort((a, b) => a.Start.CompareTo(b.Start));
            double cursor = 0;
            foreach (var interval in intervals)
            {
                total += Math.Max(0, interval.End - Math.Max(cursor, interval.Start));
                cursor = Math.Max(cursor, interval.End);
            }
        }
        return total;
    }
    public static bool InRing(XY p, IReadOnlyList<XY> ring)
    {
        bool inside = false;
        int j = ring.Count - 1;
        for (int i = 0; i < ring.Count; j = i++)
        {
            var a = ring[i];
            var b = ring[j];
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }
    public static bool OnBoundary(XY p, IReadOnlyList<XY> ring, double tol)
    {
        if (ring.Count == 0)
            return false;
        var r = ring.ToList();
        r.Add(r[0]);
        return Project(p, r).Distance <= tol;
    }
    public static bool HasOutside(Pipe pipe, IReadOnlyList<CoveragePolygon> polygons, double tol)
    {
        bool Covered(XY p) => polygons.Any(poly => poly.Contains(p, tol));
        var line = pipe.Flatten(tol);
        if (line.Count == 1)
            return !Covered(line[0]);
        for (int i = 1; i < line.Count; i++)
        {
            var a = line[i - 1];
            var d = line[i] - a;
            var cuts = new List<double> { 0, 1 };
            foreach (var poly in polygons)
                foreach (var ring in poly.Rings)
                    for (int k = 0; k < ring.Count; k++)
                    {
                        var b = ring[k];
                        var e = ring[(k + 1) % ring.Count] - b;
                        double denom = Cross(d, e);
                        if (Math.Abs(denom) < 1e-12)
                            continue;
                        double t = Cross(b - a, e) / denom, u = Cross(b - a, d) / denom;
                        if (t > 0 && t < 1 && u >= 0 && u <= 1)
                            cuts.Add(t);
                    }
            cuts.Sort();
            if (!Covered(a) || !Covered(line[i]))
                return true;
            for (int j = 1; j < cuts.Count; j++)
                if (!Covered(a + d * ((cuts[j] + cuts[j - 1]) * .5)))
                    return true;
        }
        return false;
    }
    public static bool HasExclusive(Pipe pipe, IReadOnlyList<CoveragePolygon> own, IReadOnlyList<CoveragePolygon> other, double tol)
    {
        bool Exclusive(XY p) => own.Any(poly => poly.Contains(p, tol)) && !other.Any(poly => poly.Contains(p, tol));
        var line = pipe.Flatten(tol);
        if (line.Any(Exclusive))
            return true;
        for (int i = 1; i < line.Count; i++)
        {
            var a = line[i - 1];
            var d = line[i] - a;
            var cuts = new List<double> { 0, 1 };
            foreach (var poly in own.Concat(other))
                foreach (var ring in poly.Rings)
                    for (int k = 0; k < ring.Count; k++)
                    {
                        var b = ring[k];
                        var e = ring[(k + 1) % ring.Count] - b;
                        double denom = Cross(d, e);
                        if (Math.Abs(denom) < 1e-12)
                            continue;
                        double t = Cross(b - a, e) / denom, u = Cross(b - a, d) / denom;
                        if (t > 0 && t < 1 && u >= 0 && u <= 1)
                            cuts.Add(t);
                    }
            cuts.Sort();
            for (int j = 1; j < cuts.Count; j++)
                if (Exclusive(a + d * ((cuts[j] + cuts[j - 1]) * .5)))
                    return true;
        }
        return false;
    }
    public static string ExactKey(Pipe p)
    {
        static string Coord(double n) => Math.Round(n, 6).ToString("F6", CultureInfo.InvariantCulture);
        string Forward(int start, bool reverse)
        {
            var parts = new List<string>();
            int n = p.Points.Count;
            for (int i = 0; i < n; i++)
            {
                int k = (start + (reverse ? -i : i) + n * 2) % n;
                var pt = p.Points[k];
                int bi = reverse ? (k - 1 + n) % n : k;
                double bulge = (!p.Closed && i == n - 1) ? 0 : (bi < p.Bulges.Count ? p.Bulges[bi] : 0) * (reverse ? -1 : 1);
                parts.Add(Coord(pt.X) + ":" + Coord(pt.Y) + ":" + Coord(bulge));
            }
            return string.Join(";", parts);
        }
        if (p.Points.Count == 0)
            return "empty";
        var keys = p.Closed ? Enumerable.Range(0, p.Points.Count).SelectMany(k => new[] { Forward(k, false), Forward(k, true) }) : new[] { Forward(0, false), Forward(p.Points.Count - 1, true) };
        return p.Closed + "|" + keys.Min(StringComparer.Ordinal);
    }
}
internal sealed class SpatialIndex
{
    private readonly Dictionary<(int, int), List<Pipe>> cells = new();
    private readonly List<Pipe> huge = new(); private readonly double cell; private readonly double tol;
    private (int, int, int, int) Box(Pipe p, double radius = 0)
    {
        var pts = p.Bulges.Any(b => Math.Abs(b) > 1e-12) ? p.Flatten(tol) : p.Points;
        return ((int)Math.Floor((pts.Min(v => v.X) - radius) / cell), (int)Math.Floor((pts.Min(v => v.Y) - radius) / cell), (int)Math.Floor((pts.Max(v => v.X) + radius) / cell), (int)Math.Floor((pts.Max(v => v.Y) + radius) / cell));
    }
    public SpatialIndex(IEnumerable<Pipe> pipes, double tolerance, double cellSize)
    {
        tol = tolerance;
        cell = cellSize;
        foreach (var p in pipes.Where(p => p.Points.Count > 0))
        {
            var (a, b, c, d) = Box(p);
            if ((long)(c - a + 1) * (d - b + 1) > 4096)
            {
                huge.Add(p);
                continue;
            }
            for (int x = a; x <= c; x++)
                for (int y = b; y <= d; y++)
                {
                    if (!cells.TryGetValue((x, y), out var list))
                        cells[(x, y)] = list = new();
                    list.Add(p);
                }
        }
    }
    public IEnumerable<Pipe> Query(Pipe p, double radius)
    {
        if (p.Points.Count == 0)
            return Array.Empty<Pipe>();
        var (a, b, c, d) = Box(p, radius);
        var found = new HashSet<Pipe>(huge);
        if ((long)(c - a + 1) * (d - b + 1) > 4096)
            return cells.Values.SelectMany(l => l).Concat(huge).Distinct();
        for (int x = a; x <= c; x++)
            for (int y = b; y <= d; y++)
                if (cells.TryGetValue((x, y), out var list))
                    found.UnionWith(list);
        return found;
    }
}
public static class Engine
{
    private static readonly HashSet<string> DeliveryFields = new(StringComparer.OrdinalIgnoreCase) { "LerNummer", "GmlBemærkning" };
    private static readonly HashSet<string> IdFields = new(StringComparer.OrdinalIgnoreCase) { "GmlId", "LerId" };
    public static Change Category(string key)
    {
        string field = Pipe.Leaf(key);
        if (DeliveryFields.Contains(field))
            return Change.Delivery;
        if (IdFields.Contains(field))
            return Change.Identifier;
        if (field.Equals("Driftsstatus", StringComparison.OrdinalIgnoreCase))
            return Change.Status;
        if (field.Contains("Material", StringComparison.OrdinalIgnoreCase) || field.Equals("KabelType", StringComparison.OrdinalIgnoreCase))
            return Change.Material;
        return Change.Properties;
    }
    private static string Normal(string key, string value)
    {
        if (value.Equals("Falsk", StringComparison.OrdinalIgnoreCase) || value.Equals("False", StringComparison.OrdinalIgnoreCase))
            return "false";
        if (value.Equals("Sand", StringComparison.OrdinalIgnoreCase) || value.Equals("True", StringComparison.OrdinalIgnoreCase))
            return "true";
        if (Pipe.Leaf(key) == "RegistreringFra")
        {
            foreach (var fmt in new[] { "dd-MM-yyyy HH:mm:ss", "dd/MM/yyyy HH.mm.ss", "dd/MM/yyyy HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss" })
                if (DateTime.TryParseExact(value, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    return dt.ToString("O");
        }
        return value;
    }
    public static List<Difference> Diff(Pipe a, Pipe b)
    {
        var diffs = new List<Difference>();
        foreach (var key in a.Properties.Keys.Union(b.Properties.Keys).Order(StringComparer.Ordinal))
        {
            bool av = a.Properties.ContainsKey(key), bv = b.Properties.ContainsKey(key);
            var x = av ? a.Properties[key] : "";
            var y = bv ? b.Properties[key] : "";
            if (av != bv || Normal(key, x) != Normal(key, y))
            {
                var category = Category(key);
                if (av && bv && category != Change.Identifier && category != Change.Delivery && string.Equals(x, y, StringComparison.OrdinalIgnoreCase))
                    category = Change.TextFormat;
                diffs.Add(new(key, x, y, category, av, bv));
            }
        }
        if (a.Layer != b.Layer)
            diffs.Add(new("Drawing/Layer", a.Layer, b.Layer, Change.Layer));
        if (a.EntityType != b.EntityType)
            diffs.Add(new("Drawing/EntityType", a.EntityType, b.EntityType, Change.Properties));
        return diffs;
    }
    private static void VertexDiff(Result r, Pipe a, Pipe b, double tol)
    {
        int n = a.Points.Count, m = b.Points.Count;
        if (n != m || a.Closed != b.Closed)
        {
            r.Flags |= Change.Geometry;
            var matched = new Dictionary<int, int>();
            var used = new HashSet<int>();
            for (int i = 0; i < n; i++)
            {
                var choices = Enumerable.Range(0, m).Where(j => !used.Contains(j) && a.Points[i].Distance(b.Points[j]) <= tol + 1e-9).OrderBy(j => a.Points[i].Distance(b.Points[j])).ToList();
                if (choices.Count > 0)
                {
                    matched[i] = choices[0];
                    used.Add(choices[0]);
                }
                else
                    r.OldChangedVertices.Add(i);
            }
            r.NewChangedVertices = Enumerable.Range(0, m).Where(j => !used.Contains(j)).ToList();
            int oldEdges = a.Closed ? n : Math.Max(0, n - 1), newEdges = b.Closed ? m : Math.Max(0, m - 1);
            var preservedNew = new HashSet<int>();
            for (int i = 0; i < oldEdges; i++)
            {
                int next = (i + 1) % n;
                bool same = false;
                if (matched.TryGetValue(i, out var j) && matched.TryGetValue(next, out var k))
                {
                    int edge = -1;
                    bool rev = false;
                    if (k == (j + 1) % Math.Max(m, 1) && j < newEdges)
                        edge = j;
                    else if (j == (k + 1) % Math.Max(m, 1) && k < newEdges)
                    {
                        edge = k;
                        rev = true;
                    }
                    if (edge >= 0)
                    {
                        double ab = i < a.Bulges.Count ? a.Bulges[i] : 0, bb = (edge < b.Bulges.Count ? b.Bulges[edge] : 0) * (rev ? -1 : 1);
                        var sa = Geometry.Flatten(new[] { a.Points[i], a.Points[next] }, new[] { ab, 0d }, false, tol / 4);
                        var sb = Geometry.Flatten(new[] { b.Points[j], b.Points[k] }, new[] { bb, 0d }, false, tol / 4);
                        same = Geometry.Hausdorff(sa, sb, tol) <= tol + 1e-9;
                        if (same)
                            preservedNew.Add(edge);
                    }
                }
                if (!same)
                    r.OldChangedEdges.Add(i);
            }
            r.NewChangedEdges = Enumerable.Range(0, newEdges).Where(j => !preservedNew.Contains(j)).ToList();
            r.Differences.Add(new("Geometry/Vertices", n.ToString(), m.ToString(), Change.Geometry));
            if (a.Closed != b.Closed)
                r.Differences.Add(new("Geometry/Closed", a.Closed.ToString(), b.Closed.ToString(), Change.Geometry));
            return;
        }
        if (n == 0)
            return;
        double best = double.PositiveInfinity;
        int shift = 0;
        bool reverse = false;
        foreach (bool rev in new[] { false, true })
            foreach (int s in a.Closed ? Enumerable.Range(0, n) : new[] { rev ? n - 1 : 0 })
            {
                double score = 0;
                for (int i = 0; i < n; i++)
                    score += a.Points[i].Distance(b.Points[(s + (rev ? -i : i) + n * 2) % n]);
                if (score < best)
                {
                    best = score;
                    shift = s;
                    reverse = rev;
                }
            }
        int Map(int i) => (shift + (reverse ? -i : i) + n * 2) % n;
        for (int i = 0; i < n; i++)
            if (a.Points[i].Distance(b.Points[Map(i)]) > tol + 1e-9)
            {
                r.OldChangedVertices.Add(i);
                r.NewChangedVertices.Add(Map(i));
            }
        int edges = a.Closed ? n : n - 1;
        for (int i = 0; i < edges; i++)
        {
            int next = (i + 1) % n, j = reverse ? Map(next) : Map(i);
            double ab = i < a.Bulges.Count ? a.Bulges[i] : 0, bb = (j < b.Bulges.Count ? b.Bulges[j] : 0) * (reverse ? -1 : 1);
            bool moved = r.OldChangedVertices.Contains(i) || r.OldChangedVertices.Contains(next);
            if (!moved && Math.Abs(ab - bb) > 1e-12)
            {
                var sa = Geometry.Flatten(new[] { a.Points[i], a.Points[next] }, new[] { ab, 0d }, false, tol / 4);
                var sb = Geometry.Flatten(new[] { b.Points[Map(i)], b.Points[Map(next)] }, new[] { bb, 0d }, false, tol / 4);
                moved = Geometry.Hausdorff(sa, sb, tol) > tol;
            }
            if (moved)
            {
                r.OldChangedEdges.Add(i);
                r.NewChangedEdges.Add(j);
            }
        }
        if (r.OldChangedVertices.Count > 0 || r.OldChangedEdges.Count > 0)
        {
            r.Flags |= Change.Geometry;
            r.Differences.Add(new("Geometry/Changed vertices", "", r.OldChangedVertices.Count.ToString(), Change.Geometry, false, true));
            r.Differences.Add(new("Geometry/Changed edges", "", r.OldChangedEdges.Count.ToString(), Change.Geometry, false, true));
        }
    }
    public static Report Compare(Snapshot old, Snapshot current, Options opt, CancellationToken cancel = default) => Compare(old, current, opt, _ => { }, cancel);
    public static Report Compare(Snapshot old, Snapshot current, Options opt, Action<string> progress, CancellationToken cancel = default)
    {
        if (!(opt.ToleranceUnits > 0) || !(opt.MatchRadiusUnits >= opt.ToleranceUnits))
            throw new ArgumentException("Matching radius must be at least the geometry tolerance.");
        double tol = opt.ToleranceUnits, rad = opt.MatchRadiusUnits;
        foreach (var p in old.Pipes.Concat(current.Pipes))
            p.Samples.Clear();
        var report = new Report { OldPath = old.Path, NewPath = current.Path, OldCount = old.Pipes.Count, NewCount = current.Pipes.Count, Options = opt, Warnings = old.Warnings.Concat(current.Warnings).ToList() };
        if (opt.UseCoverage && (old.Coverage.Count == 0 || current.Coverage.Count == 0))
            report.Warnings.Add("Usable coverage hatches are missing in one or both drawings. Unmatched pipes remain New/Missing.");
        report.Warnings.Add("New/Missing describes these deliveries. Physical installation/removal is not inferred.");
        var left = new HashSet<Pipe>(old.Pipes);
        var right = new HashSet<Pipe>(current.Pipes);
        void Coverage(Result r)
        {
            if (!opt.UseCoverage || old.Coverage.Count == 0 || current.Coverage.Count == 0)
                return;
            bool outside = r.Old.Any(p => Geometry.HasExclusive(p, old.Coverage, current.Coverage, tol)) || r.New.Any(p => Geometry.HasExclusive(p, current.Coverage, old.Coverage, tol));
            if (outside)
            {
                r.Flags |= Change.Coverage;
                r.Note += (r.Note.Length > 0 ? " " : "") + "At least part lies in an area requested only in one delivery; this part cannot be compared reliably.";
            }
        }
        void Add(List<Pipe> a, List<Pipe> b, Change flags, string method, string note = "")
        {
            var r = new Result { Old = a, New = b, Flags = flags, Match = method, Note = note };
            if (a.Count == 1 && b.Count == 1)
            {
                r.Differences = Diff(a[0], b[0]);
                foreach (var d in r.Differences)
                    r.Flags |= d.Category;
                VertexDiff(r, a[0], b[0], tol);
            }
            else if (a.Count > 0 && b.Count > 0)
            {
                // Preserve every field/value across split, merge, or ambiguous groups.
                foreach (var key in a.Concat(b).SelectMany(p => p.Properties.Keys).Distinct().Order(StringComparer.Ordinal))
                {
                    var av = a.Select(p => p.Properties.TryGetValue(key, out var v) ? v : "<absent>").Distinct().Order(StringComparer.Ordinal).ToList();
                    var bv = b.Select(p => p.Properties.TryGetValue(key, out var v) ? v : "<absent>").Distinct().Order(StringComparer.Ordinal).ToList();
                    if (!av.Select(v => Normal(key, v)).SequenceEqual(bv.Select(v => Normal(key, v))))
                    {
                        var category = Category(key);
                        if (category != Change.Identifier && category != Change.Delivery && av.Select(v => v.ToUpperInvariant()).Order(StringComparer.Ordinal).SequenceEqual(bv.Select(v => v.ToUpperInvariant()).Order(StringComparer.Ordinal)))
                            category = Change.TextFormat;
                        var d = new Difference(key, string.Join(" | ", av), string.Join(" | ", bv), category);
                        r.Differences.Add(d);
                        r.Flags |= d.Category;
                    }
                }
            }
            if (a.Concat(b).Any(p => p.Errors.Count > 0))
            {
                r.Flags |= Change.Review;
                r.Note += " " + string.Join("; ", a.Concat(b).SelectMany(p => p.Errors).Distinct());
            }
            Coverage(r);
            foreach (var p in a)
                left.Remove(p);
            foreach (var p in b)
                right.Remove(p);
            if (r.Flags == Change.None)
                report.Unchanged++;
            else
                report.Results.Add(r);
        }
        progress("Matching identical geometry…");
        var exactKeys = old.Pipes.Concat(current.Pipes).ToDictionary(p => p, Geometry.ExactKey);
        var exactNew = current.Pipes.GroupBy(p => exactKeys[p]).ToDictionary(g => g.Key, g => g.ToList());
        var exactOld = old.Pipes.GroupBy(p => exactKeys[p]).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var a in old.Pipes)
        {
            cancel.ThrowIfCancellationRequested();
            if (!left.Contains(a) || !exactNew.TryGetValue(exactKeys[a], out var hits))
                continue;
            var choices = hits.Where(b => right.Contains(b) && a.Utility == b.Utility).ToList();
            var sameId = choices.Where(b => a.Field("GmlId").Length > 0 && a.Field("GmlId") == b.Field("GmlId")).ToList();
            if (sameId.Count == 1 && exactOld[exactKeys[a]].Count(p => left.Contains(p) && p.Utility == a.Utility && p.Field("GmlId") == a.Field("GmlId")) == 1)
                Add(new() { a }, new() { sameId[0] }, Change.None, "Geometry + GmlId");
            else
            {
                var owner = choices.Where(b => b.Owner == a.Owner).ToList();
                if (owner.Count == 1 && exactOld[exactKeys[a]].Count(p => left.Contains(p) && p.Owner == a.Owner && p.Utility == a.Utility) == 1)
                    Add(new() { a }, new() { owner[0] }, Change.None, "Identical geometry + owner");
                else if (choices.Count == 1 && exactOld[exactKeys[a]].Count(p => left.Contains(p) && p.Utility == a.Utility) == 1)
                    Add(new() { a }, new() { choices[0] }, Change.Review, "Identical geometry, owner changed", "Owner differs; confirm the identity.");
            }
        }
        var cellSize = 50 * opt.ToleranceUnits / opt.ToleranceMeters;
        var indexNew = new SpatialIndex(current.Pipes, tol, cellSize);
        var indexOld = new SpatialIndex(old.Pipes, tol, cellSize);
        // Infer splits/merges only when the pieces fully cover the original route,
        // within the requested tolerance, and their lengths do not overlap.
        void SplitMerge(bool merge)
        {
            var wholes = (merge ? right : left).ToArray();
            var pieces = merge ? left : right;
            var index = merge ? indexOld : indexNew;
            foreach (var whole in wholes)
            {
                cancel.ThrowIfCancellationRequested();
                if (!(merge ? right : left).Contains(whole) || whole.Closed)
                    continue;
                var line = whole.Flatten(tol);
                double length = Geometry.Length(line);
                if (length <= tol)
                    continue;
                var intervals = new List<(Pipe Pipe, double A, double B)>();
                foreach (var p in index.Query(whole, tol).Where(p => pieces.Contains(p) && p.Utility == whole.Utility && p.Owner == whole.Owner && !p.Closed))
                {
                    var pts = p.Flatten(tol);
                    if (pts.Count < 2)
                        continue;
                    var first = Geometry.Project(pts[0], line);
                    var last = Geometry.Project(pts[^1], line);
                    double from = Math.Min(first.Along, last.Along), to = Math.Max(first.Along, last.Along);
                    if (to - from <= tol || Math.Abs(p.Length(tol) - (to - from)) > Math.Max(tol * 2, length * .00001))
                        continue;
                    if (pts.Any(pt => Geometry.Project(pt, line).Distance > tol))
                        continue;
                    if (Enumerable.Range(1, pts.Count - 1).Any(i => Geometry.Project((pts[i] + pts[i - 1]) * .5, line).Distance > tol))
                        continue;
                    intervals.Add((p, from, to));
                }
                intervals.Sort((x, y) => x.A.CompareTo(y.A));
                if (intervals.Count < 2 || intervals[0].A > tol || length - intervals[^1].B > tol)
                    continue;
                double cursor = 0;
                bool valid = true;
                foreach (var part in intervals)
                {
                    if (Math.Abs(part.A - cursor) > tol * 2)
                    {
                        valid = false;
                        break;
                    }
                    cursor = part.B;
                }
                if (!valid || length - cursor > tol)
                    continue;
                var list = intervals.Select(i => i.Pipe).ToList();
                Add(merge ? list : new() { whole }, merge ? new() { whole } : list, merge ? Change.Merged : Change.Split, "Complete route coverage", "Route segmentation changed. Attributes are compared as value sets; individual sections remain available in the report.");
            }
        }
        progress("Checking splits and merges…");
        SplitMerge(false);
        SplitMerge(true);
        // Stable IDs are evidence, but exports may regenerate/reuse IDs.
        // Large/conflicting matches are retained for review after spatial matching.
        foreach (var field in new[] { "GmlId", "LerId" })
        {
            string Key(Pipe p) => field == "GmlId" ? p.Field(field) : p.Owner + "|" + p.Utility + "|" + p.Field(field);
            var ag = left.Where(p => p.Field(field).Length > 0).GroupBy(Key).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.First());
            var bg = right.Where(p => p.Field(field).Length > 0).GroupBy(Key).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.First());
            foreach (var k in ag.Keys.Intersect(bg.Keys))
            {
                cancel.ThrowIfCancellationRequested();
                var a = ag[k];
                var b = bg[k];
                if (a.Utility != b.Utility)
                    continue;
                double d = Geometry.Hausdorff(a.Flatten(tol), b.Flatten(tol), rad);
                if (d <= rad)
                    Add(new() { a }, new() { b }, a.Owner != b.Owner ? Change.Review : Change.None, field + " + nearby geometry");
            }
        }
        progress("Matching nearby routes…");
        var candidates = new Dictionary<Pipe, List<(Pipe Pipe, double Score, double Distance)>>();
        foreach (var a in left.ToArray())
        {
            cancel.ThrowIfCancellationRequested();
            var list = new List<(Pipe, double, double)>();
            double al = a.Length(tol);
            foreach (var b in indexNew.Query(a, rad).Where(b => right.Contains(b) && b.Utility == a.Utility))
            {
                double bl = b.Length(tol);
                if (Math.Max(al, bl) > 0 && Math.Min(al, bl) / Math.Max(al, bl) < .7)
                    continue;
                double d = Geometry.Hausdorff(a.Flatten(tol), b.Flatten(tol), rad);
                if (d > rad)
                    continue;
                list.Add((b, d + Math.Abs(al - bl) * .05 + (a.Owner == b.Owner ? 0 : rad), d));
            }
            if (list.Count > 0)
                candidates[a] = list.OrderBy(x => x.Item2).ToList();
        }
        var incoming = candidates.SelectMany(k => k.Value.Select(v => (Old: k.Key, New: v.Pipe, Score: v.Score))).GroupBy(x => x.New).ToDictionary(g => g.Key, g => g.OrderBy(x => x.Score).ToList());
        bool Clear(double first, double second) => second - first > Math.Max(tol * 2, Math.Abs(first) * .25);
        foreach (var (a, hits) in candidates.OrderBy(k => k.Value[0].Score))
        {
            if (!left.Contains(a))
                continue;
            var b = hits[0].Pipe;
            var back = incoming[b];
            if (!right.Contains(b) || back[0].Old != a)
                continue;
            if (hits.Count > 1 && !Clear(hits[0].Score, hits[1].Score))
                continue;
            if (back.Count > 1 && !Clear(back[0].Score, back[1].Score))
                continue;
            double d = hits[0].Distance;
            var review = d > tol || a.Owner != b.Owner;
            Add(new() { a }, new() { b }, review ? Change.Review : Change.None, d <= tol ? "Geometry within tolerance" : "Probable route match", review ? "Geometry-based identity needs review; the matching radius is separate from the change tolerance." : "");
        }
        progress("Checking partially shared routes…");
        // IDs and polyline boundaries can both change between deliveries. A shorter
        // route can lie on a longer old route yet fail the whole-route Hausdorff test.
        // Keep such associations under Review rather than labelling both New/Missing.
        double minimumShared = .5 * opt.ToleranceUnits / opt.ToleranceMeters;
        var partial = new Dictionary<Pipe, List<Pipe>>();
        foreach (var a in left.ToArray())
        {
            cancel.ThrowIfCancellationRequested();
            double oldLength = a.Length(tol);
            foreach (var b in indexNew.Query(a, rad).Where(b => right.Contains(b) && b.Owner == a.Owner && b.Utility == a.Utility))
            {
                double newLength = b.Length(tol), shorter = Math.Min(oldLength, newLength), longer = Math.Max(oldLength, newLength);
                if (shorter <= 1e-9 || shorter < longer * .1)
                    continue;
                double sharedOld = Geometry.SharedLength(a.Flatten(tol), new[] { b.Flatten(tol) }, rad);
                if (sharedOld < shorter * .9)
                    continue;
                double sharedNew = Geometry.SharedLength(b.Flatten(tol), new[] { a.Flatten(tol) }, rad);
                if (sharedNew < shorter * .9)
                    continue;
                if (!partial.TryGetValue(a, out var list))
                    partial[a] = list = new();
                list.Add(b);
            }
        }
        var partialIncoming = partial.SelectMany(pair => pair.Value.Select(b => (Old: pair.Key, New: b)))
            .GroupBy(pair => pair.New).ToDictionary(group => group.Key, group => group.Select(pair => pair.Old).ToList());
        foreach (var first in left.ToArray())
        {
            if (!left.Contains(first) || !partial.ContainsKey(first))
                continue;
            var oldGroup = new HashSet<Pipe>();
            var newGroup = new HashSet<Pipe>();
            var queue = new Queue<Pipe>();
            queue.Enqueue(first);
            while (queue.Count > 0)
            {
                var a = queue.Dequeue();
                if (!left.Contains(a) || !oldGroup.Add(a))
                    continue;
                foreach (var b in partial[a].Where(right.Contains))
                {
                    if (!newGroup.Add(b))
                        continue;
                    foreach (var other in partialIncoming[b].Where(left.Contains))
                        queue.Enqueue(other);
                }
            }
            if (newGroup.Count > 0)
                Add(oldGroup.ToList(), newGroup.ToList(), Change.Review | Change.Geometry, "Partially shared route",
                    "Most of the shorter route follows existing parallel geometry within the matching radius. Possible clipping, extension, resegmentation, or a nearby separate pipe; confirm identity. This is not a confirmed addition/removal.");
        }
        progress("Collecting uncertain and unmatched pipes…");
        // Connected ambiguous candidate groups are shown together, avoiding
        // falsely confident new/missing classifications for nearby parallel routes.
        foreach (var a in left.ToArray())
        {
            if (!left.Contains(a) || !candidates.ContainsKey(a))
                continue;
            var aa = new HashSet<Pipe>();
            var bb = new HashSet<Pipe>();
            var queue = new Queue<Pipe>();
            queue.Enqueue(a);
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                if (!left.Contains(p) || !aa.Add(p))
                    continue;
                if (!candidates.TryGetValue(p, out var hs))
                    continue;
                foreach (var h in hs.Where(h => right.Contains(h.Pipe)))
                {
                    if (!bb.Add(h.Pipe))
                        continue;
                    foreach (var back in incoming[h.Pipe])
                        if (left.Contains(back.Old) && !aa.Contains(back.Old))
                            queue.Enqueue(back.Old);
                }
            }
            if (bb.Count > 0)
                Add(aa.ToList(), bb.ToList(), Change.Review, "Ambiguous nearby routes", "Several possible identities. These are not classified as confirmed New/Missing.");
        }
        // Retain far-away shared IDs as explicit conflicts, never silently as a move.
        var farOld = left.Where(p => p.Field("GmlId").Length > 0).GroupBy(p => p.Field("GmlId")).ToDictionary(g => g.Key, g => g.ToList());
        var farNew = right.Where(p => p.Field("GmlId").Length > 0).GroupBy(p => p.Field("GmlId")).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var key in farOld.Keys.Intersect(farNew.Keys))
            Add(farOld[key], farNew[key], Change.Review, "Shared ID, conflicting geometry", "Possible large move, resegmentation, or reused export ID. Confirm identity before treating as changed/new/missing.");
        // An unmatched route may reuse geometry from an entity already consumed by
        // another match. Retain those candidates separately, preserving one-to-one
        // ownership of source entities and explicitly leaving the identity unresolved.
        void Unmatched(Pipe pipe, bool isNew)
        {
            var index = isNew ? indexOld : indexNew;
            double length = pipe.Length(tol);
            bool WholeRouteCandidate(Pipe candidate)
            {
                // A length-ratio test is useful for accepting an automatic match,
                // but cannot rule out correspondence for short changed segments.
                // This is evidence for Review only, never an accepted identity.
                return candidate.Length(tol) > 0
                    && Geometry.Hausdorff(pipe.Flatten(tol), candidate.Flatten(tol), rad) <= rad;
            }
            var evidence = length <= 1e-9 ? new List<Pipe>() : index.Query(pipe, rad)
                .Where(candidate => candidate.Owner == pipe.Owner && candidate.Utility == pipe.Utility
                    && (Geometry.SharedLength(pipe.Flatten(tol), new[] { candidate.Flatten(tol) }, rad) >= Math.Min(minimumShared, length * .9)
                        || WholeRouteCandidate(candidate))).ToList();
            double shared = Geometry.SharedLength(pipe.Flatten(tol), evidence.Select(candidate => (IReadOnlyList<XY>)candidate.Flatten(tol)), rad);
            bool hasCorrespondence = length > 0 && (shared >= length * .9 || evidence.Any(WholeRouteCandidate));
            Add(isNew ? new() : new() { pipe }, isNew ? new() { pipe } : new(),
                hasCorrespondence ? Change.Review : isNew ? Change.New : Change.Missing,
                hasCorrespondence ? "Shared route, identity unresolved" : isNew ? "Unmatched new polyline" : "Unmatched old polyline",
                hasCorrespondence ? $"Parallel shared length: {shared / length:P0}. Similar whole-route geometry and/or substantial parallel coverage exists in the other delivery within the matching radius. Candidate handles are prefixed with ?. These may be resegmented, moved, or separate nearby pipes; confirm identity." : "No accepted correspondence was found; this does not establish physical installation/removal.");
            if (hasCorrespondence)
            {
                var row = report.Results[^1];
                if (isNew)
                    row.CandidateOld = evidence;
                else
                    row.CandidateNew = evidence;
            }
        }
        foreach (var p in left.ToArray())
            Unmatched(p, false);
        foreach (var p in right.ToArray())
            Unmatched(p, true);
        int number = 0;
        foreach (var r in report.Results.OrderBy(r => r.Utility).ThenBy(r => r.Owner).ThenBy(r => r.OldHandles))
            r.Number = ++number;
        report.Results = report.Results.OrderBy(r => r.Number).ToList();
        return report;
    }
}
