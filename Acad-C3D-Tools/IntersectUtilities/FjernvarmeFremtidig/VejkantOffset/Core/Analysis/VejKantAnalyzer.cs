using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using Dreambuild.AutoCAD;

using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.Analysis.Spatial;
using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.Models;
using IntersectUtilities.UtilsCommon;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Caching;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset
{
	internal class VejKantAnalyzer
	{
        internal static void AnalyzeIntersectingVejkants(
			Line workingLine,
			SpatialGridCache cache)
        {
            
        }

        // Query the cache to find which polylines intersect the given working line.
        public IEnumerable<ObjectId> GetIntersectingPolylines(SpatialGridCache cache,
			Line workingLine, double eps = 1e-7)
        {
            var a = new Point2d(workingLine.StartPoint.X, workingLine.StartPoint.Y);
            var b = new Point2d(workingLine.EndPoint.X, workingLine.EndPoint.Y);
            var probe = new Core.Analysis.Spatial.Line2d(a, b);

            // Query candidates by AABB first
            var candidates = cache.Query(probe.Bounds);

            var hits = new HashSet<ObjectId>();
            foreach (var seg in candidates)
            {
                if (Geometry2D.SegmentIntersects(probe, seg, eps, out _))
                    hits.Add(seg.PolylineId);
            }
            return hits;
        }

        internal static void CreateOffsetSegments(
			Line gkLine, 
			IEnumerable<Polyline> dimplines, 
			VejkantOffsetSettings settings,
            List<PipelineSegment> segs)
		{
			var A = gkLine.StartPoint.To2d();
			var B = gkLine.EndPoint.To2d();
			var wDir = A.GetVectorTo(B);
			var wLen = wDir.Length;
			if (wLen <= 1e-6) return;

			var wU = wDir.GetNormal();
			var leftNormal = new Vector2d(-wU.Y, wU.X);
			double cosTol = Math.Cos(settings.MaxAngleDeg.ToRad());

			var candidates = new List<SegmentHit>();

			foreach (var pl in dimplines)
			{
				if (pl == null || pl.IsErased) continue;

				for (int i = 0; i < pl.NumberOfVertices - 1; i++)
				{
					if (pl.GetSegmentType(i) != SegmentType.Line) continue;

					var seg = pl.GetLineSegment2dAt(i);

					var sA = seg.StartPoint;
					var sB = seg.EndPoint;
					var sDir = sA.GetVectorTo(sB);
					if (sDir.Length <= 1e-6) continue;

					var sU = sDir.GetNormal();

					//prallelism check
					double cos = Math.Abs(sU.DotProduct(wU));
					if (cos < cosTol) continue;

					//Offset checks
					double d0 = Math.Abs(wU.Cross2d(sA - A));
					double d1 = Math.Abs(wU.Cross2d(sB - A));
					double dMax = Math.Max(d0, d1);
					if (dMax > settings.Width) continue;

					double t0 = wU.DotProduct(sA - A);
					double t1 = wU.DotProduct(sB - A);

					double segMin = Math.Min(t0, t1);
					double segMax = Math.Max(t0, t1);
					double ov0 = Math.Max(0.0, segMin);
					double ov1 = Math.Min(wLen, segMax);
					if (ov1 <= ov0) continue;

					//signed side
					var mid = seg.MidPoint;
					double tm = wU.DotProduct(mid - A);
					var foot = A + wU * tm;
					double signedOffset = wU.Cross2d(mid - foot);

					//sortkey from start
					double sortKey = Math.Clamp(segMin, 0.0, wLen);

					candidates.Add(new SegmentHit
					{
						Polyline = pl,
						SegmentIndex = i,
						A = sA,
						B = sB,
						S0 = Math.Clamp(t0, 0.0, wLen),
						S1 = Math.Clamp(t1, 0.0, wLen),
						Overlap0 = ov0,
						Overlap1 = ov1,
						SignedOffset = signedOffset,
						SortKey = sortKey,
						PipeSystem = GetPipeSystem(pl),
						PipeDim = GetPipeDN(pl),
						PipeSeries = settings.Series,
                        Offset = GetOffset(
							pl, settings.Series, settings.Width, settings.OffsetSupplement)
					});
				}
			}

			if (candidates.Count == 0) return;

			//choose side
			double weightLeft = 0, weightRight = 0;
			foreach (var c in candidates)
			{
				double w = Math.Max(1e-9, c.Overlap1 - c.Overlap0);
				if (c.SignedOffset >= 0) weightLeft += w;
				else weightRight += w;
			}
			int sideSign = (weightLeft >= weightRight) ? 1 : -1;
			var sideNormal = sideSign >= 0 ? leftNormal : -leftNormal;

			var ordered = candidates
				.Where(c => Math.Sign(c.SignedOffset) == sideSign || Math.Abs(c.SignedOffset) < 1e-12)
				.OrderBy(c => c.SortKey)
				.ThenBy(c => c.Overlap0)
				.ToArray();

			var squashed = new List<SegmentHit>();

			for (int i = 0; i < ordered.Length; i++)
			{
				var first = ordered[i];
				int j = i;

				// merge adjacent items while LayerName stays the same
				while (j + 1 < ordered.Length && ordered[j + 1].LayerName == first.LayerName)
				{
					j++;
				}

				var last = ordered[j];

				squashed.Add(new SegmentHit
				{
					Polyline = first.Polyline,
					SegmentIndex = first.SegmentIndex,
					A = first.A,
					B = last.B,
					S0 = first.S0,
					S1 = last.S1,
					Overlap0 = first.Overlap0,
					Overlap1 = last.Overlap1,
					SignedOffset = first.SignedOffset,
					SortKey = first.SortKey,
					PipeSystem = first.PipeSystem,
					PipeDim = first.PipeDim,
					PipeSeries = first.PipeSeries,
					Offset = first.Offset
				});

				// Skip over the merged group
				i = j;
			}

			if (squashed.Count < 1) return;

			// 3D helpers along and perpendicular to gkLine on the chosen side
			var wU3 = (gkLine.EndPoint - gkLine.StartPoint).GetNormal();
			Point3d WL(double t) => gkLine.StartPoint + wU3 * t;
			var dir3 = (gkLine.EndPoint - gkLine.StartPoint).GetPerpendicularVector().GetNormal() * (sideSign >= 0 ? 1.0 : -1.0);

			// build PipelineSegments with a single line primitive for each squashed group
			foreach (var g in squashed)
			{
				var ps = new PipelineSegment(g.PipeSystem, g.PipeDim, g.PipeSeries);
				var prim = new PipelineLinePrimitiveDomain(ps)
				{
					Start = WL(g.Overlap0) + dir3 * g.Offset,
					End = WL(g.Overlap1) + dir3 * g.Offset
				};
				ps.Primitives.Add(prim);
				segs.Add(ps);
			}
		}
	}
}


