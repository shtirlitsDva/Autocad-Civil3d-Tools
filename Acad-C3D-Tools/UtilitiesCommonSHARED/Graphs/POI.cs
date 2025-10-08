using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.UtilsCommon.Graphs
{
    public class POI
    {
        public Entity Owner { get; }
        public Point2d Point { get; }
        public EndType EndType { get; }
        private PropertySetManager _psm { get; }
        private PSetDefs.DriGraph _driGraph = new();
        public POI(Entity owner, Point2d point, EndType endType, PropertySetManager psm)
        { Owner = owner; Point = point; EndType = endType; _psm = psm; }
        public bool IsSameOwner(POI toCompare) => Owner.Id == toCompare.Owner.Id;
        internal void AddReference(POI connectedEntity)
        {
            string value = _psm.ReadPropertyString(Owner, _driGraph.ConnectedEntities);
            value += $"{(int)EndType}:{(int)connectedEntity.EndType}:{connectedEntity.Owner.Handle};";
            _psm.WritePropertyString(Owner, _driGraph.ConnectedEntities, value);
        }
    }
}
