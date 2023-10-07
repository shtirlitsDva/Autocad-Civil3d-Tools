using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.GraphClasses
{
    public class EntWrap : IComparable<EntWrap>
    {
        public Entity Entity { get; set; }
        public EntWrap(Entity ent) => Entity = ent;
        public int CompareTo(EntWrap other) =>
            this.Entity.Handle.Value.CompareTo(other.Entity.Handle.Value);
    }
}
