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
        public Polyline3d GetPolyline3d() => _pl3d;
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
    }
}
