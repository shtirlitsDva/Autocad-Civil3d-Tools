using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Win32;

namespace NSLOAD.ViewModels
{
    public partial class NsLoadViewModel : ObservableObject
    {
        private NsLoadConfig _config = new();
        private Dictionary<string, string> _csvApps = new();

        public ObservableCollection<AppItemViewModel> PredefinedApps { get; } = new();
        public ObservableCollection<AppItemViewModel> UserPlugins { get; } = new();

        [ObservableProperty] private bool _hasPredefinedApps;
        [ObservableProperty] private bool _hasUserPlugins;
        [ObservableProperty] private bool _isAddingPlugin;
        [ObservableProperty] private string _newPluginName = "";
        [ObservableProperty] private string _newPluginDllPath = "";
        [ObservableProperty] private bool _newPluginLoadOnStartup;

        public NsLoadViewModel()
        {
        }

        public void Initialize(NsLoadConfig config, Dictionary<string, string> csvApps)
        {
            _config = config;
            _csvApps = csvApps;
            Refresh();
        }

        private void Refresh()
        {
            foreach (var item in PredefinedApps)
                item.PropertyChanged -= OnAppPropertyChanged;
            foreach (var item in UserPlugins)
                item.PropertyChanged -= OnAppPropertyChanged;

            PredefinedApps.Clear();
            UserPlugins.Clear();

            foreach (var entry in _config.PredefinedApps)
            {
                var vm = new AppItemViewModel(entry.DisplayName, true)
                {
                    AutoLoad = entry.AutoLoad,
                };
                vm.PropertyChanged += OnAppPropertyChanged;
                vm.RefreshState();
                PredefinedApps.Add(vm);
            }

            foreach (var entry in _config.Plugins)
            {
                var vm = new AppItemViewModel(entry.Name, false)
                {
                    AutoLoad = entry.LoadOnStartup,
                };
                vm.PropertyChanged += OnAppPropertyChanged;
                vm.RefreshState();
                UserPlugins.Add(vm);
            }

            HasPredefinedApps = PredefinedApps.Count > 0;
            HasUserPlugins = UserPlugins.Count > 0;
        }

        private void OnAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(AppItemViewModel.AutoLoad))
                return;

            if (sender is not AppItemViewModel vm)
                return;

            if (vm.IsPredefined)
            {
                var entry = _config.PredefinedApps
                    .FirstOrDefault(a => a.DisplayName == vm.Name);
                if (entry != null)
                    entry.AutoLoad = vm.AutoLoad;
            }
            else
            {
                var entry = _config.Plugins
                    .FirstOrDefault(p => p.Name == vm.Name);
                if (entry != null)
                    entry.LoadOnStartup = vm.AutoLoad;
            }

            NsLoadConfigLoader.Save(_config);
        }

        [RelayCommand]
        private void LoadApp(string name)
        {
            PluginManager.Load(name);
            RefreshStates();
        }

        [RelayCommand]
        private void UnloadApp(string name)
        {
            PluginManager.Unload(name);
            RefreshStates();
        }

        [RelayCommand]
        private void RemovePlugin(string name)
        {
            var vm = UserPlugins.FirstOrDefault(p => p.Name == name);
            if (vm == null) return;

            PluginManager.Unregister(name);

            _config.Plugins.RemoveAll(e =>
                e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            NsLoadConfigLoader.Save(_config);

            vm.PropertyChanged -= OnAppPropertyChanged;
            UserPlugins.Remove(vm);
            HasUserPlugins = UserPlugins.Count > 0;
        }

        [RelayCommand]
        private void ShowAddPlugin()
        {
            NewPluginName = "";
            NewPluginDllPath = "";
            NewPluginLoadOnStartup = false;
            IsAddingPlugin = true;
        }

        [RelayCommand]
        private void BrowseDll()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "DLL files (*.dll)|*.dll",
                Title = "Select plugin DLL",
            };
            if (dlg.ShowDialog() == true)
            {
                NewPluginDllPath = dlg.FileName;
                if (string.IsNullOrWhiteSpace(NewPluginName))
                    NewPluginName = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
            }
        }

        [RelayCommand]
        private void ConfirmAddPlugin()
        {
            if (string.IsNullOrWhiteSpace(NewPluginName) ||
                string.IsNullOrWhiteSpace(NewPluginDllPath))
                return;

            string name = NewPluginName.Trim();

            if (_config.Plugins.Any(p =>
                    p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return;

            var entry = new UserPluginEntry
            {
                Name = name,
                DllPath = NewPluginDllPath.Trim(),
                LoadOnStartup = NewPluginLoadOnStartup,
            };

            _config.Plugins.Add(entry);
            NsLoadConfigLoader.Save(_config);

            PluginManager.Register(name)
                .WithDllPath(entry.DllPath)
                .WithCommands()
                .Commit();

            var vm = new AppItemViewModel(name, false)
            {
                AutoLoad = entry.LoadOnStartup,
            };
            vm.PropertyChanged += OnAppPropertyChanged;
            vm.RefreshState();
            UserPlugins.Add(vm);
            HasUserPlugins = true;

            IsAddingPlugin = false;
        }

        [RelayCommand]
        private void CancelAddPlugin()
        {
            IsAddingPlugin = false;
        }

        private void RefreshStates()
        {
            foreach (var p in PredefinedApps) p.RefreshState();
            foreach (var p in UserPlugins) p.RefreshState();
        }
    }

    public partial class AppItemViewModel : ObservableObject
    {
        public string Name { get; }
        public bool IsPredefined { get; }

        [ObservableProperty] private bool _isLoaded;
        [ObservableProperty] private string _status = "Unloaded";
        [ObservableProperty] private bool _autoLoad;

        public AppItemViewModel(string name, bool isPredefined)
        {
            Name = name;
            IsPredefined = isPredefined;
        }

        public void RefreshState()
        {
            IsLoaded = PluginManager.IsRegistered(Name) && PluginManager.IsLoaded(Name);
            Status = IsLoaded ? "Loaded" : "Unloaded";
        }
    }

    public class InvertBoolConverter : IValueConverter
    {
        public static readonly InvertBoolConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;
    }
}
