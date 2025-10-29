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

        public RoutedGraph Route()
        {
            var g = new RoutedGraph();
            var ctx = new RouterContext(_topo);

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
        public RouterContext(Topology topo) { Topology = topo; }        
    }
}