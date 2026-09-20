using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon;

using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// ONE LEG A BEND ACTUALLY DRAWS: the world direction it runs in from the
/// corner, and how long it is.
///
/// CARRIED BY DIRECTION, NOT BY NAME. The block states its two lengths as L1
/// and L2, and which of those is which SIDE of the corner depends on how the
/// drafter placed and flipped it - measured 2026-09-21 on
/// PRÆBØJN-90GR-VARIABEL-GLD: L1 runs along the block's local +X and L2 at
/// V-180°, and either is mirrored by the block's own flip states. Two older
/// versions of the block exist and neither could be probed, so an axis
/// convention read off one version is exactly the guess this must not make.
/// A direction is the one fact that survives all of it.
/// </summary>
internal readonly record struct LegacyLeg(Vector2d Direction, double LengthM);

/// <summary>
/// WHAT A CORNER'S BLOCK SAYS ABOUT ITS LEGS. Three outcomes, each its own
/// type, because "states none" and "states two nobody can place" are different
/// answers and an empty list would collapse them into one.
/// </summary>
internal abstract record LegacyLegs
{
    internal abstract void Tell(string handle, ILegAudience audience);
}

/// <summary>A bend made to one fixed shape: it has no legs to state.</summary>
internal sealed record NoLegsStated : LegacyLegs
{
    internal override void Tell(string handle, ILegAudience audience) => audience.None();
}

/// <summary>The block states two legs and the drawing confirms which side each is on.</summary>
internal sealed record LegsDrawn(LegacyLeg A, LegacyLeg B) : LegacyLegs
{
    internal override void Tell(string handle, ILegAudience audience) => audience.Drawn(A, B);
}

/// <summary>
/// The block states two legs and the drawing does not say which side each is
/// on. REFUSED RATHER THAN GUESSED: a swapped pair is silent - both are legal
/// lengths, the family derives, nothing complains, and the bend is simply
/// wrong on the half of the drawing where the route runs the other way.
/// </summary>
internal sealed record LegsUnplaceable(string Why) : LegacyLegs
{
    internal override void Tell(string handle, ILegAudience audience) =>
        audience.Unplaceable(handle, Why);
}

/// <summary>What a caller does about one corner's legs.</summary>
internal interface ILegAudience
{
    void None();
    void Drawn(LegacyLeg a, LegacyLeg b);
    void Unplaceable(string handle, string why);
}

/// <summary>
/// READS A BEND'S LEGS OFF THE BLOCK AS DRAWN.
///
/// The lengths are the block's own L1 and L2 - exact, and the only place they
/// are exact. The SIDES are measured: each port gives a direction out of the
/// corner, and how far the block's geometry reaches along that direction says
/// which stated length lies there. The two are then cross-checked against each
/// other, so a block whose geometry and properties disagree is refused instead
/// of being taken at the word of either.
/// </summary>
internal static class LegacyElbowLegs
{
    //A port sits on the bend's unit circle, so it gives a DIRECTION and never a
    //length (measured 2026-09-21: the ports stay at 1.0 m whatever L1 and L2
    //are). The reach along it is what gives the length.
    private const double MinPortDistance = 1e-6;

    //The reach along a leg's own direction is the leg, except that the bend's
    //body reaches about 0.11 m on its own. A stated length this far from what
    //the drawing reaches is a block the two halves do not agree about.
    private const double ReachTolerance = 0.25;

    public static LegacyLegs Read(BlockReference br)
    {
        Transaction tx = br.Database.TransactionManager.TopTransaction;
        if (tx == null) return new NoLegsStated();

        List<double> stated = Stated(br);
        if (stated.Count != 2) return new NoLegsStated();

        List<ComponentPort> ports = ComponentPorts.Read(br, tx)
            .Where(p => p.Role == ComponentPortRole.Neutral).ToList();
        if (ports.Count != 2)
            return new LegsUnplaceable(
                $"blokken oplyser to ben, men har {ports.Count} tilslutning(er)");

        Point2d corner = new Point2d(br.Position.X, br.Position.Y);
        Vector2d[] dir = new Vector2d[2];
        for (int i = 0; i < 2; i++)
        {
            Vector2d v = new Point2d(ports[i].Position.X, ports[i].Position.Y) - corner;
            if (v.Length < MinPortDistance)
                return new LegsUnplaceable("blokkens tilslutning ligger i hjørnet selv");
            dir[i] = v / v.Length;
        }

        double[] reach = { Reach(br, tx, corner, dir[0]), Reach(br, tx, corner, dir[1]) };

        //TWO WAYS TO PAIR TWO THINGS. The better pairing has to be better by
        //more than the measurement is worth, or nothing is claimed.
        double straight = Math.Abs(reach[0] - stated[0]) + Math.Abs(reach[1] - stated[1]);
        double crossed = Math.Abs(reach[0] - stated[1]) + Math.Abs(reach[1] - stated[0]);
        double best = Math.Min(straight, crossed);
        if (best > ReachTolerance)
            return new LegsUnplaceable(
                $"blokken oplyser ben {stated[0]:0.###} og {stated[1]:0.###} m, men tegner " +
                $"{reach[0]:0.###} og {reach[1]:0.###} m");

        return straight <= crossed
            ? new LegsDrawn(new LegacyLeg(dir[0], stated[0]), new LegacyLeg(dir[1], stated[1]))
            : new LegsDrawn(new LegacyLeg(dir[0], stated[1]), new LegacyLeg(dir[1], stated[0]));
    }

    /// <summary>The two lengths the block states, in the order it states them.</summary>
    private static List<double> Stated(BlockReference br)
    {
        List<double> stated = new List<double>();
        foreach (DynamicBlockReferenceProperty p in br.DynamicBlockReferencePropertyCollection)
        {
            if (p.PropertyName != "L1" && p.PropertyName != "L2") continue;
            if (p.Value is double d) stated.Add(d);
        }
        return stated;
    }

    /// <summary>
    /// How far the block's own geometry reaches along <paramref name="d"/> from
    /// the corner. The extent corners bound the geometry, so this never reads
    /// SHORT - which is what lets a disagreement be a refusal rather than a
    /// quiet undercount.
    /// </summary>
    private static double Reach(BlockReference br, Transaction tx, Point2d corner, Vector2d d)
    {
        BlockTableRecord btr = (BlockTableRecord)tx.GetObject(br.BlockTableRecord, OpenMode.ForRead);
        Matrix3d xform = br.BlockTransform;
        double most = 0.0;
        foreach (ObjectId id in btr)
        {
            Entity ent = tx.GetObject(id, OpenMode.ForRead) as Entity;
            if (ent == null) continue;
            Extents3d ext;
            try { ext = ent.GeometricExtents; } catch { continue; }
            foreach (Point3d p in Corners(ext))
            {
                Point3d w = p.TransformBy(xform);
                double along = (new Point2d(w.X, w.Y) - corner).DotProduct(d);
                if (along > most) most = along;
            }
        }
        return most;
    }

    private static IEnumerable<Point3d> Corners(Extents3d e)
    {
        yield return new Point3d(e.MinPoint.X, e.MinPoint.Y, 0.0);
        yield return new Point3d(e.MinPoint.X, e.MaxPoint.Y, 0.0);
        yield return new Point3d(e.MaxPoint.X, e.MinPoint.Y, 0.0);
        yield return new Point3d(e.MaxPoint.X, e.MaxPoint.Y, 0.0);
    }
}
