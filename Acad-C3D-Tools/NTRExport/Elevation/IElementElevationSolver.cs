using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal interface IElementElevationSolver
    {
        // Given entry port and entryZ, solve per-element elevation and record in registry.
        // Returns exits (port, exitZ) for connected continuation.
        List<(TPort exitPort, double exitZ)> Solve(ElementBase element, TPort entryPort, double entryZ, ElevationRegistry registry);
    }
}



