using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

using DimensioneringV2.BBRData.Models;
using DimensioneringV2.UI;

namespace DimensioneringV2.BBRData.Views
{
    /// <summary>
    /// Dialog for creating compound keys. Works in two modes:
    /// - BBR mode: select multiple BBR properties
    /// - CSV mode: select multiple CSV columns with type per part
    /// </summary>
    internal partial class CompoundKeyDialog : Window
    {
        public enum DialogMode { Bbr, Csv }

        private readonly DialogMode _mode;
        private readonly IReadOnlyList<BbrPropertyDescriptor>? _availableBbrProps;
        private readonly IReadOnlyList<string>? _availableCsvCols;
        private readonly List<PartRow> _partRows = new();

        /// <summary>Populated in BBR mode after OK.</summary>
        public List<BbrPropertyDescriptor> SelectedBbrParts { get; } = new();

        /// <summary>Populated in CSV mode after OK.</summary>
        public List<CsvKeyPart> SelectedCsvParts { get; } = new();

        public KeyJoinMode SelectedJoinMode { get; private set; } = KeyJoinMode.SpaceSeparated;

        /// <summary>Create a BBR compound key dialog.</summary>
        public CompoundKeyDialog(IReadOnlyList<BbrPropertyDescriptor> availableBbrProps)
        {
            InitializeComponent();
            Loaded += (s, e) => DarkTitleBarHelper.EnableDarkTitleBar(this);

            _mode = DialogMode.Bbr;
            _availableBbrProps = availableBbrProps;
            HeaderText.Text = "Define compound key parts (select BBR properties):";

            AddPartRow();
            AddPartRow();
        }

        /// <summary>Create a CSV compound key dialog.</summary>
        public CompoundKeyDialog(IReadOnlyList<string> availableCsvCols)
        {
            InitializeComponent();
            Loaded += (s, e) => DarkTitleBarHelper.EnableDarkTitleBar(this);

            _mode = DialogMode.Csv;
            _availableCsvCols = availableCsvCols;
            HeaderText.Text = "Define compound key parts (select CSV columns):";

            AddPartRow();
            AddPartRow();
        }

        private void AddPartRow()
        {
            var row = new PartRow();
            _partRows.Add(row);

            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };

            var label = new TextBlock
            {
                Text = $"Part {_partRows.Count}:",
                Width = 50,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = FindResource("Text") as System.Windows.Media.Brush
            };

            panel.Children.Add(label);

            if (_mode == DialogMode.Bbr)
            {
                var propLabel = new TextBlock
                {
                    Text = "BBR:",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5, 0, 5, 0),
                    Foreground = FindResource("Text") as System.Windows.Media.Brush
                };

                var bbrCombo = new ComboBox
                {
                    Width = 200,
                    ItemsSource = _availableBbrProps,
                    DisplayMemberPath = "Name",
                    Style = FindResource("ModernDarkComboBox") as Style
                };
                row.BbrCombo = bbrCombo;

                panel.Children.Add(propLabel);
                panel.Children.Add(bbrCombo);
            }
            else // CSV mode
            {
                var csvLabel = new TextBlock
                {
                    Text = "CSV:",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5, 0, 5, 0),
                    Foreground = FindResource("Text") as System.Windows.Media.Brush
                };

                var csvCombo = new ComboBox
                {
                    Width = 180,
                    ItemsSource = _availableCsvCols,
                    Style = FindResource("ModernDarkComboBox") as Style
                };
                row.CsvCombo = csvCombo;

                var typeLabel = new TextBlock
                {
                    Text = "Type:",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 5, 0),
                    Foreground = FindResource("Text") as System.Windows.Media.Brush
                };

                var typeCombo = new ComboBox
                {
                    Width = 80,
                    ItemsSource = new[] { BbrDataType.String, BbrDataType.Int, BbrDataType.Double },
                    SelectedIndex = 0,
                    Style = FindResource("ModernDarkComboBox") as Style
                };
                row.TypeCombo = typeCombo;

                panel.Children.Add(csvLabel);
                panel.Children.Add(csvCombo);
                panel.Children.Add(typeLabel);
                panel.Children.Add(typeCombo);
            }

            var removeBtn = new Button
            {
                Content = "\uE711",
                Style = FindResource("IconButtonStyle") as Style,
                Margin = new Thickness(10, 0, 0, 0)
            };
            removeBtn.Click += (s, e) => RemovePartRow(row, panel);
            panel.Children.Add(removeBtn);

            row.Panel = panel;
            PartsPanel.Children.Add(panel);
        }

        private void RemovePartRow(PartRow row, StackPanel panel)
        {
            if (_partRows.Count <= 2)
            {
                MessageBox.Show("Compound key must have at least 2 parts.",
                    "Cannot Remove", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _partRows.Remove(row);
            PartsPanel.Children.Remove(panel);
            RenumberParts();
        }

        private void RenumberParts()
        {
            for (int i = 0; i < _partRows.Count; i++)
            {
                if (_partRows[i].Panel?.Children[0] is TextBlock label)
                    label.Text = $"Part {i + 1}:";
            }
        }

        private void AddPart_Click(object sender, RoutedEventArgs e) => AddPartRow();

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            SelectedBbrParts.Clear();
            SelectedCsvParts.Clear();

            foreach (var row in _partRows)
            {
                if (_mode == DialogMode.Bbr)
                {
                    var bbrProp = row.BbrCombo?.SelectedItem as BbrPropertyDescriptor;
                    if (bbrProp == null)
                    {
                        MessageBox.Show("All parts must have a BBR property selected.",
                            "Incomplete", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    SelectedBbrParts.Add(bbrProp);
                }
                else // CSV mode
                {
                    var csvCol = row.CsvCombo?.SelectedItem as string;
                    var csvType = row.TypeCombo?.SelectedItem is BbrDataType dt ? dt : BbrDataType.String;
                    if (string.IsNullOrEmpty(csvCol))
                    {
                        MessageBox.Show("All parts must have a CSV column selected.",
                            "Incomplete", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    SelectedCsvParts.Add(new CsvKeyPart(csvCol, csvType));
                }
            }

            SelectedJoinMode = rbSpace.IsChecked == true
                ? KeyJoinMode.SpaceSeparated
                : KeyJoinMode.DirectConcat;

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private class PartRow
        {
            public ComboBox? BbrCombo { get; set; }
            public ComboBox? CsvCombo { get; set; }
            public ComboBox? TypeCombo { get; set; }
            public StackPanel? Panel { get; set; }
        }
    }
}
