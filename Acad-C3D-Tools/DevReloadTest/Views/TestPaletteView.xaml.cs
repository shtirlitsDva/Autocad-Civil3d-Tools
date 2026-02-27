using System;
using System.Windows;
using System.Windows.Controls;

namespace DevReloadTest.Views
{
    public partial class TestPaletteView : UserControl
    {
        public TestPaletteView()
        {
            InitializeComponent();
        }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            var ed = Autodesk.AutoCAD.ApplicationServices.Application
                .DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage($"\nButton clicked from isolated ALC! *{TestVersion.Tag}* Time: {DateTime.Now:HH:mm:ss}");
        }
    }
}
