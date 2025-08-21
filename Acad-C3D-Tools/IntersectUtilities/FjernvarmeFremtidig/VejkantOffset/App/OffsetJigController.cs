using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using Dreambuild.AutoCAD;

using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App.Contracts;

using System.Collections.Generic;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App
{
	internal sealed class OffsetJigController
	{
		private readonly IOffsetAnalyzer _analyzer;
		private readonly ITransientRenderer _renderer;
		private readonly IWpfVisualizer _visualizer;
		private readonly Database _dimDb;
		private readonly Database _gkDb;
		private readonly VejkantOffsetSettings _settings;

		public OffsetJigController(
			IOffsetAnalyzer analyzer,
			ITransientRenderer renderer,
			IWpfVisualizer visualizer,
			Database dimDb,
			Database gkDb,
			VejkantOffsetSettings settings)
		{
			_analyzer = analyzer;
			_renderer = renderer;
			_visualizer = visualizer;
			_dimDb = dimDb;
			_gkDb = gkDb;
			_settings = settings;
		}

		public void Run(IEnumerable<Jigs.LineJigKeyword<VejkantOffsetSettings>> keywords)
		{
			// Drive the jig loop by repeatedly calling the jig with callbacks until user exits
			while (true)
			{
				var callbacks = new JigCallbacksAdapter(this, HostApplicationServices.WorkingDatabase);
				var line = Jigs.LineJigWithKeywords<VejkantOffsetSettings>.GetLine(keywords, _settings, callbacks);
				if (line == null)
					break; // user exited
			}
		}

		private (IEnumerable<Polyline> dim, IEnumerable<Polyline> gk) GetPlines()
		{
			using var dimTr = _dimDb.TransactionManager.StartOpenCloseTransaction();
			using var gkTr = _gkDb.TransactionManager.StartOpenCloseTransaction();
			var dim = _dimDb.ListOfType<Polyline>(dimTr).ToArray();
			var gk = _gkDb.ListOfType<Polyline>(gkTr).ToArray();
			return (dim, gk);
		}

		public void OnSamplerPointChanged(Point3d start, Point3d end)
		{
			using var line = new Line(start, end);
			var (dim, gk) = GetPlines();
			var offset = _analyzer.Analyze(line, _settings, dim, gk, out var snapshot);
			_renderer.Show(new PreviewModel { WorkingLine = line, OffsetPreview = offset });
			_visualizer.Update(snapshot);
		}

		public void OnCommit(Line line, Database targetDb)
		{
			var (dim, gk) = GetPlines();
			var offset = _analyzer.Analyze(line, _settings, dim, gk, out var snapshot);
			_renderer.Clear();
			_visualizer.Update(snapshot);

			if (offset == null) return;

			// Commit the offset polyline to DB
			using (var tr = targetDb.TransactionManager.StartTransaction())
			{
				offset.AddEntityToDbModelSpace(targetDb);
				tr.Commit();
			}
		}

		public void OnCancelLevel1()
		{
			_renderer.Clear();
		}

		public void OnCancelLevel2()
		{
			_renderer.Clear();
			_visualizer.Hide();
		}
	}
}



