using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace DimensioneringV2.UI
{
    public partial class BlockTypeFilterItem : ObservableObject
    {
        private readonly string _typeName;
        private readonly string _displayName;
        private readonly string _iconKey;

        [ObservableProperty]
        private bool isActive = true;

        public string TypeName => _typeName;
        public string DisplayName => _displayName;
        public string IconKey => _iconKey;

        public BlockTypeFilterItem(string typeName, string displayName, string iconKey)
        {
            _typeName = typeName;
            _displayName = displayName;
            _iconKey = iconKey;
        }

        public void Toggle()
        {
            IsActive = !IsActive;
        }
    }
}
