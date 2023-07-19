using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace IntersectUtilities
{
    internal class Pipeline
    {
        public Alignment Alignment { get; set; }
        public Entity[] Entities { get; set; }
        public Pipeline(Alignment alignment, IEnumerable<Entity> entities)
        {
            Alignment = alignment;
            


        }
    }
}
