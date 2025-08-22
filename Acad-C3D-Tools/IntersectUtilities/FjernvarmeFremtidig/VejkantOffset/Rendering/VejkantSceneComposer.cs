using System.Linq;
using System.Collections.Generic;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App.Contracts;
using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.Models;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Rendering
{
	internal sealed class VejkantSceneComposer : ISceneComposer<VejkantAnalysis>
	{
		public Scene Compose(VejkantAnalysis analysis, Line workingLine)
		{
			var items = new List<IRenderable>();

			foreach (var seg in analysis.Segments.SelectMany(x => x.Primitives))
			{
				switch (seg)
				{
					case PipelineLinePrimitiveDomain ls:
						items.Add(new Line2D
						{
							A = new Point2d(ls.Start.X, ls.Start.Y),
							B = new Point2d(ls.End.X, ls.End.Y),
							Style = new Style(ls.Width, ls.ColorIndex)
						});
						break;
					case PipelineArcPrimitiveDomain arc:
						items.Add(new Arc2D
						{
							Center = new Point2d(arc.Center.X, arc.Center.Y),
							Radius = arc.Radius,
							StartAngle = arc.StartAngle,
							EndAngle = arc.EndAngle,
							IsCCW = arc.IsCCW,
							Style = new Style(arc.Width, arc.ColorIndex)
						});
						break;
				}
			}

			return new Scene { Items = items };
		}
	}
}


