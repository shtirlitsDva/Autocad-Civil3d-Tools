using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App.Contracts;
using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.Models;
using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.CoordinateTransformation;
using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.UI.Models;
using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.Analysis.Spatial;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.UI
{
	internal sealed class VejkantInspectorMapper : IInspectorMapper<VejkantAnalysis, IntersectionVisualizationModel>
	{
		public IntersectionVisualizationModel Map(VejkantAnalysis analysis, Line workingLine)
		{
			// Create coordinate transformation system
			var coordSystem = new DisplayCoordinateSystem(workingLine);
			
			// Transform working line to display coordinates
			var workingLineDisplay = coordSystem.GetWorkingLineDisplay();
			var displayWorkingLine = new DisplayLineModel
			{
				StartPoint = workingLineDisplay.Start,
				EndPoint = workingLineDisplay.End,
				DisplayLength = workingLine.Length
			};
			
			// Transform intersections to display coordinates (project X to working line, fix Y to working line + signed distance)
			var displayIntersections = TransformIntersections(
				analysis.GkIntersections, coordSystem, workingLine);
			
			// Calculate distance measurements
			var distanceMeasurements = CalculateDistanceMeasurements(
				analysis.GkIntersections, coordSystem, workingLine);
			
			// Generate grid
			var grid = GenerateGrid(coordSystem, workingLine);
			
			return new IntersectionVisualizationModel
			{
				WorkingLine = displayWorkingLine,
				Intersections = displayIntersections,
				DistanceMeasurements = distanceMeasurements,
				Grid = grid
			};
		}
		
		private IReadOnlyList<IntersectionDisplayModel> TransformIntersections(
			IReadOnlyList<Segment2d> intersections, 
			DisplayCoordinateSystem coordSystem, 
			Line workingLine)
		{
			var result = new List<IntersectionDisplayModel>();
			
			foreach (var intersection in intersections)
			{
				// Project world points onto the working line X in display coordinates
				var startDisplayX = coordSystem.ProjectToDisplayX(intersection.A);
				var endDisplayX = coordSystem.ProjectToDisplayX(intersection.B);
				var workingY = coordSystem.GetWorkingLineYDisplay();
				// Signed distances in world units -> convert to display delta Y
				var dStartWorld = coordSystem.SignedDistanceToWorkingLine(intersection.A);
				var dEndWorld = coordSystem.SignedDistanceToWorkingLine(intersection.B);
				var dStartDisp = coordSystem.WorldDistanceToDisplayDeltaY(Math.Abs(dStartWorld));
				var dEndDisp = coordSystem.WorldDistanceToDisplayDeltaY(Math.Abs(dEndWorld));
				var startDisplay = new Point2d(startDisplayX, workingY - Math.Sign(dStartWorld) * dStartDisp);
				var endDisplay = new Point2d(endDisplayX, workingY - Math.Sign(dEndWorld) * dEndDisp);
				
				// Calculate offset distance from the working line
				var midPoint = new Point2d(
					(intersection.A.X + intersection.B.X) / 2,
					(intersection.A.Y + intersection.B.Y) / 2
				);
				var offsetDistance = coordSystem.CalculatePerpendicularDistance(midPoint);
				var isAbove = coordSystem.IsPointAboveLine(midPoint);
				
				// Create display model
				var displayIntersection = new IntersectionDisplayModel
				{
					StartPoint = startDisplay,
					EndPoint = endDisplay,
					OffsetDistance = offsetDistance,
					IsAboveWorkingLine = isAbove,
					DistanceLabel = $"{offsetDistance:F2}m",
					LabelPosition = new Point2d(
						(startDisplay.X + endDisplay.X) / 2,
						Math.Min(startDisplay.Y, endDisplay.Y) - 14)
				};
				
				result.Add(displayIntersection);
			}
			
			return result;
		}
		
		private IReadOnlyList<DistanceMeasurementModel> CalculateDistanceMeasurements(
			IReadOnlyList<Segment2d> intersections, 
			DisplayCoordinateSystem coordSystem, 
			Line workingLine)
		{
			var result = new List<DistanceMeasurementModel>();
			
			foreach (var intersection in intersections)
			{
				// Calculate distance from intersection start point to working line
				var startDistance = Math.Abs(coordSystem.SignedDistanceToWorkingLine(intersection.A));
				var startDisplayX = coordSystem.ProjectToDisplayX(intersection.A);
				var workingY = coordSystem.GetWorkingLineYDisplay();
				var startDisplay = new Point2d(startDisplayX, workingY - coordSystem.WorldDistanceToDisplayDeltaY(startDistance));
				var startWorkingLinePoint = new Point2d(startDisplayX, workingY);
				
				var startMeasurement = new DistanceMeasurementModel
				{
					FromPoint = startDisplay,
					ToPoint = startWorkingLinePoint,
					Distance = startDistance,
					DistanceLabel = $"{startDistance:F2}m",
					LabelPosition = new Point2d(startDisplay.X + 4, (startDisplay.Y + startWorkingLinePoint.Y) / 2)
				};
				result.Add(startMeasurement);
				
				// Calculate distance from intersection end point to working line
				var endDistance = Math.Abs(coordSystem.SignedDistanceToWorkingLine(intersection.B));
				var endDisplayX = coordSystem.ProjectToDisplayX(intersection.B);
				var endDisplay = new Point2d(endDisplayX, workingY - coordSystem.WorldDistanceToDisplayDeltaY(endDistance));
				var endWorkingLinePoint = new Point2d(endDisplayX, workingY);
				
				var endMeasurement = new DistanceMeasurementModel
				{
					FromPoint = endDisplay,
					ToPoint = endWorkingLinePoint,
					Distance = endDistance,
					DistanceLabel = $"{endDistance:F2}m",
					LabelPosition = new Point2d(endDisplay.X + 4, (endDisplay.Y + endWorkingLinePoint.Y) / 2)
				};
				result.Add(endMeasurement);
			}
			
			return result;
		}
		
		private GridModel GenerateGrid(DisplayCoordinateSystem coordSystem, Line workingLine)
		{
			var (displayWidth, displayHeight) = coordSystem.GetDisplayDimensions();
			
			// Generate vertical grid lines
			var verticalLines = new List<GridLineModel>();
			var gridSpacing = 50.0; // 50 pixels between grid lines
			
			for (double x = 0; x <= displayWidth; x += gridSpacing)
			{
				var isMajor = x % (gridSpacing * 2) == 0; // Major lines every 100 pixels
				var verticalLine = new GridLineModel
				{
					StartPoint = new Point2d(x, 0),
					EndPoint = new Point2d(x, displayHeight),
					IsMajor = isMajor
				};
				verticalLines.Add(verticalLine);
			}
			
			// Generate horizontal grid lines
			var horizontalLines = new List<GridLineModel>();
			for (double y = 0; y <= displayHeight; y += gridSpacing)
			{
				var isMajor = y % (gridSpacing * 2) == 0; // Major lines every 100 pixels
				var horizontalLine = new GridLineModel
				{
					StartPoint = new Point2d(0, y),
					EndPoint = new Point2d(displayWidth, y),
					IsMajor = isMajor
				};
				horizontalLines.Add(horizontalLine);
			}
			
			// Generate grid labels
			var labels = new List<GridLabelModel>();
			
			// X-axis labels (bottom)
			for (double x = gridSpacing; x < displayWidth; x += gridSpacing * 2)
			{
				var label = new GridLabelModel
				{
					Position = new Point2d(x, displayHeight - 20),
					Text = $"{(x - 40):F0}", // Adjust for margin
					IsHorizontal = true
				};
				labels.Add(label);
			}
			
			// Y-axis labels (left)
			for (double y = gridSpacing; y < displayHeight; y += gridSpacing * 2)
			{
				var label = new GridLabelModel
				{
					Position = new Point2d(20, y), // Adjust for margin
					Text = $"{(displayHeight - y - 40):F0}", // Invert Y coordinate
					IsHorizontal = false
				};
				labels.Add(label);
			}
			
			return new GridModel
			{
				VerticalLines = verticalLines,
				HorizontalLines = horizontalLines,
				Labels = labels
			};
		}
	}
}


