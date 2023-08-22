using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities
{
    internal class SerializablePolyline3d
    {
        public Dictionary<string, Dictionary<string, object>> Properties { get; set; }
        public string Type { get; set; }
        public string Geometry { get; set; }
        public string Layer { get; set; }

        public SerializablePolyline3d(Polyline3d pl3d)
        {
            Transaction tx = pl3d.Database.TransactionManager.TopTransaction;

            var vs = pl3d.GetVertices(tx);
            string linestring = string.Join(", ", vs.Select(x => $"{x.Position.X} {x.Position.Y} {x.Position.Z}"));

            this.Type = typeof(Polyline3d).Name;
            Layer = pl3d.Layer;
            Geometry = $"LINESTRING({linestring})";

            Properties = PropertySetManager.DumpAllProperties(pl3d);

        }
    }
}
