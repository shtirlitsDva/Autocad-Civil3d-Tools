using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.GraphWrite
{
    internal class Subgraph
    {
        private Database Database { get; }
        private System.Data.DataTable Table { get; }
        internal string Alignment { get; }
        internal bool isEntryPoint { get; set; } = false;
        internal HashSet<Handle> Nodes { get; } = new HashSet<Handle>();
        internal Subgraph(Database database, System.Data.DataTable table, string alignment)
        { Alignment = alignment; Database = database; Table = table; }
        internal string WriteSubgraph(int subgraphIndex, bool subGraphsOn = true)
        {
            StringBuilder sb = new StringBuilder();
            if (subGraphsOn) sb.AppendLine($"subgraph cluster_{subgraphIndex} {{");
            foreach (Handle handle in Nodes)
            {
                //Gather information about element
                DBObject obj = handle.Go<DBObject>(Database);
                if (obj == null) continue;
                //Write the reference to the node
                sb.Append($"\"{handle}\" ");

                switch (obj)
                {
                    case Polyline pline:
                        int dn = GetPipeDN(pline);
                        string system = GetPipeType(pline).ToString();
                        var psys = GetPipeSystem(pline).ToString();
                        sb.AppendLine($"[label=\"{{{handle}|Rør L{pline.Length.ToString("0.##")}}}|{psys} {system}\\n{dn}\"" +
                            $" URL=\"ahk://ACCOMSelectByHandle/{handle}\"];");
                        break;
                    case BlockReference br:
                        string dn1 = br.ReadDynamicCsvProperty(DynamicProperty.DN1);
                        string dn2 = br.ReadDynamicCsvProperty(DynamicProperty.DN2);
                        string dnStr = dn2 == "0" ? dn1 : dn1 + "/" + dn2;
                        system = ComponentSchedule.ReadComponentSystem(br, Table);
                        string type = ComponentSchedule.ReadDynamicCsvProperty(br, DynamicProperty.Type);
                        string color = "";
                        if (type == "Reduktion") color = " color=\"red\"";
                        sb.AppendLine($"[label=\"{{{handle}|{type}}}|{system}\\n{dnStr}\"{color}" +
                            $" URL=\"ahk://ACCOMSelectByHandle/{handle}\"];");

                        break;
                    default:
                        continue;
                }
            }
            //sb.AppendLine(string.Join(" ", Nodes) + ";");
            if (subGraphsOn)
            {
                sb.AppendLine($"label = \"{Alignment}\";");
                sb.AppendLine("color=red;");
                if (isEntryPoint) sb.AppendLine("penwidth=2.5;");
                sb.AppendLine("}");
            }
            return sb.ToString();
        }
    }
}
