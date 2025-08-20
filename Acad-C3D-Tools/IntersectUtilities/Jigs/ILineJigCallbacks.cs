using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.DatabaseServices;

namespace IntersectUtilities.Jigs
{
	public interface ILineJigCallbacks
	{
		void OnSamplerPointChanged(Point3d start, Point3d end);
		void OnKeyword(string keyword);
		void OnCommit(Line line);
		void OnCancelLevel1();
		void OnCancelLevel2();
	}
}



