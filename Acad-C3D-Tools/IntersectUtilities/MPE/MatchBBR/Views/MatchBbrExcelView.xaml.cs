using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using IntersectUtilities.MPE.MatchBBR.ViewModels;

namespace IntersectUtilities.MPE.MatchBBR.Views
{
    internal partial class MatchBbrExcelView : UserControl
    {
        private readonly ExcelTabViewModel _viewModel;

        public MatchBbrExcelView(ExcelTabViewModel viewModel)
        {
            // Theme must be in place before InitializeComponent: the root Background is a
            // StaticResource, and a plugin loaded into a collectible ALC cannot resolve pack URIs,
            // so merged dictionaries with Source="..." silently fail here.
            Resources = MatchBbrTheme.Load();
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = viewModel;

            // The preview grid's columns are the worksheet's own, so they only exist once a file
            // has been read.
            _viewModel.SheetColumnsChanged += RebuildColumns;
            Unloaded += (_, _) => _viewModel.SheetColumnsChanged -= RebuildColumns;
        }

        private void RebuildColumns()
        {
            RowsGrid.Columns.Clear();

            RowsGrid.Columns.Add(new DataGridCheckBoxColumn
            {
                Header = "✓",
                Width = 34,
                Binding = new Binding(nameof(ExcelRowViewModel.IsChecked))
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                },
            });

            RowsGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "#",
                Width = 48,
                IsReadOnly = true,
                Binding = new Binding(nameof(ExcelRowViewModel.RowReference)),
            });

            DataTemplate? filterHeader = TryFindResource("MbFilterHeaderTemplate") as DataTemplate;

            foreach (ColumnOption column in _viewModel.Columns)
            {
                DataGridTextColumn gridColumn = new DataGridTextColumn
                {
                    IsReadOnly = true,
                    Width = new DataGridLength(1, DataGridLengthUnitType.SizeToHeader),
                    // Indexer binding onto ExcelRowViewModel's string indexer, which is what lets
                    // a grid show columns that are not known until a workbook is opened.
                    Binding = new Binding($"[{column.Column}]"),

                    // The collection view cannot sort through an indexer path; the header is for
                    // filtering here anyway.
                    CanUserSort = false,
                };

                // The header's content is the column's own filter view model, rendered by
                // MbFilterHeaderTemplate — that is what puts an Excel-style dropdown on every
                // column without a line of per-column XAML.
                if (filterHeader is not null
                    && _viewModel.ColumnFilters.TryGetValue(column.Column, out ColumnFilterViewModel? filter))
                {
                    gridColumn.Header = filter;
                    gridColumn.HeaderTemplate = filterHeader;
                }
                else
                {
                    gridColumn.Header = column.Display;
                }

                RowsGrid.Columns.Add(gridColumn);
            }
        }
    }
}
