using CommunityToolkit.Mvvm.Input;

using DimensioneringV2.AutoCAD;
using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Services;
using DimensioneringV2.Services.SubGraphs;
using DimensioneringV2.UI.ApplyDim;
using DimensioneringV2.UI.MapOverlay;
using DimensioneringV2.UI.MapProperty;

using NorsynHydraulicCalc;

using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.UI
{
    internal partial class MainWindowViewModel
    {
        #region Angiv dim command
        public RelayCommand AngivDimCommand => new(AngivDim);
        private ApplyDimManager? _angivDim;
        private ApplyDimOverlayManager _overlayManager;
        private NorsynHydraulicCalc.Pipes.PipeTypes? _pipes;
        private void AngivDim()
        {
            if (_angivDim != null)
            {
                // Toggle off — Stop() fires Stopped → OnAngivDimStopped handles all cleanup
                _angivDim.Stop();
                return;
            }

            if (_dataService?.Graphs == null || !_dataService.Graphs.Any()) return;

            _pipes = new NorsynHydraulicCalc.Pipes.PipeTypes(HydraulicSettingsService.Instance.Settings);

            _prevSelectedProperty = SelectedMapPropertyWrapper;
            SelectedMapPropertyWrapper = new MapPropertyWrapper(MapPropertyEnum.Pipe, "Rørdimension");
            UpdateMap();

            _overlayManager ??= new ApplyDimOverlayManager(Mymap);
            _angivDim = new ApplyDimManager(_mapControl, _dataService.Graphs, _overlayManager);
            _angivDim.PathFinalized += OnAngivPathFinalized;
            _angivDim.Stopped += OnAngivDimStopped;
            _angivDim.PhaseChanged += OnAngivDimPhaseChanged;
            _angivDim.Start();

            UpdateStatusWidget("Tryk ESC for at afslutte.\nVælg start af strækning:");
        }

        private void OnAngivDimStopped()
        {
            _pipes = null;
            _angivDim = null;
            UpdateStatusWidget(null);
            SelectedMapPropertyWrapper = _prevSelectedProperty;

            RecalculateHydraulicsAfterDimChange();
            UpdateMap();
        }

        private void OnAngivPathFinalized(IEnumerable<AnalysisFeature> features)
        {
            UpdateStatusWidget("Vælg rørdimension i dialogboksen.");

            var dlg = new DimensioneringV2.UI.Dialogs.SelectPipeDimDialog();
            dlg.Owner = System.Windows.Application.Current?.MainWindow;
            dlg.ShowDialog();

            if (dlg.ResultReason == DimensioneringV2.UI.Dialogs.SelectPipeDimViewModel.CloseReason.Retry)
            {
                _angivDim?.RetryKeepFirst();
                UpdateStatusWidget("Tryk ESC for at gå tilbage.\nVælg slutning af strækning:");
                return;
            }
            if (dlg.ResultReason != DimensioneringV2.UI.Dialogs.SelectPipeDimViewModel.CloseReason.Ok)
            {
                // Cancel — Stop() fires Stopped → OnAngivDimStopped handles all cleanup
                _angivDim?.Stop();
                return;
            }

            // Apply selected dim to features
            var settings = Services.HydraulicSettingsService.Instance.Settings;
            var selectedType = dlg.ViewModel.SelectedPipeType;
            var selectedNominal = dlg.ViewModel.SelectedNominal;

            if (_pipes == null)
                _pipes = new NorsynHydraulicCalc.Pipes.PipeTypes(settings);
            var dim = _pipes.GetPipeType(selectedType).GetDim(selectedNominal);

            foreach (var f in features)
            {
                if (f.ManualDim)
                {
                    f.Dim = dim;
                }
                else
                {
                    f.PreviousDim = f.Dim;
                    f.Dim = dim;
                    f.ManualDim = true;
                }
            }

            // Refresh theme and reset to first selection
            _themeManager.SetTheme(MapPropertyEnum.Pipe);
            UpdateMap();
            _angivDim?.ResetToFirstSelection();
            UpdateStatusWidget("Tryk ESC for at afslutte.\nVælg start af strækning:");
        }
        #endregion

        #region Reset dim command
        public RelayCommand ResetDimCommand => new(ResetDim);
        private ResetDimManager? _resetDim;
        private void ResetDim()
        {
            if (_resetDim != null)
            {
                // Toggle off — Stop() fires Stopped → OnResetDimStopped handles all cleanup
                _resetDim.Stop();
                return;
            }

            if (_dataService?.Graphs == null ||
                !_dataService.Graphs.Any() ||
                !_dataService.Graphs.SelectMany(x => x.Edges).Any(x => x.PipeSegment.ManualDim)) return;

            _prevSelectedProperty = SelectedMapPropertyWrapper;
            SelectedMapPropertyWrapper = new MapPropertyWrapper(MapPropertyEnum.ManualDim, "Manuel dimension");
            UpdateMap();

            _resetDim = new ResetDimManager(_mapControl);
            _resetDim.Finalized += OnResetDimFinalized;
            _resetDim.Stopped += OnResetDimStopped;
            _resetDim.Start();
        }

        private void OnResetDimStopped()
        {
            _resetDim = null;
            SelectedMapPropertyWrapper = _prevSelectedProperty;

            RecalculateHydraulicsAfterDimChange();
            UpdateMap();
        }

        private void OnResetDimFinalized(AnalysisFeature feature)
        {
            if (!feature.ManualDim) return;
            feature.ManualDim = false;
            feature.Dim = feature.PreviousDim;
            UpdateMap();

            //Handle none manual dims left
            if (!_dataService.Graphs.SelectMany(x => x.Edges).Any(x => x.PipeSegment.ManualDim))
            {
                _resetDim?.Stop();
            }
        }
        #endregion

        #region Shared hydraulic recalculation
        private void RecalculateHydraulicsAfterDimChange()
        {
            var graphs = DataService.Instance.Graphs;
            var hc = new HydraulicCalc(
                HydraulicSettingsService.Instance.Settings,
                new LoggerFile());

            foreach (var ograph in graphs)
            {
                var graph = ograph.CopyToBFConditional(
                    x => x.PipeSegment.NumberOfBuildingsSupplied > 0);

                foreach (var edge in graph.Edges)
                {
                    edge.YankAllResults();

                    switch (edge.SegmentType)
                    {
                        case SegmentType.Fordelingsledning:
                            edge.ApplyResult(hc.CalculateDistributionSegment(edge));
                            edge.PushAllResults();
                            break;
                        case SegmentType.Stikledning:
                            edge.ApplyResult(hc.CalculateClientSegment(edge));
                            edge.PushAllResults();
                            break;
                    }
                }
            }

            foreach (var graph in graphs)
                PressureAnalysisService.CalculateDifferentialLossAtClient(graph);
        }
        #endregion
    }
}
