using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using QuikGraph;

namespace IntersectUtilities.PipelineNetworkSystem.QuikGraphClasses
{
    internal class EdgeBase<TNode> : IEdge<TNode> where TNode : NodeBase
    {
        private TNode _source;
        private TNode _target;
        public TNode Source => _source;
        public TNode Target => _target;

        public EdgeBase([NotNull] TNode source, [NotNull] TNode target)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }
    }
}
