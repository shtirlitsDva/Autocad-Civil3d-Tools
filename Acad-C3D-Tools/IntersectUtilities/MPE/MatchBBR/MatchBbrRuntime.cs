using Autodesk.AutoCAD.ApplicationServices;

namespace IntersectUtilities.MPE.MatchBBR
{
    // Process-wide holder for the single MatchBBR palette and the current per-document state.
    //
    // Like LERConnectNetworkRuntime this deliberately does NOT subscribe to DocumentManager
    // events: there is no IExtensionApplication hook reachable from the MPE scope to unsubscribe
    // on unload, so instead every operation validates against MdiActiveDocument and the command
    // re-binds on each invocation. The palette itself is app-scoped and survives drawing switches
    // by AutoCAD's design.
    internal static class MatchBbrRuntime
    {
        private static MatchBbrPalette? _palette;
        private static MatchBbrState? _state;

        internal static MatchBbrPalette Palette => _palette ??= new MatchBbrPalette();

        internal static MatchBbrState StateFor(Document document)
        {
            if (_state == null || _state.Owner != document)
            {
                _state?.Dispose();
                _state = new MatchBbrState(document);
            }

            return _state;
        }

        // Called from IExtensionApplication.Terminate so the palette and current state are
        // disposed on unload and do not survive an unload/reload cycle as an orphaned window.
        internal static void Reset()
        {
            _state?.Dispose();
            _state = null;
            _palette?.Dispose();
            _palette = null;
        }
    }
}
