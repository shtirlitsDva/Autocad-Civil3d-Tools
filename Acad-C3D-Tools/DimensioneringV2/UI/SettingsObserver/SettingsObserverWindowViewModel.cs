using CommunityToolkit.Mvvm.ComponentModel;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.UI.SettingsObserver
{
    internal partial class SettingsObserverWindowViewModel : ObservableObject
    {
        public ObservableCollection<Entry> Entries { get; } = new();

        internal void Init(HydraulicSettings s)
        {
            foreach (var p in s.GetType().GetProperties())
                Entries.Add(new Entry(p.Name, p.GetValue(s)));

            s.PropertyChanged += (_, e) =>
            {
                var row = Entries.First(x => x.Name == e.PropertyName);
                row.Value = s.GetType().GetProperty(e.PropertyName)!.GetValue(s);
            };
        }

        public partial class Entry : ObservableObject
        {
            public string Name { get; }
            [ObservableProperty] private object? value;
            public Entry(string n, object? v) { Name = n; value = v; }
        }
    }
}
