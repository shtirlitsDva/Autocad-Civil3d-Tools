﻿using CommunityToolkit.Mvvm.ComponentModel;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DimensioneringV2.UI
{
    internal partial class BruteForceProgressViewModel : ObservableObject
    {
        public Dispatcher Dispatcher { get; set; }

        [ObservableProperty]
        private int round;

        [ObservableProperty]
        private int bridges;

        [ObservableProperty]
        private int removalCandidates;

        [ObservableProperty]
        private int currentCandidate;

        [ObservableProperty]
        private bool stopRequested;

        public void UpdateRound(int round)
        {
            Dispatcher.Invoke(() =>
            {
                Round = round;
            });
        }

        public void UpdateBridges(int bridges)
        {
            Dispatcher.Invoke(() =>
            {
                Bridges = bridges;
            });
        }

        public void UpdateRemovalCandidates(int removalCandidates)
        {
            Dispatcher.Invoke(() =>
            {
                RemovalCandidates = removalCandidates;
            });
        }

        public void UpdateCurrentCandidate(int currentCandidate)
        {
            Dispatcher.Invoke(() =>
            {
                CurrentCandidate = currentCandidate;
            });
        }
    }
}
