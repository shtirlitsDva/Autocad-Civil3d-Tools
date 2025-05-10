using CommunityToolkit.Mvvm.ComponentModel;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

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

        [ObservableProperty]
        private double cost;

        [ObservableProperty]
        private int timeToEnumerate;

        [ObservableProperty]
        private int remainingTime;

        [ObservableProperty]
        private bool showCountdownOverlay = true;

        private Timer? countdownTimer;
        internal void StartCountdown(Dispatcher dispatcher)
        {
            countdownTimer = new Timer(_ =>
            {
                dispatcher.Invoke(() =>
                {
                    if (RemainingTime > 0)
                    {
                        RemainingTime--;
                    }
                    else
                    {
                        ShowCountdownOverlay = false;
                        countdownTimer?.Dispose();
                    }
                });
            }, null, 0, 1000); // Start immediately, tick every 1 sec
        }
    }
}