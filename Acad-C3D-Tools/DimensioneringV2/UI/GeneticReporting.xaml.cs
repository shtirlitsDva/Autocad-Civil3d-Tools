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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DimensioneringV2.UI
{
    /// <summary>
    /// Interaction logic for GeneticReporting.xaml
    /// </summary>
    public partial class GeneticReporting : Window
    {
        GeneticReportingViewModel vm = new();

        public GeneticReporting()
        {
            InitializeComponent();

            DataContext = vm;
        }
    }
}
