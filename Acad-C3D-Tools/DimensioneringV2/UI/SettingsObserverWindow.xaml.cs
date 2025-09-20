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

namespace DimensioneringV2.UI
{
    /// <summary>
    /// Interaction logic for SettingsObserverWindow.xaml
    /// </summary>
    public partial class SettingsObserverWindow : Window
    {
        SettingsObserverWindowViewModel vm = new();
        public SettingsObserverWindow()
        {
            InitializeComponent();
            DataContext = vm;
        }

        public void Init(HydraulicSettings settings)
        {
            vm.Init(settings);            
        }
    }
}
