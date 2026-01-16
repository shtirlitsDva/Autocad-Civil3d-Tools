using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;

namespace DimensioneringV2.UI
{
    /// <summary>
    /// Interaction logic for PriceSummaryWindow.xaml
    /// </summary>
    public partial class PriceSummaryWindow : Window
    {
        private static readonly CultureInfo DanishCulture = new CultureInfo("da-DK");

        public PriceSummaryWindow(
            IEnumerable<object> stikTable, 
            IEnumerable<object> flsTable, 
            object grandTotal,
            double stikTotal,
            double flsTotal)
        {
            InitializeComponent();
            
            // Enable dark title bar
            Loaded += (s, e) => DarkTitleBarHelper.EnableDarkTitleBar(this);

            double gtotal = (double)grandTotal;

            // Set data sources
            ServiceLinesTable.ItemsSource = stikTable;
            SupplyLinesTable.ItemsSource = flsTable;
            
            // Set expander headers with subtotals
            StikHeaderText.Text = $"Stikledninger: {stikTotal.ToString("N0", DanishCulture)} kr";
            FlsHeaderText.Text = $"Fordelingsledninger: {flsTotal.ToString("N0", DanishCulture)} kr";
            
            // Set grand total
            GrandTotalTextBlock.Text = $"Samlet pris: {gtotal.ToString("N0", DanishCulture)} kr";
        }
    }
}
