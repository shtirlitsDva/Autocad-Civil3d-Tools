using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.GraphFeatures
{
    internal class PipeSegmentEdge : Edge<JunctionNode>
    {
        public AnalysisFeature PipeSegment { get; }
        public int Level { get; set; } // Level in the network hierarchy

        public PipeSegmentEdge(
            JunctionNode source, 
            JunctionNode target, 
            AnalysisFeature pipeSegment) : base(source, target)
        {
            PipeSegment = pipeSegment;
            Level = -1; // Default value, will be set during level calculation
        }
    }
}
