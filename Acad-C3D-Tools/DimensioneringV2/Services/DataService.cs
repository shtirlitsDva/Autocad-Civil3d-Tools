﻿using DimensioneringV2.GraphFeatures;

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
            Features = features;
            Graphs = GraphCreationService.CreateGraphsFromFeatures(Features);
            DataLoaded?.Invoke(this, EventArgs.Empty);
        }
        #endregion
    }
}
