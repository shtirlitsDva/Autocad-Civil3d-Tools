using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Windows.Media;

namespace DimensioneringV2.UI
{
    public partial class BlockTypeFilterItem : ObservableObject
    {
        private readonly string[] _typeNames;
        private readonly string _displayName;
        private readonly DrawingImage? _icon;

        [ObservableProperty]
        private bool isActive = true;

        public IReadOnlyList<string> TypeNames => _typeNames;
        public string DisplayName => _displayName;
        public DrawingImage? Icon => _icon;

        public BlockTypeFilterItem(string typeName, string displayName, string svgFileName, bool initiallyActive = true)
            : this(new[] { typeName }, displayName, svgFileName, initiallyActive)
        {
        }

        public BlockTypeFilterItem(string[] typeNames, string displayName, string svgFileName, bool initiallyActive = true)
        {
            _typeNames = typeNames;
            _displayName = displayName;
            _icon = EmbeddedSvgLoader.LoadSvg(svgFileName);
            isActive = initiallyActive;
        }

        public void Toggle()
        {
            IsActive = !IsActive;
        }
    }
}
