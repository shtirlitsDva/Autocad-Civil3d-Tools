using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.Models
{
	public abstract class PipelineSegmentDomain { }

	public sealed class PipelineLineSegmentDomain : PipelineSegmentDomain
	{
		public Point3d Start { get; init; }
		public Point3d End { get; init; }
		public short? ColorIndex { get; init; }
		public double Width { get; init; }
	}

	public sealed class PipelineArcSegmentDomain : PipelineSegmentDomain
	{
		public Point3d Center { get; init; }
		public double Radius { get; init; }
		public double StartAngle { get; init; }
		public double EndAngle { get; init; }
		public bool IsCCW { get; init; }
		public short? ColorIndex { get; init; }
		public double Width { get; init; }
	}

	public sealed class VejkantAnalysis
	{
		public double Length { get; init; }
		public string? ChosenSideLabel { get; init; }
		public IReadOnlyList<PipelineSegmentDomain> Segments { get; init; } = Array.Empty<PipelineSegmentDomain>();
		public IReadOnlyList<SegmentHit> GkIntersections { get; init; } = Array.Empty<SegmentHit>();
	}
}


