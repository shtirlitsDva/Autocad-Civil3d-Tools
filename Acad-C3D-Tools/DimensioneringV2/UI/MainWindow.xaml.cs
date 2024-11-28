using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using Mapsui.UI.Wpf;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;

namespace DimensioneringV2.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            // Execute AutoCAD command to create features
            Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument
                .SendStringToExecute("DIM2CREATEFEATURES ", true, false, false);

            // Load features into the Mapsui MapControl
            LoadFeaturesIntoMap();
        }

        private void LoadFeaturesIntoMap()
        {
            // Assuming List<List<FeatureNode>> features is populated by the command executed
            // Code here to load the features into the Mapsui map, e.g.:
            // foreach (var featureList in features)
            // {
            //     var layer = new MemoryLayer();
            //     layer.Features = featureList.Select(fn => fn.ToMapsuiFeature()).ToList();
            //     MapControl.Map.Layers.Add(layer);
            // }
            MapControl.Refresh();
        }
    }
}
