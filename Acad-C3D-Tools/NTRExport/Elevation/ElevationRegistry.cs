using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal sealed class ElevationRegistry
    {
        // Element -> (Port -> Z)
        private readonly Dictionary<ElementBase, Dictionary<TPort, double>> _map =
            new(new ReferenceEqualityComparer<ElementBase>());
        // Element -> (Port -> slope m/m to be applied when traversing out of this port)
        private readonly Dictionary<ElementBase, Dictionary<TPort, double>> _slopeHints =
            new(new ReferenceEqualityComparer<ElementBase>());

        public void Record(ElementBase element, TPort port, double z)
        {
            if (!_map.TryGetValue(element, out var portMap))
            {
                portMap = new Dictionary<TPort, double>(new ReferenceEqualityComparer<TPort>());
                _map[element] = portMap;
            }
            portMap[port] = z;
        }

        public bool TryGetEndpointZ(ElementBase element, out IReadOnlyDictionary<TPort, double>? endpointZ)
        {
            if (_map.TryGetValue(element, out var ports))
            {
                endpointZ = ports;
                return true;
            }
            endpointZ = null;
            return false;
        }

        public void RecordSlopeHint(ElementBase element, TPort port, double slope)
        {
            if (!_slopeHints.TryGetValue(element, out var portMap))
            {
                portMap = new Dictionary<TPort, double>(new ReferenceEqualityComparer<TPort>());
                _slopeHints[element] = portMap;
            }
            portMap[port] = slope;
        }

        public bool TryGetSlopeHint(ElementBase element, TPort port, out double slope)
        {
            slope = 0.0;
            if (_slopeHints.TryGetValue(element, out var map) && map.TryGetValue(port, out var s))
            {
                slope = s;
                return true;
            }
            return false;
        }

        private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
            where T : class
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}



