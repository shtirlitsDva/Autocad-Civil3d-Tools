using Autodesk.AutoCAD.ApplicationServices;

namespace IntersectUtilities.MPE.PipePlanDE;

// Process-wide registry of per-document PipePlanDEState plus the two app-scope
// palettes (PDSETTINGS table editor and PDDRAW size picker). Mirrors
// PipePlanRuntime: state lives as long as its owning Document, and both palettes
// are rebound to the active document's state on DocumentActivated.
internal static class PipePlanDERuntime
{
    private static readonly Dictionary<Document, PipePlanDEState> _states = new();
    private static PipePlanDESettingsPalette? _settingsPalette;
    private static PipePlanDESizePalette? _sizePalette;
    private static bool _subscribed;

    // The table/diagram editor, shown by PDSETTINGS.
    internal static PipePlanDESettingsPalette SettingsPalette
    {
        get
        {
            EnsureSubscribed();
            return _settingsPalette ??= new PipePlanDESettingsPalette();
        }
    }

    // The DN picker, shown by PDDRAW.
    internal static PipePlanDESizePalette SizePalette
    {
        get
        {
            EnsureSubscribed();
            return _sizePalette ??= new PipePlanDESizePalette();
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

        _settingsPalette?.Dispose();
        _settingsPalette = null;
        _sizePalette?.Dispose();
        _sizePalette = null;
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
        _settingsPalette?.RebindTo(state);
        _sizePalette?.RebindTo(state);
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
