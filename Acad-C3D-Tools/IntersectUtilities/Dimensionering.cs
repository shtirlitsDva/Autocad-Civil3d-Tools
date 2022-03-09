using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

namespace IntersectUtilities
{
    internal class Connection
    {
        internal double Dist;
        internal Oid ParentId;
        internal Oid ChildId;
        internal Point3d NearestPoint;

        internal Connection(double dist, Oid parentId, Oid childId, Point3d nearestPoint)
        {
            Dist = dist;
            ParentId = parentId;
            ChildId = childId;
            NearestPoint = nearestPoint;
        }
    }
}
