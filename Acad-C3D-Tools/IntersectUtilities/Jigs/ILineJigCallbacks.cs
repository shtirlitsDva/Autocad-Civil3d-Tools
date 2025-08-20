using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.DatabaseServices;

namespace IntersectUtilities.Jigs
{
	public interface ILineJigCallbacks
	{
		void OnSamplerPointChanged(Line line);
		void OnKeyword(string keyword);
		void OnCommit(Line line);
		void OnCancelLevel1();
		void OnCancelLevel2();
	}
}



