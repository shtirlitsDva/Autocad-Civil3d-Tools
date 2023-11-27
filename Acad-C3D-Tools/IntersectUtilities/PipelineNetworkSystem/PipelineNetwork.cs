using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace IntersectUtilities.PipelineNetworkSystem
{
    public class PipelineNetwork
    {
        public void CreatePipeNetwork(IEnumerable<Entity> ents, IEnumerable<Alignment> als)
        {
            Database db = ents?.FirstOrDefault()?.Database;
            if (db == null) throw new Exception(
                "Either ents collection, first element or its' database is null!");


        }
    }
}
