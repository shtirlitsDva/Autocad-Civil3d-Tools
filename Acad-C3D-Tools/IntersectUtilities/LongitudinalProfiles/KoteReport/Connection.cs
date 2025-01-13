using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles.KoteReport
{
    internal interface IConnection
    {
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

        public abstract string ToLabel();
    }
    internal class ConnectionUnknown : ConnectionBase
    {
        public ConnectionUnknown(ConnectionDirection direction, double station, double elevation) : 
            base(direction, station, elevation)
        {
        }

        public override string ToLabel()
        {
            switch (_direction)
            {
                case ConnectionDirection.Out:
                    return $"|{{S: {_station.ToString("0.##")}|<p{}> NA";
                case ConnectionDirection.In:
                    break;
            }
        }
    }
    internal class ConnectionKnownElevation : ConnectionBase
    {
        public ConnectionKnownElevation(ConnectionDirection direction, double station, double elevation) :
            base(direction, station, elevation)
        {
        }
    }
    internal enum ConnectionDirection
    {
        Out,
        In
    }
}
