using Autodesk.AutoCAD.ApplicationServices;

namespace IntersectUtilities.MPE.PipePlanDE;

// Process-wide registry of per-document PipePlanDEState plus the single
// PipePlanDEPalette window. Mirrors PipePlanRuntime: state lives as long as its
// owning Document, and the app-scope palette is rebound to the active document's
// state on DocumentActivated.
internal static class PipePlanDERuntime
{
    private static readonly Dictionary<Document, PipePlanDEState> _states = new();
    private static PipePlanDEPalette? _palette;
    private static bool _subscribed;

    internal static PipePlanDEPalette Palette
    {
        get
        {
            EnsureSubscribed();
            return _palette ??= new PipePlanDEPalette();
        }
    }

    internal static PipePlanDEState StateFor(Document document)
    {
        EnsureSubscribed();
        if (!_states.TryGetValue(document, out PipePlanDEState? state))
        {
            state = new PipePlanDEState();
            _states[document] = state;
        }

        return state;
    }

    internal static void Reset()
    {
        if (_subscribed)
        {
            Application.DocumentManager.DocumentActivated -= OnDocumentActivated;
            Application.DocumentManager.DocumentToBeDestroyed -= OnDocumentToBeDestroyed;
            _subscribed = false;
        }

        foreach (PipePlanDEState state in _states.Values)
        {
            state.Dispose();
        }

        _states.Clear();

        _palette?.Dispose();
        _palette = null;
    }

    private static void EnsureSubscribed()
    {
        if (_subscribed) return;
        Application.DocumentManager.DocumentActivated += OnDocumentActivated;
        Application.DocumentManager.DocumentToBeDestroyed += OnDocumentToBeDestroyed;
        _subscribed = true;
    }

    private static void OnDocumentActivated(object? sender, DocumentCollectionEventArgs e)
    {
        if (e.Document is null) return;
        PipePlanDEState state = StateFor(e.Document);
        _palette?.RebindTo(state);
    }

    private static void OnDocumentToBeDestroyed(object? sender, DocumentCollectionEventArgs e)
    {
        if (e.Document is null) return;
        if (_states.TryGetValue(e.Document, out PipePlanDEState? state))
        {
            state.Dispose();
            _states.Remove(e.Document);
        }
    }
}
