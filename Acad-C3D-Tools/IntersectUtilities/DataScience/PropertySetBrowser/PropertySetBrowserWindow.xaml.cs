using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;

using PsDataType = Autodesk.Aec.PropertyData.DataType;

namespace IntersectUtilities.DataScience.PropertySetBrowser
{
    public partial class PropertySetBrowserWindow : Window
    {
        private readonly PropertySetBrowserViewModel _viewModel;

        #region Dark Title Bar P/Invoke
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private static void EnableDarkTitleBar(IntPtr hwnd)
        {
            int darkMode = 1;
            // Try the newer attribute first (Windows 10 20H1+), fall back to older one
            if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref darkMode, sizeof(int));
            }
        }
        #endregion

        public PropertySetBrowserWindow(Database database)
        {
            InitializeComponent();

            _viewModel = new PropertySetBrowserViewModel(database);
            DataContext = _viewModel;

            // Subscribe to column changes
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Enable dark title bar when window loads
            Loaded += (s, e) =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                EnableDarkTitleBar(hwnd);
            };
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PropertySetBrowserViewModel.PropertyColumns))
            {
                // Must run on UI thread
                Dispatcher.Invoke(() => RebuildColumns());
            }
        }

        private void RebuildColumns()
        {
            GridViewColumns.Columns.Clear();

            // Add Handle column
            GridViewColumns.Columns.Add(new GridViewColumn
            {
                Header = "Handle",
                DisplayMemberBinding = new Binding("EntityHandle"),
                Width = double.NaN // Auto width
            });

            // Add Entity Type column
            GridViewColumns.Columns.Add(new GridViewColumn
            {
                Header = "Type",
                DisplayMemberBinding = new Binding("EntityType"),
                Width = double.NaN
            });

            // Add property columns dynamically
            foreach (var propName in _viewModel.PropertyColumns)
            {
                // Determine alignment based on data type
                bool isNumeric = false;
                if (_viewModel.PropertyDataTypes.TryGetValue(propName, out var dataType))
                {
                    isNumeric = dataType == PsDataType.Real || dataType == PsDataType.Integer;
                }

                var column = new GridViewColumn
                {
                    Header = propName,
                    Width = double.NaN // Auto width
                };

                // Use CellTemplate for custom alignment
                column.CellTemplate = CreateCellTemplate(propName, isNumeric);
                GridViewColumns.Columns.Add(column);
            }
        }

        private DataTemplate CreateCellTemplate(string propertyName, bool isNumeric)
        {
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(TextBlock));
            factory.SetBinding(TextBlock.TextProperty, new Binding($"Properties[{propertyName}]"));
            factory.SetValue(TextBlock.PaddingProperty, new Thickness(6, 2, 6, 2));
            factory.SetValue(TextBlock.TextAlignmentProperty, 
                isNumeric ? System.Windows.TextAlignment.Right : System.Windows.TextAlignment.Left);
            template.VisualTree = factory;
            return template;
        }

        private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Zoom to entity on double-click (only if clicking on an item, not header)
            if (e.OriginalSource is FrameworkElement fe && fe.DataContext is PropertySetEntityRow)
            {
                if (_viewModel.ZoomToEntityCommand.CanExecute(null))
                {
                    _viewModel.ZoomToEntityCommand.Execute(null);
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            base.OnClosed(e);
        }
    }

    /// <summary>
    /// Converts bool to inverted bool for binding.
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b ? !b : value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b ? !b : value;
        }
    }
}
