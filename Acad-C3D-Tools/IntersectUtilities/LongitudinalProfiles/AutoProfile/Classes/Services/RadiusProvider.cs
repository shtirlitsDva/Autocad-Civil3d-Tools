using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    /// <summary>
    /// Simple radius provider using callback function
    /// </summary>
    internal class RadiusProvider : IFilletRadiusProvider
    {
        private readonly Func<Point2d, double> _radiusCallback;

        public RadiusProvider(Func<Point2d, double> radiusCallback)
        {
            _radiusCallback = radiusCallback ?? throw new ArgumentNullException(nameof(radiusCallback));
        }

        public double GetRadiusAtPoint(Point2d point)
        {
            return _radiusCallback(point);
        }
    }
}
