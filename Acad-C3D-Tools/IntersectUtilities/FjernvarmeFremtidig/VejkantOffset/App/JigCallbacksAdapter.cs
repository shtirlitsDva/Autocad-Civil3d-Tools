using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.Jigs;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App
{
	internal sealed class JigCallbacksAdapter : ILineJigCallbacks
	{
		private readonly OffsetJigController _controller;
		private readonly Database _targetDb;
		private bool _cancelArmed;

		public JigCallbacksAdapter(OffsetJigController controller, Database targetDb)
		{
			_controller = controller;
			_targetDb = targetDb;
			_cancelArmed = false;
		}

		public void OnSamplerPointChanged(Point3d start, Point3d end)
		{
			_controller.OnSamplerPointChanged(start, end);
		}

		public void OnKeyword(string keyword)
		{
			// No-op for now; could route to controller if needed
		}

		public void OnCommit(Line line)
		{
			_controller.OnCommit(line, _targetDb);
			_cancelArmed = false; // reset the cancel state after a successful commit
		}

		public void OnCancelLevel1()
		{
			if (!_cancelArmed)
			{
				_controller.OnCancelLevel1();
				_cancelArmed = true; // first escape
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


