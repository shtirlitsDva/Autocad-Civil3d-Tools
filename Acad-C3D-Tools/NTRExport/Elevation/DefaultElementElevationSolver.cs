using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal sealed class DefaultElementElevationSolver : IElementElevationSolver
    {
        public List<(TPort exitPort, double exitZ)> Solve(ElementBase element, TPort entryPort, double entryZ, ElevationRegistry registry)
        {
            // Record entry Z
            registry.Record(element, entryPort, entryZ);

            var exits = new List<(TPort exitPort, double exitZ)>();
            // Pass-through to other ports with the same Z
            foreach (var p in element.Ports)
            {
                if (ReferenceEquals(p, entryPort)) continue;
                registry.Record(element, p, entryZ);
                exits.Add((p, entryZ));
            }
            return exits;
        }
    }
}



