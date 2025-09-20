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
        public double Elevation { get; }
        public string ColorStation { get; set; }
        public string ColorElevation { get; set; }
        public string ToLabelHtml(int v);
    }
    internal abstract class ConnectionBase : IConnection
    {
        protected ConnectionDirection _direction;
        protected double _station;
        protected double _elevation;
        protected string _colorStation = "#FFFFFF";
        protected string _colorElevation = "#FFFFFF";
        public ConnectionBase(ConnectionDirection direction, double station, double elevation)
        {
            _direction = direction;
            _station = station;
            _elevation = elevation;
        }

        public double Station => _station;
        public double Elevation => _elevation;
        public string ColorStation { get => _colorStation; set => _colorStation = value; }
        public string ColorElevation { get => _colorElevation; set => _colorElevation = value; }

        public abstract string ToLabelHtml(int index);
    }
    internal class ConnectionUnknown : ConnectionBase
    {
        public ConnectionUnknown(ConnectionDirection direction, double station, double elevation) :
            base(direction, station, elevation) { }

        public override string ToLabelHtml(int index)
        {
            switch (_direction)
            {
                case ConnectionDirection.Out:
                    return $@"
<TR>
    <!-- Port on the STATION cell. -->
    <TD BGCOLOR=""{_colorStation}"" ALIGN=""RIGHT"">S: {_station:0.00}</TD>
    <!-- Separate cell for ELEVATION. -->
    <TD PORT=""p{index:000}"" BGCOLOR=""{_colorElevation}"" ALIGN=""RIGHT"">K: NA</TD>
</TR>";
                case ConnectionDirection.In:
                    return $@"
<TR>
    <TD PORT=""p{index:000}"" BGCOLOR=""{_colorElevation}"" ALIGN=""RIGHT"">K: NA</TD>
    <TD BGCOLOR=""{_colorStation}"" ALIGN=""RIGHT"">S: {_station:0.00}</TD>
</TR>";
            }
            return string.Empty;
        }

        public string ToLabelRecord(int index)
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

        public override string ToLabelHtml(int index)
        {
            switch (_direction)
            {
                case ConnectionDirection.Out:
                    return $@"
<TR>
    <!-- Port on the STATION cell. -->
    <TD BGCOLOR=""{_colorStation}"" ALIGN=""RIGHT"">S: {_station:0.00}</TD>
    <!-- Separate cell for ELEVATION. -->
    <TD PORT=""p{index:000}"" BGCOLOR=""{_colorElevation}"" ALIGN=""RIGHT"">K: {_elevation:0.00}</TD>
</TR>";
                case ConnectionDirection.In:
                    return $@"
<TR>
    <TD PORT=""p{index:000}"" BGCOLOR=""{_colorElevation}"" ALIGN=""RIGHT"">K: {_elevation:0.00}</TD>
    <TD BGCOLOR=""{_colorStation}"" ALIGN=""RIGHT"">S: {_station:0.00}</TD>
</TR>";
            }
            return string.Empty;
        }

        public string ToLabelRecord(int index)
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
