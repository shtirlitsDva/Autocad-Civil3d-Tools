using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal sealed class VerticalElbowSolver : IElementElevationSolver
    {
        public List<(TPort exitPort, double exitZ)> Solve(ElementBase element, TPort entryPort, double entryZ, SolverContext ctx)
        {
            var exits = new List<(TPort exitPort, double exitZ)>();

            // Record entry
            ctx.Registry.Record(element, entryPort, entryZ);

            // Identify exit ports (all others)
            var others = element.Ports.Where(p => !ReferenceEquals(p, entryPort)).ToArray();
            if (others.Length == 0) return exits;

            // If not vertical, pass-through
            if (!IsVerticalElbow(element.Source, out var slopeMag, out var isUp))
            {
                foreach (var p in others)
                {
                    ctx.Registry.Record(element, p, entryZ);
                    exits.Add((p, entryZ));
                }
                return exits;
            }

            // For each exit, set same Z at elbow, but push a slope hint outward
            var signedSlope = isUp ? slopeMag : -slopeMag;
            foreach (var p in others)
            {
                ctx.Registry.Record(element, p, entryZ);
                ctx.Registry.RecordSlopeHint(element, p, signedSlope);
                exits.Add((p, entryZ));
            }
            return exits;
        }

        private static bool IsVerticalElbow(IntersectUtilities.Handle source, out double slopeMag, out bool isUp)
        {
            slopeMag = 0.0;
            isUp = true;
            try
            {
                var db = Application.DocumentManager.MdiActiveDocument.Database;
                var br = source.Go<BlockReference>(db);
                if (br == null) return false;

                // Detection heuristic:
                // Prefer an explicit property "ElbowPlane" == "Vertical" and optional "AngleDeg"
                var plane = br.ReadDynamicCsvProperty(IntersectUtilities.UtilsCommon.Enums.DynamicProperty.Plan);
                if (!string.IsNullOrWhiteSpace(plane) &&
                    plane.Trim().Equals("Vertical", System.StringComparison.OrdinalIgnoreCase))
                {
                    var angStr = br.ReadDynamicCsvProperty(IntersectUtilities.UtilsCommon.Enums.DynamicProperty.Vinkel);
                    if (!double.TryParse(angStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var angDeg))
                        angDeg = 45.0; // default to 45 if unspecified
                    var angRad = angDeg * Math.PI / 180.0;
                    slopeMag = Math.Tan(angRad);

                    // Optional Up/Down
                    var dir = br.ReadDynamicCsvProperty(IntersectUtilities.UtilsCommon.Enums.DynamicProperty.Retning);
                    if (!string.IsNullOrWhiteSpace(dir))
                    {
                        if (dir.Equals("Down", System.StringComparison.OrdinalIgnoreCase)) isUp = false;
                    }
                    return true;
                }

                // Otherwise treat as planar elbow
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}


