using DimensioneringV2.GraphFeatures;

using Mapsui.Extensions;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Services
{
    internal class DataService
    {
        private static DataService _instance;
        public static DataService Instance => _instance ??= new DataService();
        private DataService() { }

        #region Data Loaded event
        /// <summary>
        /// Used when the data is loaded first time.
        /// Should only be triggered once -- when getting data from AC.
        /// </summary>
        public event EventHandler DataLoaded;
        public IEnumerable<IEnumerable<AnalysisFeature>> Features { get; private set; }
        public IEnumerable<UndirectedGraph<AnalysisFeature, Edge<AnalysisFeature>>> Graphs { get; private set; }
        public void LoadData(IEnumerable<IEnumerable<AnalysisFeature>> features)
        {
            List<HashSet<AnalysisFeature>> samlet = new();
            foreach (var col in features)
                samlet.Add(ProjectionService.ReProjectFeatures(
                    col, "EPSG:25832", "EPSG:3857").ToHashSet());
            Features = samlet;
            Graphs = GraphCreationService.CreateGraphsFromFeatures(Features);
            DataLoaded?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region Calculation data returned event
        /// <summary>
        /// Used when calculation service has returned data.
        /// </summary>
        public event EventHandler CalculationDataReturned;
        public IEnumerable<IEnumerable<AnalysisFeature>> CalculatedFeatures { get; private set; }
        public void StoreCalculatedData(IEnumerable<IEnumerable<AnalysisFeature>> calculatedFeatures)
        {
            CalculatedFeatures = calculatedFeatures;
            CalculationDataReturned?.Invoke(this, EventArgs.Empty);
        }
        #endregion
    }
}
