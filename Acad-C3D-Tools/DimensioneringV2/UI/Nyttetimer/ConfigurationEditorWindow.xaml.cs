using CommunityToolkit.Mvvm.ComponentModel;

using DimensioneringV2.Models.Nyttetimer;

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DimensioneringV2.UI.Nyttetimer
{
    public partial class ConfigurationEditorWindow : Window
    {
        private readonly ConfigurationEditorViewModel _viewModel;

        public ConfigurationEditorWindow(NyttetimerConfiguration configuration)
        {
            InitializeComponent();
            _viewModel = new ConfigurationEditorViewModel(configuration);
            DataContext = _viewModel;
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _viewModel.FilterText = SearchTextBox.Text;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public partial class ConfigurationEditorViewModel : ObservableObject
    {
        public NyttetimerConfiguration Configuration { get; }

        [ObservableProperty]
        private string filterText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<NyttetimerEntry> filteredEntries;

        public ConfigurationEditorViewModel(NyttetimerConfiguration configuration)
        {
            Configuration = configuration;
            filteredEntries = new ObservableCollection<NyttetimerEntry>(configuration.Entries ?? []);
        }

        partial void OnFilterTextChanged(string value)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(FilterText))
            {
                FilteredEntries = new ObservableCollection<NyttetimerEntry>(Configuration.Entries);
            }
            else
            {
                var searchTerm = FilterText.ToLowerInvariant();
                var filtered = Configuration.Entries.Where(e =>
                    e.AnvendelsesKode.ToLowerInvariant().Contains(searchTerm) ||
                    (e.AnvendelsesTekst?.ToLowerInvariant().Contains(searchTerm) ?? false) ||
                    e.Nyttetimer.ToString().Contains(searchTerm));
                FilteredEntries = new ObservableCollection<NyttetimerEntry>(filtered);
            }
        }
    }
}

