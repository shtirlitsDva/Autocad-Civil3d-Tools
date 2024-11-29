using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DimensioneringV2.GraphFeatures;

using QuikGraph;

namespace DimensioneringV2.Services
{
    internal interface IDataService
    {
        event EventHandler DataUpdated;
        IEnumerable<FeatureNode> Features { get; set; }
        IEnumerable<UndirectedGraph<FeatureNode, Edge<FeatureNode>>> Graphs { get; set; }
        void UpdateData(IEnumerable<FeatureNode> features, IEnumerable<UndirectedGraph<FeatureNode, Edge<FeatureNode>>> graphs);
    }
}
