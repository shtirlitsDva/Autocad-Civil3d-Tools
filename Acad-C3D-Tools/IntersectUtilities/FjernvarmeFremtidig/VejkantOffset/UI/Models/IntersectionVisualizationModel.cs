using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System.Collections.Generic;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.UI.Models
{
    /// <summary>
    /// Model for displaying intersection data in the WPF visualization
    /// </summary>
    public sealed class IntersectionVisualizationModel
    {
        public DisplayLineModel WorkingLine { get; init; }
        public IReadOnlyList<IntersectionDisplayModel> Intersections { get; init; }
        public IReadOnlyList<DistanceMeasurementModel> DistanceMeasurements { get; init; }
        public GridModel Grid { get; init; }
    }
    
    /// <summary>
    /// Represents the working line in display coordinates
    /// </summary>
    public sealed class DisplayLineModel
    {
        public Point2d StartPoint { get; init; }
        public Point2d EndPoint { get; init; }
        public double DisplayLength { get; init; }
    }
    
    /// <summary>
    /// Represents an intersection segment in display coordinates
    /// </summary>
    public sealed class IntersectionDisplayModel
    {
        public Point2d StartPoint { get; init; }
        public Point2d EndPoint { get; init; }
        public double OffsetDistance { get; init; }
        public bool IsAboveWorkingLine { get; init; }
        public string DistanceLabel { get; init; }
        public Point2d LabelPosition { get; init; }
    }
    
    /// <summary>
    /// Represents distance measurements from intersection points to the working line
    /// </summary>
    public sealed class DistanceMeasurementModel
    {
        public Point2d FromPoint { get; init; }
        public Point2d ToPoint { get; init; }
        public double Distance { get; init; }
        public string DistanceLabel { get; init; }
        public Point2d LabelPosition { get; init; }
    }
    
    /// <summary>
    /// Represents the grid for the visualization
    /// </summary>
    public sealed class GridModel
    {
        public IReadOnlyList<GridLineModel> VerticalLines { get; init; }
        public IReadOnlyList<GridLineModel> HorizontalLines { get; init; }
        public IReadOnlyList<GridLabelModel> Labels { get; init; }
    }
    
    /// <summary>
    /// Represents a single grid line
    /// </summary>
    public sealed class GridLineModel
    {
        public Point2d StartPoint { get; init; }
        public Point2d EndPoint { get; init; }
        public bool IsMajor { get; init; }
    }
    
    /// <summary>
    /// Represents a grid label
    /// </summary>
    public sealed class GridLabelModel
    {
        public Point2d Position { get; init; }
        public string Text { get; init; }
        public bool IsHorizontal { get; init; }
    }
}
