using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using PsDataType = Autodesk.Aec.PropertyData.DataType;

namespace IntersectUtilities.MPE.MatchBBR
{
    // The boundary polygons, captured as plain vertex rings rather than as Polyline DBObjects.
    // The palette outlives every transaction, so holding open entities is not an option; the
    // rings are all the point-in-polygon test needs anyway.
    internal sealed class BoundarySnapshot
    {
        private readonly IReadOnlyList<Point2d[]> _rings;

        private BoundarySnapshot(IReadOnlyList<Point2d[]> rings)
        {
            _rings = rings;
        }

        public static readonly BoundarySnapshot Empty = new BoundarySnapshot(Array.Empty<Point2d[]>());

        public int Count => _rings.Count;

        public bool IsEmpty => _rings.Count == 0;

        // Captures the selected polylines. Polylines carrying arc segments are rejected: the
        // containment test below is a straight-segment ray cast, so an arc boundary would give
        // quietly wrong answers near the bulged edges.
        public static BoundarySnapshot Capture(
            IEnumerable<ObjectId> boundaryIds,
            Transaction tx,
            out List<string> rejected)
        {
            rejected = new List<string>();
            List<Point2d[]> rings = new List<Point2d[]>();

            foreach (ObjectId id in boundaryIds)
            {
                if (id.IsNull || id.IsErased)
                {
                    continue;
                }

                if (tx.GetObject(id, OpenMode.ForRead) is not Polyline polyline)
                {
                    continue;
                }

                if (HasArcs(polyline))
                {
                    rejected.Add($"{polyline.Handle} (buede segmenter)");
                    continue;
                }

                if (polyline.NumberOfVertices < 3)
                {
                    rejected.Add($"{polyline.Handle} (færre end 3 knudepunkter)");
                    continue;
                }

                Point2d[] ring = new Point2d[polyline.NumberOfVertices];
                for (int i = 0; i < polyline.NumberOfVertices; i++)
                {
                    ring[i] = polyline.GetPoint2dAt(i);
                }

                rings.Add(ring);
            }

            return new BoundarySnapshot(rings);
        }

        public bool Contains(Point3d point)
        {
            if (_rings.Count == 0)
            {
                return false;
            }

            Point2d testPoint = new Point2d(point.X, point.Y);
            foreach (Point2d[] ring in _rings)
            {
                if (ContainsPoint(ring, testPoint))
                {
                    return true;
                }
            }

            return false;
        }

        // Standard crossing-number ray cast, carried over unchanged from the original
        // IsPointInsidePolyline in MatchBBR.cs.
        private static bool ContainsPoint(Point2d[] ring, Point2d testPoint)
        {
            bool inside = false;
            int vertexCount = ring.Length;

            for (int i = 0, j = vertexCount - 1; i < vertexCount; j = i++)
            {
                Point2d current = ring[i];
                Point2d previous = ring[j];

                bool straddles = (current.Y > testPoint.Y) != (previous.Y > testPoint.Y);
                if (!straddles)
                {
                    continue;
                }

                double intersectX =
                    ((previous.X - current.X) * (testPoint.Y - current.Y) / (previous.Y - current.Y))
                    + current.X;
                if (testPoint.X < intersectX)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static bool HasArcs(Polyline polyline)
        {
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                if (Math.Abs(polyline.GetBulgeAt(i)) > 1e-9)
                {
                    return true;
                }
            }

            return false;
        }
    }

    // What one pass over the drawing found. The two rejected sets exist so an Excel row that
    // matched nothing can be told *why* — "the address is not in the drawing" and "the address is
    // in the drawing but outside your boundary" look identical in the grid otherwise, and the
    // second one is the far more common mistake.
    internal sealed class BbrGatherResult
    {
        // The blocks the comparison actually runs against.
        public List<BbrRecord> Inside { get; } = new List<BbrRecord>();

        // Addresses of BBR blocks that sit outside the selected boundary.
        public List<string> OutsideBoundary { get; } = new List<string>();

        // Addresses of in-boundary blocks dropped because their BBR Type is "Ingen".
        public List<string> SkippedByType { get; } = new List<string>();
    }

    internal static class MatchBbrGather
    {
        // Snapshots every BBR block whose insertion point falls inside the boundary.
        //
        // HashSetOfTypeWithPs already filters on the BBR property set being attached, so blocks
        // without it are never touched. That matters: reading through PropertySetManager goes via
        // GetOrAttachPropertySet, which *attaches* (writes) the set when it is missing — so a
        // careless read on an unrelated block would silently modify the drawing.
        //
        // Must be called inside an open transaction, and UpdatePropertySetDefinition must have
        // been called before that transaction was started (it refuses to run inside one).
        public static BbrGatherResult Collect(
            Database database,
            Transaction tx,
            BoundarySnapshot boundary,
            IReadOnlyCollection<string> propertyNames,
            bool skipTypeIngen)
        {
            PSetDefs.BBR definition = new PSetDefs.BBR();
            Dictionary<string, PSetDefs.Property> propertyByName = definition
                .ListOfProperties()
                .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // Always read the columns the grid shows, on top of whatever the rules ask for.
            HashSet<string> wanted = new HashSet<string>(propertyNames, StringComparer.OrdinalIgnoreCase)
            {
                MatchBbrConstants.AdresseProperty,
                MatchBbrConstants.VejnavnProperty,
                MatchBbrConstants.HusnummerProperty,
                MatchBbrConstants.DistriktProperty,
                MatchBbrConstants.TypeProperty,
            };

            propertyByName.TryGetValue(
                MatchBbrConstants.AdresseProperty,
                out PSetDefs.Property? adresseProperty);

            PropertySetManager psm = new PropertySetManager(database, PSetDefs.DefinedSets.BBR);
            BbrGatherResult result = new BbrGatherResult();

            // Property names already reported as unreadable, so each is logged once.
            HashSet<string> reportedFailures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Blocks already taken from the property-set pass, so the Object Data fallback below
            // cannot add the same building twice.
            HashSet<Handle> seen = new HashSet<Handle>();

            foreach (BlockReference block in database.HashSetOfTypeWithPs<BlockReference>(
                         tx, PSetDefs.DefinedSets.BBR))
            {
                if (!boundary.Contains(block.Position))
                {
                    // Only the address is read for these — enough to explain an unmatched Excel
                    // row, without paying for a full property read on every block in the drawing.
                    if (adresseProperty is not null)
                    {
                        string outsideAddress = ReadValue(psm, block, adresseProperty, reportedFailures).Text;
                        if (!string.IsNullOrWhiteSpace(outsideAddress))
                        {
                            result.OutsideBoundary.Add(outsideAddress);
                        }
                    }

                    continue;
                }

                Dictionary<string, BbrPropertyValue> values =
                    new Dictionary<string, BbrPropertyValue>(StringComparer.OrdinalIgnoreCase);

                foreach (string name in wanted)
                {
                    if (!propertyByName.TryGetValue(name, out PSetDefs.Property? property))
                    {
                        continue;
                    }

                    values[name] = ReadValue(psm, block, property, reportedFailures);
                }

                if (skipTypeIngen
                    && values.TryGetValue(MatchBbrConstants.TypeProperty, out BbrPropertyValue? typeValue)
                    && string.Equals(
                        typeValue.Text.Trim(),
                        MatchBbrConstants.SkippedTypeValue,
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (values.TryGetValue(MatchBbrConstants.AdresseProperty, out BbrPropertyValue? skipped)
                        && !string.IsNullOrWhiteSpace(skipped.Text))
                    {
                        result.SkippedByType.Add(skipped.Text);
                    }

                    continue;
                }

                seen.Add(block.Handle);
                result.Inside.Add(new BbrRecord(block.ObjectId, block.Handle, block.Position, values));
            }

            CollectFromObjectData(
                database, tx, boundary, wanted, propertyByName, skipTypeIngen, seen, result);

            return result;
        }

        // Legacy path: drawings whose BBR data sits in a Map3D Object Data table instead of an AEC
        // property set. Those blocks are invisible to HashSetOfTypeWithPs, so without this they
        // silently read as "not in the drawing".
        //
        // TryOpen returns null unless the drawing actually has a "BBR" OD table, so a normal
        // drawing never reaches the model-space walk below.
        private static void CollectFromObjectData(
            Database database,
            Transaction tx,
            BoundarySnapshot boundary,
            IReadOnlyCollection<string> wanted,
            IReadOnlyDictionary<string, PSetDefs.Property> propertyByName,
            bool skipTypeIngen,
            HashSet<Handle> seen,
            BbrGatherResult result)
        {
            MatchBbrObjectData? objectData = MatchBbrObjectData.TryOpen();
            if (objectData is null)
            {
                return;
            }

            int added = 0;

            foreach (BlockReference block in database.ListOfType<BlockReference>(tx))
            {
                if (seen.Contains(block.Handle) || !objectData.HasRecord(block))
                {
                    continue;
                }

                Dictionary<string, BbrPropertyValue> values =
                    objectData.ReadValues(block, wanted, propertyByName);

                if (values.Count == 0)
                {
                    continue;
                }

                if (!boundary.Contains(block.Position))
                {
                    if (values.TryGetValue(MatchBbrConstants.AdresseProperty, out BbrPropertyValue? outside)
                        && !string.IsNullOrWhiteSpace(outside.Text))
                    {
                        result.OutsideBoundary.Add(outside.Text);
                    }

                    continue;
                }

                if (skipTypeIngen
                    && values.TryGetValue(MatchBbrConstants.TypeProperty, out BbrPropertyValue? typeValue)
                    && string.Equals(
                        typeValue.Text.Trim(),
                        MatchBbrConstants.SkippedTypeValue,
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (values.TryGetValue(MatchBbrConstants.AdresseProperty, out BbrPropertyValue? skipped)
                        && !string.IsNullOrWhiteSpace(skipped.Text))
                    {
                        result.SkippedByType.Add(skipped.Text);
                    }

                    continue;
                }

                seen.Add(block.Handle);
                result.Inside.Add(new BbrRecord(block.ObjectId, block.Handle, block.Position, values));
                added++;
            }

            if (added > 0)
            {
                prdDbg(
                    $"MatchBBR: {added} blok(ke) læst fra Map3D Object Data i stedet for et "
                    + "egenskabssæt. Kør ODDataConverter for at konvertere tegningen.");
            }
        }

        // Reads one property in whichever shape its declared PsDataType calls for, keeping both a
        // display string and, where meaningful, a number so a numeric rule needs no re-parse.
        private static BbrPropertyValue ReadValue(
            PropertySetManager psm,
            Entity entity,
            PSetDefs.Property property,
            HashSet<string> reportedFailures)
        {
            try
            {
                switch (property.DataType)
                {
                    case PsDataType.Real:
                    {
                        double value = psm.ReadPropertyDouble(entity, property);

                        // Displayed to two decimals. These are physical quantities — MWh, kWh/m²,
                        // afkøling — and the stored double carries binary noise that reads as
                        // 19.059999999999999 in the grid. The unrounded value is kept in Number,
                        // so comparisons and the export still use full precision.
                        return new BbrPropertyValue(
                            value.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture),
                            value,
                            property.DataType);
                    }

                    case PsDataType.Integer:
                    {
                        int value = psm.ReadPropertyInt(entity, property);
                        return new BbrPropertyValue(
                            value.ToString(System.Globalization.CultureInfo.CurrentCulture),
                            value,
                            property.DataType);
                    }

                    case PsDataType.TrueFalse:
                    {
                        bool value = psm.ReadPropertyBool(entity, property);
                        return new BbrPropertyValue(value.ToString(), null, property.DataType);
                    }

                    default:
                    {
                        string value = psm.ReadPropertyString(entity, property) ?? string.Empty;
                        return new BbrPropertyValue(value, null, property.DataType);
                    }
                }
            }
            catch (System.Exception ex)
            {
                // A property missing from an older attached set must not abort the whole gather;
                // it shows as blank and the rule that wanted it reports "ikke sammenlignelig".
                //
                // Reported once per property, not once per block: a property missing from the
                // definition fails on every one of several thousand blocks, and that many
                // identical lines would bury the log rather than inform it.
                if (reportedFailures.Add(property.Name))
                {
                    prdDbg(
                        $"MatchBBR: BBR-egenskaben '{property.Name}' kunne ikke læses "
                        + $"(først set på {entity.Handle}). Videre uden den.");
                    prdDbg(ex);
                }

                return BbrPropertyValue.Missing;
            }
        }
    }
}
