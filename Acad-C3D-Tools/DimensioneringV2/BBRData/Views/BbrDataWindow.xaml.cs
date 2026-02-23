using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

using DimensioneringV2.BBRData.Models;
using DimensioneringV2.BBRData.ViewModels;
using DimensioneringV2.UI;

namespace DimensioneringV2.BBRData.Views
{
    internal partial class BbrDataWindow : Window
    {
        private readonly BbrDataWindowViewModel _viewModel;

        public BbrDataWindow()
        {
            InitializeComponent();
            _viewModel = new BbrDataWindowViewModel();
            DataContext = _viewModel;
            Loaded += (s, e) => DarkTitleBarHelper.EnableDarkTitleBar(this);
            _viewModel.ColumnsChanged += OnColumnsChanged;
        }

        private void OnColumnsChanged(object? sender, EventArgs e)
        {
            var dg = MatchDataGrid;

            // Keep first 3 static columns: Match, Key, Ignore
            while (dg.Columns.Count > 3)
                dg.Columns.RemoveAt(3);

            // Transfer mapping columns (comparison: bbr_value <= csv_value)
            foreach (var mapping in _viewModel.TransferMappings)
            {
                dg.Columns.Add(CreateTransferColumn(mapping));
            }

            // Display-only BBR columns
            foreach (var prop in _viewModel.DisplayBbrProps)
            {
                dg.Columns.Add(new DataGridTextColumn
                {
                    Header = $"BBR: {prop.Name}",
                    Binding = new Binding($"BbrValues[{prop.Name}]"),
                    IsReadOnly = true,
                    Width = DataGridLength.SizeToHeader
                });
            }

            // Display-only CSV columns
            foreach (var col in _viewModel.DisplayCsvCols)
            {
                dg.Columns.Add(new DataGridTextColumn
                {
                    Header = $"CSV: {col.ColumnName}",
                    Binding = new Binding($"CsvValues[{col.ColumnName}]"),
                    IsReadOnly = true,
                    Width = DataGridLength.SizeToHeader
                });
            }
        }

        /// <summary>
        /// Creates a DataGrid comparison column for a transfer mapping.
        /// Layout: [BBR value] [<=] [CSV value] with per-cell background coloring.
        /// </summary>
        private static DataGridTemplateColumn CreateTransferColumn(TransferMapping mapping)
        {
            var column = new DataGridTemplateColumn
            {
                Header = mapping.DisplayHeader,
                IsReadOnly = true,
                MinWidth = 150,
                Width = DataGridLength.SizeToHeader
            };

            // Build CellTemplate programmatically using FrameworkElementFactory
            var cellTemplate = new DataTemplate();

            // Root: Border with background bound to transfer cell state
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetBinding(Border.BackgroundProperty,
                new Binding($"TransferCells[{mapping.Key}].CellBackground"));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(4, 2, 4, 2));

            // Inner: Grid with 3 columns [Star | Auto | Star] for aligned <=
            var gridFactory = new FrameworkElementFactory(typeof(Grid));

            var col0 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col0.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
            var col1 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col1.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
            var col2 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col2.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));

            gridFactory.AppendChild(col0);
            gridFactory.AppendChild(col1);
            gridFactory.AppendChild(col2);

            // TextBlock 0: BBR value (left-aligned)
            var bbrTextFactory = new FrameworkElementFactory(typeof(TextBlock));
            bbrTextFactory.SetBinding(TextBlock.TextProperty,
                new Binding($"TransferCells[{mapping.Key}].BbrValue"));
            bbrTextFactory.SetValue(Grid.ColumnProperty, 0);
            bbrTextFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            bbrTextFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Colors.White));

            // TextBlock 1: "<=" arrow (centered, dimmed)
            var arrowFactory = new FrameworkElementFactory(typeof(TextBlock));
            arrowFactory.SetValue(TextBlock.TextProperty, " <= ");
            arrowFactory.SetValue(Grid.ColumnProperty, 1);
            arrowFactory.SetValue(TextBlock.ForegroundProperty,
                new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)));

            // TextBlock 2: CSV value (left-aligned)
            var csvTextFactory = new FrameworkElementFactory(typeof(TextBlock));
            csvTextFactory.SetBinding(TextBlock.TextProperty,
                new Binding($"TransferCells[{mapping.Key}].CsvValue"));
            csvTextFactory.SetValue(Grid.ColumnProperty, 2);
            csvTextFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            csvTextFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Colors.White));

            gridFactory.AppendChild(bbrTextFactory);
            gridFactory.AppendChild(arrowFactory);
            gridFactory.AppendChild(csvTextFactory);

            borderFactory.AppendChild(gridFactory);
            cellTemplate.VisualTree = borderFactory;

            column.CellTemplate = cellTemplate;
            return column;
        }

        #region BBR key handlers

        private void BbrKeyPropCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox combo) return;
            if (combo.SelectedItem is not BbrPropertyDescriptor prop) return;
            if (combo.DataContext is not BbrMatchKey key) return;

            key.Parts.Clear();
            key.Parts.Add(prop);
            key.NotifyDescriptionChanged();
            _viewModel.NotifyBbrKeyChanged();
            combo.SelectedItem = null;
        }

        private void AddBbrCompoundKey_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CompoundKeyDialog(
                _viewModel.AvailableBbrPropsForKey.ToList())
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                var key = new BbrMatchKey
                {
                    Order = _viewModel.BbrKeys.Count + 1,
                    JoinMode = dialog.SelectedJoinMode
                };
                key.Parts.AddRange(dialog.SelectedBbrParts);
                key.NotifyDescriptionChanged();
                _viewModel.BbrKeys.Add(key);
                _viewModel.NotifyBbrKeyChanged();
            }
        }

        #endregion

        #region CSV key handlers

        private void CsvKeyColCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox combo) return;
            if (combo.SelectedItem is not string colName) return;
            if (combo.DataContext is not CsvMatchKey key) return;

            var dataType = key.Parts.FirstOrDefault()?.DataType ?? BbrDataType.String;
            key.Parts.Clear();
            key.Parts.Add(new CsvKeyPart(colName, dataType));
            key.NotifyDescriptionChanged();
            _viewModel.NotifyCsvKeyChanged();
            combo.SelectedItem = null;
        }

        private void CsvKeyTypeCombo_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not ComboBox combo) return;
            if (combo.DataContext is not CsvMatchKey key) return;
            if (key.Parts.Count == 0) return;

            var typeName = key.Parts[0].DataType.ToString();
            foreach (var item in combo.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Content?.ToString(), typeName, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = item;
                    break;
                }
            }
        }

        private void CsvKeyTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox combo) return;
            if (combo.DataContext is not CsvMatchKey key) return;
            if (combo.SelectedItem is not ComboBoxItem item) return;

            var dataType = GetSelectedDataType(item);
            if (key.Parts.Count == 0) return;

            key.Parts[0].DataType = dataType;
            key.NotifyDescriptionChanged();
            _viewModel.NotifyCsvKeyChanged();
        }

        private void AddCsvCompoundKey_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CompoundKeyDialog(
                _viewModel.AvailableCsvColsForKey.ToList())
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                var key = new CsvMatchKey
                {
                    Order = _viewModel.CsvKeys.Count + 1,
                    JoinMode = dialog.SelectedJoinMode
                };
                key.Parts.AddRange(dialog.SelectedCsvParts);
                key.NotifyDescriptionChanged();
                _viewModel.CsvKeys.Add(key);
                _viewModel.NotifyCsvKeyChanged();
            }
        }

        #endregion

        #region Transfer mapping handler

        private void AddTransferMapping_Click(object sender, RoutedEventArgs e)
        {
            var bbrProp = TransferBbrPropCombo.SelectedItem as BbrPropertyDescriptor;
            var csvCol = TransferCsvColCombo.SelectedItem as string;

            if (bbrProp != null && !string.IsNullOrEmpty(csvCol))
            {
                _viewModel.AddTransferMappingCommand.Execute(new object[] { bbrProp, csvCol });
                TransferBbrPropCombo.SelectedItem = null;
                TransferCsvColCombo.SelectedItem = null;
            }
        }

        #endregion

        #region Display prop/col handlers

        private void AddBbrDisplayProp_Click(object sender, RoutedEventArgs e)
        {
            if (BbrDisplayPropCombo.SelectedItem is BbrPropertyDescriptor prop)
            {
                _viewModel.AddDisplayBbrPropCommand.Execute(prop);
                BbrDisplayPropCombo.SelectedItem = null;
            }
        }

        private void AddCsvDisplayCol_Click(object sender, RoutedEventArgs e)
        {
            var colName = CsvDisplayColCombo.SelectedItem as string;
            var dataType = GetSelectedDataType(CsvDisplayTypeCombo.SelectedItem as ComboBoxItem);

            if (!string.IsNullOrEmpty(colName))
            {
                _viewModel.AddDisplayCsvColCommand.Execute(new object[] { colName, dataType });
                CsvDisplayColCombo.SelectedItem = null;
            }
        }

        #endregion

        private static BbrDataType GetSelectedDataType(ComboBoxItem? item)
        {
            var typeName = item?.Content?.ToString() ?? string.Empty;
            var dataType = BbrPropertyDescriptor.MapTypeFromString(typeName);
            return dataType == BbrDataType.Unknown ? BbrDataType.String : dataType;
        }
    }
}
