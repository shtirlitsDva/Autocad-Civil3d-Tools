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
        private IEnumerable<IEnumerable<AnalysisFeature>> _features;
        public IEnumerable<IEnumerable<AnalysisFeature>> Features
        {
            get => _features;
            set
            {
                _features = value;
                OnDataUpdated();
            }
        }
        public void UpdateFeatures(IEnumerable<IEnumerable<AnalysisFeature>> features)
        {
            Features = features;
        }
        private void OnDataUpdated()
        {
            DataUpdated?.Invoke(this, EventArgs.Empty);
        }
    }
}
