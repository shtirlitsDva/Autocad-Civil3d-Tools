using System.Windows;

namespace DimensioneringV2.UI.CacheTest
{
    public partial class CacheTestWindow : Window
    {
        public CacheTestWindow()
        {
            InitializeComponent();
        }

        public CacheTestWindow(CacheTestViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}
