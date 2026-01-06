using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DimensioneringV2.Models.Nyttetimer;
using DimensioneringV2.Services;

using Microsoft.Win32;

using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace DimensioneringV2.UI.Nyttetimer
{
    public partial class ConfigurationSelectorViewModel : ObservableObject
    {
        private readonly NyttetimerService _service;
        private readonly Action _closeWindow;
        private string _originalSelection;

        [ObservableProperty]
        private NyttetimerConfiguration? selectedConfiguration;

        public ObservableCollection<NyttetimerConfiguration> Configurations => _service.AllConfigurations;

        public bool CanEditOrView => SelectedConfiguration != null;
        public bool CanRename => SelectedConfiguration != null && !SelectedConfiguration.IsDefault;
        public bool CanDelete => SelectedConfiguration != null && !SelectedConfiguration.IsDefault;
        public bool CanDuplicate => SelectedConfiguration != null;
        public bool CanExport => SelectedConfiguration != null;

        public ConfigurationSelectorViewModel(Action closeWindow)
        {
            _service = NyttetimerService.Instance;
            _closeWindow = closeWindow;
            _originalSelection = _service.CurrentConfiguration.Name;
            selectedConfiguration = _service.CurrentConfiguration;
        }

        partial void OnSelectedConfigurationChanged(NyttetimerConfiguration? value)
        {
            OnPropertyChanged(nameof(CanEditOrView));
            OnPropertyChanged(nameof(CanRename));
            OnPropertyChanged(nameof(CanDelete));
            OnPropertyChanged(nameof(CanDuplicate));
            OnPropertyChanged(nameof(CanExport));
        }

        [RelayCommand]
        private void New()
        {
            var dialog = new NewConfigurationDialog(_service.AllConfigurations);
            
            if (dialog.ShowDialog() == true && dialog.SelectedTemplate != null)
            {
                var newConfig = _service.CreateNew(dialog.ConfigurationName, dialog.SelectedTemplate);
                SelectedConfiguration = newConfig;
            }
        }

        [RelayCommand]
        private void Duplicate()
        {
            if (SelectedConfiguration == null) return;
            
            var newConfig = _service.Duplicate(SelectedConfiguration);
            SelectedConfiguration = newConfig;
        }

        [RelayCommand]
        private void Rename()
        {
            if (SelectedConfiguration == null || SelectedConfiguration.IsDefault) return;

            var dialog = new RenameDialog(SelectedConfiguration.Name);
            
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.NewName))
            {
                if (!_service.Rename(SelectedConfiguration, dialog.NewName))
                {
                    MessageBox.Show("Kunne ikke omdøbe konfigurationen. Navnet findes muligvis allerede.",
                        "Fejl", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        [RelayCommand]
        private void Edit()
        {
            if (SelectedConfiguration == null) return;

            var editor = new ConfigurationEditorWindow(SelectedConfiguration);
            editor.ShowDialog();
        }

        [RelayCommand]
        private void Delete()
        {
            if (SelectedConfiguration == null || SelectedConfiguration.IsDefault) return;

            var result = MessageBox.Show(
                $"Er du sikker på, at du vil slette konfigurationen '{SelectedConfiguration.Name}'?",
                "Bekræft sletning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _service.Delete(SelectedConfiguration);
                SelectedConfiguration = _service.DefaultConfiguration;
            }
        }

        [RelayCommand]
        private void ImportCsv()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "CSV filer (*.csv)|*.csv|Alle filer (*.*)|*.*",
                Title = "Importer konfiguration fra CSV"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var config = _service.ImportFromCsv(dialog.FileName);
                    SelectedConfiguration = config;
                    MessageBox.Show($"Konfigurationen '{config.Name}' blev importeret.",
                        "Import fuldført", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fejl ved import: {ex.Message}",
                        "Fejl", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void ImportDwg()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "AutoCAD tegninger (*.dwg)|*.dwg|Alle filer (*.*)|*.*",
                Title = "Importer konfigurationer fra DWG"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var imported = _service.ImportFromDwg(dialog.FileName);
                    if (imported.Count > 0)
                    {
                        SelectedConfiguration = imported[0];
                        MessageBox.Show($"{imported.Count} konfiguration(er) blev importeret.",
                            "Import fuldført", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Ingen konfigurationer fundet i den valgte tegning.",
                            "Import", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fejl ved import: {ex.Message}",
                        "Fejl", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void Export()
        {
            if (SelectedConfiguration == null) return;

            var dialog = new SaveFileDialog
            {
                Filter = "CSV filer (*.csv)|*.csv|Alle filer (*.*)|*.*",
                Title = "Eksporter konfiguration til CSV",
                FileName = SelectedConfiguration.Name
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _service.ExportToCsv(SelectedConfiguration, dialog.FileName);
                    MessageBox.Show($"Konfigurationen blev eksporteret til '{dialog.FileName}'.",
                        "Eksport fuldført", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fejl ved eksport: {ex.Message}",
                        "Fejl", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void Ok()
        {
            if (SelectedConfiguration != null)
            {
                _service.SelectConfiguration(SelectedConfiguration.Name);
                _service.SaveToActiveDocument();
            }
            _closeWindow();
        }

        [RelayCommand]
        private void Cancel()
        {
            // Restore original selection
            _service.SelectConfiguration(_originalSelection);
            _closeWindow();
        }
    }
}

