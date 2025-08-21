using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App.Contracts;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Rendering
{
	internal sealed class TransientPreviewRenderer : IRenderer
	{
		private readonly TransientManager _tm = TransientManager.CurrentTransientManager;
		private readonly List<Entity> _current = new List<Entity>();

		public void Show(Scene scene)
		{
			Clear();

			var visitor = new AcadTransientVisitor(_tm, _current);
			foreach (var item in scene.Items)
				item.Accept(visitor);
		}

		public void Clear()
		{
			foreach (var e in _current)
			{
				try { _tm.EraseTransient(e, new IntegerCollection()); } catch { }
				e.Dispose();
			}
			_current.Clear();
		}

		private sealed class AcadTransientVisitor : IRenderVisitor
		{
			private readonly TransientManager _tm;
			private readonly List<Entity> _bucket;

			public AcadTransientVisitor(TransientManager tm, List<Entity> bucket)
			{
				_tm = tm;
				_bucket = bucket;
			}

			public void Visit(Line2D line)
			{
				var ln = new Line(new Point3d(line.A.X, line.A.Y, 0), new Point3d(line.B.X, line.B.Y, 0));
				if (line.Style.ColorIndex.HasValue) ln.Color = Color.FromColorIndex(ColorMethod.ByAci, line.Style.ColorIndex.Value);
				_bucket.Add(ln);
				_tm.AddTransient(ln, TransientDrawingMode.DirectShortTerm, 128, new IntegerCollection());
			}

			public void Visit(Arc2D arc)
			{
				var a = new Arc(new Point3d(arc.Center.X, arc.Center.Y, 0), arc.Radius, arc.StartAngle, arc.EndAngle);
				if (arc.Style.ColorIndex.HasValue) a.Color = Color.FromColorIndex(ColorMethod.ByAci, arc.Style.ColorIndex.Value);
				_bucket.Add(a);
				_tm.AddTransient(a, TransientDrawingMode.DirectShortTerm, 128, new IntegerCollection());
			}

			public void Visit(PolyPath2D path)
			{
				var pl = new Autodesk.AutoCAD.DatabaseServices.Polyline();
				for (int i = 0; i < path.Vertices.Count; i++)
				{
					var p = path.Vertices[i];
					var bulge = (i < path.Bulges.Count) ? path.Bulges[i] : 0.0;
					pl.AddVertexAt(i, p, bulge, 0, 0);
				}
				if (path.Style.ColorIndex.HasValue) pl.Color = Color.FromColorIndex(ColorMethod.ByAci, path.Style.ColorIndex.Value);
				_bucket.Add(pl);
				_tm.AddTransient(pl, TransientDrawingMode.DirectShortTerm, 128, new IntegerCollection());
			}
		}
	}
}



