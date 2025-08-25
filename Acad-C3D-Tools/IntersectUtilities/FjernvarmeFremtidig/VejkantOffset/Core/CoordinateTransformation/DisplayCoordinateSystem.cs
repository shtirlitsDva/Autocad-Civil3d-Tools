using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.Analysis.Spatial;

using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.CoordinateTransformation
{
    /// <summary>
    /// Handles coordinate transformation from AutoCAD world coordinates to display coordinates
    /// </summary>
    internal sealed class DisplayCoordinateSystem
    {
        public const double DISPLAY_WIDTH = 800.0;  // Logical design width in pixels
        public const double DISPLAY_HEIGHT = 400.0; // Logical design height in pixels
        private const double MARGIN = 6.0;           // Small horizontal margin around the display area
        private const double WORKING_LINE_Y = 200.0; // Y-coordinate for the fixed horizontal working line
        
        private readonly Line _workingLine;
        private readonly double _scaleX;
        private readonly double _scaleY;
        private readonly Point2d _worldOrigin;
        private readonly Point2d _displayOrigin;
        private readonly double _workingLineLength;
        
        public DisplayCoordinateSystem(Line workingLine)
        {
            _workingLine = workingLine;
            
            // Calculate world bounds
            var worldBounds = CalculateWorldBounds(workingLine);

            var width = worldBounds.MaxPoint.X - worldBounds.MinPoint.X;
            var height = worldBounds.MaxPoint.Y - worldBounds.MinPoint.Y;
            
            // Calculate scale factors (avoid division by zero)
            _scaleX = (DISPLAY_WIDTH - 2 * MARGIN) / Math.Max(width, 0.0001);
            _scaleY = (DISPLAY_HEIGHT - 2 * MARGIN) / Math.Max(height, 0.0001);
            
            // Use the smaller scale to maintain aspect ratio
            var scale = Math.Min(_scaleX, _scaleY);
            _scaleX = scale;
            _scaleY = scale;
            
            // Calculate origins
            _worldOrigin = new Point2d(worldBounds.MinPoint.X, worldBounds.MinPoint.Y);
            _displayOrigin = new Point2d(MARGIN, MARGIN);

            _workingLineLength = workingLine.Length;
        }
        
        /// <summary>
        /// Transforms a world coordinate point to display coordinates
        /// </summary>
        public Point2d WorldToDisplay(Point2d worldPoint)
        {
            var relativeX = worldPoint.X - _worldOrigin.X;
            var relativeY = worldPoint.Y - _worldOrigin.Y;
            
            var displayX = _displayOrigin.X + (relativeX * _scaleX);
            var displayY = _displayOrigin.Y + (relativeY * _scaleY);
            
            return new Point2d(displayX, displayY);
        }
        
        /// <summary>
        /// Transforms a display coordinate point to world coordinates
        /// </summary>
        public Point2d DisplayToWorld(Point2d displayPoint)
        {
            var relativeX = displayPoint.X - _displayOrigin.X;
            var relativeY = displayPoint.Y - _displayOrigin.Y;
            
            var worldX = _worldOrigin.X + (relativeX / _scaleX);
            var worldY = _worldOrigin.Y + (relativeY / _scaleY);
            
            return new Point2d(worldX, worldY);
        }
        
        /// <summary>
        /// Gets the display coordinates for the working line
        /// </summary>
        public (Point2d Start, Point2d End) GetWorkingLineDisplay()
        {
            // Always span the full logical width with a tiny margin
            var start = new Point2d(MARGIN, WORKING_LINE_Y);
            var end = new Point2d(DISPLAY_WIDTH - MARGIN, WORKING_LINE_Y);
            
            return (start, end);
        }

        public double GetWorkingLineYDisplay() => WORKING_LINE_Y;

        /// <summary>
        /// Projects a world point onto the working line and returns the parametric t in [0,1]
        /// along the working line from start to end.
        /// </summary>
        public double ProjectToWorkingLineT(Point2d worldPoint)
        {
            var p0 = new Point2d(_workingLine.StartPoint.X, _workingLine.StartPoint.Y);
            var p1 = new Point2d(_workingLine.EndPoint.X, _workingLine.EndPoint.Y);
            var d = new Vector2d(p1.X - p0.X, p1.Y - p0.Y);
            var v = new Vector2d(worldPoint.X - p0.X, worldPoint.Y - p0.Y);
            var dDot = d.X * d.X + d.Y * d.Y;
            if (dDot <= 0) return 0.0;
            var t = (v.X * d.X + v.Y * d.Y) / dDot;
            return t;
        }

        /// <summary>
        /// Returns display X on the fixed working line corresponding to the world point's
        /// projection along the working line.
        /// </summary>
        public double ProjectToDisplayX(Point2d worldPoint)
        {
            var (ws, we) = GetWorkingLineDisplay();
            var t = ProjectToWorkingLineT(worldPoint);
            var clampedT = Math.Max(0.0, Math.Min(1.0, t));
            return ws.X + (we.X - ws.X) * clampedT;
        }

        /// <summary>
        /// Returns signed perpendicular distance (world units) from worldPoint to working line.
        /// Positive means above the line (left-hand side).
        /// </summary>
        public double SignedDistanceToWorkingLine(Point2d worldPoint)
        {
            var p0 = new Point2d(_workingLine.StartPoint.X, _workingLine.StartPoint.Y);
            var p1 = new Point2d(_workingLine.EndPoint.X, _workingLine.EndPoint.Y);
            var A = p1.Y - p0.Y;
            var B = p0.X - p1.X;
            var C = p1.X * p0.Y - p0.X * p1.Y;
            var numerator = (A * worldPoint.X + B * worldPoint.Y + C);
            var denom = Math.Sqrt(A * A + B * B);
            if (denom == 0) return 0;
            return numerator / denom;
        }

        /// <summary>
        /// Converts a world distance (meters) to a display delta in Y (pixels).
        /// </summary>
        public double WorldDistanceToDisplayDeltaY(double worldDistance)
        {
            return worldDistance * _scaleY;
        }
        
        /// <summary>
        /// Calculates the perpendicular distance from a point to the working line
        /// </summary>
        public double CalculatePerpendicularDistance(Point2d point)
        {
            var worldPoint = new Point2d(point.X, point.Y);
            var workingLineStart = new Point2d(_workingLine.StartPoint.X, _workingLine.StartPoint.Y);
            var workingLineEnd = new Point2d(_workingLine.EndPoint.X, _workingLine.EndPoint.Y);
            
            // Calculate perpendicular distance using point-to-line formula
            var A = workingLineEnd.Y - workingLineStart.Y;
            var B = workingLineStart.X - workingLineEnd.X;
            var C = workingLineEnd.X * workingLineStart.Y - workingLineStart.X * workingLineEnd.Y;
            
            var distance = Math.Abs(A * worldPoint.X + B * worldPoint.Y + C) / Math.Sqrt(A * A + B * B);
            return distance;
        }
        
        /// <summary>
        /// Determines if a point is above or below the working line
        /// </summary>
        public bool IsPointAboveLine(Point2d point)
        {
            var worldPoint = new Point2d(point.X, point.Y);
            var workingLineStart = new Point2d(_workingLine.StartPoint.X, _workingLine.StartPoint.Y);
            var workingLineEnd = new Point2d(_workingLine.EndPoint.X, _workingLine.EndPoint.Y);
            
            // Calculate which side of the line the point is on
            var result = (workingLineEnd.X - workingLineStart.X) * (worldPoint.Y - workingLineStart.Y) -
                        (workingLineEnd.Y - workingLineStart.Y) * (worldPoint.X - workingLineStart.X);
            
            return result > 0; // Positive means above, negative means below
        }
        
        /// <summary>
        /// Gets the display dimensions
        /// </summary>
        public (double Width, double Height) GetDisplayDimensions()
        {
            return (DISPLAY_WIDTH, DISPLAY_HEIGHT);
        }
        
        private Extents2d CalculateWorldBounds(Line line)
        {
            var points = new List<Point2d>
            {
                new Point2d(line.StartPoint.X, line.StartPoint.Y),
                new Point2d(line.EndPoint.X, line.EndPoint.Y)
            };
            
            var minX = points.Min(p => p.X);
            var maxX = points.Max(p => p.X);
            var minY = points.Min(p => p.Y);
            var maxY = points.Max(p => p.Y);
            
            // Add some padding
            var padding = Math.Max(maxX - minX, maxY - minY) * 0.1;
            
            return new Extents2d(
                new Point2d(minX - padding, minY - padding),
                new Point2d(maxX + padding, maxY + padding)
            );
        }
    }
}
