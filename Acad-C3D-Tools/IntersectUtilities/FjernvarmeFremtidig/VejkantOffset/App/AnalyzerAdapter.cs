using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App.Contracts;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App
{
	internal sealed class AnalyzerAdapter : IOffsetAnalyzer
	{
		public Polyline? Analyze(
			Line workingLine,
			VejkantOffsetSettings settings,
			IEnumerable<Polyline> dimPlines,
			IEnumerable<Polyline> gkPlines,
			out SamplerSnapshot snapshot)
		{
			var start = workingLine.StartPoint;
			var end = workingLine.EndPoint;
			snapshot = new SamplerSnapshot
			{
				Start = start,
				End = end,
				Length = start.DistanceTo(end),
				ChosenSideLabel = null
			};

			// Analyzer currently only needs dimPlines + settings according to CreateOffsetPolyline signature.
			return VejKantAnalyzerOffsetter.CreateOffsetPolyline(workingLine, dimPlines, settings);
		}
	}
}



