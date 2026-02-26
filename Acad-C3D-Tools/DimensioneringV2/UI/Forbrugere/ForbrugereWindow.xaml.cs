using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Shell;

using DimensioneringV2.UI;

namespace DimensioneringV2.UI.Forbrugere
{
    public partial class ForbrugereWindow : Window
    {
        private readonly List<ForbrugerRow> _rows;
        private readonly ICollectionView _view;

        public ForbrugereWindow(List<ForbrugerRow> rows)
        {
            InitializeComponent();
            Loaded += (s, e) => DarkTitleBarHelper.EnableDarkTitleBar(this);
            _rows = rows;
            _view = CollectionViewSource.GetDefaultView(_rows);
            ForbrugereGrid.ItemsSource = _view;

            // Apply natural sort on Adresse column by default
            if (_view is ListCollectionView lcv)
            {
                lcv.CustomSort = new NaturalPropertyComparer("Adresse", ListSortDirection.Ascending);
                ForbrugereGrid.Columns[0].SortDirection = ListSortDirection.Ascending;
            }
        }

        #region Search
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            bool hasText = !string.IsNullOrEmpty(SearchBox.Text);
            SearchWatermark.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
            ClearSearchButton.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;
            ApplyFilter();
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SearchBox.Text = "";
                ForbrugereGrid.Focus();
            }
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            SearchBox.Focus();
        }

        private void ApplyFilter()
        {
            var text = SearchBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(text))
            {
                _view.Filter = null;
                return;
            }

            _view.Filter = obj =>
            {
                if (obj is not ForbrugerRow row) return false;
                return row.Adresse.Contains(text, StringComparison.OrdinalIgnoreCase)
                    || row.Type.Contains(text, StringComparison.OrdinalIgnoreCase)
                    || row.BBRAreal.ToString("N0").Contains(text, StringComparison.OrdinalIgnoreCase)
                    || row.Effekt.ToString("N2").Contains(text, StringComparison.OrdinalIgnoreCase)
                    || row.Aarsforbrug.ToString("N2").Contains(text, StringComparison.OrdinalIgnoreCase)
                    || row.Stiklaengde.ToString("N2").Contains(text, StringComparison.OrdinalIgnoreCase)
                    || row.DN.Contains(text, StringComparison.OrdinalIgnoreCase)
                    || row.Tryktab.ToString("N4").Contains(text, StringComparison.OrdinalIgnoreCase);
            };
        }
        #endregion

        #region Natural sort
        private void ForbrugereGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;

            var direction = e.Column.SortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            // Clear sort indicators on all columns
            foreach (var col in ForbrugereGrid.Columns)
                col.SortDirection = null;

            e.Column.SortDirection = direction;

            var propertyName = e.Column.SortMemberPath;
            if (string.IsNullOrEmpty(propertyName) && e.Column is DataGridBoundColumn bc
                && bc.Binding is Binding b)
            {
                propertyName = b.Path.Path;
            }

            if (_view is ListCollectionView lcv)
            {
                lcv.CustomSort = new NaturalPropertyComparer(propertyName, direction);
            }
        }

        private sealed class NaturalPropertyComparer : IComparer
        {
            private readonly string _propertyName;
            private readonly ListSortDirection _direction;

            [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
            private static extern int StrCmpLogicalW(string psz1, string psz2);

            public NaturalPropertyComparer(string propertyName, ListSortDirection direction)
            {
                _propertyName = propertyName;
                _direction = direction;
            }

            public int Compare(object? x, object? y)
            {
                if (x is not ForbrugerRow a || y is not ForbrugerRow b) return 0;

                int result = CompareValues(GetValue(a), GetValue(b));
                return _direction == ListSortDirection.Ascending ? result : -result;
            }

            private object? GetValue(ForbrugerRow row) => _propertyName switch
            {
                "Adresse" => row.Adresse,
                "Type" => row.Type,
                "BBRAreal" => row.BBRAreal,
                "Effekt" => row.Effekt,
                "Aarsforbrug" => row.Aarsforbrug,
                "Stiklaengde" => row.Stiklaengde,
                "DN" => row.DN,
                "Tryktab" => row.Tryktab,
                "NødvendigtDisponibeltTryk" => row.NødvendigtDisponibeltTryk,
                _ => row.Adresse
            };

            private static int CompareValues(object? a, object? b)
            {
                if (a is double dA && b is double dB)
                    return dA.CompareTo(dB);

                return StrCmpLogicalW(a?.ToString() ?? "", b?.ToString() ?? "");
            }
        }
        #endregion

        #region Export
        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            ForbrugereExporter.ExportToExcel(_rows);
        }
        #endregion

        #region Caption buttons
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
            => SystemCommands.MinimizeWindow(this);

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                SystemCommands.RestoreWindow(this);
            else
                SystemCommands.MaximizeWindow(this);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
            => SystemCommands.CloseWindow(this);

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            MaximizeButton.Content = WindowState == WindowState.Maximized
                ? "\uE923"   // ChromeRestore
                : "\uE922";  // ChromeMaximize
        }
        #endregion
    }
}
