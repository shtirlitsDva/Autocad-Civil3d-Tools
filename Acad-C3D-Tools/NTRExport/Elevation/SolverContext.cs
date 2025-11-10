using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal sealed class SolverContext
    {
        public Topology Topology { get; }
        public ElevationRegistry Registry { get; }
        public SolverContext(Topology topology, ElevationRegistry registry)
        {
            Topology = topology;
            Registry = registry;
        }
    }
}


