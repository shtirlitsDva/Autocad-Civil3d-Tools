using Autodesk.AutoCAD.DatabaseServices;
using ADB = Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.Jigs;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App
{
	internal sealed class JigCallbacksAdapter : ILineJigCallbacks
	{
		private readonly JigController _controller;
		private bool _cancelArmed;

		public JigCallbacksAdapter(JigController controller)
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
				// Do NOT hide the palette here; keep it visible between Escape1 and re-prompt
			}
			else
			{
				// If already armed, treat as second escape explicitly
				_controller.OnCancelLevel2();
				_cancelArmed = false;
			}
		}

		public void OnCancelLevel2()
		{
			_controller.OnCancelLevel2();
		}
	}
}



