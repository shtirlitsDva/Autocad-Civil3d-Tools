using DimensioneringV2.Vejklasser.Models;
using DimensioneringV2.Vejklasser.ViewModels;

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

namespace DimensioneringV2.Vejklasser.Views
{
    /// <summary>
    /// Interaction logic for VejklasserGridView.xaml
    /// </summary>
    public partial class VejklasserGridView : Window
    {
        VejklasserGridViewModel vm = new();
        public VejklasserGridView(List<VejnavnTilVejklasseModel> models)
        {
            InitializeComponent();
            vm.ReceiveData(models);
            DataContext = vm;
        }

        public List<VejnavnTilVejklasseModel> Results => vm.Models.ToList();
    }
}
