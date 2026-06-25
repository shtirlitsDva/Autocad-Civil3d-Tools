using Autodesk.AutoCAD.DatabaseServices;

using NetTopologySuite.Geometries;

using NorsynDistrictZones.Model;
using NorsynDistrictZones.Pricing;
using NorsynDistrictZones.Topology;

namespace NorsynDistrictZones.Acad;

/// <summary>
/// Orchestrates creating a rendered, priced zone from a face polygon. This is the
/// command-driven core; the automatic reactor (P8) and grip editing (P9) will call
/// the same primitives. Pricing is computed live and never persisted (the Xref can change).
/// </summary>
internal static class ZoneService
{
    /// <summary>Next free zone number = max existing NDZ zone number + 1 (1 if none).</summary>
    public static int NextNumber(Transaction tx, BlockTableRecord ms)
    {
        int max = 0;
        foreach (ObjectId id in ms)
        {
            if (tx.GetObject(id, OpenMode.ForRead) is not Entity ent) continue;
            if (ent.GetRXClass().Name != "NorsynContainer") continue;
            ZoneRecord? rec = ZoneXData.Read(ent);
            if (rec is { } r && r.Number > max) max = r.Number;
        }
        return max + 1;
    }

    /// <summary>Build the face, price it against the supplied pipes, and render it. Returns the new face.</summary>
    public static ZoneFace CreateAndRender(
        Database db, Transaction tx, BlockTableRecord ms,
        Polygon polygon, PipePriceCatalog catalog,
        IReadOnlyList<PipeSegment> pipes, Func<Guid> newGuid, Random rng)
    {
        var face = new ZoneFace(new ZoneId(newGuid()), polygon)
        {
            Number = NextNumber(tx, ms),
            ColorArgb = RandomColorArgb(rng),
            Name = string.Empty,
        };

        string priceText = PriceFace(face, catalog, pipes, out _);
        ObjectId id = ZoneRenderer.Render(db, tx, ms, face, priceText);
        Reactors.ZoneSession.For(db).Add(id, face);
        return face;
    }

    /// <summary>Price all pipe portions that fall inside the face (in-memory clip), formatted for the label.</summary>
    public static string PriceFace(
        ZoneFace face, PipePriceCatalog catalog, IReadOnlyList<PipeSegment> pipes,
        out PriceCalculator.ZonePrice price)
    {
        var contributions = pipes
            .Select(p => (Pipe: p, LengthInside: ZoneGeometryOps.InsideLength(face.Polygon, p.Geometry)))
            .Where(x => x.LengthInside > 0);

        price = PriceCalculator.PriceZone(catalog, contributions);

        if (price.PipeCount == 0) return "— no pipes —";
        // Fail LOUD, not silent-wrong (user decision): if any pipe in the zone lacks
        // authoritative FL/SL identity (NORSYN_NHS_PIPE XData), the stik surcharge can't be
        // trusted, so show a clear call-to-action instead of a possibly-wrong total.
        if (price.AnyProvisional) return "re-export DIM (no FL/SL data)";
        return $"{price.Total:N0} DKK";
    }

    /// <summary>Re-price and re-render every zone with the active catalog (config switch / NDZRECALC).</summary>
    public static int RecomputeAll(Database db)
    {
        using Transaction tx = db.TransactionManager.StartTransaction();
        IReadOnlyList<PipeSegment> pipes = PipeReader.ReadFromXrefs(db, tx, null);
        PipePriceCatalog catalog = CatalogStore.GetActive(db);
        int n = 0;
        foreach (var (cid, face) in ZoneReader.ReadAll(db, tx))
        {
            var nc = (NorsynObjectsInterop.NorsynContainer)tx.GetObject(cid, OpenMode.ForWrite);
            string price = PriceFace(face, catalog, pipes, out _);
            ZoneRenderer.Update(db, tx, nc, face, price);
            n++;
        }
        tx.Commit();
        Reactors.ZoneSession.For(db).Clear();
        return n;
    }

    private static int RandomColorArgb(Random rng)
    {
        // Mid-bright, readable hues (avoid near-black and near-white).
        int r = rng.Next(60, 225), g = rng.Next(60, 225), b = rng.Next(60, 225);
        return (0xFF << 24) | (r << 16) | (g << 8) | b;
    }
}
