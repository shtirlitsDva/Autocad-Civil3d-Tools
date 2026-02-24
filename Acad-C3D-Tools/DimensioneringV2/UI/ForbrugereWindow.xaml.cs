using System.Collections.Generic;
using System.Windows;

namespace DimensioneringV2.UI
{
    public partial class ForbrugereWindow : Window
    {
        private readonly List<ForbrugerRow> _rows;

        public ForbrugereWindow(List<ForbrugerRow> rows)
        {
            InitializeComponent();
            Loaded += (s, e) => DarkTitleBarHelper.EnableDarkTitleBar(this);
            _rows = rows;
            ForbrugereGrid.ItemsSource = _rows;
        }

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            ForbrugereExporter.ExportToExcel(_rows);
        }

        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            ForbrugereExporter.ExportToPdf(_rows);
        }
    }
}
