using System.Windows;

namespace DimensioneringV2.UI.CacheStatistics
{
    public partial class CacheStatisticsWindow : Window
    {
        public CacheStatisticsWindow()
        {
            InitializeComponent();
        }

        public CacheStatisticsWindow(CacheStatisticsViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
