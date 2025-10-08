using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using static IntersectUtilities.UtilsCommon.Utils;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.GraphClasses
{
    public class POI2
    {
        public Entity Source { get; }
        public Entity? Target { get; private set; }
        public Point2d Point { get; }
        public EndType EndType { get; }
        public POI2(Entity owner, Point2d point, EndType endType)
        { Source = owner; Point = point; EndType = endType; Target = null; }
        public bool IsSameSource(POI2 toCompare) => Source.Handle == toCompare.Source.Handle;
        internal void AddReference(POI2 connectedEntity) => Target = connectedEntity.Source;
    }
}
