using Autodesk.AutoCAD.Geometry;

using NTRExport.Enums;

using System.Globalization;
using System.IO;
using System.Reflection;

namespace NTRExport.TopologyModel.Data;

    internal static class FModelCatalog
    {
        private static readonly object _lock = new();
        private static Dictionary<(int dn, string variant), VariantData>? _variants;

        internal static VariantData GetVariant(int dn, string variant)
        {
            EnsureLoaded();
            if (_variants!.TryGetValue((dn, variant), out var data))
                return data;
            throw new InvalidOperationException($"FModelCatalog: No entry for DN {dn} variant {variant}.");
        }

        internal static IEnumerable<VariantData> EnumerateVariants(int dn)
        {
            EnsureLoaded();
            return _variants!.Where(kv => kv.Key.dn == dn).Select(kv => kv.Value);
        }

        private static void EnsureLoaded()
        {
            if (_variants != null) return;
            lock (_lock)
            {
                if (_variants != null) return;
                _variants = new();
                using var stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("NTRExport.TopologyModel.Data.fmodel_tables.csv");
                if (stream == null)
                    throw new InvalidOperationException("Unable to locate embedded resource fmodel_tables.csv.");
                using var reader = new StreamReader(stream);
                var header = reader.ReadLine();
                if (header == null)
                    throw new InvalidOperationException("fmodel_tables.csv is empty.");
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var cols = line.Split(';');
                    if (cols.Length < 7)
                        throw new InvalidOperationException($"Malformed CSV line: {line}");
                    int dn = int.Parse(cols[0], CultureInfo.InvariantCulture);
                    string variant = cols[1].Trim();
                    string element = cols[2].Trim().ToLowerInvariant();
                    string type = cols[3].Trim();
                    var flow = ParseFlow(type);
                    var p1 = ParsePoint(cols[4]);
                    var p2 = ParsePoint(cols[5]);
                    Point3d? center = string.IsNullOrWhiteSpace(cols[6]) ? null : ParsePoint(cols[6]);

                    var primitive = new FModelPrimitive(
                        flow,
                        element switch
                        {
                            "pipe" => PrimitiveKind.Pipe,
                            "elbow" => PrimitiveKind.Elbow,
                            _ => throw new InvalidOperationException($"Unknown element '{element}' in FModel catalog.")
                        },
                        p1,
                        p2,
                        center);

                    var key = (dn, variant);
                    if (!_variants.TryGetValue(key, out var data))
                    {
                        data = new VariantData(dn, variant);
                        _variants[key] = data;
                    }
                    data.AddPrimitive(primitive);
                }
                foreach (var data in _variants.Values)
                    data.FinalizePorts();
            }
        }

    private static FlowRole ParseFlow(string value) =>
        value.Equals("FREM", StringComparison.OrdinalIgnoreCase)
            ? FlowRole.Supply
            : FlowRole.Return;

    private static Point3d ParsePoint(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
            throw new InvalidOperationException($"Invalid point '{value}' in FModel catalog.");
        double x = double.Parse(parts[0], CultureInfo.InvariantCulture) / 1000.0;
        double y = double.Parse(parts[1], CultureInfo.InvariantCulture) / 1000.0;
        double z = double.Parse(parts[2], CultureInfo.InvariantCulture) / 1000.0;
        return new Point3d(x, y, z);
    }

    private sealed class PointComparer : IEqualityComparer<Point3d>
    {
        private static readonly Tolerance Tol = new(1e-6, 1e-6);
        public bool Equals(Point3d a, Point3d b) => a.IsEqualTo(b, Tol);
        public int GetHashCode(Point3d obj) => HashCode.Combine(
            Math.Round(obj.X, 6),
            Math.Round(obj.Y, 6),
            Math.Round(obj.Z, 6));
    }

    internal sealed class VariantData
    {
        public int DN { get; }
        public string Variant { get; }
        public IReadOnlyList<FModelPrimitive> Primitives => _primitives;
        public Dictionary<FlowRole, PortInfo> TwinPorts { get; } = new();
        public Dictionary<FlowRole, PortInfo> BondPorts { get; } = new();

        private readonly List<FModelPrimitive> _primitives = new();
        private readonly List<EndpointRecord> _endpointRecords = new();
        private readonly Dictionary<Point3d, EndpointUsage> _usage = new(new PointComparer());

        public VariantData(int dn, string variant)
        {
            DN = dn;
            Variant = variant;
        }

        public void AddPrimitive(FModelPrimitive primitive)
        {
            _primitives.Add(primitive);
            RegisterUsage(primitive.P1, primitive.Kind == PrimitiveKind.Pipe);
            RegisterUsage(primitive.P2, primitive.Kind == PrimitiveKind.Pipe);

            if (primitive.Kind == PrimitiveKind.Pipe)
            {
                _endpointRecords.Add(new EndpointRecord(primitive.Flow, primitive.P1, primitive.P2));
                _endpointRecords.Add(new EndpointRecord(primitive.Flow, primitive.P2, primitive.P1));
            }
        }

        public void FinalizePorts()
        {
            var openEndpoints = _endpointRecords
                .Where(r =>
                {
                    if (!_usage.TryGetValue(r.Point, out var usage)) return true;
                    return usage.ElbowCount == 0 && usage.PipeCount == 1;
                })
                .ToList();

            if (openEndpoints.Count != 4)
                throw new InvalidOperationException(
                    $"FModelCatalog: expected 4 open pipe ends for DN {DN} variant {Variant}, found {openEndpoints.Count}.");

            var supplyEndpoints = openEndpoints.Where(r => r.Flow == FlowRole.Supply).ToList();
            var returnEndpoints = openEndpoints.Where(r => r.Flow == FlowRole.Return).ToList();

            if (supplyEndpoints.Count != 2 || returnEndpoints.Count != 2)
                throw new InvalidOperationException($"FModelCatalog: mismatched supply/return endpoints for DN {DN} variant {Variant}.");

            EndpointRecord? supplyTwin = null;
            EndpointRecord? returnTwin = null;
            EndpointRecord? supplyBond = null;
            EndpointRecord? returnBond = null;

            foreach (var s in supplyEndpoints)
            {
                foreach (var r in returnEndpoints)
                {
                    if (supplyTwin == null && returnTwin == null && SameXY(s.Point, r.Point) && !SameZ(s.Point, r.Point))
                    {
                        supplyTwin = s;
                        returnTwin = r;
                        continue;
                    }

                    if (supplyBond == null && returnBond == null && SameXZ(s.Point, r.Point) && !SameY(s.Point, r.Point))
                    {
                        supplyBond = s;
                        returnBond = r;
                    }
                }
            }

            if (supplyTwin == null || returnTwin == null || supplyBond == null || returnBond == null)
                throw new InvalidOperationException($"FModelCatalog: unable to classify ports for DN {DN} variant {Variant}.");

            var shift = ComputeVariantShift(
                supplyTwin.Value.Point,
                returnTwin.Value.Point,
                supplyBond.Value.Point);

            if (shift.Length > 1e-9)
                ApplyShiftToPrimitives(shift);

            TwinPorts[FlowRole.Supply] = CreatePortInfo(supplyTwin.Value, shift);
            TwinPorts[FlowRole.Return] = CreatePortInfo(returnTwin.Value, shift);
            BondPorts[FlowRole.Supply] = CreatePortInfo(supplyBond.Value, shift);
            BondPorts[FlowRole.Return] = CreatePortInfo(returnBond.Value, shift);
        }

        private void RegisterUsage(Point3d point, bool isPipe)
        {
            if (!_usage.TryGetValue(point, out var usage))
            {
                usage = new EndpointUsage();
                _usage[point] = usage;
            }

            if (isPipe)
                usage.PipeCount++;
            else
                usage.ElbowCount++;
        }

        private static bool SameXY(Point3d a, Point3d b) =>
            Math.Abs(a.X - b.X) <= 1e-6 && Math.Abs(a.Y - b.Y) <= 1e-6;

        private static bool SameXZ(Point3d a, Point3d b) =>
            Math.Abs(a.X - b.X) <= 1e-6 && Math.Abs(a.Z - b.Z) <= 1e-6;

        private static bool SameZ(Point3d a, Point3d b) => Math.Abs(a.Z - b.Z) <= 1e-6;
        private static bool SameX(Point3d a, Point3d b) => Math.Abs(a.X - b.X) <= 1e-6;
        private static bool SameY(Point3d a, Point3d b) => Math.Abs(a.Y - b.Y) <= 1e-6;

        private Vector3d ComputeVariantShift(Point3d twinSupply, Point3d twinReturn, Point3d bondedSupply)
        {
            var variantUpper = Variant.ToUpperInvariant();
            if (variantUpper != "V3" && variantUpper != "V4")
                return default;

            var twinMidX = 0.5 * (twinSupply.X + twinReturn.X);
            var deltaX = twinMidX - bondedSupply.X;
            if (Math.Abs(deltaX) < 1e-9)
                return default;

            return new Vector3d(deltaX, 0.0, 0.0);
        }

        private void ApplyShiftToPrimitives(Vector3d shift)
        {
            for (int i = 0; i < _primitives.Count; i++)
            {
                _primitives[i] = _primitives[i].Shifted(shift);
            }
        }

        private static PortInfo CreatePortInfo(EndpointRecord record, Vector3d shift)
        {
            var tangent = (record.Other - record.Point).Length < 1e-9
                ? Vector3d.XAxis
                : (record.Other - record.Point).GetNormal();
            return new PortInfo(record.Point + shift, tangent);
        }

        private sealed class EndpointUsage
        {
            public int PipeCount;
            public int ElbowCount;
        }

        private readonly record struct EndpointRecord(FlowRole Flow, Point3d Point, Point3d Other);
    }

    internal readonly record struct PortInfo(Point3d Position, Vector3d Tangent);

    internal enum PrimitiveKind { Pipe, Elbow }

    internal sealed class FModelPrimitive
    {
        public FlowRole Flow { get; }
        public PrimitiveKind Kind { get; }
        public Point3d P1 { get; }
        public Point3d P2 { get; }
        public Point3d? Centre { get; }

        public FModelPrimitive(FlowRole flow, PrimitiveKind kind, Point3d p1, Point3d p2, Point3d? centre)
        {
            Flow = flow;
            Kind = kind;
            P1 = p1;
            P2 = p2;
            Centre = centre;
        }

        public FModelPrimitive Shifted(Vector3d shift) =>
            new(
                Flow,
                Kind,
                P1 + shift,
                P2 + shift,
                Centre.HasValue ? Centre.Value.Add(shift) : null);
    }
}

