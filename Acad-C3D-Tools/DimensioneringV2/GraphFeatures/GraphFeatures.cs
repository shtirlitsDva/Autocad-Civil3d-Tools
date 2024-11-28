using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.DimensioneringV2.GraphFeatures
{
    internal class GraphFeatures
    {
        private UndirectedGraph<FeatureNode, Edge<FeatureNode>> compressedGraph;
        public GraphFeatures()
        {
            this.compressedGraph = new();
        }
    }
}
