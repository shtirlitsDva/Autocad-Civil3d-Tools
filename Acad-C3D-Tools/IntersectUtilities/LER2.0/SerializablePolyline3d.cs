using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities
{
    public class SerializablePolyline3d
    {
        //public Dictionary<string, Dictionary<string, object>> Properties { get; set; }
        public Dictionary<string, object> Properties { get; set; }
        public string Type { get; set; }
        public string Geometry { get; set; }
        public string Layer { get; set; }
        public string Handle { get; set; }
        public int GroupNumber { get; set; }
        private readonly Polyline3d _pl3d;
        public Polyline3d GetPolyline3d()
        { 
            if (_pl3d != null) return _pl3d;

            //Try to load the Polyline3d
            Entity ent = UtilsCommon.Utils.GetEntityFromLocalDbByHandleString(Handle);
            if (ent == null)
                throw new Exception(
                    $"Enity {Handle} cannot be loaded from local DB! Is your data stale?");
            if (ent is Polyline3d pl3d) return pl3d;
            else throw new Exception(
                $"Entity {Handle} does not exist!");
        }
        public Polyline3d GetPolyline3d(Handle handle)
        {
            //Try to load the Polyline3d
            Entity ent = UtilsCommon.Utils.GetEntityFromLocalDbByHandle(handle);
            if (ent == null)
                throw new Exception(
                    $"Enity {Handle} cannot be loaded from local DB! Is your data stale?");
            if (ent is Polyline3d pl3d) return pl3d;
            else throw new Exception(
                $"Entity {Handle} does not exist!");
        }
        public SerializablePolyline3d() {}
        public SerializablePolyline3d(Polyline3d pl3d, int groupNumber)
        {
            Transaction tx = pl3d.Database.TransactionManager.TopTransaction;
            _pl3d = pl3d;

            var vs = pl3d.GetVertices(tx);
            string linestring = string.Join(", ", vs.Select(x => $"{x.Position.X} {x.Position.Y} {x.Position.Z}"));

            this.Type = typeof(Polyline3d).Name;
            Layer = pl3d.Layer;
            Handle = pl3d.Handle.ToString();
            Geometry = $"LINESTRING({linestring})";
            GroupNumber = groupNumber;

            Properties = PropertySetManager.DumpAllProperties(pl3d);

        }
        public SerializablePolyline3d(Handle handle, int groupNumber) : 
            this((Polyline3d)UtilsCommon.Utils.GetEntityFromLocalDbByHandle(handle), groupNumber) {}
    }
}
