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


        public void CreatePipeNetwork(Database db, DataReferencesOptions dro)
        {
            if (db == null) throw new ArgumentNullException("Database is null!");
            if (dro == null) throw new ArgumentNullException("DataReferencesOptions is null!");

            this.db = db;


        }
    }
}
