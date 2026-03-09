using System.Windows;
using System.Windows.Controls;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.BatchProcessing.BPUIv2.UI
{
    public partial class BatchProcessingControl : UserControl
    {
        public BatchProcessingControl()
        {
            try
            {
                InitializeComponent();
                DataContext = new BatchProcessingViewModel();
            }
            catch (Exception ex)
            {
                prdDbg($"BPUIv2: Failed to initialize BatchProcessingControl: {ex}");
                Content = new TextBlock
                {
                    Text = $"BPv2 failed to load:\n{ex}",
                    Foreground = System.Windows.Media.Brushes.OrangeRed,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10)
                };
            }
        }

        public BatchProcessingViewModel? ViewModel =>
            DataContext as BatchProcessingViewModel;
    }
}
