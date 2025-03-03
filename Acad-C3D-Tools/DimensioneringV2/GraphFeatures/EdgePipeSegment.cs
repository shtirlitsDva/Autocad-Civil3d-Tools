﻿using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.GraphFeatures
{
    internal class EdgePipeSegment : Edge<NodeJunction>
    {
        public AnalysisFeature PipeSegment { get; }
        public int Level { get; set; } // Level in the network hierarchy

        public EdgePipeSegment(
            NodeJunction source, 
            NodeJunction target, 
            AnalysisFeature pipeSegment) : base(source, target)
        {
            PipeSegment = pipeSegment;
            Level = -1; // Default value, will be set during level calculation
        }
    }
}
