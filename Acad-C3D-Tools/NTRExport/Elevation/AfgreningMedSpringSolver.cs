using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using NTRExport.TopologyModel;
using static IntersectUtilities.UtilsCommon.Utils;

namespace NTRExport.Elevation
{
    internal sealed class AfgreningMedSpringSolver : IElementElevationSolver
    {
        public List<(TPort exitPort, double exitZ)> Solve(ElementBase element, TPort entryPort, double entryZ, SolverContext ctx)
        {
            var exits = new List<(TPort exitPort, double exitZ)>();

            // Record entry
            ctx.Registry.Record(element, entryPort, entryZ);

            if (element is not TFitting tf)
            {
                // Pass-through
                foreach (var p in element.Ports)
                {
                    if (ReferenceEquals(p, entryPort)) continue;
                    ctx.Registry.Record(element, p, entryZ);
                    exits.Add((p, entryZ));
                }
                return exits;
            }

            // Determine orientation (Up/Down) from property set (must exist)
            var (isUp, hasDir) = TryReadSpringDirection(element.Source);
            if (!hasDir)
                throw new System.Exception($"AfgreningMedSpring {element.Source}: missing SpringDirection (Up/Down) property.");

            // Determine deltaZ from size table (by branch DN preferred, fallback to main)
            int dnBranch = ctx.Topology.InferBranchDn(tf);
            int dnMain = ctx.Topology.InferMainDn(tf);
            double dz = LookupSpringDeltaZ(dnBranch > 0 ? dnBranch : dnMain);
            if (dz <= 0.0)
                throw new System.Exception($"AfgreningMedSpring {element.Source}: no Spring ΔZ found for DN {dnBranch}/{dnMain}.");

            double signedDz = isUp ? dz : -dz;

            // Ports grouped by role
            var mains = tf.Ports.Where(p => p.Role == PortRole.Main).ToArray();
            var branch = tf.Ports.FirstOrDefault(p => p.Role == PortRole.Branch);

            if (mains.Length < 2 || branch == null)
            {
                // Fallback: treat as pass-through
                foreach (var p in element.Ports)
                {
                    if (ReferenceEquals(p, entryPort)) continue;
                    ctx.Registry.Record(element, p, entryZ);
                    exits.Add((p, entryZ));
                }
                return exits;
            }

            bool entryIsMain = mains.Contains(entryPort);

            if (entryIsMain)
            {
                // Both mains at entry Z
                foreach (var m in mains)
                {
                    if (ReferenceEquals(m, entryPort)) continue;
                    ctx.Registry.Record(element, m, entryZ);
                    exits.Add((m, entryZ));
                }
                // Branch at stepped Z
                double zBranch = entryZ + signedDz;
                ctx.Registry.Record(element, branch, zBranch);
                exits.Add((branch, zBranch));
            }
            else // entry via branch
            {
                // Infer main Z from branch
                double zMain = entryZ - signedDz;
                foreach (var m in mains)
                {
                    ctx.Registry.Record(element, m, zMain);
                    exits.Add((m, zMain));
                }
                // Other ports on branch (if any) pass-through; in tee there is only one branch
            }

            return exits;
        }

        private static (bool isUp, bool ok) TryReadSpringDirection(IntersectUtilities.Handle source)
        {
            // Placeholder: read a property set value "SpringDirection" with "Up"/"Down"
            try
            {
                var db = Application.DocumentManager.MdiActiveDocument.Database;
                var br = source.Go<BlockReference>(db);
                if (br == null) return (true, false);
                // Prefer property sets; fallback to dynamic csv if available
                // Example (to be aligned with your property set):
                // var psm = new PropertySetManager(db, "NtrData");
                // var dir = psm.ReadPropertyString(br, "SpringDirection");
                var dir = br.ReadDynamicCsvProperty(IntersectUtilities.UtilsCommon.Enums.DynamicProperty.Retning);
                if (string.IsNullOrWhiteSpace(dir)) return (true, false);
                dir = dir.Trim();
                if (dir.Equals("Up", System.StringComparison.OrdinalIgnoreCase) ||
                    dir.Equals("UP", System.StringComparison.OrdinalIgnoreCase))
                    return (true, true);
                if (dir.Equals("Down", System.StringComparison.OrdinalIgnoreCase) ||
                    dir.Equals("DOWN", System.StringComparison.OrdinalIgnoreCase))
                    return (false, true);
                return (true, false);
            }
            catch
            {
                return (true, false);
            }
        }

        private static double LookupSpringDeltaZ(int dn)
        {
            // TODO: hook actual catalog/table. For now, error by returning <= 0 to trigger exception up the stack.
            return 0.0;
        }
    }
}


