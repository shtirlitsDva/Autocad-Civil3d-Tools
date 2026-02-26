using DimensioneringV2.Models.Trykprofil;

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

namespace DimensioneringV2.UI.Trykprofil
{
    /// <summary>
    /// Interaction logic for TrykprofilWindow.xaml
    /// </summary>
    public partial class TrykprofilWindow : Window
    {
        private TrykprofilWindowViewModel vm = new();
        public TrykprofilWindow(IEnumerable<PressureProfileEntry> entries, PressureData pdata)
        {
            InitializeComponent();
            DataContext = vm;
            vm.LoadData(entries, pdata);
        }
    }
}