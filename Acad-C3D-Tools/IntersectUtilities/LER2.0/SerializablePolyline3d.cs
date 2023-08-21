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
        public Dictionary<string, object> Properties { get; set; }

        public SerializablePolyline3d(Polyline3d pl3d)
        {
            Transaction tx = pl3d.Database.TransactionManager.TopTransaction;

            var vs = pl3d.GetVertices(tx);
            string linestring = string.Join(", ", vs.Select(x => $"{x.Position.X} {x.Position.Y} {x.Position.Z}"));

            Properties = new Dictionary<string, object>
            {
                { "Layer", pl3d.Layer },
                { "Geometry", $"LINESTRING({linestring})" }
            };

            var moreData = PropertySetManager.DumpAllProperties(pl3d);

            Properties.Add("Properties", moreData);
        }
    }
}
