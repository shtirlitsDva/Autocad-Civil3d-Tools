using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace IntersectUtilities.LongitudinalProfiles
{
    public interface ILer3dManager
    {
        void Load(string path);
        void Dispose(bool disposing);
        HashSet<Entity> GetIntersectingEntities(Alignment al);
        string GetHandle(Entity ent);
        bool IsPointWithinPolygon(Entity ent, Point3d p3d);
        Entity GetEntityByHandle(string handle);
    }
}
