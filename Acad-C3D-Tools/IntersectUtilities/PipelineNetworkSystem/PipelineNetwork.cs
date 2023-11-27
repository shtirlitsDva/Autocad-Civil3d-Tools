using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.PipelineNetworkSystem
{
    public class PipelineNetwork
    {
        private Database db;


        public void CreatePipeNetwork(Database db)
        {
            this.db = db;


        }
    }
}
