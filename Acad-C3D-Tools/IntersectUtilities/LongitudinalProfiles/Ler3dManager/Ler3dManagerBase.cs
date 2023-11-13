using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles
{
    public abstract class Ler3dManagerBase : ILer3dManager
    {
        private Dictionary<string, Database> storage = new Dictionary<string, Database>();
        private Dictionary<string, MPolygon> areas = new Dictionary<string, MPolygon>();
        public abstract void Load(string path);
    }
}