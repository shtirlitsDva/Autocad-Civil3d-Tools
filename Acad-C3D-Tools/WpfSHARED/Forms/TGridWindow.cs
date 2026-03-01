using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Runtime.InteropServices;

namespace IntersectUtilities.Forms
{
    internal static class DpiNative
    {
        [DllImport("user32.dll")]
        internal static extern int GetDpiForWindow(IntPtr hwnd);
    }

    public class TGridWindow<T>: Window
    {
        public T? SelectedValue { get; private set; }

        private readonly UniformGrid _grid;
        private readonly List<Button> _buttons = new();
        private int _columns;
        private int _rows;

        public TGridWindow(IEnumerable<T> items, Func<T, string> displayValue, string message = "")
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (displayValue == null) throw new ArgumentNullException(nameof(displayValue));

            var itemList = items.ToList();
            if (itemList.Count == 0)
                throw new ArgumentException("Items collection cannot be empty.", nameof(items));

            var displayTexts = itemList.Select(i => displayValue(i) ?? string.Empty).ToList();

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.Manual;
            ShowInTaskbar = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;

            var root = new StackPanel();

            if (!string.IsNullOrEmpty(message))
            {
                var caption = new TextBlock
                {
                    Text = message,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8),
                    FontWeight = FontWeights.Bold,
                };
                root.Children.Add(caption);
            }

            CalculateLayout(displayTexts);

            _grid = new UniformGrid
            {
                Columns = _columns,
                Rows = _rows,
            };

            for (int i = 0; i < itemList.Count; i++)
            {
                var item = itemList[i];
                var text = displayTexts[i];

                var btn = new Button
                {
                    Content = text,
                    Margin = new Thickness(1),
                    Padding = new Thickness(8, 4, 8, 4),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Focusable = true,
                };
                btn.Tag = item;
                btn.Click += (_, _) =>
                {
                    SelectedValue = item;
                    DialogResult = true;
                    Close();
                };
                _buttons.Add(btn);
                _grid.Children.Add(btn);
            }

            var border = new Border
            {
                Child = _grid,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4),
            };
            root.Children.Add(border);

            Content = root;

            SourceInitialized += (_, _) =>
            {
                if (PresentationSource.FromVisual(this) is HwndSource source)
                {
                    double wpfDpi = 96.0 * source.CompositionTarget.TransformToDevice.M11;
                    double monitorDpi = DpiNative.GetDpiForWindow(source.Handle);
                    double scale = monitorDpi / wpfDpi;
                    if (Math.Abs(scale - 1.0) > 0.01)
                        root.LayoutTransform = new ScaleTransform(scale, scale);
                }
            };

            PreviewKeyDown += OnPreviewKeyDown;
            Loaded += OnLoaded;
            ContentRendered += OnContentRendered;
        }

        private void CalculateLayout(List<string> displayTexts)
        {
            int count = displayTexts.Count;

            var typeface = new Typeface(SystemFonts.MessageFontFamily, FontStyles.Normal,
                FontWeights.Bold, FontStretches.Normal);
            double dpi = 96;
            double maxWidth = 0;

            foreach (var text in displayTexts)
            {
                var ft = new FormattedText(text, CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, typeface, SystemFonts.MessageFontSize, Brushes.Black,
                    new NumberSubstitution(), TextFormattingMode.Display, dpi);
                if (ft.Width > maxWidth) maxWidth = ft.Width;
            }

            double buttonWidth = maxWidth + 32;

            _columns = (int)Math.Ceiling(Math.Sqrt(count * 16.0 / 9.0));
            if (_columns < 1) _columns = 1;
            if (_columns > count) _columns = count;

            while (_columns > 1)
            {
                int rowsCandidate = (int)Math.Ceiling((double)count / _columns);
                double w = _columns * buttonWidth;
                double h = rowsCandidate * 30;
                if (w / h <= 16.0 / 9.0) break;
                _columns--;
            }

            _rows = (int)Math.Ceiling((double)count / _columns);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            double dpiScale = 1.0;
            if (PresentationSource.FromVisual(this) is HwndSource source)
                dpiScale = source.CompositionTarget.TransformToDevice.M11;

            var screen = System.Windows.Forms.Screen.FromPoint(
                System.Windows.Forms.Cursor.Position);
            var workArea = screen.WorkingArea;

            double cx = System.Windows.Forms.Cursor.Position.X / dpiScale;
            double cy = System.Windows.Forms.Cursor.Position.Y / dpiScale;
            double waL = workArea.Left / dpiScale;
            double waT = workArea.Top / dpiScale;
            double waR = workArea.Right / dpiScale;
            double waB = workArea.Bottom / dpiScale;

            double x = cx - ActualWidth / 2;
            double y = cy - ActualHeight / 2;

            if (x < waL) x = waL;
            if (y < waT) y = waT;
            if (x + ActualWidth > waR) x = waR - ActualWidth;
            if (y + ActualHeight > waB) y = waB - ActualHeight;

            Left = x;
            Top = y;
        }

        private void OnContentRendered(object? sender, EventArgs e)
        {
            if (_buttons.Count > 0)
            {
                int mid = (_rows / 2) * _columns + (_columns / 2);
                if (mid >= _buttons.Count) mid = _buttons.Count / 2;
                _buttons[mid].Focus();
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            int idx = -1;
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i].IsFocused) { idx = i; break; }
            }

            if (idx < 0) return;

            int row = idx / _columns;
            int col = idx % _columns;
            int newIdx = -1;

            switch (e.Key)
            {
                case Key.Up:
                    for (int r = row - 1; ; r--)
                    {
                        if (r < 0) r = _rows - 1;
                        if (r == row) break;
                        int candidate = r * _columns + col;
                        if (candidate < _buttons.Count) { newIdx = candidate; break; }
                    }
                    break;
                case Key.Down:
                    for (int r = row + 1; ; r++)
                    {
                        if (r >= _rows) r = 0;
                        if (r == row) break;
                        int candidate = r * _columns + col;
                        if (candidate < _buttons.Count) { newIdx = candidate; break; }
                    }
                    break;
                case Key.Left:
                    newIdx = idx - 1;
                    if (newIdx < 0) newIdx = _buttons.Count - 1;
                    break;
                case Key.Right:
                    newIdx = idx + 1;
                    if (newIdx >= _buttons.Count) newIdx = 0;
                    break;
                case Key.Escape:
                    Close();
                    e.Handled = true;
                    return;
                case Key.Enter:
                    if (idx < _buttons.Count)
                    {
                        SelectedValue = (T?)_buttons[idx].Tag;
                        DialogResult = true;
                        Close();
                    }
                    e.Handled = true;
                    return;
            }

            if (newIdx >= 0 && newIdx < _buttons.Count)
            {
                _buttons[newIdx].Focus();
                e.Handled = true;
            }
        }
    }
}
