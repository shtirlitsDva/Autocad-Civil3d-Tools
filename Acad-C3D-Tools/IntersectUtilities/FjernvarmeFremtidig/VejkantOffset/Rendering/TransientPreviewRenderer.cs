using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.GraphicsInterface;

using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App.Contracts;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Rendering
{
	internal sealed class TransientPreviewRenderer : ITransientRenderer
	{
		private readonly TransientManager _tm = TransientManager.CurrentTransientManager;
		private readonly List<Entity> _current = new();

		public void Show(PreviewModel model)
		{
			Clear();
			if (model.WorkingLine != null)
			{
				var ln = (Line)model.WorkingLine.Clone();
				ln.Color = Color.FromColorIndex(ColorMethod.ByAci, 1); // red
				_current.Add(ln);
				_tm.AddTransient(ln, TransientDrawingMode.DirectShortTerm, 128, new IntegerCollection());
			}
			if (model.OffsetPreview != null)
			{
				var pl = (Polyline)model.OffsetPreview.Clone();
				pl.Color = Color.FromColorIndex(ColorMethod.ByAci, 2); // yellow
				_current.Add(pl);
				_tm.AddTransient(pl, TransientDrawingMode.DirectShortTerm, 128, new IntegerCollection());
			}
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
	}
}



