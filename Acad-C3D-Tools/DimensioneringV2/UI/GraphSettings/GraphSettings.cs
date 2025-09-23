using CommunityToolkit.Mvvm.ComponentModel;

using System;
using System.Linq;

namespace DimensioneringV2.UI.Graph
{
    public partial class GraphSettings : ObservableObject
    {
        [ObservableProperty]
        private bool showMinimumHoldetrykLineAndLabel = true;

        [ObservableProperty]
        private bool showTillægTilHoldetrykLineAndLabel = true;

        internal void CopyFrom(GraphSettings src)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));

            foreach (var p in typeof(GraphSettings)
                              .GetProperties(System.Reflection.BindingFlags.Instance |
                                             System.Reflection.BindingFlags.Public)
                              .Where(pr => pr.CanRead && pr.CanWrite && pr.GetIndexParameters().Length == 0))
            {
                p.SetValue(this, p.GetValue(src));
            }
        }
    }
}