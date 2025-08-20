using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App.Contracts
{
	public sealed class PreviewModel
	{
		public Line? WorkingLine { get; init; }
		public Polyline? OffsetPreview { get; init; }
		// Extend with helper primitives if needed
	}
}



