using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

using NetTopologySuite.Geometries;

using NorsynDistrictZones.Model;
using NorsynDistrictZones.Pricing;
using NorsynDistrictZones.Topology;

using NhsPipeType = NorsynHydraulicCalc.PipeType;

namespace NorsynDistrictZones.Acad;

/// <summary>
/// Orchestrates creating a rendered, priced zone from a face polygon. This is the
/// command-driven core; the automatic reactor (P8) and grip editing (P9) will call
/// the same primitives. Pricing is computed live and never persisted (the Xref can change).
/// </summary>
internal static class ZoneService
{
    /// <summary>EU/Danish number formatting for the price label (dot thousands, comma decimal),
    /// independent of AutoCAD's host culture.</summary>
    private static readonly System.Globalization.CultureInfo Da =
        System.Globalization.CultureInfo.GetCultureInfo("da-DK");

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

    /// <summary>
    /// Price all pipe portions that fall inside the face (in-memory clip), formatted for the
    /// label. Any (type, DN) the active catalog can't price is posted as a warning to the
    /// command line; pass a shared <paramref name="warned"/> set across a bulk recompute to
    /// suppress duplicate warnings for the same missing entry.
    /// </summary>
    public static string PriceFace(
        ZoneFace face, PipePriceCatalog catalog, IReadOnlyList<PipeSegment> pipes,
        out PriceCalculator.ZonePrice price,
        ISet<(NhsPipeType Type, int Dn)>? warned = null)
    {
        var contributions = pipes
            .Select(p => (Pipe: p, LengthInside: ZoneGeometryOps.InsideLength(face.Polygon, p.Geometry)))
            .Where(x => x.LengthInside > 0);

        price = PriceCalculator.PriceZone(catalog, contributions);

        if (price.MissingEntries.Count > 0)
            WarnMissingEntries(catalog.Name, price.MissingEntries, warned);

        if (price.PipeCount == 0) return "— no pipes —";
        // Fail LOUD, never silent-wrong (user decision): if any pipe in the zone lacks the
        // authoritative NHS identity (NORSYN_NHS_PIPE XData: pipe type + FL/SL role), the pipe
        // is unidentifiable and NOTHING is guessed — show a clear call-to-action instead of a
        // partial/possibly-wrong total.
        if (price.AnyProvisional) return "incomplete pipe data — re-export DIM";
        return $"{price.Total.ToString("N0", Da)} DKK";
    }

    /// <summary>Re-price and re-render every zone with the active catalog (config switch / NDZRECALC).</summary>
    public static int RecomputeAll(Database db)
    {
        using Transaction tx = db.TransactionManager.StartTransaction();
        IReadOnlyList<PipeSegment> pipes = PipeReader.ReadFromXrefs(db, tx, null);
        PipePriceCatalog catalog = CatalogStore.GetActive(db);
        var warned = new HashSet<(NhsPipeType Type, int Dn)>();
        int n = 0;
        foreach (var (cid, face) in ZoneReader.ReadAll(db, tx))
        {
            var nc = (NorsynObjectsInterop.NorsynContainer)tx.GetObject(cid, OpenMode.ForWrite);
            string price = PriceFace(face, catalog, pipes, out _, warned);
            ZoneRenderer.Update(db, tx, nc, face, price);
            n++;
        }
        tx.Commit();
        Reactors.ZoneSession.For(db).Clear();
        return n;
    }

    /// <summary>
    /// Post a command-line warning for every distinct (type, DN) the active catalog couldn't
    /// price. A missing entry understates the zone total, so it must never pass silently — it
    /// is the catalog-side twin of the missing-stamp failure. <paramref name="warned"/>, when
    /// supplied, suppresses repeats of the same entry across a multi-zone recompute.
    /// </summary>
    private static void WarnMissingEntries(
        string catalogName,
        IReadOnlyList<(NhsPipeType Type, int Dn)> missing,
        ISet<(NhsPipeType Type, int Dn)>? warned)
    {
        Editor? ed = Autodesk.AutoCAD.ApplicationServices.Application
            .DocumentManager.MdiActiveDocument?.Editor;
        if (ed is null) return;
        foreach (var m in missing)
        {
            if (warned is not null && !warned.Add(m)) continue;
            ed.WriteMessage(
                $"\nNDZ WARNING: price catalog '{catalogName}' has no entry for {m.Type} DN{m.Dn}; " +
                "that pipe priced as 0 and the zone total is understated.");
        }
    }

    private static int RandomColorArgb(Random rng)
    {
        // Mid-bright, readable hues (avoid near-black and near-white).
        int r = rng.Next(60, 225), g = rng.Next(60, 225), b = rng.Next(60, 225);
        return (0xFF << 24) | (r << 16) | (g << 8) | b;
    }
}
