using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.GraphWrite
{
    internal class Edge
    {
        internal Handle Id1 { get; }
        internal EndType EndType1 { get; }
        internal Handle Id2 { get; }
        internal EndType EndType2 { get; }
        internal string Label { get; set; }
        internal Edge(Handle id1, Handle id2)
        {
            Id1 = id1; Id2 = id2;
        }
        internal Edge(
            Handle id1, EndType endType1,
            Handle id2, EndType endType2)
        {
            Id1 = id1; Id2 = id2;
            EndType1 = endType1; EndType2 = endType2;
        }
        internal Edge(Handle id1, Handle id2, string label)
        {
            Id1 = id1; Id2 = id2; Label = label;
        }
        internal string ToString(string edgeSymbol)
        {
            if (Label.IsNoE()) return $"\"{Id1}\" {edgeSymbol} \"{Id2}\"";
            else return $"\"{Id1}\" {edgeSymbol} \"{Id2}\"{Label}";
        }
    }
}
