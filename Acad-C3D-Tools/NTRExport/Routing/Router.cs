using NTRExport.TopologyModel;

namespace NTRExport.Routing
{
    internal sealed class Router
    {
        private readonly Topology _topo;

        public Router(Topology topo)
        {
            _topo = topo;
        }

        public RoutedGraph Route(IElevationProvider elevation)
        {
            var g = new RoutedGraph();
            var ctx = new RouterContext(_topo, elevation);

            foreach (var e in _topo.Elements)
            {
                e.Route(g, _topo, ctx);
            }

            return g;
        }
    }

    internal sealed class RouterContext
    {        
        public Topology Topology { get; }
        public IElevationProvider Elevation { get; }
        public RouterContext(Topology topo, IElevationProvider elevation) { Topology = topo; Elevation = elevation; }        
    }
}