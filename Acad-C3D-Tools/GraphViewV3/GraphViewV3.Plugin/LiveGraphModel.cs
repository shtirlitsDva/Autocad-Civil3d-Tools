using System.Windows.Threading;

using Autodesk.AutoCAD.DatabaseServices;

using GraphViewV3.Core;

using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace GraphViewV3;

/// <summary>
/// The live update loop. A dirty flag is set ONLY when an FJV-relevant object changes in the
/// active drawing (via AcadEventManager's active-document Database events). On idle — throttled
/// so a burst of edits coalesces — if dirty, it reads a fast DTO snapshot on the main thread,
/// skips when the content hash is unchanged, otherwise builds the graph + stats on a BACKGROUND
/// thread (Core is pure CPU) and marshals the result back to the WPF thread.
/// </summary>
internal sealed class LiveGraphModel : IDisposable
{
    private readonly GraphViewModel _vm;
    private readonly Dispatcher _dispatcher;
    private NetworkGraphService _service;
    private double _tolerance;

    private EventHandler? _idleHandler;
    private ObjectEventHandler? _appendedHandler;
    private ObjectEventHandler? _modifiedHandler;
    private ObjectErasedEventHandler? _erasedHandler;

    private DateTime _lastRun = DateTime.MinValue;
    private long _lastHash = unchecked((long)0xDEADBEEF);
    private volatile bool _dirty = true;     // populate on first idle
    private volatile bool _forceRebuild;     // bypass hash skip (e.g. tolerance changed)
    private int _building;                    // 0/1 via Interlocked
    private bool _disposed;

    public TimeSpan MinInterval { get; init; } = TimeSpan.FromMilliseconds(750);

    public LiveGraphModel(GraphViewModel vm, Dispatcher dispatcher, double tolerance = 0.5)
    {
        _vm = vm;
        _dispatcher = dispatcher;
        _tolerance = tolerance;
        _service = new NetworkGraphService(tolerance);
    }

    public void Start()
    {
        var events = GraphViewV3Plugin.Events;
        if (events == null) return;

        _idleHandler = OnIdle;
        events.Idle += _idleHandler;

        // Dirty only on FJV-relevant changes — unrelated edits never trigger a rebuild.
        _appendedHandler = (_, e) => MarkIfRelevant(e.DBObject);
        _modifiedHandler = (_, e) => MarkIfRelevant(e.DBObject);
        _erasedHandler = (_, e) => MarkIfRelevant(e.DBObject);
        events.ActiveObjectAppended += _appendedHandler;
        events.ActiveObjectModified += _modifiedHandler;
        events.ActiveObjectErased += _erasedHandler;
    }

    /// <summary>Change the connection tolerance and force a rebuild (settings panel).</summary>
    public void SetTolerance(double tolerance)
    {
        if (Math.Abs(tolerance - _tolerance) < 1e-9) return;
        _tolerance = tolerance;
        _service = new NetworkGraphService(tolerance);
        _forceRebuild = true;
        _dirty = true;
    }

    private void MarkIfRelevant(DBObject? o)
    {
        if (o == null) { _dirty = true; return; }
        try
        {
            _dirty |= o switch
            {
                Polyline pl => FjvLayer.IsFjv(pl.Layer),
                BlockReference => true,
                _ => false,
            };
        }
        catch { _dirty = true; } // when unsure, rebuild — the hash check guards the cost
    }

    private void OnIdle(object? sender, EventArgs e)
    {
        if (_disposed || !_dirty) return;
        var now = DateTime.UtcNow;
        if (now - _lastRun < MinInterval) return;
        _lastRun = now;
        _dirty = false;

        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return;
        if (_building != 0) return;

        NetworkSnapshot snapshot;
        try
        {
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                snapshot = SnapshotReader.Read(doc.Database, tr);
                tr.Commit();
            }
        }
        catch { return; }

        if (!_forceRebuild && snapshot.ContentHash == _lastHash) return;
        _forceRebuild = false;
        _lastHash = snapshot.ContentHash;

        if (System.Threading.Interlocked.CompareExchange(ref _building, 1, 0) != 0) return;
        var service = _service;
        System.Threading.Tasks.Task.Run(() =>
        {
            NetworkResult result;
            try { result = service.Build(snapshot); }
            catch { result = NetworkResult.Empty; }

            _dispatcher.BeginInvoke(() =>
            {
                if (!_disposed)
                {
                    var s = result.Stats;
                    int errors = result.Graph.Edges.Count(ed => ed.IsError);
                    string qa = errors > 0 ? $" · ⚠ {errors} QA" : "";
                    _vm.Set(result,
                        $"{s.PipeCount} pipes · {s.ComponentCount} components · " +
                        $"{result.Graph.Components.Count} groups · {s.TotalLength:N0} m{qa}");
                }
                System.Threading.Interlocked.Exchange(ref _building, 0);
            });
        });
    }

    public void Dispose()
    {
        _disposed = true;
        var events = GraphViewV3Plugin.Events;
        if (events != null)
        {
            if (_idleHandler != null) events.Idle -= _idleHandler;
            if (_appendedHandler != null) events.ActiveObjectAppended -= _appendedHandler;
            if (_modifiedHandler != null) events.ActiveObjectModified -= _modifiedHandler;
            if (_erasedHandler != null) events.ActiveObjectErased -= _erasedHandler;
        }
        _idleHandler = null;
        _appendedHandler = null;
        _modifiedHandler = null;
        _erasedHandler = null;
    }
}
