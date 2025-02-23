using IntersectUtilities.Forms.PipeSettingsWpf.ViewModels;

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

namespace IntersectUtilities.Forms.PipeSettingsWpf.Views
{
    /// <summary>
    /// Interaction logic for PipeSettingsWindow.xaml
    /// </summary>
    public partial class PipeSettingsWindow : Window
    {
        PipeSettingsViewModel vm = new();
        public PipeSettingsWindow()
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
