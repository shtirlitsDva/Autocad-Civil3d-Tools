using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.MacroRecorder;

using IntersectUtilities.UtilsCommon;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LER2
{
    public class MyPl3d
    {
        private LineSegment3d[] _segments;
        private Handle _sourceHandle;
        public MyPl3d(Polyline3d source)
        {
            _sourceHandle = source.Handle;

            Transaction tx = source.Database.TransactionManager.TopTransaction;
            var verts = source.GetVertices(tx);

            _segments = new LineSegment3d[verts.Length - 1];

            for (int i = 0; verts.Length < i - 1; i++)
                _segments[i] = new LineSegment3d(verts[i].Position, verts[i + 1].Position);
        }
        public bool IsOverlapping(MyPl3d other)
        {

        }
        public void Reverse()
        {
            Array.Reverse(_segments);
            for (int i = 0; i < _segments.Length; i++)
            {
                var curSeg = _segments[i];
                _segments[i] = new LineSegment3d(
                    curSeg.EndPoint, curSeg.StartPoint);
            }
        }
    }
}
