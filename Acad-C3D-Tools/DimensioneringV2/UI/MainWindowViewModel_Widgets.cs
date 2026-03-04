using CommunityToolkit.Mvvm.ComponentModel;

using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Legend;
using DimensioneringV2.UI.ApplyDim;
using DimensioneringV2.UI.FeaturePopup;

using Mapsui;
using Mapsui.Styles;
using Mapsui.UI.Wpf;

namespace DimensioneringV2.UI
{
    internal partial class MainWindowViewModel
    {
        #region Feature popup
        private FeaturePopupWindow? _popupWindow;
        private readonly FeaturePopupViewModel _popupVm = new();

        public void OnMapInfo(object? sender, MapInfoEventArgs e)
        {
            if (_angivDim != null || _resetDim != null)
            {
                _popupWindow?.HidePopup();
                return;
            }

            if (e.MapInfo?.Feature is not AnalysisFeature feature)
            {
                _popupWindow?.HidePopup();
                return;
            }

            _popupVm.Update(feature);

            if (_popupWindow == null)
            {
                _popupWindow = new FeaturePopupWindow();
            }

            _popupWindow.ShowPopup(_popupVm);
        }
        #endregion

        #region Legend
        [ObservableProperty]
        private bool isLegendVisible;
        private LegendWidget? _legendWidget;
        private StatusWidget? _statusWidget;

        partial void OnIsLegendVisibleChanged(bool value)
        {
            UpdateLegendVisibility();
        }

        private void UpdateLegendVisibility()
        {
            if (_legendWidget == null) return;
            _legendWidget.Enabled = IsLegendVisible;
            _mymap.RefreshData();
        }
        #endregion

        #region Status widget helpers
        private void UpdateStatusWidget(string? instruction)
        {
            if (_statusWidget == null) return;
            if (instruction == null)
            {
                _statusWidget.Enabled = false;
            }
            else
            {
                _statusWidget.Content = BuildStatusContent(instruction);
                _statusWidget.Enabled = true;
            }
            Mymap?.RefreshData();
        }

        private static LegendElement BuildStatusContent(string instruction)
        {
            return new StackPanel
            {
                Background = new Color(40, 40, 40, 220),
                Padding = new Thickness(12, 8, 12, 8),
                Children =
                {
                    new TextBlock
                    {
                        Text = "Angiv Dim",
                        FontSize = 16,
                        Bold = true,
                        Color = new Color(255, 255, 255)
                    },
                    new Spacer { Height = 4 },
                    new TextBlock
                    {
                        Text = instruction,
                        FontSize = 13,
                        Bold = false,
                        Color = new Color(200, 200, 200)
                    }
                }
            };
        }

        private void OnAngivDimPhaseChanged(AngivDimPhase phase)
        {
            var text = phase switch
            {
                AngivDimPhase.PickFirst => "Tryk ESC for at afslutte.\nVælg start af strækning:",
                AngivDimPhase.PickSecond => "Tryk ESC for at gå tilbage.\nVælg slutning af strækning:",
                _ => ""
            };
            UpdateStatusWidget(text);
        }
        #endregion
    }
}
