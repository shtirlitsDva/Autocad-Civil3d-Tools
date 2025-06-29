using NetTopologySuite.Geometries;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DimensioneringV2.Serialization
{
    public class LineStringDto
    {
        public double[][]? Coordinates { get; set; }
        public LineStringDto() { }
        public LineStringDto(LineString lineString)
        {
            if (lineString == null) throw new ArgumentNullException(nameof(lineString));
            Coordinates = lineString.Coordinates
                .Select(c => new[] { c.X, c.Y }).ToArray();
        }
        public LineString? ToLineString(JsonSerializerOptions? options = null)
        {
            var geometry = new LineString(
                Coordinates?.Select(c => new Coordinate(c[0], c[1])).ToArray());
            return geometry;
        }
    }
}
