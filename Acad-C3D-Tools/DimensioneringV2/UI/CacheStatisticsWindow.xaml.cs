using System.Windows;

namespace DimensioneringV2.UI
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
