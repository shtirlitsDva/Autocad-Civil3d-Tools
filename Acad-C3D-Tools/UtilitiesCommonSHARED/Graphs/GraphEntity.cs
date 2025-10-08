using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.UtilsCommon.Graphs
{
    public class GraphEntity
    {
        public Entity Owner { get; }
        public Handle OwnerHandle { get; }
        public Con[] Cons { get; }
        private PSetDefs.DriGraph _driGraph = new();
        public GraphEntity(
            Entity entity, 
            PropertySetManager psm)
        {
            Owner = entity;
            OwnerHandle = Owner.Handle;
            string conString = psm.ReadPropertyString(entity, _driGraph.ConnectedEntities);
            if (conString.IsNoE()) throw new System.Exception(
                $"Constring is EMPTY! for entity: {Owner.Handle}.\nCheck connectivity.");
            Cons = Con.ParseConString(conString);
        }
        public int LargestDn()
        {
            switch (Owner)
            {
                case Polyline pline:
                    return GetPipeDN(pline);
                case BlockReference br:
                    return Convert.ToInt32(
                        br.ReadDynamicCsvProperty(
                            DynamicProperty.DN1));
            }
            return 0;
        }        
    }
}
