using System;
using System.Windows.Media;
using System.Windows.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace IntersectUtilities.GraphWriteV2.Theming.UI
{
    /// <summary>
    /// Backs the Label Theme Designer window. Holds the working <see cref="LabelTheme"/> as flat,
    /// bindable state; any change re-renders a single hardcoded sample label through Graphviz
    /// (debounced). The six exposed colors plus the three Series colors mirror into a master
    /// <see cref="ThemeColors"/>, so the colors the window does not expose (Fluent/Segmented tints)
    /// are preserved and editing any visible color flips the active preset to "Custom".
    /// </summary>
    internal sealed class LabelThemeDesignerViewModel : ObservableObject
    {
        // The hardcoded preview sample (matches the design package's reference node).
        private const string SampleId = "32D2";
        private const string SampleType = "PertFlextra Twin 63";
        private const string SampleDesc = "Rør L74.39";
        private const int SampleSeries = 2;

        private readonly Dispatcher _dispatcher;
        private readonly DispatcherTimer _debounce;
        private readonly DispatcherTimer _toastTimer;

        private ThemeColors _colors;
        private PalettePreset? _preset;
        private PalettePreset _lastPreset = PalettePreset.Cyan;

        private LabelStyle _style;
        private SegmentMode _tileMode;
        private bool _showSeries, _chipFilled, _chipBefore, _rounded;
        private string _idFont, _bodyFont;
        private int _padding;

        private ImageSource? _previewImage;
        private string? _previewError;
        private string _toast = "";

        public LabelThemeDesignerViewModel(LabelTheme theme)
        {
            _dispatcher = Dispatcher.CurrentDispatcher;

            _colors = theme.Colors.Clone();
            _style = theme.Style;
            _tileMode = theme.TileMode;
            _showSeries = theme.Series.Show;
            _chipFilled = theme.Series.Fill == ChipFill.Filled;
            _chipBefore = theme.Series.Pos == ChipPos.Before;
            _idFont = theme.Fonts.Id;
            _bodyFont = theme.Fonts.Body;
            _padding = theme.Padding;
            _rounded = theme.Rounded;
            _preset = DetectPreset(_colors);
            _lastPreset = _preset ?? PalettePreset.Cyan;

            SelectStyleCommand = new RelayCommand<LabelStyle>(s => Style = s);
            SelectTileModeCommand = new RelayCommand<SegmentMode>(m => TileMode = m);
            SelectPresetCommand = new RelayCommand<PalettePreset>(ApplyPreset);
            ToggleSeriesCommand = new RelayCommand(() => ShowSeries = !ShowSeries);
            ToggleRoundedCommand = new RelayCommand(() => Rounded = !Rounded);
            ResetCommand = new RelayCommand(() => ApplyPreset(_lastPreset));
            CopyDotCommand = new RelayCommand(CopyDot);

            _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            _debounce.Tick += (_, _) => { _debounce.Stop(); RenderPreview(); };
            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1600) };
            _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); Toast = ""; };
        }

        // ---------- commands ----------
        public RelayCommand<LabelStyle> SelectStyleCommand { get; }
        public RelayCommand<SegmentMode> SelectTileModeCommand { get; }
        public RelayCommand<PalettePreset> SelectPresetCommand { get; }
        public RelayCommand ToggleSeriesCommand { get; }
        public RelayCommand ToggleRoundedCommand { get; }
        public RelayCommand ResetCommand { get; }
        public RelayCommand CopyDotCommand { get; }

        // ---------- style / layout ----------
        public LabelStyle Style
        {
            get => _style;
            set { if (SetProperty(ref _style, value)) { OnPropertyChanged(nameof(IsSegmented)); ScheduleRefresh(); } }
        }
        public bool IsSegmented => _style == LabelStyle.Segmented;

        public SegmentMode TileMode
        {
            get => _tileMode;
            set { if (SetProperty(ref _tileMode, value)) ScheduleRefresh(); }
        }

        public int Padding
        {
            get => _padding;
            set { if (SetProperty(ref _padding, value)) ScheduleRefresh(); }
        }

        public bool Rounded
        {
            get => _rounded;
            set { if (SetProperty(ref _rounded, value)) ScheduleRefresh(); }
        }

        public string IdFont
        {
            get => _idFont;
            set { if (SetProperty(ref _idFont, value)) ScheduleRefresh(); }
        }
        public string BodyFont
        {
            get => _bodyFont;
            set { if (SetProperty(ref _bodyFont, value)) ScheduleRefresh(); }
        }

        // ---------- series ----------
        public bool ShowSeries
        {
            get => _showSeries;
            set { if (SetProperty(ref _showSeries, value)) ScheduleRefresh(); }
        }
        public bool ChipFilled
        {
            get => _chipFilled;
            set { if (SetProperty(ref _chipFilled, value)) ScheduleRefresh(); }
        }
        public bool ChipBefore
        {
            get => _chipBefore;
            set { if (SetProperty(ref _chipBefore, value)) ScheduleRefresh(); }
        }

        // ---------- palette ----------
        public PalettePreset? Preset
        {
            get => _preset;
            private set { if (SetProperty(ref _preset, value)) OnPropertyChanged(nameof(PresetName)); }
        }
        public string PresetName => _preset is null ? "Custom" : LabelThemePresets.DisplayName(_preset.Value);

        // Editable colors (bound one-way; the ColorDialog drives them via SetColor).
        public string Frame => _colors.Frame;
        public string Fill1 => _colors.Fill1;
        public string Fill2 => _colors.Fill2;
        public string IdColor => _colors.Id;
        public string BodyColor => _colors.Body;
        public string Divider => _colors.Divider;
        public string Series1 => _colors.Chip1;
        public string Series2 => _colors.Chip2;
        public string Series3 => _colors.Chip3;

        /// <summary>Called from the window after a ColorDialog pick. key is the property name.</summary>
        public void SetColor(string key, string hex)
        {
            switch (key)
            {
                case nameof(Frame): _colors.Frame = hex; break;
                case nameof(Fill1): _colors.Fill1 = hex; break;
                case nameof(Fill2): _colors.Fill2 = hex; break;
                case nameof(IdColor): _colors.Id = hex; break;
                case nameof(BodyColor): _colors.Body = hex; break;
                case nameof(Divider): _colors.Divider = hex; break;
                case nameof(Series1): _colors.Chip1 = hex; break;
                case nameof(Series2): _colors.Chip2 = hex; break;
                case nameof(Series3): _colors.Chip3 = hex; break;
                default: return;
            }
            OnPropertyChanged(key);
            MarkCustom();
            ScheduleRefresh();
        }

        private void ApplyPreset(PalettePreset p)
        {
            _colors = LabelThemePresets.Colors(p).Clone();
            _lastPreset = p;
            Preset = p;
            RaiseAllColors();
            ScheduleRefresh();
        }

        private void MarkCustom()
        {
            if (_preset is not null) Preset = null;
        }

        private void RaiseAllColors()
        {
            OnPropertyChanged(nameof(Frame));
            OnPropertyChanged(nameof(Fill1));
            OnPropertyChanged(nameof(Fill2));
            OnPropertyChanged(nameof(IdColor));
            OnPropertyChanged(nameof(BodyColor));
            OnPropertyChanged(nameof(Divider));
            OnPropertyChanged(nameof(Series1));
            OnPropertyChanged(nameof(Series2));
            OnPropertyChanged(nameof(Series3));
        }

        // ---------- preview ----------
        public ImageSource? PreviewImage
        {
            get => _previewImage;
            private set => SetProperty(ref _previewImage, value);
        }
        public string? PreviewError
        {
            get => _previewError;
            private set => SetProperty(ref _previewError, value);
        }
        public string Toast
        {
            get => _toast;
            private set => SetProperty(ref _toast, value);
        }

        public void Start() => RenderPreview();

        private void ScheduleRefresh()
        {
            _debounce.Stop();
            _debounce.Start();
        }

        private void RenderPreview()
        {
            string markup = new LabelMarkupBuilder(BuildTheme())
                .Build(SampleId, SampleType, SampleDesc, SampleSeries);

            System.Threading.Tasks.Task.Run(() =>
            {
                var bmp = GraphvizPreviewRenderer.Render(markup, out var err);
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    if (bmp is not null)
                    {
                        PreviewImage = bmp;
                        PreviewError = null;
                    }
                    else
                    {
                        PreviewError = err;
                    }
                }));
            });
        }

        private void CopyDot()
        {
            string markup = new LabelMarkupBuilder(BuildTheme())
                .Build(SampleId, SampleType, SampleDesc, SampleSeries);
            try
            {
                System.Windows.Clipboard.SetText(markup);
                ShowToast("DOT copied to clipboard");
            }
            catch
            {
                ShowToast("Copy failed");
            }
        }

        private void ShowToast(string msg)
        {
            Toast = msg;
            _toastTimer.Stop();
            _toastTimer.Start();
        }

        // ---------- theme assembly ----------
        public LabelTheme BuildTheme() => new()
        {
            Style = _style,
            TileMode = _tileMode,
            Colors = _colors.Clone(),
            Series = new SeriesStyle
            {
                Show = _showSeries,
                Fill = _chipFilled ? ChipFill.Filled : ChipFill.Outline,
                Pos = _chipBefore ? ChipPos.Before : ChipPos.After,
            },
            Fonts = new FontSet { Id = _idFont, Body = _bodyFont },
            Padding = _padding,
            Rounded = _rounded,
        };

        private static PalettePreset? DetectPreset(ThemeColors c)
        {
            foreach (var p in LabelThemePresets.All)
                if (SamePalette(c, LabelThemePresets.Colors(p))) return p;
            return null;
        }

        private static bool SamePalette(ThemeColors a, ThemeColors b) =>
            a.Background == b.Background && a.Fill1 == b.Fill1 && a.Fill2 == b.Fill2 &&
            a.FillLite1 == b.FillLite1 && a.FillLite2 == b.FillLite2 && a.Frame == b.Frame &&
            a.Divider == b.Divider && a.Id == b.Id && a.Type == b.Type && a.Body == b.Body &&
            a.ChipText == b.ChipText && a.Chip1 == b.Chip1 && a.Chip2 == b.Chip2 && a.Chip3 == b.Chip3 &&
            a.TileA1 == b.TileA1 && a.TileA2 == b.TileA2 && a.Ink == b.Ink && a.IdInk == b.IdInk;
    }
}
