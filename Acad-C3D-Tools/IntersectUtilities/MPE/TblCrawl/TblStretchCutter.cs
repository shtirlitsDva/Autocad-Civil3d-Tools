using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.Tbl;
using IntersectUtilities.UtilsCommon;

using NetTopologySuite.Geometries;
using NetTopologySuite.LinearReferencing;

namespace IntersectUtilities.MPE.TblCrawl;

/// <summary>
/// The cut rule (decision 3): a new stretch wins over the part of an older one it covers, so two
/// stretches never share pipe. An older stretch is split at the ends of every stretch of it that lies on
/// a new line, the covered piece is erased and the rest is kept with its layer and property set values.
/// Stretches on every TBL layer are cut, whatever their Belægning.
///
/// "Lies on" is a coincident core, not a corridor: the older line must come within
/// <see cref="TblStretch.CoincidenceTolerance"/> of the new one over at least
/// <see cref="TblStretch.MinOverlap"/>, inside a part of it within <see cref="TblStretch.Tolerance"/>. A
/// branch that only leaves a stretch at a T touches it for a few millimetres and cuts nothing.
/// </summary>
internal static class TblStretchCutter
{
    /// <summary>Returns how many covered pieces were erased from older stretches.</summary>
    public static int CutUnder(
        Database db, Transaction tx, IReadOnlyList<LineString> newLines, PropertySetManager psm, PSetDefs.DriOmråder def)
    {
        int erased = 0;
        List<Polyline> existing = db.ListOfType<Polyline>(tx).Where(p => TblBelægninger.IsStretchLayer(p.Layer)).ToList();
        foreach (Polyline old in existing)
        {
            LineString oldLine = TblStretchGeometry.ToLineString(old);
            List<(double From, double To)> overlaps = Merge(Overlaps(oldLine, newLines));
            if (overlaps.Count == 0)
            {
                continue;
            }

            erased += overlaps.Count;
            Replace(old, oldLine, overlaps, tx, psm, def);
        }

        return erased;
    }

    private static IEnumerable<(double From, double To)> Overlaps(LineString oldLine, IReadOnlyList<LineString> newLines)
    {
        LengthIndexedLine index = new(oldLine);
        foreach (LineString line in newLines)
        {
            Envelope reach = new(line.EnvelopeInternal);
            reach.ExpandBy(2 * TblStretch.Tolerance);
            if (!reach.Intersects(oldLine.EnvelopeInternal))
            {
                continue;
            }

            Geometry coincident = TblStretchGeometry.Corridor(line, TblStretch.CoincidenceTolerance);
            Geometry near = oldLine.Intersection(TblStretchGeometry.Corridor(line, TblStretch.Tolerance));
            foreach (LineString part in TblStretchGeometry.Parts(near))
            {
                Geometry core = part.Intersection(coincident);
                if (core.Length < TblStretch.MinOverlap)
                {
                    continue;
                }

                double[] stations = core.Coordinates.Select(c => index.Project(c)).ToArray();
                yield return (stations.Min(), stations.Max());
            }
        }
    }

    private static List<(double From, double To)> Merge(IEnumerable<(double From, double To)> overlaps)
    {
        List<(double From, double To)> merged = [];
        foreach ((double from, double to) in overlaps.OrderBy(o => o.From))
        {
            if (merged.Count > 0 && from <= merged[^1].To + TblStretch.CoincidenceTolerance)
            {
                merged[^1] = (merged[^1].From, Math.Max(merged[^1].To, to));
            }
            else
            {
                merged.Add((from, to));
            }
        }

        return merged;
    }

    private static void Replace(
        Polyline old, LineString oldLine, List<(double From, double To)> overlaps, Transaction tx,
        PropertySetManager psm, PSetDefs.DriOmråder def)
    {
        old.UpgradeOpen();
        PSetDefs.Property[] properties = [def.Nummer, def.Vejnavn, def.Vejklasse, def.Belægning];
        string[] values = properties.Select(p => psm.ReadPropertyString(old, p)).ToArray();

        LengthIndexedLine index = new(oldLine);
        double length = oldLine.Length;
        DoubleCollection parameters = [];
        foreach (double station in overlaps.SelectMany(o => new[] { o.From, o.To })
                     .Where(s => s > TblStretch.CoincidenceTolerance && s < length - TblStretch.CoincidenceTolerance)
                     .Distinct()
                     .OrderBy(s => s))
        {
            Coordinate c = index.ExtractPoint(station);
            Point3d at = old.GetClosestPointTo(new Point3d(c.X, c.Y, old.Elevation), false);
            parameters.Add(old.GetParameterAtPoint(at));
        }

        // No cut inside the line: the new stretch covers all of it.
        DBObjectCollection pieces = parameters.Count == 0 ? [] : old.GetSplitCurves(parameters);
        BlockTableRecord space = (BlockTableRecord)tx.GetObject(old.OwnerId, OpenMode.ForWrite);
        foreach (DBObject obj in pieces)
        {
            if (obj is not Polyline piece)
            {
                obj.Dispose();
                continue;
            }

            Point3d mid = piece.GetPointAtDist(piece.Length / 2);
            double station = index.Project(new Coordinate(mid.X, mid.Y));
            if (piece.Length < TblStretch.CoincidenceTolerance || overlaps.Any(o => station >= o.From && station <= o.To))
            {
                piece.Dispose();
                continue;
            }

            space.AppendEntity(piece);
            tx.AddNewlyCreatedDBObject(piece, true);
            for (int i = 0; i < properties.Length; i++)
            {
                psm.WritePropertyString(piece, properties[i], values[i]);
            }
        }

        old.Erase();
    }
}
