using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App.Contracts
{
	public sealed class SamplerSnapshot
	{
		public Point3d Start { get; init; }
		public Point3d End { get; init; }
		public double Length { get; init; }
		public string? ChosenSideLabel { get; init; }
		// Extend with groups/offsets for UI
	}
}



