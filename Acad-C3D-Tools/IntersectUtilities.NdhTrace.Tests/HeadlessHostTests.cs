using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.NdhTrace.Tests;

/// <summary>
/// WHAT THIS TEST PROJECT RESTS ON, stated as tests so it fails loudly rather
/// than as a hundred type-load errors. acdbmgd.dll is a mixed-mode C++/CLI
/// assembly and is widely believed to be unloadable outside acad.exe; it is
/// not. Given the install directory on the probing and native search paths
/// (<see cref="AcadAssemblyHost"/>), its in-memory geometry - Point2d,
/// Vector2d, Polyline and every measurement NdhRouteBuilder makes on a
/// centreline - works in a plain test process with no application, no document
/// and no Database.
/// </summary>
public class HeadlessHostTests
{
    [Fact]
    public void GeometryValuesWorkWithoutAutoCad()
    {
        Assert.Equal(5.0, new Point2d(3.0, 4.0).GetDistanceTo(new Point2d(0.0, 0.0)), 9);
        Assert.Equal(
            System.Math.PI / 2.0,
            new Vector2d(1.0, 0.0).GetAngleTo(new Vector2d(0.0, 1.0)), 9);
    }

    [Fact]
    public void APolylineMeasuresItselfWithoutAutoCad()
    {
        using Polyline pl = Legacy.Centreline((0, 0), (10, 0), (10, 10));

        Assert.Equal(20.0, pl.Length, 6);
        Assert.Equal(10.0, pl.GetPointAtDist(10.0).X, 6);
        Assert.Equal(
            14.0,
            pl.GetDistAtPoint(pl.GetClosestPointTo(new Point3d(12.0, 4.0, 0.0), false)), 6);
    }
}
