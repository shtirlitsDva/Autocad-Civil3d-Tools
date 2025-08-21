using System;
using System.Collections.Generic;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App.Contracts;
using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.Models;
using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Rendering;
using IntersectUtilities.UtilsCommon;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App
{
	internal sealed class VejkantAnalyzer : IAnalyzer<Line, VejkantAnalysis>
	{
		private readonly Database _dimDb;
		private readonly Database _gkDb;
		private readonly Database _targetDb;
		private readonly VejkantOffsetSettings _settings;

		public VejkantAnalyzer(Database dimDb, Database gkDb, Database targetDb, VejkantOffsetSettings settings)
		{
			_dimDb = dimDb;
			_gkDb = gkDb;
			_targetDb = targetDb;
			_settings = settings;
		}

		public VejkantAnalysis Analyze(Line workingLine)
		{
			Polyline? offset = null;

			using (var tr = _dimDb.TransactionManager.StartOpenCloseTransaction())
			{
				var dim = _dimDb.ListOfType<Polyline>(tr, discardFrozen: false);
				offset = VejKantAnalyzerOffsetter.CreateOffsetPolyline(workingLine, dim, _settings);
			}

			var segs = new List<PipelineSegmentDomain>();
			if (offset != null)
			{
				for (int i = 0; i < offset.NumberOfVertices - 1; i++)
				{
					var p0 = offset.GetPoint3dAt(i);
					var p1 = offset.GetPoint3dAt(i + 1);
					var bulge = offset.GetBulgeAt(i);

					if (Math.Abs(bulge) < 1e-9)
					{
						segs.Add(new PipelineLineSegmentDomain
						{
							Start = p0,
							End = p1,
							Width = 1.0,
							ColorIndex = 2
						});
					}
					else
					{
						var theta = 4.0 * Math.Atan(bulge);
						var chord = p0.DistanceTo(p1);
						var radius = chord / (2.0 * Math.Sin(Math.Abs(theta) / 2.0));

						var mid = new Point3d((p0.X + p1.X) / 2.0, (p0.Y + p1.Y) / 2.0, 0);
						var vec = (p1 - p0);
						var perp = new Vector3d(-vec.Y, vec.X, 0.0).GetNormal();
						var sagitta = radius * (1 - Math.Cos(Math.Abs(theta) / 2.0));
						var ccw = bulge > 0;
						var center = mid + (ccw ? perp : -perp) * sagitta;

						var v0 = (p0 - center);
						var v1 = (p1 - center);
						var a0 = Math.Atan2(v0.Y, v0.X);
						var a1 = Math.Atan2(v1.Y, v1.X);

						segs.Add(new PipelineArcSegmentDomain
						{
							Center = center,
							Radius = radius,
							StartAngle = a0,
							EndAngle = a1,
							IsCCW = ccw,
							Width = 1.0,
							ColorIndex = 2
						});
					}
				}
			}

			return new VejkantAnalysis
			{
				Segments = segs,
				GkIntersections = Array.Empty<SegmentHit>(),
				Length = workingLine.StartPoint.DistanceTo(workingLine.EndPoint),
				ChosenSideLabel = null
			};
		}

		public void Commit(VejkantAnalysis result)
		{
			var pl = new Polyline();
			int vi = 0;
			Point3d? current = null;

			foreach (var seg in result.Segments)
			{
				if (seg is PipelineLineSegmentDomain ls)
				{
					if (current == null)
					{
						pl.AddVertexAt(vi++, new Point2d(ls.Start.X, ls.Start.Y), 0, 0, 0);
						current = ls.Start;
					}
					pl.AddVertexAt(vi++, new Point2d(ls.End.X, ls.End.Y), 0, 0, 0);
					current = ls.End;
				}
				else if (seg is PipelineArcSegmentDomain arc)
				{
					var s = new Point3d(
						arc.Center.X + arc.Radius * Math.Cos(arc.StartAngle),
						arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngle), 0);
					var e = new Point3d(
						arc.Center.X + arc.Radius * Math.Cos(arc.EndAngle),
						arc.Center.Y + arc.Radius * Math.Sin(arc.EndAngle), 0);

					if (current == null)
					{
						pl.AddVertexAt(vi++, new Point2d(s.X, s.Y), 0, 0, 0);
						current = s;
					}

					var sweep = arc.EndAngle - arc.StartAngle;
					if (sweep < 0 && arc.IsCCW) sweep += 2 * Math.PI;
					if (sweep > 0 && !arc.IsCCW) sweep -= 2 * Math.PI;

					var bulge = Math.Tan(sweep / 4.0);
					pl.AddVertexAt(vi++, new Point2d(e.X, e.Y), bulge, 0, 0);
					current = e;
				}
			}

			using (var tr = _targetDb.TransactionManager.StartTransaction())
			{
				pl.AddEntityToDbModelSpace(_targetDb);
				tr.Commit();
			}
		}
	}
}



