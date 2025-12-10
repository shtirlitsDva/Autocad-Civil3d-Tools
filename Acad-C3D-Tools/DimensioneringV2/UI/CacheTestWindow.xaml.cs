using System.Windows;

namespace DimensioneringV2.UI
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
