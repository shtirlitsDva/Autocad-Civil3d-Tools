using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace DimensioneringV2.UI
{
    public partial class FeaturePopupWindow : Window
    {
        private static double? _lastLeft;
        private static double? _lastTop;

        public FeaturePopupWindow()
        {
            InitializeComponent();
        }

        internal void ShowPopup(FeaturePopupViewModel vm)
        {
            DataContext = vm;

            if (_lastLeft.HasValue && _lastTop.HasValue)
            {
                Left = _lastLeft.Value;
                Top = _lastTop.Value;
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            Show();
        }

        internal void HidePopup()
        {
            if (IsVisible)
            {
                _lastLeft = Left;
                _lastTop = Top;
                Hide();
            }
        }

        private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Walk up from the click source — skip drag if the user clicked
            // on a button or scrollbar so those controls still work normally.
            DependencyObject? source = e.OriginalSource as DependencyObject;
            while (source != null && source != this)
            {
                if (source is ButtonBase or ScrollBar)
                    return;
                source = VisualTreeHelper.GetParent(source);
            }

            DragMove();
        }

        protected override void OnLocationChanged(System.EventArgs e)
        {
            base.OnLocationChanged(e);
            _lastLeft = Left;
            _lastTop = Top;
        }

        protected override void OnDeactivated(System.EventArgs e)
        {
            base.OnDeactivated(e);
            HidePopup();
        }

        private void CopyValue_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is string value)
                Clipboard.SetText(value);
        }
    }
}
