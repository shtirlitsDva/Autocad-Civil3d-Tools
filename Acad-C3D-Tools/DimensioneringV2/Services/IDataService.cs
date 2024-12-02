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
        IEnumerable<IEnumerable<AnalysisFeature>> Features { get; set; }
        void UpdateFeatures(IEnumerable<IEnumerable<AnalysisFeature>> features);
    }
}
