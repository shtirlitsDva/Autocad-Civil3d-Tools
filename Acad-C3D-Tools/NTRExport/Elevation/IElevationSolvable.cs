using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal interface IElevationSolvable
    {
        // Solve elevation inside the element and push Z through exit ports.
        // Return the list of exits (port, exitZ) to continue traversal.
        List<(TPort exitPort, double exitZ)> SolveElevation(TPort entryPort, double entryZ, SolverContext ctx);
    }
}


