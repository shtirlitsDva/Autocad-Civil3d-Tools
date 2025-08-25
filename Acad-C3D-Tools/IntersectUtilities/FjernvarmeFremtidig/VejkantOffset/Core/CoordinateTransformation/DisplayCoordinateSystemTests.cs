using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.CoordinateTransformation
{
    /// <summary>
    /// Simple tests for the DisplayCoordinateSystem (can be run in AutoCAD)
    /// </summary>
    internal static class DisplayCoordinateSystemTests
    {
        /// <summary>
        /// Test the coordinate transformation with a simple horizontal line
        /// </summary>
        public static void TestHorizontalLine()
        {
            // Create a test line from (0,0) to (100,0)
            var startPoint = new Point3d(0, 0, 0);
            var endPoint = new Point3d(100, 0, 0);
            var testLine = new Line(startPoint, endPoint);
            
            var coordSystem = new DisplayCoordinateSystem(testLine);
            
            // Test world to display transformation
            var worldPoint = new Point2d(50, 25);
            var displayPoint = coordSystem.WorldToDisplay(worldPoint);
            
            Console.WriteLine($"World point (50, 25) -> Display point ({displayPoint.X:F1}, {displayPoint.Y:F1})");
            
            // Test working line display
            var workingLineDisplay = coordSystem.GetWorkingLineDisplay();
            Console.WriteLine($"Working line display: ({workingLineDisplay.Start.X:F1}, {workingLineDisplay.Start.Y:F1}) to ({workingLineDisplay.End.X:F1}, {workingLineDisplay.End.Y:F1})");
            
            // Test perpendicular distance
            var distance = coordSystem.CalculatePerpendicularDistance(worldPoint);
            Console.WriteLine($"Perpendicular distance from (50, 25) to working line: {distance:F2}");
            
            // Test which side
            var isAbove = coordSystem.IsPointAboveLine(worldPoint);
            Console.WriteLine($"Point (50, 25) is {(isAbove ? "above" : "below")} the working line");
        }
        
        /// <summary>
        /// Test the coordinate transformation with a diagonal line
        /// </summary>
        public static void TestDiagonalLine()
        {
            // Create a test line from (0,0) to (100,100)
            var startPoint = new Point3d(0, 0, 0);
            var endPoint = new Point3d(100, 100, 0);
            var testLine = new Line(startPoint, endPoint);
            
            var coordSystem = new DisplayCoordinateSystem(testLine);
            
            // Test world to display transformation
            var worldPoint = new Point2d(50, 25);
            var displayPoint = coordSystem.WorldToDisplay(worldPoint);
            
            Console.WriteLine($"World point (50, 25) -> Display point ({displayPoint.X:F1}, {displayPoint.Y:F1})");
            
            // Test working line display
            var workingLineDisplay = coordSystem.GetWorkingLineDisplay();
            Console.WriteLine($"Working line display: ({workingLineDisplay.Start.X:F1}, {workingLineDisplay.Start.Y:F1}) to ({workingLineDisplay.End.X:F1}, {workingLineDisplay.End.Y:F1})");
            
            // Test perpendicular distance
            var distance = coordSystem.CalculatePerpendicularDistance(worldPoint);
            Console.WriteLine($"Perpendicular distance from (50, 25) to working line: {distance:F2}");
            
            // Test which side
            var isAbove = coordSystem.IsPointAboveLine(worldPoint);
            Console.WriteLine($"Point (50, 25) is {(isAbove ? "above" : "below")} the working line");
        }
    }
}
