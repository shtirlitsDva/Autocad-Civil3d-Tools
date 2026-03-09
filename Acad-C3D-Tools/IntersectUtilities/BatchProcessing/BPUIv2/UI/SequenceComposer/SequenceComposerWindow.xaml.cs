using System.Windows;
using DimensioneringV2.UI;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.BatchProcessing.BPUIv2.UI.SequenceComposer
{
    public partial class SequenceComposerWindow : Window
    {
        public SequenceComposerWindow()
        {
            try
            {
                InitializeComponent();
                DataContext = new SequenceComposerViewModel();
                Loaded += (_, _) => DarkTitleBarHelper.EnableDarkTitleBar(this);
            }
            catch (Exception ex)
            {
                prdDbg($"BPUIv2: Failed to initialize SequenceComposerWindow: {ex}");
                MessageBox.Show(
                    $"Failed to open Sequence Composer:\n{ex}",
                    "BPv2 Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        public SequenceComposerViewModel? ViewModel =>
            DataContext as SequenceComposerViewModel;
    }
}
