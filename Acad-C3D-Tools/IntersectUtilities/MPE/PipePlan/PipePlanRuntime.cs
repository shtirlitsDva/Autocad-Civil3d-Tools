using Autodesk.AutoCAD.ApplicationServices;

namespace IntersectUtilities.MPE.PipePlan;

// Process-wide registry of per-document PipePlanState instances plus the single
// PipePlanPalette window. State entries live as long as their owning Document.
// The palette survives drawing switches (AutoCAD palettes are app-scope by design)
// and is rebound to the active document's state on DocumentActivated.
internal static class PipePlanRuntime
{
    private static readonly Dictionary<Document, PipePlanState> _states = new();
    private static PipePlanPalette? _palette;
    private static bool _subscribed;

    internal static PipePlanPalette Palette
    {
        get
        {
            EnsureSubscribed();
            return _palette ??= new PipePlanPalette();
        }
    }

    // Forwards a status update to the palette only if it has already been
    // created. Avoids auto-creating the palette as a side effect of routine
    // PipePlanState.SetStatus calls inside a draw loop.
    internal static void NotifyPaletteStatus(string message, PipePlanStatusKind kind)
    {
        _palette?.SetStatus(message, kind);
    }

    internal static PipePlanState StateFor(Document document)
    {
        EnsureSubscribed();
        if (!_states.TryGetValue(document, out PipePlanState? state))
        {
            state = new PipePlanState(document);
            _states[document] = state;
        }
        return state;
    }

    // Called from IExtensionApplication.Terminate so DocumentManager event handlers
    // don't fire into a no-longer-loaded module after the plugin is unloaded.
    internal static void Reset()
    {
        if (_subscribed)
        {
            Application.DocumentManager.DocumentActivated -= OnDocumentActivated;
            Application.DocumentManager.DocumentToBeDestroyed -= OnDocumentToBeDestroyed;
            _subscribed = false;
        }

        foreach (PipePlanState state in _states.Values)
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
        PipePlanState state = StateFor(e.Document);
        _palette?.RebindTo(state);
    }

    private static void OnDocumentToBeDestroyed(object? sender, DocumentCollectionEventArgs e)
    {
        if (e.Document is null) return;
        if (_states.TryGetValue(e.Document, out PipePlanState? state))
        {
            state.Dispose();
            _states.Remove(e.Document);
        }
    }
}
