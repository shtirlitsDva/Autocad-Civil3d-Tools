using Autodesk.AutoCAD.DatabaseServices;
using ADB = Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.Jigs;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App
{
	internal sealed class JigCallbacksAdapter<TAnalysis, TInspectorModel, TContext> : ILineJigCallbacks
	{
		private readonly JigController<TAnalysis, TInspectorModel, TContext> _controller;
		private bool _cancelArmed;

		public JigCallbacksAdapter(JigController<TAnalysis, TInspectorModel, TContext> controller)
		{
			_controller = controller;
			_cancelArmed = false;
		}

		public void OnSamplerPointChanged(Line line)
		{
			_controller.OnSamplerPointChanged(line);
		}

		public void OnKeyword(string keyword)
		{
		}

		public void OnCommit(Line line)
		{
			_controller.OnCommit(line);
			_cancelArmed = false;
		}

		public void OnCancelLevel1()
		{
			if (!_cancelArmed)
			{
				_controller.OnCancelLevel1();
				_cancelArmed = true;
			}
			else
			{
				OnCancelLevel2();
			}
		}

		public void OnCancelLevel2()
		{
			_controller.OnCancelLevel2();
		}
	}
}



