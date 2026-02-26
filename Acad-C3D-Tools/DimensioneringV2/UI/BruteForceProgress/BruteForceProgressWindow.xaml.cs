using CommunityToolkit.Mvvm.ComponentModel;

using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace DimensioneringV2.UI.BruteForceProgress
{
    public partial class BruteForceProgressWindow : Window
    {
        BruteForceProgressViewModel vm = new();
        
        public BruteForceProgressWindow()
        {
            InitializeComponent();
            DataContext = vm;
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            vm.StopRequested = true;
        }
    }
}
