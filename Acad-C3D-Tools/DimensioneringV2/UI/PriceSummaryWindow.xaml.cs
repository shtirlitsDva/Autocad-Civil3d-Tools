using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Interaction logic for PriceSummaryWindow.xaml
    /// </summary>
    public partial class PriceSummaryWindow : Window
    {
        public PriceSummaryWindow(IEnumerable<object> stikTable, IEnumerable<object> flsTable, object grandTotal)
        {
            InitializeComponent();

            double gtotal = (double)grandTotal;

            ServiceLinesTable.ItemsSource = stikTable;
            SupplyLinesTable.ItemsSource = flsTable;
            GrandTotalTextBlock.Text = $"Samlet pris: {gtotal.ToString("N0", new CultureInfo("da-DK"))}"; ;
        }
    }
}
