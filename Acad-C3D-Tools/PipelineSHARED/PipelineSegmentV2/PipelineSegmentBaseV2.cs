using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace IntersectUtilities.PipelineNetworkSystem
{
    internal abstract class SegmentBaseV2 : IPipelineSegmentV2
    {
        internal SegmentBaseV2(IPipelineV2 owner, PropertySetHelper psh)
        {
            Owner = owner;
            _psh = psh;
        }

        public IPipelineV2 Owner { get; private set; }
        public abstract double MidStation { get; }
        public abstract IEnumerable<Handle> Handles { get; }
        public IEnumerable<Handle> ExternalHandles {
            get => _ents.SelectMany(x => GetOtherHandles(ReadConnection(x)))
                .Where(x => _ents.All(y => y.Handle != x)).Distinct();
        }
        

        public bool IsConnectedTo(IPipelineSegmentV2 other)
        {
            return other.ExternalHandles.Any(x => Handles.Any(y => x == y));
        }
        
        protected abstract List<Entity> _ents { get; }
        protected PropertySetHelper _psh;
        private static readonly Regex _conRgx = new Regex(
            @"(?<OwnEndType>\d):(?<ConEndType>\d):(?<Handle>\w*)");
        private string ReadConnection(Entity ent) =>
            _psh.Graph.ReadPropertyString(ent, _psh.GraphDef.ConnectedEntities);
        private IEnumerable<Handle> GetOtherHandles(string connectionString)
        {
            string[] conns = connectionString.Split(';');
            foreach (var item in conns)
                if (_conRgx.IsMatch(item))
                    yield return new Handle(
                        Convert.ToInt64(_conRgx.Match(item).Groups["Handle"].Value, 16));
        }
    }
}
