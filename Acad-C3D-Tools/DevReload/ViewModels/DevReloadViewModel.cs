using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DevReload.ViewModels
{
    public partial class DevReloadViewModel : ObservableObject
    {
        private PluginConfig _config = new();

        public ObservableCollection<PluginItemViewModel> Plugins { get; } = new();

        [ObservableProperty] private bool _hasPlugins;
        [ObservableProperty] private bool _isAddingPlugin;

        // ── Add Plugin form fields ────────────────────────────────────

        [ObservableProperty] private string _newPluginName = "";
        [ObservableProperty] private string _newPluginPrefix = "";
        [ObservableProperty] private string _newPluginDll = "";
        [ObservableProperty] private string _newPluginSubfolder = "";
        [ObservableProperty] private string _newPluginVsProject = "";
        [ObservableProperty] private bool _newPluginHasCommands;
        [ObservableProperty] private bool _newPluginLoadOnStartup;

        // ── Construction ──────────────────────────────────────────────

        public DevReloadViewModel()
        {
            LoadFromConfig();
        }

        private void LoadFromConfig()
        {
            foreach (var item in Plugins)
                item.PropertyChanged -= OnPluginPropertyChanged;
            Plugins.Clear();

            _config = PluginConfigLoader.Load() ?? new PluginConfig();

            foreach (var entry in _config.Plugins)
            {
                var vm = new PluginItemViewModel(entry);
                vm.PropertyChanged += OnPluginPropertyChanged;
                vm.RefreshState();
                Plugins.Add(vm);
            }

            HasPlugins = Plugins.Count > 0;
        }

        private void OnPluginPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PluginItemViewModel.LoadOnStartup))
                SaveConfig();
        }

        // ── Plugin lifecycle commands ─────────────────────────────────

        [RelayCommand]
        private void LoadPlugin(string name)
        {
            PluginManager.Load(name);
            RefreshStates();
        }

        [RelayCommand]
        private void DevReloadPlugin(string name)
        {
            PluginManager.DevReload(name);
            RefreshStates();
        }

        [RelayCommand]
        private void UnloadPlugin(string name)
        {
            PluginManager.Unload(name);
            RefreshStates();
        }

        // ── Add / Remove ─────────────────────────────────────────────

        [RelayCommand]
        private void ShowAddPlugin()
        {
            NewPluginName = "";
            NewPluginPrefix = "";
            NewPluginDll = "";
            NewPluginSubfolder = "";
            NewPluginVsProject = "";
            NewPluginHasCommands = false;
            NewPluginLoadOnStartup = false;
            IsAddingPlugin = true;
        }

        [RelayCommand]
        private void ConfirmAddPlugin()
        {
            if (string.IsNullOrWhiteSpace(NewPluginName))
                return;

            string name = NewPluginName.Trim();

            // Prevent duplicates
            if (_config.Plugins.Any(p =>
                    p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return;

            var entry = new PluginEntry
            {
                Name = name,
                CommandPrefix = string.IsNullOrWhiteSpace(NewPluginPrefix)
                    ? null : NewPluginPrefix.Trim().ToUpperInvariant(),
                Dll = string.IsNullOrWhiteSpace(NewPluginDll) ? null : NewPluginDll.Trim(),
                Subfolder = string.IsNullOrWhiteSpace(NewPluginSubfolder)
                    ? null : NewPluginSubfolder.Trim(),
                VsProject = string.IsNullOrWhiteSpace(NewPluginVsProject)
                    ? null : NewPluginVsProject.Trim(),
                Commands = NewPluginHasCommands,
                LoadOnStartup = NewPluginLoadOnStartup,
            };

            _config.Plugins.Add(entry);
            SaveConfig();

            // Register with PluginManager + create LOAD/DEV/UNLOAD commands
            DevReloaderCommands.RegisterFromConfig(entry);

            var vm = new PluginItemViewModel(entry);
            vm.PropertyChanged += OnPluginPropertyChanged;
            vm.RefreshState();
            Plugins.Add(vm);
            HasPlugins = true;

            IsAddingPlugin = false;
        }

        [RelayCommand]
        private void CancelAddPlugin()
        {
            IsAddingPlugin = false;
        }

        [RelayCommand]
        private void RemovePlugin(string name)
        {
            var vm = Plugins.FirstOrDefault(p => p.Name == name);
            if (vm == null) return;

            // Unregister from PluginManager (tears down + removes commands)
            PluginManager.Unregister(name);

            // Remove from config + save
            _config.Plugins.RemoveAll(e =>
                e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            SaveConfig();

            vm.PropertyChanged -= OnPluginPropertyChanged;
            Plugins.Remove(vm);
            HasPlugins = Plugins.Count > 0;
        }

        // ── Reload Config ─────────────────────────────────────────────

        [RelayCommand]
        private void ReloadConfig()
        {
            // Unregister all current plugins
            foreach (var name in PluginManager.GetRegisteredPluginNames().ToList())
                PluginManager.Unregister(name);

            // Re-read config and register fresh
            LoadFromConfig();

            foreach (var entry in _config.Plugins)
            {
                if (!PluginManager.IsRegistered(entry.Name))
                    DevReloaderCommands.RegisterFromConfig(entry);
            }

            // Auto-load plugins with loadOnStartup
            foreach (var entry in _config.Plugins.Where(e => e.LoadOnStartup))
            {
                if (!PluginManager.IsLoaded(entry.Name))
                    PluginManager.Load(entry.Name);
            }

            RefreshStates();
        }

        // ── Helpers ───────────────────────────────────────────────────

        private void RefreshStates()
        {
            foreach (var p in Plugins)
                p.RefreshState();
        }

        private void SaveConfig()
        {
            PluginConfigLoader.Save(_config);
        }
    }

    // ══════════════════════════════════════════════════════════════════

    public partial class PluginItemViewModel : ObservableObject
    {
        internal readonly PluginEntry Entry;

        public string Name => Entry.Name;
        public string CommandPrefix => (Entry.CommandPrefix ?? Entry.Name).ToUpperInvariant();

        [ObservableProperty] private bool _isLoaded;
        [ObservableProperty] private string _status = "Unloaded";
        [ObservableProperty] private bool _loadOnStartup;

        public PluginItemViewModel(PluginEntry entry)
        {
            Entry = entry;
            _loadOnStartup = entry.LoadOnStartup;
        }

        partial void OnLoadOnStartupChanged(bool value)
        {
            Entry.LoadOnStartup = value;
        }

        public void RefreshState()
        {
            IsLoaded = PluginManager.IsRegistered(Name) && PluginManager.IsLoaded(Name);
            Status = IsLoaded ? "Loaded" : "Unloaded";
        }
    }

    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Simple IValueConverter that inverts a boolean.
    /// Used in XAML as {x:Static vm:InvertBoolConverter.Instance}.
    /// </summary>
    public class InvertBoolConverter : IValueConverter
    {
        public static readonly InvertBoolConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter,
            CultureInfo culture)
            => value is bool b ? !b : value;

        public object ConvertBack(object value, Type targetType, object parameter,
            CultureInfo culture)
            => value is bool b ? !b : value;
    }
}
