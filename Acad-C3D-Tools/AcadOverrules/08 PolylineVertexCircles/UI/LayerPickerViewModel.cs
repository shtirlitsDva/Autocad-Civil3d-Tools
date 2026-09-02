using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace AcadOverrules.VertexCircles.UI
{
    /// <summary>One layer of the active drawing, tickable in the picker.</summary>
    public partial class LayerChoiceViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool isSelected;

        public LayerChoiceViewModel(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    /// <summary>
    /// Lists the layers of the active drawing so the user can tick the ones the overrule
    /// should apply to, instead of typing layer names by hand.
    /// </summary>
    public partial class LayerPickerViewModel : ObservableObject
    {
        [ObservableProperty]
        private string filterText = string.Empty;

        private readonly List<LayerChoiceViewModel> _allLayers;

        public LayerPickerViewModel()
        {
            _allLayers = ReadLayerNames()
                .Select(n => new LayerChoiceViewModel(n))
                .ToList();

            VisibleLayers = new ObservableCollection<LayerChoiceViewModel>(_allLayers);
        }

        public ObservableCollection<LayerChoiceViewModel> VisibleLayers { get; }

        public IReadOnlyList<string> SelectedLayerNames =>
            _allLayers.Where(l => l.IsSelected).Select(l => l.Name).ToList();

        public bool HasLayers => _allLayers.Count > 0;

        [RelayCommand]
        private void SelectAllVisible()
        {
            foreach (LayerChoiceViewModel layer in VisibleLayers)
                layer.IsSelected = true;
        }

        [RelayCommand]
        private void ClearSelection()
        {
            foreach (LayerChoiceViewModel layer in _allLayers)
                layer.IsSelected = false;
        }

        partial void OnFilterTextChanged(string value)
        {
            VisibleLayers.Clear();

            foreach (LayerChoiceViewModel layer in _allLayers)
            {
                if (!string.IsNullOrWhiteSpace(value) &&
                    layer.Name.IndexOf(value.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                VisibleLayers.Add(layer);
            }
        }

        private static IEnumerable<string> ReadLayerNames()
        {
            var names = new List<string>();

            Document? doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return names;

            using (Transaction tx = doc.Database.TransactionManager.StartOpenCloseTransaction())
            {
                var layerTable = (LayerTable)tx.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);

                foreach (Autodesk.AutoCAD.DatabaseServices.ObjectId id in layerTable)
                {
                    var layer = (LayerTableRecord)tx.GetObject(id, OpenMode.ForRead);
                    names.Add(layer.Name);
                }

                tx.Commit();
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }
    }
}
