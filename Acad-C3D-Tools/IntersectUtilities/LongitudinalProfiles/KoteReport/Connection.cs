using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles.KoteReport
{
    internal interface IConnection
    {
        public double Station { get; }
        public string ToLabel(int v);
    }
    internal abstract class ConnectionBase : IConnection
    {
        protected ConnectionDirection _direction;
        protected double _station;
        protected double _elevation;
        public ConnectionBase(ConnectionDirection direction, double station, double elevation)
        {
            _direction = direction;
            _station = station;
            _elevation = elevation;
        }

        public double Station => _station;

        public abstract string ToLabel(int index);
    }
    internal class ConnectionUnknown : ConnectionBase
    {
        public ConnectionUnknown(ConnectionDirection direction, double station, double elevation) :
            base(direction, station, elevation) { }

        public override string ToLabel(int index)
        {
            switch (_direction)
            {
                case ConnectionDirection.Out:
                    return $"|{{S: {_station.ToString("0.00")}|<p{index.ToString("D3")}> NA}}";
                case ConnectionDirection.In:
                    return $"|{{<p{index.ToString("D3")}> NA|S: {_station.ToString("0.00")}}}";
            }
            return string.Empty;
        }
    }
    internal class ConnectionKnownElevation : ConnectionBase
    {
        public ConnectionKnownElevation(ConnectionDirection direction, double station, double elevation) :
            base(direction, station, elevation) { }

        public override string ToLabel(int index)
        {
            switch (_direction)
            {
                case ConnectionDirection.Out:
                    return $"|{{S: {_station.ToString("0.00")}|<p{index.ToString("D3")}> K: {_elevation.ToString("0.00")}}}";
                case ConnectionDirection.In:
                    return $"|{{<p{index.ToString("D3")}> K: {_elevation.ToString("0.00")}|S: {_station.ToString("0.00")}}}";
            }
            return string.Empty;
        }
    }
    internal enum ConnectionDirection
    {
        Out,
        In
    }
}
