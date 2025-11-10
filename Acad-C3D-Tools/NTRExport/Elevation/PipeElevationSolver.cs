using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal sealed class PipeElevationSolver : IElementElevationSolver
    {
        public List<(TPort exitPort, double exitZ)> Solve(ElementBase element, TPort entryPort, double entryZ, SolverContext ctx)
        {
            var exits = new List<(TPort exitPort, double exitZ)>();

            // Record entry Z
            ctx.Registry.Record(element, entryPort, entryZ);

            if (element is not TPipe pipe)
            {
                // Fallback pass-through
                foreach (var p in element.Ports)
                {
                    if (ReferenceEquals(p, entryPort)) continue;
                    ctx.Registry.Record(element, p, entryZ);
                    exits.Add((p, entryZ));
                }
                return exits;
            }

            // Determine the other end port
            var other = pipe.Ports.First(p => !ReferenceEquals(p, entryPort));

            // If a slope hint exists at the entry, apply along pipe length; else keep level
            double exitZ = entryZ;
            if (ctx.Registry.TryGetSlopeHint(element, entryPort, out var slope))
            {
                exitZ = entryZ + slope * pipe.Length;
                // Propagate slope hint to the far end for continuity until changed
                ctx.Registry.RecordSlopeHint(element, other, slope);
            }

            ctx.Registry.Record(element, other, exitZ);
            exits.Add((other, exitZ));
            return exits;
        }
    }
}


