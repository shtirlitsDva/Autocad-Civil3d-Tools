using Autodesk.AutoCAD.ApplicationServices;

using CommunityToolkit.Mvvm.ComponentModel;

using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Models;
using DimensioneringV2.StateMachine;

using EventManager;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;

using AcAp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace DimensioneringV2.Services;

internal partial class HydraulicNetworkManager : ObservableObject
{
    private static HydraulicNetworkManager? _instance;
    public static HydraulicNetworkManager Instance => _instance ??= new HydraulicNetworkManager(Commands.Events!);

    internal static void Reset()
    {
        if (_instance != null)
        {
            _instance._events.DocumentActivated -= _instance.OnDocumentActivated;
            _instance._events.DocumentToBeDeactivated -= _instance.OnDocumentToBeDeactivated;
            _instance._events.DocumentToBeDestroyed -= _instance.OnDocumentToBeDestroyed;
        }
        _instance = null;
    }

    private readonly AcadEventManager _events;
    private readonly DocumentStateStore _docStore = new();
    private StateMachine<HnState, HnEvent> _fsm;
    private string _currentDocKey;

    [ObservableProperty]
    private HydraulicNetwork? activeNetwork;

    partial void OnActiveNetworkChanged(HydraulicNetwork? value)
    {
        OnPropertyChanged(nameof(Graphs));
        OnPropertyChanged(nameof(AllFeatures));
        OnPropertyChanged(nameof(Features));
    }

    public HnState CurrentState => _fsm.CurrentState;

    public IEnumerable<UndirectedGraph<NodeJunction, EdgePipeSegment>>? Graphs =>
        ActiveNetwork?.Graphs;

    public IEnumerable<AnalysisFeature>? AllFeatures =>
        ActiveNetwork?.AllFeatures;

    public IEnumerable<IEnumerable<AnalysisFeature>>? Features =>
        ActiveNetwork?.Graphs.Select(g => g.Edges.Select(e => e.PipeSegment));

    public event EventHandler? NetworkLoaded;
    public event EventHandler? CalculationsFinished;
    public event EventHandler? ActiveNetworkChanged;

    private HydraulicNetworkManager(AcadEventManager events)
    {
        _events = events;
        _currentDocKey = DocumentStateStore.GetDocKey(AcAp.DocumentManager.MdiActiveDocument);
        var state = _docStore.GetOrCreate(_currentDocKey);
        state.Fsm = CreateFsm();
        _fsm = state.Fsm;

        _events.DocumentActivated += OnDocumentActivated;
        _events.DocumentToBeDeactivated += OnDocumentToBeDeactivated;
        _events.DocumentToBeDestroyed += OnDocumentToBeDestroyed;
    }

    private StateMachine<HnState, HnEvent> CreateFsm()
    {
        var fsm = new StateMachine<HnState, HnEvent>(HnState.Empty);

        fsm.Configure(HnState.Empty, HnEvent.NewCalc, HnState.Nascent, ctx =>
            ApplyNewNetwork(ctx));

        fsm.Configure(HnState.Nascent, HnEvent.NewCalc, HnState.Nascent, ctx =>
            ApplyNewNetwork(ctx));

        fsm.Configure(HnState.Nascent, HnEvent.StartCalc, HnState.Calculating, ctx =>
        {
            // TODO Phase 4: HydraulicSettingsService.Instance.IsLocked = true;
            ActiveNetwork!.Freeze(HydraulicSettingsService.Instance.Settings);
            ActiveNetworkChanged?.Invoke(this, EventArgs.Empty);
        });

        fsm.Configure(HnState.Calculating, HnEvent.CalcSuccess, HnState.Calculated, ctx =>
        {
            var duration = (TimeSpan)ctx.Payload!;
            var docState = _docStore.GetOrCreate(_currentDocKey);
            ActiveNetwork!.FinalizeCalculation(duration);
            ActiveNetwork.Id = docState.Counter.Next();
            docState.CalculatedNetworks.Add(ActiveNetwork);
            CalculationsFinished?.Invoke(this, EventArgs.Empty);
            ActiveNetworkChanged?.Invoke(this, EventArgs.Empty);
        });

        fsm.Configure(HnState.Calculating, HnEvent.CalcError, HnState.Nascent, ctx =>
        {
            ActiveNetwork?.ResetResults();
            // TODO Phase 4: HydraulicSettingsService.Instance.IsLocked = false;
            ActiveNetworkChanged?.Invoke(this, EventArgs.Empty);
        });

        fsm.Configure(HnState.Calculating, HnEvent.CalcCancel, HnState.Nascent, ctx =>
        {
            ActiveNetwork?.ResetResults();
            // TODO Phase 4: HydraulicSettingsService.Instance.IsLocked = false;
            ActiveNetworkChanged?.Invoke(this, EventArgs.Empty);
        });

        fsm.Configure(HnState.Calculated, HnEvent.NewCalc, HnState.Nascent, ctx =>
            ApplyNewNetwork(ctx));

        fsm.Configure(HnState.Calculated, HnEvent.LoadHn, HnState.Calculated, ctx =>
        {
            var hn = (HydraulicNetwork)ctx.Payload!;
            var docState = _docStore.GetOrCreate(_currentDocKey);
            docState.ActiveNetwork = hn;
            ActiveNetwork = hn;
            if (hn.FrozenSettings != null)
                HydraulicSettingsService.Instance.Settings.CopyFrom(hn.FrozenSettings);
            // TODO Phase 4: HydraulicSettingsService.Instance.IsLocked = true;
            CalculationsFinished?.Invoke(this, EventArgs.Empty);
            ActiveNetworkChanged?.Invoke(this, EventArgs.Empty);
        });

        return fsm;
    }

    private void ApplyNewNetwork(StateMachine<HnState, HnEvent>.TransitionContext ctx)
    {
        var hn = (HydraulicNetwork)ctx.Payload!;
        var docState = _docStore.GetOrCreate(_currentDocKey);
        docState.ActiveNetwork = hn;
        ActiveNetwork = hn;
        // TODO Phase 4: HydraulicSettingsService.Instance.IsLocked = false;
        NetworkLoaded?.Invoke(this, EventArgs.Empty);
        ActiveNetworkChanged?.Invoke(this, EventArgs.Empty);
    }

    public void NewCalculation(HydraulicNetwork hn)
    {
        _fsm.Fire(HnEvent.NewCalc, hn);
        OnPropertyChanged(nameof(CurrentState));
    }

    public void StartCalculation()
    {
        _fsm.Fire(HnEvent.StartCalc);
        OnPropertyChanged(nameof(CurrentState));
    }

    public void CalculationSucceeded(TimeSpan duration)
    {
        _fsm.Fire(HnEvent.CalcSuccess, duration);
        OnPropertyChanged(nameof(CurrentState));
    }

    public void CalculationFailed()
    {
        _fsm.Fire(HnEvent.CalcError);
        OnPropertyChanged(nameof(CurrentState));
    }

    public void CalculationCancelled()
    {
        _fsm.Fire(HnEvent.CalcCancel);
        OnPropertyChanged(nameof(CurrentState));
    }

    public void LoadHn(HydraulicNetwork hn)
    {
        _fsm.Fire(HnEvent.LoadHn, hn);
        OnPropertyChanged(nameof(CurrentState));
    }

    public List<HydraulicNetwork> GetCalculatedNetworks()
        => _docStore.GetOrCreate(_currentDocKey).CalculatedNetworks;

    public bool HasUnsavedNetworks()
        => _docStore.HasUnsavedNetworks(_currentDocKey);

    private void OnDocumentActivated(object sender, DocumentCollectionEventArgs e)
    {
        if (e.Document == null) return;
        _currentDocKey = DocumentStateStore.GetDocKey(e.Document);
        var docState = _docStore.GetOrCreate(_currentDocKey);
        if (docState.Fsm == null)
        {
            docState.Fsm = CreateFsm();
        }
        _fsm = docState.Fsm;
        ActiveNetwork = docState.ActiveNetwork;
        OnPropertyChanged(nameof(CurrentState));
        ActiveNetworkChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDocumentToBeDeactivated(object sender, DocumentCollectionEventArgs e)
    {
        if (e.Document == null) return;
        var docKey = DocumentStateStore.GetDocKey(e.Document);
        var docState = _docStore.GetOrCreate(docKey);
        docState.ActiveNetwork = ActiveNetwork;
    }

    private void OnDocumentToBeDestroyed(object sender, DocumentCollectionEventArgs e)
    {
        if (e.Document == null) return;
        var docKey = DocumentStateStore.GetDocKey(e.Document);
        // TODO Phase 11: prompt user about unsaved HNs before removing
        _docStore.Remove(docKey);
    }
}
