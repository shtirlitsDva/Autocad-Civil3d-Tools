using System.Text.RegularExpressions;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.UtilsCommon;

using static IntersectUtilities.UtilsCommon.Utils;

using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;

namespace IntersectUtilities.MPE.MapConnections;

/// <summary>
/// Reads the LP drawing (the data-referenced alignments, their profile views and the branch blocks CREATEDETAILING
/// placed on them) and works out where the LPs touch. Everything comes from this one drawing — no Fremtid, no
/// PipelineNetwork. Two alignments are connected when an end of one lies within <see cref="TouchTolerance"/> of
/// the other; the Svanehals and "Afgrening med spring" joints sit up to ~0.4 m off, hence 0.5 rather than 0.05.
/// </summary>
internal static class LpConnectionResolver
{
    internal const double TouchTolerance = 0.5;
    private const double SameSpotTolerance = 1.0;   // plan distance under which two hits are one connection
    private const double HopMergeTolerance = 0.5;   // station distance under which connections share a hop point
    private const double BlockMatchTolerance = 1.0; // station distance from a branch block to its connection
    private const double BlockViewBuffer = 2.0;     // a block this close to a view's graph rectangle is on it
    private const double SampleSpacing = 2.0;       // plan sampling step for the network view

    private const string BranchBlockName = "DRISizeChangeAnno";

    private static readonly HashSet<string> BranchTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Parallelafgrening",
        "Lige afgrening",
        "Afgrening med spring",
        "Afgrening, parallel",
        "Svejsetee",
        "Preskobling tee",
        "Stikafgrening",
        "Afgreningsstuds",
        "Svanehals",
    };

    private static readonly Regex NaName = new(@"^NA\s*\d+", RegexOptions.IgnoreCase);

    /// <summary>
    /// Builds the snapshot. Alignment.GetPolyline() adds a polyline to the database, so the caller must hold the
    /// document lock; the transaction is always aborted, leaving the drawing untouched.
    /// </summary>
    public static LpNetworkSnapshot Build(Database db)
    {
        Dictionary<string, Polyline> plans = new();
        Dictionary<string, double> startStations = new();
        Dictionary<string, LpProfileView> views = new();
        List<(Point2d Position, string Type, string Right)> rawBlocks = [];

        try
        {
            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord ms = (BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                foreach (ObjectId id in ms)
                {
                    DBObject obj = tx.GetObject(id, OpenMode.ForRead);
                    switch (obj)
                    {
                        case Alignment al:
                            ReadAlignment(tx, al, plans, startStations);
                            break;
                        case ProfileView pv:
                            ReadProfileView(tx, pv, views);
                            break;
                        case BlockReference br when br.RealName() == BranchBlockName:
                            string type = (br.GetAttributeStringValue("LEFTSIZE") ?? "").Trim();
                            if (BranchTypes.Contains(type))
                            {
                                string right = (br.GetAttributeStringValue("RIGHTSIZE") ?? "").Trim();
                                rawBlocks.Add((new Point2d(br.Position.X, br.Position.Y), type, right));
                            }
                            break;
                    }
                }

                tx.Abort();
            }

            Dictionary<string, LpLine> lines = new();
            foreach ((string name, Polyline pl) in plans)
            {
                views.TryGetValue(name, out LpProfileView? view);
                lines[name] = new LpLine(name, startStations[name], pl.Length, Sample(pl), view);
            }

            List<LpBranchBlock> blocks = PlaceBlocks(rawBlocks, lines.Values);
            List<LpConnection> connections = FindConnections(plans, startStations);
            connections = LabelConnections(connections, blocks);
            connections.AddRange(NaConnections(plans, startStations, blocks, connections));

            Dictionary<string, List<LpHopPoint>> hops = BuildHopPoints(lines, connections, blocks);
            return new LpNetworkSnapshot(lines, connections, hops);
        }
        finally
        {
            foreach (Polyline pl in plans.Values)
            {
                pl.Dispose();
            }
        }
    }

    private static void ReadAlignment(
        Transaction tx, Alignment al, Dictionary<string, Polyline> plans, Dictionary<string, double> startStations)
    {
        if (plans.ContainsKey(al.Name))
        {
            return;
        }

        try
        {
            // GetPolyline() appends a real polyline to the drawing; keep a detached clone and let the aborted
            // transaction take the original away again.
            Polyline dbPl = (Polyline)tx.GetObject(al.GetPolyline(), OpenMode.ForRead);
            plans[al.Name] = (Polyline)dbPl.Clone();
            startStations[al.Name] = al.StartingStation;
        }
        catch (System.Exception ex)
        {
            prdDbg($"MapConnections: kunne ikke læse alignment {al.Name}: {ex.Message}");
        }
    }

    private static void ReadProfileView(Transaction tx, ProfileView pv, Dictionary<string, LpProfileView> views)
    {
        try
        {
            Alignment al = (Alignment)tx.GetObject(pv.AlignmentId, OpenMode.ForRead);

            // Prefer the "{alignment}_PV" view when a drawing carries more than one view per alignment.
            if (views.ContainsKey(al.Name) && pv.Name != $"{al.Name}_PV")
            {
                return;
            }

            double x0 = 0, y0 = 0, x1 = 0, y1 = 0;
            pv.FindXYAtStationAndElevation(pv.StationStart, pv.ElevationMin, ref x0, ref y0);
            pv.FindXYAtStationAndElevation(pv.StationEnd, pv.ElevationMax, ref x1, ref y1);
            if (Math.Abs(x1 - x0) < 1e-6)
            {
                return;
            }

            views[al.Name] = new LpProfileView(
                pv.Name, pv.StationStart, pv.StationEnd, x0, x1, Math.Min(y0, y1), Math.Max(y0, y1));
        }
        catch (System.Exception ex)
        {
            prdDbg($"MapConnections: kunne ikke læse profile view {pv.Name}: {ex.Message}");
        }
    }

    private static List<Point2d> Sample(Polyline pl)
    {
        List<Point2d> samples = [];
        double length = pl.Length;
        int count = Math.Clamp((int)Math.Ceiling(length / SampleSpacing), 1, 400);
        for (int i = 0; i <= count; i++)
        {
            Point3d p = pl.GetPointAtDist(Math.Min(length, length * i / count));
            samples.Add(new Point2d(p.X, p.Y));
        }

        return samples;
    }

    private static List<LpBranchBlock> PlaceBlocks(
        IEnumerable<(Point2d Position, string Type, string Right)> rawBlocks, IEnumerable<LpLine> lines)
    {
        List<LpLine> withViews = lines.Where(l => l.View is not null).ToList();
        List<LpBranchBlock> blocks = [];
        foreach ((Point2d position, string type, string right) in rawBlocks)
        {
            LpLine? host = withViews
                .Select(l => (Line: l, Distance: l.View!.DistanceTo(position)))
                .Where(x => x.Distance <= BlockViewBuffer)
                .OrderBy(x => x.Distance)
                .Select(x => x.Line)
                .FirstOrDefault();
            if (host is not null)
            {
                blocks.Add(new LpBranchBlock(host.Name, host.View!.StationAt(position.X), position, type, right));
            }
        }

        return blocks;
    }

    private static List<LpConnection> FindConnections(
        Dictionary<string, Polyline> plans, Dictionary<string, double> startStations)
    {
        List<LpConnection> found = [];
        foreach ((string a, Polyline plA) in plans)
        {
            foreach ((string b, Polyline plB) in plans)
            {
                if (a == b)
                {
                    continue;
                }

                foreach (Point3d end in new[] { plA.StartPoint, plA.EndPoint })
                {
                    Point3d onB = plB.GetClosestPointTo(end, false);
                    if (onB.DistanceTo(end) > TouchTolerance)
                    {
                        continue;
                    }

                    double distA = plA.GetDistAtPoint(plA.GetClosestPointTo(end, false));
                    double distB = plB.GetDistAtPoint(onB);
                    double stationA = startStations[a] + distA;
                    double stationB = startStations[b] + distB;
                    Point2d at = new(end.X, end.Y);

                    bool bAtEnd = distB <= TouchTolerance || distB >= plB.Length - TouchTolerance;
                    LpConnection connection = bAtEnd
                        ? string.CompareOrdinal(a, b) < 0
                            ? new LpConnection(a, stationA, b, stationB, at, LpConnectionKind.EndToEnd, null)
                            : new LpConnection(b, stationB, a, stationA, at, LpConnectionKind.EndToEnd, null)
                        : new LpConnection(b, stationB, a, stationA, at, LpConnectionKind.Branch, null);

                    // End-to-end joints are found from both sides; keep one.
                    bool duplicate = found.Any(c =>
                        ((c.From == connection.From && c.To == connection.To) || (c.From == connection.To && c.To == connection.From))
                        && c.PlanPoint.GetDistanceTo(at) <= SameSpotTolerance);
                    if (!duplicate)
                    {
                        found.Add(connection);
                    }
                }
            }
        }

        return found.OrderBy(c => c.From).ThenBy(c => c.FromStation).ToList();
    }

    /// <summary>Names each connection after the branch block sitting on it, preferring the block on the From side.</summary>
    private static List<LpConnection> LabelConnections(List<LpConnection> connections, List<LpBranchBlock> blocks)
    {
        return connections
            .Select(c => c with
            {
                Label = NearestBlock(blocks, c.From, c.FromStation)?.Type ?? NearestBlock(blocks, c.To, c.ToStation)?.Type,
            })
            .ToList();
    }

    private static LpBranchBlock? NearestBlock(IEnumerable<LpBranchBlock> blocks, string host, double station) =>
        blocks
            .Where(b => b.Host == host && Math.Abs(b.Station - station) <= BlockMatchTolerance)
            .OrderBy(b => Math.Abs(b.Station - station))
            .FirstOrDefault();

    private static IEnumerable<LpConnection> NaConnections(
        Dictionary<string, Polyline> plans, Dictionary<string, double> startStations,
        List<LpBranchBlock> blocks, List<LpConnection> connections)
    {
        HashSet<(string Host, string Na)> seen = new();
        foreach (LpBranchBlock block in blocks)
        {
            Match match = NaName.Match(block.Right);
            if (!match.Success || !seen.Add((block.Host, match.Value)))
            {
                continue;
            }

            if (connections.Any(c => c.Involves(block.Host)
                && Math.Abs(StationOn(c, block.Host) - block.Station) <= BlockMatchTolerance))
            {
                continue;
            }

            Polyline pl = plans[block.Host];
            double dist = Math.Clamp(block.Station - startStations[block.Host], 0.0, pl.Length);
            Point3d at = pl.GetPointAtDist(dist);
            yield return new LpConnection(
                block.Host, block.Station, match.Value, double.NaN, new Point2d(at.X, at.Y), LpConnectionKind.Na, block.Type);
        }
    }

    internal static double StationOn(LpConnection c, string lp) => c.From == lp ? c.FromStation : c.ToStation;

    private static Dictionary<string, List<LpHopPoint>> BuildHopPoints(
        Dictionary<string, LpLine> lines, List<LpConnection> connections, List<LpBranchBlock> blocks)
    {
        Dictionary<string, List<LpHopPoint>> hops = new();
        foreach (LpLine line in lines.Values)
        {
            if (line.View is null)
            {
                continue;
            }

            // Every LP-to-LP connection on this line, as (station here, the other LP, its station).
            List<(double Station, LpHopTarget Target)> touches = connections
                .Where(c => c.Kind != LpConnectionKind.Na && c.Involves(line.Name))
                .Select(c => c.From == line.Name
                    ? (c.FromStation, new LpHopTarget(c.To, c.ToStation))
                    : (c.ToStation, new LpHopTarget(c.From, c.FromStation)))
                .OrderBy(t => t.Item1)
                .ToList();

            List<(double Station, List<LpHopTarget> Targets)> groups = [];
            foreach ((double station, LpHopTarget target) in touches)
            {
                if (groups.Count > 0 && Math.Abs(groups[^1].Station - station) <= HopMergeTolerance)
                {
                    if (!groups[^1].Targets.Any(t => t.Lp == target.Lp))
                    {
                        groups[^1].Targets.Add(target);
                    }
                }
                else
                {
                    groups.Add((station, [target]));
                }
            }

            List<LpBranchBlock> hostBlocks = blocks.Where(b => b.Host == line.Name).ToList();
            List<LpHopPoint> lineHops = [];
            foreach ((double station, List<LpHopTarget> targets) in groups)
            {
                LpBranchBlock? block = NearestBlock(hostBlocks, line.Name, station);
                lineHops.Add(new LpHopPoint(
                    line.Name, station, block?.Position ?? line.View.PointAt(station), block?.Type,
                    targets.OrderBy(t => t.Lp).ToList(), null));
            }

            // Branch blocks with no alignment behind them: NA pipelines, or a tee whose branch was never drawn.
            // Blocks next to a connection belong to it (a Svanehals is drawn as a pair), and a pair of orphan
            // blocks on the same spot is one hop point.
            foreach (LpBranchBlock block in hostBlocks.OrderBy(b => b.Station))
            {
                if (groups.Any(g => Math.Abs(g.Station - block.Station) <= BlockMatchTolerance)
                    || lineHops.Any(h => h.Targets.Count == 0 && Math.Abs(h.Station - block.Station) <= BlockMatchTolerance))
                {
                    continue;
                }

                Match match = NaName.Match(block.Right);
                lineHops.Add(new LpHopPoint(
                    line.Name, block.Station, block.Position, block.Type, [], match.Success ? match.Value : null));
            }

            hops[line.Name] = lineHops.OrderBy(h => h.Station).ToList();
        }

        return hops;
    }
}

/// <summary>The resolved connection map of one LP drawing. Holds no database objects.</summary>
internal sealed class LpNetworkSnapshot
{
    private const double HostSearchDistance = 40.0; // how far outside a view's graph the cursor may be

    public LpNetworkSnapshot(
        IReadOnlyDictionary<string, LpLine> lines,
        IReadOnlyList<LpConnection> connections,
        IReadOnlyDictionary<string, List<LpHopPoint>> hops)
    {
        Lines = lines;
        Connections = connections;
        Hops = hops;
    }

    public IReadOnlyDictionary<string, LpLine> Lines { get; }
    public IReadOnlyList<LpConnection> Connections { get; }
    public IReadOnlyDictionary<string, List<LpHopPoint>> Hops { get; }

    /// <summary>The LP whose profile view the point is in (or nearest to, within a band around the views).</summary>
    public LpLine? FindHost(Point2d point) =>
        Lines.Values
            .Where(l => l.View is not null)
            .Select(l => (Line: l, Distance: l.View!.DistanceTo(point)))
            .Where(x => x.Distance <= HostSearchDistance)
            .OrderBy(x => x.Distance)
            .Select(x => x.Line)
            .FirstOrDefault();

    /// <summary>The hop point nearest the cursor's station on the view the cursor is over, within <paramref name="maxStationDistance"/>.</summary>
    public LpHopPoint? FindHop(Point2d cursor, double maxStationDistance, out LpLine? host)
    {
        host = FindHost(cursor);
        if (host is null || !Hops.TryGetValue(host.Name, out List<LpHopPoint>? hops))
        {
            return null;
        }

        double station = host.View!.StationAt(cursor.X);
        return hops
            .Select(h => (Hop: h, Distance: Math.Abs(h.Station - station)))
            .Where(x => x.Distance <= maxStationDistance)
            .OrderBy(x => x.Distance)
            .Select(x => x.Hop)
            .FirstOrDefault();
    }
}
