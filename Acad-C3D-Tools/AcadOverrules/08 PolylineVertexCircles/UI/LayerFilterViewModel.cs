using CommunityToolkit.Mvvm.ComponentModel;

namespace AcadOverrules.VertexCircles.UI
{
    /// <summary>
    /// One editable row in the layer filter list - either a plain layer name or a mask.
    /// </summary>
    public partial class LayerFilterViewModel : ObservableObject
    {
        [ObservableProperty]
        private string pattern = string.Empty;

        public LayerFilterViewModel()
        {
        }

        public LayerFilterViewModel(string pattern)
        {
            this.pattern = pattern;
        }
    }
}
