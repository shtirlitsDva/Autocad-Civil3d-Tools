using NSPaletteSet.ViewModels;

using System.Windows.Controls;

namespace NSPaletteSet.Views
{
    public partial class PipePaletteView : UserControl
    {
        public PipePaletteView()
        {
            InitializeComponent();
            DataContext = new PipePaletteViewModel();
        }
    }
}
