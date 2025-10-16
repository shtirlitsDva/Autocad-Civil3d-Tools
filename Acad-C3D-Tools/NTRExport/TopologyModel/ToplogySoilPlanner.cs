using NTRExport.SoilModel;

namespace NTRExport.TopologyModel
{
    internal sealed class TopologySoilPlanner
    {
        private readonly Topology _topo;
        private readonly double _reachM;
        private readonly SoilProfile _defaultSoil;
        private readonly SoilProfile _cushionSoil;
        private const double Tol = 1e-6;

        public TopologySoilPlanner(Topology topo, double reachM, SoilProfile defaultSoil, SoilProfile cushionSoil)
        { _topo = topo; _reachM = reachM; _defaultSoil = defaultSoil; _cushionSoil = cushionSoil; }

        public void Apply()
        {
            // Clear any previous spans
            foreach (var p in _topo.Elements.OfType<TPipe>()) p.CushionSpans.Clear();

            var anchors = CollectAnchors();

            foreach (var node in anchors)
            {
                foreach (var pipe in IncidentPipes(node))
                {
                    // From the node, find which end (A/B) aligns with this node
                    var fromA = pipe.A.Node == node;
                    var s0 = fromA ? 0.0 : pipe.Length;
                    Walk(pipe, fromA, s0, _reachM);
                }
            }
        }

        private IEnumerable<TNode> CollectAnchors()
        {
            var set = new HashSet<TNode>();
            foreach (var f in _topo.Elements.OfType<TFitting>())
            {
                switch (f.Kind)
                {
                    case IntersectUtilities.UtilsCommon.Enums.PipelineElementType.Kedelrørsbøjning:
                    case IntersectUtilities.UtilsCommon.Enums.PipelineElementType.PræisoleretBøjning90gr:
                    case IntersectUtilities.UtilsCommon.Enums.PipelineElementType.Bøjning45gr:
                    case IntersectUtilities.UtilsCommon.Enums.PipelineElementType.Bøjning30gr:
                    case IntersectUtilities.UtilsCommon.Enums.PipelineElementType.Bøjning15gr:
                    case IntersectUtilities.UtilsCommon.Enums.PipelineElementType.PræisoleretBøjningVariabel:
                    case IntersectUtilities.UtilsCommon.Enums.PipelineElementType.Buerør:
                    case IntersectUtilities.UtilsCommon.Enums.PipelineElementType.Svejsetee:
                    case IntersectUtilities.UtilsCommon.Enums.PipelineElementType.PreskoblingTee:
                    case IntersectUtilities.UtilsCommon.Enums.PipelineElementType.Muffetee:
                    case IntersectUtilities.UtilsCommon.Enums.PipelineElementType.LigeAfgrening:
                    case IntersectUtilities.UtilsCommon.Enums.PipelineElementType.AfgreningMedSpring:
                    case IntersectUtilities.UtilsCommon.Enums.PipelineElementType.AfgreningParallel:
                    case IntersectUtilities.UtilsCommon.Enums.PipelineElementType.Stikafgrening:
                        foreach (var p in f.Ports) set.Add(p.Node);
                        break;
                }
            }
            return set;
        }

        private IEnumerable<TPipe> IncidentPipes(TNode node)
        {
            foreach (var e in _topo.Elements)
                if (e is TPipe p && (p.A.Node == node || p.B.Node == node))
                    yield return p;
        }

        private void Walk(TPipe pipe, bool fromA, double sFrom, double reach)
        {
            if (reach <= Tol || pipe.Length <= Tol) return;

            var segRem = fromA ? (pipe.Length - sFrom) : sFrom;
            if (reach <= segRem + Tol)
            {
                var s1 = fromA ? (sFrom + reach) : (sFrom - reach);
                AddSpan(pipe, sFrom, s1);
                return;
            }

            // consume this pipe and continue across the node at the far end
            var sEnd = fromA ? pipe.Length : 0.0;
            AddSpan(pipe, sFrom, sEnd);
            var nextNode = fromA ? pipe.B.Node : pipe.A.Node;
            var nextPipe = NextPipe(pipe, nextNode);
            if (nextPipe is null) return;
            var leftover = Math.Max(0.0, reach - segRem);
            var nextFromA = nextPipe.A.Node == nextNode;
            Walk(nextPipe, nextFromA, nextFromA ? 0.0 : nextPipe.Length, leftover);
        }

        private static void AddSpan(TPipe p, double a, double b)
        {
            var s0 = Math.Max(0, Math.Min(a, b));
            var s1 = Math.Min(p.Length, Math.Max(a, b));
            if (s1 - s0 > 1e-6) p.CushionSpans.Add((s0, s1));
        }

        private TPipe? NextPipe(TPipe current, TNode at)
        {
            // Robust: use graph connections, not geometry
            foreach (var e in _topo.Elements)
                if (e is TPipe p && p != current && (p.A.Node == at || p.B.Node == at))
                    return p; // pick any; DH is tree-like so degree typically 2 at bends/tees branches handled by multiple starts
            return null;
        }
    }
}