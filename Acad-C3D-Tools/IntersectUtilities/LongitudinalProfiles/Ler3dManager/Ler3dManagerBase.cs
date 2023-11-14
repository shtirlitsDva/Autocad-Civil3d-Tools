using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace IntersectUtilities.LongitudinalProfiles
{
    public abstract class Ler3dManagerBase : ILer3dManager, IDisposable
    {
        private bool _disposed = false;
        public abstract void Load(string path);
        protected abstract bool IsLoadValid();
        public virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;
        }
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        public abstract HashSet<Entity> GetIntersectingEntities(Alignment al);
        public abstract string GetHandle(Entity ent);
        public abstract bool IsPointWithinPolygon(Entity ent, Point3d p3d);
    }
}