using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using IntersectUtilities.MPE.MatchBBR.ViewModels;

namespace IntersectUtilities.MPE.MatchBBR.Views
{
    internal partial class MatchBbrCompareView : UserControl
    {
        private static readonly BooleanToVisibilityConverter BoolToVisibility = new();

        private readonly CompareTabViewModel _viewModel;

        public MatchBbrCompareView(CompareTabViewModel viewModel)
        {
            // See MatchBbrTheme: the dictionary must be assigned before InitializeComponent.
            Resources = MatchBbrTheme.Load();
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = viewModel;

            _viewModel.RuleColumnsChanged += RebuildColumns;
            Unloaded += (_, _) => _viewModel.RuleColumnsChanged -= RebuildColumns;

            RebuildColumns();
        }

        // The fixed columns are always there; one group of three is added per configured value
        // rule, so adding a rule on the Excel tab grows the grid without a code change.
        private void RebuildColumns()
        {
            ComparisonGrid.Columns.Clear();

            AddTextColumn("Status", nameof(ComparisonRowViewModel.StatusText), 110);
            AddTextColumn("Adresse (Excel)", nameof(ComparisonRowViewModel.ExcelAddress), 200);
            AddTextColumn("Adresse (BBR)", nameof(ComparisonRowViewModel.BbrAddress), 200);
            AddTextColumn("Distrikt", nameof(ComparisonRowViewModel.District), 100);

            foreach (CompareRule rule in _viewModel.ValueRules)
            {
                string label = MatchBbrState.LabelFor(rule);
                AddTextColumn($"{label} (Excel)", $"[{rule.Id}].ExcelText", 130);
                AddTextColumn($"{label} (BBR)", $"[{rule.Id}].BbrText", 130);
                ComparisonGrid.Columns.Add(BuildDeltaColumn(rule, label));
            }

            AddTextColumn("Årsag", nameof(ComparisonRowViewModel.Reason), 240);
        }

        private void AddTextColumn(string header, string path, double width)
        {
            ComparisonGrid.Columns.Add(new DataGridTextColumn
            {
                Header = header,
                Width = new DataGridLength(width),
                IsReadOnly = true,
                Binding = new Binding(path),
                HeaderStyle = TryFindResource("MbColumnHeaderWithTooltip") as Style,

                // Sorting resolves the binding path by name through the collection view, which
                // cannot walk an indexer — clicking such a header would throw rather than sort.
                CanUserSort = !path.StartsWith("[", StringComparison.Ordinal),
            });
        }

        // Delta plus the per-cell write arrow. The arrow only appears where the two sides
        // actually disagree — an arrow on every row would invite writing values that are already
        // identical.
        private DataGridTemplateColumn BuildDeltaColumn(CompareRule rule, string label)
        {
            FrameworkElementFactory panel = new FrameworkElementFactory(typeof(StackPanel));
            panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            FrameworkElementFactory text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetBinding(TextBlock.TextProperty, new Binding($"[{rule.Id}].DeltaText"));
            text.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            panel.AppendChild(text);

            FrameworkElementFactory button = new FrameworkElementFactory(typeof(Button));
            button.SetValue(ContentControl.ContentProperty, "→");
            button.SetValue(StyleProperty, TryFindResource("MbCellButton") as Style);
            button.SetBinding(
                System.Windows.Controls.Primitives.ButtonBase.CommandProperty,
                new Binding(nameof(ComparisonRowViewModel.WriteCellCommand)));
            button.SetValue(System.Windows.Controls.Primitives.ButtonBase.CommandParameterProperty, rule.Id);
            button.SetBinding(
                VisibilityProperty,
                new Binding($"[{rule.Id}].IsMismatch") { Converter = BoolToVisibility });
            panel.AppendChild(button);

            return new DataGridTemplateColumn
            {
                Header = $"Δ {label}",
                Width = new DataGridLength(90),
                CellTemplate = new DataTemplate { VisualTree = panel },
                HeaderStyle = TryFindResource("MbColumnHeaderWithTooltip") as Style,
                CanUserSort = false,
            };
        }
    }
}
