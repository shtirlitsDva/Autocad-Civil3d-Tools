using Autodesk.AutoCAD.ApplicationServices;

using CommunityToolkit.Mvvm.ComponentModel;

using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Models;
using DimensioneringV2.Models.Nyttetimer;
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
    public static HydraulicNetworkManager Instance =>
        _instance ??= new HydraulicNetworkManager(
            Commands.Events ?? throw new InvalidOperationException(
                "HydraulicNetworkManager accessed before Commands.Initialize()"));

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
        LoadFromStorage(AcAp.DocumentManager.MdiActiveDocument, state);

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
            SetSettingsLocked(true);
            ActiveNetwork!.Freeze(HydraulicSettingsService.Instance.Settings);
            ActiveNetwork!.FrozenNyttetimerConfig = new NyttetimerConfigurationData(
                NyttetimerService.Instance.CurrentConfiguration);
            ActiveNetworkChanged?.Invoke(this, EventArgs.Empty);
        });

        fsm.Configure(HnState.Calculating, HnEvent.CalcSuccess, HnState.Calculated, ctx =>
        {
            var duration = (TimeSpan)ctx.Payload!;
            var docState = _docStore.GetOrCreate(_currentDocKey);
            ActiveNetwork!.FinalizeCalculation(duration);
            ActiveNetwork.Id = docState.Counter.Next();
            // Persist counter immediately so it survives crashes / "No" on save prompt
            var doc = AcAp.DocumentManager.MdiActiveDocument;
            HydraulicNetworkStorage.SaveCounter(doc, docState.Counter);
            docState.CalculatedNetworks.Add(ActiveNetwork);
            CalculationsFinished?.Invoke(this, EventArgs.Empty);
            ActiveNetworkChanged?.Invoke(this, EventArgs.Empty);
        });

        fsm.Configure(HnState.Calculating, HnEvent.CalcError, HnState.Nascent, ctx =>
        {
            ActiveNetwork?.ResetResults();
            SetSettingsLocked(false);
            ActiveNetworkChanged?.Invoke(this, EventArgs.Empty);
        });

        fsm.Configure(HnState.Calculating, HnEvent.CalcCancel, HnState.Nascent, ctx =>
        {
            ActiveNetwork?.ResetResults();
            SetSettingsLocked(false);
            ActiveNetworkChanged?.Invoke(this, EventArgs.Empty);
        });

        fsm.Configure(HnState.Calculated, HnEvent.NewCalc, HnState.Nascent, ctx =>
            ApplyNewNetwork(ctx));

        fsm.Configure(HnState.Empty, HnEvent.LoadHn, HnState.Calculated, ctx =>
            ApplyLoadedNetwork(ctx));

        fsm.Configure(HnState.Nascent, HnEvent.LoadHn, HnState.Calculated, ctx =>
            ApplyLoadedNetwork(ctx));

        fsm.Configure(HnState.Calculated, HnEvent.LoadHn, HnState.Calculated, ctx =>
            ApplyLoadedNetwork(ctx));

        return fsm;
    }

    private void ApplyNewNetwork(StateMachine<HnState, HnEvent>.TransitionContext ctx)
    {
        var hn = (HydraulicNetwork)ctx.Payload!;
        var docState = _docStore.GetOrCreate(_currentDocKey);
        docState.ActiveNetwork = hn;
        ActiveNetwork = hn;
        SetSettingsLocked(false);
        NetworkLoaded?.Invoke(this, EventArgs.Empty);
        ActiveNetworkChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyLoadedNetwork(StateMachine<HnState, HnEvent>.TransitionContext ctx)
    {
        var hn = (HydraulicNetwork)ctx.Payload!;
        var docState = _docStore.GetOrCreate(_currentDocKey);
        docState.ActiveNetwork = hn;
        if (!docState.CalculatedNetworks.Contains(hn))
            docState.CalculatedNetworks.Add(hn);
        ActiveNetwork = hn;
        if (hn.FrozenSettings != null)
            HydraulicSettingsService.Instance.Settings.CopyFrom(hn.FrozenSettings);
        if (hn.FrozenNyttetimerConfig != null)
        {
            var name = NyttetimerService.Instance.ReconcileImportedConfig(hn.FrozenNyttetimerConfig);
            NyttetimerService.Instance.SelectConfiguration(name);
        }
        SetSettingsLocked(true);

        // Fire NetworkLoaded (not CalculationsFinished) — loading a saved HN
        // is semantically "network loaded", which creates _themeManager and
        // calls CreateMapFirstTime() via OnDataLoaded().
        NetworkLoaded?.Invoke(this, EventArgs.Empty);
        ActiveNetworkChanged?.Invoke(this, EventArgs.Empty);

        // Restore BBR AFTER map creation so BBR layers are added on top
        // (CreateMapFirstTime calls Mymap.Layers.Clear()).
        if (hn.BbrFeatures != null && hn.BbrFeatures.Count > 0)
            BBRLayerService.Instance.RestoreFeatures(hn.BbrFeatures);
    }

    private static void SetSettingsLocked(bool locked)
    {
        HydraulicSettingsService.Instance.IsLocked = locked;
        NyttetimerService.Instance.IsLocked = locked;
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

    public void AddNetwork(HydraulicNetwork hn)
    {
        var docState = _docStore.GetOrCreate(_currentDocKey);
        if (hn.Id == null) hn.Id = docState.Counter.Next();
        if (!docState.CalculatedNetworks.Contains(hn))
            docState.CalculatedNetworks.Add(hn);
        CalculationsFinished?.Invoke(this, EventArgs.Empty);
    }

    public List<HydraulicNetwork> GetCalculatedNetworks()
        => _docStore.GetOrCreate(_currentDocKey).CalculatedNetworks;

    public void RemoveNetwork(HydraulicNetwork hn)
    {
        var docState = _docStore.GetOrCreate(_currentDocKey);
        docState.CalculatedNetworks.Remove(hn);

        if (ActiveNetwork == hn)
        {
            var fallback = docState.CalculatedNetworks.LastOrDefault();
            if (fallback != null)
            {
                LoadHn(fallback);
            }
            else
            {
                ActiveNetwork = null;
                docState.ActiveNetwork = null;
                docState.Fsm = CreateFsm();
                _fsm = docState.Fsm;
                SetSettingsLocked(false);
                OnPropertyChanged(nameof(CurrentState));
                ActiveNetworkChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool HasUnsavedNetworks()
        => _docStore.HasUnsavedNetworks(_currentDocKey);

    private void LoadFromStorage(Document? doc, DocumentHnState state)
    {
        if (state.StorageLoaded || doc == null) return;
        state.StorageLoaded = true;
        try
        {
            state.Counter = HydraulicNetworkStorage.LoadCounter(doc);
            var savedIds = HydraulicNetworkStorage.GetSavedIds(doc);
            var existingIds = new HashSet<string>(
                state.CalculatedNetworks
                    .Where(hn => hn.Id != null)
                    .Select(hn => hn.Id!));

            var failedIds = new List<string>();

            foreach (var id in savedIds)
            {
                if (existingIds.Contains(id)) continue;
                var (hn, deserializationFailed) = HydraulicNetworkStorage.Load(doc, id);
                if (hn != null)
                {
                    state.CalculatedNetworks.Add(hn);
                }
                else if (deserializationFailed)
                {
                    failedIds.Add(id);
                }
            }

            if (failedIds.Count > 0)
            {
                var idList = string.Join(", ", failedIds);
                var result = System.Windows.MessageBox.Show(
                    $"Gamle inkompatible beregningsresultater fundet.\n\n" +
                    $"Berørte beregninger: {idList}\n\n" +
                    $"Vil du slette de inkompatible data?\n" +
                    $"Ja = Slet, Nej = Afbryd (data beholdes men indlæses ikke)",
                    "Inkompatible data",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning,
                    System.Windows.MessageBoxResult.No);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    using var docLock = doc.LockDocument();
                    foreach (var id in failedIds)
                    {
                        Norsyn.Storage.NorsynStorage.Remove("dimv2:hn:" + id);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Utils.prtDbg($"Error loading from storage: {ex.Message}");
        }
    }

    private void OnDocumentActivated(object sender, DocumentCollectionEventArgs e)
    {
        try
        {
            if (e.Document == null) return;
            _currentDocKey = DocumentStateStore.GetDocKey(e.Document);
            var docState = _docStore.GetOrCreate(_currentDocKey);
            if (docState.Fsm == null)
            {
                docState.Fsm = CreateFsm();
            }
            _fsm = docState.Fsm;
            LoadFromStorage(e.Document, docState);
            ActiveNetwork = docState.ActiveNetwork;
            OnPropertyChanged(nameof(CurrentState));
            ActiveNetworkChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Utils.prtDbg($"Error in OnDocumentActivated: {ex.Message}");
        }
    }

    private void OnDocumentToBeDeactivated(object sender, DocumentCollectionEventArgs e)
    {
        try
        {
            if (e.Document == null) return;
            var docKey = DocumentStateStore.GetDocKey(e.Document);
            var docState = _docStore.GetOrCreate(docKey);
            docState.ActiveNetwork = ActiveNetwork;
        }
        catch (Exception ex)
        {
            Utils.prtDbg($"Error in OnDocumentToBeDeactivated: {ex.Message}");
        }
    }

    private void OnDocumentToBeDestroyed(object sender, DocumentCollectionEventArgs e)
    {
        try
        {
            if (e.Document == null) return;
            var docKey = DocumentStateStore.GetDocKey(e.Document);
            var state = _docStore.GetOrCreate(docKey);

            if (_docStore.HasUnsavedNetworks(docKey))
            {
                var unsaved = state.CalculatedNetworks.Where(hn => !hn.IsSaved).ToList();
                var names = string.Join(", ", unsaved.Select(hn => hn.Id ?? "?"));

                var result = System.Windows.MessageBox.Show(
                    $"Du har {unsaved.Count} ikke-gemte beregning(er): {names}.\nGem i tegningen?",
                    "Ikke-gemte beregninger",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    foreach (var hn in unsaved)
                        HydraulicNetworkStorage.Save(e.Document, hn);
                }
            }

            // Always persist counter on document close, regardless of unsaved networks
            HydraulicNetworkStorage.SaveCounter(e.Document, state.Counter);

            _docStore.Remove(docKey);
        }
        catch (Exception ex)
        {
            Utils.prtDbg($"Error in OnDocumentToBeDestroyed: {ex.Message}");
        }
    }
}
