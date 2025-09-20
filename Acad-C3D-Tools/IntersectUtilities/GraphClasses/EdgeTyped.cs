using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.AutoCAD.DatabaseServices;
using static IntersectUtilities.UtilsCommon.Utils;

using QuikGraph;

namespace IntersectUtilities.GraphClasses
{
    public class EdgeTyped : Edge<Entity>
    {
        public EndType EndType { get; }
        public EdgeTyped(Entity source, Entity target, EndType endType) : base(source, target)
        {
            EndType = endType;
        }
    }
}
