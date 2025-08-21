using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using Dreambuild.AutoCAD;

using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset
{
	internal class VejKantAnalyzerOffsetter
	{
		internal static Polyline? CreateOffsetPolyline(
			Line gkLine, IEnumerable<Polyline> dimplines, VejkantOffsetSettings settings)
		{
			var A = gkLine.StartPoint.To2d();
			var B = gkLine.EndPoint.To2d();
			var wDir = A.GetVectorTo(B);
			var wLen = wDir.Length;
			if (wLen <= 1e-6) return null;

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
						PolylineId = pl.ObjectId,
						SegmentIndex = i,
						A = sA,
						B = sB,
						S0 = Math.Clamp(t0, 0.0, wLen),
						S1 = Math.Clamp(t1, 0.0, wLen),
						Overlap0 = ov0,
						Overlap1 = ov1,
						SignedOffset = signedOffset,
						SortKey = sortKey,
						Offset = PipeScheduleV2.PipeScheduleV2.GetOffset(
							pl, settings.Series, settings.Width, settings.OffsetSupplement)
					});
				}
			}

			if (candidates.Count == 0) return null;

			//prdDbg(string.Join(", ", candidates.Select(x => x.Offset).Distinct()) + "\n");

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

				// Look-ahead: merge adjacent items while Offset stays the same
				while (j + 1 < ordered.Length && Math.Abs(ordered[j + 1].Offset - first.Offset) < 1e-9)
				{
					j++;
				}

				var last = ordered[j];

				squashed.Add(new SegmentHit
				{
					PolylineId = first.PolylineId,
					SegmentIndex = first.SegmentIndex,
					A = first.A,
					B = last.B,
					S0 = first.S0,
					S1 = last.S1,
					Overlap0 = first.Overlap0,
					Overlap1 = last.Overlap1,
					SignedOffset = first.SignedOffset,
					SortKey = first.SortKey,
					Offset = first.Offset
				});

				// Skip over the merged group
				i = j;
			}

			if (squashed.Count < 1) return null;

			var dir = (gkLine.EndPoint - gkLine.StartPoint).GetPerpendicularVector() *
				(Math.Sign(squashed.First().SignedOffset));

			Polyline npl = new Polyline();

			// unit direction along the white line (for parameter t in drawing units)
			var wU3 = (gkLine.EndPoint - gkLine.StartPoint).GetNormal();
			Point3d WL(double t) => gkLine.StartPoint + wU3 * t;

			// start at t=0 with the first group's offset
			{
				var p0 = WL(0.0) + dir * squashed[0].Offset;
				npl.AddVertexAt(npl.NumberOfVertices, p0.To2d(), 0, 0, 0);
			}

			// walk group-by-group using stations, not A/B
			for (int i = 0; i < squashed.Count; i++)
			{
				var cur = squashed[i];

				// end of current group at its end station (Overlap1)
				var pEnd = WL(cur.Overlap1) + dir * cur.Offset;
				npl.AddVertexAt(npl.NumberOfVertices, pEnd.To2d(), 0, 0, 0);

				// if there is a next group, hop at the SAME station to next offset
				if (i + 1 < squashed.Count)
				{
					var next = squashed[i + 1];
					var pHop = WL(cur.Overlap1) + dir * next.Offset;
					npl.AddVertexAt(npl.NumberOfVertices, pHop.To2d(), 0, 0, 0);
				}
			}

			return npl;
		}
	}
}


