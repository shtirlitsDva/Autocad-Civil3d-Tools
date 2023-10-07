using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.GraphClasses
{
    public class POI
    {
        public Entity Source { get; }
        public Entity Target { get; private set; }
        public Point2d Point { get; }
        public EndType EndType { get; }
        public POI(Entity owner, Point2d point, EndType endType)
        { Source = owner; Point = point; EndType = endType; Target = null; }
        public bool IsSameSource(POI toCompare) => Source.Handle == toCompare.Source.Handle;
        internal void AddReference(POI connectedEntity) => Target = connectedEntity.Source;
    }
}
