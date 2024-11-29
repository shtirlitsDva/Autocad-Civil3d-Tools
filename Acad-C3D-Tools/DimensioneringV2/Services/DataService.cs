using DimensioneringV2.GraphFeatures;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Services
{
    internal class DataService : IDataService
    {
        private static DataService _instance;
        public static DataService Instance => _instance ??= new DataService();
        private DataService() { }

        public event EventHandler DataUpdated;

        private IEnumerable<FeatureNode> _features;
        private IEnumerable<UndirectedGraph<FeatureNode, Edge<FeatureNode>>> _graphs;

        public IEnumerable<FeatureNode> Features
        {
            get => _features;
            set
            {
                _features = value;
                OnDataUpdated();
            }
        }

        public IEnumerable<UndirectedGraph<FeatureNode, Edge<FeatureNode>>> Graphs
        {
            get => _graphs;
            set
            {
                _graphs = value;
                OnDataUpdated();
            }
        }

        public void UpdateData(IEnumerable<FeatureNode> features, IEnumerable<UndirectedGraph<FeatureNode, Edge<FeatureNode>>> graphs)
        {
            Features = features;
            Graphs = graphs;
        }

        private void OnDataUpdated()
        {
            DataUpdated?.Invoke(this, EventArgs.Empty);
        }
    }
}
