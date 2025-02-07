using CommunityToolkit.Mvvm.ComponentModel;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.UI
{
    public abstract partial class GraphCalculationBaseViewModel : ObservableObject
    {
        [ObservableProperty]
        private string? title;

        [ObservableProperty]
        private int nodeCount;

        [ObservableProperty]
        private int edgeCount;

        [ObservableProperty]
        private string? nonBridgesCount;
    }
}
