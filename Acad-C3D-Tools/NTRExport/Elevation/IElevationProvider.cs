using Autodesk.AutoCAD.Geometry;

using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal interface IElevationProvider
    {
        // Return centerline Z at param t in [0,1] along plan segment a->b for the given element.
        double GetZ(ElementBase element, Point3d a, Point3d b, double t);

        // Convenience: Z at an endpoint (0 or 1)
        double GetZAtStart(ElementBase element, Point3d a, Point3d b) => GetZ(element, a, b, 0.0);
        double GetZAtEnd(ElementBase element, Point3d a, Point3d b) => GetZ(element, a, b, 1.0);
    }
}


