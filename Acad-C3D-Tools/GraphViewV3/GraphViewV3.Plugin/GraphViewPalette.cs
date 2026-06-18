using Autodesk.AutoCAD.Windows;

namespace GraphViewV3;

/// <summary>
/// The dockable palette. Hosts three WPF visuals — Graph, Statistics, Settings — each via
/// AddVisual, so AutoCAD renders them as native side tabs. All share one GraphViewModel +
/// GraphSettings, so the single live loop drives every tab. WPF runs on AutoCAD's main thread;
/// heavy work is off-thread in <see cref="LiveGraphModel"/>. Terminate() tears down the loop,
/// the settings hook, and both disposable controls so the assembly fully unpins for hot-reload.
/// </summary>
internal sealed class GraphViewPalette : PaletteSet
{
    private static readonly Guid PaletteGuid = new("3F9C7A20-7C2E-4E2B-9C1D-A1B2C3D4E5F6");

    private readonly GraphSettings _settings;
    private readonly GraphTabControl _graph;
    private readonly StatsTabControl _stats;
    private readonly LiveGraphModel _live;

    public GraphViewPalette()
        : base("Live Network Graph", "GRAPHVIEWV3", PaletteGuid)
    {
        Style = PaletteSetStyles.ShowCloseButton
              | PaletteSetStyles.ShowAutoHideButton
              | PaletteSetStyles.ShowTabForSingle;
        MinimumSize = new System.Drawing.Size(320, 240);

        var vm = new GraphViewModel();
        _settings = new GraphSettings();

        _graph = new GraphTabControl(vm, _settings);
        _stats = new StatsTabControl(vm);
        var settingsTab = new SettingsTabControl(_settings);

        AddVisual("Graph", _graph);
        AddVisual("Statistics", _stats);
        AddVisual("Settings", settingsTab);

        _live = new LiveGraphModel(vm, _graph.Dispatcher, _settings.Tolerance);
        _settings.Changed += OnSettingsChanged;
        _live.Start();
    }

    private void OnSettingsChanged() => _live.SetTolerance(_settings.Tolerance);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _settings.Changed -= OnSettingsChanged;
            _live.Dispose();
            _graph.Dispose();
            _stats.Dispose();
        }
        base.Dispose(disposing);
    }
}
